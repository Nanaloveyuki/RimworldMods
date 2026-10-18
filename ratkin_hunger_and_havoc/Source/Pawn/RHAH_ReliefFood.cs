using System;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn.Compat;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    internal enum RHAH_FoodReject
    {
        None = 0,
        Missing,
        NotIngestible,
        WillNotEat,
        Zone,
        Forbidden,
        Unreachable,
        Reserved,
        NoStand,
        EmptyDispenser,
        DisabledFood,
        TooYoung
    }

    internal static class RHAH_ReliefFood
    {
        internal const float SatisfiedLevel = 0.82f;
        internal const float RefeedMalnutrition = 0.4f;
        internal const int RetryBaseTicks = GenDate.TicksPerHour;
        internal const float SearchRadius = 40f;

        internal static bool ReliefRulesApply
        {
            get
            {
                RHAH_Settings settings = RHAH_Mod.Settings;
                return settings == null || settings.reliefEnabled;
            }
        }

        internal static bool MayEatOutside(Verse.Pawn pawn)
        {
            if (!ReliefRulesApply)
            {
                return true;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings != null && !settings.allowEatOutsideRelief)
            {
                return false;
            }

            return RHAH_Api.Allows(pawn, RHAH_BehaviorGate.EatOutsideRelief);
        }

        internal static bool FoodAllowed(ThingDef def)
        {
            if (!ReliefRulesApply || def == null)
            {
                return true;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            return settings == null || settings.IsReliefFoodEnabled(def.defName);
        }

        internal static RHAH_FoodReject Reject(Verse.Pawn pawn, Thing food, bool insideZone)
        {
            return Reject(pawn, food, insideZone, false, true);
        }

        internal static RHAH_FoodReject Reject(
            Verse.Pawn pawn,
            Thing food,
            bool insideZone,
            bool ignoreZone)
        {
            return Reject(pawn, food, insideZone, ignoreZone, true);
        }

        internal static RHAH_FoodReject Reject(
            Verse.Pawn pawn,
            Thing food,
            bool insideZone,
            bool ignoreZone,
            bool checkPath)
        {
            if (pawn == null || food == null || food.Destroyed)
            {
                return RHAH_FoodReject.Missing;
            }

            bool ownInventory = pawn.inventory != null && food.ParentHolder == pawn.inventory;
            if (!ownInventory && food.MapHeld != pawn.Map)
            {
                return RHAH_FoodReject.Missing;
            }

            if (!RHAH_ChildMovement.CanFeedSelf(pawn))
            {
                return RHAH_FoodReject.TooYoung;
            }

            if (!food.IngestibleNow && !IsHarvestable(food) && !IsDispenser(food))
            {
                return RHAH_FoodReject.NotIngestible;
            }

            ThingDef eaten = EatenDef(food);
            if (eaten == null || pawn.RaceProps == null || !pawn.RaceProps.CanEverEat(eaten))
            {
                return RHAH_FoodReject.WillNotEat;
            }

            if (!ignoreZone && !FoodAllowed(eaten))
            {
                return RHAH_FoodReject.DisabledFood;
            }

            Need_Food need = pawn.needs != null ? pawn.needs.food : null;
            bool starving = need != null && need.CurCategory == HungerCategory.Starving;
            if (!pawn.WillEat(eaten, pawn, true, false) && !starving)
            {
                return RHAH_FoodReject.WillNotEat;
            }

            if (ownInventory)
            {
                return RHAH_FoodReject.None;
            }

            if (!ignoreZone && ReliefRulesApply && !InRequestedZone(pawn.Map, food, insideZone))
            {
                return RHAH_FoodReject.Zone;
            }

            if (food.IsForbidden(pawn))
            {
                return RHAH_FoodReject.Forbidden;
            }

            if (!checkPath)
            {
                return RHAH_FoodReject.None;
            }

            if (!pawn.CanReserve(food))
            {
                return RHAH_FoodReject.Reserved;
            }

            if (IsDispenser(food))
            {
                return RejectDispenser(pawn, food);
            }

            if (IsHarvestable(food))
            {
                return pawn.CanReach(food, PathEndMode.Touch, Danger.Deadly)
                    ? RHAH_FoodReject.None
                    : RHAH_FoodReject.Unreachable;
            }

            if (!pawn.CanReach(food, PathEndMode.ClosestTouch, Danger.Deadly))
            {
                return RHAH_FoodReject.Unreachable;
            }

            if (RHAH_ChildMovement.EatsOnFloor(pawn))
            {
                return RHAH_FoodReject.None;
            }

            IntVec3 spot;
            if (!Toils_Ingest.TryFindChairOrSpot(pawn, food, out spot) ||
                !spot.IsValid ||
                !spot.InBounds(pawn.Map) ||
                !spot.Standable(pawn.Map) ||
                !pawn.CanReserveSittableOrSpot(spot, false))
            {
                return RHAH_FoodReject.NoStand;
            }

            return RHAH_FoodReject.None;
        }

        internal static Job MakeJob(Verse.Pawn pawn, Thing food)
        {
            if (pawn == null || food == null || JobDefOf.Ingest == null)
            {
                return null;
            }

            if (IsHarvestable(food) && JobDefOf.Harvest != null)
            {
                return JobMaker.MakeJob(JobDefOf.Harvest, food);
            }

            Pawn_InventoryTracker holder = food.ParentHolder as Pawn_InventoryTracker;
            Verse.Pawn carrier = holder != null ? holder.pawn : null;
            if (carrier != null && carrier != pawn && JobDefOf.TakeFromOtherInventory != null)
            {
                Job take = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, food, carrier);
                take.count = StackCount(pawn, food);
                return take;
            }

            Job ingest = JobMaker.MakeJob(JobDefOf.Ingest, food);
            ingest.count = IsDispenser(food) ? 1 : StackCount(pawn, food);
            return ingest;
        }

        internal static int StackCount(Verse.Pawn pawn, Thing food)
        {
            ThingDef eaten = EatenDef(food);
            if (pawn == null || food == null || eaten == null)
            {
                return 1;
            }

            float nutrition = FoodUtility.NutritionForEater(pawn, food);
            int count = FoodUtility.WillIngestStackCountOf(pawn, eaten, nutrition);
            return count < 1 ? 1 : count;
        }

        internal static ThingDef EatenDef(Thing food)
        {
            if (food == null || food.def == null)
            {
                return null;
            }

            if (IsDispenser(food))
            {
                Building_NutrientPasteDispenser dispenser = food as Building_NutrientPasteDispenser;
                return dispenser != null ? dispenser.DispensableDef : ThingDefOf.MealNutrientPaste;
            }

            if (IsHarvestable(food))
            {
                return food.def.plant != null ? food.def.plant.harvestedThingDef : null;
            }

            return food.def;
        }

        internal static bool IsDispenser(Thing food)
        {
            return food is Building_NutrientPasteDispenser ||
                   (food != null && food.def != null && food.def.building != null && food.def.building.isMealSource);
        }

        internal static bool IsHarvestable(Thing food)
        {
            Plant plant = food as Plant;
            return plant != null &&
                   plant.HarvestableNow &&
                   plant.def != null &&
                   plant.def.plant != null &&
                   plant.def.plant.harvestedThingDef != null &&
                   plant.def.plant.harvestedThingDef.IsNutritionGivingIngestible;
        }

        static RHAH_FoodReject RejectDispenser(Verse.Pawn pawn, Thing food)
        {
            // 无尽膏机不耗料斗 没通电时原版 CanDispenseNow 仍失败
            CompPowerTrader power = food.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                return RHAH_FoodReject.EmptyDispenser;
            }

            Building_NutrientPasteDispenser dispenser = food as Building_NutrientPasteDispenser;
            if (dispenser != null && !dispenser.HasEnoughFeedstockInHoppers())
            {
                return RHAH_FoodReject.EmptyDispenser;
            }

            IntVec3 cell = food.InteractionCell;
            if (!cell.IsValid || !cell.InBounds(pawn.Map) || !cell.Standable(pawn.Map) ||
                !pawn.CanReserveSittableOrSpot(cell, false) ||
                !pawn.CanReach(food, PathEndMode.InteractionCell, Danger.Deadly))
            {
                return RHAH_FoodReject.NoStand;
            }

            return RHAH_FoodReject.None;
        }

        static bool InRequestedZone(Map map, Thing food, bool insideZone)
        {
            if (food.ParentHolder is Pawn_InventoryTracker)
            {
                return false;
            }

            bool inside = RHAH_ReliefArea.Contains(map, food.PositionHeld);
            return insideZone ? inside : !inside;
        }

        internal static List<ThingDef> CandidateFoods()
        {
            List<ThingDef> result = new List<ThingDef>();
            AppendCandidateFoods(result);
            return result;
        }

        internal static void AppendCandidateFoods(List<ThingDef> result)
        {
            if (result == null || DefDatabase<ThingDef>.AllDefsListForReading == null)
            {
                return;
            }

            HashSet<ThingDef> seen = new HashSet<ThingDef>(result);
            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ThingDef def = defs[i];
                if (def != null && def.IsNutritionGivingIngestible && seen.Add(def))
                {
                    result.Add(def);
                }
            }

            if (ThingDefOf.MealNutrientPaste != null && seen.Add(ThingDefOf.MealNutrientPaste))
            {
                result.Add(ThingDefOf.MealNutrientPaste);
            }
        }

        internal static bool GiveFoodAllowed(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            return settings == null || settings.IsGiveFoodEnabled(def.defName);
        }

        internal static bool BegFoodAllowed(ThingDef def)
        {
            if (!BegFoodCandidate(def))
            {
                return false;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            return settings == null || settings.IsBegFoodEnabled(def.defName);
        }

        internal static bool BegFoodCandidate(ThingDef def)
        {
            if (def == null || def.ingestible == null)
            {
                return false;
            }

            return RHAH_VisitorRules.BegFoodEligible(
                def.IsIngestible,
                def.IsNutritionGivingIngestible,
                (int)def.ingestible.preferability,
                (int)FoodPreferability.MealAwful);
        }

        internal static void AppendBegFoods(List<ThingDef> result)
        {
            if (result == null)
            {
                return;
            }

            List<ThingDef> foods = new List<ThingDef>();
            AppendCandidateFoods(foods);
            for (int i = 0; i < foods.Count; i++)
            {
                if (BegFoodCandidate(foods[i]))
                {
                    result.Add(foods[i]);
                }
            }
        }

        internal static string GroupKey(int mode, ThingDef food)
        {
            int safe = mode < 0 || mode > 2 ? 0 : mode;
            if (safe == 1)
            {
                string label = food == null ? null : food.label;
                if (string.IsNullOrEmpty(label))
                {
                    label = food == null ? null : food.defName;
                }

                return string.IsNullOrEmpty(label) ? "#" : label.Substring(0, 1).ToUpperInvariant();
            }

            if (safe == 2)
            {
                return CategoryKey(food);
            }

            string name = SourceModName(food);
            return name ?? string.Empty;
        }

        internal static string GroupTitle(int mode, string key)
        {
            int safe = mode < 0 || mode > 2 ? 0 : mode;
            if (safe == 2)
            {
                return CategoryTitle(key);
            }

            if (safe == 0 && string.IsNullOrEmpty(key))
            {
                return null;
            }

            return key;
        }

        internal static List<List<ThingDef>> GroupFoods(int mode, List<ThingDef> foods)
        {
            List<List<ThingDef>> groups = new List<List<ThingDef>>();
            if (foods == null)
            {
                return groups;
            }

            List<string> keys = new List<string>();
            for (int i = 0; i < foods.Count; i++)
            {
                string key = GroupKey(mode, foods[i]);
                int index = IndexOf(keys, key);
                if (index < 0)
                {
                    keys.Add(key);
                    groups.Add(new List<ThingDef>());
                    index = groups.Count - 1;
                }

                groups[index].Add(foods[i]);
            }

            if (mode == 1 || mode == 2)
            {
                groups.Sort((left, right) => string.Compare(
                    GroupKey(mode, left.Count == 0 ? null : left[0]),
                    GroupKey(mode, right.Count == 0 ? null : right[0]),
                    StringComparison.CurrentCultureIgnoreCase));
                for (int i = 0; i < groups.Count; i++)
                {
                    groups[i].Sort((left, right) => string.Compare(
                        left == null ? null : left.label,
                        right == null ? null : right.label,
                        StringComparison.CurrentCultureIgnoreCase));
                }
            }

            return groups;
        }

        static string CategoryKey(ThingDef food)
        {
            if (food != null && food.thingCategories != null)
            {
                for (int i = 0; i < food.thingCategories.Count; i++)
                {
                    ThingCategoryDef category = food.thingCategories[i];
                    while (category != null)
                    {
                        if (category.parent != null && category.parent.defName == "Foods")
                        {
                            return category.defName;
                        }

                        category = category.parent;
                    }
                }

                for (int i = 0; i < food.thingCategories.Count; i++)
                {
                    ThingCategoryDef category = food.thingCategories[i];
                    if (category != null && !string.IsNullOrEmpty(category.defName) && category.defName != "Foods")
                    {
                        return category.defName;
                    }
                }
            }

            return "other";
        }

        static string CategoryTitle(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "other")
            {
                return null;
            }

            if (DefDatabase<ThingCategoryDef>.AllDefsListForReading == null)
            {
                return key;
            }

            ThingCategoryDef category = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(key);
            return category == null || string.IsNullOrEmpty(category.LabelCap) ? key : category.LabelCap;
        }

        internal static string SourceModName(ThingDef food)
        {
            string name = food == null || food.modContentPack == null
                ? null
                : food.modContentPack.Name;
            return string.IsNullOrEmpty(name) ? null : name;
        }

        internal static List<List<ThingDef>> GroupBySourceMod(List<ThingDef> foods)
        {
            List<List<ThingDef>> groups = new List<List<ThingDef>>();
            if (foods == null)
            {
                return groups;
            }

            List<string> names = new List<string>();
            for (int i = 0; i < foods.Count; i++)
            {
                string name = SourceModName(foods[i]);
                int index = IndexOf(names, name);
                if (index < 0)
                {
                    names.Add(name);
                    groups.Add(new List<ThingDef>());
                    index = groups.Count - 1;
                }

                groups[index].Add(foods[i]);
            }

            return groups;
        }

        internal static bool MatchesQuery(ThingDef food, string query)
        {
            if (food == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            string[] terms = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < terms.Length; i++)
            {
                if (!ContainsTerm(food, terms[i]))
                {
                    return false;
                }
            }

            return true;
        }

        static bool ContainsTerm(ThingDef food, string term)
        {
            return Contains(food.LabelCap, term)
                || Contains(food.label, term)
                || Contains(food.defName, term)
                || Contains(SourceModName(food), term);
        }

        static bool Contains(string value, string term)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        static int IndexOf(List<string> names, string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
