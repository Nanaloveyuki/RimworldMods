using System;
using System.Collections.Generic;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_NutritionPayment
    {
        internal static bool IsFood(RHAH_RequestKind kind) =>
            kind == RHAH_RequestKind.SimpleMeal || kind == RHAH_RequestKind.FineMeal;

        internal static double Nutrition(Thing food)
        {
            double value = food?.def?.ingestible == null ? 0 : food.GetStatValue(StatDefOf.Nutrition);
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0;
        }

        internal static double Required(RHAH_ChoiceRecord record)
        {
            if (record.RequiredFoodNutrition > 0) return record.RequiredFoodNutrition;
            if (record.TransactionVersion == RHAH_RequestRules.TransactionVersion)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(record.QuotedFoodDefName);
                return record.QuotedFoodCount * RHAH_DemandFood.EdibleNutrition(def);
            }
            return record.Amount * RHAH_DemandFood.MealNutrition;
        }

        internal static bool CanPay(Map map, double required, List<Verse.Pawn> visitors)
        {
            return Plan(map, required, visitors, out _, out _);
        }

        static bool Plan(Map map, double required, List<Verse.Pawn> visitors, out List<Thing> food, out List<int> counts)
        {
            food = new List<Thing>();
            counts = new List<int>();
            if (map == null || visitors.Count == 0 || required <= 0 || double.IsNaN(required) || double.IsInfinity(required)) return false;
            List<Thing> available = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            IReadOnlyList<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < available.Count; i++)
            {
                Thing thing = available[i];
                if (!RHAH_FoodTransaction.IsUsableFood(thing, visitors)) continue;
                bool reachable = false;
                for (int j = 0; !reachable && j < visitors.Count; j++)
                    reachable = !visitors[j].Downed && visitors[j].inventory != null &&
                        visitors[j].inventory.innerContainer.CanAcceptAnyOf(thing, false) &&
                        RHAH_FoodTransaction.CanDeliverFood(thing, visitors[j], colonists);
                if (!reachable) continue;
                double nutrition = Nutrition(thing);
                if (nutrition <= 0) continue;
                int count = Math.Min(thing.stackCount, RHAH_DemandFood.Units(required, nutrition));
                food.Add(thing);
                counts.Add(count);
                required -= count * nutrition;
                if (required <= RHAH_DemandFood.Tolerance) return true;
            }
            return false;
        }

        internal static bool TryPay(Map map, double required, List<Verse.Pawn> visitors)
        {
            if (!Plan(map, required, visitors, out List<Thing> food, out List<int> counts)) return false;
            List<Thing> payment = new List<Thing>();
            List<IntVec3> origins = new List<IntVec3>();
            IReadOnlyList<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            try
            {
                for (int i = 0; i < food.Count; i++)
                {
                    Verse.Pawn receiver = null;
                    for (int j = 0; j < visitors.Count && receiver == null; j++)
                    {
                        Verse.Pawn candidate = visitors[j];
                        if (!candidate.Downed && candidate.inventory != null &&
                            candidate.inventory.innerContainer.CanAcceptAnyOf(food[i], false) &&
                            RHAH_FoodTransaction.CanDeliverFood(food[i], candidate, colonists)) receiver = candidate;
                    }
                    if (receiver == null) throw new InvalidOperationException("Food receiver is no longer available");
                    origins.Add(food[i].Position);
                    Thing split = food[i].SplitOff(counts[i]);
                    payment.Add(split);
                    if (split.Spawned) split.DeSpawn();
                    if (!receiver.inventory.innerContainer.TryAdd(split, false))
                        throw new InvalidOperationException("Visitor inventory refused food payment");
                    required -= split.stackCount * Nutrition(split);
                }
                if (required > RHAH_DemandFood.Tolerance) throw new InvalidOperationException("Food stock changed during payment");
            }
            catch (Exception exception)
            {
                for (int i = 0; i < payment.Count; i++)
                {
                    payment[i].holdingOwner?.Remove(payment[i]);
                    if (food[i] != payment[i] && food[i].Spawned && food[i].TryAbsorbStack(payment[i], false)) continue;
                    if (!payment[i].Spawned && !GenPlace.TryPlaceThing(payment[i], origins[i], map, ThingPlaceMode.Near))
                        Log.Error("[RHAH] Could not restore food payment at " + origins[i]);
                }
                Log.Warning("[RHAH] Nutrition payment rolled back: " + exception.Message);
                return false;
            }
            return true;
        }
    }
}
