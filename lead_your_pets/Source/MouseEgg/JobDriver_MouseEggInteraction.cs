using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public class JobDriver_MouseEggInteraction : JobDriver
    {
        private Pawn Pet => job.GetTarget(TargetIndex.A).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, 180, true, false, false, TargetIndex.A);

            Toil finish = ToilMaker.MakeToil("LeadYourPet_MouseEggInteractionFinish");
            finish.initAction = delegate
            {
                LeadYourPetInteractionKind kind = (LeadYourPetInteractionKind)job.count;
                LeadYourPetUtility.ApplyInteractionMemories(pawn, Pet, kind);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
