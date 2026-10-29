using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Toddlers;

namespace ToddlersPlayExpanded
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        /// <summary>
        /// ThoughtDefs this mod deliberately REMOVED. A save in which a pawn still holds
        /// one of these memories would otherwise log a red "Could not load reference to
        /// ThoughtDef named X" error on load (ScribeExtractor.DefFromNode only stays
        /// silent for names in vanilla's BackCompatibility removed-defs list). Adding the
        /// names there makes the loader drop the memory silently: the def resolves to
        /// null and MemoryThoughtHandler.ExposeData discards null-def memories in
        /// PostLoadInit — the exact path vanilla uses for its own removed defs.
        /// • TPE_Event_SolarFlare — retired (user request); covered by the power outage
        ///   mood. See Architecture.md §5.2.
        /// </summary>
        private static readonly string[] RetiredThoughtDefNames = { "TPE_Event_SolarFlare" };

        /// <summary>
        /// Registers <see cref="RetiredThoughtDefNames"/> in vanilla's private
        /// <c>BackCompatibility.RemovedDefs</c> list (a static readonly List — the field
        /// is readonly, its contents are not). Reflection, once, at startup. If the field
        /// ever disappears the only consequence is the cosmetic red error above, so this
        /// warns and moves on instead of throwing.
        /// </summary>
        private static void RegisterRetiredDefs()
        {
            try
            {
                if (!(AccessTools.Field(typeof(BackCompatibility), "RemovedDefs")?.GetValue(null) is List<Tuple<string, Type>> removed))
                {
                    Log.Warning("[TPE] BackCompatibility.RemovedDefs not found — saves holding a retired toddler mood may log one harmless 'Could not load reference' error.");
                    return;
                }
                for (int i = 0; i < RetiredThoughtDefNames.Length; i++)
                    removed.Add(new Tuple<string, Type>(RetiredThoughtDefNames[i], typeof(ThoughtDef)));
            }
            catch (Exception ex)
            {
                Log.Warning($"[TPE] Could not register retired ThoughtDefs for save compatibility: {ex.Message}");
            }
        }

        static HarmonyPatches()
        {
            var harmony = new Harmony("drati.toddlersplayexpanded");
            harmony.PatchAll();

            // Save compatibility for ThoughtDefs this mod has retired — must run
            // before any save is loaded, which a [StaticConstructorOnStartup] cctor does.
            RegisterRetiredDefs();

            // BULLETPROOF: Force correct developmentalStageFilter on all plush toys.
            // XML <developmentalStageFilter>Baby, Child</developmentalStageFilter> is
            // somehow resolving to just Child at runtime (observed in test run #8).
            // This startup hook ensures the bitmask is exactly Baby | Child | Adult
            // regardless of XML parsing quirks or inheritance issues.
            //
            // BUGFIX (live-test run #2, this session): this hardcoded value was still
            // Baby | Child ONLY, even after the plush ThingDefs' XML was separately
            // widened to "Baby, Child, Adult" so adults could equip them at all. Since
            // this startup hook runs AFTER Defs finish loading and unconditionally
            // OVERWRITES whatever developmentalStageFilter the XML resolved to, the
            // XML edit was completely inert for this specific field — every adult's
            // equip attempt failed ApparelProperties.PawnCanWear's
            // "developmentalStageFilter.Has(DevelopmentalStage.Adult)" check and got
            // silently dropped (surfaced via JobDriver_SeekPlushToy's debug-logging
            // branch: "cannot wear TPE_PlushTeddy (filter: Baby, Child)"). Both the
            // XML and this hardcoded fallback must be kept in sync going forward —
            // if a future dev widens one without the other, this exact bug recurs.
            // SECOND PURPOSE OF THIS SAME LOOP: pre-resolve and cache each plush's
            // plain "dropped on the ground" Graphic (ThingDef.graphicData.Graphic, a
            // non-body-type Graphic_Single) into TPE_ToyUtility.PlushDecorativeGraphics
            // for the sleeping-plush head-position renderer
            // (Patch_PawnRenderer_RenderPawnAt_DrawSleepingPlush, below) to read at
            // render time. Doing this HERE — once, at startup, on the main thread, in
            // the exact same pass that's already iterating every TPE_Plush* def —
            // means the render hook itself never has to resolve or cache anything at
            // runtime; it's just a dictionary read. See that field's own doc comment
            // in TPE_ToyUtility.cs for why this matters: a previous version of the
            // sleeping-plush renderer resolved the WORN (body-type-specific) graphic
            // on demand via ApparelGraphicRecordGetter/GraphicDatabase directly from
            // the render hook and crashed with "concurrent collection" exceptions,
            // which is why that whole feature was disabled for a long time. Using the
            // plain, pre-resolved graphic instead sidesteps that API entirely.
            DevelopmentalStage correctFilter = DevelopmentalStage.Baby | DevelopmentalStage.Child | DevelopmentalStage.Adult;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.defName != null && def.defName.StartsWith("TPE_Plush") && def.apparel != null)
                {
                    // Capture the pre-overwrite value BEFORE mutating the field — logging
                    // def.apparel.developmentalStageFilter AFTER the assignment below would
                    // always print the new value, making the "(was X)" diagnostic useless
                    // for ever catching a future XML/hardcode mismatch like this one.
                    DevelopmentalStage before = def.apparel.developmentalStageFilter;
                    def.apparel.developmentalStageFilter = correctFilter;
                    if (TPE_Mod.Settings?.debugLogging ?? false)
                        Log.Message($"[TPE] Startup fix applied to {def.defName}: developmentalStageFilter = {correctFilter} (was {before}).");

                    // Pre-resolve the plain map/dropped graphic now (main thread,
                    // startup) so the render hook never needs to touch GraphicDatabase
                    // itself. def.graphicData.Graphic is a simple, non-apparel,
                    // non-body-type Graphic_Single built from <texPath> — completely
                    // independent of the WORN apparel graphic (which needs a body type
                    // and goes through the heavier ApparelGraphicRecordGetter path).
                    if (def.graphicData != null)
                    {
                        TPE_ToyUtility.PlushDecorativeGraphics[def] = def.graphicData.Graphic;
                    }
                }
            }

            // Optional compat: "Intimacy - Homely" (walk-to-wardrobe undress
            // ritual before bed). Fully inert no-op when that mod isn't
            // installed — see HomelyCompat for details.
            HomelyCompat.Initialize();
        }
    }


    // =========================================================================
    // COMPREHENSIVE TV BLOCKING FOR CRIB-LOCKED TODDLERS
    // =========================================================================
    // A locked toddler must NEVER watch television, regardless of whether they
    // are physically in the crib or temporarily outside it (feeding, rescue,
    // etc.). The base mod assigns TV watching through TWO parallel paths:
    //
    //   1. In-crib  → JobGiver_ToddlerPlayInCrib → CanDoFromCrib / TryGiveJobFromCrib
    //   2. Out-of-crib → JobGiver_ToddlerPlay → CanDo / TryGiveJob
    //
    // Both paths are siblings under ThinkNode_ConditionalToddler. The think tree
    // falls through from the in-crib branch to the out-of-crib branch when the
    // in-crib giver returns null. Therefore blocking ONLY the in-crib path is
    // insufficient — the out-of-crib path will assign the TV job on the very
    // next think-node evaluation.
    //
    // Eligibility prefixes avoid television searches for locked toddlers; job
    // creation prefixes also cover direct callers that do not ask CanDo first.
    // =========================================================================

    /// <summary>
    /// Layer 1: Block in-crib TV eligibility.
    /// CanDoFromCrib returns false if the toddler is crib-locked, preventing
    /// JobGiver_ToddlerPlayInCrib from ever calling TryGiveJobFromCrib.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ToddlerPlayGiver_WatchTelevision_CanDoFromCrib
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.ToddlerPlayGiver_WatchTelevision");
            return AccessTools.Method(type, "CanDoFromCrib", new System.Type[] { typeof(Pawn) });
        }

        static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!CribLockUtility.IsCribLocked(pawn)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Layer 2: Block out-of-crib TV eligibility.
    /// CanDo returns false if the toddler is crib-locked, preventing
    /// JobGiver_ToddlerPlay from ever considering TV watching.
    /// THIS WAS THE MISSING PIECE — the think tree falls through from the
    /// in-crib giver to the out-of-crib giver when the former returns null.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ToddlerPlayGiver_WatchTelevision_CanDo
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.ToddlerPlayGiver_WatchTelevision");
            return AccessTools.Method(type, "CanDo", new System.Type[] { typeof(Pawn) });
        }

        static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!CribLockUtility.IsCribLocked(pawn)) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Layer 3: Block in-crib job creation.
    /// Defensive prefix — if CanDoFromCrib is somehow bypassed (e.g. JIT
    /// inlining), this catches the job before it is returned.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ToddlerPlayGiver_WatchTelevision_TryGiveJobFromCrib
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.ToddlerPlayGiver_WatchTelevision");
            return AccessTools.Method(type, "TryGiveJobFromCrib", new System.Type[] { typeof(Pawn) });
        }

        static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (CribLockUtility.IsCribLocked(pawn))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Layer 4: Block out-of-crib job creation.
    /// Defensive prefix — if CanDo is somehow bypassed, this catches the job
    /// before it is returned.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ToddlerPlayGiver_WatchTelevision_TryGiveJob
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.ToddlerPlayGiver_WatchTelevision");
            return AccessTools.Method(type, "TryGiveJob", new System.Type[] { typeof(Pawn) });
        }

        static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (CribLockUtility.IsCribLocked(pawn))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }


    // Patch ToddlerPlayUtility.ToddlerPlayTickCheckEnd — never end job just because play need is full
    [HarmonyPatch(typeof(ToddlerPlayUtility), "ToddlerPlayTickCheckEnd")]
    public static class Patch_ToddlerPlayUtility_ToddlerPlayTickCheckEnd
    {
        public static bool Prefix(Pawn pawn, int delta = 1)
        {
            if (pawn.needs?.play == null) return false;
            
            // Apply play gain up to max (engine caps it at 1.0)
            if (pawn.needs.play.CurLevel <= ToddlerPlayUtility.GetMaxPlay(pawn))
            {
                pawn.needs.play.Play((float)delta * 0.00012f * BabyPlayUtility.GetRoomPlayGainFactors(pawn));
            }
            
            // Do NOT end the job just because play need is full.
            // The job ends naturally via its duration in the JobDriver.
            return false;
        }
    }

    // Protect our spinning animation from being overridden by SetLocomotionAnimation
    [HarmonyPatch]
    public static class Patch_AnimationUtility_SetLocomotionAnimation
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.AnimationUtility");
            return AccessTools.Method(type, "SetLocomotionAnimation");
        }

        public static bool Prefix(Pawn pawn, AnimationDef animation)
        {
            if (pawn?.Drawer?.renderer == null) return true;
            var cur = pawn.Drawer.renderer.CurAnimation;
            if (cur == TPE_AnimationDefOf.TPE_ToddlerSpinning)
            {
                // Don't let locomotion animation override our spinning animation
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Read-only access to the base Toddlers mod's walking-wobble values for the toddler
    /// hop sway in <see cref="Patch_PawnRenderer_GetBodyPos"/>. <c>Hediff_LearningToWalk</c>
    /// is <c>internal</c> to Toddlers.dll, so we can't cast to it. This used to call
    /// <c>Traverse.Create(h).Property(...)</c> twice per FRAME per jumping toddler — a
    /// reflection lookup on the parallel render thread. Now: compiled field-ref delegates
    /// to the two private backing fields its <c>WobbleMagnitude</c>/<c>WobblePeriod</c>
    /// properties return verbatim (verified by decompiling Toddlers.dll), resolved ONCE.
    ///
    /// <c>[StaticConstructorOnStartup]</c> ⇒ the static ctor runs on the main thread during
    /// load, never lazily on a render worker. It never throws (that would poison the type
    /// with a TypeInitializationException): if Toddlers renames anything, it logs one
    /// warning and <see cref="TryGet"/> just reports "no wobble" (the hop itself still works).
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ToddlerWobbleAccess
    {
        /// <summary>Runtime type of Toddlers' internal <c>Hediff_LearningToWalk</c>; null if not found.</summary>
        static readonly Type? LearningToWalkType;

        /// <summary>Getter for <c>Hediff_LearningToWalk.cachedWobbleMagnitude</c> (degrees).</summary>
        static readonly AccessTools.FieldRef<Hediff, float>? MagnitudeRef;

        /// <summary>Getter for <c>Hediff_LearningToWalk.cachedWobblePeriod</c> (ticks).</summary>
        static readonly AccessTools.FieldRef<Hediff, int>? PeriodRef;

        static ToddlerWobbleAccess()
        {
            try
            {
                Type? type = AccessTools.TypeByName("Toddlers.Hediff_LearningToWalk");
                FieldInfo? magnitude = type == null ? null : AccessTools.Field(type, "cachedWobbleMagnitude");
                FieldInfo? period = type == null ? null : AccessTools.Field(type, "cachedWobblePeriod");
                if (type == null || magnitude == null || period == null
                    || magnitude.FieldType != typeof(float) || period.FieldType != typeof(int))
                {
                    Log.Warning("[TPE] Toddlers' Hediff_LearningToWalk wobble fields not found — the jump sway is disabled (the hop itself still works).");
                    return;
                }
                // T = Hediff (a parent of the declaring type) is explicitly allowed by
                // Harmony's FieldRefAccess<T,F>(FieldInfo); TryGet only ever passes an
                // instance that IS a Hediff_LearningToWalk.
                MagnitudeRef = AccessTools.FieldRefAccess<Hediff, float>(magnitude);
                PeriodRef = AccessTools.FieldRefAccess<Hediff, int>(period);
                LearningToWalkType = type;
            }
            catch (Exception e)
            {
                MagnitudeRef = null;
                PeriodRef = null;
                LearningToWalkType = null;
                Log.Warning("[TPE] Could not bind Toddlers' walking-wobble fields — the jump sway is disabled (the hop itself still works): " + e.Message);
            }
        }

        /// <summary>
        /// Reads the current wobble magnitude (degrees) and period (ticks) from a
        /// LearningToWalk hediff. False when unavailable or the period is unusable
        /// (≤ 0 would divide by zero in the caller). Allocation-free and reflection-free;
        /// safe on render worker threads (pure reads of fields the main thread only
        /// writes during ticks, which never overlap the draw phase).
        /// </summary>
        internal static bool TryGet(Hediff hediff, out float magnitude, out int period)
        {
            magnitude = 0f;
            period = 0;
            if (LearningToWalkType == null || MagnitudeRef == null || PeriodRef == null)
                return false;
            // Guard the cast the field-ref performs (another mod could swap hediffClass).
            if (!LearningToWalkType.IsInstanceOfType(hediff))
                return false;
            magnitude = MagnitudeRef(hediff);
            period = PeriodRef(hediff);
            return period > 0;
        }
    }

    // Vertical jump offset applied via PawnRenderer.GetBodyPos postfix.
    //
    // CRITICAL: RimWorld's isometric camera maps world Z to screen vertical.
    // PawnFlyer adds Vector3.forward (Z+) for visible height; Yayo's offsets
    // are (x, 0, z) with Y hardcoded to zero.  Adding to Y only changes
    // altitude/depth sorting — it does NOT move the pawn up on screen.
    // We add to Z (and a tiny Y for proper sorting) so the body rises above
    // its shadow while the shadow stays on the ground.
    //
    // PERFORMANCE: GetBodyPos runs for EVERY rendered pawn EVERY frame, on the
    // parallel pre-draw threads (PawnRenderer.ParallelGetPreRenderResults). The
    // postfix therefore bails on the cheapest checks first (posture parameter, then
    // two type tests / one reference compare) so non-jumping pawns pay ~nothing, and
    // the hop math is O(1) (see ApplyToddlerHop) — it used to walk every hop since
    // tick 0, i.e. ~75k loop iterations per frame per jumping toddler after one
    // in-game year, growing forever.
    //
    // SETTINGS: both bounces are "play animations" — the toddler hop is gated on
    // PlayAnimationUtility.ToddlerAnimationsDisabled (this patch is the ONLY gate for
    // it: toddler Jumping / Crib Jumping set no AnimationDef), the child hop on
    // ChildAnimationsDisabled (its driver also stops setting the marker animation).
    // Read here every frame, so a flip takes effect on the next frame, even paused.
    [HarmonyPatch]
    public static class Patch_PawnRenderer_GetBodyPos
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PawnRenderer), "GetBodyPos");
        }

        // ── Toddler hop timing (values unchanged from the original loop) ──
        /// <summary>Ticks of one up-and-down hop arc.</summary>
        const int JumpTicks = 30;
        /// <summary>Shortest landing pause between hops (ticks). Pauses are 10–25.</summary>
        const int MinPauseTicks = 10;
        /// <summary>Number of distinct pause lengths (10..25) — the pause hash is "% 16".</summary>
        const int PauseVariants = 16;
        /// <summary>
        /// The per-hop pause is <c>10 + (hopIndex*31 + seed*17) % 16</c>. Because
        /// 31·16 ≡ 0 (mod 16) the sequence repeats every 16 hops, and because 31 ≡ 15 is
        /// coprime with 16, each 16-hop block uses every pause 10..25 exactly once.
        /// </summary>
        const int HopsPerPattern = PauseVariants;
        /// <summary>
        /// Length of one repeating 16-hop block: 16·30 hop ticks + (10+11+…+25) pause
        /// ticks = 480 + 280 = 760. Lets us jump straight to the right block with one
        /// modulo instead of walking every hop since tick 0.
        /// </summary>
        const int HopPatternTicks = HopsPerPattern * (JumpTicks + MinPauseTicks)
                                    + PauseVariants * (PauseVariants - 1) / 2;
        /// <summary>Peak toddler hop height in world Z (screen-up).</summary>
        const float ToddlerHopHeight = 0.4f; // halved from 0.8f

        // ── Child hop ──
        /// <summary>Peak child hop height in world Z (screen-up).</summary>
        const float ChildHopHeight = 0.5f;
        /// <summary>
        /// Period of the sine driving the child hop, in ticks. The bounce is |sin|, so the
        /// child completes one hop every half period (15 ticks). Unchanged from the original.
        /// </summary>
        const float ChildHopCycleTicks = 30f;

        // ── Shared ──
        /// <summary>Y (altitude) added per unit of Z lift so the raised body sorts correctly.</summary>
        const float AltitudePerLift = 0.03658537f;
        /// <summary>The base mod's wobble is intensified by this factor while airborne.</summary>
        const float WobbleIntensity = 1.3f; // 30% more intense
        /// <summary>Sway angle → horizontal screen displacement scale.</summary>
        const float WobbleSwayScale = 0.08f;

        /// <summary>
        /// Cached quarter-waveform delegate for <c>Toddlers.AnimationUtility.Waveform</c>
        /// (the same curve the base mod's AnimationWorker_ToddlerWobble uses). Explicit
        /// static so the per-frame call never touches lambda-cache init logic.
        /// </summary>
        static readonly Func<float, float> ToddleQuarterWave =
            f => Toddlers.AnimationUtility.toddleCurve.Evaluate(f);

        /// <summary>
        /// Adds the hop offset to the body position. Parameters are only what's used
        /// (Harmony injects each one, so unused ones are dropped). <c>__instance</c>
        /// gives the renderer directly — cheaper than <c>pawn.Drawer.renderer</c>.
        /// </summary>
        public static void Postfix(PawnRenderer __instance, ref Vector3 __result, PawnPosture posture, Pawn ___pawn)
        {
            // Cheapest rejections first: the bounce only exists for standing pawns.
            if (posture != PawnPosture.Standing || ___pawn == null)
                return;

            // Toddler jumping / crib jumping — identified by job driver (no AnimationDef).
            JobDriver? driver = ___pawn.jobs?.curDriver;
            if (driver is JobDriver_ToddlerJumping || driver is JobDriver_ToddlerCribJumping)
            {
                if (PlayAnimationUtility.ToddlerAnimationsDisabled || ___pawn.Downed)
                    return;
                ApplyToddlerHop(ref __result, ___pawn);
                return;
            }

            // Child Active Play jumping — identified by its marker AnimationDef.
            AnimationDef? current = __instance.CurAnimation;
            if (current == null || current != TPE_AnimationDefOf.TPE_ChildJumping)
                return;
            if (PlayAnimationUtility.ChildAnimationsDisabled || ___pawn.Downed)
                return;

            // Moderate continuous bounce: one |sin| hop every 15 ticks, 0.5 world units peak
            // (identical math to the original).
            float cycle = Find.TickManager.TicksGame / ChildHopCycleTicks * Mathf.PI * 2f;
            float bounce = Mathf.Abs(Mathf.Sin(cycle)) * ChildHopHeight;
            __result.z += bounce;
            __result.y += bounce * AltitudePerLift; // altitude sorting
        }

        /// <summary>
        /// Toddler hop: discrete 30-tick hops separated by a 10–25-tick landing pause
        /// that varies per hop (hashed from the hop index and the pawn's ID), plus the
        /// base Toddlers mod's walking wobble intensified 30% while airborne.
        ///
        /// Produces exactly the same motion as the original "walk every hop since tick 0"
        /// loop, in constant time: the pause sequence repeats every 16 hops (760 ticks,
        /// see <see cref="HopPatternTicks"/>), so we take the position inside the current
        /// 760-tick block and walk at most 16 hops. Equivalence: for hop index
        /// <c>c = 16k + i</c>, <c>(c*31 + seed*17) % 16 == (i*31 + (seed &amp; 15)*17) &amp; 15</c>
        /// for every non-negative ID (31·16k ≡ 0 and seed*17 ≡ (seed&amp;15)*17 mod 16), and
        /// block k starts at tick 760k. The tick+phase sum is done in 64-bit so huge IDs or
        /// very long games can't overflow; everything after the modulo is small ints.
        /// </summary>
        static void ApplyToddlerHop(ref Vector3 result, Pawn pawn)
        {
            int seed = pawn.thingIDNumber;

            // Per-pawn phase offset (seed*7) so toddlers don't hop in sync.
            long t = (long)Find.TickManager.TicksGame + (long)seed * 7L;
            int tInPattern = (int)(t % HopPatternTicks);
            if (tInPattern < 0)
                tInPattern += HopPatternTicks; // only reachable with a negative ID; keeps it in range

            int seedTerm = (seed & (PauseVariants - 1)) * 17;
            int accumulated = 0;
            int tickInHop = 0;
            for (int i = 0; i < HopsPerPattern; i++)
            {
                int hopLen = JumpTicks + MinPauseTicks + ((i * 31 + seedTerm) & (PauseVariants - 1));
                if (tInPattern < accumulated + hopLen)
                {
                    tickInHop = tInPattern - accumulated;
                    break;
                }
                accumulated += hopLen;
            }
            // (The 16 hop lengths sum to exactly HopPatternTicks, so the loop always
            // breaks; tickInHop's 0 default is never actually used.)

            if (tickInHop >= JumpTicks)
                return; // landed — in the pause between hops

            float arcT = (tickInHop / (float)JumpTicks) * Mathf.PI; // 0 → π
            float bounce = Mathf.Sin(arcT) * ToddlerHopHeight;
            result.z += bounce;
            result.y += bounce * AltitudePerLift; // altitude sorting

            // Replicate the base Toddlers mod walking wobble (AnimationWorker_ToddlerWobble)
            // and intensify it 30% — only for toddlers still learning to walk.
            // Toddlers_DefOf is public (unlike the hediff class) — no per-frame name lookup.
            HediffDef walkDef = Toddlers_DefOf.LearningToWalk;
            Hediff? walkHediff = walkDef == null ? null : pawn.health?.hediffSet?.GetFirstHediffOfDef(walkDef);
            if (walkHediff == null || !ToddlerWobbleAccess.TryGet(walkHediff, out float wobbleMag, out int wobblePeriod))
                return;

            float wx = (float)(Find.TickManager.TicksGame % wobblePeriod) / wobblePeriod;
            float wobble = Toddlers.AnimationUtility.Waveform(ToddleQuarterWave, wx);
            float angle = wobbleMag * wobble * WobbleIntensity;

            // Convert sway angle to horizontal screen displacement.
            result.x += Mathf.Sin(angle * Mathf.Deg2Rad) * WobbleSwayScale;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  BUGWATCH ANIMATION: ENABLE FOR LOOK-THROUGH-STORAGE + MAGNIFYING GLASS
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// The base Toddlers mod's <c>AnimationWorker_Bugwatch.Enabled</c> only returns
    /// true when the pawn's <c>CurJobDef == ToddlerBugwatching</c> AND the current
    /// toil is named "Bugwatching". Two of our custom jobs reuse the same tilt
    /// animation with different job defs / toil names:
    ///
    ///   1. <see cref="JobDriver_ToddlerLookThroughStorage"/> — job def
    ///      <c>TPE_ToddlerLookThroughStorage</c>, toil "LookThroughStorage".
    ///   2. <see cref="JobDriver_TPE_BugwatchingWithMagnifyingGlass"/> — job def
    ///      <c>TPE_BugwatchingWithGlass</c>, toil "BugwatchWithGlass".
    ///
    /// This prefix intercepts the check and returns true for either combo,
    /// bypassing the original method entirely so the body-tilt animation plays.
    /// Without this, toddlers holding a magnifying glass appear upright instead
    /// of tilted forward like base-mod bug watching.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_AnimationWorker_Bugwatch_Enabled
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Toddlers.AnimationWorker_Bugwatch"), "Enabled");
        }

        static bool Prefix(PawnDrawParms parms, ref bool __result)
        {
            if (parms.pawn == null) return true;

            // "Disable toddler play animations": stop forcing the tilt on for OUR two
            // jobs. Falling through to the original is enough — it returns false for
            // them (job def / toil name don't match its check) while the base Toddlers
            // mod's OWN bug watching keeps its tilt (out of scope, user-confirmed).
            // Render-thread safe: static bool read only.
            if (PlayAnimationUtility.ToddlerAnimationsDisabled) return true;

            string? toilName = parms.pawn.jobs?.curDriver?.CurToilString;
            JobDef? curJob = parms.pawn.CurJobDef;

            // Look Through Storage activity
            if (curJob == TPE_JobDefOf.TPE_ToddlerLookThroughStorage
                && toilName == "LookThroughStorage")
            {
                __result = true;
                return false; // Skip original
            }

            // Bug watching with magnifying glass activity
            if (curJob == TPE_JobDefOf.TPE_BugwatchingWithGlass
                && toilName == "BugwatchWithGlass")
            {
                __result = true;
                return false; // Skip original
            }

            return true; // Fall through to original Bugwatching check.
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  WIGGLE ANIMATION: ENABLE FOR SELF-SOOTHING ACTIVITY
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// The base Toddlers mod's <c>AnimationWorker_WiggleInCrib.Enabled</c> only
    /// returns true when the pawn's <c>CurJobDef == WiggleInCrib</c>. Our
    /// <see cref="JobDriver_ToddlerSelfSoothing"/> reuses the same wiggle
    /// animation while the toddler is calming down in the crib. This prefix
    /// intercepts the check and returns true for our job, bypassing the
    /// original method entirely so the animation plays.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_AnimationWorker_WiggleInCrib_Enabled
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Toddlers.AnimationWorker_WiggleInCrib"), "Enabled");
        }

        static bool Prefix(PawnDrawParms parms, ref bool __result)
        {
            // "Disable toddler play animations": stop forcing the wiggle on for Self
            // Soothing. The original returns false for it (its toil is "LayDown", not
            // "WiggleInCrib") and still wiggles for the base mod's own crib wiggle job.
            if (PlayAnimationUtility.ToddlerAnimationsDisabled) return true;

            if (parms.pawn != null
                && parms.pawn.CurJobDef == TPE_JobDefOf.TPE_ToddlerSelfSoothing)
            {
                __result = true;
                return false; // Skip original — animation is enabled for our job.
            }
            return true; // Fall through to original WiggleInCrib check.
        }
    }

    // Disable vanilla toy box play interaction — toddlers should only play with real toys
    [HarmonyPatch]
    public static class Patch_ToddlerPlayGiver_PlayToys_CanDo
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.ToddlerPlayGiver_PlayToys");
            return AccessTools.Method(type, "CanDo");
        }

        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false; // Skip original
        }
    }

    [HarmonyPatch]
    public static class Patch_ToddlerPlayGiver_PlayToys_CanDoFromCrib
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.ToddlerPlayGiver_PlayToys");
            return AccessTools.Method(type, "CanDoFromCrib");
        }

        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false; // Skip original
        }
    }

    // Suppress "Could not reserve" spam from ToddlerSkydreaming — multiple toddlers can pick the same cell
    [HarmonyPatch]
    public static class Patch_ToddlerSkydreaming_TryMakePreToilReservations
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.JobDriver_ToddlerSkydreaming");
            return AccessTools.Method(type, "TryMakePreToilReservations");
        }

        public static bool Prefix(JobDriver __instance, bool errorOnFailed, ref bool __result)
        {
            // Try to reserve silently; if it fails, just return false without error spam
            var target = __instance.job.GetTarget(TargetIndex.A);
            __result = ReservationUtility.Reserve(__instance.pawn, target, __instance.job, 1, -1, null, false, false);
            return false; // Skip original
        }
    }

    // Prevent base mod's apparel optimizer from stripping our plush toys off toddlers
    [HarmonyPatch]
    public static class Patch_JobGiver_OptimizeBabyApparel
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.JobGiver_OptimizeBabyApparel");
            return AccessTools.Method(type, "TryGiveJob");
        }

        public static void Postfix(ref Job __result)
        {
            if (__result?.def == JobDefOf.Strip && IsPlushToy(__result.targetB.Thing?.def))
            {
                __result = null;
            }
        }

        static bool IsPlushToy(ThingDef def)
        {
            if (def == null) return false;
            return def == TPE_ThingDefOf.TPE_PlushTeddy
                || def == TPE_ThingDefOf.TPE_PlushMuffalo
                || def == TPE_ThingDefOf.TPE_PlushSheep
                || def == TPE_ThingDefOf.TPE_PlushDino
                || def == TPE_ThingDefOf.TPE_PlushMonster
                || def == TPE_ThingDefOf.TPE_PlushFox;
        }
    }

    // Apply plush sleep thought using the exact same hook vanilla uses for "Slept in the cold".
    // Toils_LayDown.ApplyBedThoughts is private static; AccessTools.Method finds it reliably.
    [HarmonyPatch]
    public static class Patch_Toils_LayDown_ApplyBedThoughts
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Toils_LayDown), "ApplyBedThoughts");
        }

        public static void Postfix(Pawn actor, Building_Bed bed)
        {
            if (actor == null) return;
            // Only apply sleep thoughts when the pawn is actually asleep,
            // not just lying in a crib while awake.
            if (actor.Awake()) return;
            if (actor.needs?.mood?.thoughts?.memories == null) return;

            // Resolve which of the four plush-sleep mood brackets (toddler / child
            // 3–6 / child 7–12 / adult 13+) this pawn belongs to, whether its
            // governing setting(s) are on, and — for adults only — which
            // direction(s) of mood the 4-way mode dial permits. Returns false if
            // the pawn isn't eligible for any plush-sleep mood right now.
            if (!TryResolvePlushMoodBracket(actor, out bool isToddlerBracket, out ThoughtDef plushThoughtDef,
                    out ThoughtDef noPlushThoughtDef, out bool allowPositive, out bool allowNegative))
            {
                return;
            }

            var memories = actor.needs.mood.thoughts.memories;

            // Only apply once per sleep session (thought lasts ~1 day).
            if (memories.GetFirstMemoryOfDef(plushThoughtDef) != null ||
                memories.GetFirstMemoryOfDef(noPlushThoughtDef) != null)
            {
                return;
            }

            bool hasPlush = HasPlushToyWorn(actor);

            if (hasPlush)
            {
                if (allowPositive) memories.TryGainMemory(plushThoughtDef);
                // Plush stays equipped for all four brackets regardless of mood
                // outcome — this only decides whether a thought is granted, not
                // whether the plush is visible. Visual rendering while asleep is a
                // fully separate concern, handled uniformly for every bracket by
                // Patch_PawnRenderNode_Apparel_GraphicsFor_SuppressSleepingPlush +
                // Patch_PawnRenderer_RenderPawnAt_DrawSleepingPlush (see Architecture.md §5.1).
            }
            else
            {
                if (allowNegative) memories.TryGainMemory(noPlushThoughtDef);
            }

            // ========================================================
            // TODDLER "BED TOO BIG" THOUGHT: Applied when toddlers
            // sleep in regular beds (not cribs). Regular beds trigger
            // anxiety due to perceived size and lack of walls.
            // Only for toddlers; children and adults don't get this thought.
            // ========================================================
            if (!isToddlerBracket) return; // Only toddlers get this thought
            if (bed == null) return; // Sleeping on ground, no thought
            
            // Skip if already has the thought (stackLimit=1 prevents duplicates anyway)
            if (memories.GetFirstMemoryOfDef(TPE_ThoughtDefOf.TPE_BedTooBig) != null)
                return;
            
            // Only apply if sleeping in a regular bed (NOT a crib).
            // Crib has defName "Crib"; check bed.def.defName to detect it.
            // If bed.def is null (shouldn't happen but defensive), treat it as non-crib.
            if (bed.def != null && bed.def.defName == "Crib")
                return; // Crib is safe; no anxiety thought
            
            // Toddler is sleeping in a regular bed instead of their safe crib.
            // Apply the "bed too big" debuff.
            memories.TryGainMemory(TPE_ThoughtDefOf.TPE_BedTooBig);
        }

        /// <summary>
        /// Resolves which plush-sleep mood bracket <paramref name="pawn"/> currently
        /// falls into — toddler (~1–3, unconditional), child age group 1 (3–6, gated
        /// by <c>enableChildPlushSleep</c>), child age group 2 (7–12, gated by
        /// <c>enableOlderChildPlushSleep</c>), or adult (13+, gated by
        /// <c>enableAdultPlushSleep</c> AND the 4-way <c>adultPlushieMoodMode</c>
        /// dial) — and outputs the correct pair of ThoughtDefs plus which
        /// direction(s) (positive/negative) are currently allowed to apply.
        ///
        /// Returns false if the pawn is not eligible for ANY plush-sleep mood right
        /// now: wrong race/age/stage, the bracket's toggle is off, or (adults only)
        /// the mood mode is set to "None".
        ///
        /// BUGFIX (folded into this rewrite): the previous version of this check
        /// compared a FLOAT age (“age &lt;= 6f”) while the sleep-job redirect that
        /// actually equips the plush (<c>IsAgeGroup1Child</c>) compares an INT age
        /// (“age &lt;= 6”, i.e. true for the entire 6.0–6.999 range). A child aged
        /// e.g. 6.5 could therefore be redirected to fetch a plush but then never
        /// receive credit for wearing it. Both checks now use the same integer
        /// biological age, so they always agree.
        ///
        /// FIX A3 (preserved from the original IsToddlerAge): only humanlike pawns
        /// are ever eligible. Animals never receive these thoughts, even if
        /// somehow age-matched.
        /// </summary>
        internal static bool TryResolvePlushMoodBracket(
            Pawn pawn,
            out bool isToddlerBracket,
            out ThoughtDef plushThoughtDef,
            out ThoughtDef noPlushThoughtDef,
            out bool allowPositive,
            out bool allowNegative)
        {
            isToddlerBracket = false;
            plushThoughtDef = null;
            noPlushThoughtDef = null;
            allowPositive = false;
            allowNegative = false;

            if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.ageTracker == null) return false;

            // ── Toddlers (~1–3): always on, unconditional (existing behavior). ──
            if (ToddlerUtility.IsToddler(pawn))
            {
                isToddlerBracket = true;
                plushThoughtDef = TPE_ThoughtDefOf.TPE_SleptWithPlushToy;
                noPlushThoughtDef = TPE_ThoughtDefOf.TPE_NoSoftCuddleBuddy;
                allowPositive = true;
                allowNegative = true;
                return true;
            }

            DevelopmentalStage stage = pawn.DevelopmentalStage;
            int ageYears = pawn.ageTracker.AgeBiologicalYears;

            // ── Children (3–13, DevelopmentalStage.Child) — two independent age brackets. ──
            if (stage == DevelopmentalStage.Child)
            {
                if (ageYears >= 3 && ageYears <= 6)
                {
                    // Age Group 1 (existing 3–6 toggle) — unchanged behavior.
                    if (TPE_Mod.Settings?.enableChildPlushSleep != true) return false;
                }
                else if (ageYears > 6)
                {
                    // Age Group 2 (NEW 7–12 toggle). DevelopmentalStage.Child caps
                    // below 13 (HumanlikeTeenager takes over at 13), so this
                    // naturally covers exactly ages 7–12.
                    if (TPE_Mod.Settings?.enableOlderChildPlushSleep != true) return false;
                }
                else
                {
                    return false; // Child stage starts at age 3; defensive only.
                }

                // Both child age groups reuse the same ThoughtDefs — the flavor
                // text reads fine for a 12-year-old, and it keeps the def count
                // minimal. Both directions always apply for children, matching
                // the existing (pre-adult) behavior exactly.
                plushThoughtDef = TPE_ThoughtDefOf.TPE_ChildSleptWithPlushToy;
                noPlushThoughtDef = TPE_ThoughtDefOf.TPE_ChildNoPlushToy;
                allowPositive = true;
                allowNegative = true;
                return true;
            }

            // ── Adults (13+, DevelopmentalStage.Adult — includes the Teenager life
            // stage; see TPE_Settings.enableAdultPlushSleep's doc comment for why
            // 13 is the correct cutoff) — NEW: gated by a master toggle AND a
            // 4-way mood-mode dial that can independently suppress either
            // direction, unlike every other bracket above. ──
            if (stage == DevelopmentalStage.Adult)
            {
                if (TPE_Mod.Settings?.enableAdultPlushSleep != true) return false;

                AdultPlushieMoodMode mode = TPE_Mod.Settings?.adultPlushieMoodMode ?? AdultPlushieMoodMode.Both;
                if (mode == AdultPlushieMoodMode.None) return false; // neither direction ever applies

                plushThoughtDef = TPE_ThoughtDefOf.TPE_AdultSleptWithPlushToy;
                noPlushThoughtDef = TPE_ThoughtDefOf.TPE_AdultNoPlushToy;
                allowPositive = mode == AdultPlushieMoodMode.PositiveOnly || mode == AdultPlushieMoodMode.Both;
                allowNegative = mode == AdultPlushieMoodMode.NegativeOnly || mode == AdultPlushieMoodMode.Both;
                return true;
            }

            return false; // Newborn or unrecognized developmental stage.
        }

        static bool HasPlushToyWorn(Pawn pawn)
        {
            // Check worn apparel
            if (pawn.apparel?.WornApparel != null)
            {
                foreach (Apparel a in pawn.apparel.WornApparel)
                {
                    if (IsPlushToy(a.def)) return true;
                }
            }
            // Also check carried item (plush might be in hand during sleep job)
            if (pawn.carryTracker?.CarriedThing != null && IsPlushToy(pawn.carryTracker.CarriedThing.def))
                return true;
            return false;
        }

        static bool IsPlushToy(ThingDef def)
        {
            return def == TPE_ThingDefOf.TPE_PlushTeddy
                || def == TPE_ThingDefOf.TPE_PlushMuffalo
                || def == TPE_ThingDefOf.TPE_PlushSheep
                || def == TPE_ThingDefOf.TPE_PlushDino
                || def == TPE_ThingDefOf.TPE_PlushMonster
                || def == TPE_ThingDefOf.TPE_PlushFox;
        }
    }

    // ============================================================
    // INJECT PLUSH-GETTING INTO SLEEP JOB (JobDriver_LayDown)
    // ONLY for in-crib toddlers — walkers are handled by the
    // sleep-job redirect (Patch_JobDriver_LayDown_SetupToils).
    //
    // CRITICAL: every injected toil must soft-fail (no-op + instant complete).
    // Using Toils_Haul.StartCarryThing or Toils_Goto.GotoThing inside LayDown
    // causes hard job-fail cascades ("10 jobs in one tick") because their
    // internal error checks call pawn.jobs.EndCurrentJob().
    // ============================================================
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_JobDriver_SetupToils_AddPlush
    {
        // Cached field accessors (compiled once) — replaces a per-call
        // Traverse.Create(...).Field("toils") reflection lookup on this hot path.
        static readonly AccessTools.FieldRef<JobDriver, List<Toil>> ToilsRef =
            AccessTools.FieldRefAccess<JobDriver, List<Toil>>("toils");
        static readonly AccessTools.FieldRef<JobDriver, int> CurToilIndexRef =
            AccessTools.FieldRefAccess<JobDriver, int>("curToilIndex");

        static void Postfix(JobDriver __instance)
        {
            if (__instance is not JobDriver_LayDown layDown) return;

            // BUGFIX (save-load): never touch a job whose toils are being REBUILT on
            // load — see TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned (also covers
            // the old !pawn.Spawned check). While restoring, only run the one-time
            // legacy-save index repair below.
            if (TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned(__instance))
            {
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                    ClampLegacyRestoredToilIndex(__instance);
                return;
            }
            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead) return;
            
            // FIX A2: Only humanlike pawns can request plush toys in sleep jobs.
            // Animals should never try to equip toys, even if somehow marked as toddlers.
            if (!pawn.RaceProps.Humanlike) return;
            
            if (!IsToddler(pawn)) return;
            // Already lying down somewhere else — too late
            if (RestUtility.InBed(pawn)) return;
            // Already carrying something (even non-plush) — don't interfere
            if (pawn.carryTracker?.CarriedThing != null) return;
            if (HasPlushEquippedOrCarried(pawn)) return;

            List<Toil> toils = ToilsRef(__instance);
            if (toils == null) return;

            // Only handle in-crib toddlers here. Walkers without plush are
            // redirected to TPE_SeekPlushToy by Patch_JobDriver_LayDown_SetupToils.
            if (!CribUtility.InCrib(pawn)) return;

            // Run the equip right before the "go to bed" step:
            // - Bed case:  ClaimBed(0), GotoBed(1), LayDown(2) → toil 1
            // - No bed:    GotoCell(0), LayDown(1)            → toil 0
            //   (vanilla can omit GotoCell; then toil 0 is LayDown — still "before
            //   lying down", same timing as the old insert-at-0.)
            int insertIndex = (layDown.Bed != null) ? 1 : 0;
            if (insertIndex >= toils.Count) return;

            // If already in a crib, only equip a plush that's within arm's reach.
            // Don't make the toddler leave the crib.
            Thing? nearbyPlush = FindNearestPlush(pawn, maxDist: 1.9f);
            if (nearbyPlush == null || !nearbyPlush.Spawned) return;

            // BUGFIX (save-load toil-count drift): this used to INSERT a whole extra
            // toil. The toil list is rebuilt from scratch when a save loads, and the
            // insert is (correctly) skipped then — so a toddler saved asleep in a crib
            // had a saved curToilIndex of 3 against a rebuilt list of only 3 toils,
            // which errors and wakes them. Attaching the equip as a PRE-INIT ACTION
            // of the existing toil keeps the toil count identical to vanilla in every
            // case, so saved indices always line up. Pre-init actions run right before
            // that toil's own initAction (JobDriver.TryActuallyStartNextToil) — the
            // same moment the old inserted instant toil ran.
            toils[insertIndex].AddPreInitAction(() =>
            {
                if (nearbyPlush is Apparel apparel && !apparel.Destroyed && apparel.Spawned)
                {
                    // BUGFIX: same conflict guard as JobDriver_SeekPlushToy — see
                    // TPE_ToyUtility.HasConflictingWornApparel's doc comment.
                    // Wear(dropReplacedApparel:false) would otherwise silently
                    // orphan any currently worn item sharing the plush's layer.
                    if (TPE_ToyUtility.HasConflictingWornApparel(pawn, apparel.def))
                        return;
                    pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                }
            });
        }

        /// <summary>
        /// One-time migration for saves made with older versions of this mod, which
        /// INSERTED an extra toil into crib toddlers' LayDown jobs (see the BUGFIX
        /// note in <see cref="Postfix"/>). Such a save stores curToilIndex = 3
        /// (LayDown, after Claim/Equip/Goto) but the rebuilt vanilla list only has
        /// 3 toils, which vanilla reports as "tried to get CurToil with
        /// curToilIndex=3 but only has 3 toils" and ends the job — waking the
        /// toddler. Clamping an out-of-range index to the LAST toil maps it back
        /// onto LayDown (the toil the toddler was actually in). Only ever touches a
        /// LayDown driver whose index is already invalid, so it can't alter any
        /// correctly-restored job. Runs only during load (PostLoadInit).
        /// </summary>
        static void ClampLegacyRestoredToilIndex(JobDriver driver)
        {
            List<Toil> toils = ToilsRef(driver);
            if (toils == null || toils.Count == 0) return;
            ref int curToilIndex = ref CurToilIndexRef(driver);
            if (curToilIndex >= toils.Count)
                curToilIndex = toils.Count - 1;
        }

        /// <summary>
        /// FIX A4: Defense-in-depth humanlike check on local helper.
        /// Even though the postfix now guards, this function should be independently safe.
        /// </summary>
        static bool IsToddler(Pawn pawn)
        {
            // Only humanlike pawns can be toddlers for our purposes.
            if (!pawn.RaceProps.Humanlike) return false;
            
            if (ToddlerUtility.IsToddler(pawn)) return true;
            float age = pawn.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
            return age >= 1f && age < 3f;
        }

        static bool HasPlushEquippedOrCarried(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel != null)
            {
                foreach (Apparel a in pawn.apparel.WornApparel)
                {
                    if (IsPlushDef(a.def)) return true;
                }
            }
            if (pawn.carryTracker?.CarriedThing != null && IsPlushDef(pawn.carryTracker.CarriedThing.def))
                return true;
            return false;
        }

        static bool IsPlushDef(ThingDef def)
        {
            return def == TPE_ThingDefOf.TPE_PlushTeddy
                || def == TPE_ThingDefOf.TPE_PlushMuffalo
                || def == TPE_ThingDefOf.TPE_PlushSheep
                || def == TPE_ThingDefOf.TPE_PlushDino
                || def == TPE_ThingDefOf.TPE_PlushMonster
                || def == TPE_ThingDefOf.TPE_PlushFox;
        }

        static Thing? FindNearestPlush(Pawn pawn)
        {
            return FindNearestPlush(pawn, 30f);
        }

        static Thing? FindNearestPlush(Pawn pawn, float maxDist)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                maxDist,
                t => IsPlushDef(t.def) && !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }
    }

    // ==========================================
    // GIZMO: Toggle "Lock in Crib" on selected toddlers
    // ==========================================
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos_CribLock
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            // FIX G1: Null-safety — if original returned null, don't crash.
            if (__result != null)
            {
                foreach (Gizmo g in __result)
                    yield return g;
            }

            if (!IsPlayerToddler(__instance))
                yield break;
            // FIX G2: Health null-safety
            if (__instance.health?.hediffSet == null)
                yield break;

            // FIX G3: isActive must read the hediff dynamically. Capturing a
            // local bool creates a stale closure: the button never updates
            // its visual state after clicking, making the lock appear broken.
            yield return new Command_Toggle
            {
                defaultLabel = "TPE_CribLockLabel".Translate(),
                defaultDesc = "TPE_CribLockTooltip".Translate(),
                icon = TexCommand.ForbidOff,
                activateSound = SoundDefOf.Checkbox_TurnedOn,
                // Dynamic read — always reflects current hediff state
                isActive = () => __instance.health.hediffSet.HasHediff(TPE_HediffDefOf.TPE_CribLocked),
                toggleAction = () => ToggleCribLock(__instance)
            };
        }

        static bool IsPlayerToddler(Pawn pawn)
        {
            if (pawn.Faction != Faction.OfPlayer)
                return false;
            // Cyanobot's Toddlers mod already checks humanlike; this covers it.
            if (ToddlerUtility.IsToddler(pawn))
                return true;
            // Fallback: only humanlike pawns aged 1–3. Animals, mechs, and other
            // non-humanlike pawns must never show the crib-lock gizmo.
            if (!pawn.RaceProps.Humanlike)
                return false;
            float age = pawn.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
            return age >= 1f && age < 3f;
        }

        static void ToggleCribLock(Pawn pawn)
        {
            if (pawn.health?.hediffSet == null) return;
            Hediff? existing = pawn.health.hediffSet.GetFirstHediffOfDef(TPE_HediffDefOf.TPE_CribLocked);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
                if (pawn.Map != null)
                    Messages.Message("TPE_CribUnlockedMessage".Translate(pawn.Name.ToStringShort), pawn, MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                pawn.health.AddHediff(TPE_HediffDefOf.TPE_CribLocked);
                if (pawn.Map != null)
                    Messages.Message("TPE_CribLockedMessage".Translate(pawn.Name.ToStringShort), pawn, MessageTypeDefOf.NeutralEvent);
            }
        }
    }

    // ==========================================
    // BASE MOD INTEGRATION: Crib-locked toddlers are always "should be in bed"
    // Hooks into the base Toddlers mod so colonists automatically put locked
    // toddlers back in their crib via the existing baby-safety system.
    // ==========================================
    [HarmonyPatch]
    public static class Patch_BabyMoveUtility_ShouldBeInBed
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Toddlers.BabyMoveUtility"), "ShouldBeInBed");
        }

        static bool Prefix(Pawn baby, ref bool __result)
        {
            if (baby == null) return true;
            // FIX N4a: Only intercept for actual toddlers. If a pawn aged up
            // while crib-locked, fall through to vanilla logic.
            if (!ToddlerUtility.IsToddler(baby)) return true;
            if (baby.health?.hediffSet == null) return true;
            if (!baby.health.hediffSet.HasHediff(TPE_HediffDefOf.TPE_CribLocked))
                return true;

            __result = true;
            return false; // skip original
        }
    }

    // ==========================================
    // GIZMO: Toggle "Lock Play Need at 50%" on selected toddlers/children
    // Only visible when god mode is active. Locks the pawn's play need so
    // it neither decays nor rises from play activities.
    // ==========================================
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos_LockPlayNeed
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            if (__result != null)
            {
                foreach (Gizmo g in __result)
                    yield return g;
            }

            if (!DebugSettings.godMode)
                yield break;
            if (__instance.Faction != Faction.OfPlayer)
                yield break;
            if (!HasPlayNeed(__instance))
                yield break;

            Pawn_PlayTracker? tracker = Current.Game?.GetComponent<GameComponent_PlayTracker>()?.GetTracker(__instance);
            if (tracker == null)
                yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "Lock Play Need",
                defaultDesc = "When enabled, this pawn's play need is frozen at 50% and will not decay or rise from play activities.",
                icon = ContentFinder<Texture2D>.Get("UI/LockPlayNeed"),
                activateSound = SoundDefOf.Checkbox_TurnedOn,
                isActive = () => tracker.lockPlayNeed,
                toggleAction = () =>
                {
                    tracker.lockPlayNeed = !tracker.lockPlayNeed;
                    if (tracker.lockPlayNeed)
                    {
                        // Force to 50% immediately
                        Need? need = __instance.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay);
                        if (need != null)
                            need.CurLevel = 0.5f;
                        else if (__instance.needs?.play != null)
                            __instance.needs.play.CurLevel = 0.5f;
                        Messages.Message(__instance.Name.ToStringShort + "'s play need is now locked at 50%.", __instance, MessageTypeDefOf.NeutralEvent);
                    }
                    else
                    {
                        Messages.Message(__instance.Name.ToStringShort + "'s play need is no longer locked.", __instance, MessageTypeDefOf.NeutralEvent);
                    }
                }
            };
        }

        static bool HasPlayNeed(Pawn pawn)
        {
            // Children (3–13) have our custom Need_ChildPlay
            if (pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay) != null)
                return true;
            // Toddlers (1–3) have the base-mod Need_Play
            if (ToddlerUtility.IsToddler(pawn) && pawn.needs?.play != null)
                return true;
            return false;
        }
    }

    // ==========================================
    // DEV MODE: Lock toddler play need at 50%
    // Postfix on base Toddlers mod's Need_Play.NeedInterval so that locked
    // toddlers always stay at exactly 50% play.
    // ==========================================
    [HarmonyPatch(typeof(Need_Play), "NeedInterval")]
    public static class Patch_Need_Play_NeedInterval_LockAt50
    {
        static void Postfix(Need_Play __instance)
        {
            Pawn? pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null) return;
            if (pawn.Faction != Faction.OfPlayer) return;
            if (!ToddlerUtility.IsToddler(pawn)) return;

            Pawn_PlayTracker? tracker = Current.Game?.GetComponent<GameComponent_PlayTracker>()?.GetTracker(pawn);
            if (tracker?.lockPlayNeed != true) return;

            __instance.CurLevel = 0.5f;
        }
    }

    // ==========================================
    // UNFORBID LAYER 1: Patch vanilla apparel drops
    // Pawn_ApparelTracker.TryDrop defaults forbid=true. Any code path that
    // drops our plush without explicitly passing forbid=false will forbid it.
    // This postfix catches ALL of them.
    // ==========================================
    [HarmonyPatch]
    public static class Patch_Pawn_ApparelTracker_TryDrop_UnforbidPlush
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_ApparelTracker), "TryDrop",
                new[] { typeof(Apparel), typeof(Apparel).MakeByRefType(), typeof(IntVec3), typeof(bool) });
        }

        /// <summary>
        /// After any apparel drop, if the dropped item is one of our plush toys,
        /// unconditionally unforbid it so toddlers can pick it back up.
        /// </summary>
        public static void Postfix(bool __result, Apparel resultingAp)
        {
            if (!__result) return; // drop failed — nothing to do
            if (resultingAp == null) return;
            if (TPE_ToyUtility.IsPlushThing(resultingAp))
            {
                TPE_ToyUtility.Unforbid(resultingAp);
            }
        }
    }

    // ==========================================
    // UNFORBID LAYER 1: Patch vanilla carry drops
    // Catches any code path that drops a carried thing (our toys).
    // ==========================================
    [HarmonyPatch]
    public static class Patch_Pawn_CarryTracker_TryDropCarriedThing_UnforbidToy
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_CarryTracker), "TryDropCarriedThing",
                new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) });
        }

        /// <summary>
        /// After any carried-thing drop, if the dropped item is one of our toys,
        /// unconditionally unforbid it.
        /// </summary>
        public static void Postfix(bool __result, Thing resultingThing)
        {
            if (!__result) return; // drop failed
            if (resultingThing == null) return;
            if (TPE_ToyUtility.IsAnyToyThing(resultingThing))
            {
                TPE_ToyUtility.Unforbid(resultingThing);
            }
        }
    }

    // ==========================================
    // SLEEP JOB REDIRECT: Toddler needs plush before bed
    // When a toddler receives the vanilla LayDown (sleep) job, check if they
    // have a plush equipped. If not, cancel sleep and start the seek-plush job
    // instead. The seek-plush job will queue sleep again after equipping.
    // Crawlers in cribs are skipped — they get plush via the injected sleep toils.
    // ==========================================
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_JobDriver_LayDown_SetupToils
    {
        /// <summary>
        /// Tracks the last tick each pawn was redirected so we don't spin in a
        /// tight loop if the seek-plush job keeps failing.
        /// </summary>
        static readonly Dictionary<int, int> lastRedirectTick = new Dictionary<int, int>();
        const int RedirectCooldownTicks = 250; // ~4 seconds

        public static bool Prefix(JobDriver __instance)
        {
            // CRITICAL: must use base type for __instance because this prefix
            // runs on SetupToils for ALL job drivers. Using JobDriver_LayDown
            // as the parameter type causes Harmony to pass null for non-LayDown
            // drivers, which NREs on __instance.pawn and cascades into a job
            // death spiral ("10 jobs in one tick").
            if (__instance is not JobDriver_LayDown layDown) return true;
            // BUGFIX (save-load): SetupToils also runs while RESTORING a saved job —
            // never redirect then. See TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned.
            if (TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned(__instance)) return true;
            Pawn pawn = __instance.pawn;
            
            // FIX A1: Only humanlike pawns can seek toys before sleep.
            // Animals should never redirect to toy-seeking, even if somehow marked as toddlers.
            if (!pawn.RaceProps.Humanlike) return true;
            
            if (!TPE_ToyUtility.IsToddler(pawn)) return true;
            if (CribUtility.InCrib(pawn)) return true; // in-crib handled separately
            if (TPE_ToyUtility.HasPlushEquippedOrCarried(pawn)) return true;

            // Redirect well-rested toddlers who are actually going to bed.
            // Toddlers are fearful and stubborn — they want their plush for ANY
            // sleep except when collapsing from exhaustion (rest at or below 15%).
            // LayDown jobs are also created for idle resting and spawn init;
            // we must not hijack those.
            // NEW SETTING: alwaysSeekPlushRegardlessOfRest lets the player disable
            // this safety valve entirely — when on, toddlers fetch a plush before
            // EVERY bedtime no matter how exhausted they already are. Default OFF,
            // so this check still applies unless the player explicitly opts in.
            if (TPE_Mod.Settings?.alwaysSeekPlushRegardlessOfRest != true
                && pawn.needs?.rest?.CurLevel <= 0.15f) return true;

            // Cooldown to prevent tight loops
            int currentTick = Find.TickManager.TicksGame;
            if (lastRedirectTick.TryGetValue(pawn.thingIDNumber, out int lastTick))
            {
                if (currentTick - lastTick < RedirectCooldownTicks) return true;
            }

            Thing? plush = TPE_ToyUtility.FindNearestPlush(pawn);
            if (plush == null) return true; // no plush available, just sleep without it

            // Mark redirect time
            lastRedirectTick[pawn.thingIDNumber] = currentTick;

            // Queue plush-seeking job, preserving the original bed target so we
            // can sleep in the same bed after equipping.
            Job seekJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_SeekPlushToy, plush);
            seekJob.targetB = __instance.job.targetA;
            pawn.jobs.jobQueue.EnqueueFirst(seekJob);

            // End this sleep job before any toils are created
            __instance.EndJobWith(JobCondition.Incompletable);
            return false; // skip original SetupToils
        }
    }

    // SLEEP JOB REDIRECT: Child (3–6) needs plush before bed
    // Mirrors the toddler redirect above but targets children aged 3–6.
    // When a child receives a vanilla LayDown (sleep) job, check if they
    // have a plush equipped. If not, cancel sleep and queue the seek-plush
    // job instead. The seek-plush job will queue sleep again after equipping.
    // ==========================================
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_JobDriver_LayDown_SetupToils_Child
    {
        /// <summary>
        /// Tracks the last tick each child was redirected so we don't spin in a
        /// tight loop if the seek-plush job keeps failing.
        /// Separate dictionary from the toddler patch to avoid key collisions.
        /// </summary>
        static readonly Dictionary<int, int> lastRedirectTick = new Dictionary<int, int>();
        const int RedirectCooldownTicks = 250; // ~4 seconds

        /// <summary>
        /// Harmony prefix on <see cref="JobDriver.SetupToils"/>.
        /// Intercepts LayDown jobs for children 3–6 who lack a plush toy,
        /// redirecting them to seek and equip one before sleeping.
        /// </summary>
        public static bool Prefix(JobDriver __instance)
        {
            // CRITICAL: must use base type for __instance because this prefix
            // runs on SetupToils for ALL job drivers. Using JobDriver_LayDown
            // as the parameter type causes Harmony to pass null for non-LayDown
            // drivers, which NREs on __instance.pawn.
            if (__instance is not JobDriver_LayDown) return true;
            Pawn pawn = __instance.pawn;

            // ── Mod setting guard ────────────────────────────────────────
            if (TPE_Mod.Settings?.enableChildPlushSleep != true) return true;

            // ── BUGFIX (user-reported): save-load restore guard ─────────────
            // SetupToils also runs while RESTORING a saved, in-progress sleep job,
            // before the child is spawned — this used to NRE in FindNearestPlush and
            // wake the child. See TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned.
            if (TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned(__instance)) return true;

            // ── Homely compat guard ──────────────────────────────────────
            // This whole check only ever does anything when BOTH gates hold:
            // "Intimacy - Homely" is active (HomelyCompat.Active) AND the
            // enableChildPlushSleep setting is on (already checked above, so
            // by this point it's guaranteed true). If this specific LayDown
            // job is Homely's own "walk to wardrobe and undress" job
            // (JobDriver_UndressBeforeBed — itself a JobDriver_LayDown
            // subclass), step aside and let it run to completion instead of
            // hijacking it. The REAL sleep job Homely starts afterwards is a
            // plain vanilla LayDown, which still passes through this same
            // prefix normally — so the plush-fetch redirect still fires, just
            // after undressing instead of pre-empting it.
            if (HomelyCompat.Active
                && __instance.GetType().FullName == HomelyCompat.UndressBeforeBedTypeName)
            {
                return true;
            }

            // ── Age filter: exactly 3–6 years old (Child stage) ──────────
            if (!IsAgeGroup1Child(pawn)) return true;

            // ── Already in bed ───────────────────────────────────────────
            if (RestUtility.InBed(pawn)) return true;

            // ── Already has plush ────────────────────────────────────────
            if (TPE_ToyUtility.HasPlushEquippedOrCarried(pawn)) return true;

            // ── Rest threshold guard ─────────────────────────────────────
            // Redirect well-rested children who are actually going to bed.
            // Children are slightly more mature — they want their plush for most
            // sleep, but skip it when genuinely exhausted (rest at or below 25%).
            // LayDown jobs are also created for idle resting and spawn init;
            // we must not hijack those.
            // NEW SETTING: alwaysSeekPlushRegardlessOfRest (default OFF) skips this
            // safety valve entirely when the player opts in — see its own doc
            // comment in ModSettings.cs for the full rationale.
            if (TPE_Mod.Settings?.alwaysSeekPlushRegardlessOfRest != true
                && pawn.needs?.rest?.CurLevel <= 0.25f) return true;

            // ── Cooldown to prevent tight loops ──────────────────────────
            int currentTick = Find.TickManager.TicksGame;
            if (lastRedirectTick.TryGetValue(pawn.thingIDNumber, out int lastTick))
            {
                if (currentTick - lastTick < RedirectCooldownTicks) return true;
            }

            // ── Plush availability ───────────────────────────────────────
            Thing? plush = TPE_ToyUtility.FindNearestPlush(pawn);
            if (plush == null) return true; // no plush available, sleep without it

            // ── Perform redirect ─────────────────────────────────────────
            lastRedirectTick[pawn.thingIDNumber] = currentTick;

            // Queue plush-seeking job, preserving the original bed target so we
            // can sleep in the same bed after equipping.
            Job seekJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_SeekPlushToy, plush);
            seekJob.targetB = __instance.job.targetA;
            pawn.jobs.jobQueue.EnqueueFirst(seekJob);

            // End this sleep job before any toils are created
            __instance.EndJobWith(JobCondition.Incompletable);
            return false; // skip original SetupToils
        }

        /// <summary>
        /// Returns true if the pawn is a humanlike child aged 3–6 (inclusive).
        /// This is "Age Group 1" in our child-play system.
        /// </summary>
        static bool IsAgeGroup1Child(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.DevelopmentalStage != DevelopmentalStage.Child) return false;
            int age = pawn.ageTracker?.AgeBiologicalYears ?? 999;
            return age >= 3 && age <= 6;
        }
    }

    // ==========================================
    // SLEEP JOB REDIRECT: Older child (7–12) needs plush before bed
    // "Age Group 2" in our child-play system — mirrors
    // Patch_JobDriver_LayDown_SetupToils_Child exactly (same mechanism, same
    // seek-plush job), just a different age bracket gated by its own
    // independent setting (enableOlderChildPlushSleep) so it can be toggled
    // without touching the original 3–6 behavior at all.
    // ==========================================
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_JobDriver_LayDown_SetupToils_OlderChild
    {
        /// <summary>
        /// Tracks the last tick each older child was redirected so we don't spin in a
        /// tight loop if the seek-plush job keeps failing. Separate dictionary from
        /// the toddler/Age-Group-1 patches to avoid key collisions.
        /// </summary>
        static readonly Dictionary<int, int> lastRedirectTick = new Dictionary<int, int>();
        const int RedirectCooldownTicks = 250; // ~4 seconds

        /// <summary>
        /// Harmony prefix on <see cref="JobDriver.SetupToils"/>.
        /// Intercepts LayDown jobs for children 7–12 who lack a plush toy,
        /// redirecting them to seek and equip one before sleeping.
        /// </summary>
        public static bool Prefix(JobDriver __instance)
        {
            // CRITICAL: must use base type for __instance — see the identical
            // comment on Patch_JobDriver_LayDown_SetupToils_Child above.
            if (__instance is not JobDriver_LayDown) return true;
            Pawn pawn = __instance.pawn;

            // ── Mod setting guard ─────────────────────────────────
            if (TPE_Mod.Settings?.enableOlderChildPlushSleep != true) return true;

            // ── BUGFIX: save-load restore guard (same as the 3–6 patch) ────
            if (TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned(__instance)) return true;

            // ── Homely compat guard ────────────────────────────
            // Same reasoning as the 3–6 patch: step aside for Homely's own
            // undress-before-bed job so it isn't hijacked mid-ritual.
            if (HomelyCompat.Active
                && __instance.GetType().FullName == HomelyCompat.UndressBeforeBedTypeName)
            {
                return true;
            }

            // ── Age filter: 7–12 years old (still Child developmental stage) ──
            if (!IsAgeGroup2Child(pawn)) return true;

            // ── Already in bed ─────────────────────────────────────
            if (RestUtility.InBed(pawn)) return true;

            // ── Already has plush ────────────────────────────────
            if (TPE_ToyUtility.HasPlushEquippedOrCarried(pawn)) return true;

            // ── Rest threshold guard ─────────────────────────
            // BUGFIX: this was originally 0.30f ("a little more relaxed than the
            // 3–6 bracket's 0.25"), which sat AT/ABOVE the point rest is already
            // guaranteed to be at by the time this runs — vanilla's own
            // JobGiver_GetRest.GetPriority ONLY assigns a LayDown job outside a
            // scheduled Sleep block once rest has already dropped below 0.30
            // (RestCategory itself doesn't even leave "Rested" until 0.28). So a
            // 0.30 "give up" threshold silently swallowed almost every
            // non-scheduled bedtime — the redirect never fired in practice.
            // Matching the proven-working 3–6 bracket's 0.25 leaves genuine
            // margin below that ~0.30 ceiling instead of sitting on top of it.
            // NEW SETTING: alwaysSeekPlushRegardlessOfRest (default OFF) skips this
            // safety valve entirely when the player opts in — see its own doc
            // comment in ModSettings.cs for the full rationale.
            if (TPE_Mod.Settings?.alwaysSeekPlushRegardlessOfRest != true
                && pawn.needs?.rest?.CurLevel <= 0.25f) return true;

            // ── Cooldown to prevent tight loops ─────────────────
            int currentTick = Find.TickManager.TicksGame;
            if (lastRedirectTick.TryGetValue(pawn.thingIDNumber, out int lastTick))
            {
                if (currentTick - lastTick < RedirectCooldownTicks) return true;
            }

            // ── Plush availability ────────────────────────────
            Thing? plush = TPE_ToyUtility.FindNearestPlush(pawn);
            if (plush == null) return true; // no plush available, sleep without it

            // ── BUGFIX: apparel-layer conflict pre-check ───────────────
            // Same reasoning as the Adult patch below: an older child essentially
            // never wears anything else on the plush's "Belt" layer today, but
            // checking here (rather than relying solely on the equip-time guard
            // in JobDriver_SeekPlushToy) costs nothing and avoids a wasted
            // fetch-and-fail round trip if a future def/mod ever gives a child
            // a Belt-layer item. Kept consistent with the Adult patch on
            // purpose — both age-group-2-and-up brackets share this check.
            if (TPE_ToyUtility.HasConflictingWornApparel(pawn, plush.def)) return true;

            // ── Perform redirect ────────────────────────────
            lastRedirectTick[pawn.thingIDNumber] = currentTick;

            // Queue plush-seeking job, preserving the original bed target so we
            // can sleep in the same bed after equipping.
            Job seekJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_SeekPlushToy, plush);
            seekJob.targetB = __instance.job.targetA;
            pawn.jobs.jobQueue.EnqueueFirst(seekJob);

            // End this sleep job before any toils are created
            __instance.EndJobWith(JobCondition.Incompletable);
            return false; // skip original SetupToils
        }

        /// <summary>
        /// Returns true if the pawn is a humanlike child aged 7–12 (inclusive) —
        /// still <see cref="DevelopmentalStage.Child"/> but past the original 3–6
        /// bracket. This is "Age Group 2" in our child-play system.
        /// </summary>
        static bool IsAgeGroup2Child(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.DevelopmentalStage != DevelopmentalStage.Child) return false;
            int age = pawn.ageTracker?.AgeBiologicalYears ?? 999;
            return age > 6; // DevelopmentalStage.Child itself caps this below 13
        }
    }

    // ==========================================
    // SLEEP JOB REDIRECT: Adult (13+) needs plush before bed
    // Mirrors the child redirects above but for adults (which, per RimWorld's
    // own lifeStageAges/LifeStageDef defaults, means DevelopmentalStage.Adult
    // starting at age 13 — the Teenager life stage never overrides the
    // developmentalStage field away from LifeStageDef's own class default of
    // Adult, so "teenager" (13–17) and "adult" (18+) are both Adult here).
    // Gated by TPE_Settings.enableAdultPlushSleep, default OFF.
    // ==========================================
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_JobDriver_LayDown_SetupToils_Adult
    {
        /// <summary>
        /// Tracks the last tick each adult was redirected so we don't spin in a
        /// tight loop if the seek-plush job keeps failing. Separate dictionary
        /// from the toddler/child patches to avoid key collisions.
        /// </summary>
        static readonly Dictionary<int, int> lastRedirectTick = new Dictionary<int, int>();
        const int RedirectCooldownTicks = 250; // ~4 seconds

        /// <summary>
        /// Harmony prefix on <see cref="JobDriver.SetupToils"/>.
        /// Intercepts LayDown jobs for adults (13+) who lack a plush toy,
        /// redirecting them to seek and equip one before sleeping.
        /// </summary>
        public static bool Prefix(JobDriver __instance)
        {
            // CRITICAL: must use base type for __instance — see the identical
            // comment on Patch_JobDriver_LayDown_SetupToils_Child above.
            if (__instance is not JobDriver_LayDown) return true;
            Pawn pawn = __instance.pawn;

            // ── Mod setting guard ─────────────────────────────────
            if (TPE_Mod.Settings?.enableAdultPlushSleep != true) return true;

            // ── BUGFIX: save-load restore guard (same as the 3–6 patch) ────
            if (TPE_ToyUtility.IsSetupToilsRestoreOrUnspawned(__instance)) return true;

            // ── Homely compat guard ────────────────────────────
            // "Intimacy - Homely" is fundamentally an ADULT-facing romance mod, so
            // this job-hijack guard is if anything MORE relevant here than for
            // the child brackets it was originally written for. Same reasoning as
            // the 3–6 patch: step aside for Homely's own undress-before-bed job so
            // it isn't hijacked mid-ritual by our plush-fetch redirect.
            if (HomelyCompat.Active
                && __instance.GetType().FullName == HomelyCompat.UndressBeforeBedTypeName)
            {
                return true;
            }

            // ── Age filter: 13+ (DevelopmentalStage.Adult) ─────────────
            if (!pawn.RaceProps.Humanlike) return true;
            if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) return true;

            // ── Already in bed ─────────────────────────────────────
            if (RestUtility.InBed(pawn)) return true;

            // ── Already has plush ────────────────────────────────
            if (TPE_ToyUtility.HasPlushEquippedOrCarried(pawn)) return true;

            // ── Rest threshold guard ─────────────────────────
            // BUGFIX: this was originally 0.35f (intended as "the most relaxed
            // threshold of any bracket"), which was even further above the
            // ceiling than the 7–12 bracket's original 0.30 — see the identical
            // BUGFIX comment on Patch_JobDriver_LayDown_SetupToils_OlderChild for
            // the full mechanism (vanilla's JobGiver_GetRest never assigns a
            // non-scheduled LayDown job above ~0.30 rest to begin with, so
            // anything at/above that value as a "give up" threshold makes the
            // redirect never fire). Matching the proven-working 3–6 bracket's
            // 0.25 restores a genuine safety margin below that ceiling. A
            // meaningful "adults are more relaxed than children" gradient simply
            // doesn't fit in the tiny numeric gap left between 0.25 and ~0.28
            // (where RestCategory itself leaves "Rested") without reintroducing
            // this exact bug, so all three non-toddler brackets now intentionally
            // share one proven-safe value instead.
            // NEW SETTING: alwaysSeekPlushRegardlessOfRest (default OFF) skips this
            // safety valve entirely when the player opts in — see its own doc
            // comment in ModSettings.cs for the full rationale.
            if (TPE_Mod.Settings?.alwaysSeekPlushRegardlessOfRest != true
                && pawn.needs?.rest?.CurLevel <= 0.25f) return true;

            // ── Cooldown to prevent tight loops ─────────────────
            int currentTick = Find.TickManager.TicksGame;
            if (lastRedirectTick.TryGetValue(pawn.thingIDNumber, out int lastTick))
            {
                if (currentTick - lastTick < RedirectCooldownTicks) return true;
            }

            // ── Plush availability ────────────────────────────
            Thing? plush = TPE_ToyUtility.FindNearestPlush(pawn);
            if (plush == null) return true; // no plush available, sleep without it

            // ── BUGFIX: apparel-layer conflict pre-check ───────────────
            // Adults very plausibly already wear something on the plush's "Belt"
            // layer (most commonly a shield belt). Without this check, an adult
            // who can never actually equip the plush (see
            // TPE_ToyUtility.HasConflictingWornApparel's doc comment for why
            // JobDriver_SeekPlushToy's own equip-time guard would otherwise still
            // safely no-op, just AFTER walking there and back) would be sent on
            // a pointless fetch-and-fail trip on every single sleep cycle,
            // forever. Checking here avoids that wasted pathing/AI churn
            // entirely — they simply sleep without a plush, exactly as if none
            // were found.
            if (TPE_ToyUtility.HasConflictingWornApparel(pawn, plush.def)) return true;

            // ── Perform redirect ────────────────────────────
            lastRedirectTick[pawn.thingIDNumber] = currentTick;

            // Queue plush-seeking job, preserving the original bed target so we
            // can sleep in the same bed after equipping.
            Job seekJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_SeekPlushToy, plush);
            seekJob.targetB = __instance.job.targetA;
            pawn.jobs.jobQueue.EnqueueFirst(seekJob);

            // End this sleep job before any toils are created
            __instance.EndJobWith(JobCondition.Incompletable);
            return false; // skip original SetupToils
        }
    }

    // ==========================================
    // SAFETY NET: prevent vanilla's automatic apparel-shopping AI
    // (JobGiver_OptimizeApparel, the "outfit policy" job giver colonists use
    // to autonomously dress themselves) from ever picking up a plush toy as
    // everyday wear. Plush toys must ONLY ever be equipped through our own
    // controlled sleep-time job (JobDriver_SeekPlushToy) — never as a random
    // "this technically fills an empty Belt slot" choice made independently
    // of bedtime. Mirrors Patch_JobGiver_OptimizeBabyApparel above, which
    // guards the opposite direction (stripping) for the base mod's own
    // optimizer. Applies to every age bracket uniformly; harmless no-op for
    // ages/settings where the plush was never wearable in the first place.
    // ==========================================
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    public static class Patch_JobGiver_OptimizeApparel_ExcludePlush
    {
        static void Postfix(ref Job __result)
        {
            if (__result?.def == JobDefOf.Wear && TPE_ToyUtility.IsPlushThing(__result.targetA.Thing))
            {
                __result = null;
            }
        }
    }

// Child play need system — ENABLED
    // ============================================================
    // CHILD PLAY NEED SYSTEM — Phase 1
    // ============================================================

    // ==========================================
    // GROWTH POINTS: Average Learning + Play
    // Replaces vanilla growth-point calculation for children (3–13)
    // so that both learning and play needs contribute to growth.
    // ==========================================
    [HarmonyPatch(typeof(Pawn_AgeTracker), "GrowthPointsPerDay", MethodType.Getter)]
    public static class Patch_Pawn_AgeTracker_GrowthPointsPerDay
    {
        public static bool Prefix(Pawn_AgeTracker __instance, ref float __result)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return true;

            // Pawn_AgeTracker stores its pawn in a private field.
            Pawn? pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || !pawn.DevelopmentalStage.Child())
                return true; // Not a child — use vanilla

            // Replicate vanilla edge-case guards exactly
            if (!ModsConfig.BiotechActive)
                return true;
            if (pawn.ParentHolder is Building_GrowthVat)
                return true; // Vanilla returns 2f for vat — let it handle this
            if (pawn.Suspended)
                return true; // Vanilla returns 0f

            Need_Learning? learning = pawn.needs?.learning;
            Need? playNeedRaw = pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay);
            Need_ChildPlay? play = playNeedRaw as Need_ChildPlay;

            // If learning need is missing or frozen, fall back to vanilla
            // (vanilla would return 0f in that case, which is correct)
            if (learning == null || learning.Suspended || play == null)
                return true;

            float weight = TPE_Mod.Settings?.playWeightInGrowth ?? 0.5f;
            float learningLevel = learning.CurLevel;
            float playLevel = play.CurLevel;

            // Weighted average: weight*play + (1-weight)*learning
            float blended = (playLevel * weight) + (learningLevel * (1f - weight));

            // Mirror vanilla's private calculation exactly
            float factor = (float)pawn.ageTracker.AgeBiologicalYears < 7f ? 0.75f : 1f;
            __result = blended * factor * pawn.ageTracker.ChildAgingMultiplier;
            return false; // Skip original
        }
    }

    // ==========================================
    // LEARNING DECAY: Slow to match play need
    // Vanilla learning decays in ~5.5 days. We slow it to the same rate
    // as play need decay (TPE_Settings.playNeedDecayDays, 6 days by default) so
    // children have time for both.
    // ==========================================
    [HarmonyPatch(typeof(Need_Learning), "NeedInterval")]
    public static class Patch_Need_Learning_NeedInterval
    {
        public static bool Prefix(Need_Learning __instance)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return true;

            // Need.pawn is protected — access via Traverse.
            Pawn? pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || !pawn.DevelopmentalStage.Child())
                return true; // Only slow decay for children

            // IsFrozen is protected on Need; Suspended is the public equivalent on Need_Learning.
            if (__instance.Suspended)
                return true; // Let vanilla handle frozen state

            // Apply the slowed decay rate instead of vanilla's 0.00045f
            float days = TPE_Mod.Settings?.playNeedDecayDays ?? 6f;
            float fallPerInterval = 1f / (days * 60000f) * 150f;
            __instance.CurLevel -= fallPerInterval;

            // Vanilla also adds growth points during NeedInterval — we MUST still do that
            if (pawn.ageTracker.canGainGrowthPoints)
            {
                pawn.ageTracker.growthPoints += pawn.ageTracker.GrowthPointsPerDay * 0.0025f;
            }

            return false; // Skip original
        }
    }

    // ==========================================
    // SCHEDULE UI: Widen the assignment button strip and draw all buttons
    // in a single row. Vanilla hardcodes a 191px-wide strip and uses a 2x2
    // We mirror vanilla's DrawTimeAssignmentSelectorGrid exactly and simply
    // insert our Learning button before Meditation. Vanilla uses rect.width/2
    // by rect.height/2 per button; it already overflows with 5 buttons — we
    // just add the 6th in the same row.
    // ==========================================

    /// <summary>
    /// Prefix that replaces vanilla's DrawTimeAssignmentSelectorGrid with an
    /// identical implementation plus one extra DrawTimeAssignmentSelectorFor
    /// call for TPE_Learning inserted before Meditation.
    /// </summary>
    [HarmonyPatch(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorGrid")]
    public static class Patch_TimeAssignmentSelector_DrawGrid_ExtraButton
    {
        public static bool Prefix(Rect rect)
        {
            // --- Identical to vanilla ---
            rect.yMax -= 2f;
            Rect rect2 = rect;
            rect2.xMax = rect2.center.x;
            rect2.yMax = rect2.center.y;

            DrawTimeAssignmentSelectorFor(rect2, TimeAssignmentDefOf.Anything);
            rect2.x += rect2.width;
            DrawTimeAssignmentSelectorFor(rect2, TimeAssignmentDefOf.Work);
            rect2.x += rect2.width;
            DrawTimeAssignmentSelectorFor(rect2, TimeAssignmentDefOf.Joy);
            rect2.x += rect2.width;
            DrawTimeAssignmentSelectorFor(rect2, TimeAssignmentDefOf.Sleep);
            rect2.x += rect2.width;

            // --- Our extra button ---
            if (TPE_TimeAssignmentDefOf.TPE_Learning != null)
            {
                DrawTimeAssignmentSelectorFor(rect2, TPE_TimeAssignmentDefOf.TPE_Learning);
                rect2.x += rect2.width;
            }

            // --- Vanilla Meditation (shifted right by one slot) ---
            if (ModsConfig.RoyaltyActive)
            {
                DrawTimeAssignmentSelectorFor(rect2, TimeAssignmentDefOf.Meditate);
            }

            return false; // Skip original
        }

        /// <summary>
        /// Exact copy of vanilla private method
        /// <see cref="TimeAssignmentSelector.DrawTimeAssignmentSelectorFor"/>.
        /// </summary>
        private static void DrawTimeAssignmentSelectorFor(Rect rect, TimeAssignmentDef ta)
        {
            rect = rect.ContractedBy(2f);
            GUI.DrawTexture(rect, ta.ColorTexture);
            if (Widgets.ButtonInvisible(rect))
            {
                TimeAssignmentSelector.selectedAssignment = ta;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            using (new TextBlock(TextAnchor.MiddleCenter))
            {
                Widgets.Label(rect, ta.LabelCap);
            }
            if (TimeAssignmentSelector.selectedAssignment == ta)
            {
                Widgets.DrawBox(rect, 2);
            }
            else
            {
                UIHighlighter.HighlightOpportunity(rect, ta.cachedHighlightNotSelectedTag);
            }
        }
    }

    // ==========================================
    // TPE_LEARNING ASSIGNMENT BEHAVIOR
    //
    // Adults / toddlers (no learning need)  → behave like "Anything"
    // Children (have learning need)         → dedicated learning mode
    //
    // Priority layout inside MainColonistBehaviorCore (highest first):
    //   Food / Autofeed          ~9.5f
    //   Chemical needs           ~9.25f
    //   Child play (urgent)       9.15f (only after shared schedule/rest disqualification)
    //   Learning (child)         ~9.1f  (boosted to 9.4f when on TPE_Learning)
    //   Hemogen (vanilla)         9.1f  (flat; urgent play goes first for one session, accepted)
    //   Meditate (Meditate sched.) 9.0f (play never enters this assignment)
    //   Child play (normal)       8.5f (disqualified whenever vanilla Rest is eligible)
    //   Rest                     ~8f
    //   Joy                      ~7f    (suppressed to 0f for children on Learning)
    //   Work                     ~5.5f  (suppressed to 0f for children on Learning)
    // ==========================================

    /// <summary>
    /// Rest patch: TPE_Learning behaves exactly like "Anything" for everyone.
    /// A tired pawn on Learning still goes to bed when rest drops below 0.3.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_GetRest), "GetPriority")]
    public static class Patch_JobGiver_GetRest_GetPriority
    {
        public static bool Prefix(JobGiver_GetRest __instance, Pawn pawn, ref float __result)
        {
            TimeAssignmentDef current = pawn.timetable?.CurrentAssignment;
            if (current != TPE_TimeAssignmentDefOf.TPE_Learning)
                return true; // Let vanilla handle

            Need_Rest? rest = pawn.needs?.rest;
            if (rest == null) { __result = 0f; return false; }

            RestCategory minCat = Traverse.Create(__instance).Field("minCategory").GetValue<RestCategory>();
            float maxPct = Traverse.Create(__instance).Field("maxLevelPercentage").GetValue<float>();

            if ((int)rest.CurCategory < (int)minCat) { __result = 0f; return false; }
            if (rest.CurLevelPercentage > maxPct) { __result = 0f; return false; }
            if (Find.TickManager.TicksGame < pawn.mindState.canSleepTick) { __result = 0f; return false; }

            Lord? lord = pawn.GetLord();
            if (lord != null && !lord.CurLordToil.AllowSatisfyLongNeeds) { __result = 0f; return false; }
            if (!RestUtility.CanFallAsleep(pawn)) { __result = 0f; return false; }

            // Learning = Anything behavior for rest
            if (rest.CurLevel < 0.3f) { __result = 8f; return false; }
            __result = 0f;
            return false;
        }
    }

    /// <summary>
    /// Work patch: Children on TPE_Learning are forbidden from work.
    /// Adults / toddlers without a learning need fall back to "Anything" behavior.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), "GetPriority")]
    public static class Patch_JobGiver_Work_GetPriority
    {
        /// <summary>Returns true when the pawn is a child that should be in learning mode.</summary>
        private static bool IsChildInLearningMode(Pawn pawn)
        {
            return pawn.learning != null && pawn.DevelopmentalStage.Child();
        }

        public static bool Prefix(Pawn pawn, ref float __result)
        {
            TimeAssignmentDef current = pawn.timetable?.CurrentAssignment;
            if (current != TPE_TimeAssignmentDefOf.TPE_Learning)
                return true; // Let vanilla handle

            if (pawn.workSettings == null || !pawn.workSettings.EverWork)
            {
                __result = 0f;
                return false;
            }

            // Children with a learning need get no work during Learning time.
            if (IsChildInLearningMode(pawn))
            {
                __result = 0f;
                return false;
            }

            // Everyone else (adults, toddlers) treats Learning like Anything.
            __result = 5.5f;
            return false;
        }
    }

    /// <summary>
    /// Joy patch: Children on TPE_Learning are forbidden from recreation.
    /// Adults / toddlers without a learning need fall back to "Anything" behavior.
    /// </summary>
    [HarmonyPatch(typeof(ThinkNode_Priority_GetJoy), "GetPriority")]
    public static class Patch_ThinkNode_Priority_GetJoy_GetPriority
    {
        /// <summary>Returns true when the pawn is a child that should be in learning mode.</summary>
        private static bool IsChildInLearningMode(Pawn pawn)
        {
            return pawn.learning != null && pawn.DevelopmentalStage.Child();
        }

        public static bool Prefix(Pawn pawn, ref float __result)
        {
            TimeAssignmentDef current = pawn.timetable?.CurrentAssignment ?? TimeAssignmentDefOf.Anything;
            if (current != TPE_TimeAssignmentDefOf.TPE_Learning)
                return true; // Let vanilla handle

            if (pawn.needs?.joy == null) { __result = 0f; return false; }
            if (Find.TickManager.TicksGame < 5000) { __result = 0f; return false; }
            if (JoyUtility.LordPreventsGettingJoy(pawn)) { __result = 0f; return false; }

            // Children with a learning need get no joy during Learning time.
            if (IsChildInLearningMode(pawn))
            {
                __result = 0f;
                return false;
            }

            // Everyone else treats Learning like Anything.
            if (pawn.needs.joy.CurLevel < 0.35f) { __result = 6f; return false; }
            __result = 0f;
            return false;
        }

        public static Exception? Finalizer(Exception? __exception, Pawn pawn, ref float __result)
        {
            if (__exception is NotImplementedException)
            {
                __result = 0f;
                return null; // Swallow the exception
            }
            return __exception; // Re-throw anything else
        }
    }

    /// <summary>
    /// Learning patch: Manages ThinkNode_Priority_Learn priority for children
    /// based on their schedule assignment.
    ///
    /// <b>During TPE_Learning:</b>  Boosts priority to 9.4f so learning clearly
    /// wins over rest (8f), play (0f), work (0f), and reload (5.9f).  Critical
    /// needs (food 9.5f, chemicals 9.25f) still take precedence.
    ///
    /// <b>During Recreation (Joy):</b>  Suppresses learning entirely when the
    /// play-need system is enabled.  Recreation is dedicated play time — a child
    /// should do play activities, not learning activities, during these hours.
    /// This mirrors how we suppress play during Learning time.
    ///
    /// <b>All other assignments:</b>  Vanilla behavior unchanged.
    /// </summary>
    [HarmonyPatch(typeof(ThinkNode_Priority_Learn), "GetPriority")]
    public static class Patch_ThinkNode_Priority_Learn_GetPriority
    {
        public static void Postfix(Pawn pawn, ref float __result)
        {
            TimeAssignmentDef current = pawn.timetable?.CurrentAssignment ?? TimeAssignmentDefOf.Anything;

            // --- Boost learning during TPE_Learning ---
            if (current == TPE_TimeAssignmentDefOf.TPE_Learning)
            {
                // Only boost for children who have an active learning need.
                if (pawn.learning == null || !pawn.DevelopmentalStage.Child())
                    return;

                // Vanilla returned 9.1f (learning not satisfied) or 0f (satisfied).
                // When satisfied we leave it at 0f — the child can rest or idle.
                if (__result > 0f)
                    __result = 9.4f;
                return;
            }

            // --- Suppress learning during Recreation when play system is active ---
            if (current == TimeAssignmentDefOf.Joy)
            {
                // Only suppress when our play-need system is enabled.
                if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                    return;

                // Only affect children who actually have the play need.
                if (!pawn.DevelopmentalStage.Child())
                    return;
                if (pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay) == null)
                    return;

                // Force learning priority to 0f so play (8.5f / urgent 9.15f) wins.
                __result = 0f;
            }
        }
    }

    /// <summary>
    /// Defense-in-depth companion to <see cref="Patch_ThinkNode_Priority_Learn_GetPriority"/>.
    ///
    /// The GetPriority postfix above already forces Learning's think-node priority to 0f
    /// during Recreation (Joy) time when the play-need system is active, which is normally
    /// sufficient on its own — <c>ThinkNode_PrioritySorter</c> skips any subnode whose
    /// <c>GetPriority</c> is ≤ 0 (see its <c>TryIssueJobPackage</c>), so
    /// <c>JobGiver_Learn.TryGiveJob</c> is never even called in that case. This prefix is a
    /// belt-and-suspenders backstop for that guarantee, mirroring this mod's own
    /// "COMPREHENSIVE TV BLOCKING" multi-layer style elsewhere in this file: if
    /// <c>TryGiveJob</c> is ever reached anyway for a Recreation-scheduled child while the
    /// play-need system is active — e.g. another mod calling it directly and bypassing the
    /// think tree's priority gate entirely — this still refuses to hand back a learning job,
    /// guaranteeing Recreation time stays play-only for children exactly like the "only
    /// capable of doing play activities" design intent requires.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Learn), "TryGiveJob")]
    public static class Patch_JobGiver_Learn_TryGiveJob_BlockDuringRecreation
    {
        public static bool Prefix(Pawn pawn, ref Job? __result)
        {
            // Play system off — vanilla behavior (Learning already usable during Joy).
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return true;

            TimeAssignmentDef current = pawn.timetable?.CurrentAssignment ?? TimeAssignmentDefOf.Anything;
            // Only Recreation time is restricted to play-only; every other assignment
            // (including TPE_Learning and Anything) is untouched by this guard.
            if (current != TimeAssignmentDefOf.Joy)
                return true;

            if (!pawn.DevelopmentalStage.Child())
                return true; // Not a child — not part of this mod's schedule split.

            // Not enrolled in the play-need system (e.g. a guest/prisoner child, or
            // the need hasn't been added for some other reason) — leave vanilla alone.
            if (pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay) == null)
                return true;

            __result = null; // Recreation time never grants a vanilla learning job.
            return false; // Skip original.
        }
    }

    /// <summary>
    /// Mutual exclusion between Play Desires and vanilla Learning Desires of the
    /// SAME activity (user-requested): a child should never simultaneously have
    /// "floor drawing" (etc.) as both an active PLAY desire and an active
    /// LEARNING desire. See <see cref="PlayDesireDef.conflictingLearningDesire"/>
    /// for the data-side activity mapping (5 pairs: FloorDrawing, NatureRunning,
    /// Radiotalking, Reading, Skydreaming — the other 5 play desires and the
    /// other 2 learning desires, Lessontaking/Workwatching, have no counterpart
    /// and are never affected) and <see cref="Pawn_PlayTracker.AddNewPlayDesire"/>
    /// for the PLAY-excludes-LEARNING direction. THIS patch is the reverse:
    /// LEARNING-excludes-PLAY.
    ///
    /// Full-body Prefix replacement of <c>Pawn_LearningTracker.AddNewLearningDesire</c>
    /// rather than a transpiler — the method is tiny (one LINQ statement plus a
    /// two-line FIFO list update) and the only change from vanilla is one extra
    /// <c>Where()</c> clause, so reimplementing it verbatim plus that one clause
    /// is far simpler and more maintainable than rewriting the LINQ predicate's
    /// IL in place. <c>ActiveLearningDesires</c> returns the tracker's actual
    /// backing list (a reference type), so mutating it here mutates the real
    /// private field with no reflection required.
    ///
    /// ── Same-tick race safety ──────────────────────────────────────────────
    /// RimWorld ticks are processed strictly sequentially (there is no true
    /// concurrency in the simulation), so even if a pawn's Play-desire refresh
    /// (driven by Need_ChildPlay.NeedInterval, every 150 ticks) and their
    /// Learning-desire refresh (driven by Pawn.TickRare, every 250 ticks)
    /// happen to both expire on the exact same absolute game tick, they still
    /// execute one after the other, never simultaneously. Whichever one runs
    /// SECOND on that tick always reads the OTHER tracker's just-updated,
    /// fully-current state (never a stale snapshot from before the tick
    /// started) — so checking the live state at the moment of selection, on
    /// BOTH sides, is sufficient to prevent a double-up with no additional
    /// locking, ordering, or "pending pick" bookkeeping needed.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_LearningTracker), nameof(Pawn_LearningTracker.AddNewLearningDesire))]
    public static class Patch_Pawn_LearningTracker_AddNewLearningDesire_ExcludeActivePlayDesires
    {
        public static bool Prefix(Pawn_LearningTracker __instance)
        {
            // Feature is inert while the child play-need system is off — no Play
            // tracker will ever have anything active in that case, so let vanilla
            // run unmodified rather than paying for the dictionary lookup below.
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return true; // run original vanilla method

            Pawn pawn = __instance.Pawn;
            List<LearningDesireDef> active = __instance.ActiveLearningDesires;

            // Non-creating peek: NEVER lazily creates a Pawn_PlayTracker just to
            // find it empty — see TryGetTracker's own doc comment for why
            // GetTracker (which does create) would be wrong here.
            Pawn_PlayTracker playTracker = Current.Game?.GetComponent<GameComponent_PlayTracker>()?.TryGetTracker(pawn);
            List<PlayDesireDef> activePlayDesires = playTracker?.ActivePlayDesires;

            // Identical to vanilla's own filter (!active.Contains && CanGiveDesire)
            // plus exactly one extra clause: exclude any LearningDesireDef that's
            // the mapped counterpart of one of the pawn's currently active PLAY
            // desires.
            LearningDesireDef item = DefDatabase<LearningDesireDef>.AllDefsListForReading
                .Where(ld => !active.Contains(ld)
                    && ld.Worker.CanGiveDesire
                    && !ConflictsWithActivePlayDesire(ld, activePlayDesires))
                .RandomElementByWeight(ld => ld.selectionWeight);

            // Defensive: vanilla's own RandomElementByWeight logs an error and
            // returns null/default if the filtered sequence is ever empty. This
            // can't normally happen here — Lessontaking and Workwatching have no
            // Play counterpart, so at least those 2 of the 7 total learning
            // desires can never be excluded by this filter — but never write a
            // null into the active list regardless of how unlikely.
            if (item == null)
                return false; // skip original; nothing changes this cycle.

            if (active.Count >= 2)
                active.RemoveAt(0);
            active.Add(item);

            return false; // skip original — fully replaced above.
        }

        /// <summary>
        /// True if any of <paramref name="activePlayDesires"/> maps (via
        /// <see cref="PlayDesireDef.conflictingLearningDesire"/>) to
        /// <paramref name="learningDesire"/>.
        /// </summary>
        private static bool ConflictsWithActivePlayDesire(LearningDesireDef learningDesire,
            List<PlayDesireDef>? activePlayDesires)
        {
            if (activePlayDesires.NullOrEmpty())
                return false;
            // Same false-positive as PlayUtility.ConflictsWithActiveLearningDesire:
            // NullOrEmpty() already proved non-null/non-empty above, the analyzer
            // just can't see it through an unannotated extension method.
            for (int i = 0; i < activePlayDesires!.Count; i++)
            {
                if (activePlayDesires[i].conflictingLearningDesire == learningDesire)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Idle-joy patch: Prevents children on TPE_Learning from doing idle joy
    /// (wandering to a joy activity) when no learning job is available.
    /// Without this, a child with joy below 0.9 would still do idle joy even
    /// though regular joy was suppressed via GetPriority.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_IdleJoy), "TryGiveJob")]
    public static class Patch_JobGiver_IdleJoy_TryGiveJob
    {
        public static bool Prefix(Pawn pawn, ref Job? __result)
        {
            TimeAssignmentDef current = pawn.timetable?.CurrentAssignment ?? TimeAssignmentDefOf.Anything;
            if (current != TPE_TimeAssignmentDefOf.TPE_Learning)
                return true; // Let vanilla handle

            // Only block idle joy for children who have a learning need.
            if (pawn.learning != null && pawn.DevelopmentalStage.Child())
            {
                __result = null; // Skip idle joy — child will wander instead.
                return false;
            }

            return true; // Adults / toddlers fall through to vanilla.
        }
    }

    // ==========================================
    // NEEDS TRACKER: Gate TPE_ChildPlay need behind the toggle.
    // The NeedDef has NO developmentalStageFilter so vanilla never auto-adds it.
    // This postfix manually adds the need when the toggle is ON and the pawn
    // is a child colonist, and removes it when the toggle is OFF.
    // ==========================================
    /// <summary>
    /// Gates TPE_ChildPlay need behind the toggleable mod setting.
    /// The NeedDef has NO developmentalStageFilter, so vanilla never auto-adds it.
    /// This postfix manually adds the need when the toggle is ON and the pawn
    /// is a child colonist, and removes it when the toggle is OFF.
    ///
    /// CRITICAL FIX FOR RESURRECTION BUG:
    /// When a child is resurrected, AddOrRemoveNeedsAsAppropriate may be called
    /// multiple times before the pawn's needs list is properly synchronized.
    /// Vanilla's AddNeed() method does NOT deduplicate — it unconditionally adds
    /// to the list. This postfix now:
    /// 1. Checks the needs list DIRECTLY for all instances of the need (not just
    ///    via TryGetNeed, which only returns the first one).
    /// 2. Removes any duplicates, keeping the first one.
    /// 3. Only adds a new need if ZERO instances are found.
    /// This prevents multiple identical play need bars from appearing in the needs tab.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "AddOrRemoveNeedsAsAppropriate")]
    public static class Patch_Pawn_NeedsTracker_AddOrRemoveNeedsAsAppropriate
    {
        public static void Postfix(Pawn_NeedsTracker __instance)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null)
                return;

            NeedDef needDef = TPE_ChildPlayDefOf.TPE_ChildPlay;
            if (needDef == null)
                return;

            // Direct access to the needs list (private List<Need> field).
            // This allows us to detect and remove duplicates that TryGetNeed cannot see.
            List<Need>? needsList = Traverse.Create(__instance).Field("needs").GetValue<List<Need>>();
            if (needsList == null)
                return;

            // Count how many instances of this need exist in the list.
            int instanceCount = 0;
            int firstInstanceIndex = -1;
            for (int i = 0; i < needsList.Count; i++)
            {
                if (needsList[i]?.def == needDef)
                {
                    instanceCount++;
                    if (firstInstanceIndex == -1)
                        firstInstanceIndex = i; // Remember the first occurrence
                }
            }

            // If multiple instances exist (resurrection bug), remove all duplicates.
            // Remove in reverse order to avoid index shifting.
            if (instanceCount > 1)
            {
                for (int i = needsList.Count - 1; i >= 0; i--)
                {
                    if (i != firstInstanceIndex && needsList[i]?.def == needDef)
                    {
                        needsList.RemoveAt(i);
                    }
                }
                instanceCount = 1; // Now we're back to having exactly one
            }

            // Determine whether this pawn should have the play need.
            bool shouldHave = TPE_Mod.Settings?.enableChildrenPlayNeed == true
                && pawn.DevelopmentalStage.Child()
                && pawn.Faction != null
                && pawn.Faction.IsPlayer;

            // Add the need if it should exist but doesn't.
            if (shouldHave && instanceCount == 0)
            {
                Traverse.Create(__instance).Method("AddNeed", needDef).GetValue();
            }
            // Remove the need if it shouldn't exist but does.
            else if (!shouldHave && instanceCount > 0)
            {
                // Use RemoveNeed to properly clean up.
                Traverse.Create(__instance).Method("RemoveNeed", needDef).GetValue();
            }
        }
    }

    // ==========================================
    // MAP COMPONENT: Register god-mode needs tracker on every player map.
    // ==========================================
    [HarmonyPatch(typeof(Map), "FinalizeInit")]
    public static class Patch_Map_FinalizeInit
    {
        public static void Postfix(Map __instance)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return;

            if (!__instance.IsPlayerHome) return;
            var existing = __instance.GetComponent<MapComponent_GodModeNeeds>();
            if (existing == null)
            {
                __instance.components.Add(new MapComponent_GodModeNeeds(__instance));
            }
        }
    }

    // ==========================================
    // MAP COMPONENT: Register post-wake plush drop tracker.
    // Ensures toddlers automatically drop their plush toys after getting
    // out of bed, so colonists can haul them back to the toy box.
    // ==========================================
    [HarmonyPatch(typeof(Map), "FinalizeInit")]
    public static class Patch_Map_FinalizeInit_ToyDrop
    {
        public static void Postfix(Map __instance)
        {
            if (!__instance.IsPlayerHome) return;
            var existing = __instance.GetComponent<MapComponent_ToddlerPostWakeToyDrop>();
            if (existing == null)
            {
                __instance.components.Add(new MapComponent_ToddlerPostWakeToyDrop(__instance));
            }
        }
    }

    // ==========================================
    // SUPPRESS LEARNING GAIN DURING PLAY JOBS
    // When a child is doing one of our play activities, vanilla
    // LearningTickCheckEnd must not increase their learning need.
    // Our play jobs fill play need, not learning need.
    // ==========================================
    [HarmonyPatch(typeof(LearningUtility), "LearningTickCheckEnd")]
    public static class Patch_LearningUtility_LearningTickCheckEnd_SuppressDuringPlay
    {
        public static bool Prefix(Pawn pawn)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return true;

            JobDef? curJobDef = pawn.CurJob?.def;
            if (curJobDef != null && IsPlayJob(curJobDef))
            {
                // Still end the job if learning is somehow already satisfied,
                // but do NOT add any learning need.
                if (LearningUtility.LearningSatisfied(pawn))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
                return false; // Skip original
            }
            return true;
        }

        static bool IsPlayJob(JobDef def)
        {
            return def == TPE_JobDefOf.TPE_ChildReadBook
                || def == TPE_JobDefOf.TPE_ChildPlayWithToy
                || def == TPE_JobDefOf.TPE_ChildFloorDrawing
                || def == TPE_JobDefOf.TPE_ChildWatchTelevision
                || def == TPE_JobDefOf.TPE_ChildBuildDirtCastles
                || def == TPE_JobDefOf.TPE_ChildNatureRunning
                || def == TPE_JobDefOf.TPE_ChildSkydreaming
                || def == TPE_JobDefOf.TPE_ChildRadiotalking;
        }
    }

    // ==========================================
    // CLASSROOM BUFF (Path A): Hook lesson-end tick check.
    // LearningTickCheckEnd returns true when the child has learned enough.
    // ==========================================
    [HarmonyPatch(typeof(LearningUtility), "LearningTickCheckEnd")]
    public static class Patch_LearningUtility_LearningTickCheckEnd
    {
        public static void Postfix(Pawn pawn, int delta, bool forced, bool __result)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return;

            // __result is true when the lesson ended (LearningSatisfied)
            if (!__result) return;

            // Only apply to children
            if (!pawn.DevelopmentalStage.Child()) return;

            // Check if pawn is in a Classroom room
            Room? room = pawn.GetRoom();
            if (room == null || room.Role == null) return;
            if (room.Role.defName != "Classroom") return;

            // Apply the classroom learning buff
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(
                TPE_ThoughtDefOf.TPE_LearnedInClassroom);
        }
    }

    // ==========================================
    // CLASSROOM BUFF (Path B): Backup via job finish action.
    // When a Lessontaking job ends successfully, check the room.
    // This catches edge cases where Path A might miss.
    // ==========================================
    [HarmonyPatch(typeof(JobDriver_Lessontaking), "MakeNewToils")]
    public static class Patch_JobDriver_Lessontaking_MakeNewToils
    {
        public static void Postfix(JobDriver_Lessontaking __instance)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return;

            __instance.AddFinishAction(delegate (JobCondition condition)
            {
                if (condition != JobCondition.Succeeded) return;

                Pawn pawn = __instance.pawn;
                if (!pawn.DevelopmentalStage.Child()) return;

                Room? room = pawn.GetRoom();
                if (room?.Role?.defName != "Classroom") return;

                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(
                    TPE_ThoughtDefOf.TPE_LearnedInClassroom);
            });
        }
    }

    // ==========================================
    // GIZMO: Add play-need bar below learning in child growth-tier gizmo.
    // When a child has the TPE_ChildPlay need and the feature is enabled,
    // the vanilla Gizmo_GrowthTier is redrawn with three rows:
    // Growth Tier (top), Learning (middle), Play (bottom).
    // ==========================================
    // BUGFIX: RimWorld's own startup-safety scanner flagged this class
    // ("Type Patch_Gizmo_GrowthTier_GizmoOnGUI probably needs a
    // StaticConstructorOnStartup attribute") because it holds Texture2D
    // fields. SolidColorMaterials.NewSolidColorTexture constructs a real
    // Unity Texture2D under the hood, which must happen on the main thread
    // once assets are ready — [StaticConstructorOnStartup] guarantees that,
    // matching the same convention every other texture-holding static class
    // in this file already follows.
    [HarmonyPatch(typeof(Gizmo_GrowthTier), "GizmoOnGUI")]
    [StaticConstructorOnStartup]
    public static class Patch_Gizmo_GrowthTier_GizmoOnGUI
    {
        /// <summary>Orange fill texture for the play-need bar.</summary>
        private static readonly Texture2D PlayBarTex =
            SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.55f, 0.15f));

        /// <summary>Empty bar texture matching vanilla style.</summary>
        private static readonly Texture2D EmptyBarTex =
            SolidColorMaterials.NewSolidColorTexture(GenUI.FillableBar_Empty);

        /// <summary>
        /// Total gizmo height when the play-need bar is shown.
        /// Vanilla is 75f (2 rows). We add a third row of the same height
        /// plus the same 4f gap that vanilla uses between rows.
        /// </summary>
        const float ThreeRowHeight = 107f;

        public static bool Prefix(Gizmo_GrowthTier __instance, Vector2 topLeft,
            float maxWidth, GizmoRenderParms parms, ref GizmoResult __result)
        {
            Pawn child = Traverse.Create(__instance).Field("child").GetValue<Pawn>();
            if (child == null)
                return true;

            Need_ChildPlay? playNeed = child.needs?.TryGetNeed(
                TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
            if (playNeed == null || TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return true; // Vanilla draw when play need is absent or disabled

            float width = __instance.GetWidth(maxWidth);
            // Shift the entire gizmo UP so its bottom edge aligns with where a
            // vanilla 75f gizmo would end, keeping it flush with the Draft button.
            float drawY = topLeft.y + 75f - ThreeRowHeight;
            Rect rect = new Rect(topLeft.x, drawY, width, ThreeRowHeight);
            Rect inner = rect.ContractedBy(8f);

            // Vanilla layout: inner height 59f split into 2 rows of 27.5f
            // with a 4f gap (2f trimmed from each row).
            // We replicate the same row height and gap for 3 rows.
            float rowH = (inner.height - 8f) / 3f; // 8f = two 4f gaps
            Rect rTier  = new Rect(inner.x, inner.y,                    inner.width, rowH);
            Rect rLearn = new Rect(inner.x, rTier.yMax  + 4f,           inner.width, rowH);
            Rect rPlay  = new Rect(inner.x, rLearn.yMax + 4f,           inner.width, rowH);

            Widgets.DrawWindowBackground(rect);

            // Re-use vanilla draw methods via reflection
            Traverse.Create(__instance).Method("DrawGrowthTier", rTier).GetValue();
            Traverse.Create(__instance).Method("DrawLearning", rLearn).GetValue();

            DrawPlayBar(rPlay, playNeed);

            __result = new GizmoResult(GizmoState.Clear);
            return false; // Skip original
        }

        /// <summary>
        /// Draws the play-need bar in the exact same style as vanilla DrawLearning.
        /// Label on the left (55% width), fillable bar on the right with percentage
        /// text centered inside it.
        /// </summary>
        static void DrawPlayBar(Rect rect, Need_ChildPlay playNeed)
        {
            Rect labelRect = rect;
            labelRect.xMax = rect.x + rect.width * 0.55f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, playNeed.def.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;

            Rect barRect = rect;
            barRect.xMin = labelRect.xMax;
            barRect.yMin += 2f;
            barRect.yMax -= 2f;

            Widgets.FillableBar(barRect, playNeed.CurLevelPercentage,
                PlayBarTex, EmptyBarTex, doBorder: true);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, playNeed.CurLevelPercentage.ToStringPercent());
            Text.Anchor = TextAnchor.UpperLeft;

            TooltipHandler.TipRegion(rect, playNeed.GetTipString());
        }
    }

    // ==========================================
    // GIZMO TOOLTIP: Modify growth-tier tooltip to mention play
    // When child play system is enabled, replace the vanilla
    // learning-only description with one that mentions both needs.
    // ==========================================
    [HarmonyPatch(typeof(Gizmo_GrowthTier), "GrowthTierTooltip")]
    public static class Patch_Gizmo_GrowthTier_GrowthTierTooltip
    {
        public static void Postfix(Gizmo_GrowthTier __instance, ref string __result)
        {
            Pawn child = Traverse.Create(__instance).Field("child").GetValue<Pawn>();
            if (child == null) return;

            Need_ChildPlay? playNeed = child.needs?.TryGetNeed(
                TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
            if (playNeed == null || TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return;

            // Replace the colorized vanilla description with our colorized
            // version so the swap works regardless of language.
            string vanillaDesc = "StatsReport_GrowthTierDesc".Translate()
                .Colorize(ColoredText.SubtleGrayColor);
            string playDesc = "TPE_StatsReport_GrowthTierDesc".Translate()
                .Colorize(ColoredText.SubtleGrayColor);
            __result = __result.Replace(vanillaDesc, playDesc);
        }
    }
// End child play need system

    // ============================================================
    // CRIB LOCK SYSTEM
    // ============================================================

    // ==========================================
    // TODDLER SELF-ESCAPE: Allow/block JobGiver_LeaveCrib (with Crib Gate & Sleep support)
    // When a toddler is crib-locked they must not leave on their own.
    // When a toddler is sleeping, they cannot leave regardless of gate state.
    // When a toddler's crib gate is open, crawlers can escape (like walkers).
    // Medical rest is the only exception to crib lock.
    // ==========================================

    // ==========================================
    // CRAWLERS WITH OPEN GATE: Bypass base mod's crawler block
    // The base Toddlers mod blocks all crawlers from escaping via JobGiver_LeaveCrib.GetPriority().
    // This PREFIX intercepts that check and allows crawlers with an OPEN crib gate to escape,
    // giving them the same escape priority (2f) as walking toddlers.
    // ==========================================
    [HarmonyPatch]
    public static class Patch_JobGiver_LeaveCrib_GetPriority_CrawlerGateOverride
    {
        static System.Reflection.MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("Toddlers.JobGiver_LeaveCrib"), "GetPriority");

        /// <summary>
        /// PREFIX: Intercept the base mod's crawler block and allow escape if gate is open.
        /// 
        /// ARCHITECTURE NOTE:
        /// This PREFIX works in conjunction with Patch_JobGiver_LeaveCrib_TryGiveJob.
        /// GetPriority determines IF a job giver should even try to create a job (priority -1 = "no").
        /// TryGiveJob creates the actual escape job. This PREFIX handles the "should we try?" question.
        /// 
        /// The base mod's GetPriority returns -1 for all crawlers, preventing them from ever
        /// attempting to leave their cribs. This prefix checks if the crawler's crib gate is open,
        /// and if so, skips the base method entirely and assigns walker-equivalent escape priority (2f).
        /// 
        /// Execution order:
        /// 1. If crawler with open gate: Set priority to 2f (walker default), return false (skip base method)
        /// 2. If crawler with closed gate: Return true (use base method, which will block with -1f)
        /// 3. If not a crawler: Return true (use base method, normal walker behavior)
        /// 
        /// This PREFIX is NOT tied to debug logging — the Log.Message calls are conditional but
        /// do not affect control flow. The system works identically with or without logging enabled.
        /// 
        /// [DEBUG] When debugLogging is ON, logs each step of the check.
        /// </summary>
        public static bool Prefix(Pawn pawn, ref float __result)
        {
            bool debug = TPE_Mod.Settings?.debugLogging ?? false;

            // STEP 1: Check if pawn is a crawler
            bool isCrawler = ToddlerLearningUtility.IsCrawler(pawn);
            if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} checking if crawler: {isCrawler}");
            if (!isCrawler)
            {
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} is not a crawler - delegating to base method");
                return true;  // Not a crawler - let base method handle normally
            }

            // STEP 2: Check if crawler is in a crib
            Building_Bed currentCrib = CribUtility.GetCurrentCrib(pawn);
            bool inCrib = currentCrib != null && currentCrib.def.defName == "Crib";
            if (debug)
            {
                string cribInfo = currentCrib == null ? "null" : $"{currentCrib.def.defName} at {currentCrib.Position}";
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} current crib: {cribInfo}");
            }
            if (!inCrib)
            {
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} not in a crib - delegating to base method");
                return true;  // Not in a crib - let base method handle
            }

            // STEP 3: Check gate component existence
            MapComponent_CribGate gateComponent = pawn.Map?.GetComponent<MapComponent_CribGate>();
            if (debug)
            {
                string componentStatus = gateComponent == null ? "NOT FOUND" : "found";
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} gate component: {componentStatus}");
            }
            if (gateComponent == null)
            {
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} no gate component on map - delegating to base method");
                return true;  // Component missing - let base method block crawler
            }

            // STEP 4: Check if gate is open
            bool gateIsOpen = gateComponent.IsCribGateOpen(currentCrib);
            if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} crib gate is open: {gateIsOpen}");
            if (!gateIsOpen)
            {
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} gate is closed - delegating to base method (will block)");
                return true;  // Gate is closed - let base method block crawler
            }

            // STEP 5: CRAWLER WITH OPEN GATE - Determine escape priority based on confinement tier
            // 
            // When enableConfinementPriorityScaling is OFF, all crawlers get priority 2f (vanilla behavior).
            // When ON, priority escalates as confinement builds, representing increasing desperation to escape.
            // CRITICAL: If the "WantsOutOfCrib" mental break is active, priority RESETS to 2f (panic mode).
            // The toddler has worked themselves into such a frenzy that they forget the gate exists—
            // they prioritize crying/panic over strategic escape.
            
            bool scalingEnabled = TPE_Mod.Settings?.enableConfinementPriorityScaling ?? false;
            if (!scalingEnabled)
            {
                // Feature disabled (or setting is null / Settings object missing): use default walker priority
                __result = 2f;
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} GATE OPEN - priority scaling DISABLED, using default 2f");
                return false;
            }

            // Feature enabled: check for mental break first (highest override)
            // CRITICAL ASSUMPTION: TPE_HediffDefOf.TPE_WantsOutOfCrib must be loaded (should always be true,
            // as it's part of this mod's HediffDefs). If for some reason the hediff def fails to load,
            // the HasHediff call will simply return false (safe fallback).
            if (pawn.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib) == true)
            {
                // Mental break active: priority RESETS to 2f (panic mode, forgot the gate exists)
                __result = 2f;
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} mental break ACTIVE - priority RESET to 2f (panic mode)");
                return false;
            }

            // No mental break: check confinement hediff severity for tier-based scaling
            // Tiers divide the 0-1 severity range into three equal thirds:
            //   Tier 1 (Restless):   0.00–0.33 → Priority 2.0f (baseline)
            //   Tier 2 (Frustrated): 0.34–0.66 → Priority 3.0f (escalated)
            //   Tier 3 (Desperate):  0.67–1.00 → Priority 4.5f (highly eager)
            // These thresholds (0.34, 0.67) are intentional thirds and do NOT correspond to hediff
            // stage boundaries (0.001, 0.5, 1.0). We use thirds for smoother priority escalation.
            Hediff confinementHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(TPE_HediffDefOf.TPE_CribConfinement);
            float priority = 2f;  // Default: Tier 1 (Restless)

            if (confinementHediff != null)
            {
                float severity = confinementHediff.Severity;
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} confinement severity: {severity:F2}");

                // Determine priority tier based on severity thresholds
                if (severity >= 0.67f)
                {
                    priority = 4.5f;  // Tier 3: Desperate (on verge of mental break)
                    if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} Tier 3 (Desperate, 0.67+) - priority 4.5f");
                }
                else if (severity >= 0.34f)
                {
                    priority = 3.0f;  // Tier 2: Frustrated
                    if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} Tier 2 (Frustrated, 0.34–0.66) - priority 3.0f");
                }
                else
                {
                    priority = 2.0f;  // Tier 1: Restless
                    if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} Tier 1 (Restless, 0–0.33) - priority 2.0f");
                }
            }
            else if (debug)
            {
                if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} no confinement hediff found - using default priority 2f");
            }

            __result = priority;
            if (debug) Log.Message($"[TPE Crib Gate] {pawn.Name} GATE OPEN - escape priority set to {priority}f (skipping base method)");
            return false;  // Skip base method - we've set the priority ourselves
        }
    }

    /// <summary>
    /// DELETED: The Postfix was redundant with TryGiveJob PREFIX.
    /// TryGiveJob PREFIX already handles all guard checks (sleep, lock, gate).
    /// Having both caused timing/interference issues when debug logging was disabled.
    /// The PREFIX on GetPriority alone is sufficient to override the crawler block.
    /// The PREFIX on TryGiveJob applies all safety checks before job creation.
    /// </summary>

    [HarmonyPatch]
    public static class Patch_JobGiver_LeaveCrib_TryGiveJob
    {
        static System.Reflection.MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("Toddlers.JobGiver_LeaveCrib"), "TryGiveJob");

        /// <summary>
        /// PREFIX: Implements critical checks (sleep, lock, gate) before base JobGiver
        /// generates an escape job. All three gates must pass for the job to be created.
        /// 
        /// ARCHITECTURE NOTE:
        /// This PREFIX works in conjunction with Patch_JobGiver_LeaveCrib_GetPriority_CrawlerGateOverride.
        /// GetPriority determines IF a job giver should try (via priority). TryGiveJob creates the actual job.
        /// This PREFIX handles the "can we create the job safely?" question by checking three conditions:
        /// 1. Sleep check: Sleeping pawns don't escape
        /// 2. Crib lock check: Locked toddlers don't escape (except medical)
        /// 3. Crawler + gate check: Crawlers with closed gates don't escape
        /// 
        /// The crawler check in TryGiveJob is belt-and-suspenders with GetPriority PREFIX:
        /// - GetPriority blocks crawlers with closed gates at the priority level
        /// - TryGiveJob blocks them again at job creation level (safety redundancy)
        /// - If both somehow fail, the base method would also block crawlers, so system is fail-safe
        /// 
        /// This PREFIX is NOT tied to debug logging — the Log.Message calls are conditional but
        /// do not affect control flow. The system works identically with or without logging enabled.
        /// 
        /// [DEBUG] When debugLogging is ON, logs each check result.
        /// </summary>
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            bool debug = TPE_Mod.Settings?.debugLogging ?? false;

            // SLEEP CHECK: Sleeping toddlers cannot escape (no escape job)
            if (!pawn.Awake())
            {
                if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} is sleeping - blocking escape job");
                __result = null;
                return false;
            }
            if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} is awake - proceeding");

            // CRIB LOCK CHECK: Locked toddlers cannot escape (unless medical emergency)
            if (CribLockUtility.IsCribLockedAndNotMedical(pawn))
            {
                if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} is crib-locked - blocking escape job");
                __result = null;
                return false;
            }
            if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} is not crib-locked - proceeding");

            // CRIB GATE CHECK: Block crawlers with CLOSED gates BEFORE calling base method
            // Base mod's JobGiver_LeaveCrib has its own crawler check that blocks all crawlers.
            // We must intercept that BEFORE base method runs, not after.
            bool isCrawler = ToddlerLearningUtility.IsCrawler(pawn);
            if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} is crawler: {isCrawler}");

            if (isCrawler)
            {
                Building_Bed currentCrib = CribUtility.GetCurrentCrib(pawn);
                if (currentCrib != null && currentCrib.def.defName == "Crib")
                {
                    MapComponent_CribGate gateComponent = pawn.Map?.GetComponent<MapComponent_CribGate>();
                    bool gateOpen = gateComponent != null && gateComponent.IsCribGateOpen(currentCrib);
                    if (debug)
                    {
                        string componentStatus = gateComponent == null ? "missing" : "found";
                        if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} gate component: {componentStatus}, gate open: {gateOpen}");
                    }

                    // CRITICAL: If crawler with closed gate, block BEFORE base method runs
                    if (!gateOpen)
                    {
                        if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} is a crawler with CLOSED gate - blocking escape job (skipping base method)");
                        __result = null;
                        return false;  // Skip base method entirely
                    }

                    if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} is a crawler with OPEN gate - allowing base mod to generate escape job");
                }
            }

            // All checks passed - let base mod run normally
            if (debug) Log.Message($"[TPE Crib Gate TryGiveJob] {pawn.Name} passed all gates - delegating to base mod");
            return true;
        }
    }

    // ==========================================
    // ADULT PLAY: Block play givers that move the toddler
    // BabyPlayGiver_PlayWalking and PlayToys carry the toddler away.
    // For locked toddlers only PlayStatic (in-place) is allowed.
    // ==========================================
    [HarmonyPatch(typeof(BabyPlayGiver_PlayWalking), "CanDo")]
    public static class Patch_BabyPlayGiver_PlayWalking_CanDo
    {
        public static void Postfix(Pawn pawn, Pawn other, ref bool __result)
        {
            if (__result && CribLockUtility.IsCribLockedAndNotMedical(other))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(BabyPlayGiver_PlayToys), "CanDo")]
    public static class Patch_BabyPlayGiver_PlayToys_CanDo
    {
        public static void Postfix(Pawn pawn, Pawn baby, ref bool __result)
        {
            if (__result && CribLockUtility.IsCribLockedAndNotMedical(baby))
                __result = false;
        }
    }

    // ==========================================
    // FLOAT MENU: Hide "Let out of crib" for locked toddlers
    // Prevents players from manually ordering colonists to extract a
    // crib-locked toddler.
    // ==========================================
    [HarmonyPatch]
    public static class Patch_FloatMenuOptionProvider_LetOutOfCrib_GetSingleOptionFor
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.FloatMenuOptionProvider_LetOutOfCrib");
            return AccessTools.Method(type, "GetSingleOptionFor", new System.Type[]
            {
                typeof(Pawn),
                typeof(RimWorld.FloatMenuContext)
            });
        }

        public static void Postfix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (__result == null) return;
            if (CribLockUtility.IsCribLockedAndNotMedical(clickedPawn))
                __result = null;
        }
    }

    // ==========================================
    // CRIB GATE GIZMO: Toggle "Open Crib Gate" on selected cribs
    // Adds a gizmo to cribs allowing players to open/close the crib gate.
    // When open, crawling toddlers can escape like walking toddlers.
    // ==========================================
    [HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
    public static class Patch_Building_Bed_GetGizmos_CribGate
    {
        /// <summary>
        /// Adds a "Open Crib Gate" toggle gizmo to all crib buildings.
        /// 
        /// The gizmo allows players to control toddler escape permissions:
        /// - Open gate: Crawlers can escape (same permissions as walkers)
        /// - Closed gate: Crawlers cannot escape (default, base mod behavior)
        /// 
        /// Uses TexCommand.ForbidOff/ForbidOn icons (same as crib lock) for consistency.
        /// </summary>
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance)
        {
            // Yield all existing gizmos first
            if (__result != null)
            {
                foreach (Gizmo g in __result)
                    yield return g;
            }

            // Only add gizmo if this is a crib building
            if (__instance == null || __instance.def.defName != "Crib")
                yield break;

            // Get the gate component from the map
            MapComponent_CribGate gateComponent = __instance.Map?.GetComponent<MapComponent_CribGate>();
            if (gateComponent == null)
                yield break;

            // Create the toggle gizmo
            // Note: Using ForbidOff/ForbidOn for consistency with crib lock gizmo
            yield return new Command_Toggle
            {
                // Label stays same regardless of state (like the plan specifies)
                defaultLabel = "TPE_CribGateLabel".Translate(),
                defaultDesc = "TPE_CribGateTooltip".Translate(),
                
                // Icon: Use ForbidOff icon (consistent with crib lock gizmo)
                // The icon will show forbid-off appearance when gate is closed
                icon = TexCommand.ForbidOff,
                
                // Click sound
                activateSound = SoundDefOf.Checkbox_TurnedOn,
                
                // Dynamic state: read current gate state
                // isActive = true means gate is OPEN (active)
                // isActive = false means gate is CLOSED (inactive)
                isActive = () => gateComponent.IsCribGateOpen(__instance),
                
                // Action: toggle the gate and show message
                toggleAction = () => ToggleCribGate(__instance, gateComponent)
            };
        }

        /// <summary>
        /// Toggles the crib gate and displays a message to the player.
        /// </summary>
        private static void ToggleCribGate(Building_Bed crib, MapComponent_CribGate gateComponent)
        {
            if (crib == null || gateComponent == null) return;

            // Toggle the gate and get new state
            bool newState = gateComponent.ToggleCribGate(crib);

            // Show message (using localization keys from plan)
            string messageKey = newState ? "TPE_CribGateOpened" : "TPE_CribGateClosed";
            Messages.Message(messageKey.Translate(), crib, MessageTypeDefOf.TaskCompletion);
        }
    }

    // ==========================================
    // EVENT-TRIGGERED TODDLER MOODS — ONE-TIME Harmony sources
    // (the polled ONGOING/LINGERING moods live in MapComponent_ToddlerEventMoods;
    //  never source the same ThoughtDef from both — see Architecture.md §5.2).
    // Every patch here applies through MapComponent_ToddlerEventMoods'
    // ApplyToAwakeToddlers / RefreshMemory helpers, so "which toddlers react"
    // has exactly one definition.
    // ==========================================

    /// <summary>
    /// "Someone went away forever..." — every awake toddler on the map where a
    /// colonist died.
    ///
    /// BUGFIX: this used to be a Postfix that read <c>__instance.Map</c>. Pawn.Kill
    /// calls DeSpawnOrDeselect() (the corpse replaces the pawn), so by the time any
    /// postfix runs the pawn is unspawned and <c>Map</c> is ALWAYS null — the mood
    /// could never fire. The Prefix now captures the map (MapHeld, so a colonist
    /// dying while carried / in a casket on the map still counts) and "was a living
    /// colonist" BEFORE the kill; the Postfix applies only if the pawn really died.
    /// Prefix and Postfix share <c>__state</c>, so they MUST stay in this one class
    /// (Harmony keys __state by the patch method's declaring type).
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_ColonistDeathEvent
    {
        public static void Prefix(Pawn __instance, out Map? __state)
        {
            __state = null;
            if (TPE_Mod.Settings?.enableEventTriggeredMoods != true) return;
            if (__instance.Dead || !__instance.IsColonist) return; // IsColonist: player-faction humanlike (incl. secure slaves), not subhuman
            __state = __instance.MapHeld;
        }

        public static void Postfix(Pawn __instance, Map? __state)
        {
            if (__state == null || !__instance.Dead) return;
            // The dead pawn is not in the toddler list (Dead/unspawned), so a dying
            // toddler never mourns itself; surviving toddlers do.
            MapComponent_ToddlerEventMoods.ApplyToAwakeToddlers(__state, TPE_ThoughtDefOf.TPE_Event_ColonistDied);
        }
    }

    // ==========================================
    // INCIDENT-BASED TODDLER MOODS
    // Catches one-shot incidents and applies mood reactions immediately.
    // ==========================================
    [HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
    public static class Patch_IncidentWorker_TryExecute_ToddlerMoods
    {
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result) return;
            if (TPE_Mod.Settings?.enableEventTriggeredMoods != true) return; // null-safe (Architecture §2.9)

            if (!(parms?.target is Map map)) return;

            // "People are getting sick!" — BUGFIX: this mood used to fire only when
            // the TODDLER ITSELF caught a hediff whose name looked like a disease,
            // which contradicts its own text ("Everyone's coughing... I hope I don't
            // get sick too!"). It is now driven by the actual human disease-outbreak
            // incident (IncidentWorker_DiseaseHuman — flu, plague, malaria, sleeping
            // sickness, gut worms, muscle parasites, mechanites, ...; animal diseases
            // excluded). TryExecute also returns true when every victim was immune,
            // so we confirm someone in the colony actually has the disease first.
            if (__instance is IncidentWorker_DiseaseHuman)
            {
                if (AnyColonyHumanlikeHas(map, __instance.def.diseaseIncident))
                    MapComponent_ToddlerEventMoods.ApplyToAwakeToddlers(map, TPE_ThoughtDefOf.TPE_Event_DiseaseOutbreak);
                return;
            }

            string defName = __instance.def.defName;

            // Mechanoid raid detection (base RaidEnemy incident rolling the Mechanoid
            // faction). "MechCluster" (Royalty — a dormant mech cluster awakening) is a
            // separate IncidentDef entirely and is handled in the switch below.
            if (defName == "RaidEnemy" && parms.faction != null && parms.faction.def.defName == "Mechanoid")
            {
                ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_MechanoidRaid);
                return;
            }

            switch (defName)
            {
                // BUGFIX: added DeepDrillInfestation and WastepackInfestation
                // (Biotech) — both burst hostile bugs out of the ground just like the
                // base Infestation incident but were previously uncovered, so
                // toddlers never reacted to them. (Ideology's "Infestation_Jelly" is
                // deliberately NOT included here — despite the name, it's a friendly
                // gift incident, not a threat.)
                case "Infestation":
                case "DeepDrillInfestation":
                case "WastepackInfestation":
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_Infestation);
                    break;
                // BUGFIX: added MechCluster (Royalty) — a dormant mechanoid cluster
                // waking up and attacking is just as scary as a RaidEnemy mech raid.
                case "MechCluster":
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_MechanoidRaid);
                    break;
                case "WandererJoin":
                case "StrangerInBlackJoin":
                    // BUGFIX (double mood): the wanderer is already a free colonist
                    // on the map by now. Pre-register them in the known-colonist
                    // registry so MapComponent_ToddlerEventMoods' generic "A new
                    // person came to play!" (+5) doesn't ALSO fire for the same
                    // person on its next check, stacked on this +4. (The earlier
                    // registry rewrite didn't stop this: a wanderer genuinely IS a
                    // never-seen-before colonist.)
                    Current.Game?.GetComponent<GameComponent_KnownColonists>()?.RegisterColonistsOn(map);
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_WandererJoins);
                    break;
                case "ResourcePodCrash":
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_ResourcePodCrash);
                    break;
                case "MeteoriteImpact":
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_MeteoriteImpact);
                    break;
                case "ThrumboPasses":
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_ThrumbosPassing);
                    break;
                case "VisitorGroup":
                case "TravelerGroup":
                // BUGFIX: was misspelled "WanderersSkylanterns" (trailing "s") — the
                // real IncidentDef is "WanderersSkylantern", so this case was
                // unreachable dead code and the Ideology skylantern-wanderers event
                // never applied TPE_Event_VisitorsArrived.
                case "WanderersSkylantern":
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_VisitorsArrived);
                    break;
                case "TraderCaravanArrival":
                case "OrbitalTraderArrival":
                    ApplyToAll(map, TPE_ThoughtDefOf.TPE_Event_TraderCaravan);
                    break;
            }
        }

        /// <summary>Thin forwarder to the shared helper (awake, living, spawned toddlers; allocation-free).</summary>
        private static void ApplyToAll(Map map, ThoughtDef thoughtDef)
        {
            MapComponent_ToddlerEventMoods.ApplyToAwakeToddlers(map, thoughtDef);
        }

        /// <summary>
        /// True if any colonist/prisoner on the map (the exact victim pool of
        /// IncidentWorker_DiseaseHuman for a map target) now has the disease hediff.
        /// Runs once per disease incident — cost irrelevant.
        /// </summary>
        private static bool AnyColonyHumanlikeHas(Map map, HediffDef? disease)
        {
            if (disease == null) return false;
            List<Pawn> pawns = map.mapPawns.FreeColonistsAndPrisoners;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i].health?.hediffSet?.HasHediff(disease) == true) return true;
            }
            return false;
        }
    }

    // ==========================================
    // PRISON BREAK TODDLER MOOD — REMOVED (BUGFIX)
    // ==========================================
    // A Postfix on PrisonBreakUtility.StartPrisonBreak used to apply
    // TPE_Event_PrisonBreak as a ONE-TIME mood while MapComponent_ToddlerEventMoods
    // ALSO maintained it as an ONGOING mood (LordJob_PrisonBreak present) — the
    // ongoing/one-time double-sourcing Architecture.md forbids. The lord check in
    // the component is now the sole source (the lord exists from the moment the
    // break starts until every escapee is recaptured, killed or gone).

    // ==========================================
    // TODDLER BIRTHDAY MOOD
    // ==========================================
    /// <summary>
    /// "It's my birthday!" — BUGFIX: this used to be polled by comparing a toddler's
    /// age with the age last recorded for them. The Toddlers mod's toddler life stage
    /// begins EXACTLY at age 1, so a toddler was never recorded at age 0 and the 1st
    /// birthday could never be detected (only the 2nd ever fired). Hooking vanilla's
    /// own birthday callback fires at the exact moment, for every birthday inside
    /// the toddler age span (1 and 2 for humans; HAR races via Toddlers'
    /// ToddlerMinAge/ToddlerEndAge). Age range is used instead of IsToddler because
    /// at the 1st birthday the life stage may not have switched to toddler yet.
    ///
    /// Applied even if the toddler is asleep — it's their own birthday and lasts a
    /// day (the old poll effectively gave it on waking, too). World pawns are
    /// skipped (BirthdayBiological also runs for mothballed world pawns); toddlers
    /// on a map (incl. carried) or travelling in a caravan get it.
    ///
    /// Private target → string name; Prepare() verifies it exists so a future
    /// rename only disables THIS patch instead of failing PatchAll for the whole mod.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Patch_Pawn_AgeTracker_BirthdayBiological_ToddlerBirthday
    {
        static bool Prepare()
        {
            bool found = AccessTools.Method(typeof(Pawn_AgeTracker), "BirthdayBiological", new[] { typeof(int) }) != null;
            if (!found) Log.Warning("[TPE] Pawn_AgeTracker.BirthdayBiological(int) not found — toddler birthday mood disabled.");
            return found;
        }

        public static void Postfix(Pawn ___pawn, int birthdayAge)
        {
            if (TPE_Mod.Settings?.enableEventTriggeredMoods != true) return;
            Pawn pawn = ___pawn;
            if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike) return;
            if (pawn.MapHeld == null && !RimWorld.Planet.CaravanUtility.IsCaravanMember(pawn)) return;
            // A vat-grown baby ages ~20x inside a Biotech growth vat; it isn't having
            // a birthday party in there. (No DLC guard needed: the type lives in
            // Assembly-CSharp; without Biotech nothing is ever held by one.)
            if (pawn.ParentHolder is Building_GrowthVat) return;
            // Toddlers (and babies about to become toddlers) are DevelopmentalStage.Baby.
            if (pawn.DevelopmentalStage != DevelopmentalStage.Baby) return;
            if (birthdayAge < Toddlers.ToddlerUtility.ToddlerMinAge(pawn) ||
                birthdayAge >= Toddlers.ToddlerUtility.ToddlerEndAge(pawn)) return;
            MapComponent_ToddlerEventMoods.RefreshMemory(pawn, TPE_ThoughtDefOf.TPE_Event_Birthday);
        }
    }

    // ==========================================
    // ANIMAL TAMED TODDLER MOOD
    // ==========================================
    // TargetMethod is required here because DoRecruit has two overloads:
    //   DoRecruit(Pawn, Pawn, bool) and DoRecruit(Pawn, Pawn, out string, out string, bool, bool)
    // Using nameof() without parameter types caused AmbiguousMatchException which crashed
    // the entire HarmonyPatches static constructor and disabled ALL patches in the mod.
    //
    // BUGFIX: this used to patch the 3-param overload, but real taming never goes
    // through it — InteractionWorker_RecruitAttempt.Interacted calls the 6-param
    // overload DIRECTLY (only debug tools, TendUtility and Pawn_MindState use the
    // 3-param one, which itself just forwards to the 6-param one). The mood could
    // only ever fire from the dev-mode "recruit" tool. Patching the 6-param overload
    // covers every path exactly once. The out-string params need MakeByRefType(),
    // which can't be expressed in a [HarmonyPatch] attribute (see harmony lore), so
    // TargetMethod() resolves it at runtime; Prepare() turns a future signature
    // change into a single disabled patch instead of a failed PatchAll.
    [HarmonyPatch]
    public static class Patch_InteractionWorker_DoRecruit_AnimalTamed
    {
        static System.Reflection.MethodBase? TargetMethod()
        {
            return AccessTools.Method(typeof(InteractionWorker_RecruitAttempt), "DoRecruit",
                new System.Type[]
                {
                    typeof(Pawn), typeof(Pawn),
                    typeof(string).MakeByRefType(), typeof(string).MakeByRefType(),
                    typeof(bool), typeof(bool)
                });
        }

        static bool Prepare()
        {
            bool found = TargetMethod() != null;
            if (!found) Log.Warning("[TPE] InteractionWorker_RecruitAttempt.DoRecruit (6-param) not found — animal-tamed toddler mood disabled.");
            return found;
        }

        public static void Postfix(Pawn recruitee)
        {
            if (TPE_Mod.Settings?.enableEventTriggeredMoods != true) return;
            if (recruitee == null || recruitee.RaceProps.Humanlike) return; // humans = recruitment, not taming
            if (recruitee.Faction != Faction.OfPlayer) return;              // the tame actually took
            MapComponent_ToddlerEventMoods.ApplyToAwakeToddlers(recruitee.MapHeld, TPE_ThoughtDefOf.TPE_Event_AnimalTamed);
        }
    }



    // ══════════════════════════════════════════════════════════════════════
    //  ACCESSIBILITY: MUTE CRYING SOUNDS
    // ══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Prefix on <see cref="SubEffecter_Sustainer.SubEffecterTick"/>.
    /// Silently suppresses crying sound sustainers when the corresponding
    /// mute accessibility setting is enabled. Visual effects (tears, motes)
    /// are NOT affected — only the audio is muted.
    ///
    /// Covers:
    ///   • All enabled child crying: <c>TPE_ChildCryingSound</c> → gated by <c>muteChildCryingSound</c>
    ///
    /// Does NOT cover baby/toddler crying — that uses <see cref="SubEffecter_SoundTriggered"/>
    /// (one-shot on effecter spawn) and is handled by <see cref="Patch_SubEffecter_SoundTriggered_SubTrigger_MuteBabyCrying"/>.
    ///
    /// Why a prefix on SubEffecter_Sustainer instead of muting the SoundDef
    /// or removing the stateEffecter? Because the stateEffecter also drives
    /// visual tear/mote particles that we want to keep. A prefix here is the
    /// surgical way to kill only audio while preserving visuals.
    /// </summary>
    [HarmonyPatch(typeof(SubEffecter_Sustainer), "SubEffectTick")]
    public static class Patch_SubEffecter_Sustainer_SubEffecterTick_MuteCrying
    {
        public static bool Prefix(SubEffecter_Sustainer __instance)
        {
            TPE_Settings? settings = TPE_Mod.Settings;
            if (settings == null)
                return true; // Settings not ready yet — let sound play.

            // Fast path: child mute is off — skip all checks.
            if (!settings.muteChildCryingSound)
                return true;

            // Identify the sound this sustainer would play.
            SoundDef? sound = __instance.def?.soundDef;
            if (sound == null)
                return true; // No sound attached — nothing to mute.

            // Child crying (our custom sound)
            if (sound.defName == "TPE_ChildCryingSound")
            {
                return !settings.muteChildCryingSound;
            }

            // Any other sustainer: pass through unchanged.
            return true;
        }
    }

    /// <summary>
    /// Prefix on <see cref="SubEffecter_SoundTriggered.SubTrigger"/>.
    /// Silently suppresses the one-shot baby-crying sound that plays when
    /// the <c>BabyCrying</c> effecter is spawned (vanilla baby crying and
    /// our <c>TPE_WantsOutOfCrib</c> mental state). Visual effects are NOT
    /// affected — only the audio is muted.
    /// </summary>
    [HarmonyPatch(typeof(SubEffecter_SoundTriggered), "SubTrigger")]
    public static class Patch_SubEffecter_SoundTriggered_SubTrigger_MuteBabyCrying
    {
        public static bool Prefix(SubEffecter_SoundTriggered __instance)
        {
            TPE_Settings? settings = TPE_Mod.Settings;
            if (settings == null)
                return true;

            if (!settings.muteBabyCryingSound)
                return true;

            SoundDef? sound = __instance.def?.soundDef;
            if (sound == null)
                return true;

            if (sound.defName == "BabyCrying")
                return false; // Skip the one-shot sound.

            return true;
        }
    }

    // ==========================================
    // CANCEL TODDLER PLAY JOBS ON AGE-UP TO CHILD
    // When a toddler's life stage changes from Baby to Child, immediately
    // cancel any toddler-specific play jobs and clear them from the queue.
    // Prevents toddlers from continuing to skydream, play with toys, etc.
    // after they've aged up and no longer have toddler AI/animations.
    // ==========================================
    [HarmonyPatch(typeof(LifeStageWorker_HumanlikeChild), "Notify_LifeStageStarted")]
    public static class Patch_LifeStageWorker_HumanlikeChild_Notify_LifeStageStarted
    {
        public static void Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (previousLifeStage == null) return;
            // Only act when transitioning FROM baby/toddler stage
            if (!previousLifeStage.developmentalStage.Baby()) return;

            // End current job if it's a toddler play activity
            Job? curJob = pawn.jobs?.curJob;
            if (curJob != null && IsToddlerPlayJob(curJob.def))
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Clear queued toddler play jobs
            pawn.jobs?.jobQueue?.RemoveAll(pawn, job => IsToddlerPlayJob(job.def));
        }

        static bool IsToddlerPlayJob(JobDef def)
        {
            if (def == null) return false;
            string n = def.defName;
            // Base Toddlers mod jobs
            if (n == "ToddlerSkydreaming") return true;
            if (n == "ToddlerFloordrawing") return true;
            if (n == "ToddlerWatchTelevision") return true;
            if (n == "ToddlerPlayToys") return true;
            // Our expanded toddler play jobs
            if (n.StartsWith("TPE_Toddler")) return true;
            return false;
        }
    }

    // ==========================================
    // SUPPRESS BODY-WORN PLUSH WHILE SLEEPING (CHILDREN + ADULTS ONLY —
    // deliberately EXCLUDES toddlers, see BUGFIX below)
    // While a sleeping child (3–6, 7–12) or adult is wearing a plush, the
    // normal apparel render node would draw it flat on their body (Belt
    // layer / Torso bodypart group). We suppress that draw and let
    // Patch_PawnRenderer_RenderPawnAt_DrawSleepingPlush (below) draw a
    // properly positioned replacement instead — or nothing at all, if the
    // player has the "render plush while sleeping" setting off.
    //
    // HISTORY: this used to be scoped to children 3–6 ONLY, because the
    // "draw near head" replacement below used to be permanently disabled
    // (see that patch's own history note) — suppressing without a
    // replacement for the other brackets would have made their plush
    // invisible too, so they were deliberately left unsuppressed (showing
    // the flat on-body graphic) as the lesser evil. Once the replacement
    // renderer started working, this was WIDENED to apply to all four
    // brackets uniformly — including toddlers.
    //
    // BUGFIX (user-reported, screenshot-confirmed): that widening was wrong
    // for toddlers specifically. Unlike the other three brackets, a sleeping
    // toddler's body is NOT hidden/reduced while lying down — their plush
    // already rendered correctly via the normal on-body apparel path before
    // any of this rendering system existed. Suppressing it and replacing it
    // with the head-position redraw actively made toddlers look WORSE (the
    // reference screenshot shows the plush awkwardly overlapping the
    // toddler's own body instead of sitting naturally beside them). Toddlers
    // are now explicitly excluded from suppression — their plush is once
    // again drawn exactly the way vanilla's own apparel-render pipeline
    // already draws it, completely untouched by this system. This also
    // means TPE_Settings.renderPlushWhileSleeping has NO EFFECT on toddlers
    // either way — see the exclusion in the redraw patch below for the
    // other half of this fix.
    // ==========================================
    [HarmonyPatch(typeof(PawnRenderNode_Apparel), "GraphicsFor")]
    public static class Patch_PawnRenderNode_Apparel_GraphicsFor_SuppressSleepingPlush
    {
        static IEnumerable<Graphic> Postfix(IEnumerable<Graphic> __result, PawnRenderNode_Apparel __instance, Pawn pawn)
        {
            // Defensive: if vanilla returns null, pass it through unchanged.
            if (__result == null)
                yield break;

            // NOTE: deliberately NOT gated on any settings toggle here. Bracket
            // eligibility (enableChildPlushSleep / enableOlderChildPlushSleep /
            // enableAdultPlushSleep) already controls whether a pawn could ever
            // have EQUIPPED a plush in the first place; if they're physically
            // wearing one and asleep, we always suppress the flat on-body draw
            // regardless of whether that bracket's toggle is still on (toggling
            // a setting off mid-game shouldn't strip an already-worn item's
            // sleep rendering inconsistently with its equip state — see
            // TPE_ToyUtility.IsAsleepInBed's doc comment).
            //
            // TODDLER EXCLUSION (see class doc comment above): toddlers'
            // sleeping bodies are never hidden, so their worn plush already
            // renders correctly without any help from this system — checked
            // FIRST (cheapest possible reject) via the exact same
            // TPE_ToyUtility.IsToddler check the equip-redirect patch itself
            // uses, so "is this pawn in the toddler bracket" can never
            // disagree between equip-time and render-time.
            if (__instance.apparel != null && TPE_ToyUtility.IsPlushDef(__instance.apparel.def)
                && !TPE_ToyUtility.IsToddler(pawn)
                && TPE_ToyUtility.IsAsleepInBed(pawn))
            {
                yield break;
            }
            foreach (Graphic g in __result)
                yield return g;
        }
    }

    // ==========================================
    // DRAW PLUSH NEXT TO A SLEEPING PAWN'S HEAD (CHILDREN + ADULTS ONLY —
    // deliberately EXCLUDES toddlers, see the TODDLER EXCLUSION comment
    // inside the Postfix below for the full rationale)
    // Draws the plush's own plain "dropped on the ground" graphic (NOT the
    // worn/apparel graphic) at a fixed offset from the pawn's head end of
    // the bed, for every NON-TODDLER sleeping bracket that currently has one
    // equipped. Gated behind TPE_Settings.renderPlushWhileSleeping (default
    // ON) — when OFF, the plush is simply hidden while asleep for those
    // brackets (the suppression patch above still applies to them either
    // way, so it's never shown flat-on-body as a fallback). Toddlers are
    // entirely unaffected by this setting in either state — their plush is
    // always the plain, un-suppressed, un-repositioned on-body apparel
    // render, exactly as vanilla's own pipeline already draws it.
    //
    // HISTORY: an earlier version of this patch resolved the plush's WORN,
    // body-type-specific graphic on demand via
    // ApparelGraphicRecordGetter.TryGetGraphicApparel (which internally calls
    // GraphicDatabase.Get&lt;Graphic_Multi&gt;, mutating a shared, non-thread-safe
    // cache) directly from this render hook, and it was disabled after
    // causing "concurrent collection" exceptions. This version never touches
    // that API at all — it reads a plain Graphic_Single that was fully
    // resolved once at startup (see HarmonyPatches' static constructor and
    // TPE_ToyUtility.PlushDecorativeGraphics), so there is nothing here that
    // can race with anything, on any thread, ever.
    //
    // YAYO'S ANIMATIONS COMPATIBILITY: the position below is computed from
    // this method's own `drawLoc` PARAMETER (the pawn's overall draw-phase
    // position, exactly as RenderPawnAt receives it) plus the BED's fixed
    // Rot4 rotation — never from the pawn's actual rendered head node, and
    // never from anything Yayo's Animation (com.yayo.yayoAni.continued)
    // patches. Decompiling that mod confirms it only ever adds its own
    // per-frame offsets inside PawnRenderer.GetBodyPos (position) and
    // PawnRenderer.BodyAngle (rotation) — both of which feed the pawn's own
    // body-mesh transform inside the render TREE, not the `drawLoc` argument
    // RenderPawnAt is called with. Since we never read GetBodyPos's result,
    // BodyAngle's result, or the head node's transform, Yayo's walk/aim/combat
    // animations (which don't apply to a sleeping, LayingInBed pawn anyway)
    // can never move our plush even if it were active for a sleeping pawn.
    // ==========================================
    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    public static class Patch_PawnRenderer_RenderPawnAt_DrawSleepingPlush
    {
        /// <summary>
        /// Forward offset (along the bed's head-facing direction) and side offset
        /// (90° clockwise from that direction) that together place the plush
        /// beside the pawn's head/pillow, matching the position the user specified
        /// via a reference screenshot (up and to the right of the head, roughly at
        /// pillow height). Tunable constants — nudge these if the in-game result
        /// needs adjusting; they're independent of bed size or pawn body type.
        /// </summary>
        const float HeadOffsetForward = 0.12f;
        const float HeadOffsetSide = 0.32f;

        /// <summary>One-time warning flag so a genuinely unexpected failure logs once, never spams every frame.</summary>
        static bool hasLoggedError;

        public static void Postfix(Vector3 drawLoc, Pawn ___pawn)
        {
            // Cheapest possible checks first — this postfix runs once per visible
            // pawn EVERY FRAME, so the overwhelming common case (not our concern at
            // all) must bail out before touching apparel lists or the bed lookup.
            if (TPE_Mod.Settings?.renderPlushWhileSleeping != true) return;
            Pawn pawn = ___pawn;
            if (pawn?.apparel?.WornApparel == null) return;

            // TODDLER EXCLUSION (BUGFIX, user-reported + screenshot-confirmed):
            // toddlers must NEVER go through this redraw. Their sleeping body is
            // not hidden/reduced the way the other three brackets' bodies are, so
            // their worn plush already renders correctly via the normal apparel
            // path with zero help from this system — drawing a SECOND copy beside
            // their head produced a visibly wrong doubled-up/overlapping result.
            // Uses the exact same TPE_ToyUtility.IsToddler check the equip-redirect
            // patch (Patch_JobDriver_LayDown_SetupToils) itself uses, so "is this
            // pawn a toddler" can never disagree between equip-time and
            // render-time. Checked before the (slightly more expensive) sleep
            // check below since it's a hard bracket exclusion, not a soft gate.
            if (TPE_ToyUtility.IsToddler(pawn)) return;

            if (!TPE_ToyUtility.IsAsleepInBed(pawn)) return;

            // Find the worn plush with an INDEX-based loop, not a foreach/enumerator.
            // This is the exact same list Pawn_ApparelTracker.Wear/Remove mutate —
            // indexing avoids any possibility of an "enumeration operation may not
            // execute" InvalidOperationException if apparel ever changes reentrantly
            // on this same thread mid-frame. A stale read (missed or extra item) is
            // harmless here since we only draw for a single frame at a time anyway.
            List<Apparel> worn = pawn.apparel.WornApparel;
            Apparel plush = null;
            for (int i = 0; i < worn.Count; i++)
            {
                if (TPE_ToyUtility.IsPlushDef(worn[i].def))
                {
                    plush = worn[i];
                    break;
                }
            }
            if (plush == null) return;

            if (!TPE_ToyUtility.TryGetPlushDecorativeGraphic(plush.def, out Graphic graphic)) return;

            // BUGFIX (caught during mandated review, never actually live-tested
            // before): use the bed's own authoritative head/foot SLOT positions
            // (BedUtility.GetSleepingSlotPos/GetFeetSlotPos, the exact same source
            // vanilla itself uses to know where a sleeper's head goes) to derive
            // the head direction, rather than reasoning about it from
            // Rot4.FacingCell directly. A first draft of this method assumed
            // "FacingCell points from foot toward head", copied forward from the
            // original (disabled-before-ever-being-tested) predecessor patch's own
            // comment — but BedUtility.GetSlotPos's actual switch statement shows
            // the opposite: for every one of the 4 Rot4 values, the head slot sits
            // on the FacingCell.Opposite side of the bed's footprint. Deriving the
            // direction directly from the two authoritative slot positions instead
            // of re-reasoning about Rot4 semantics means this can never silently
            // point the plush at the pawn's FEET, regardless of which rotation is
            // ever fed in — correct by construction, not by (fallible) inspection.
            Building_Bed bed = RestUtility.CurrentBed(pawn, out int? sleepingSlot);
            if (bed == null || sleepingSlot == null) return;
            IntVec3 headCell = bed.GetSleepingSlotPos(sleepingSlot.Value);
            IntVec3 footCell = bed.GetFootSlotPos(sleepingSlot.Value);
            Vector3 headDir = (headCell - footCell).ToVector3();
            if (headDir == Vector3.zero)
            {
                // Degenerate case: a 1x1 bed (e.g. the Biotech Crib, <size>(1,1)</size>)
                // has an identical head slot and foot slot — GetSlotPos's CellRect
                // math collapses to the same cell for both, so there's no
                // game-defined "which way does the sleeper's head point" for these.
                // Fall back to the BED'S OWN facing direction (which does at least
                // rotate correctly when the player rotates the crib, unlike a fixed
                // constant) rather than always assuming North regardless of how the
                // crib is actually placed.
                headDir = bed.Rotation.FacingCell.ToVector3();
            }
            headDir = headDir.normalized;

            // sideDir is headDir rotated 90° clockwise in the XZ ground plane,
            // giving a consistent "to the right of the head" direction regardless
            // of which of the 4 cardinal ways the bed ends up facing.
            Vector3 sideDir = new Vector3(headDir.z, 0f, -headDir.x);
            Vector3 plushPos = drawLoc + headDir * HeadOffsetForward + sideDir * HeadOffsetSide;
            // Draw at a slightly higher altitude than the pawn's own body layer so
            // the plush never gets sorted underneath the pawn/bed graphics.
            plushPos.y = drawLoc.y + 0.02f;

            // BUGFIX (user-reported, screenshot-confirmed): passing bed.Rotation
            // as the rot argument here was WRONG and is what caused the plush to
            // render upside-down. Graphic_Single.ShouldDrawRotated defaults to true
            // (our plush ThingDefs never set <drawRotated>false</drawRotated>), so
            // Graphic.DrawWorker's AngleFromRot(rot) actually SPINS the flat sprite
            // mesh around the vertical axis by rot.AsAngle degrees (North=0,
            // East=90, South=180, West=270) — it is NOT simply "which directional
            // variant to pick" the way it would be for a multi-facing Graphic_Multi.
            // Since beds default-place at Rot4.South (BedBase's
            // <defaultPlacingRot>South</defaultPlacingRot>), every plush was being
            // rotated a full 180°, i.e. drawn upside-down, for the most common bed
            // orientation in the game — exactly matching the reported bug and the
            // reference screenshot. The plush is a single fixed decorative icon
            // (like a sticker placed next to the pillow), not a directional sprite
            // that should visually "face" the bed's placement rotation — WHERE it
            // sits is already fully handled by headDir/sideDir above; its own visual
            // orientation should stay constant regardless of bed rotation. Passing
            // Rot4.North (AsAngle == 0) makes AngleFromRot return exactly 0, i.e. no
            // rotation at all, so the plush always renders upright as authored.
            try
            {
                graphic.Draw(plushPos, Rot4.North, plush);
            }
            catch (Exception ex)
            {
                // Defensive net around the one external draw call in this method —
                // this exact call site has a history of causing hard-to-diagnose
                // rendering exceptions (see the class doc comment above). If
                // something unforeseen still goes wrong, log it ONCE rather than
                // flooding the log every frame, and never let it interrupt drawing
                // the rest of the scene.
                if (!hasLoggedError)
                {
                    hasLoggedError = true;
                    Log.Warning($"[TPE] Sleeping-plush render failed once for {pawn} — suppressing further identical logs this session. {ex}");
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELD-TOY TILT — toy sword / magnifying glass carried at 45° (user-requested)
    // ═══════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Draws a carried toy sword or magnifying glass tilted 45° when the carrier
    /// is a toddler or child, so it reads as "held in the hand" instead of standing
    /// bolt-upright (both sprites are authored vertically: blade/lens up, handle
    /// down). Covers every toddler/child activity that holds them — Monster Hunt,
    /// Carry Toy, Play With Toy, bug watching with the glass, child toy play —
    /// because they all hold the item through <c>Pawn_CarryTracker</c>, and vanilla
    /// draws every carried thing through this one method (from
    /// PawnRenderNodeWorker_Carried, plus PawnFlyer for a pawn mid-jump).
    ///
    /// Direction (user-confirmed): leans toward the way the pawn faces — top of
    /// the sprite to the right when facing east, mirrored to the left when facing
    /// west, and to the right when facing the camera or away. On RimWorld's
    /// top-down camera a positive rotation about the Y axis is clockwise on screen
    /// (Rot4.East.AsAngle == 90 turns a north-pointing sprite to the right), so
    /// +45 leans right and -45 leans left.
    ///
    /// Adults (hauling the toy to a stockpile, etc.) keep the vanilla upright look
    /// — user-confirmed: "adults are not playing with it". Toddlers are
    /// DevelopmentalStage.Baby (the Toddlers mod's life stage), children are
    /// DevelopmentalStage.Child.
    ///
    /// How: vanilla is <c>CalculateCarriedDrawPos(...)</c> then
    /// <c>carriedThing.DrawNowAt(loc, flip)</c>, which has no rotation argument.
    /// For our two defs only, the prefix runs the SAME position maths (so other
    /// mods' changes to CalculateCarriedDrawPos and any job driver's
    /// ModifyCarriedThingDrawPos still apply) and then draws through
    /// <c>Graphic.Draw(loc, Rot4.North, thing, extraRotation)</c> — vanilla's own
    /// extra-rotation path (Graphic.DrawWorker multiplies the quaternion by
    /// Euler(up * extraRotation)). What the skipped DrawNowAt → ThingWithComps.DrawAt
    /// would additionally have done is nothing for these defs: their only comps
    /// (CompForbiddable, CompToyCategory) don't draw, and the silhouette call is
    /// kept below. The vanilla "flip" output is ignored on purpose: only the
    /// eating/drinking drivers set it (JobDriver_Ingest / _SocialRelax, which carry
    /// food, never a toy), and for these vertical sprites a flip would draw them
    /// upside-down anyway.
    ///
    /// PERFORMANCE: this method runs every frame for every visible pawn carrying
    /// anything (haulers included). The first two lines are a null check and two
    /// static-field reference compares, so every other carried item costs
    /// essentially nothing extra and goes straight to vanilla.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawCarriedThing))]
    public static class Patch_PawnRenderUtility_DrawCarriedThing_TiltHeldToys
    {
        /// <summary>Tilt angle in degrees (user-requested 45°).</summary>
        public const float TiltDegrees = 45f;

        /// <summary>
        /// Set after the first failure: from then on the tilt is skipped for the rest of
        /// the session and vanilla draws the item upright. Prevents a throw + catch every
        /// frame (exceptions are expensive) and doubles as the one-time log guard.
        /// </summary>
        private static bool tiltDisabled;

        // Parameter names match the original exactly (pawn, drawLoc, carriedThing) —
        // Harmony binds by name.
        public static bool Prefix(Pawn pawn, Vector3 drawLoc, Thing carriedThing)
        {
            // ── Hot-path early outs (every other carried item) ──
            if (carriedThing == null) return true;
            ThingDef def = carriedThing.def;
            if (def != TPE_ThingDefOf.TPE_ToySword && def != TPE_ThingDefOf.TPE_MagnifyingGlass) return true;
            if (pawn == null || tiltDisabled) return true;

            // Toddlers (Baby stage) and children only — adults keep vanilla.
            DevelopmentalStage stage = pawn.DevelopmentalStage;
            if (stage != DevelopmentalStage.Baby && stage != DevelopmentalStage.Child) return true;

            try
            {
                // Same position maths as vanilla (honours job-driver overrides and
                // other mods' patches to CalculateCarriedDrawPos). `flip` ignored — see summary.
                PawnRenderUtility.CalculateCarriedDrawPos(pawn, carriedThing, ref drawLoc, out bool _);

                float angle = pawn.Rotation == Rot4.West ? -TiltDegrees : TiltDegrees;
                carriedThing.Graphic.Draw(drawLoc, Rot4.North, carriedThing, angle);

                // Parity with Thing.DrawAt (no-op unless the player uses the
                // "silhouettes" highlight style).
                SilhouetteUtility.DrawGraphicSilhouette(carriedThing, drawLoc);
                return false; // drawn — skip vanilla's upright draw
            }
            catch (Exception ex)
            {
                tiltDisabled = true; // never retry this session — vanilla draws from now on
                Log.Warning($"[TPE] Tilted held-toy draw failed for {pawn} ({def.defName}); held toys will be drawn upright (vanilla) for the rest of this session. {ex}");
                return true; // let vanilla draw it upright rather than not at all
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SELF SOOTHING — Integration patches
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures the base Toddlers mod recognises Self Soothing as a valid
    /// play activity. Without this, <see cref="ToddlerPlayUtility.IsToddlerPlaying"/>
    /// returns false for toddlers performing Self Soothing, which can break
    /// downstream systems that rely on the play-status check.
    /// </summary>
    [HarmonyPatch(typeof(ToddlerPlayUtility), "IsToddlerPlaying")]
    public static class Patch_ToddlerPlayUtility_IsToddlerPlaying
    {
        static void Postfix(Pawn toddler, ref bool __result)
        {
            if (!__result && toddler?.jobs?.curDriver is JobDriver_ToddlerSelfSoothing)
                __result = true;
        }
    }


    /// <summary>
    /// Mood-weighted replacement: when Self Soothing is eligible but was NOT
    /// the activity randomly selected by <see cref="JobGiver_ToddlerPlay"/>,
    /// there is a chance to swap to Self Soothing based on the toddler's
    /// current mood percentage.
    ///
    /// Replace chance = (1 - moodPercentage) × 0.25
    ///   100% mood → 0% replacement
    ///   50%  mood → 12.5% replacement
    ///   0%   mood → 25% replacement
    /// </summary>
    [HarmonyPatch]
    public static class Patch_JobGiver_ToddlerPlay_TryGiveJob_SelfSoothing
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.JobGiver_ToddlerPlay");
            return AccessTools.Method(type, "TryGiveJob", new System.Type[] { typeof(Pawn) });
        }

        static void Postfix(Pawn pawn, ref Job __result)
        {
            // No job was found at all — nothing to replace.
            if (__result == null) return;

            // Already self-soothing — no need to replace.
            // (Reference compare against the bound JobDef — same meaning as the old defName
            // string compare, without the string comparison on every toddler play job.)
            if (__result.def == TPE_JobDefOf.TPE_ToddlerSelfSoothing) return;

            // Feature disabled in mod settings.
            if (TPE_Mod.Settings?.enableSelfSoothing != true) return;

            // Bound once at startup (TPE_ToddlerPlayDefOf); was a DefDatabase lookup per call.
            ToddlerPlayDef? playDef = TPE_ToddlerPlayDefOf.TPE_ToddlerSelfSoothing;
            if (playDef == null) return;

            // Self Soothing not eligible for this toddler right now.
            // (No sleep guard needed: this out-of-crib JobGiver is never reached in the
            // "must keep lying down" branch, and it only reaches here when the base
            // already produced a job for this toddler.)
            if (!playDef.Worker.CanDo(pawn)) return;

            float moodPct = pawn.needs?.mood?.CurLevelPercentage ?? 1f;
            float replaceChance = (1f - moodPct) * 0.25f;

            if (Rand.Value < replaceChance)
            {
                Job? job = playDef.Worker.TryGiveJob(pawn);
                if (job != null)
                    __result = job;
            }
        }
    }

    /// <summary>
    /// BUG WATCHING MAGNIFYING GLASS ENHANCEMENT
    /// ═════════════════════════════════════════════════════════════════════
    /// Intercepts ToddlerBugwatching jobs created by the base mod's
    /// JobGiver_ToddlerPlay and attaches an optional magnifying glass as TargetC.
    ///
    /// The magnifying glass is purely cosmetic (makes the job report say
    /// "bug watching with magnifying glass") and optional — if no glass is
    /// available, the job proceeds without it.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_JobGiver_ToddlerPlay_TryGiveJob_BugWatchingMagnifyingGlass
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.JobGiver_ToddlerPlay");
            return AccessTools.Method(type, "TryGiveJob", new System.Type[] { typeof(Pawn) });
        }

        /// <summary>
        /// Postfix that enhances base mod's bug watching with magnifying glass pickup.
        /// If the returned job is ToddlerBugwatching and a magnifying glass is available,
        /// we REPLACE it with a new job that picks up the glass first, then watches bugs.
        /// If no glass is found, the base job proceeds unchanged.
        /// </summary>
        static void Postfix(Pawn pawn, ref Job __result)
        {
            // No job was created at all.
            if (__result == null) return;

            // Only enhance ToddlerBugwatching jobs (the base mod's bug watching activity).
            if (__result.def?.defName != "ToddlerBugwatching") return;

            // Try to find a magnifying glass for the toddler to pick up and carry.
            Thing? glass = FindMagnifyingGlassForBugWatching(pawn);
            if (glass != null)
            {
                // Found a glass! Create a new job that picks it up first.
                // This job uses our custom JobDriver_TPE_BugwatchingWithMagnifyingGlass
                // which hauls the glass, then does the bug watching animation.
                IntVec3 originalTarget = __result.targetA.Cell; // Save the original bug watching cell
                Job newJob = JobMaker.MakeJob(TPE_JobDefOf.TPE_BugwatchingWithGlass, glass, originalTarget);
                __result = newJob;
            }
        }

        /// <summary>
        /// Finds the nearest reachable magnifying glass within 30 tiles.
        /// Returns null if none are available or if the toddler already carries one.
        /// </summary>
        static Thing? FindMagnifyingGlassForBugWatching(Pawn pawn)
        {
            // If already carrying a magnifying glass, don't bother searching.
            if (pawn.carryTracker?.CarriedThing?.def == TPE_ThingDefOf.TPE_MagnifyingGlass)
                return null;

            // Search for nearby magnifying glasses (30 tile radius, same as toy sword hunting).
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(TPE_ThingDefOf.TPE_MagnifyingGlass),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                30f,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }
    }

    // CRIB JUMPING — Integration patches
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures the base Toddlers mod recognises Crib Jumping as a valid
    /// play activity. Without this, <see cref="ToddlerPlayUtility.IsToddlerPlaying"/>
    /// returns false for toddlers performing Crib Jumping.
    /// </summary>
    [HarmonyPatch(typeof(ToddlerPlayUtility), "IsToddlerPlaying")]
    public static class Patch_ToddlerPlayUtility_IsToddlerPlaying_CribJumping
    {
        static void Postfix(Pawn toddler, ref bool __result)
        {
            if (!__result && toddler?.jobs?.curDriver is JobDriver_ToddlerCribJumping)
                __result = true;
        }
    }


    /// <summary>
    /// Prevents colonists from repeatedly picking up and re-placing toddlers
    /// who are actively jumping in their crib.
    ///
    /// ROOT CAUSE: during <see cref="JobDriver_ToddlerCribJumping"/> the toddler's
    /// posture is <see cref="PawnPosture.Standing"/> (required so the
    /// <c>GetBodyPos</c> bounce offset is visible). Because they are standing,
    /// <see cref="RestUtility.CurrentBed"/> returns <c>null</c>, which makes
    /// <see cref="BabyMoveUtility.AlreadyAtTarget"/> return <c>false</c> when
    /// the base mod's baby-safety system checks whether the toddler is already
    /// in their crib. The system then assigns a colonist to "bring baby to
    /// safety" (i.e. carry them back to the crib), interrupting Crib Jumping.
    ///
    /// FIX: postfix on <c>BabyMoveUtility.AlreadyAtTarget</c>. When the original
    /// method returns <c>false</c> but the pawn is a toddler doing Crib Jumping
    /// and the target is their current crib, we override the result to <c>true</c>.
    /// This tells the baby-safety system "this toddler is already where they
    /// should be" without affecting any other behaviour.
    ///
    /// Vanilla comparison:
    ///   - The base mod's <see cref="JobDriver_WiggleInCrib"/> avoids this
    ///     problem by using <see cref="PawnPosture.LayingInBed"/>, so
    ///     <c>RestUtility.CurrentBed</c> still works. We cannot use that posture
    ///     because it suppresses the standing bounce animation.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_BabyMoveUtility_AlreadyAtTarget_CribJumping
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Toddlers.BabyMoveUtility"), "AlreadyAtTarget");
        }

        static void Postfix(Pawn pawn, LocalTargetInfo target, ref bool __result)
        {
            if (__result) return; // Already true — nothing to do.

            // Only intervene for toddlers actively doing our Crib Jumping job.
            if (pawn?.jobs?.curDriver is not JobDriver_ToddlerCribJumping)
                return;

            // The target must be the crib the toddler is currently in.
            Building_Bed? crib = CribUtility.GetCurrentCrib(pawn);
            if (crib == null) return;
            if (!target.HasThing || target.Thing != crib) return;

            // Toddler is crib-jumping and the target is their crib —
            // consider them already at their destination.
            __result = true;
        }
    }

    /// <summary>
    /// Hides the pawn's name-plate label while they are actively crib jumping.
    ///
    /// PROBLEM: during <see cref="JobDriver_ToddlerCribJumping"/> the toddler
    /// stands upright in their crib (required posture for the vertical bounce
    /// animation). The name plate floats above their head, making it obvious
    /// they are "out of bed" even though they are still in the crib tile.
    /// This visual inconsistency breaks immersion and makes colonists look like
    /// they should be rescuing the toddler.
    ///
    /// FIX: prefix on <see cref="PawnUIOverlay.DrawPawnGUIOverlay"/>. When the
    /// pawn is doing Crib Jumping we skip the entire overlay draw, suppressing
    /// the name label (and the question-mark overlay) for the duration of the
    /// job. All other pawns draw normally.
    ///
    /// Vanilla comparison:
    ///   - Vanilla pawns in beds also have their labels hidden when zoomed out,
    ///     but this is distance-based, not job-based.
    ///   - Mutants with <c>hideLabel</c> use a similar early-return pattern.
    /// </summary>
    [HarmonyPatch(typeof(PawnUIOverlay), nameof(PawnUIOverlay.DrawPawnGUIOverlay))]
    public static class Patch_PawnUIOverlay_DrawPawnGUIOverlay_CribJumping
    {
        // PERFORMANCE: this runs for EVERY pawn on screen, several times per frame (once
        // per OnGUI event pass). It used to read the private 'pawn' field with
        // Traverse.Create(__instance).Field<Pawn>("pawn") — an allocation plus a
        // reflection lookup per call. Harmony's ___pawn injection reads the same private
        // field (PawnUIOverlay.pawn) directly in IL: zero reflection, zero allocation.
        //
        // Deliberately NOT gated on "Disable toddler play animations": with the bounce
        // off, a crib-jumping toddler still STANDS upright in the crib (the job's posture
        // is unchanged), which is exactly the "looks out of bed" case this hides.
        static bool Prefix(Pawn ___pawn)
        {
            // Suppress name plate (and question mark / lord overlays) while
            // the toddler is actively jumping in their crib.
            if (___pawn?.jobs?.curDriver is JobDriver_ToddlerCribJumping)
                return false; // Skip original — nothing is drawn.

            return true; // Draw normally.
        }
    }

    // CEILING WATCHING — Integration patches
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures the base Toddlers mod recognises Ceiling Watching as a valid
    /// play activity. Without this, <see cref="ToddlerPlayUtility.IsToddlerPlaying"/>
    /// returns false for toddlers performing Ceiling Watching.
    /// </summary>
    [HarmonyPatch(typeof(ToddlerPlayUtility), "IsToddlerPlaying")]
    public static class Patch_ToddlerPlayUtility_IsToddlerPlaying_CeilingWatching
    {
        static void Postfix(Pawn toddler, ref bool __result)
        {
            if (!__result && toddler?.jobs?.curDriver is JobDriver_ToddlerCeilingWatching)
                __result = true;
        }
    }


    // PILLOW NESTING — SHELVED FOR LATER
    // All three integration patches (IsToddlerPlaying, TryGiveJob, GetBodyPos) commented out.
    // Animation caching issues; to restore: uncomment all three patch classes below.
    /*
    // PILLOW NESTING — Integration patches
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures the base Toddlers mod recognises Pillow Nesting as a valid
    /// play activity. Without this, <see cref="ToddlerPlayUtility.IsToddlerPlaying"/>
    /// returns false for toddlers performing Pillow Nesting.
    /// </summary>
    [HarmonyPatch(typeof(ToddlerPlayUtility), "IsToddlerPlaying")]
    public static class Patch_ToddlerPlayUtility_IsToddlerPlaying_PillowNesting
    {
        static void Postfix(Pawn toddler, ref bool __result)
        {
            if (!__result && toddler?.jobs?.curDriver is JobDriver_ToddlerPillowNesting)
                __result = true;
        }
    }

    /// <summary>
    /// Adds Pillow Nesting as an in-crib play option. The base
    /// <see cref="JobGiver_ToddlerPlayInCrib"/> hard-codes only TV watching
    /// and sky dreaming; this postfix runs when the base method returns null
    /// and tries Pillow Nesting if the toddler is eligible.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_JobGiver_ToddlerPlayInCrib_TryGiveJob_PillowNesting
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Toddlers.JobGiver_ToddlerPlayInCrib");
            return AccessTools.Method(type, "TryGiveJob", new System.Type[] { typeof(Pawn) });
        }

        static void Postfix(Pawn pawn, ref Job __result)
        {
            // Base already found a job — nothing to do.
            if (__result != null) return;

            ToddlerPlayDef? playDef = DefDatabase<ToddlerPlayDef>.GetNamed(
                "TPE_ToddlerPillowNesting", errorOnFail: false);
            if (playDef == null) return;

            // CanDoFromCrib checks CribUtility.InCrib, play need, etc.
            if (playDef.Worker.CanDoFromCrib(pawn))
            {
                // TryGiveJob on PillowNesting delegates to CanDoFromCrib
                // and creates the job targeting the crib.
                Job? job = playDef.Worker.TryGiveJob(pawn);
                if (job != null)
                    __result = job;
            }
        }
    }

    /// <summary>
    /// Position offset for Pillow Nesting: when a toddler is performing the
    /// Pillow Nesting activity, this postfix adds the job driver's stored
    /// <see cref="JobDriver_ToddlerPillowNesting.currentOffset"/> to the
    /// rendered body position.  The offset is chosen discretely every 3–5
    /// seconds (not time-varying) so the toddler appears to snap to a new
    /// nestled position and hold it, matching vanilla lay-down behaviour.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
    public static class Patch_PawnRenderer_GetBodyPos_PillowNesting
    {
        static void Postfix(ref Vector3 __result, Pawn ___pawn)
        {
            // Fast path: only apply when doing Pillow Nesting.
            if (___pawn?.CurJobDef != TPE_JobDefOf.TPE_ToddlerPillowNesting)
                return;

            // Cast to our job driver to read the stored offset.
            JobDriver_ToddlerPillowNesting? driver = ___pawn.jobs?.curDriver as JobDriver_ToddlerPillowNesting;
            if (driver == null) return;

            // Apply the discrete offset chosen at the last SnapPose() call.
            __result += driver.currentOffset;
        }
    }
    */

    // SPEAKING PRACTICE — Integration patches
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ensures the base Toddlers mod recognises in-crib Speaking Practice as
    /// a valid play activity. Without this,
    /// <see cref="ToddlerPlayUtility.IsToddlerPlaying"/> returns false for
    /// toddlers performing the in-crib version, which can break downstream
    /// systems that rely on the play-status check.
    /// </summary>
    [HarmonyPatch(typeof(ToddlerPlayUtility), "IsToddlerPlaying")]
    public static class Patch_ToddlerPlayUtility_IsToddlerPlaying_SpeechPracticeInCrib
    {
        static void Postfix(Pawn toddler, ref bool __result)
        {
            if (!__result && toddler?.jobs?.curDriver is JobDriver_ToddlerSpeechPracticeInCrib)
                __result = true;
        }
    }

    /// <summary>
    /// Ensures the base Toddlers mod recognises Heater Snuggling as a valid
    /// play activity. Without this, <see cref="ToddlerPlayUtility.IsToddlerPlaying"/>
    /// returns false for toddlers performing Heater Snuggling.
    /// </summary>
    [HarmonyPatch(typeof(ToddlerPlayUtility), "IsToddlerPlaying")]
    public static class Patch_ToddlerPlayUtility_IsToddlerPlaying_HeaterSnuggling
    {
        static void Postfix(Pawn toddler, ref bool __result)
        {
            if (!__result && toddler?.jobs?.curDriver is JobDriver_ToddlerHeaterSnuggling)
                __result = true;
        }
    }


    // ══════════════════════════════════════════════════════════════════════
    //  SETTING: "Keep play mood for psychopaths" — suppress Psychopath's
    //  thought-nullification specifically for our play-mood ThoughtDefs.
    // ══════════════════════════════════════════════════════════════════════
    //
    // Design: TPE_ChildPlayHigh, TPE_ChildPlayEmpty, TPE_SelfSoothingPositive,
    // and TPE_SelfSoothingNegative all declare
    // <nullifyingTraits><li>Psychopath</li></nullifyingTraits> in XML, matching
    // vanilla's own convention for child/baby-facing empathy thoughts (see
    // Biotech's MyChildHappy / MyChildSad / MyParentHappy / MyParentsHappy,
    // which all nullify for Psychopath the same way). This means a toddler or
    // child with the Psychopath trait (in practice almost always acquired via
    // a forced-trait gene, since backstory traits aren't assigned this young)
    // has these specific play thoughts "nullified" by default: the memory is
    // still gained and still shows in the Needs tab (greyed out, with a
    // "Nullified by: Psychopath" tooltip line), but it contributes zero mood
    // — exactly like every other vanilla thought Psychopath suppresses.
    //
    // ALSO PROTECTED: vanilla Biotech's own "NeedPlay" ThoughtDef (see
    // TPE_ThoughtDefOf.NeedPlay in DefOfs.cs). This is the baby-side
    // equivalent of TPE_ChildPlayHigh/Empty — its ThoughtWorker_NeedPlay
    // worker maps Need_Play.CurCategory (the vanilla baby Play need
    // Cyanobot's Toddlers mod feeds into) directly to mood stages from
    // "desperately wants to play" (-20) up to "very entertained" (+10), and
    // vanilla ships it with nullifyingTraits: Psychopath already. Without
    // covering this def, a psychopathic baby's play-need mood (the vanilla
    // percentage-driven thought, as opposed to our own Self Soothing
    // activity thought) stayed nullified even with the setting on — the
    // fix for Self Soothing (Thought_SelfSoothing.Description) only
    // addressed the tooltip visibility bug on OUR thought; this is a
    // separate, additional thought entirely that we hadn't covered at all.
    //
    // ThoughtUtility.NullifyingTrait(def, pawn) is the single choke point used
    // by BOTH Thought.MoodOffset() (the actual mood math, inherited by both
    // Thought_Situational — used by TPE_ChildPlayHigh/Empty and vanilla's
    // NeedPlay — and Thought_Memory — used by TPE_SelfSoothingPositive/Negative
    // via Thought_SelfSoothing) AND ThoughtUtility.ThoughtNullifiedMessage()
    // (the tooltip text). Postfixing it here means the settings-gated
    // exemption applies consistently to both the mood number and the
    // displayed explanation, with no risk of the two disagreeing.
    //
    // When TPE_Settings.keepPlayMoodForPsychopaths is enabled, this postfix
    // clears __result specifically when the trait found is Psychopath AND the
    // thought is one of our protected play-mood defs. Every other nullifying
    // trait/thought combination in the game (vanilla or modded, including
    // Psychopath nullifying unrelated thoughts of ours or of other mods) is
    // left completely untouched.
    [HarmonyPatch(typeof(ThoughtUtility), nameof(ThoughtUtility.NullifyingTrait))]
    public static class Patch_ThoughtUtility_NullifyingTrait_PlayMood
    {
        static void Postfix(ThoughtDef def, Pawn pawn, ref Trait __result)
        {
            // Nothing was nullified by the original call — nothing to override.
            if (__result == null) return;

            // Player has not opted in — preserve default (vanilla-style) behaviour.
            if (TPE_Mod.Settings?.keepPlayMoodForPsychopaths != true) return;

            // Only intercede when Psychopath specifically is the nullifying
            // trait. A ThoughtDef could in principle list multiple nullifying
            // traits; if NullifyingTrait found some OTHER trait first, that
            // trait isn't Psychopath and must be left alone.
            if (__result.def != TraitDefOf.Psychopath) return;

            // Only intercede for our protected set of play-mood ThoughtDefs
            // (see IsProtectedPlayMoodThought below). Deliberately narrow —
            // this must NOT affect any other Psychopath-nullified thought
            // (e.g. TPE_PlayedInPlayground, or vanilla's own
            // MyChildHappy/MyChildSad), so the setting's effect stays exactly
            // as described to the player.
            if (!IsProtectedPlayMoodThought(def)) return;

            // Suppress nullification: pretend no nullifying trait was found,
            // restoring normal mood contribution for this specific thought.
            __result = null;
        }

        /// <summary>
        /// Returns true for the exact set of ThoughtDefs the
        /// "keep play mood for psychopaths" setting protects: the child
        /// play-need mood thoughts, the toddler Self Soothing outcome
        /// thoughts, and vanilla Biotech's own baby play-need thought
        /// (NeedPlay). <c>TPE_ThoughtDefOf.NeedPlay</c> can be null if
        /// Biotech is somehow inactive (see [MayRequire] in DefOfs.cs) —
        /// the <c>def ==</c> comparison is null-safe either way, it simply
        /// never matches.
        /// </summary>
        static bool IsProtectedPlayMoodThought(ThoughtDef def)
        {
            return def == TPE_ThoughtDefOf.TPE_ChildPlayHigh
                || def == TPE_ThoughtDefOf.TPE_ChildPlayEmpty
                || def == TPE_ThoughtDefOf.TPE_SelfSoothingPositive
                || def == TPE_ThoughtDefOf.TPE_SelfSoothingNegative
                || def == TPE_ThoughtDefOf.NeedPlay;
        }
    }
}