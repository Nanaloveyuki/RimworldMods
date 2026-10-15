using System.Collections.Generic;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    public class JobDriver_RHAH_CarryYoung : JobDriver
    {
        Lord startedLord;
        bool lordCaptured;

        Verse.Pawn Takee
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Thing as Verse.Pawn;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Verse.Pawn takee = Takee;
            return takee != null && (takee.CarriedBy == pawn || pawn.Reserve(takee, job, 1, -1, null, errorOnFailed));
        }

        public override string GetReport()
        {
            Verse.Pawn child = Takee;
            if (child == null)
            {
                return base.GetReport();
            }

            return "RHAH_Job_CarryYoung_Report".Translate(child.LabelShort);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CaptureLord();
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(CarryReleased);
            this.FailOn(() => !RHAH_BatchAttitude.CanOrderLeave(pawn) ||
                pawn.GetLord() != startedLord || !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Carry) ||
                !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.ExitMap) ||
                !RHAH_BatchAttitude.CanOrderLeave(Takee) ||
                RHAH_Api.Get(Takee).Lifecycle != RHAH_Lifecycle.Leaving ||
                !RHAH_Api.Allows(Takee, RHAH_BehaviorGate.ExitMap));
            if (Takee?.CarriedBy != pawn)
            {
                Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                    .FailOnSomeonePhysicallyInteracting(TargetIndex.A);
                yield return approach;
                yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true, false);
            }
            Toil walk = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            walk.AddFailCondition(CarryReleased);
            walk.tickIntervalAction = (delta) => LeaveIfExit(pawn);
            yield return walk;
            Toil leave = ToilMaker.MakeToil("CarryYoungExit");
            leave.AddFailCondition(CarryReleased);
            leave.initAction = () => LeaveIfExit(pawn);
            leave.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return leave;
        }

        void CaptureLord()
        {
            if (lordCaptured)
            {
                return;
            }

            startedLord = ChildLord(Takee);
            lordCaptured = true;
        }

        bool CarryReleased()
        {
            return ReleaseStop() != JobCondition.Ongoing;
        }

        JobCondition ReleaseStop()
        {
            Verse.Pawn child = Takee;
            return ReleaseStop(
                child,
                startedLord,
                ChildLord(child),
                ChildHolder(child),
                pawn,
                RHAH_Api.IsVisitor(child));
        }

        internal static JobCondition ReleaseStop(
            Verse.Pawn child,
            Lord started,
            Lord current,
            Verse.Pawn holder,
            Verse.Pawn carrier,
            bool isVisitor)
        {
            if (child == null || child.Destroyed || !isVisitor || current != started)
            {
                return JobCondition.Incompletable;
            }

            if (holder != null && holder != carrier)
            {
                return JobCondition.Incompletable;
            }

            return JobCondition.Ongoing;
        }

        static void LeaveIfExit(Verse.Pawn carrier)
        {
            if (carrier.Map == null || !RHAH_Api.Allows(carrier, RHAH_BehaviorGate.ExitMap) ||
                !RHAH_Api.Allows(carrier.carryTracker?.CarriedThing as Verse.Pawn, RHAH_BehaviorGate.ExitMap))
            {
                return;
            }

            if (!carrier.Position.OnEdge(carrier.Map) && !carrier.Map.exitMapGrid.IsExitCell(carrier.Position))
            {
                return;
            }

            Rot4 direction = CellRect.WholeMap(carrier.Map).GetClosestEdge(carrier.Position);
            Verse.Pawn child = carrier.carryTracker.CarriedThing as Verse.Pawn;
            carrier.carryTracker.innerContainer.Remove(child);
            child.ExitMap(false, direction);
            carrier.ExitMap(true, direction);
        }

        static Lord ChildLord(Verse.Pawn child)
        {
            return child != null ? child.GetLord() : null;
        }

        static Verse.Pawn ChildHolder(Verse.Pawn child)
        {
            return child != null ? child.CarriedBy : null;
        }
    }
}
