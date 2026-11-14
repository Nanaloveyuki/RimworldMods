using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Trade
{
    internal static class RHAH_BackgroundTrade
    {
        internal static bool RatkinWeapon(ThingDef def)
        {
            if (def?.IsWeapon != true) return false;
            string package = def.modContentPack?.PackageIdPlayerFacing;
            return string.Equals(package, "Solaris.RatkinRaceMod", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(package, "bbb.ratkinweapon.morefailure", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(package, RHAH_WarlordCompat.WarlordPackage, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(package, RHAH_WarlordCompat.UndergroundPackage, System.StringComparison.OrdinalIgnoreCase);
        }

        internal static EventChainInstance Offer(ThingDef def, TradeAction action, bool currency)
        {
            if (action != TradeAction.PlayerBuys || currency || def == null || TradeSession.giftMode ||
                TradeSession.trader?.Faction?.def?.defName != Incidents.RHAH_BackgroundStories.KingdomDef ||
                TradeSession.TradeCurrency != TradeCurrency.Silver || RHAH_Mod.Settings?.background.tradeDiscountPercent <= 0f) return null;
            bool weapons = RatkinWeapon(def);
            if (!weapons && !def.IsNutritionGivingIngestible) return null;
            return RHAH_BackgroundTradeFollow.Pending(TradeSession.trader.Faction.loadID, weapons);
        }

        [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
        static class PricePatch
        {
            [HarmonyPostfix]
            static void Postfix(Tradeable __instance, TradeAction action, ref float __result)
            {
                if (Offer(__instance.ThingDef, action, __instance.IsCurrency) != null)
                    __result *= 1f - RHAH_Mod.Settings.background.tradeDiscountPercent / 100f;
            }
        }

        [HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
        static class ExecutePatch
        {
            [HarmonyPrefix]
            static void Prefix(TradeDeal __instance, out int[] __state)
            {
                __state = new int[2];
                foreach (Tradeable tradeable in __instance.AllTradeables)
                {
                    EventChainInstance offer = Offer(tradeable.ThingDef, tradeable.ActionToDo, tradeable.IsCurrency);
                    if (offer == null) continue;
                    __state[RatkinWeapon(tradeable.ThingDef) ? 1 : 0] = offer.Id;
                }
            }

            [HarmonyPostfix]
            static void Postfix(bool __result, bool actuallyTraded, int[] __state)
            {
                if (!__result || !actuallyTraded) return;
                for (int i = 0; i < __state.Length; i++)
                    if (__state[i] > 0) RHAH_BackgroundTradeFollow.Consume(__state[i], Find.TickManager.TicksGame);
            }
        }
    }
}
