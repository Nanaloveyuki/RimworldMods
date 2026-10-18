using System;
using System.Collections.Generic;

namespace MouseDisaster
{
    public static class MouseDisasterTraderTradePolicy
    {
        private const string MouseDisasterTraderKindDefName = "MouseDisaster_RatkinTrader";

        private static readonly string[] OptionalGoods =
        {
            "RatEgg_Meat",
            "RatEgg_Ear",
            "RatEgg_Tail",
            "RatEgg_Brain",
            "RatEgg_Viscera",
            "RatEgg_SilkSkin"
        };

        private static readonly string[] RatEggCuisineKeywords =
        {
            "rategg",
            "rat_egg",
            "rat egg",
            "鼠蛋"
        };

        public static bool IsRatEggTradeGood(string defName, string label)
        {
            if (string.IsNullOrWhiteSpace(defName) && string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            for (int i = 0; i < OptionalGoods.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(defName) &&
                    defName.Equals(OptionalGoods[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return MatchesCuisineKeywords(defName, label);
        }

        public static IReadOnlyList<string> OptionalGoodDefNames { get; } = Array.AsReadOnly(OptionalGoods);

        public static bool MatchesCuisineKeywords(string defName, string label)
        {
            string source = ((defName ?? string.Empty) + " " + (label ?? string.Empty)).ToLowerInvariant();
            for (int i = 0; i < RatEggCuisineKeywords.Length; i++)
            {
                if (source.Contains(RatEggCuisineKeywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ShouldForceTraderWillTrade(string traderKindDefName)
        {
            return !string.IsNullOrWhiteSpace(traderKindDefName) &&
                   traderKindDefName.Equals(MouseDisasterTraderKindDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanTraderStockRatEggTradeGood(string traderKindDefName, string defName, string label)
        {
            return ShouldForceTraderWillTrade(traderKindDefName) &&
                   IsRatEggTradeGood(defName, label);
        }
    }
}
