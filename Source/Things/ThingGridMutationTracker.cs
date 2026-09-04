using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Things
{
    internal static class ThingGridMutationTracker
    {
        private sealed class State
        {
            public int Revision;
        }

        private static readonly ConditionalWeakTable<Map, State> States =
            new ConditionalWeakTable<Map, State>();

        public static int RevisionFor(Map map)
        {
            return map == null ? 0 : States.GetOrCreateValue(map).Revision;
        }

        public static void NotifyChanged(Map map)
        {
            if (map == null)
                return;

            State state;
            if (States.TryGetValue(map, out state))
                state.Revision++;
        }
    }

    [HarmonyPatch(typeof(ThingGrid), nameof(ThingGrid.Register))]
    internal static class Patch_ThingGrid_Register_DenseThingCaches
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Thing t, Map ___map)
        {
            if (t != null && t.def.category == ThingCategory.Item)
                ThingGridMutationTracker.NotifyChanged(___map);
        }
    }

    [HarmonyPatch(typeof(ThingGrid), nameof(ThingGrid.Deregister))]
    internal static class Patch_ThingGrid_Deregister_DenseThingCaches
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Thing t, Map ___map)
        {
            if (t != null && t.def.category == ThingCategory.Item)
                ThingGridMutationTracker.NotifyChanged(___map);
        }
    }

    [HarmonyPatch(typeof(ListerMergeables), nameof(ListerMergeables.Notify_SlotGroupChanged))]
    internal static class Patch_ListerMergeables_NotifySlotGroupChanged_DenseThingCaches
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Map ___map)
        {
            ThingGridMutationTracker.NotifyChanged(___map);
        }
    }
}
