using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    // Walking-child visuals and comfort seeking share the vanilla baby's saved aura lifecycle.
    public class MentalState_ChildCry : MentalState_BabyFit
    {
        // Sound effecter is managed automatically by PawnStatusEffecters system via stateEffecter.

        // ─── Visual effect tick counters ────────────────────────────────────
        // Not saved – these are purely cosmetic. Resetting to 0 on load just
        // means the first tick after load produces a tear immediately, which is fine.
        private int ticksUntilLeftTear;
        private int ticksUntilRightTear;
        private List<Pawn> decidedParents = new List<Pawn>(2);
        private List<Pawn> ignoredParents = new List<Pawn>(2);
        private bool ignoredMemoryGiven;
        private int comfortJobId = -1;

        // ─── Visual effect constants (matches vanilla baby crying) ───────────
        /// <summary>Ticks between each crying-dot mote pair.</summary>
        private const int TicksBetweenTearDots = 35;

        /// <summary>Random range of ticks between each individual tear fleck.</summary>
        private static readonly IntRange TicksBetweenTears = new IntRange(25, 40);

        /// <summary>Speed of the tear fleck particles.</summary>
        private const float TearVelocitySpeed = 0.66f;

        /// <summary>Random angular spread of each tear fleck.</summary>
        private static readonly FloatRange TearAngleRange = new FloatRange(10f, 30f);

        /// <summary>Random scale range for tear flecks.</summary>
        private static readonly FloatRange TearScaleRange = new FloatRange(1.0f, 1.5f);

        // ────────────────────────────────────────────────────────────────────
        //  POST START
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called after the mental state is fully initialized on the pawn.
        /// Notifies the episode tracker so the First-Cry 1-Hour Cap knows
        /// this pawn has cried and stops its timer.
        /// </summary>
        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            ChildCryTracker.OnCryStarted(pawn);
        }

        public override void PostEnd()
        {
            Job current = pawn.CurJob;
            if (current != null && comfortJobId >= 0 && current.loadID == comfortJobId && !current.playerForced)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            comfortJobId = -1;
            base.PostEnd();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref decidedParents, "decidedParents", LookMode.Reference);
            Scribe_Collections.Look(ref ignoredParents, "ignoredParents", LookMode.Reference);
            Scribe_Values.Look(ref ignoredMemoryGiven, "ignoredMemoryGiven", false);
            Scribe_Values.Look(ref comfortJobId, "comfortJobId", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                decidedParents ??= new List<Pawn>(2);
                ignoredParents ??= new List<Pawn>(2);
            }
        }

        private bool ParentIgnoresCry(Pawn parent)
        {
            if (parent != pawn.GetMother() && parent != pawn.GetFather())
                return false;
            if (decidedParents.Contains(parent))
                return ignoredParents.Contains(parent);

            decidedParents.Add(parent);
            float chance = TPE_Mod.Settings?.parentIgnoreCryChance ?? 0.8f;
            if (chance < 1f && (chance <= 0f || !Rand.Chance(chance)))
                return false;

            ignoredParents.Add(parent);
            if (!ignoredMemoryGiven && pawn.needs?.mood != null)
            {
                ignoredMemoryGiven = true;
                pawn.needs.mood.thoughts.memories.TryGainMemory(TPE_ThoughtDefOf.TPE_ChildCryingIgnored);
            }
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        //  TICK
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ticks the mental state each game tick.
        /// Calls base (MentalState_BabyFit) which handles the clamor AuraEffect loop,
        /// maintains the crying sound sustainer, and adds visual effects (tear flecks
        /// and crying-dot motes) at the child's position.
        /// </summary>
        public override void MentalStateTick(int delta)
        {
            if (!ChildCryTracker.IsCryingEligibleChild(pawn)
                || (ChildCryTracker.IsOlderChild(pawn) && age >= def.maxTicksBeforeRecovery / 2))
            {
                RecoverFromState();
                return;
            }

            if (ChildCryTracker.IsOlderChild(pawn)
                && (forceRecoverAfterTicks < 0 || forceRecoverAfterTicks > def.maxTicksBeforeRecovery / 2))
                forceRecoverAfterTicks = def.maxTicksBeforeRecovery / 2;

            // Base tick handles the AuraEffect clamor (fires every 150 ticks via GenClamor).
            base.MentalStateTick(delta);
            
            // Guard: the base tick may have triggered recovery (AgeTicks >= def timeout).
            // If the pawn is no longer in this mental state, skip spawning visuals.
            if (pawn.MentalState?.def != def)
                return;

            // Skip visuals if the child is not on a visible map tile.
            if (!pawn.Spawned || pawn.Map == null || pawn.Position.Fogged(pawn.Map))
                return;

            // Get the pawn's current body rotation angle for positioning effects.
            float bodyAngle = pawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None);

            // Use DrawPosHeld if the child is inside a container (e.g. crib).
            // Fall back to DrawPos for walking children (DrawPosHeld is null when not held).
            Vector3 drawPos = pawn.DrawPosHeld ?? pawn.DrawPos;
            
            // The crying sound effecter is maintained by the vanilla PawnStatusEffecters system.
            // We don't need to manage it manually here.

            // ── Left tear fleck ──────────────────────────────────────────────
            if (--ticksUntilLeftTear <= 0)
            {
                pawn.Map.flecks.CreateFleck(new FleckCreationData
                {
                    spawnPosition = drawPos + new Vector3(-0.10f, 0f, 0.22f).RotatedBy(bodyAngle),
                    velocitySpeed = -TearVelocitySpeed,
                    velocityAngle = 90f + bodyAngle - TearAngleRange.RandomInRange,
                    def = FleckDefOf.FleckBabyCrying,
                    scale = TearScaleRange.RandomInRange
                });
                ticksUntilLeftTear = TicksBetweenTears.RandomInRange;
            }

            // ── Right tear fleck ─────────────────────────────────────────────
            if (--ticksUntilRightTear <= 0)
            {
                pawn.Map.flecks.CreateFleck(new FleckCreationData
                {
                    spawnPosition = drawPos + new Vector3(0.10f, 0f, 0.22f).RotatedBy(bodyAngle),
                    velocitySpeed = TearVelocitySpeed,
                    velocityAngle = 90f + bodyAngle + TearAngleRange.RandomInRange,
                    def = FleckDefOf.FleckBabyCrying,
                    scale = TearScaleRange.RandomInRange,
                    exactScale = new Vector3(-1f, 1f, 1f)   // Mirror so right tear streams the correct way
                });
                ticksUntilRightTear = TicksBetweenTears.RandomInRange;
            }

            // ── Crying-dot motes (attached overlay, refreshed every 35 ticks) ──
            if (pawn.IsHashIntervalTick(TicksBetweenTearDots, delta))
            {
                // Offset slightly inward (X=±0.45) and lowered (Z=0.22) for a standing child.
                // scale=1.4f makes the motes visible but not oversized.
                MoteMaker.MakeAttachedOverlay(
                    pawn,
                    ThingDefOf.Mote_BabyCryingDots,
                    new Vector3(0.45f, 0f, 0.22f).RotatedBy(bodyAngle),
                    scale: 1.4f
                ).exactRotation = Rand.Value * 180f;

                MoteMaker.MakeAttachedOverlay(
                    pawn,
                    ThingDefOf.Mote_BabyCryingDots,
                    new Vector3(-0.45f, 0f, 0.22f).RotatedBy(bodyAngle),
                    scale: 1.4f
                ).exactRotation = Rand.Value * 180f;
            }
            
            // Update crying behavior: find adult colonist to follow, or wait.
            UpdateCryingBehavior(delta);
        }

        private void UpdateCryingBehavior(int delta)
        {
            if (!pawn.IsHashIntervalTick(150, delta) || pawn.jobs == null || pawn.CurJob != null
                || pawn.Drafted || pawn.Downed || !pawn.Awake() || pawn.jobs.jobQueue.Count != 0)
                return;

            Pawn targetAdult = FindNearbyGoodRelationshipAdult();
            Job job = targetAdult == null
                ? JobMaker.MakeJob(JobDefOf.Wait)
                : JobMaker.MakeJob(JobDefOf.Follow, targetAdult);
            comfortJobId = job.loadID;
            pawn.jobs.StartJob(job);
        }

        /// <summary>
        /// Finds the nearest adult colonist within 15 tiles who has a good opinion
        /// (opinion >= 0) of the child. Returns null if none found.
        /// </summary>
        private Pawn FindNearbyGoodRelationshipAdult()
        {
            if (pawn.Map == null)
                return null;

            Pawn result = null;
            float closestDist = float.MaxValue;

            foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                // Skip non-adults, non-colonists, and the child themselves.
                if (candidate == pawn || candidate.DevelopmentalStage != DevelopmentalStage.Adult)
                    continue;
                if (!candidate.IsColonist)
                    continue;

                // Check if within search radius (15 tiles).
                float dist = (pawn.Position - candidate.Position).LengthManhattan;
                if (dist > 15)
                    continue;

                // Check reachability.
                if (!candidate.CanReach(pawn, PathEndMode.OnCell, Danger.None))
                    continue;

                // Check relationship (opinion >= 0).
                float opinion = pawn.relations?.OpinionOf(candidate) ?? float.MinValue;
                if (opinion < 0)
                    continue;

                if (ParentIgnoresCry(candidate))
                    continue;

                // This is a good candidate; keep it if it's the closest.
                if (dist < closestDist)
                {
                    result = candidate;
                    closestDist = dist;
                }
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────────────
        //  AURA EFFECT  (called by MentalState_BabyFit every 150 ticks)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the base class for every pawn within the scream radius (~10 tiles).
        /// Applies mood debuffs to adults. Children aged 6 or younger are immune to prevent
        /// a doom loop where one crying child makes other young children sad, causing them to
        /// cry too until everyone is perpetually in crisis.
        /// </summary>
        protected override void AuraEffect(Thing source, Pawn hearer)
        {
            if (!ChildCryTracker.IsCryingEligibleChild(pawn))
                return;
            if (hearer?.ageTracker == null || hearer.ageTracker.AgeBiologicalYears <= 6)
                return;

            // BabyCry clamor disturbs sleep and notifies the lord; decide before either response.
            // Only this mod's cry response is skipped, never feeding, medicine or another mod's jobs.
            if (ParentIgnoresCry(hearer))
                return;
            hearer.HearClamor(source, ClamorDefOf.BabyCry);
            if (!(source is Pawn otherPawn) || hearer.needs?.mood == null)
                return;

            bool isParent = hearer == otherPawn.GetMother() || hearer == otherPawn.GetFather();
            bool olderChild = ChildCryTracker.IsOlderChild(otherPawn);
            ThoughtDef thought = isParent
                ? (olderChild ? TPE_ThoughtDefOf.TPE_MyOlderChildCrying : TPE_ThoughtDefOf.TPE_MyChildCrying)
                : (olderChild ? TPE_ThoughtDefOf.TPE_OlderChildCrying : TPE_ThoughtDefOf.TPE_ChildCrying);
            hearer.needs.mood.thoughts.memories.TryGainMemory(thought, otherPawn);
        }
    }
}
