using Verse;

namespace MouseDisaster
{
    internal static class MouseDisasterTrace
    {
        private const string Prefix = "[MouseDisaster Trace] ";

        internal static bool Enabled => MouseDisasterMod.Settings?.enableDetailedTraceLog == true;

        internal static void Log(string message)
        {
            if (!Enabled) return;
            Verse.Log.Message(Prefix + (message ?? string.Empty));
        }

        internal static string DescribeMap(Map map)
        {
            if (map == null) return "map=none";
            string temperature = map.mapTemperature == null
                ? "unavailable"
                : map.mapTemperature.OutdoorTemp.ToString("0.0");
            return "map=" + map.uniqueID + ";outdoorTemp=" + temperature;
        }

        internal static string DescribePawn(Pawn pawn)
        {
            if (pawn == null) return "pawn=none";
            return "pawn=" + pawn.ThingID + ";kind=" + (pawn.kindDef?.defName ?? "unknown");
        }

        internal static string DescribeTarget(object target)
        {
            return target is Map map
                ? DescribeMap(map)
                : "target=" + (target?.GetType().Name ?? "none");
        }
    }
}
