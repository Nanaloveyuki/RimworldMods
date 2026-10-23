using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_DemandFood
    {
        internal const double MealNutrition = 0.9;
        internal const double Tolerance = 0.000001;

        internal static bool Candidate(ThingDef def)
        {
            return def?.ingestible != null && def.category == ThingCategory.Item &&
                def.IsIngestible && def.ingestible.HumanEdible && !def.IsDrug && !def.IsCorpse &&
                def.ingestible.preferability >= FoodPreferability.MealAwful &&
                ValidNutrition(def.ingestible.CachedNutrition);
        }

        internal static bool DefaultAllowed(ThingDef def)
        {
            ModContentPack source = def?.modContentPack;
            return source != null && (source.IsCoreMod || source.IsOfficialMod ||
                string.Equals(source.PackageId, "Solaris.RatkinRaceMod", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool Allowed(ThingDef def)
        {
            return Candidate(def) && (RHAH_Mod.Settings == null
                ? DefaultAllowed(def) : RHAH_Mod.Settings.IsDemandFoodEnabled(def));
        }

        internal static void AppendCandidates(List<ThingDef> result)
        {
            if (result == null) return;
            List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
                if (Candidate(defs[i]) && !result.Contains(defs[i])) result.Add(defs[i]);
        }

        internal static double Nutrition(Thing food)
        {
            if (!Allowed(food?.def)) return 0;
            double value = food.GetStatValue(StatDefOf.Nutrition);
            return ValidNutrition(value) ? value : 0;
        }

        static bool ValidNutrition(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static double Remaining(RHAH_ChoiceRecord record)
        {
            double left = (record.Amount - record.ArmedFoodDelivered) * MealNutrition - record.ArmedFoodNutritionDelivered;
            return left <= Tolerance ? 0 : left;
        }

        internal static int Units(double remaining, double nutrition)
        {
            if (remaining <= Tolerance || !ValidNutrition(nutrition)) return 0;
            return (int)Math.Min(int.MaxValue, Math.Ceiling((remaining - Tolerance) / nutrition));
        }

        internal static double BeingHauledTo(Verse.Pawn receiver)
        {
            if (receiver?.Map?.mapPawns == null) return 0;
            RHAH_ChoiceRecord request = RHAH_ArmedDemand.FoodRequest(receiver);
            if (request == null) return 0;
            double nutrition = 0;
            IReadOnlyList<Verse.Pawn> pawns = receiver.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn hauler = pawns[i];
                Job job = hauler.CurJob;
                if (!(hauler.jobs?.curDriver is JobDriver_GiveToPawn) || job == null ||
                    RHAH_ArmedDemand.FoodRequest(job.targetB.Pawn) != request) continue;
                Thing carried = hauler.carryTracker?.CarriedThing;
                nutrition += Nutrition(carried) * (carried?.stackCount ?? 0);
            }
            return nutrition;
        }
    }
}
