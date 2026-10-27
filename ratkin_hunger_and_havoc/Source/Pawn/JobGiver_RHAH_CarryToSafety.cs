using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Pawn.Compat;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_CarryToSafety : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        internal static Job TryCreate(Verse.Pawn pawn)
        {
            Lord lord = pawn?.GetLord();
            LordJob_RHAH_Visitor visitor = lord?.LordJob as LordJob_RHAH_Visitor;
            if (visitor == null || visitor.IsPasserby || RHAH_DefOf.RHAH_CarryToSafety == null ||
                !RHAH_BatchAttitude.CanOrderLeave(pawn) || !pawn.Spawned ||
                !pawn.DevelopmentalStage.Adult() || !RHAH_ChildMovement.CanWalkOut(pawn) ||
                RHAH_Api.Get(pawn).Lifecycle == RHAH_Lifecycle.Leaving ||
                !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Carry) ||
                RHAH_LongFollowRuntime.HasEdgeHold(pawn))
            {
                return null;
            }

            IntVec3 destination = visitor.WaitSpot;
            if (!destination.IsValid || !pawn.CanReach(destination, PathEndMode.OnCell, Danger.Deadly))
            {
                return null;
            }

            Verse.Pawn held = pawn.carryTracker?.CarriedThing as Verse.Pawn;
            if (held != null)
            {
                return Eligible(pawn, held, lord) ? Create(held, destination) : null;
            }
            if (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null) return null;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn child = lord.ownedPawns[i];
                if (!Eligible(pawn, child, lord) || !child.Spawned || child.Map != pawn.Map ||
                    child.Position.InHorDistOf(destination, 10f) ||
                    !pawn.CanReserve(child) || !pawn.CanReach(child, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    continue;
                }
                return Create(child, destination);
            }
            return null;
        }

        internal static bool Eligible(Verse.Pawn carrier, Verse.Pawn child, Lord lord)
        {
            return child != null && child != carrier && !child.Dead && !child.Destroyed &&
                !child.DevelopmentalStage.Adult() && child.GetLord() == lord &&
                RHAH_BatchAttitude.CanOrderLeave(child) &&
                RHAH_Api.Get(child).Lifecycle != RHAH_Lifecycle.Leaving &&
                !RHAH_LongFollowRuntime.HasEdgeHold(child) &&
                (child.CarriedBy == carrier || (child.CarriedBy == null && !RHAH_ChildMovement.CanWalkOut(child))) &&
                !JobGiver_RHAH_Leave.WasDropped(lord, child);
        }

        static Job Create(Verse.Pawn child, IntVec3 destination)
        {
            Job job = JobMaker.MakeJob(RHAH_DefOf.RHAH_CarryToSafety, child, destination);
            job.count = 1;
            return job;
        }
    }
}
