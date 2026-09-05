using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SaveYourStorageAndTickEveryMinute.Collections;
using SaveYourStorageAndTickEveryMinute.Compatibility;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Things
{
    internal static class DenseCellItemCountCache
    {
        private sealed class CellCount
        {
            public List<Thing> Source;
            public int SourceVersion;
            public int ItemCount;
        }

        private sealed class MapCounts
        {
            public readonly Dictionary<int, CellCount> Cells =
                new Dictionary<int, CellCount>();
        }

        private static readonly ConditionalWeakTable<Map, MapCounts> Counts =
            new ConditionalWeakTable<Map, MapCounts>();

        public static bool TryGetCount(IntVec3 cell, Map map, out int itemCount)
        {
            itemCount = 0;
            SaveYourStorageSettings settings = SaveYourStorageMod.Settings;
            if (settings == null
                || !settings.denseCellLayoutCache
                || map == null
                || !cell.InBounds(map)
                || !ListVersionAccessor.IsAvailable)
            {
                return false;
            }

            List<Thing> source = map.thingGrid.ThingsListAtFast(cell);
            if (source.Count < settings.denseCellMinimumItems)
                return false;

            int sourceVersion;
            if (!ListVersionAccessor.TryGet(source, out sourceVersion))
                return false;

            int cellIndex = map.cellIndices.CellToIndex(cell);
            MapCounts mapCounts = Counts.GetOrCreateValue(map);
            CellCount cached;
            if (mapCounts.Cells.TryGetValue(cellIndex, out cached)
                && ReferenceEquals(cached.Source, source)
                && cached.SourceVersion == sourceVersion)
            {
                itemCount = cached.ItemCount;
                return true;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].def.category == ThingCategory.Item)
                    itemCount++;
            }

            if (cached == null)
            {
                cached = new CellCount();
                mapCounts.Cells.Add(cellIndex, cached);
            }
            cached.Source = source;
            cached.SourceVersion = sourceVersion;
            cached.ItemCount = itemCount;
            return true;
        }
    }

    [HarmonyPatch(typeof(GridsUtility), nameof(GridsUtility.GetItemCount))]
    internal static class Patch_GridsUtility_GetItemCount
    {
        private static readonly OriginalMethodGuard Guard = new OriginalMethodGuard(
            AccessTools.Method(
                typeof(GridsUtility),
                nameof(GridsUtility.GetItemCount),
                new[] { typeof(IntVec3), typeof(Map) }));

        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(IntVec3 c, Map map, ref int __result)
        {
            return !Guard.CanReplace()
                || !DenseCellItemCountCache.TryGetCount(c, map, out __result);
        }
    }
}
