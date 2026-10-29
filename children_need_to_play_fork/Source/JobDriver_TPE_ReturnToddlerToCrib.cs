using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Colonist picks up a locked toddler who is out of their crib and
    /// <b>tucks them directly into the crib</b> via
    /// <see cref="Toils_Bed.TuckIntoBed"/>.
    ///
    /// This mirrors vanilla <c>JobDriver_BringBabyToSafety</c> and the base
    /// mod's own bed-placement logic. Dropping a toddler adjacent to the crib
    /// leaves them <i>out</i> of the crib, which causes
    /// <see cref="MapComponent_ToddlerCribReturn"/> to re-assign the same job
    /// indefinitely.
    /// </summary>
    public class JobDriver_TPE_ReturnToddlerToCrib : JobDriver
    {
        protected Pawn Toddler => (Pawn)job.targetA.Thing;
        protected Building_Bed Crib => (Building_Bed)job.targetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Only reserve the toddler; TuckIntoBed handles the bed
            return pawn.Reserve(Toddler, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            // Fail if toddler gets picked up by someone else during this job
            this.FailOn(() => Toddler.ParentHolder is Pawn_CarryTracker && Toddler.ParentHolder != pawn.carryTracker);

            // Walk to toddler and pick them up
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);

            // Walk to the crib cell
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell);

            // Tuck toddler directly into the crib (vanilla bed-placement)
            yield return Toils_Bed.TuckIntoBed(TargetIndex.B, TargetIndex.A);
        }
    }
}
