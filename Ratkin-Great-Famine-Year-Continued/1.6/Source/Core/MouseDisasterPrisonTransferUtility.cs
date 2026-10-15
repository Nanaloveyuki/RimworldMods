using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MouseDisaster
{
    public static class MouseDisasterPrisonTransferUtility
    {
        private const string RimPrisonApiTypeName = "RimPrisonReset.RimPrisonApi";
        private const string RimPrisonIsPrisonAreaCellMethodName = "IsPrisonAreaCell";
        private const int PrisonScanCacheDurationTicks = 600;

        private static bool rimPrisonApiResolved;
        private static MethodInfo rimPrisonIsPrisonAreaCellMethod;
        private static int cachedScanMapId = -1;
        private static int cachedScanTick = -1;
        private static List<IntVec3> cachedPrisonCells;
        private static bool cachedScanComplete;

        internal static void ResetTransientState()
        {
            cachedScanMapId = -1;
            cachedScanTick = -1;
            cachedPrisonCells = null;
            cachedScanComplete = false;
        }

        public static bool IsPrisonIntegrationEnabled()
        {
            return TryResolveRimPrisonMethod(out _);
        }

        public static bool HasAvailablePrisonArea(Map map)
        {
            return TryFindAvailablePrisonCells(map, 1, out _);
        }

        public static bool TryFindAvailablePrisonCells(Map map, int desiredCount, out List<IntVec3> cells)
        {
            cells = new List<IntVec3>();
            if (map == null || desiredCount <= 0 || !TryResolveRimPrisonMethod(out MethodInfo method))
            {
                return false;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (cachedScanMapId == map.uniqueID && nowTick >= cachedScanTick &&
                nowTick - cachedScanTick < PrisonScanCacheDurationTicks)
            {
                for (int i = 0; i < cachedPrisonCells.Count && cells.Count < desiredCount; i++)
                {
                    IntVec3 cell = cachedPrisonCells[i];
                    if (cell.InBounds(map) && cell.Standable(map) && !cell.GetThingList(map).Any(thing => thing is Pawn))
                    {
                        cells.Add(cell);
                    }
                }

                if (cells.Count >= desiredCount || cachedScanComplete)
                {
                    return cells.Count >= desiredCount;
                }
            }

            cachedScanMapId = map.uniqueID;
            cachedScanTick = nowTick;
            cachedPrisonCells = new List<IntVec3>();
            cachedScanComplete = false;

            foreach (IntVec3 cell in map.AllCells)
            {
                if (!cell.InBounds(map) ||
                    !cell.Standable(map) ||
                    !IsPrisonAreaCell(method, map, cell))
                {
                    continue;
                }

                cachedPrisonCells.Add(cell);
                if (!cell.GetThingList(map).Any(thing => thing is Pawn))
                {
                    cells.Add(cell);
                    if (cells.Count >= desiredCount)
                    {
                        return true;
                    }
                }
            }

            cachedScanComplete = true;
            return cells.Count >= desiredCount;
        }

        private static bool TryResolveRimPrisonMethod(out MethodInfo method)
        {
            if (!rimPrisonApiResolved)
            {
                Type apiType = AccessTools.TypeByName(RimPrisonApiTypeName);
                rimPrisonIsPrisonAreaCellMethod = apiType?.GetMethod(
                    RimPrisonIsPrisonAreaCellMethodName,
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(Map), typeof(IntVec3) },
                    modifiers: null);
                rimPrisonApiResolved = true;
            }

            method = rimPrisonIsPrisonAreaCellMethod;
            return method != null;
        }

        private static bool IsPrisonAreaCell(MethodInfo method, Map map, IntVec3 cell)
        {
            object result = method.Invoke(null, new object[] { map, cell });
            return result is bool isPrisonArea && isPrisonArea;
        }
    }
}
