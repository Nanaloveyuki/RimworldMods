using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public class JobDriver_BeginLeash : JobDriver
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

            Toil finish = ToilMaker.MakeToil("LeadYourPet_FinishBeginLeash");
            finish.initAction = delegate
            {
                if (job.def == LeadYourPetDefOf.LeadYourPet_EndLeash)
                {
                    LeadYourPetUtility.Component?.EndLeashForPet(Pet);
                    return;
                }

                LeadYourPetUtility.Component?.StartLeash(pawn, Pet, job.count == 1);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
