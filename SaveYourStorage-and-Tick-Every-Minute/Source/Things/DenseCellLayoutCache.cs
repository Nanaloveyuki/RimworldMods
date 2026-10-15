using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using SaveYourStorageAndTickEveryMinute.Collections;
using SaveYourStorageAndTickEveryMinute.Compatibility;
using UnityEngine;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Things
{
    internal static class DenseCellLayoutCache
    {
        private sealed class CellLayout
        {
            public readonly Dictionary<Thing, Vector3> Centers =
                new Dictionary<Thing, Vector3>(ReferenceEqualityComparer<Thing>.Instance);
            public readonly List<Thing> Items = new List<Thing>();
            public List<Thing> Source;
            public int SourceVersion;
            public int MapRevision;
            public int MinimumItems;
            public bool Eligible;
            public bool AnyWeapon;
        }

        private sealed class MapLayouts
        {
            public readonly Dictionary<int, CellLayout> Cells =
                new Dictionary<int, CellLayout>();
        }

        private static readonly ConditionalWeakTable<Map, MapLayouts> Layouts =
            new ConditionalWeakTable<Map, MapLayouts>();

        public static bool TryGetCenter(Thing thing, out Vector3 center)
        {
            center = default(Vector3);
            SaveYourStorageSettings settings = SaveYourStorageMod.Settings;
            if (settings == null
                || !settings.denseCellLayoutCache
                || thing == null
                || !thing.Spawned
                || thing.def.category != ThingCategory.Item
                || !ListVersionAccessor.IsAvailable)
            {
                return false;
            }

            Map map = thing.Map;
            IntVec3 position = thing.Position;
            if (map == null || !position.InBounds(map))
                return false;

            List<Thing> source = map.thingGrid.ThingsListAtFast(position);
            if (source.Count < settings.denseCellMinimumItems)
                return false;

            int sourceVersion;
            if (!TryGetVersion(source, out sourceVersion))
                return false;

            int cellIndex = map.cellIndices.CellToIndex(position);
            int mapRevision = ThingGridMutationTracker.RevisionFor(map);
            MapLayouts mapLayouts = Layouts.GetOrCreateValue(map);
            CellLayout layout;
            if (!mapLayouts.Cells.TryGetValue(cellIndex, out layout))
            {
                layout = new CellLayout();
                mapLayouts.Cells.Add(cellIndex, layout);
            }

            bool valid = ReferenceEquals(layout.Source, source)
                && layout.SourceVersion == sourceVersion
                && layout.MinimumItems == settings.denseCellMinimumItems
                && (!layout.AnyWeapon || layout.MapRevision == mapRevision);

            if (!valid)
            {
                Rebuild(
                    layout,
                    source,
                    sourceVersion,
                    mapRevision,
                    position,
                    map,
                    settings.denseCellMinimumItems);
            }

            return layout.Eligible && layout.Centers.TryGetValue(thing, out center);
        }

        private static void Rebuild(
            CellLayout layout,
            List<Thing> source,
            int sourceVersion,
            int mapRevision,
            IntVec3 position,
            Map map,
            int minimumItems)
        {
            layout.Centers.Clear();
            layout.Source = source;
            layout.SourceVersion = sourceVersion;
            layout.MapRevision = mapRevision;
            layout.MinimumItems = minimumItems;

            List<Thing> items = layout.Items;
            items.Clear();
            if (items.Capacity < source.Count)
                items.Capacity = source.Count;
            ThingDef firstDef = null;
            bool allSameDef = true;
            bool anyWeapon = false;
            for (int i = 0; i < source.Count; i++)
            {
                Thing candidate = source[i];
                if (candidate.def.category != ThingCategory.Item)
                    continue;

                if (firstDef == null)
                    firstDef = candidate.def;
                else if (candidate.def != firstDef)
                    allSameDef = false;

                if (candidate.def.IsWeapon && candidate.def != ThingDefOf.WoodLog)
                    anyWeapon = true;
                items.Add(candidate);
            }

            layout.Eligible = items.Count >= minimumItems;
            layout.AnyWeapon = anyWeapon;
            if (!layout.Eligible)
                return;

            items.Sort(CompareThingIds);
            Vector3 baseCenter = position.ToVector3Shifted();
            int adjacencyOffset = anyWeapon
                ? GetWeaponAdjacencyOffset(position + IntVec3.West, map)
                : 0;
            for (int rank = 0; rank < items.Count; rank++)
            {
                Thing item = items[rank];
                float altitude = item.def.Altitude + rank * 0.03658537f / 10f;
                Vector3 result;
                if (items.Count <= 1)
                {
                    result = new Vector3(baseCenter.x, item.def.Altitude, baseCenter.z);
                }
                else if (anyWeapon)
                {
                    float width = 1f / items.Count;
                    result = new Vector3(
                        baseCenter.x - 0.5f + width * (rank + 0.5f),
                        altitude,
                        baseCenter.z + ((adjacencyOffset % 2 == 0) ? -0.02f : 0.2f));
                }
                else if (allSameDef)
                {
                    result = new Vector3(
                        baseCenter.x + rank * 0.11f - 0.08f,
                        altitude,
                        baseCenter.z + rank * 0.24f - 0.05f);
                }
                else
                {
                    Vector2 offset = GenGeo.RegularPolygonVertexPosition(
                        items.Count,
                        rank,
                        ((position.x + position.z) % 2 == 0) ? 0f : 60f) * 0.3f;
                    result = new Vector3(
                        offset.x + baseCenter.x,
                        altitude,
                        offset.y + baseCenter.z);
                }

                layout.Centers[item] = result;
            }
        }

        private static int GetWeaponAdjacencyOffset(IntVec3 cell, Map map)
        {
            int offset = 0;
            while (cell.InBounds(map))
            {
                int itemCount = cell.GetItemCount(map);
                if (itemCount <= 1)
                    break;
                if (itemCount % 2 == 1)
                    offset++;
                cell += IntVec3.West;
            }
            return offset;
        }

        private static int CompareThingIds(Thing left, Thing right)
        {
            return left.thingIDNumber.CompareTo(right.thingIDNumber);
        }

        private static bool TryGetVersion(List<Thing> source, out int version)
        {
            return ListVersionAccessor.TryGet(source, out version);
        }
    }

    [HarmonyPatch(typeof(GenThing), "ItemCenterAt")]
    internal static class Patch_GenThing_ItemCenterAt
    {
        private static readonly OriginalMethodGuard Guard = new OriginalMethodGuard(
            AccessTools.Method(typeof(GenThing), "ItemCenterAt"));

        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(Thing thing, ref Vector3 __result)
        {
            if (!Guard.CanReplace())
                return true;

            Vector3 center;
            if (!DenseCellLayoutCache.TryGetCenter(thing, out center))
                return true;

            __result = center;
            return false;
        }

    }
}
