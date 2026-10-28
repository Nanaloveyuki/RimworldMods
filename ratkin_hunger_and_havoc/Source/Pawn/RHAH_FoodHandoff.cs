using System;
using Verse.AI.Group;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_FoodHandoff
    {
        internal static int Begin(List<Verse.Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0)
            {
                return 0;
            }

            int each = RHAH_RequestRules.FoodRequestCount(1);
            int started = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (Refusal(pawns[i]) == null && Start(pawns[i], each))
                {
                    started++;
                }
            }

            return started;
        }

        internal static bool Start(Verse.Pawn receiver, int count)
        {
            if (count <= 0 || Refusal(receiver) != null)
            {
                return false;
            }

            // 索粮交入背包，只有普通亲喂不登记等饭。
            if (RHAH_ArmedDemand.FoodRequest(receiver) == null && Compat.RHAH_ChildMovement.NeedsHandFeed(receiver))
            {
                return false;
            }

            Lord lord = receiver.GetLord();
            LordJob_RHAH_Visitor job = lord == null ? null : lord.LordJob as LordJob_RHAH_Visitor;
            if (job == null)
            {
                return false;
            }

            if (job.BeginFoodWait(receiver, count))
            {
                ArmWait(receiver);
            }

            // 点击只登记 换到等饭 toil 由 LordJob 的到点判据驱动 不再当场打断行走
            if (!job.ListedForFood(receiver))
            {
                return false;
            }

            if (job.ArmedChoiceId == 0) RHAH_BatchAttitude.AcceptFood(receiver);
            return true;
        }

        internal static bool CanEnterFoodWait(LordToil toil)
        {
            return toil is LordToil_RHAH_VisitorSeek || toil is LordToil_RHAH_VisitorTravel;
        }


        internal static string Refusal(Verse.Pawn pawn)
        {
            Lord lord = pawn == null ? null : pawn.GetLord();
            // 要人喂的来客常因年龄阶段常年倒地 由殖民者直喂放行
            if (!RHAH_Api.IsVisitor(pawn) || pawn.Dead || !pawn.Spawned ||
                (pawn.Downed && !Compat.RHAH_ChildMovement.NeedsHandFeed(pawn)) ||
                !(lord != null && lord.LordJob is LordJob_RHAH_Visitor) ||
                (!CanEnterFoodWait(lord.CurLordToil) && !(lord.CurLordToil is LordToil_RHAH_WaitFood)) ||
                RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Leaving)
            {
                return "RHAH_Choice_FeedNoVisitor";
            }

            if (RHAH_Api.Get(pawn)?.SourceIncidentDisplayId == "I-053" || RHAH_MouseSwarm.IsProvoked(pawn))
            {
                return "RHAH_Choice_FeedBlocked";
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.FeedFromRelief))
            {
                return "RHAH_Choice_FeedBlocked";
            }

            if (RHAH_ArmedDemand.FoodRequest(pawn) != null) return null;

#if RIMWORLD_1_7
            if (!RHAH_Feeding.NeedsFood(pawn))
#else
            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot != null && snapshot.HasBeenFed)
#endif
            {
                return "RHAH_Choice_FeedNotHungry";
            }

            return null;
        }

        static void ArmWait(Verse.Pawn receiver)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(receiver);
            if (comp == null || Find.TickManager == null)
            {
                return;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            bool wait = settings == null || settings.waitWhenNoFood;
            float days = settings == null ? RHAH_VisitorRules.DefaultNoFoodWaitDays : settings.noFoodWaitDays;
            comp.SetFoodWait(RHAH_VisitorRules.NextWaitTick(
                Find.TickManager.TicksGame,
                -1,
                false,
                wait,
                days));
        }

        // 只列有效库存里接收者能吃的食物 排序仍按到点击者距离
        internal static void CollectFood(Verse.Pawn giver, List<Thing> result, Verse.Pawn receiver, bool demand = false)
        {
            if (result == null || giver == null || giver.Map == null || giver.Map.listerThings == null)
            {
                return;
            }

            List<Thing> things = giver.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            if (things == null)
            {
                return;
            }

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (Accepts(giver, thing, receiver, demand))
                {
                    result.Add(thing);
                }
            }

            result.Sort((left, right) =>
            {
                float leftDistance = left == null ? float.MaxValue : (left.PositionHeld - giver.Position).LengthHorizontalSquared;
                float rightDistance = right == null ? float.MaxValue : (right.PositionHeld - giver.Position).LengthHorizontalSquared;
                int distance = leftDistance.CompareTo(rightDistance);
                if (distance != 0)
                {
                    return distance;
                }

                string leftName = left == null || left.def == null ? null : left.def.defName;
                string rightName = right == null || right.def == null ? null : right.def.defName;
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });
        }

        // 在途食物的营养和 未完成搬运不计进账本
        // excluding 用于「自己这一趟不算在途」
        internal static double BeingHauledTo(Verse.Pawn receiver, JobDriver excluding = null)
        {
            if (receiver == null || receiver.Map == null || receiver.Map.mapPawns == null)
            {
                return 0;
            }

            double nutrition = 0;
            IReadOnlyList<Verse.Pawn> spawned = receiver.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                Verse.Pawn hauler = spawned[i];
                JobDriver_GiveToPawn driver = hauler == null || hauler.jobs == null
                    ? null
                    : hauler.jobs.curDriver as JobDriver_GiveToPawn;
                if (driver != null && driver != excluding && driver.job.GetTarget(TargetIndex.B).Pawn == receiver)
                {
                    Thing carried = hauler.carryTracker == null ? null : hauler.carryTracker.CarriedThing;
                    nutrition += RHAH_DemandFood.EdibleNutrition(carried?.def) * (carried?.stackCount ?? 0);
                }
            }

            return nutrition;
        }

        // 这趟 GiveToPawn 是不是我们自己的普通投喂 出发方与目标方都按这一份判据
        // 索粮走 RHAH_DemandHaulCountPatch 自己那条 DetermineNumToHaul 补丁 这里一律不接
        internal static bool IsOurFeed(Verse.Pawn giver)
        {
            Job job = giver == null ? null : giver.CurJob;
            if (job == null || job.def != JobDefOf.GiveToPawn)
            {
                return false;
            }

            Verse.Pawn receiver = job.GetTarget(TargetIndex.B).Pawn;
            LordJob_RHAH_Visitor visitor = receiver == null || receiver.GetLord() == null
                ? null
                : receiver.GetLord().LordJob as LordJob_RHAH_Visitor;
            return visitor != null && RHAH_ArmedDemand.FoodRequest(receiver) == null &&
                visitor.ListedForFood(receiver);
        }

        // 只给原版一个「还要不要再搬一趟」的信号 件数在搬货那一刻按实际食物重算
        internal static bool TryCountLeft(Verse.Pawn receiver, out int left)
        {
            left = 0;
            LordJob_RHAH_Visitor job = receiver == null || receiver.GetLord() == null
                ? null
                : receiver.GetLord().LordJob as LordJob_RHAH_Visitor;
            if (job == null || !job.ListedForFood(receiver))
            {
                return false;
            }

            left = job.FoodLeftToCollect(receiver) > 0 ? 1 : 0;
            return true;
        }

        // 要人喂的来客按胃容量缺口算一次吃几件
        internal static int DirectFeedUnits(Verse.Pawn receiver, Thing food)
        {
            Need_Food need = receiver == null || receiver.needs == null ? null : receiver.needs.food;
            double nutrition = RHAH_DemandFood.EdibleNutrition(food == null ? null : food.def);
            if (need == null || food == null || nutrition <= 0)
            {
                return 0;
            }

            double gap = need.MaxLevel - need.CurLevel;
            if (gap <= 0)
            {
                return 0;
            }

            int units = (int)Math.Ceiling(gap / nutrition);
            return units < food.stackCount ? units : food.stackCount;
        }

        // 直喂：从所选堆扣件后销毁 营养直接进胃 不产生搬运 不登记等饭
        internal static void FeedDirectly(Verse.Pawn receiver, Thing food)
        {
            int units = DirectFeedUnits(receiver, food);
            if (units <= 0)
            {
                Messages.Message(
                    "RHAH_Choice_FeedNotHungry".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            double nutrition = RHAH_DemandFood.EdibleNutrition(food.def);
            food.SplitOff(units).Destroy(DestroyMode.Vanish);
            receiver.needs.food.CurLevel += (float)(units * nutrition);
            LordJob_RHAH_Visitor job = receiver.GetLord() == null
                ? null
                : receiver.GetLord().LordJob as LordJob_RHAH_Visitor;
            if (job == null || job.ArmedChoiceId == 0) RHAH_BatchAttitude.AcceptFood(receiver);
            RHAH_Feeding.TryComplete(receiver);
        }

        static bool Accepts(Verse.Pawn giver, Thing thing, Verse.Pawn receiver, bool demand)
        {
            return thing != null &&
                thing.Spawned &&
                thing.IsInValidStorage() &&
                !thing.IsForbidden(giver) &&
                giver.CanReserve(thing) &&
                thing.def.IsIngestible &&
                (demand ? RHAH_DemandFood.Allowed(thing.def) : EdibleBy(receiver, thing.def));
        }

        // 婴儿额外检查原版适口性，仍遵循普通投喂名单与食物等级。
        static bool EdibleBy(Verse.Pawn receiver, ThingDef def)
        {
            if (receiver == null || def == null || !receiver.RaceProps.CanEverEat(def))
            {
                return false;
            }

            return RHAH_ReliefFood.GiveFoodAllowed(def) &&
                def.ingestible.preferability >= FoodPreferability.MealAwful &&
                (!Compat.RHAH_ChildMovement.IsFeedingBaby(receiver) || receiver.FoodIsSuitable(def));
        }
    }
}
