using System;
using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_MouseSwarm
    {
        const float SearchRadius = 80f;
        const float HighValue = 320f;
        static readonly Predicate<Thing> movableValidator = MovableTarget;
        static readonly Predicate<Thing> buildingValidator = BuildingTarget;
        static readonly Func<Thing, float> priority = TargetPriority;
        static Verse.Pawn searcher;

        internal static bool IsSource(string source)
        {
            return source == "I-052";
        }

        internal static bool IsProvoked(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            return comp != null && comp.State.IsActiveVisitor && IsSource(comp.State.sourceIncidentDisplayId) &&
                comp.State.attitude == RHAH_Attitude.Hostile;
        }

        internal static void Initialize(List<Verse.Pawn> members)
        {
            Faction neutral = RHAH_AttitudeFactions.Require(RHAH_Attitude.Neutral);
            for (int i = 0; members != null && i < members.Count; i++)
            {
                Verse.Pawn member = members[i];
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(member);
                if (comp == null || !IsSource(comp.State.sourceIncidentDisplayId)) continue;
                comp.State.attitudeAtArrival = RHAH_Attitude.Neutral;
                comp.State.SetAttitude(RHAH_Attitude.Neutral);
                comp.State.foodWaitUntilTick = -1;
                comp.State.leaveAfterGameTick = -1;
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.LeaveAfterFed, false);
                RHAH_BatchAttitude.SetVisitorFaction(member, neutral);
            }
        }

        internal static bool TryProvoke(Verse.Pawn affected)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(affected);
            if (comp == null || !IsSource(comp.State.sourceIncidentDisplayId) ||
                !RHAH_BatchAttitude.CanOrderLeave(affected)) return false;
            if (IsProvoked(affected)) return true;
            List<Verse.Pawn> members = RHAH_BatchAttitude.Collect(affected.MapHeld, comp.State.spawnBatchId);
            if (members.Count == 0) members.Add(affected);
            return Provoke(members);
        }

        internal static bool Provoke(List<Verse.Pawn> members)
        {
            Verse.Pawn affected = null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                if (RHAH_BatchAttitude.CanOrderLeave(members[i]) &&
                    IsSource(CompRHAH_Pawn.TryGet(members[i]).State.sourceIncidentDisplayId))
                {
                    affected = members[i];
                    break;
                }
            }
            if (affected == null) return false;
            if (IsProvoked(affected)) return true;
            int batchId = CompRHAH_Pawn.TryGet(affected).State.spawnBatchId;
            List<Verse.Pawn> batch = RHAH_BatchAttitude.Collect(affected.MapHeld, batchId);
            for (int i = 0; i < members.Count; i++)
            {
                Verse.Pawn member = members[i];
                if (RHAH_BatchAttitude.CanOrderLeave(member) && member.MapHeld == affected.MapHeld &&
                    CompRHAH_Pawn.TryGet(member).State.spawnBatchId == batchId && !batch.Contains(member)) batch.Add(member);
            }
            List<Lord> lords = new List<Lord>();
            for (int i = 0; i < batch.Count; i++)
            {
                Verse.Pawn member = batch[i];
                if (!IsSource(CompRHAH_Pawn.TryGet(member).State.sourceIncidentDisplayId)) continue;
                RHAH_PawnState state = CompRHAH_Pawn.TryGet(member).State;
                // 已开始离场也必须停下反抗 保留吃饱记录但撤销离场和等粮
                state.lifecycle = RHAH_Lifecycle.SeekingFood;
                state.foodWaitUntilTick = -1;
                state.leaveAfterGameTick = -1;
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.Fight, true);
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.Steal, false);
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.FeedFromRelief, false);
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.EatOutsideRelief, false);
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.Beg, false);
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.Gnaw, false);
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.ExitMap, false);
                RHAH_Api.SetGate(member, RHAH_BehaviorGate.LeaveAfterFed, false);
                Lord lord = member.GetLord();
                if (lord != null && !lords.Contains(lord)) lords.Add(lord);
                if (member.mindState != null) member.mindState.enemyTarget = null;
                member.jobs?.ClearQueuedJobs();
            }
            RHAH_BatchAttitude.StartArmedAssault(batch);
            for (int i = 0; i < lords.Count; i++)
            {
                Lord lord = lords[i];
                (lord.LordJob as LordJob_RHAH_Visitor)?.EndFoodWait();
                for (int j = 0; j < lord.Graph.lordToils.Count; j++)
                {
                    if (lord.Graph.lordToils[j] is LordToil_RHAH_ArmedAssault)
                    {
                        lord.GotoToil(lord.Graph.lordToils[j]);
                        break;
                    }
                }
            }
            for (int i = 0; i < batch.Count; i++)
            {
                Verse.Pawn member = batch[i];
                if (!IsProvoked(member) || !member.Spawned || member.Downed || member.jobs == null) continue;
                if (member.mindState != null)
                    member.mindState.duty = new PawnDuty(RHAH_DefOf.RHAH_VisitorSeek, member.Position, 3f);
                if (member.jobs?.curJob != null) member.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                Job attack = JobGiver_RHAH_Visitor.TryFight(member);
                if (attack != null) member.jobs?.StartJob(attack, JobCondition.InterruptForced);
            }
            return true;
        }

        internal static Job TryCreateJob(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            LordJob_RHAH_Visitor visitor = pawn?.GetLord()?.LordJob as LordJob_RHAH_Visitor;
            if (comp == null || !IsSource(comp.State.sourceIncidentDisplayId) || IsProvoked(pawn) ||
                !RHAH_BatchAttitude.CanOrderLeave(pawn) || !pawn.Spawned || pawn.Downed ||
                visitor?.ListedForFood(pawn) == true || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return null;
            int now = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            if (now < comp.State.gnawSearchUntilTick) return null;
            comp.State.gnawSearchUntilTick = now + RHAH_ReliefFood.RetryBaseTicks + pawn.thingIDNumber % 60;
            searcher = pawn;
            try
            {
                if (RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Steal))
                {
                    Thing target = FindTarget(ThingRequestGroup.HaulableEverOrMinifiable, movableValidator);
                    IntVec3 exit;
                    if (target != null && RCellFinder.TryFindBestExitSpot(pawn, out exit))
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Steal, target, exit);
                        float volume = target.def.VolumePerUnit;
                        int carry = volume > 0f ? (int)(pawn.GetStatValue(StatDefOf.CarryingCapacity) / volume) : target.stackCount;
                        job.count = Mathf.Max(1, Mathf.Min(target.stackCount, carry));
                        return job;
                    }
                }
                if (RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Gnaw))
                {
                    Thing target = FindTarget(ThingRequestGroup.BuildingArtificial, buildingValidator);
                    if (target != null) return JobMaker.MakeJob(RHAH_DefOf.RHAH_Gnaw, target);
                }
                return null;
            }
            finally
            {
                searcher = null;
            }
        }

        static Thing FindTarget(ThingRequestGroup group, Predicate<Thing> validator)
        {
            return GenClosest.ClosestThing_Regionwise_ReachablePrioritized(searcher.Position, searcher.Map,
                ThingRequest.ForGroup(group), PathEndMode.ClosestTouch, TraverseParms.For(searcher, Danger.Some),
                SearchRadius, validator, priority, 15, 15);
        }

        static bool MovableTarget(Thing thing)
        {
            return EligibleTarget(thing) && thing.def.stealable &&
                (thing.def.category == ThingCategory.Item || thing.def.Minifiable) &&
                (IsFood(thing) || StealAIUtility.GetValue(thing) >= HighValue);
        }

        static bool BuildingTarget(Thing thing)
        {
            return EligibleTarget(thing) && thing.Faction == Faction.OfPlayer && thing.def.useHitPoints &&
                !thing.def.Minifiable && StealAIUtility.GetValue(thing) >= HighValue;
        }

        static bool EligibleTarget(Thing thing)
        {
            return thing != null && thing.Spawned && !thing.Destroyed && !thing.IsBurning() &&
                !thing.IsForbidden(searcher) && (thing.Faction == null || thing.Faction == Faction.OfPlayer) &&
                searcher.CanReserve(thing);
        }

        static bool IsFood(Thing thing)
        {
            return thing.IngestibleNow && thing.def.ingestible?.CachedNutrition > 0f &&
                searcher.RaceProps.CanEverEat(thing);
        }

        static float TargetPriority(Thing thing)
        {
            return StealAIUtility.GetValue(thing) + (IsFood(thing) ? HighValue : 0f);
        }
    }

    // 原版收监先释放来客身份 必须在这一步之前通知仍自由的同批成员
    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.CapturedBy))]
    internal static class RHAH_MouseSwarmCapturePatch
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(Verse.Pawn ___pawn, Faction by)
        {
            if (by == Faction.OfPlayer) RHAH_MouseSwarm.TryProvoke(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
    internal static class RHAH_MouseSwarmCustodyPatch
    {
        static void Prefix(Verse.Pawn ___pawn, Faction newHost, GuestStatus guestStatus)
        {
            if (newHost == Faction.OfPlayer && (guestStatus == GuestStatus.Prisoner || guestStatus == GuestStatus.Slave))
                RHAH_MouseSwarm.TryProvoke(___pawn);
        }
    }
}
