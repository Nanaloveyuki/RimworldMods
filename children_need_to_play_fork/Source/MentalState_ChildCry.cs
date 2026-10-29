using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Mental state for children in growth tier 1 (ages 3–6) who are sufficiently unhappy.
    /// Inherits from MentalState_BabyFit directly (NOT MentalState_BabyCry) so we can:
    ///   1. Write our own MentalStateTick using DrawPos instead of DrawPosHeld (which is null for walking children).
    ///   2. Write our own AuraEffect that applies TPE_ChildCrying / TPE_MyChildCrying thoughts instead
    ///      of vanilla CryingBaby, and grants immunity to hearers aged 6 or younger.
    ///
    /// The base MentalState_BabyFit provides:
    ///   - The clamor aura loop (fires AuraEffect on nearby pawns every 150 ticks).
    ///   - inCaravanCanDo, unspawnedNotInCaravanCanDo support.
    ///   - Save/load of lastScreamTick and alreadyHeard via ExposeData.
    /// </summary>
    public class MentalState_ChildCry : MentalState_BabyFit
    {
        // Sound effecter is managed automatically by PawnStatusEffecters system via stateEffecter.

        // ─── Visual effect tick counters ────────────────────────────────────
        // Not saved – these are purely cosmetic. Resetting to 0 on load just
        // means the first tick after load produces a tear immediately, which is fine.
        private int ticksUntilLeftTear;
        private int ticksUntilRightTear;

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
            // If the crying system was disabled mid-game, end this bout immediately.
            if (TPE_Mod.Settings?.enableChildCrying != true)
            {
                RecoverFromState();
                return;
            }

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
            UpdateCryingBehavior();
        }

        /// <summary>
        /// Manages the child's behavior while crying: seeking a nearby adult colonist
        /// to follow for comfort, or waiting in place if none are found.
        /// </summary>
        private void UpdateCryingBehavior()
        {
            // Only re-evaluate behavior every 60 ticks (≈1 second) to avoid
            // iterating all map pawns every single tick. Vanilla baby-cry aura
            // also uses a similar interval discipline (150 ticks).
            if (!pawn.IsHashIntervalTick(60))
                return;

            if (pawn.jobs?.curJob == null)
            {
                // No job currently. Find an adult to follow.
                Pawn targetAdult = FindNearbyGoodRelationshipAdult();
                if (targetAdult != null)
                {
                    Job followJob = JobMaker.MakeJob(JobDefOf.Follow);
                    followJob.targetA = targetAdult;
                    pawn.jobs.TryTakeOrderedJob(followJob);
                }
                else
                {
                    // No good adult nearby. Wait in place.
                    Job waitJob = JobMaker.MakeJob(JobDefOf.Wait);
                    pawn.jobs.TryTakeOrderedJob(waitJob);
                }
            }
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
            // ── Immunity: children aged 6 or younger are not affected ────────
            // This deliberately also covers the crying child themselves (prevents
            // self-debuff) and any other very young children nearby.
            if (hearer?.ageTracker != null && hearer.ageTracker.AgeBiologicalYears <= 6)
                return;

            // Apply the baby-cry clamor so AI systems (e.g. enemy alertness) hear the noise.
            hearer.HearClamor(source, ClamorDefOf.BabyCry);

            // Only apply mood thoughts when source is an identifiable pawn with a mood need.
            if (!(source is Pawn otherPawn) || hearer.needs?.mood == null)
                return;

            // Parents get a softer version of the thought (same as vanilla MyCryingBaby).
            if (hearer == otherPawn.GetMother() || hearer == otherPawn.GetFather())
            {
                hearer.needs.mood.thoughts.memories.TryGainMemory(
                    TPE_ThoughtDefOf.TPE_MyChildCrying, otherPawn);
            }
            else
            {
                // Everyone else (aged 7+) gets the full debuff.
                hearer.needs.mood.thoughts.memories.TryGainMemory(
                    TPE_ThoughtDefOf.TPE_ChildCrying, otherPawn);
            }
        }
    }
}
