using System;
using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Trade
{
    internal static class RHAH_FamineTrade
    {
        internal static float Price(bool applies, bool food, TradeAction action, float original,
            float vanillaBuy, float vanillaSell, float foodSaleFactor, float goodsBuyFactor)
        {
            if (!applies) return original;
            if (food && action == TradeAction.PlayerSells) return Math.Min(original * foodSaleFactor, vanillaBuy);
            if (!food && action == TradeAction.PlayerBuys) return Math.Max(original * goodsBuyFactor, vanillaSell);
            return original;
        }

        internal static void Stock(Verse.Pawn trader, Verse.Pawn carrier)
        {
            TraderKindDef kind = trader.trader?.traderKind;
            if (kind?.defName != "RHAH_Trader_Famine" || carrier?.inventory == null) return;
            for (int i = 0; i < kind.stockGenerators.Count; i++)
            {
                foreach (Thing thing in kind.stockGenerators[i].GenerateThings(trader.Map.Tile, trader.Faction))
                {
                    if (thing == null) continue;
                    if (thing.def.IsNutritionGivingIngestible || !carrier.inventory.innerContainer.TryAdd(thing, false))
                        thing.Destroy();
                }
            }
        }
    }

    // 原版交易基价与禁售先执行 只改本模组商队对应的交易方向
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    internal static class RHAH_FaminePricePatch
    {
        static void Postfix(Tradeable __instance, TradeAction action, float ___pricePlayerBuy,
            float ___pricePlayerSell, ref float __result)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            Verse.Pawn trader = TradeSession.trader as Verse.Pawn;
            bool applies = (settings == null || settings.famineTradeEnabled) && trader != null &&
                RHAH_CaravanStay.IsTradeCaravan(trader) && trader.Faction?.IsPlayer != true &&
                HungerAndHavoc.Api.RHAH_Api.IsVisitor(trader) && !trader.IsPrisoner && !trader.IsSlave &&
                !__instance.IsCurrency && TradeSession.TradeCurrency == TradeCurrency.Silver;
            __result = RHAH_FamineTrade.Price(applies, __instance.ThingDef?.IsNutritionGivingIngestible == true,
                action, __result, ___pricePlayerBuy, ___pricePlayerSell,
                settings == null ? 1.5f : settings.famineFoodSaleFactor,
                settings == null ? 0.75f : settings.famineGoodsBuyFactor);
        }
    }
}
