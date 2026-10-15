using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Trade
{
    // 原版商队会因温度、天气或到不了边缘离图 本模组商队按停留规则拦住
    [HarmonyPatch(typeof(Transition), nameof(Transition.CheckSignal))]
    internal static class RHAH_CaravanLeavePatch
    {
        static bool Prefix(Transition __instance, Lord lord)
        {
            if (__instance == null || lord?.LordJob is not LordJob_TradeWithColony || __instance.preActions == null)
            {
                return true;
            }

            Verse.Pawn trader = TraderCaravanUtility.FindTrader(lord);
            if (!RHAH_CaravanStay.IsTradeCaravan(trader))
            {
                return true;
            }

            bool ignoreHarsh = RHAH_Mod.Settings == null || RHAH_Mod.Settings.traderIgnoresHarshEnvironment;
            bool ignoreEnclosed = RHAH_Mod.Settings == null || RHAH_Mod.Settings.traderIgnoresEnclosedSpace;
            for (int i = 0; i < __instance.preActions.Count; i++)
            {
                string key = RHAH_CaravanStay.MessageKey(__instance.preActions[i]);
                if (RHAH_CaravanStay.IsEnvironmentLeave(ignoreHarsh, true, key) ||
                    RHAH_CaravanStay.IsEnclosedLeave(ignoreEnclosed, true, key))
                {
                    return false;
                }
            }

            return true;
        }
    }

    // 商队货物不含食物 玩家仍可用食物换孩子
    [HarmonyPatch(typeof(TraderKindDef), nameof(TraderKindDef.WillTrade))]
    internal static class RHAH_CaravanFoodSalePatch
    {
        static void Postfix(ThingDef td, ref bool __result)
        {
            if (!__result || td == null || !td.IsNutritionGivingIngestible)
            {
                return;
            }
            // 专用买粮 generator 已限定 Sellable 不用旧双向禁售覆盖买方
            if (TradeSession.trader?.TraderKind?.defName == "RHAH_Trader_Famine") return;

            Verse.Pawn trader = TradeSession.trader as Verse.Pawn;
            if (trader != null && RHAH_CaravanStay.IsTradeCaravan(trader) &&
                !RHAH_CaravanStay.NpcSellsFood(true, true, td.defName))
            {
                __result = false;
            }
        }
    }
}
