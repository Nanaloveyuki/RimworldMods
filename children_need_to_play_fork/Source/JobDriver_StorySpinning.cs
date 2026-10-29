using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Job driver for the Story Spinning learning activity.
    /// The child picks up a plush toy, finds a comfortable nearby spot,
    /// and tells it a story for the default learning duration.
    /// 
    /// Mirrors vanilla <see cref="JobDriver_Reading"/> for item pickup/return
    /// and <see cref="JobDriver_Skydreaming"/> for stationary learning behavior.
    /// </summary>
    public class JobDriver_StorySpinning : JobDriver
    {
        /// <summary>The plush toy being told a story (TargetA).</summary>
        private Thing PlushToy => job.targetA.Thing;

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            job.count = 1;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (PlushToy == null)
                return false;
            return pawn.Reserve(PlushToy, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Finalizer: return toy to storage on success, drop on failure
            this.SetFinalizerJob(delegate (JobCondition condition)
            {
                if (!pawn.IsCarryingThing(PlushToy))
                    return null;
                if (condition != JobCondition.Succeeded)
                {
                    pawn.carryTracker.TryDropCarriedThing(
                        pawn.Position, ThingPlaceMode.Direct, out Thing _);
                    return null;
                }
                return HaulAIUtility.HaulToStorageJob(pawn, PlushToy, forced: false);
            });

            // --- Step 1: Go to the plush toy ---
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDestroyedOrNull(TargetIndex.A);

            // --- Step 2: Pick up the plush toy ---
            yield return Toils_Haul.StartCarryThing(TargetIndex.A)
                .FailOnDestroyedOrNull(TargetIndex.A);

            // --- Step 3: Find a comfortable nearby spot to sit ---
            Toil findSpot = ToilMaker.MakeToil("FindStorySpot");
            findSpot.initAction = () =>
            {
                IntVec3 spot = FindNearbySitSpot(pawn.Position, pawn.Map, pawn);
                job.targetB = new LocalTargetInfo(spot);
            };
            findSpot.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findSpot;

            // --- Step 4: Go to the sit spot (if different from current cell) ---
            if (job.targetB.IsValid && job.targetB.Cell != pawn.Position)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            }

            // --- Step 5: Tell the story ---
            Toil storyToil = ToilMaker.MakeToil("TellStory");
            storyToil.initAction = () =>
            {
                pawn.pather.StopDead();
                // Face a random direction to start
                pawn.rotationTracker.FaceCell(
                    pawn.Position + GenRadial.RadialPattern[Rand.Range(0, 8)]);
            };
            storyToil.tickIntervalAction = (int delta) =>
            {
                // Fill learning need; vanilla handles auto-end when satisfied
                LearningUtility.LearningTickCheckEnd(pawn, delta);

                // Occasionally shift facing to simulate animated storytelling
                if (pawn.IsHashIntervalTick(600))
                {
                    pawn.rotationTracker.FaceCell(
                        pawn.Position + GenRadial.RadialPattern[Rand.Range(0, 8)]);
                }
            };
            storyToil.defaultCompleteMode = ToilCompleteMode.Delay;
            storyToil.defaultDuration = job.def.learningDuration;
            storyToil.handlingFacing = true;
            storyToil.AddFinishAction(delegate
            {
                // If carrying the toy and standing on the toy's original cell,
                // drop it here so it's not stuck in inventory
                if (pawn.IsCarryingThing(PlushToy) && PlushToy != null)
                {
                    // Finalizer above will handle hauling to storage on success
                }
            });
            yield return storyToil;
        }

        /// <summary>
        /// Finds a safe, standable cell within 3 tiles of the given center.
        /// Falls back to the center cell if nothing suitable is found.
        /// </summary>
        static IntVec3 FindNearbySitSpot(IntVec3 center, Map map, Pawn pawn)
        {
            IntVec3 result = center;
            CellFinder.TryFindRandomReachableNearbyCell(
                center,
                map,
                3f,
                TraverseParms.For(pawn),
                c => c.Standable(map) && c.GetDangerFor(pawn, map) == Danger.None,
                null,
                out result);
            return result;
        }
    }
}
