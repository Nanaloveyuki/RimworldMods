using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    public static class MouseDisasterIncidentTargetPolicy
    {
        public static bool ShouldBlockEnvironmentTemperature(string incidentDefName, IIncidentTarget target)
        {
            if (target is not Map map || map.mapTemperature == null ||
                !MouseDisasterIncidentCatalog.IsKnownIncident(incidentDefName))
            {
                return false;
            }

            MouseDisasterIncidentEntry entry = MouseDisasterIncidentCatalog.AllEntries
                .FirstOrDefault(candidate => candidate.DefName.Equals(incidentDefName, StringComparison.OrdinalIgnoreCase));
            if (entry == null || entry.TargetKind != MouseDisasterIncidentTargetKind.Map)
            {
                return false;
            }

            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            return settings != null &&
                   !settings.IsMouseDisasterEnvironmentTemperatureAllowed(map.mapTemperature.OutdoorTemp);
        }

        public static bool ShouldBlockCanFireNowForInvalidTarget(
            string incidentDefName,
            bool hasTarget,
            bool targetIsMap,
            bool targetIsCaravan)
        {
            if (string.IsNullOrWhiteSpace(incidentDefName) ||
                !incidentDefName.StartsWith("MouseDisaster_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!hasTarget)
            {
                return true;
            }

            var entry = MouseDisasterIncidentCatalog.AllEntries
                .FirstOrDefault(candidate => candidate.DefName.Equals(incidentDefName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                return false;
            }

            switch (entry.TargetKind)
            {
                case MouseDisasterIncidentTargetKind.Caravan:
                    return !targetIsCaravan;
                default:
                    return !targetIsMap;
            }
        }

        public static bool ShouldBlockTargetAllowedEvaluation(
            string incidentDefName,
            bool hasTarget,
            bool hasTargetTags,
            bool hasIncidentTargetTags)
        {
            return !string.IsNullOrWhiteSpace(incidentDefName) &&
                   incidentDefName.StartsWith("MouseDisaster_", StringComparison.OrdinalIgnoreCase) &&
                   (!hasTarget || !hasTargetTags || !hasIncidentTargetTags);
        }

        public static bool TryEvaluateSafeTargetAllowed(
            string incidentDefName,
            bool targetIsMap,
            bool targetIsPlayerHomeMap,
            bool targetIsCaravan,
            bool targetIsPlayerControlledCaravan,
            out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(incidentDefName) ||
                !incidentDefName.StartsWith("MouseDisaster_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var entry = MouseDisasterIncidentCatalog.AllEntries
                .FirstOrDefault(candidate => candidate.DefName.Equals(incidentDefName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                result = false;
                return false;
            }

            switch (entry.TargetKind)
            {
                case MouseDisasterIncidentTargetKind.Caravan:
                    result = targetIsCaravan && targetIsPlayerControlledCaravan;
                    return true;
                default:
                    result = targetIsMap && targetIsPlayerHomeMap;
                    return true;
            }
        }
    }
}
