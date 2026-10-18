using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.EventMgr
{
    public sealed class JobDriver_RHAH_LongMeetWalk : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil meet = ToilMaker.MakeToil("RHAH_LongMeet");
            meet.initAction = () => RHAH_LongFollowRuntime.NotifyMeeting(pawn, job.targetA.Thing as Verse.Pawn);
            meet.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return meet;
        }
    }
}
