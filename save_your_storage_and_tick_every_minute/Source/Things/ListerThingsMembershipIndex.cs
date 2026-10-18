using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SaveYourStorageAndTickEveryMinute.Collections;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Things
{
    internal static class ListerThingsMembershipIndex
    {
        private const int MinimumIndexedCount = 64;

        private sealed class State
        {
            public readonly HashSet<Thing> Things =
                new HashSet<Thing>(ReferenceEqualityComparer<Thing>.Instance);
            public int ObservedCount;
            public int ObservedVersion;
            public bool Initialized;
            public bool Exact;

            public void Rebuild(List<Thing> source, int version)
            {
                Things.Clear();
                bool exact = true;
                for (int i = 0; i < source.Count; i++)
                {
                    Thing thing = source[i];
                    if (thing == null || !Things.Add(thing))
                        exact = false;
                }

                ObservedCount = source.Count;
                ObservedVersion = version;
                Initialized = true;
                Exact = exact && Things.Count == source.Count;
            }
        }

        private static readonly ConditionalWeakTable<ListerThings, State> States =
            new ConditionalWeakTable<ListerThings, State>();

        public static void InitializeEmpty(ListerThings lister)
        {
            if (lister == null || lister.use != ListerThingsUse.Global)
                return;

            int version;
            if (!TryGetVersion(lister.AllThings, out version))
                return;

            States.Remove(lister);
            States.GetOrCreateValue(lister).Rebuild(lister.AllThings, version);
        }

        public static void ObserveAdd(ListerThings lister, Thing thing, int previousCount)
        {
            ObserveMutation(lister, thing, previousCount, true);
        }

        public static void ObserveRemove(ListerThings lister, Thing thing, int previousCount)
        {
            ObserveMutation(lister, thing, previousCount, false);
        }

        public static bool TryContains(ListerThings lister, Thing thing, out bool contains)
        {
            contains = false;
            if (lister == null)
                return false;

            List<Thing> source = lister.AllThings;
            if (source.Count < MinimumIndexedCount)
                return false;

            int version;
            if (!TryGetVersion(source, out version))
                return false;

            State state = States.GetOrCreateValue(lister);
            if (!state.Initialized
                || state.ObservedCount != source.Count
                || state.ObservedVersion != version)
            {
                state.Rebuild(source, version);
            }

            if (!state.Exact)
                return false;

            contains = thing != null && state.Things.Contains(thing);
            return true;
        }

        private static void ObserveMutation(
            ListerThings lister,
            Thing thing,
            int previousCount,
            bool adding)
        {
            if (lister == null || lister.use != ListerThingsUse.Global)
                return;

            State state = States.GetOrCreateValue(lister);
            if (!state.Initialized)
                return;

            List<Thing> source = lister.AllThings;
            int version;
            int expectedDelta = adding ? 1 : -1;
            if (!state.Exact
                || state.ObservedCount != previousCount
                || source.Count != previousCount + expectedDelta
                || !TryGetVersion(source, out version))
            {
                state.Initialized = false;
                state.Exact = false;
                return;
            }

            bool changed = adding ? state.Things.Add(thing) : state.Things.Remove(thing);
            state.ObservedCount = source.Count;
            state.ObservedVersion = version;
            state.Exact = changed && state.Things.Count == source.Count;
        }

        private static bool TryGetVersion(List<Thing> source, out int version)
        {
            return ListVersionAccessor.TryGet(source, out version);
        }
    }

    [HarmonyPatch(
        typeof(ListerThings),
        MethodType.Constructor,
        new[] { typeof(ListerThingsUse), typeof(ThingListChangedCallbacks) })]
    internal static class Patch_ListerThings_Constructor
    {
        private static void Postfix(ListerThings __instance)
        {
            ListerThingsMembershipIndex.InitializeEmpty(__instance);
        }
    }

    [HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Add), new[] { typeof(Thing) })]
    internal static class Patch_ListerThings_Add
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ListerThings __instance, ref int __state)
        {
            __state = __instance.use == ListerThingsUse.Global
                ? __instance.AllThings.Count
                : -1;
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ListerThings __instance, Thing t, int __state)
        {
            if (__state >= 0)
                ListerThingsMembershipIndex.ObserveAdd(__instance, t, __state);
        }
    }

    [HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Remove), new[] { typeof(Thing) })]
    internal static class Patch_ListerThings_Remove
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ListerThings __instance, ref int __state)
        {
            __state = __instance.use == ListerThingsUse.Global
                ? __instance.AllThings.Count
                : -1;
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ListerThings __instance, Thing t, int __state)
        {
            if (__state >= 0)
                ListerThingsMembershipIndex.ObserveRemove(__instance, t, __state);
        }
    }

    [HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Contains), new[] { typeof(Thing) })]
    internal static class Patch_ListerThings_Contains
    {
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(ListerThings __instance, Thing t, ref bool __result)
        {
            SaveYourStorageSettings settings = SaveYourStorageMod.Settings;
            if (settings == null
                || !settings.listerMembershipIndex
                || __instance.use != ListerThingsUse.Global)
                return true;

            bool contains;
            if (!ListerThingsMembershipIndex.TryContains(__instance, t, out contains))
                return true;

            __result = contains;
            return false;
        }
    }

    [HarmonyPatch(typeof(ListerThings), nameof(ListerThings.Clear))]
    internal static class Patch_ListerThings_Clear
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ListerThings __instance)
        {
            ListerThingsMembershipIndex.InitializeEmpty(__instance);
        }
    }
}
