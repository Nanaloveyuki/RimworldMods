using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SaveYourStorageAndTickEveryMinute.Compatibility;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Things
{
    [HarmonyPatch(
        typeof(StoreUtility),
        "NoStorageBlockersIn",
        new[] { typeof(IntVec3), typeof(Map), typeof(Thing) })]
    internal static class Patch_StoreUtility_NoStorageBlockersIn
    {
        private static readonly OriginalMethodGuard Guard = new OriginalMethodGuard(
            AccessTools.Method(
                typeof(StoreUtility),
                "NoStorageBlockersIn",
                new[] { typeof(IntVec3), typeof(Map), typeof(Thing) }));

        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(IntVec3 c, Map map, Thing thing, ref bool __result)
        {
            SaveYourStorageSettings settings = SaveYourStorageMod.Settings;
            if (settings == null
                || !settings.singlePassStorageCheck
                || !Guard.CanReplace())
                return true;

            List<Thing> things = map.thingGrid.ThingsListAt(c);
            if (things.Count < settings.denseCellMinimumItems)
                return true;

            bool hasMergeTarget = false;
            int itemCount = 0;
            for (int i = 0; i < things.Count; i++)
            {
                Thing existing = things[i];
                if (existing.def.category == ThingCategory.Item)
                    itemCount++;

                if (!hasMergeTarget
                    && existing.def.EverStorable(false)
                    && existing.CanStackWith(thing)
                    && existing.stackCount < existing.def.stackLimit)
                {
                    hasMergeTarget = true;
                }

                if (existing.def.entityDefToBuild != null
                    && existing.def.entityDefToBuild.passability != Traversability.Standable)
                {
                    __result = false;
                    return false;
                }

                if (existing.def.surfaceType == SurfaceType.None
                    && existing.def.passability != Traversability.Standable
                    && (c.GetMaxItemsAllowedInCell(map) <= 1
                        || existing.def.category != ThingCategory.Item))
                {
                    __result = false;
                    return false;
                }
            }

            __result = hasMergeTarget || itemCount < c.GetMaxItemsAllowedInCell(map);
            return false;
        }

    }
}
