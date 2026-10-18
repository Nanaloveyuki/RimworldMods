using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    internal static class MouseDisasterPhase3CaravanUtility
    {
        public static List<ThingCount> GenerateCaravanDemands(Caravan caravan, float targetValue)
        {
            List<Thing> candidates = caravan.AllThings
                .Where(thing =>
                    thing != null &&
                    !(thing is Pawn) &&
                    thing.stackCount > 0 &&
                    thing.def.category == ThingCategory.Item &&
                    (thing.def.IsNutritionGivingIngestible || thing.def.IsMedicine || thing.def == ThingDefOf.Silver))
                .OrderByDescending(thing => thing.MarketValue)
                .ToList();

            List<ThingCount> demands = new List<ThingCount>();
            float remainingValue = Mathf.Max(120f, targetValue);
            for (int i = 0; i < candidates.Count && remainingValue > 30f; i++)
            {
                Thing thing = candidates[i];
                int count = Mathf.Clamp(Mathf.CeilToInt(remainingValue / Mathf.Max(1f, thing.MarketValue)), 1, thing.stackCount);
                demands.Add(new ThingCount(thing, count));
                remainingValue -= thing.MarketValue * count;
            }

            return demands;
        }

        public static void TakeDemandFromCaravan(Caravan caravan, List<ThingCount> demands)
        {
            if (caravan == null || demands.NullOrEmpty())
            {
                return;
            }

            for (int i = 0; i < demands.Count; i++)
            {
                ThingCount demand = demands[i];
                demand.Thing?.SplitOff(demand.Count).Destroy();
            }
        }

        public static List<Pawn> CreateCaravanMuggerParty(Faction faction, float points, bool infect)
        {
            int total = MouseDisasterUtility.CalculateEscalatingGroupCount(points, 3, 10, 85f);
            int children = Find.Storyteller.difficulty.ChildrenAllowed ? Mathf.Clamp(Mathf.RoundToInt(total * 0.35f), 0, total - 1) : 0;
            int adults = Mathf.Max(1, total - children);
            List<Pawn> pawns = new List<Pawn>();
            for (int i = 0; i < adults; i++)
            {
                Pawn pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult, faction, DevelopmentalStage.Adult, 0.28f);
                if (pawn == null)
                {
                    continue;
                }

                if (infect)
                {
                    MouseDisasterPlagueUtility.InfectWithPlague(pawn);
                }

                pawns.Add(pawn);
            }

            for (int i = 0; i < children; i++)
            {
                Pawn pawn = MouseDisasterPhase3Utility.CreateRatEggPawn(faction, babyStage: false, thiefLike: true, pureNegative: false, infect: infect, foodLevel: 0.16f);
                if (pawn != null)
                {
                    pawns.Add(pawn);
                }
            }

            return pawns;
        }
    }
}
