using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Trade
{
    internal static class RHAH_CaravanStay
    {
        internal const int FoodPerChild = 10;
        internal const float TemperatureSeverity = 0.15f;
        internal const float TemperatureMargin = 10f;

        internal static bool IsTradeCaravan(string displayId, RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.Trader || displayId == "I-012" || displayId == "I-038";
        }

        internal static bool ShouldLeave(
            bool isTradeCaravan,
            bool ignoreHarshEnvironment,
            bool ignoreEnclosedSpace,
            bool temperatureSevere,
            bool anomalousWeather,
            bool traderExitCondition,
            bool cannotReachEdge)
        {
            if (!isTradeCaravan)
            {
                return false;
            }

            bool environment = !ignoreHarshEnvironment && (temperatureSevere || anomalousWeather || traderExitCondition);
            bool enclosed = !ignoreEnclosedSpace && cannotReachEdge;
            return environment || enclosed;
        }

        internal static bool TemperatureSevere(float heatstroke, float hypothermia)
        {
            return Severity(heatstroke) > TemperatureSeverity || Severity(hypothermia) > TemperatureSeverity;
        }

        static float Severity(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return 0f;
            }

            return value;
        }
        internal static bool MemberMustLeave(Verse.Pawn pawn, bool ignoreHarshEnvironment, bool ignoreEnclosedSpace)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || !IsTradeCaravan(pawn))
            {
                return false;
            }

            float heat = HediffSeverity(pawn, HediffDefOf.Heatstroke);
            float cold = HediffSeverity(pawn, HediffDefOf.Hypothermia);
            float blood = 0f;
            if (ModsConfig.AnomalyActive && HediffDefOf.BloodRage != null)
            {
                blood = HediffSeverity(pawn, HediffDefOf.BloodRage);
            }

            return ShouldLeave(
                true,
                ignoreHarshEnvironment,
                ignoreEnclosedSpace,
                TemperatureSevere(heat, cold),
                TemperatureSevere(blood, 0f),
                TraderExitActive(pawn),
                !pawn.CanReachMapEdge());
        }

        static float HediffSeverity(Verse.Pawn pawn, HediffDef def)
        {
            Hediff hediff = pawn.health?.hediffSet == null || def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
            return hediff == null ? 0f : hediff.Severity;
        }

        static bool TraderExitActive(Verse.Pawn pawn)
        {
            GameConditionManager manager = pawn.Map?.gameConditionManager;
            if (manager == null)
            {
                return false;
            }

            List<GameConditionDef> defs = DefDatabase<GameConditionDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                GameConditionDef def = defs[i];
                if (def != null && def.causesTraderCaravanExit && manager.ConditionIsActive(def))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsTradeCaravan(Verse.Pawn pawn)
        {
            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            return snapshot != null && IsTradeCaravan(snapshot.SourceIncidentDisplayId, snapshot.Role);
        }

        static bool CellIsHarsh(Verse.Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            FloatRange comfort = pawn.ComfortableTemperatureRange();
            return CellIsHarsh(pawn.AmbientTemperature, comfort.min, comfort.max);
        }

        static bool RoomIsEnclosed(Verse.Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            Room room = pawn.GetRoom();
            return RoomIsEnclosed(room != null, room != null && room.PsychologicallyOutdoors, room != null && room.UsesOutdoorTemperature);
        }

        internal static bool BlocksEnvironmentLeave(bool ignoreHarshEnvironment, bool isTradeCaravan)
        {
            return ignoreHarshEnvironment && isTradeCaravan;
        }

        internal static bool BlocksEnclosedLeave(bool ignoreEnclosedSpace, bool isTradeCaravan)
        {
            return ignoreEnclosedSpace && isTradeCaravan;
        }

        internal static bool IsEnvironmentLeave(bool ignoreHarshEnvironment, bool isTradeCaravan, string messageKey)
        {
            return BlocksEnvironmentLeave(ignoreHarshEnvironment, isTradeCaravan) &&
                (messageKey == "MessageVisitorsDangerousTemperature" ||
                 messageKey == "MessageVisitorsAnomalousWeather" ||
                 messageKey == "MessageVisitorsDangerousConditions");
        }

        internal static bool IsEnclosedLeave(bool ignoreEnclosedSpace, bool isTradeCaravan, string messageKey)
        {
            return BlocksEnclosedLeave(ignoreEnclosedSpace, isTradeCaravan) && messageKey == "MessageVisitorsTrappedLeaving";
        }

        internal static bool CellIsHarsh(float temperature, float comfortMin, float comfortMax)
        {
            if (float.IsNaN(temperature) || float.IsInfinity(temperature) ||
                float.IsNaN(comfortMin) || float.IsInfinity(comfortMin) ||
                float.IsNaN(comfortMax) || float.IsInfinity(comfortMax))
            {
                return false;
            }

            return temperature < comfortMin - TemperatureMargin || temperature > comfortMax + TemperatureMargin;
        }

        internal static bool RoomIsEnclosed(bool hasRoom, bool psychologicallyOutdoors, bool usesOutdoorTemperature)
        {
            return hasRoom && !psychologicallyOutdoors && !usesOutdoorTemperature;
        }

        internal static bool AllowsFoodForChild(bool foodSubstitutionEnabled, RHAH_ChoiceKind choice, bool playerSide, bool foodIsNutrition, int foodCount, int childCount)
        {
            if (!foodSubstitutionEnabled || choice != RHAH_ChoiceKind.ChildExchange || !playerSide || !foodIsNutrition || childCount <= 0)
            {
                return false;
            }

            int each = RHAH_Mod.Settings == null ? FoodPerChild : RHAH_Mod.Settings.foodPerChild;
            return foodCount >= childCount * each;
        }

        internal static bool NpcSellsFood(bool isTradeCaravan, bool foodIsNutrition, string defName)
        {
            return HungerAndHavoc.Pawn.Compat.RHAH_RatEggCuisine.NpcSells(isTradeCaravan, foodIsNutrition, defName);
        }

        internal static string MessageKey(TransitionAction action)
        {
            TransitionAction_Message message = action as TransitionAction_Message;
            return message?.message;
        }
    }
}
