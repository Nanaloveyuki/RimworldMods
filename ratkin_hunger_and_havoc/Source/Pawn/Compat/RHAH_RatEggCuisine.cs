using System;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Trade;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn.Compat
{
    // 只认鼠蛋佳肴已加载的 Def 不引用它的程序集
    internal static class RHAH_RatEggCuisine
    {
        internal const int IngredientMin = 3;
        internal const int IngredientMax = 8;
        internal const int MealCount = 1;
        internal const int MealPick = 2;
        internal const float AdultAge = 14f;
        internal const string TailDefName = "RatEgg_Tail";
        internal const string MealPrefix = "Meal_RatEgg";
        internal const string ThoughtDefName = "RHAH_Thought_AteRatEggMeal";

        internal static readonly string[] Ingredients =
        {
            "RatEgg_Meat",
            "RatEgg_Ear",
            TailDefName,
            "RatEgg_Brain",
            "RatEgg_Viscera",
            "RatEgg_SilkSkin",
            "RatEgg_RoundHead"
        };

        internal static readonly string[] Meals =
        {
            "Meal_RatEggMeatStewed",
            "Meal_RatEggBrainSoup",
            "Meal_RatEggFullBodyFeast",
            "Meal_RatEggHeadCustard",
            "Meal_RatEggEarSlices",
            "Meal_RatEggTailPocky",
            "Meal_RatEggIntestineTwist",
            "Meal_RatEggSkinJelly"
        };

        internal static bool IsMeal(string defName)
        {
            return !string.IsNullOrEmpty(defName) &&
                defName.StartsWith(MealPrefix, StringComparison.Ordinal);
        }

        internal static bool NpcSells(bool isTradeCaravan, bool foodIsNutrition, string defName)
        {
            if (!isTradeCaravan || !foodIsNutrition)
            {
                return true;
            }

            return IsMeal(defName);
        }

        internal static bool CanStock(bool exists, bool corpse, float marketValue, bool willTrade)
        {
            return exists && !corpse && marketValue > 0f && willTrade;
        }

        internal static int IngredientCount(float roll)
        {
            float clamped = roll < 0f ? 0f : (roll > 0.999f ? 0.999f : roll);
            return IngredientMin + (int)(clamped * (IngredientMax - IngredientMin + 1));
        }

        internal static void PickMeals(int count, float first, float second, int[] picked)
        {
            if (picked == null || picked.Length < MealPick || count <= 0)
            {
                return;
            }

            picked[0] = Index(count, first);
            picked[1] = count < 2 ? -1 : Index(count - 1, second);
            if (picked[1] >= picked[0])
            {
                picked[1]++;
            }
        }

        internal static bool IsCarrier(bool tradeCaravan, float age)
        {
            return tradeCaravan && age >= 0f && age < AdultAge;
        }

        internal static bool FearsMeal(bool origin, float age, string defName)
        {
            return origin && age >= 0f && age < AdultAge && IsMeal(defName);
        }

        internal static void Stock(Verse.Pawn trader, Verse.Pawn carrier)
        {
            if (trader == null || carrier?.inventory?.innerContainer == null || trader == carrier)
            {
                return;
            }

            TraderKindDef kind = trader.trader?.traderKind;
            for (int i = 0; i < Ingredients.Length; i++)
            {
                TryAdd(carrier, kind, Ingredients[i], IngredientCount(Rand.Value));
            }

            int[] picked = { -1, -1 };
            PickMeals(Meals.Length, Rand.Value, Rand.Value, picked);
            for (int i = 0; i < picked.Length; i++)
            {
                if (picked[i] >= 0)
                {
                    TryAdd(carrier, kind, Meals[picked[i]], MealCount);
                }
            }
        }

        internal static void TryDropTail(Verse.Pawn target)
        {
            if (target?.MapHeld == null)
            {
                return;
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(TailDefName);
            if (!CanStock(def != null, def != null && def.IsCorpse, def == null ? 0f : def.BaseMarketValue, true))
            {
                return;
            }

            Thing drop = ThingMaker.MakeThing(def);
            drop.stackCount = 1;
            GenPlace.TryPlaceThing(drop, target.PositionHeld, target.MapHeld, ThingPlaceMode.Near);
        }

        internal static void NoteEaten(Verse.Pawn ingester, string defName)
        {
            if (ingester?.ageTracker == null ||
                !FearsMeal(RHAH_Api.IsOrigin(ingester), ingester.ageTracker.AgeBiologicalYearsFloat, defName))
            {
                return;
            }

            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(ThoughtDefName);
            if (thought == null)
            {
                return;
            }

            ingester.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
        }

        static int Index(int count, float roll)
        {
            float clamped = roll < 0f ? 0f : (roll > 0.999f ? 0.999f : roll);
            return (int)(clamped * count);
        }

        static void TryAdd(Verse.Pawn carrier, TraderKindDef kind, string defName, int count)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            bool willTrade = kind == null || (def != null && kind.WillTrade(def));
            if (def == null || count <= 0 ||
                !CanStock(true, def.IsCorpse, def.BaseMarketValue, willTrade) ||
                def.category != ThingCategory.Item)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = Math.Min(count, def.stackLimit);
            if (!carrier.inventory.innerContainer.TryAdd(thing))
            {
                thing.Destroy();
            }
        }

        internal static void OpenTrade(Verse.Pawn trader)
        {
            if (trader?.mindState == null || trader.Faction == null || trader.Faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            TraderKindDef kind = DefDatabase<TraderKindDef>.GetNamedSilentFail("RHAH_Trader_Famine");
            if (kind == null)
            {
                return;
            }

            trader.mindState.wantsToTradeWithColony = true;
            PawnComponentsUtility.AddAndRemoveDynamicComponents(trader, true);
            if (trader.trader != null)
            {
                trader.trader.traderKind = kind;
            }
        }

        internal static bool ListsGoods(bool tradeCaravan, bool hostile, bool tradeLord)
        {
            return tradeCaravan && !hostile && !tradeLord;
        }
    }

    // 原版交易 Lord 只卖驮夫背包 本模组幼年随行当驮夫
    [HarmonyPatch(typeof(TraderCaravanUtility), nameof(TraderCaravanUtility.GetTraderCaravanRole))]
    internal static class RHAH_CuisineCarrierPatch
    {
        static void Postfix(Verse.Pawn p, ref TraderCaravanRole __result)
        {
            if (p == null || __result == TraderCaravanRole.Trader || __result == TraderCaravanRole.Carrier)
            {
                return;
            }

            Lord lord = p.GetLord();
            if (lord?.LordJob is not LordJob_TradeWithColony)
            {
                return;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(p);
            float age = p.ageTracker == null ? RHAH_RatEggCuisine.AdultAge : p.ageTracker.AgeBiologicalYearsFloat;
            if (snapshot != null && RHAH_RatEggCuisine.IsCarrier(
                RHAH_CaravanStay.IsTradeCaravan(snapshot.SourceIncidentDisplayId, snapshot.Role), age))
            {
                __result = TraderCaravanRole.Carrier;
            }
        }
    }

    // 访客 Lord 不进原版货物表 本模组商队把随行背包列出来
    [HarmonyPatch(typeof(Pawn_TraderTracker), "get_Goods")]
    internal static class RHAH_CuisineGoodsPatch
    {
        static readonly System.Reflection.FieldInfo PawnField =
            AccessTools.Field(typeof(Pawn_TraderTracker), "pawn");

        static void Postfix(Pawn_TraderTracker __instance, ref System.Collections.Generic.IEnumerable<Thing> __result)
        {
            Verse.Pawn trader = PawnField?.GetValue(__instance) as Verse.Pawn;
            if (trader == null)
            {
                return;
            }

            Lord lord = trader.GetLord();
            bool tradeLord = lord?.LordJob is LordJob_TradeWithColony;
            IRHAH_Pawn snapshot = RHAH_Api.Get(trader);
            bool caravan = snapshot != null &&
                RHAH_CaravanStay.IsTradeCaravan(snapshot.SourceIncidentDisplayId, snapshot.Role);
            bool hostile = trader.Faction != null && trader.Faction.HostileTo(Faction.OfPlayer);
            if (!RHAH_RatEggCuisine.ListsGoods(caravan, hostile, tradeLord) || lord?.ownedPawns == null)
            {
                return;
            }

            System.Collections.Generic.List<Thing> goods = new System.Collections.Generic.List<Thing>();
            if (__result != null)
            {
                foreach (Thing thing in __result)
                {
                    if (thing != null)
                    {
                        goods.Add(thing);
                    }
                }
            }

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn member = lord.ownedPawns[i];
                if (member?.inventory?.innerContainer == null)
                {
                    continue;
                }

                for (int j = 0; j < member.inventory.innerContainer.Count; j++)
                {
                    Thing thing = member.inventory.innerContainer[j];
                    if (thing != null && !goods.Contains(thing))
                    {
                        goods.Add(thing);
                    }
                }
            }

            __result = goods;
        }
    }
}
