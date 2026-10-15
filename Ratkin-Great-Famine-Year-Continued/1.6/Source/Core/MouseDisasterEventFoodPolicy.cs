using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public static class MouseDisasterEventFoodPolicy
    {
        public const float DefaultWeight = 0.1f;
        public const float MinWeight = 0f;
        public const float MaxWeight = 1f;
        private const float WeightScoreScale = 10000f;
        private const float MarketValueScoreScale = 100f;

        private static List<ThingDef> cachedFoodDefs;
        private static int cachedDefCount = -1;

        public static bool AllowsOutsideReliefSearch(bool allowOutsideRelief, bool hasReliefFood)
        {
            return allowOutsideRelief && !hasReliefFood;
        }

        public static bool AppliesEventFoodRestrictions(bool shouldPrioritizeReliefArea, bool playerAffiliated)
        {
            return shouldPrioritizeReliefArea && !playerAffiliated;
        }

        public static bool IsFoodAllowed(string defName, ICollection<string> disabledDefNames)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return false;
            }

            if (disabledDefNames == null || disabledDefNames.Count == 0)
            {
                return true;
            }

            foreach (string disabled in disabledDefNames)
            {
                if (string.Equals(disabled, defName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public static float ResolveWeight(string defName, IDictionary<string, float> weights)
        {
            if (string.IsNullOrWhiteSpace(defName) || weights == null ||
                !TryGetWeight(weights, defName, out float value))
            {
                return DefaultWeight;
            }

            return NormalizeWeight(value);
        }

        public static float ScoreFood(float weight, float distance, float marketValue, bool preferLowestValue)
        {
            float normalizedWeight = NormalizeWeight(weight);
            float normalizedDistance = float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f
                ? 0f
                : distance;
            float score = normalizedWeight * WeightScoreScale - normalizedDistance;
            if (preferLowestValue)
            {
                float value = float.IsNaN(marketValue) || float.IsInfinity(marketValue) || marketValue < 0f
                    ? 0f
                    : marketValue;
                score -= value * MarketValueScoreScale;
            }

            return score;
        }

        public static float NormalizeWeight(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return DefaultWeight;
            }

            if (value < MinWeight)
            {
                return MinWeight;
            }

            if (value > MaxWeight)
            {
                return MaxWeight;
            }

            return value;
        }

        public static bool IsListedEventFood(ThingDef def)
        {
            if (def?.ingestible == null || !def.IsNutritionGivingIngestible)
            {
                return false;
            }

            if (def.category != ThingCategory.Item)
            {
                return false;
            }

            if (def.thingClass != null && typeof(Corpse).IsAssignableFrom(def.thingClass))
            {
                return false;
            }

            return def.ingestible.preferability != FoodPreferability.NeverForNutrition;
        }

        public static IReadOnlyList<ThingDef> AllFoodDefs()
        {
            List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            if (allDefs == null)
            {
                return Array.Empty<ThingDef>();
            }

            if (cachedFoodDefs != null && cachedDefCount == allDefs.Count)
            {
                return cachedFoodDefs;
            }

            var foods = new List<ThingDef>();
            for (int i = 0; i < allDefs.Count; i++)
            {
                ThingDef def = allDefs[i];
                if (IsListedEventFood(def))
                {
                    foods.Add(def);
                }
            }

            foods.Sort(CompareFoodDefs);
            cachedFoodDefs = foods;
            cachedDefCount = allDefs.Count;
            return foods;
        }

        public static ThingDef ResolveIngestibleDef(Thing food)
        {
            if (food == null)
            {
                return null;
            }

            if (food is Building_NutrientPasteDispenser)
            {
                return FoodUtility.GetFinalIngestibleDef(food, false);
            }

            if (food is Plant plant && plant.def?.plant?.harvestedThingDef != null)
            {
                return plant.def.plant.harvestedThingDef;
            }

            return food.def;
        }

        public static float GetMarketValue(Thing thing, ThingDef foodDef)
        {
            if (thing != null && thing.def == foodDef)
            {
                float spawnedValue = thing.GetStatValue(StatDefOf.MarketValue);
                if (!float.IsNaN(spawnedValue) && !float.IsInfinity(spawnedValue) && spawnedValue >= 0f)
                {
                    return spawnedValue;
                }
            }

            if (foodDef != null)
            {
                float abstractValue = foodDef.GetStatValueAbstract(StatDefOf.MarketValue);
                if (!float.IsNaN(abstractValue) && !float.IsInfinity(abstractValue) && abstractValue >= 0f)
                {
                    return abstractValue;
                }

                if (foodDef.BaseMarketValue >= 0f)
                {
                    return foodDef.BaseMarketValue;
                }
            }

            return 0f;
        }

        private static bool TryGetWeight(IDictionary<string, float> weights, string defName, out float value)
        {
            if (weights.TryGetValue(defName, out value))
            {
                return true;
            }

            foreach (KeyValuePair<string, float> entry in weights)
            {
                if (string.Equals(entry.Key, defName, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            value = DefaultWeight;
            return false;
        }

        private static int CompareFoodDefs(ThingDef left, ThingDef right)
        {
            string leftLabel = left?.label ?? left?.defName ?? string.Empty;
            string rightLabel = right?.label ?? right?.defName ?? string.Empty;
            int compared = string.Compare(leftLabel, rightLabel, StringComparison.CurrentCultureIgnoreCase);
            if (compared != 0)
            {
                return compared;
            }

            return string.Compare(left?.defName, right?.defName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
