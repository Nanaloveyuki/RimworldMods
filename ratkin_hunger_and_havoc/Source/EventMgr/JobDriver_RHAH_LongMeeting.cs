using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.EventMgr
{
    public sealed class JobDriver_RHAH_LongMeeting : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            if (!job.exitMapOnArrival) this.FailOnDestroyedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true, false);
            if (job.exitMapOnArrival)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
                Toil leave = ToilMaker.MakeToil("RHAH_LongChildExit");
                leave.initAction = () =>
                {
                    Verse.Pawn child = pawn.carryTracker.CarriedThing as Verse.Pawn;
                    if (child == null || pawn.Map == null) return;
                    Rot4 direction = CellRect.WholeMap(pawn.Map).GetClosestEdge(pawn.Position);
                    pawn.carryTracker.innerContainer.Remove(child);
                    child.ExitMap(false, direction);
                    pawn.ExitMap(true, direction);
                };
                leave.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return leave;
            }
            else
            {
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
                Toil drop = ToilMaker.MakeToil("RHAH_LongMeetingDrop");
                drop.initAction = () =>
                {
                    Verse.Pawn child = pawn.carryTracker.CarriedThing as Verse.Pawn;
                    Verse.Pawn mother = job.targetB.Thing as Verse.Pawn;
                    if (child != null && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing ignored))
                        RHAH_LongFollowRuntime.NotifyMeeting(child, mother);
                };
                drop.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return drop;
            }
        }
    }
}
