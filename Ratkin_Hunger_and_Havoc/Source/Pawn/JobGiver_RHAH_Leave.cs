using HungerAndHavoc.Identity;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn.Compat;
using RimWorld;
using Verse.AI.Group;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_Leave : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        // 供无 Lord 的总 JobGiver 调度
        internal static Job TryCreate(Verse.Pawn pawn)
        {
            if (!RHAH_BatchAttitude.CanOrderLeave(pawn))
            {
                return null;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (!LordJob_RHAH_Visitor.ReadyToLeave(pawn, snapshot))
            {
                return null;
            }

            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.ExitMap) || pawn.Downed ||
                (pawn.stances?.stunner?.Stunned ?? false) || pawn.Map == null || JobDefOf.Goto == null)
            {
                return null;
            }

            Job drop = JobGiver_RHAH_DropChild.TryCreate(pawn);
            if (drop != null)
            {
                return drop;
            }

            if (RHAH_ChildMovement.CanWalkOut(pawn))
            {
                Job carry = CarryDependent(pawn);
                if (carry != null)
                {
                    return carry;
                }
            }
            else
            {
                return null;
            }

            IntVec3 spot;
            if (!RCellFinder.TryFindBestExitSpot(pawn, out spot))
            {
                return null;
            }

            if (HasUnreadyCarriedChild(pawn))
            {
                return null;
            }
            Verse.Pawn carried = pawn.carryTracker?.CarriedThing as Verse.Pawn;
            if (carried != null)
            {
                if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Carry) || RHAH_DefOf.RHAH_CarryYoung == null)
                {
                    return null;
                }
                Job carry = JobMaker.MakeJob(RHAH_DefOf.RHAH_CarryYoung, carried, spot);
                carry.count = 1;
                RHAH_LeashBridge.ClearDeparture(pawn);
                return carry;
            }
            RHAH_LeashBridge.ClearDeparture(pawn);
            Job job = JobMaker.MakeJob(JobDefOf.Goto, spot);
            job.exitMapOnArrival = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }


        static Job CarryDependent(Verse.Pawn pawn)
        {
            if (pawn.Downed || pawn.carryTracker?.CarriedThing != null ||
                pawn.CarriedBy != null)
            {
                return null;
            }

            if (!RHAH_ChildMovement.CanWalkOut(pawn) ||
                !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Carry))
            {
                return null;
            }

            Lord lord = pawn.GetLord();
            if (lord == null || lord.ownedPawns == null || RHAH_DefOf.RHAH_CarryYoung == null)
            {
                return null;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn child = lord.ownedPawns[i];
                if (child == null || child == pawn || child.Map != pawn.Map || child.CarriedBy != null)
                {
                    continue;
                }

                if (RHAH_ChildMovement.CanWalkOut(child) || !RHAH_BatchAttitude.CanOrderLeave(child) ||
                    WasDropped(lord, child))
                {
                    continue;
                }

                IRHAH_Pawn childState = RHAH_Api.Get(child);
                if (!LordJob_RHAH_Visitor.ReadyToLeave(child, childState))
                {
                    continue;
                }

                RHAH_Api.SetLifecycle(child, RHAH_Lifecycle.Leaving);
                if (!RHAH_Api.Allows(child, RHAH_BehaviorGate.ExitMap))
                {
                    continue;
                }

                if (!pawn.CanReach(child, PathEndMode.Touch, Danger.Deadly) || !pawn.CanReserve(child))
                {
                    continue;
                }

                IntVec3 exit;
                if (!RCellFinder.TryFindBestExitSpot(pawn, out exit, TraverseMode.ByPawn, true))
                {
                    continue;
                }

                Job carry = JobMaker.MakeJob(RHAH_DefOf.RHAH_CarryYoung, child, exit);
                carry.count = 1;
                return carry;
            }

            return null;
        }

        static bool HasUnreadyCarriedChild(Verse.Pawn pawn)
        {
            Verse.Pawn child = pawn.carryTracker?.CarriedThing as Verse.Pawn;
            return child != null && (!RHAH_BatchAttitude.CanOrderLeave(child) ||
                RHAH_Api.Get(child).Lifecycle != RHAH_Lifecycle.Leaving ||
                !RHAH_Api.Allows(child, RHAH_BehaviorGate.ExitMap));
        }

        internal static bool WasDropped(Lord lord, Verse.Pawn child)
        {
            if (lord?.ownedPawns == null)
            {
                return false;
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(lord.ownedPawns[i]);
                if (comp != null && comp.State.droppedChildLoadIds.Contains(child.thingIDNumber))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
