using System.Collections.Generic;
using Verse.AI;

namespace MouseDisaster
{
    // Optimized saves may contain this driver with the child in target A rather
    // than B. Retire that job without consuming food; normal AI selects the next job.
    public sealed class JobDriver_MotherFeedBaby : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // The old driver saved one of four toil indexes. Resumed toils do
            // not run initAction again, so retire them from tickAction as well.
            for (int i = 0; i < 4; i++)
            {
                Toil toil = Toils_General.DoAtomic(() => EndJobWith(JobCondition.Incompletable));
                toil.tickAction = () => EndJobWith(JobCondition.Incompletable);
                yield return toil;
            }
        }
    }
}
