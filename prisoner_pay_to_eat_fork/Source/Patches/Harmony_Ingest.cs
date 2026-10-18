using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Charges colony prisoners meal tickets whenever they eat.
    /// Cost comes from <see cref="MarketPriceHelper.TicketCost(Thing, Pawn)"/>; balances support
    /// fractional tickets. Consumption may use credit down to the configured balance floor;
    /// wages and other income repay existing debt first. Unaffordable meals are refused intact.
    /// Riot/escaping prisoners skip the check when ignoreDuringRiot is on.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Harmony_Ingest
    {
        // Price and requested quantity are captured before consumption; the latter uses the exact
        // nutritionWanted passed to 1.7 Thing.Ingested. Stack deltas settle actual consumption.
        private class IngestEvent
        {
            public float unitCost;
            public int units;          // 估算份数（用于前置拦截判断；尸体等堆叠不变的场景也用它计费）
            public int stackBefore;    // 进食前的堆叠数，用于事后精确算份数
        }

        private static readonly ConditionalWeakTable<Thing, IngestEvent> pending = new ConditionalWeakTable<Thing, IngestEvent>();

        // 赊账关闭时"没钱不吃"的提示节流：同一囚犯每 0.1 天最多弹一次消息
        // （进食尝试会随饥饿周期反复发生，不节流会刷屏）
        private static readonly Dictionary<int, int> lastBlockedMsgTick = new Dictionary<int, int>();
        private const int BlockedMsgIntervalTicks = 6000;

        /// <summary>读档/换存档时清空进程级节流表（thingIDNumber 跨存档会复用，不能留着）。</summary>
        public static void ResetTransientState() => lastBlockedMsgTick.Clear();

        [HarmonyPrefix]
        public static bool Prefix(Thing __instance, Pawn ingester, float nutritionWanted)
        {
            if (!ShouldCharge(ingester, __instance?.def, out float unitCost))
            {
                // 防御性清理：若上一次 Ingested 异常中断留下了条目，别让这次误扣费
                pending.Remove(__instance);
                return true;
            }
            int units = CountUnits.ForIngestion(ingester, __instance, nutritionWanted);
            int stackBefore = __instance.stackCount;
            bool consumable = !__instance.Destroyed && __instance.IngestibleNow;

            // Invalid ingestion must retain vanilla's diagnostic without charging or refusing it.
            pending.Remove(__instance);
            if (!consumable) return true;

            var mgr = PrisonersPayToEat2Manager.Current;
            float available = mgr?.SpendingPower(ingester) ?? 0f;
            float cost = unitCost * units;
            if (available < cost)
            {
                NotifyBlocked(ingester, cost, available);
                return false;
            }

            pending.Add(__instance, new IngestEvent
            {
                unitCost = unitCost,
                units = units,
                stackBefore = stackBefore
            });
            return true;
        }

        /// <summary>
        /// 按实际吃掉的份数计费。份数优先用进食前后的 stackCount 差值反推（生食 / 肉干等
        /// 一次会吃多份，饿极了可能十几份）；差值取不到时退回前缀的估算值——
        /// 尸体每次只吃掉一个部位且 numTaken = 0、堆叠不变，这种按 1 份计（与原行为一致）。
        /// 不用 hook IngestedCalculateAmounts：它是 protected 且被 Corpse / Plant 重写。
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!pending.TryGetValue(__instance, out var evt))
            {
                pending.Remove(__instance);
                return;
            }
            pending.Remove(__instance);

            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;

            int units;
            if (__instance.Destroyed) units = Mathf.Max(1, evt.stackBefore);          // 整叠 / 整个被吃光
            else if (evt.stackBefore > __instance.stackCount) units = evt.stackBefore - __instance.stackCount;
            else units = evt.units;                                                  // 堆叠未变（尸体等）

            float cost = evt.unitCost * units;
            float available = mgr.AvailableBalance(ingester);

            if (available >= cost)
            {
                mgr.PayWithSupport(ingester, cost);
                if (ingester.Spawned)
                    MoteMaker.ThrowText(ingester.DrawPos, ingester.Map, "-" + cost.ToString("0.##"), new Color(1f, 0.7f, 0.3f));
                if (PrisonersPayToEat2Mod.Settings.logVerbose)
                    Log.Message($"[PPTE2] {ingester.LabelShortCap} ate {units}x {__instance.LabelNoCount} for {cost:0.##} tickets. balance剩={mgr.Balance(ingester):0.##}");
                return;
            }

            // Preflight has already checked the remaining credit allowance.
            if (PrisonersPayToEat2Mod.Settings.allowMealDebt)
            {
                mgr.PayWithSupport(ingester, cost);
                float debt = cost - available;
                if (ingester.Spawned)
                    MoteMaker.ThrowText(ingester.DrawPos, ingester.Map, "+" + debt.ToString("0.##") + "!", Color.red);
                if (PrisonersPayToEat2Mod.Settings.logVerbose)
                    Log.Warning($"[PPTE2] {ingester.LabelShortCap} ate {units}x {__instance.LabelNoCount} on credit: need={cost:0.##} paid={available:0.##} debt={debt:0.##}");
                Messages.Message("PPTE2_MealOnCredit".Translate(ingester.LabelShortCap,
                        __instance.LabelNoCount, debt.ToString("0.##"), PPTEName.Ticket),
                    ingester, MessageTypeDefOf.RejectInput);
                // 欠账压力（心情 -5；同 def 不叠加，只刷新持续时间）
                var debtThought = PrisonersPayToEat2Manager.GetThought("PPTE2_MealDebt");
                if (debtThought != null)
                    ingester.needs?.mood?.thoughts?.memories?.TryGainMemory(debtThought);
                return;
            }

            mgr.PayWithSupport(ingester, cost);
        }

        /// <summary>Refused consumption, throttled per prisoner.</summary>
        private static void NotifyBlocked(Pawn ingester, float cost, float balance)
        {
            if (PrisonersPayToEat2Mod.Settings.logVerbose)
                Log.Warning($"[PPTE2] {ingester.LabelShortCap} couldn't eat: need={cost:0.##} spending power={balance:0.##}");

            int now = Find.TickManager.TicksGame;
            if (lastBlockedMsgTick.TryGetValue(ingester.thingIDNumber, out int last)
                && now - last < BlockedMsgIntervalTicks) return;
            lastBlockedMsgTick[ingester.thingIDNumber] = now;

            Messages.Message("PPTE2_NoTicketsBlocked".Translate(ingester.LabelShortCap,
                    cost.ToString("0.##"), PPTEName.Ticket, balance.ToString("0.##")),
                ingester, MessageTypeDefOf.RejectInput);
        }

        internal static bool ShouldCharge(Pawn ingester, ThingDef foodDef, out float ticketCost)
        {
            ticketCost = 0f;
            if (ingester == null || foodDef == null) return false;
            if (!ingester.IsPrisonerOfColony)
            {
                // 顺带清理拦截提示的节流记录（释放/招募/越狱后不再需要）
                lastBlockedMsgTick.Remove(ingester.thingIDNumber);
                return false;
            }
            var s = PrisonersPayToEat2Mod.Settings;
            if (s.ignoreDuringRiot && (ingester.MentalStateDef != null || InPrisonBreak(ingester)))
                return false;
            if (!IsChargeableFood(foodDef)) return false;
            if (PrisonersPayToEat2Manager.Current?.IsExempt(ingester, FeeCategory.Food, foodDef) == true)
                return false;

            ticketCost = MarketPriceHelper.TicketCost(foodDef, ingester);
            return true;
        }

        /// <summary>
        /// 只对"能提供营养的可摄入物"收费：啤酒/仙馔等含营养的照收，
        /// 而魔鬼素、精神茶、醒神药等营养为 0 的药品不该被当成饭收费
        /// （与设置页食物定价列表的过滤条件一致）。
        /// </summary>
        internal static bool IsChargeableFood(ThingDef def)
            => def != null && def.ingestible != null && def.IsNutritionGivingIngestible;

        internal static bool InPrisonBreak(Pawn p)
        {
            if (p?.mindState == null || p.Map == null) return false;
            // Vanilla keeps Pawn.lord synchronized on add/remove and rebuilds it at PostLoadInit.
            var lord = p.lord; // escape lords carry a LordJob named *PrisonBreak*
            return lord != null && lord.LordJob != null
                && lord.LordJob.GetType().Name.Contains("PrisonBreak");
        }
    }

    /// <summary>
    /// Preflight quantities share vanilla's ceil/clamp rules. Food search estimates the ordinary
    /// FinalizeIngest request; ingestion itself uses its actual nutritionWanted argument, including
    /// direct callers. Corpses are billed per body-part meal and plants per plant.
    /// Actual settlement still uses the consumed stack delta.
    /// </summary>
    internal static class CountUnits
    {
        public static int Estimate(Pawn eater, Thing food)
        {
            if (eater == null || food == null || food.def == null) return 1;

            // Corpses consume one body part and plants one plant, not a nutrition-sized stack.
            if (food is Corpse || food is Plant) return 1;

            float wanted = eater.needs?.food?.NutritionWanted
                           ?? (food.GetStatValue(StatDefOf.Nutrition) * food.stackCount);
            var job = eater.jobs?.curJob;
            if (job != null)
            {
                if (job.ingestTotalCount)
                    wanted = food.GetStatValue(StatDefOf.Nutrition) * food.stackCount;
                else if (job.overeat)
                    wanted = Mathf.Max(wanted, 0.75f);
            }

            return ForIngestion(eater, food, wanted);
        }

        public static int ForIngestion(Pawn eater, Thing food, float nutritionWanted)
        {
            if (food == null || food is Corpse || food is Plant) return 1;
            float nutritionPerUnit = FoodUtility.NutritionForEater(eater, food);
            if (nutritionPerUnit <= 0.0001f) return 1;
            int units = Mathf.CeilToInt(nutritionWanted / nutritionPerUnit);
            units = Mathf.Min(units, food.stackCount);
            int cap = food.def.ingestible?.maxNumToIngestAtOnce ?? 0;
            if (cap > 0) units = Mathf.Min(units, cap);
            return Mathf.Max(units, 1);
        }
    }
}