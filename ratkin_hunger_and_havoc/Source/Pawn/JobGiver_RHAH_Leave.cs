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
        // 全封闭的来客每个闲置 tick 都会问到 搜不到就按游戏内小时冷却 省掉区域 BFS
        const int FailedExitSearchCooldownTicks = GenDate.TicksPerHour;

        // 冷却期内发原地等待 取 120 与原版回退游荡节点同频
        const int CooldownWaitJobTicks = 120;

        // 不落盘 读档后是新 pawn 实例 表项自然为空 最多重搜一次
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

        // 供无 Lord 的总 JobGiver 调度
        internal static Job TryCreate(Verse.Pawn pawn)
        {
            if (!RHAH_BatchAttitude.CanOrderLeave(pawn))
            {
                return null;
            }

            // 冷却判断放在建快照之前 冷却期内一次搜索和一次分配都不发生
            int now = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            ExitSearchCooldown cooldown;
            if (ExitSearchCooldowns.TryGetValue(pawn, out cooldown) && !IsExitSearchCooldownOver(cooldown, now))
            {
                return CreateCooldownWaitJob();
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
                return DigOutWhenNoExit(pawn, now);
            }
            ExitSearchCooldowns.Remove(pawn);

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


        // 走不到地图边缘时改走凿墙出图 否则全封闭基地里的离场来客永远走不掉
        static Job DigOutWhenNoExit(Verse.Pawn pawn, int now)
        {
            if (pawn.mindState != null)
            {
                // 寻路按当时的 duty.canDig 决定能不能砸墙 调用方不保证已经换上离场 duty
                pawn.mindState.duty = RHAH_VisitorRules.CreateLeaveDuty();
            }

            Job job = JobGiver_RHAH_DigOutWhenNoExit.TryCreate(pawn);
            if (job != null)
            {
                ExitSearchCooldowns.Remove(pawn);
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

        // 原地等待 不寻路也不改状态 到期后 JobTracker 会重新问
        // 不开 checkOverrideOnExpire 同 def 的连发 job 会被判成续做而丢弃 旧 job 就不更新了
        static Job CreateCooldownWaitJob()
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait);
            job.expiryInterval = CooldownWaitJobTicks;
            return job;
        }

        // 时间往回拨时旧值可能远在未来 按到期处理 免得很久不再检查
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

        // 抱持者由 JobDriver_RHAH_CarryYoung 保证双方都允许离图后一起走 其它 giver 不许单独带走
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
