using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static bool HasFoodInInventory(Pawn pawn)
        {
            if (pawn?.inventory == null)
            {
                return false;
            }

            ThingOwner<Thing> container = pawn.inventory.innerContainer;
            for (int i = 0; i < container.Count; i++)
            {
                Thing thing = container[i];
                if (thing == null || !thing.def.IsNutritionGivingIngestible || !thing.IngestibleNow)
                {
                    continue;
                }

                if (ShouldPrioritizeReliefAreaFood(pawn) && !IsEventFoodSelectable(thing.def))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public static Area_MouseDisasterRelief GetReliefArea(Map map)
        {
            return map?.areaManager?.Get<Area_MouseDisasterRelief>();
        }

        public static bool MapHasReliefArea(Map map)
        {
            return map?.areaManager?.Get<Area_MouseDisasterRelief>() is Area_MouseDisasterRelief area && area.TrueCount > 0;
        }

        public static bool IsReliefAreaPostfixSuppressed => suppressReliefAreaPostfix;

        public static void EnsureReliefAreaExists(Map map)
        {
            if (map?.areaManager == null || GetReliefArea(map) != null)
            {
                return;
            }

            map.areaManager.AllAreas.Add(new Area_MouseDisasterRelief(map.areaManager));
        }

        public static bool ShouldBlockColonistReliefFood(Pawn pawn, Thing food)
        {
            return IsPlayerColonist(pawn) && IsReliefAreaFood(food);
        }

        public static bool ShouldPrioritizeReliefAreaFood(Pawn pawn)
        {
            bool isMouseDisasterFoodConsumer = pawn != null &&
                                               (IsBeggarPawn(pawn) ||
                                                IsThiefPawn(pawn) ||
                                                IsWildMouseDisasterKind(pawn) ||
                                                IsMouseDisasterTraderAdult(pawn) ||
                                                IsMouseDisasterTraderEscort(pawn) ||
                                                IsMouseEggOrChild(pawn));
            return MouseDisasterEventFoodPolicy.AppliesEventFoodRestrictions(
                MouseDisasterReliefAreaPolicy.CanUseReliefFood(
                    isMouseDisasterFoodConsumer,
                    false,
                    IsAbandonedDeliveryPawn(pawn)),
                IsPlayerAffiliatedRatkin(pawn));
        }

        public static bool AllowsEventPawnOutsideReliefFood(Pawn pawn)
        {
            return ShouldPrioritizeReliefAreaFood(pawn) &&
                   MouseDisasterMod.Settings?.allowEventPawnsEatOutsideReliefArea == true;
        }

        public static bool IsEventFoodSelectable(ThingDef foodDef)
        {
            return foodDef != null &&
                   (MouseDisasterMod.Settings == null || MouseDisasterMod.Settings.IsEventFoodEnabled(foodDef.defName));
        }

        private static bool IsPlayerColonist(Pawn pawn)
        {
            return pawn != null && pawn.Faction == Faction.OfPlayer && pawn.IsColonist;
        }

        private static bool IsReliefAreaFood(Thing food)
        {
            if (food?.MapHeld == null || !food.Spawned)
            {
                return false;
            }

            Area_MouseDisasterRelief reliefArea = GetReliefArea(food.MapHeld);
            return reliefArea != null && reliefArea[food.PositionHeld];
        }

        internal static bool IsAreaFoodSourceThing(Thing thing, bool allowHarvest)
        {
            if (thing == null || !thing.Spawned)
            {
                return false;
            }

            if (MouseDisasterReliefAreaPolicy.ShouldAllowMealSourceBuildingInReliefAreaSearch(thing.def?.building?.isMealSource == true))
            {
                return true;
            }

            if (thing is Corpse)
            {
                return true;
            }

            if (thing.def.IsNutritionGivingIngestible && thing.IngestibleNow)
            {
                return true;
            }

            return allowHarvest &&
                   thing is Plant plant &&
                   plant.HarvestableNow &&
                   plant.def?.plant?.harvestedThingDef?.IsNutritionGivingIngestible == true;
        }

        public static int CountFoodInInventory(Pawn pawn)
        {
            if (pawn?.inventory?.innerContainer == null)
            {
                return 0;
            }

            int count = 0;
            ThingOwner<Thing> container = pawn.inventory.innerContainer;
            for (int i = 0; i < container.Count; i++)
            {
                Thing thing = container[i];
                if (thing == null || thing.stackCount <= 0 || !thing.def.IsNutritionGivingIngestible || !thing.IngestibleNow)
                {
                    continue;
                }

                if (ShouldPrioritizeReliefAreaFood(pawn) && !IsEventFoodSelectable(thing.def))
                {
                    continue;
                }

                count += thing.stackCount;
            }

            return count;
        }

        public static bool HasAnyStealableFoodOnMap(Map map)
        {
            return map?.GetComponent<MapComponent_MouseDisasterFoodTargets>()?.HasAnyFood() == true;
        }

        public static Job TryCreateReliefFoodJob(Pawn pawn, bool allowInventorySearch) =>
            TryCreateReliefFoodJob(pawn, allowInventorySearch, reliefOnly: true);

        private static Job TryCreateReliefFoodJob(Pawn pawn, bool allowInventorySearch, bool reliefOnly)
        {
            if (pawn?.needs?.food == null || MouseDisasterFeeding.IsSeekingSuppressed(pawn) || !ShouldPrioritizeReliefAreaFood(pawn))
            {
                return null;
            }

            Job inventoryJob = TryCreateInventoryFoodJob(pawn);
            if (inventoryJob != null)
            {
                return inventoryJob;
            }

            Thing foodSource;
            ThingDef foodDef;
            bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;
            bool found = TryFindBestReliefFoodSourceFor(
                    pawn,
                    pawn,
                    desperate,
                    allowHarvest: true,
                    out foodSource,
                    out foodDef,
                    allowForbidden: false,
                    allowCorpse: true,
                    allowSociallyImproper: true,
                    ignoreReservations: false,
                    calculateWantedStackCount: false,
                    allowVenerated: false,
                    FoodPreferability.Undefined);
            if (!found && !reliefOnly && AllowsEventPawnOutsideReliefFood(pawn))
            {
                found = TryFindBestNonReliefFoodSourceFor(
                    pawn,
                    pawn,
                    desperate,
                    allowHarvest: true,
                    out foodSource,
                    out foodDef,
                    allowForbidden: false,
                    allowCorpse: true,
                    allowSociallyImproper: true,
                    ignoreReservations: false,
                    calculateWantedStackCount: false,
                    allowVenerated: false,
                    FoodPreferability.Undefined);
            }
            if (!found)
            {
                return null;
            }

            if (foodSource is Plant && foodSource.def.plant?.harvestedThingDef?.IsNutritionGivingIngestible == true)
            {
                return JobMaker.MakeJob(JobDefOf.Harvest, foodSource);
            }

            if (MouseDisasterReliefAreaPolicy.ShouldUseMealSourceInteractionFlow(foodSource.def?.building?.isMealSource == true))
            {
                IntVec3 interactionCell = foodSource.InteractionCell;
                if (!interactionCell.IsValid || !interactionCell.Standable(pawn.Map) || !pawn.CanReserveAndReach(foodSource, PathEndMode.InteractionCell, Danger.Some))
                {
                    return null;
                }

                Job mealSourceJob = JobMaker.MakeJob(JobDefOf.Ingest, foodSource);
                mealSourceJob.count = 1;
                return mealSourceJob;
            }

            if (!Toils_Ingest.TryFindChairOrSpot(pawn, foodSource, out _))
            {
                return null;
            }

            float nutrition = FoodUtility.GetNutrition(pawn, foodSource, foodDef);
            if (allowInventorySearch && foodSource.Spawned && pawn.CanReserveAndReach(foodSource, PathEndMode.ClosestTouch, Danger.Some))
            {
                Job takeInventory = JobMaker.MakeJob(JobDefOf.TakeInventory, foodSource);
                takeInventory.count = 1;
                takeInventory.takeInventoryDelay = 60;
                return takeInventory;
            }

            Job ingest = JobMaker.MakeJob(JobDefOf.Ingest, foodSource);
            ingest.count = Mathf.Max(1, FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition));
            return ingest;
        }

        private static Job TryCreateInventoryFoodJob(Pawn pawn)
        {
            Thing food = FoodUtility.BestFoodInInventory(
                pawn,
                pawn,
                FoodPreferability.NeverForNutrition,
                FoodPreferability.MealLavish);
            if (food == null)
            {
                return null;
            }

            ThingDef foodDef = MouseDisasterEventFoodPolicy.ResolveIngestibleDef(food);
            if (!IsEventFoodSelectable(foodDef))
            {
                return null;
            }

            float nutrition = FoodUtility.GetNutrition(pawn, food, foodDef);
            Job ingest = JobMaker.MakeJob(JobDefOf.Ingest, food);
            ingest.count = Mathf.Max(1, FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition));
            return ingest;
        }

        public static bool IsMalnourished(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition) != null;
        }

        public static bool HasAccessibleFood(Pawn pawn)
        {
            return HasAccessibleFood(pawn, out _);
        }

        public static bool HasAccessibleFood(Pawn pawn, out string reason)
        {
            reason = string.Empty;
            if (pawn == null)
            {
                reason = "pawn_null";
                return false;
            }

            if (HasFoodInInventory(pawn))
            {
                reason = "inventory_food";
                return true;
            }

            Job job = TryCreateImproperFoodJob(pawn, false);
            if (job == null)
            {
                reason = "no_food_job";
                return false;
            }

            if (job.def == JobDefOf.TakeFromOtherInventory)
            {
                Pawn owner = job.targetB.Thing as Pawn;
                if (owner != null && pawn.CanReserveAndReach(owner, PathEndMode.Touch, Danger.Some))
                {
                    reason = "reachable_other_inventory";
                    return true;
                }

                reason = "other_inventory_unreachable";
                return false;
            }

            Thing targetThing = job.targetA.Thing;
            if (targetThing != null)
            {
                if (targetThing.Spawned && pawn.CanReserveAndReach(targetThing, PathEndMode.ClosestTouch, Danger.Some))
                {
                    reason = "reachable_map_food";
                    return true;
                }

                reason = targetThing.Spawned ? "map_food_unreachable" : "food_not_spawned";
                return false;
            }

            if (job.targetA.IsValid && job.targetA.Cell.IsValid)
            {
                if (pawn.CanReach(job.targetA.Cell, PathEndMode.OnCell, Danger.Some))
                {
                    reason = "reachable_food_cell";
                    return true;
                }

                reason = "food_cell_unreachable";
                return false;
            }

            reason = "food_job_unknown_target";
            return false;
        }

        public static Job TryCreateImproperFoodJob(Pawn pawn, bool allowInventorySearch)
        {
            if (pawn?.needs?.food == null || MouseDisasterFeeding.IsSeekingSuppressed(pawn))
            {
                return null;
            }

            Job reliefFoodJob = TryCreateReliefFoodJob(pawn, allowInventorySearch, reliefOnly: false);
            if (reliefFoodJob != null)
            {
                return reliefFoodJob;
            }

            if (!AllowsEventPawnOutsideReliefFood(pawn))
            {
                return null;
            }

            bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;
            suppressReliefAreaPostfix = true;
            bool foodFound = false;
            Thing foodSource = null;
            ThingDef foodDef = null;
            try
            {
                foodFound = FoodUtility.TryFindBestFoodSourceFor(
                    pawn,
                    pawn,
                    desperate,
                    out foodSource,
                    out foodDef,
                    canRefillDispenser: true,
                    canUseInventory: true,
                    canUsePackAnimalInventory: false,
                    allowForbidden: false,
                    allowCorpse: true,
                    allowSociallyImproper: true,
                    allowHarvest: false,
                    forceScanWholeMap: true,
                    ignoreReservations: false,
                    calculateWantedStackCount: false,
                    allowVenerated: false);
            }
            finally
            {
                suppressReliefAreaPostfix = false;
            }

            if (!foodFound || !IsEventFoodSelectable(foodDef ?? MouseDisasterEventFoodPolicy.ResolveIngestibleDef(foodSource)))
            {
                return null;
            }

            if (!Toils_Ingest.TryFindChairOrSpot(pawn, foodSource, out _))
            {
                return null;
            }

            float nutrition = FoodUtility.GetNutrition(pawn, foodSource, foodDef);
            Pawn inventoryOwner = (foodSource.ParentHolder as Pawn_InventoryTracker)?.pawn;
            if (inventoryOwner != null && inventoryOwner != pawn)
            {
                Job takeOtherInventory = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, foodSource, inventoryOwner);
                takeOtherInventory.count = Mathf.Max(1, FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition));
                return takeOtherInventory;
            }

            if (allowInventorySearch && foodSource.Spawned && pawn.CanReserveAndReach(foodSource, PathEndMode.ClosestTouch, Danger.Some))
            {
                Job takeInventory = JobMaker.MakeJob(JobDefOf.TakeInventory, foodSource);
                takeInventory.count = 1;
                takeInventory.takeInventoryDelay = 60;
                return takeInventory;
            }

            Job ingest = JobMaker.MakeJob(JobDefOf.Ingest, foodSource);
            ingest.count = Mathf.Max(1, FoodUtility.WillIngestStackCountOf(pawn, foodDef, nutrition));
            return ingest;
        }

        public static bool TryFindStoredFoodCell(Map map, out IntVec3 cell)
        {
            if (TryFindDiningRoomCell(map, out cell))
            {
                return true;
            }

            Thing foodThing = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(thing => thing.Spawned && thing.def.IsNutritionGivingIngestible)
                .OrderBy(thing => thing.Position.DistanceToSquared(map.Center))
                .FirstOrDefault();

            if (foodThing != null)
            {
                cell = foodThing.Position;
                return true;
            }

            cell = map.Center;
            return false;
        }

        public static bool TryFindAbandonedDeliveryFoodCell(Map map, IntVec3 entryCell, out IntVec3 cell)
        {
            bool foundReliefFood = TryFindNearestReliefAreaFoodCellToEvent(map, entryCell, out cell);
            if (foundReliefFood)
            {
                return true;
            }

            bool foundHomeAreaFood = TryFindNearestFoodCellToEvent(map, entryCell, requireHomeArea: true, out cell);
            if (foundHomeAreaFood)
            {
                return true;
            }

            bool foundAnyFoodThing = map?.listerThings != null &&
                                     map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource)
                                         .Any(thing => IsAreaFoodSourceThing(thing, allowHarvest: true));
            if (MouseDisasterAbandonedDeliveryPolicy.ShouldTryAnyFoodFallback(foundHomeAreaFood, foundAnyFoodThing) &&
                TryFindNearestFoodCellToEvent(map, entryCell, requireHomeArea: false, out cell))
            {
                return true;
            }

            if (MouseDisasterAbandonedDeliveryPolicy.ShouldFallbackToStoredFoodCell(foundHomeAreaFood, foundAnyFoodThing))
            {
                return TryFindStoredFoodCell(map, out cell);
            }

            cell = IntVec3.Invalid;
            return false;
        }

        private static bool TryFindNearestReliefAreaFoodCellToEvent(Map map, IntVec3 entryCell, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            Area_MouseDisasterRelief reliefArea = GetReliefArea(map);
            if (map?.listerThings == null || reliefArea == null)
            {
                return false;
            }

            List<Thing> foodThings = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource)
                .Where(thing => thing != null &&
                                thing.Spawned &&
                                reliefArea[thing.PositionHeld] &&
                                IsAreaFoodSourceThing(thing, allowHarvest: true))
                .OrderBy(thing => entryCell.IsValid ? entryCell.DistanceToSquared(thing.PositionHeld) : 0f)
                .ToList();

            for (int i = 0; i < foodThings.Count; i++)
            {
                Thing foodThing = foodThings[i];
                if (TryFindReachableStandCellNear(map, entryCell, foodThing.PositionHeld, requireHomeArea: false, out cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindNearestFoodCellToEvent(Map map, IntVec3 entryCell, bool requireHomeArea, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map?.listerThings == null || (requireHomeArea && map.areaManager?.Home == null))
            {
                return false;
            }

            List<Thing> foodThings = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource)
                .Where(thing => thing != null &&
                                thing.Spawned &&
                                (!requireHomeArea || map.areaManager.Home[thing.Position]) &&
                                IsAreaFoodSourceThing(thing, allowHarvest: true))
                .OrderBy(thing => entryCell.IsValid ? entryCell.DistanceToSquared(thing.Position) : 0f)
                .ToList();

            for (int i = 0; i < foodThings.Count; i++)
            {
                Thing foodThing = foodThings[i];
                if (TryFindReachableStandCellNear(map, entryCell, foodThing.Position, requireHomeArea, out cell))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryFindBestReliefFoodSourceFor(
            Pawn getter,
            Pawn eater,
            bool desperate,
            bool allowHarvest,
            out Thing foodSource,
            out ThingDef foodDef,
            bool allowForbidden,
            bool allowCorpse,
            bool allowSociallyImproper,
            bool ignoreReservations,
            bool calculateWantedStackCount,
            bool allowVenerated,
            FoodPreferability minPrefOverride)
        {
            return TryFindBestAreaFilteredFoodSourceFor(
                getter,
                eater,
                desperate,
                allowHarvest,
                onlyReliefAreaFood: true,
                out foodSource,
                out foodDef,
                allowForbidden,
                allowCorpse,
                allowSociallyImproper,
                ignoreReservations,
                calculateWantedStackCount,
                allowVenerated,
                minPrefOverride);
        }

        public static bool TryFindBestNonReliefFoodSourceFor(
            Pawn getter,
            Pawn eater,
            bool desperate,
            bool allowHarvest,
            out Thing foodSource,
            out ThingDef foodDef,
            bool allowForbidden,
            bool allowCorpse,
            bool allowSociallyImproper,
            bool ignoreReservations,
            bool calculateWantedStackCount,
            bool allowVenerated,
            FoodPreferability minPrefOverride)
        {
            return TryFindBestAreaFilteredFoodSourceFor(
                getter,
                eater,
                desperate,
                allowHarvest,
                onlyReliefAreaFood: false,
                out foodSource,
                out foodDef,
                allowForbidden,
                allowCorpse,
                allowSociallyImproper,
                ignoreReservations,
                calculateWantedStackCount,
                allowVenerated,
                minPrefOverride);
        }

        private static bool TryFindBestAreaFilteredFoodSourceFor(
            Pawn getter,
            Pawn eater,
            bool desperate,
            bool allowHarvest,
            bool onlyReliefAreaFood,
            out Thing foodSource,
            out ThingDef foodDef,
            bool allowForbidden,
            bool allowCorpse,
            bool allowSociallyImproper,
            bool ignoreReservations,
            bool calculateWantedStackCount,
            bool allowVenerated,
            FoodPreferability minPrefOverride)
        {
            foodSource = null;
            foodDef = null;
            if (getter?.Map == null || eater == null)
            {
                return false;
            }

            Area_MouseDisasterRelief reliefArea = GetReliefArea(getter.Map);
            if (onlyReliefAreaFood && (reliefArea == null || reliefArea.TrueCount == 0))
            {
                return false;
            }

            List<Thing> foodThings = getter.Map.listerThings?.ThingsInGroup(ThingRequestGroup.FoodSource);
            if (foodThings == null)
            {
                return false;
            }

            FoodPreferability minPref = ResolveReliefFoodMinPreferability(eater, desperate, minPrefOverride);
            var targetCache = getter == eater && MapComponent_MouseDisasterFoodTargets.Eligible(getter)
                ? getter.Map.GetComponent<MapComponent_MouseDisasterFoodTargets>() : null;
            int cacheKey = MapComponent_MouseDisasterFoodTargets.Key(desperate, false, false, false, allowForbidden,
                allowCorpse, allowSociallyImproper, allowHarvest, true, ignoreReservations, calculateWantedStackCount,
                allowVenerated, minPref, areaSearch: true, reliefOnly: onlyReliefAreaFood);
            if (targetCache != null && targetCache.TryRead(getter, cacheKey, out foodSource, out foodDef, out bool cachedFound)) return cachedFound;
            Thing bestThing = null;
            ThingDef bestFoodDef = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < foodThings.Count; i++)
            {
                Thing candidate = foodThings[i];
                if (candidate == null || !candidate.Spawned)
                {
                    continue;
                }

                bool isReliefFood = reliefArea != null && reliefArea[candidate.PositionHeld];
                if (onlyReliefAreaFood != isReliefFood)
                {
                    continue;
                }

                bool isHarvestablePlant = allowHarvest &&
                                          candidate is Plant plant &&
                                          plant.HarvestableNow &&
                                          plant.def?.plant?.harvestedThingDef?.IsNutritionGivingIngestible == true;
                bool isCorpse = candidate is Corpse;
                if (!MouseDisasterReliefAreaPolicy.ShouldAllowFoodTypeInReliefAreaSearch(
                        candidate.def.IsNutritionGivingIngestible,
                        candidate.IngestibleNow,
                        isHarvestablePlant,
                        isCorpse,
                        allowCorpse))
                {
                    continue;
                }

                ThingDef candidateFoodDef = isHarvestablePlant ? candidate.def.plant.harvestedThingDef : candidate.def;
                if (candidateFoodDef?.ingestible == null || !IsEventFoodSelectable(candidateFoodDef))
                {
                    continue;
                }

                if ((int)candidateFoodDef.ingestible.preferability < (int)minPref)
                {
                    continue;
                }

                if (!allowForbidden && candidate.IsForbidden(getter))
                {
                    continue;
                }

                if (!desperate && candidate.IsNotFresh())
                {
                    continue;
                }

                if (candidate.IsDessicated())
                {
                    continue;
                }

                if (!allowSociallyImproper)
                {
                    bool animalsCare = !getter.IsAnimal;
                    if (!isHarvestablePlant &&
                        !candidate.IsSociallyProper(getter) &&
                        !candidate.IsSociallyProper(eater, eater.IsPrisonerOfColony, animalsCare))
                    {
                        continue;
                    }
                }

                if (isHarvestablePlant)
                {
                    if (!eater.WillEat(candidateFoodDef, getter, careIfNotAcceptableForTitle: true, allowVenerated))
                    {
                        continue;
                    }
                }
                else if (!eater.WillEat(candidate, getter, careIfNotAcceptableForTitle: true, allowVenerated))
                {
                    continue;
                }

                float nutrition = FoodUtility.GetNutrition(eater, candidate, candidateFoodDef);
                int wantedStackCount = calculateWantedStackCount
                    ? Mathf.Max(1, FoodUtility.WillIngestStackCountOf(eater, candidateFoodDef, nutrition))
                    : 1;
                float distance = (getter.Position - candidate.PositionHeld).LengthManhattan;
                bool preferLowestValue = !onlyReliefAreaFood && AllowsEventPawnOutsideReliefFood(getter);
                float score = MouseDisasterEventFoodPolicy.ScoreFood(
                    MouseDisasterMod.Settings?.GetEventFoodWeight(candidateFoodDef.defName) ?? MouseDisasterEventFoodPolicy.DefaultWeight,
                    distance,
                    MouseDisasterEventFoodPolicy.GetMarketValue(candidate, candidateFoodDef),
                    preferLowestValue);
                if (score <= bestScore) continue;
                if (!ignoreReservations && !getter.CanReserve(candidate, 10, wantedStackCount))
                {
                    continue;
                }

                if (!getter.CanReach(candidate, PathEndMode.ClosestTouch, Danger.Some))
                {
                    continue;
                }

                bestThing = candidate;
                bestFoodDef = candidateFoodDef;
                bestScore = score;
            }

            targetCache?.Store(getter, cacheKey, bestThing, bestFoodDef, bestThing != null && bestFoodDef != null);
            if (bestThing == null || bestFoodDef == null)
            {
                return false;
            }

            foodSource = bestThing;
            foodDef = bestFoodDef;
            return true;
        }

        private static FoodPreferability ResolveReliefFoodMinPreferability(Pawn eater, bool desperate, FoodPreferability minPrefOverride)
        {
            if (minPrefOverride != FoodPreferability.Undefined)
            {
                return minPrefOverride;
            }

            bool eaterNonHumanlikeOrWildMan = eater.NonHumanlikeOrWildMan();
            bool eaterDoesNotMindRawFood = eater.genes != null && eater.genes.DontMindRawFood;
            if (MouseDisasterReliefAreaPolicy.ShouldAllowRawFoodAsMinimumPreferability(desperate, eaterNonHumanlikeOrWildMan, eaterDoesNotMindRawFood))
            {
                return FoodPreferability.NeverForNutrition;
            }

            return FoodPreferability.RawBad;
        }

        private static bool TryFindReachableStandCellNear(Map map, IntVec3 startCell, IntVec3 targetCell, bool requireHomeArea, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map == null || !targetCell.IsValid || !targetCell.InBounds(map) || (requireHomeArea && map.areaManager?.Home == null))
            {
                return false;
            }

            TraverseParms traverseParms = TraverseParms.For(TraverseMode.PassDoors);
            if (targetCell.Standable(map) &&
                (!requireHomeArea || map.areaManager.Home[targetCell]) &&
                (!startCell.IsValid || map.reachability.CanReach(startCell, targetCell, PathEndMode.OnCell, traverseParms)))
            {
                cell = targetCell;
                return true;
            }

            IEnumerable<IntVec3> candidates = CellRect.CenteredOn(targetCell, 3)
                .Cells
                .Where(candidate => candidate.InBounds(map) &&
                                    candidate.Standable(map) &&
                                    (!requireHomeArea || map.areaManager.Home[candidate]))
                .OrderBy(candidate => candidate.DistanceToSquared(targetCell));
            foreach (IntVec3 candidate in candidates)
            {
                if (!startCell.IsValid || map.reachability.CanReach(startCell, candidate, PathEndMode.OnCell, traverseParms))
                {
                    cell = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryFindDiningRoomCell(Map map, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (map?.regionGrid?.AllRooms == null)
            {
                return false;
            }

            Room diningRoom = map.regionGrid.AllRooms
                .Where(room => room != null &&
                               !room.PsychologicallyOutdoors &&
                               room.Role != null &&
                               room.Role.workerClass != null &&
                               room.Role.workerClass.Name.IndexOf("DiningRoom", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(room => room.CellCount)
                .FirstOrDefault();
            if (diningRoom == null)
            {
                return false;
            }

            IEnumerable<IntVec3> candidates = diningRoom.Cells.Where(c => c.Standable(map));
            if (!candidates.Any())
            {
                return false;
            }

            cell = candidates.RandomElement();
            return true;
        }
    }
}
