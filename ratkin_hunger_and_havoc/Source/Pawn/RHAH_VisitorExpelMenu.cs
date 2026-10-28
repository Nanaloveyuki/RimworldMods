using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using Verse.AI.Group;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    // 原版扫描公开子类 殖民者右键在场来客时出现
    public class RHAH_VisitorExpelMenu : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Verse.Pawn clickedPawn, FloatMenuContext context)
        {
            if (!RHAH_BatchAttitude.CanOrderLeave(clickedPawn))
            {
                yield break;
            }

            Verse.Pawn actor = context.FirstSelectedPawn;
            if (actor == null || actor.Faction != Faction.OfPlayer || actor.Dead)
            {
                yield break;
            }

            yield return new FloatMenuOption(
                "RHAH_Menu_ExpelAll".Translate(),
                () => AssignExpel(actor, clickedPawn, true),
                MenuOptionPriority.Default,
                null,
                clickedPawn);
            yield return new FloatMenuOption(
                "RHAH_Menu_ExpelSingle".Translate(clickedPawn.LabelShort),
                () => AssignExpel(actor, clickedPawn, false),
                MenuOptionPriority.Default,
                null,
                clickedPawn);
            if (RHAH_Api.Allows(clickedPawn, RHAH_BehaviorGate.JoinColony))
            {
                yield return new FloatMenuOption("RHAH_Choice_Join".Translate(), () =>
                {
                    if (!RHAH_ColonyGates.AllowsJoin(clickedPawn))
                    {
                        return;
                    }
                    RHAH_VisitorRules.ApplyPlayerIdeo(
                        clickedPawn,
                        RHAH_Mod.Settings == null ? 100 : RHAH_Mod.Settings.playerIdeoPercent,
                        Rand.Value);
                    clickedPawn.SetFaction(Faction.OfPlayer);
                    RHAH_VisitorMood.NotifySheltered(clickedPawn);
                });
            }

            if (RHAH_Api.Allows(clickedPawn, RHAH_BehaviorGate.Hire))
            {
                yield return new FloatMenuOption("RHAH_Choice_Hire".Translate(StayText(true)), () =>
                    RHAH_VisitorStay.Begin(clickedPawn, RHAH_StayKind.Hire));
            }
            if (RHAH_Api.Allows(clickedPawn, RHAH_BehaviorGate.JoinColony))
            {
                yield return new FloatMenuOption("RHAH_Choice_Shelter".Translate(StayText(false)), () =>
                    RHAH_VisitorStay.Begin(clickedPawn, RHAH_StayKind.Shelter));
            }

            string blocked = RHAH_FoodHandoff.Refusal(clickedPawn);
            if (blocked != null)
            {
                yield return new FloatMenuOption("RHAH_Choice_Feed".Translate(), null)
                {
                    Disabled = true,
                    tooltip = new TipSignal(blocked.Translate(clickedPawn.LabelShort))
                };
            }
            else
            {
                int each = RHAH_RequestRules.FoodRequestCount(1);
                if (each <= 0 || JobDefOf.GiveToPawn == null)
                {
                    yield return new FloatMenuOption("RHAH_Choice_Feed".Translate(), null)
                    {
                        Disabled = true,
                        tooltip = new TipSignal("RHAH_Choice_FeedBlocked".Translate(clickedPawn.LabelShort))
                    };
                }
                else
                {
                    LordJob_RHAH_Visitor job = clickedPawn.GetLord() == null
                        ? null
                        : clickedPawn.GetLord().LordJob as LordJob_RHAH_Visitor;
                    List<Thing> foods = new List<Thing>();
                    RHAH_ChoiceRecord demand = RHAH_ArmedDemand.FoodRequest(clickedPawn);
                    RHAH_FoodHandoff.CollectFood(actor, foods, clickedPawn, demand != null);
                    if (foods.Count == 0)
                    {
                        yield return new FloatMenuOption(
                            "RHAH_Choice_NoFood".Translate(each, clickedPawn.LabelShort),
                            null);
                    }

                    int listed = 0;
                    for (int i = 0; i < foods.Count; i++)
                    {
                        Thing food = foods[i];
                        int amount = UnitsOffered(job, demand, clickedPawn, food, each);
                        if (amount <= 0)
                        {
                            continue;
                        }

                        listed++;
                        yield return new FloatMenuOption(
                            "RHAH_Choice_GiveFood".Translate(amount + " " + food.def.label, clickedPawn.LabelShort),
                            () => GiveFood(actor, clickedPawn, food, each));
                    }

                    if (foods.Count > 0 && listed == 0)
                    {
                        yield return new FloatMenuOption("RHAH_Choice_Feed".Translate(), null)
                        {
                            Disabled = true,
                            tooltip = new TipSignal("RHAH_Choice_FeedNotHungry".Translate(clickedPawn.LabelShort))
                        };
                    }
                }
            }
        }

        // 件数按营养折算 要人喂的按这一口能吃几件 未登记的来客按设置里的份数预演
        // 最后按这堆实有量截断 免得标签承诺得比堆里还多
        static int UnitsOffered(LordJob_RHAH_Visitor job, RHAH_ChoiceRecord demand, Verse.Pawn receiver, Thing food, int each)
        {
            int units;
            if (demand != null)
            {
                units = RHAH_DemandFood.Units(RHAH_DemandFood.Remaining(demand), RHAH_DemandFood.Nutrition(food));
            }
            else if (Compat.RHAH_ChildMovement.NeedsHandFeed(receiver))
            {
                units = RHAH_FoodHandoff.DirectFeedUnits(receiver, food);
            }
            else
            {
                units = job == null ? each : job.FoodUnitsFor(receiver, food.def, each);
            }

            return System.Math.Min(units, food.stackCount);
        }

        static void GiveFood(Verse.Pawn actor, Verse.Pawn receiver, Thing food, int count)
        {
            if (actor == null || actor.Dead || actor.jobs == null || food == null || receiver == null || count <= 0)
            {
                return;
            }

            RHAH_ChoiceRecord demand = RHAH_ArmedDemand.FoodRequest(receiver);
            if (demand != null && !RHAH_DemandFood.Allowed(food.def))
            {
                Messages.Message("RHAH_Choice_FeedBlocked".Translate(receiver.LabelShort), MessageTypeDefOf.RejectInput);
                return;
            }

            // 要人喂的来客由殖民者当场直喂 不产生搬运 不登记等饭
            if (Compat.RHAH_ChildMovement.NeedsHandFeed(receiver))
            {
                RHAH_FoodHandoff.FeedDirectly(receiver, food);
                return;
            }

            if (JobDefOf.GiveToPawn == null)
            {
                Messages.Message(
                    "RHAH_Choice_FeedBlocked".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            if (!RHAH_FoodHandoff.Start(receiver, count))
            {
                Messages.Message(
                    "RHAH_Choice_FeedNoVisitor".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            LordJob_RHAH_Visitor job = receiver.GetLord() == null
                ? null
                : receiver.GetLord().LordJob as LordJob_RHAH_Visitor;
            int needed = demand != null ? RHAH_DemandFood.Units(
                RHAH_DemandFood.Remaining(demand) - RHAH_DemandFood.BeingHauledTo(receiver), RHAH_DemandFood.Nutrition(food))
                : job == null ? count : job.FoodStillNeeded(receiver, food.def);
            if (needed <= 0)
            {
                Messages.Message(
                    "RHAH_Choice_FeedNotHungry".Translate(receiver.LabelShort),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            if (job != null)
            {
                job.NoteFood(food.def);
            }

            Job give = JobMaker.MakeJob(JobDefOf.GiveToPawn, food, receiver);
            give.haulMode = HaulMode.ToContainer;
            give.count = needed;
            give.lord = receiver.GetLord();
            actor.jobs.TryTakeOrderedJob(give, JobTag.Misc);
        }

        static void AssignExpel(Verse.Pawn actor, Verse.Pawn target, bool wholeBatch)
        {
            JobDef jobDef = wholeBatch ? RHAH_DefOf.RHAH_Expel : RHAH_DefOf.RHAH_ExpelSingle;
            if (actor == null || actor.Dead || actor.jobs == null || jobDef == null)
            {
                return;
            }

            if (!RHAH_BatchAttitude.CanOrderLeave(target) || target.Map != actor.Map)
            {
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, target);
            job.playerForced = true;
            actor.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        static string StayText(bool hire)
        {
            HungerAndHavoc.Core.RHAH_Settings settings = HungerAndHavoc.Core.RHAH_Mod.Settings;
            int days = hire
                ? (settings == null ? RHAH_VisitorRules.DefaultHireDays : settings.hireDays)
                : (settings == null ? RHAH_VisitorRules.DefaultShelterDays : settings.shelterDays);
            return RHAH_VisitorRules.StayLabel(days);
        }
    }

    // 原版按整个 Lord 扣正在搬运的数量 同批第二人会被第一人的搬运清成 0
    // 现在只回答「还要不要再搬一趟」 件数见下面按实际食物重算的那条
    [HarmonyLib.HarmonyPatch(typeof(GiveItemsToPawnUtility), nameof(GiveItemsToPawnUtility.ItemCountLeftToCollect))]
    internal static class RHAH_GiveToPawnCountPatch
    {
        static bool Prefix(Verse.Pawn requester, ref int __result)
        {
            if (!RHAH_FoodHandoff.TryCountLeft(requester, out int left))
            {
                return true;
            }

            __result = left;
            return false;
        }
    }

    // 原版把 ItemCountLeftToCollect 直接写进 job.count 换食物时会按上一次的类型折件数
    // 按这趟手上真正要搬的食物重算 且必须排在原版 initAction 之后 否则会被它覆盖
    [HarmonyLib.HarmonyPatch(typeof(JobDriver_GiveToPawn), "DetermineNumToHaul")]
    internal static class RHAH_GiveToPawnHaulCountPatch
    {
        static void Postfix(JobDriver_GiveToPawn __instance, ref Toil __result)
        {
            if (__result == null || __result.initAction == null)
            {
                return;
            }

            System.Action vanilla = __result.initAction;
            __result.initAction = () =>
            {
                vanilla();
                Haul(__instance);
            };
        }

        static void Haul(JobDriver_GiveToPawn driver)
        {
            // 手上有货时结束 job 会把货丢在脚下 这一趟交给外层 JumpIf 再走
            if (!RHAH_FoodHandoff.IsOurFeed(driver.pawn) || driver.CountBeingHauled > 0)
            {
                return;
            }

            Job job = driver.job;
            Verse.Pawn receiver = job.GetTarget(TargetIndex.B).Pawn;
            Thing food = job.GetTarget(TargetIndex.A).Thing;
            if (food == null || receiver.GetLord() == null)
            {
                return;
            }

            LordJob_RHAH_Visitor visitor = receiver.GetLord().LordJob as LordJob_RHAH_Visitor;
            int units = visitor.HaulUnitsFor(receiver, food, driver);
            if (units <= 0)
            {
                driver.pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                return;
            }

            job.count = units;
        }
    }

    // 原版搬上第一趟后还会在 8 格内找同类的可叠堆 找到就换目标整段重来
    // 普通投喂只搬菜单承诺的那几件 不够交给外层 JumpIf 再走一趟 所以这里整段跳过
    [HarmonyLib.HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.CheckForGetOpportunityDuplicate))]
    internal static class RHAH_GiveToPawnDuplicatePatch
    {
        static void Postfix(Toil __result)
        {
            if (__result == null || __result.initAction == null)
            {
                return;
            }

            System.Action vanilla = __result.initAction;
            __result.initAction = () =>
            {
                if (IsOurHaul(__result.actor))
                {
                    return;
                }

                vanilla();
            };
        }

        // duplicate 这一步只在手上有货时才跑
        static bool IsOurHaul(Verse.Pawn actor)
        {
            return actor != null && actor.carryTracker != null && actor.carryTracker.CarriedThing != null &&
                RHAH_FoodHandoff.IsOurFeed(actor);
        }
    }

    // 已登记等饭的来客在走到等待点之前也必须能收下食物 否则原版投喂 job 会当条件失败立刻中止
    // 与上面那条补丁同一判据 逐件计数不会走到原版的 toil 强转
    [HarmonyLib.HarmonyPatch(typeof(GiveItemsToPawnUtility), nameof(GiveItemsToPawnUtility.IsWaitingForItems))]
    internal static class RHAH_GiveToPawnWaitPatch
    {
        static bool Prefix(Verse.Pawn pawn, ref bool __result)
        {
            if (__result || pawn == null)
            {
                return true;
            }

            LordJob_RHAH_Visitor job = pawn.GetLord() == null
                ? null
                : pawn.GetLord().LordJob as LordJob_RHAH_Visitor;
            if (job == null || !job.ListedForFood(pawn))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }
}
