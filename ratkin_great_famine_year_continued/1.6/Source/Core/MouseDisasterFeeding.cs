using HarmonyLib;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    internal static class MouseDisasterFeeding
    {
        internal const float SatisfiedFoodLevel = 0.82f;

        public static bool HasSatisfied(Pawn pawn)
        {
            if (pawn == null) return false;
            var state = GameComponent_MouseDisasterEventBehavior.Component;
            if (state?.HasCompletedFeeding(pawn) == true) return true;
            // Migrate the earlier hidden mark before removing it from the health system.
            var legacy = pawn.health?.hediffSet?.GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_FedOnce);
            if (legacy == null) return false;
            if (state != null)
            {
                state.CompleteFeeding(pawn);
                state.RecordRefeeding(pawn);
                pawn.health.RemoveHediff(legacy);
            }
            return true;
        }

        public static bool HasTemporarySatiety(Pawn pawn) =>
            pawn?.health?.hediffSet?.HasHediff(MouseDisasterDefOf.MouseDisaster_GuanyinTuSatiety) == true;

        public static bool ShouldLeaveAfterFed(Pawn pawn) => MouseDisasterMod.Settings?.leaveAfterFed != false && HasSatisfied(pawn) &&
            GameComponent_MouseDisasterEventBehavior.Component?.FedDepartureDue(pawn) == true;
        public static bool IsSeekingSuppressed(Pawn pawn) =>
            HasSatisfied(pawn) || HasTemporarySatiety(pawn) ||
            MouseDisasterUtility.IsAbandonedDeliveryPawn(pawn);

        internal static bool IsFull(float level) =>
            !float.IsNaN(level) && !float.IsInfinity(level) && level >= SatisfiedFoodLevel;

        internal static bool ShouldRefeed(float level, float malnutrition) => IsFull(level) && malnutrition >= 0.4f;

        public static void Evaluate(Pawn pawn, float foodLevel)
        {
            if (pawn?.Spawned != true || pawn.Dead || pawn.health == null ||
                MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn)) return;
            var state = GameComponent_MouseDisasterEventBehavior.Component;
            if (!MouseDisasterUtility.IsThiefPawn(pawn) && !MouseDisasterUtility.IsBeggarPawn(pawn) &&
                state?.HasFoodSeekingProfile(pawn) != true) return;
            bool completed = HasSatisfied(pawn);
            // A temporary health effect must not become a permanent feeding completion.
            if (state == null || !IsFull(foodLevel)) return;

            if (!completed && !HasTemporarySatiety(pawn))
            {
                state.CompleteFeeding(pawn);
                if (MouseDisasterMod.Settings?.leaveAfterFed != false &&
                    (pawn.MentalStateDef == MouseDisasterDefOf.MouseDisaster_BeggingState ||
                    pawn.MentalStateDef == MouseDisasterDefOf.MouseDisaster_ThievingState))
                    pawn.mindState?.mentalStateHandler?.Reset();
            }
            float malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition)?.Severity ?? 0f;
            if (pawn.health.hediffSet.HasHediff(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome))
                state.RecordRefeeding(pawn);
            if (MouseDisasterRuntime.AllowsNewContent && ShouldRefeed(foodLevel, malnutrition) &&
                !state.HasAppliedRefeeding(pawn))
            {
                state.RecordRefeeding(pawn);
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_RefeedingSyndrome);
            }
        }
    }

    // Observe ordinary setters immediately, regardless of the source of the food increase.
    [HarmonyPatch(typeof(Need), nameof(Need.CurLevel), MethodType.Setter)]
    internal static class MouseDisasterFoodLevelPatch
    {
        public static void Prefix(Need __instance, float value, out bool __state)
        {
            __state = __instance is Need_Food && value >= __instance.CurLevel;
        }

        public static void Postfix(Need __instance, Pawn ___pawn, bool __state)
        {
            if (__state && __instance is Need_Food)
                MouseDisasterFeeding.Evaluate(___pawn, __instance.CurLevelPercentage);
        }
    }

    // The existing 150-tick need cadence catches direct field edits, loaded values and MaxNutrition changes.
    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.NeedInterval))]
    internal static class MouseDisasterFoodIntervalPatch
    {
        public static void Prefix(Need_Food __instance, Pawn ___pawn)
        {
            MouseDisasterFeeding.Evaluate(___pawn, __instance.CurLevelPercentage);
        }
    }
}
