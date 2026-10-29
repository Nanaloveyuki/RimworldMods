using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    using Toddlers;

    /// <summary>
    /// Shared utility for identifying and manipulating our custom toy/plush defs.
    /// Centralizes the def-name checks so every system uses the same canonical list.
    /// </summary>
    public static class TPE_ToyUtility
    {
        /// <summary>
        /// Returns true if <paramref name="def"/> is one of our five plush toy defs.
        /// </summary>
        public static bool IsPlushDef(ThingDef def)
        {
            if (def == null) return false;
            return def == TPE_ThingDefOf.TPE_PlushTeddy
                || def == TPE_ThingDefOf.TPE_PlushMuffalo
                || def == TPE_ThingDefOf.TPE_PlushSheep
                || def == TPE_ThingDefOf.TPE_PlushDino
                || def == TPE_ThingDefOf.TPE_PlushMonster
                || def == TPE_ThingDefOf.TPE_PlushFox;
        }

        /// <summary>
        /// Returns true if <paramref name="thing"/> is one of our six plush toy defs.
        /// </summary>
        public static bool IsPlushThing(Thing thing)
        {
            return thing != null && IsPlushDef(thing.def);
        }

        /// <summary>
        /// Returns true if <paramref name="def"/> is any of our custom toy defs
        /// (plushes OR the toy sword).
        /// </summary>
        public static bool IsAnyToyDef(ThingDef def)
        {
            if (def == null) return false;
            return IsPlushDef(def) || def == TPE_ThingDefOf.TPE_ToySword;
        }

        /// <summary>
        /// Returns true if <paramref name="thing"/> is any of our custom toy defs.
        /// </summary>
        public static bool IsAnyToyThing(Thing thing)
        {
            return thing != null && IsAnyToyDef(thing.def);
        }

        /// <summary>
        /// Unconditionally unforbids <paramref name="thing"/> if it has
        /// <see cref="CompForbiddable"/>. Safe to call on null or destroyed things.
        /// </summary>
        public static void Unforbid(Thing thing)
        {
            if (thing == null || thing.Destroyed) return;
            if (thing is ThingWithComps twc && twc.compForbiddable != null)
            {
                twc.compForbiddable.Forbidden = false;
            }
        }

        /// <summary>
        /// Finds the nearest reachable unreserved plush toy for the given pawn.
        /// Returns null if none is found within <paramref name="maxDistance"/>.
        /// </summary>
        public static Thing? FindNearestPlush(Pawn pawn, float maxDistance = 25f)
        {
            // BUGFIX (save-load NRE): GenClosest dereferences the map unconditionally.
            // An unspawned pawn (e.g. mid save-load, in a caravan, carried) has no
            // map to search, so there is simply no plush "nearby". One bool check.
            if (pawn == null || !pawn.Spawned)
                return null;
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                maxDistance,
                t => IsPlushThing(t) && !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }

        /// <summary>
        /// Returns true when a <see cref="JobDriver.SetupToils"/> call is NOT a fresh
        /// job start, and every plush-sleep SetupToils patch must therefore step aside
        /// completely (return true from a prefix / do nothing in a postfix).
        ///
        /// WHY (user-reported bug: "loading a save with a 3–6 child sleeping without a
        /// plush throws 'Could not do PostLoadInit on JobDriver_LayDown' and the child
        /// wakes up"): vanilla calls SetupToils from TWO places —
        /// <c>Pawn_JobTracker.StartJob</c> (fresh job) AND <c>JobDriver.ExposeData</c>
        /// during <see cref="LoadSaveMode.PostLoadInit"/> to rebuild the toil list of a
        /// job that was in progress when the game was saved. During that load-time call
        /// the pawn is not spawned yet (Map.FinalizeLoading spawns pawns later), so:
        ///  1. <c>RestUtility.InBed</c> reads false even for a sleeping pawn, letting the
        ///     redirect fall through to <see cref="FindNearestPlush"/>, which NRE'd on the
        ///     null map inside GenClosest. The exception aborted SetupToils, leaving 0
        ///     toils for a saved curToilIndex of 2 → job ended → pawn woke up.
        ///  2. Even without the NRE, redirecting (EndJobWith + queue a seek job) or
        ///     inserting toils while RESTORING a job is wrong: the saved curToilIndex
        ///     refers to the original toil list and must line up exactly.
        /// Any non-Inactive scribe mode means we're inside load/save processing, and an
        /// unspawned/map-less pawn can't fetch anything anyway — both mean "leave it".
        /// Two field reads; safe to call from the hot SetupToils prefix path.
        /// </summary>
        public static bool IsSetupToilsRestoreOrUnspawned(JobDriver driver)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
                return true;
            Pawn pawn = driver.pawn;
            return pawn == null || !pawn.Spawned || pawn.Map == null;
        }

        /// <summary>
        /// Returns true if <paramref name="thing"/> has a <see cref="CompToyCategory"/>
        /// with categoryLabel "Plush toys".  This lets the play-desire system filter
        /// by toy category rather than hard-coding def names.
        /// </summary>
        public static bool IsPlushCategory(Thing thing)
        {
            if (thing == null) return false;
            var comp = thing.TryGetComp<CompToyCategory>();
            return comp != null && comp.Props.categoryLabel == "Plush toys";
        }

        /// <summary>
        /// Returns true if the pawn is a toddler (base-mod <see cref="ToddlerUtility.IsToddler"/>
        /// or age 1–3 biological).
        /// </summary>
        public static bool IsToddler(Pawn pawn)
        {
            if (ToddlerUtility.IsToddler(pawn)) return true;
            float age = pawn.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
            return age >= 1f && age < 3f;
        }

        /// <summary>
        /// BUGFIX / safety guard: returns true if equipping <paramref name="candidateApparelDef"/>
        /// onto <paramref name="pawn"/> would conflict with something they already have worn
        /// (same apparel layer + interfering body part group — e.g. a shield belt also on the
        /// "Belt" layer our plush toys use).
        ///
        /// This matters because <see cref="Pawn_ApparelTracker.Wear"/>, when called with
        /// <c>dropReplacedApparel: false</c> (which every plush-equip call site in this mod
        /// uses, to avoid dropping the pawn's real gear on the floor), resolves a layer
        /// conflict by calling <c>Remove()</c> on the conflicting item — which just detaches
        /// it from <see cref="Pawn_ApparelTracker.WornApparel"/> WITHOUT spawning it on the
        /// map or placing it in any container. The item is silently orphaned: not dropped,
        /// not destroyed, just gone. Toddlers and young children essentially never wear
        /// anything else in the Belt/Torso slot so this never surfaced in practice for them,
        /// but adults very plausibly wear a shield belt — so every plush-equip call site
        /// (toddler, child, and adult) must check this FIRST and skip equipping entirely if
        /// it would conflict, rather than risk deleting the pawn's real gear.
        /// </summary>
        public static bool HasConflictingWornApparel(Pawn pawn, ThingDef candidateApparelDef)
        {
            if (pawn?.apparel?.WornApparel == null || candidateApparelDef?.apparel == null)
                return false;

            foreach (Apparel worn in pawn.apparel.WornApparel)
            {
                if (!ApparelUtility.CanWearTogether(candidateApparelDef, worn.def, pawn.RaceProps.body))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the pawn is currently wearing or carrying a plush toy.
        /// </summary>
        public static bool HasPlushEquippedOrCarried(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel != null)
            {
                foreach (Apparel a in pawn.apparel.WornApparel)
                {
                    if (IsPlushThing(a)) return true;
                }
            }
            if (pawn.carryTracker?.CarriedThing != null && IsPlushThing(pawn.carryTracker.CarriedThing))
                return true;
            return false;
        }

        /// <summary>
        /// Shared "is this pawn currently asleep in a bed" check, used by every
        /// sleeping-plush render hook (suppression AND the head-position redraw).
        /// Replaces what used to be several near-duplicate private
        /// <c>IsSleepingChild</c> helpers scattered across HarmonyPatches.cs, each
        /// hard-coded to a single age bracket (3–6 only). The rendering system no
        /// longer needs to know which bracket a pawn belongs to at all — bracket
        /// eligibility already gated whether the pawn could ever have EQUIPPED a
        /// plush in the first place (see the four <c>Patch_JobDriver_LayDown_SetupToils*</c>
        /// redirects), so by the time we're asking "is this sleeping pawn wearing a
        /// plush", the bracket check has already done its job upstream. Checking
        /// only race + awake-state + in-bed here keeps this hot render-path check
        /// as cheap as possible and automatically covers all four brackets (and any
        /// future ones) without needing to be touched again.
        /// </summary>
        public static bool IsAsleepInBed(Pawn pawn)
        {
            if (pawn?.RaceProps?.Humanlike != true) return false;
            if (!pawn.Spawned || pawn.Dead) return false;
            if (pawn.Awake()) return false;
            return RestUtility.InBed(pawn);
        }

        /// <summary>
        /// One-time-per-def cache of the plush's own simple, non-apparel "dropped on
        /// the ground" <see cref="Graphic"/> (i.e. <c>ThingDef.graphicData.Graphic</c>,
        /// a plain <see cref="Graphic_Single"/> built from <c>texPath</c>) — used to
        /// draw the plush decoratively next to a sleeping pawn's head instead of as
        /// worn apparel. Populated ONCE, at startup, by the static constructor in
        /// <c>HarmonyPatches.cs</c> (piggybacking on the same one-time
        /// <c>TPE_Plush*</c> def loop that also fixes <c>developmentalStageFilter</c>)
        /// — never written to again after that.
        ///
        /// WHY a separate cache instead of resolving on demand: the plush toys'
        /// WORN appearance (<c>ApparelGraphicRecordGetter.TryGetGraphicApparel</c>,
        /// which resolves a body-type-specific <see cref="Graphic_Multi"/> via the
        /// shared, mutable <c>GraphicDatabase</c> cache) is exactly what an earlier
        /// version of the sleeping-plush-redraw feature called directly from a
        /// <c>PawnRenderer.RenderPawnAt</c> postfix, and it caused "concurrent
        /// collection" exceptions that forced the whole feature to be disabled.
        /// Using the plush's plain, non-body-type, already-resolved-at-startup
        /// graphic instead means the render hook never touches
        /// <c>ApparelGraphicRecordGetter</c>/<c>GraphicDatabase</c> at runtime at
        /// all — it's a read-only dictionary lookup, which can never race with
        /// anything, on any thread, at any point after startup.
        /// </summary>
        public static readonly Dictionary<ThingDef, Graphic> PlushDecorativeGraphics = new Dictionary<ThingDef, Graphic>();

        /// <summary>
        /// Looks up the pre-resolved decorative graphic for a plush ThingDef (see
        /// <see cref="PlushDecorativeGraphics"/>). Returns false (and a null out
        /// graphic) if the def isn't a known plush or the cache hasn't been
        /// populated yet (should never happen post-startup, but this is the hot
        /// render path so we guard rather than assume).
        /// </summary>
        public static bool TryGetPlushDecorativeGraphic(ThingDef def, out Graphic graphic)
        {
            if (def == null)
            {
                graphic = null;
                return false;
            }
            return PlushDecorativeGraphics.TryGetValue(def, out graphic) && graphic != null;
        }
    }
}
