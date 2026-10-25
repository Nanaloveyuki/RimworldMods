using System.Runtime.CompilerServices;
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
        // 无路且无法破障时按游戏内小时重试
        const int FailedExitSearchCooldownTicks = GenDate.TicksPerHour;

        const int CooldownWaitJobTicks = 120;

        static readonly ConditionalWeakTable<Verse.Pawn, ExitSearchCooldown> ExitSearchCooldowns =
            new ConditionalWeakTable<Verse.Pawn, ExitSearchCooldown>();

        sealed class ExitSearchCooldown
        {
            internal int nextSearchTick;
        }

        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        internal static bool IsDeparting(Verse.Pawn pawn)
        {
            return RHAH_BatchAttitude.CanOrderLeave(pawn) &&
                RHAH_Api.Get(pawn).Lifecycle == RHAH_Lifecycle.Leaving &&
                RHAH_Api.Allows(pawn, RHAH_BehaviorGate.ExitMap);
        }

        internal static bool RejectsFoodJob(Verse.Pawn pawn, Job job, ThinkNode giver)
        {
            if (job?.def == null) return false;
            bool foodJob = giver is JobGiver_GetFood || giver is JobGiver_RHAH_Feed || giver is JobGiver_RHAH_Gnaw ||
                job.def == JobDefOf.Ingest || job.def == RHAH_DefOf.RHAH_Gnaw;
            if (!foodJob && (job.def == JobDefOf.Harvest || job.def == JobDefOf.TakeFromOtherInventory))
            {
                ThingDef food = job.targetA.Thing?.def;
                foodJob = food?.IsNutritionGivingIngestible == true ||
                    food?.plant?.harvestedThingDef?.IsNutritionGivingIngestible == true;
            }
            return foodJob && IsDeparting(pawn);
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

            if (HasUnreadyCarriedChild(pawn))
            {
                return null;
            }
            Verse.Pawn carried = pawn.carryTracker?.CarriedThing as Verse.Pawn;
            if (carried != null && (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Carry) || RHAH_DefOf.RHAH_CarryYoung == null))
            {
                return null;
            }

            int now = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            ExitSearchCooldown cooldown;
            if (ExitSearchCooldowns.TryGetValue(pawn, out cooldown) && !IsExitSearchCooldownOver(cooldown, now))
            {
                return CanDepartAlone(pawn) ? CreateCooldownWaitJob() : null;
            }

            IntVec3 spot;
            if (!RCellFinder.TryFindBestExitSpot(pawn, out spot))
            {
                return DigOutWhenNoExit(pawn, now);
            }
            ExitSearchCooldowns.Remove(pawn);

            if (carried != null)
            {
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


        static Job DigOutWhenNoExit(Verse.Pawn pawn, int now)
        {
            // 原版破障任务会先放下怀中物品 不能绕过抱持与弃婴规则
            if (!CanDepartAlone(pawn))
            {
                return null;
            }

            if (pawn.mindState != null)
            {
                pawn.mindState.duty = RHAH_VisitorRules.CreateLeaveDuty();
            }

            Job job = JobGiver_RHAH_DigOutWhenNoExit.TryCreate(pawn);
            if (job != null)
            {
                ExitSearchCooldowns.Remove(pawn);
                RHAH_LeashBridge.ClearDeparture(pawn);
                return job;
            }

            ExitSearchCooldown cooldown;
            if (!ExitSearchCooldowns.TryGetValue(pawn, out cooldown))
            {
                cooldown = new ExitSearchCooldown();
                ExitSearchCooldowns.Add(pawn, cooldown);
            }
            cooldown.nextSearchTick = now + FailedExitSearchCooldownTicks;
            return CreateCooldownWaitJob();
        }

        static Job CreateCooldownWaitJob()
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait);
            job.expiryInterval = CooldownWaitJobTicks;
            return job;
        }

        static bool IsExitSearchCooldownOver(ExitSearchCooldown cooldown, int now)
        {
            return now >= cooldown.nextSearchTick || cooldown.nextSearchTick - now > FailedExitSearchCooldownTicks;
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

        internal static bool CanDepartAlone(Verse.Pawn pawn)
        {
            return pawn != null && pawn.CarriedBy == null && pawn.carryTracker?.CarriedThing == null;
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
