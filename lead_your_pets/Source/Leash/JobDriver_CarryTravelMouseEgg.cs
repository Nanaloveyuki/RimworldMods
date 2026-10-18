using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public class JobDriver_CarryTravelMouseEgg : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => !LeadYourPetUtility.CanCarryForNonPlayerDeparture(pawn, job.targetA.Pawn));
            this.FailOn(() => GodHandsCompat.IsGrabbed(pawn) || GodHandsCompat.IsGrabbed(job.targetA.Pawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            // Travel's arrival check waits until the carried member is spawned again.
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil finish = ToilMaker.MakeToil("LeadYourPet_FinishTravelCarry");
            finish.initAction = delegate
            {
                if (job.exitMapOnArrival)
                {
                    Rot4 exitDir = CellRect.WholeMap(Map).GetClosestEdge(pawn.Position);
                    if (!pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing carried))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    // Vanilla treats factionless cargo as kidnapped; these are the Lord's own members.
                    ((Pawn)carried).ExitMap(false, exitDir);
                }
                else if (!pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing _))
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
