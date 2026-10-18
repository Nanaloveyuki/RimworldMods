using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 娱乐计费：囚犯每娱乐满 1 个游戏小时计一次费（默认 50 白银×汇率，可按建筑单独定价），
    /// 娱乐工作结束时按不足一小时的零头比例补收。费用只针对"使用娱乐建筑"——散步、冥想等
    /// 无建筑娱乐免费。
    /// 赊账开启且付不起：扣成负数（与吃饭欠账同一套 pressure）；赊账关闭且余额不足：
    /// 在 <see cref="Patch_JobGiverGetJoy"/> 直接拦下娱乐工作（付不起就不去），
    /// 期间把钱花光的边界情况按实有余额补收、不扣成负数。
    /// </summary>
    public static class PrisonerJoyCharge
    {
        /// <summary>一次计费的娱乐时长：1 游戏小时（2500 tick）。</summary>
        public const int SessionTicks = 2500;

        // 每囚犯累计未结算的娱乐 tick；建筑 defName 随累计一起记（结束补收时用，建筑可能中途被拆）
        private static readonly Dictionary<int, float> accumTicks = new Dictionary<int, float>();
        private static readonly Dictionary<int, string> accumBuilding = new Dictionary<int, string>();

        // 赊账关闭时被拦截提示的节流（每囚犯每 0.1 天最多一条）
        private static readonly Dictionary<int, int> lastBlockedMsgTick = new Dictionary<int, int>();
        private const int BlockedMsgIntervalTicks = 6000;

        public static void ResetTransientState()
        {
            accumTicks.Clear();
            accumBuilding.Clear();
            lastBlockedMsgTick.Clear();
        }

        /// <summary>娱乐收费对某囚犯生效（开关 + 未被豁免）。</summary>
        public static bool FeesApply(Pawn pawn)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (s == null || !s.enableJoyFees) return false;
            if (pawn == null || !pawn.IsPrisonerOfColony) return false;
            return PrisonersPayToEat2Manager.Current?.IsExempt(pawn, FeeCategory.Joy) != true;
        }

        /// <summary>某建筑一次计费的饭票价格（自定义按建筑价，否则全局默认价；白银×汇率）。</summary>
        public static float FeeFor(string buildingDefName)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            float silver = s.joyFeePerUse;
            if (!string.IsNullOrEmpty(buildingDefName) && s.customJoyFees.TryGetValue(buildingDefName, out float v))
                silver = v;
            return silver * s.silverToTicketRate;
        }

        /// <summary>娱乐 tick：充能自定义娱乐条 + 累计计费时长（建筑娱乐才计费）。</summary>
        public static void Accumulate(Pawn pawn, int delta, Building joySource)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (s == null) return;

            if (s.enablePrisonerJoyNeed && pawn.IsPrisonerOfColony)
                pawn.needs?.TryGetNeed<Need_PrisonerJoy>()?.NotifyJoyTick(delta, 1f);

            if (!FeesApply(pawn) || delta <= 0) return;

            int id = pawn.thingIDNumber;
            accumTicks.TryGetValue(id, out float v);
            v += delta;
            if (joySource != null)
                accumBuilding[id] = joySource.def?.defName;

            // 每满 1 小时立刻收一次费（长时间娱乐按小时计）
            while (v >= SessionTicks)
            {
                v -= SessionTicks;
                ChargeOnce(pawn, accumBuilding.TryGetValue(id, out var dn) ? dn : null, 1f);
            }
            accumTicks[id] = v;
        }

        /// <summary>娱乐工作结束（含被打断）：按不足一小时的零头比例补收。</summary>
        public static void FinishJob(Pawn pawn)
        {
            int id = pawn.thingIDNumber;
            if (!accumTicks.TryGetValue(id, out float v))
            {
                accumBuilding.Remove(id);
                return;
            }
            string defName = accumBuilding.TryGetValue(id, out var dn) ? dn : null;
            accumTicks.Remove(id);
            accumBuilding.Remove(id);
            if (!FeesApply(pawn) || v <= 0f) return;
            ChargeOnce(pawn, defName, Mathf.Clamp01(v / SessionTicks));
        }

        private static void ChargeOnce(Pawn pawn, string buildingDefName, float fraction)
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            var s = PrisonersPayToEat2Mod.Settings;
            if (mgr == null || s == null) return;

            // 无建筑娱乐（散步/冥想/望远等）免费
            float cost = buildingDefName == null ? 0f : FeeFor(buildingDefName) * fraction;
            if (cost < 0.009f) return;

            float available = mgr.AvailableBalance(pawn);
            float paid = Mathf.Min(cost, available);
            if (paid > 0.009f || s.allowMealDebt)
            {
                // 赊账开启时全额（可为负）；关闭时只收实有
                float charge = s.allowMealDebt ? cost : paid;
                mgr.PayWithSupport(pawn, charge);
                if (pawn.Spawned)
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "-" + charge.ToString("0.##"), new Color(1f, 0.7f, 0.3f));

                if (s.allowMealDebt && cost > available + 0.009f)
                {
                    if (pawn.Spawned)
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "+" + (cost - available).ToString("0.##") + "!", Color.red);
                    Messages.Message("PPTE2_JoyOnCredit".Translate(pawn.LabelShortCap,
                            (cost - available).ToString("0.##"), PPTEName.Ticket),
                        pawn, MessageTypeDefOf.RejectInput);
                    var debtThought = PrisonersPayToEat2Manager.GetThought("PPTE2_MealDebt");
                    if (debtThought != null)
                        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(debtThought);
                }
                if (s.logVerbose)
                    Log.Message($"[PPTE2] {pawn.LabelShortCap} joy fee {charge:0.##} tickets ({buildingDefName ?? "?"}, fraction={fraction:0.##}). balance={mgr.Balance(pawn):0.##}");
            }
        }

        /// <summary>赊账关闭且余额不足时的拦截提示（带节流）。</summary>
        public static void NotifyBlocked(Pawn pawn, float cost, float balance)
        {
            int now = Find.TickManager.TicksGame;
            if (lastBlockedMsgTick.TryGetValue(pawn.thingIDNumber, out int last)
                && now - last < BlockedMsgIntervalTicks) return;
            lastBlockedMsgTick[pawn.thingIDNumber] = now;
            Messages.Message("PPTE2_JoyBlocked".Translate(pawn.LabelShortCap,
                    cost.ToString("0.##"), PPTEName.Ticket, balance.ToString("0.##")),
                pawn, MessageTypeDefOf.RejectInput);
        }
    }

    public static class Harmony_JoyCharges
    {
        /// <summary>囚犯娱乐 tick：娱乐条充能 + 累计计费时长。</summary>
        [HarmonyPatch(typeof(JoyUtility), nameof(JoyUtility.JoyTickCheckEnd))]
        public static class Patch_JoyTickCheckEnd
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, int delta, Building joySource)
            {
                if (pawn == null || !pawn.IsPrisonerOfColony) return;
                PrisonerJoyCharge.Accumulate(pawn, delta, joySource);
            }
        }

        /// <summary>娱乐工作结束（成功/中断都算）：补收零头。前缀快照，结束时 curDriver 已被清理。</summary>
        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
        public static class Patch_JoyJobEnd
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn_JobTracker __instance, out JobDriver __state)
            {
                var d = __instance.curDriver;
                __state = d != null && d.pawn != null && d.pawn.IsPrisonerOfColony
                    && d.job?.def?.joyKind != null ? d : null;
            }

            [HarmonyPostfix]
            public static void Postfix(JobDriver __state)
            {
                if (__state?.pawn == null) return;
                PrisonerJoyCharge.FinishJob(__state.pawn);
            }
        }

        /// <summary>赊账关闭且付不起一次完整娱乐的费用时，直接不给派发娱乐工作。</summary>
        [HarmonyPatch(typeof(JobGiver_GetJoy), "TryGiveJob")]
        public static class Patch_JobGiverGetJoy
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, ref Job __result)
            {
                if (__result == null) return;
                var s = PrisonersPayToEat2Mod.Settings;
                if (s == null || !s.enableJoyFees || s.allowMealDebt) return;
                if (!PrisonerJoyCharge.FeesApply(pawn)) return;

                string defName = (__result.targetA.Thing as Building)?.def?.defName;
                float cost = defName == null ? 0f : PrisonerJoyCharge.FeeFor(defName);
                if (cost < 0.009f) return; // 无建筑娱乐免费，不拦
                var mgr = PrisonersPayToEat2Manager.Current;
                if (mgr == null) return;
                float available = mgr.AvailableBalance(pawn);
                if (available >= cost) return;
                __result = null;
                PrisonerJoyCharge.NotifyBlocked(pawn, cost, available);
            }
        }
    }
}
