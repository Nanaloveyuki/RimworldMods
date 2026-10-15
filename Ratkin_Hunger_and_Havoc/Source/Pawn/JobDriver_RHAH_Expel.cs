using System.Collections.Generic;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobDriver_RHAH_Expel : JobDriver
    {
        Verse.Pawn TargetPawn
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Pawn;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !RHAH_BatchAttitude.CanOrderLeave(TargetPawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil expel = ToilMaker.MakeToil("ExpelVisitor");
            expel.initAction = () => RHAH_BatchAttitude.TryShift(TargetPawn, RHAH_BatchReaction.Expel);
            expel.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return expel;
        }

        public override string GetReport()
        {
            Verse.Pawn target = TargetPawn;
            if (target == null)
            {
                return base.GetReport();
            }

            return "RHAH_Job_Expel_Report".Translate(target.LabelShort);
        }
    }
}
