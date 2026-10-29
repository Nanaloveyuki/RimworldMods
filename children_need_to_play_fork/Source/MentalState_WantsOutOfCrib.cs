using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// "Wants to be let out the crib" mental state.
    /// Driven by the presence of <see cref="Hediff_TPE_WantsOutOfCrib"/>.
    /// Features intermittent crying (burst/pause cycles), tear particle effects,
    /// active mood debuff, and automatic recovery when the driving hediff is removed.
    /// </summary>
    public class MentalState_WantsOutOfCrib : MentalState
    {
        private int ticksUntilLeftTear;
        private int ticksUntilRightTear;
        private int cryBurstRemaining;
        private int cryPauseRemaining;
        private bool isCrying;
        // FIX N1: Track whether the toddler was ever let out during this
        // mental state, so PostEnd can award the post-release thought even
        // if they were put back in a crib during the 60-tick recovery window.
        private bool wasEverLetOut;

        private const int TicksBetweenTearDots = 35;
        private static readonly IntRange TicksBetweenTears = new IntRange(25, 40);
        private static readonly FloatRange randAngle = new FloatRange(10f, 30f);
        private static readonly FloatRange randScale = new FloatRange(0.6f, 1f);
        private static readonly IntRange CryBurstRange = new IntRange(480, 840);   // 8-14 sec
        private static readonly IntRange CryPauseRange = new IntRange(240, 600);   // 4-10 sec

        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            causedByMood = false; // no catharsis
            wasEverLetOut = false;
            StartCryBurst();

            // Apply active mood debuff
            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(TPE_ThoughtDefOf.TPE_WantsOutOfCrib_Active);
        }

        void StartCryBurst()
        {
            isCrying = true;
            cryBurstRemaining = CryBurstRange.RandomInRange;
            cryPauseRemaining = 0;
        }

        void StartCryPause()
        {
            isCrying = false;
            cryPauseRemaining = CryPauseRange.RandomInRange;
            cryBurstRemaining = 0;
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);

            // FIX N1: Remember if the toddler was ever let out while this
            // mental state was active.
            if (!CribUtility.InCrib(pawn))
                wasEverLetOut = true;

            // End immediately if the driving hediff is gone (timed out or toddler freed)
            if (!HasWantsOutHediff())
            {
                RecoverFromState();
                return;
            }

            // Defensive 60-tick check: verify the toddler is still confined.
            // Mirrors Hediff_TPE_WantsOutOfCrib.Tick() logic to prevent desync between
            // the hediff and mental state. If somehow the toddler escapes the crib but
            // the hediff hasn't removed itself yet, this ensures the mental state ends.
            if (pawn.IsHashIntervalTick(60))
            {
                if (!CribUtility.InCrib(pawn))
                {
                    // Toddler has escaped the crib — mental break purpose is resolved.
                    // End the mental state immediately.
                    RecoverFromState();
                    return;
                }
            }

            // Intermittent crying: burst then pause
            if (isCrying)
            {
                cryBurstRemaining -= delta;
                if (cryBurstRemaining <= 0)
                    StartCryPause();
                else
                    SpawnCryingVisuals(delta);
            }
            else
            {
                cryPauseRemaining -= delta;
                if (cryPauseRemaining <= 0)
                    StartCryBurst();
            }
        }

        bool HasWantsOutHediff()
        {
            return pawn.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib) == true;
        }

        void SpawnCryingVisuals(int delta)
        {
            if (pawn.SpawnedParentOrMe is not Pawn visualPawn || visualPawn.Position.Fogged(visualPawn.Map))
                return;

            float bodyAngle = visualPawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None);
            Vector3? drawPosMaybe = pawn.DrawPosHeld;
            if (!drawPosMaybe.HasValue) return;
            Vector3 drawPos = drawPosMaybe.Value;

            if ((ticksUntilLeftTear -= delta) <= 0)
            {
                visualPawn.Map.flecks.CreateFleck(new FleckCreationData
                {
                    spawnPosition = drawPos + new Vector3(-0.15f, 0f, 0.066f).RotatedBy(bodyAngle),
                    velocitySpeed = -0.66f,
                    velocityAngle = 90f + bodyAngle - randAngle.RandomInRange,
                    def = FleckDefOf.FleckBabyCrying,
                    scale = randScale.RandomInRange
                });
                ticksUntilLeftTear = TicksBetweenTears.RandomInRange;
            }

            if ((ticksUntilRightTear -= delta) <= 0)
            {
                visualPawn.Map.flecks.CreateFleck(new FleckCreationData
                {
                    spawnPosition = drawPos + new Vector3(0.15f, 0f, 0.066f).RotatedBy(bodyAngle),
                    velocitySpeed = 0.66f,
                    velocityAngle = 90f + bodyAngle + randAngle.RandomInRange,
                    def = FleckDefOf.FleckBabyCrying,
                    scale = randScale.RandomInRange,
                    exactScale = new Vector3(-1f, 1f, 1f)
                });
                ticksUntilRightTear = TicksBetweenTears.RandomInRange;
            }

            if (pawn.IsHashIntervalTick(TicksBetweenTearDots, delta))
            {
                MoteMaker.MakeAttachedOverlay(visualPawn, ThingDefOf.Mote_BabyCryingDots,
                    new Vector3(0.27f, 0f, 0.066f).RotatedBy(bodyAngle)).exactRotation = Rand.Value * 180f;
                MoteMaker.MakeAttachedOverlay(visualPawn, ThingDefOf.Mote_BabyCryingDots,
                    new Vector3(-0.27f, 0f, 0.066f).RotatedBy(bodyAngle)).exactRotation = Rand.Value * 180f;
            }
        }

        public override void PostEnd()
        {
            base.PostEnd();

            var memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories != null)
            {
                // Remove active crib debuff
                var active = memories.GetFirstMemoryOfDef(TPE_ThoughtDefOf.TPE_WantsOutOfCrib_Active);
                if (active != null)
                    memories.RemoveMemory(active);

                // FIX N1: Apply post-release thought if the toddler was ever
                // let out during this mental state (not just at PostEnd time).
                if (wasEverLetOut)
                {
                    memories.TryGainMemory(TPE_ThoughtDefOf.TPE_WasLockedInCrib);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilLeftTear, "ticksUntilLeftTear", 0);
            Scribe_Values.Look(ref ticksUntilRightTear, "ticksUntilRightTear", 0);
            Scribe_Values.Look(ref cryBurstRemaining, "cryBurstRemaining", 0);
            Scribe_Values.Look(ref cryPauseRemaining, "cryPauseRemaining", 0);
            Scribe_Values.Look(ref isCrying, "isCrying", false);
            Scribe_Values.Look(ref wasEverLetOut, "wasEverLetOut", false);
        }
    }
}
