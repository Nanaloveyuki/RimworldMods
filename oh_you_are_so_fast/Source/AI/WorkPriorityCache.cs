using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace OhYouAreSoFast
{
    internal static class WorkPriorityCache
    {
        private sealed class CachedList
        {
            public List<WorkGiver> Value;
            public long ExpiresAtTick;
            public int IntervalSeconds = -1;

            public void Invalidate()
            {
                Value = null;
                ExpiresAtTick = 0L;
                IntervalSeconds = -1;
            }
        }

        private sealed class CacheState
        {
            public readonly CachedList Normal = new CachedList();
            public readonly CachedList Emergency = new CachedList();

            public void Invalidate()
            {
                Normal.Invalidate();
                Emergency.Invalidate();
            }
        }

        private static readonly ConditionalWeakTable<Pawn_WorkSettings, CacheState> States =
            new ConditionalWeakTable<Pawn_WorkSettings, CacheState>();
        private static readonly AccessTools.FieldRef<Pawn_WorkSettings, bool> VanillaWorkGiversDirty =
            AccessTools.FieldRefAccess<Pawn_WorkSettings, bool>("workGiversDirty");

        private static int revision;

        public static int Revision => revision;

        public static bool TryGet(
            Pawn_WorkSettings workSettings,
            bool emergency,
            out List<WorkGiver> workGivers)
        {
            workGivers = null;
            OhYouAreSoFastSettings settings = OhYouAreSoFastMod.Settings;
            if (settings == null || !settings.workPriorityCache || workSettings == null)
                return false;

            CacheState state;
            if (!States.TryGetValue(workSettings, out state))
                return false;

            CachedList cached = emergency ? state.Emergency : state.Normal;
            if (cached.Value == null)
                return false;
            if (VanillaWorkGiversDirty(workSettings))
            {
                state.Invalidate();
                return false;
            }

            long now = GenTicks.TicksGame;
            if (cached.IntervalSeconds != settings.workPriorityCacheIntervalSeconds
                || now >= cached.ExpiresAtTick)
            {
                state.Invalidate();
                VanillaWorkGiversDirty(workSettings) = true;
                return false;
            }

            workGivers = cached.Value;
            return true;
        }

        public static void Record(
            Pawn_WorkSettings workSettings,
            bool emergency,
            List<WorkGiver> workGivers)
        {
            if (workSettings == null || workGivers == null)
                return;

            OhYouAreSoFastSettings settings = OhYouAreSoFastMod.Settings;
            if (settings == null || !settings.workPriorityCache)
                return;

            CacheState state = States.GetOrCreateValue(workSettings);
            if ((state.Normal.IntervalSeconds != -1
                    && state.Normal.IntervalSeconds != settings.workPriorityCacheIntervalSeconds)
                || (state.Emergency.IntervalSeconds != -1
                    && state.Emergency.IntervalSeconds != settings.workPriorityCacheIntervalSeconds))
            {
                state.Invalidate();
            }

            CachedList cached = emergency ? state.Emergency : state.Normal;
            cached.Value = workGivers;
            cached.IntervalSeconds = settings.workPriorityCacheIntervalSeconds;
            cached.ExpiresAtTick = (long)GenTicks.TicksGame
                + settings.workPriorityCacheIntervalSeconds * 60L;
        }

        public static void Invalidate(Pawn_WorkSettings workSettings)
        {
            if (workSettings != null && States.TryGetValue(workSettings, out var state))
                state.Invalidate();

            unchecked
            {
                revision++;
            }
        }

        public static void WarmPlayerPawns()
        {
            if (OhYouAreSoFastMod.Settings == null
                || !OhYouAreSoFastMod.Settings.workPriorityCache
                || Current.Game == null)
            {
                return;
            }

            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (pawn.Faction == Faction.OfPlayer
                    && pawn.workSettings != null
                    && pawn.workSettings.EverWork)
                {
                    Warm(pawn.workSettings);
                }
            }
        }

        private static void Warm(Pawn_WorkSettings workSettings)
        {
            _ = workSettings.WorkGiversInOrderEmergency;
            _ = workSettings.WorkGiversInOrderNormal;
        }
    }

    [HarmonyPatch(
        typeof(Pawn_WorkSettings),
        nameof(Pawn_WorkSettings.WorkGiversInOrderNormal),
        MethodType.Getter)]
    internal static class Patch_Pawn_WorkSettings_Normal
    {
        private static bool Prefix(
            Pawn_WorkSettings __instance,
            ref List<WorkGiver> __result,
            ref bool __state)
        {
            __state = false;
            if (!WorkPriorityCache.TryGet(__instance, false, out var workGivers))
            {
                __state = true;
                return true;
            }

            __result = workGivers;
            return false;
        }

        private static void Postfix(
            Pawn_WorkSettings __instance,
            ref List<WorkGiver> __result,
            bool __state)
        {
            if (__state)
                WorkPriorityCache.Record(__instance, false, __result);
        }
    }

    [HarmonyPatch(
        typeof(Pawn_WorkSettings),
        nameof(Pawn_WorkSettings.WorkGiversInOrderEmergency),
        MethodType.Getter)]
    internal static class Patch_Pawn_WorkSettings_Emergency
    {
        private static bool Prefix(
            Pawn_WorkSettings __instance,
            ref List<WorkGiver> __result,
            ref bool __state)
        {
            __state = false;
            if (!WorkPriorityCache.TryGet(__instance, true, out var workGivers))
            {
                __state = true;
                return true;
            }

            __result = workGivers;
            return false;
        }

        private static void Postfix(
            Pawn_WorkSettings __instance,
            ref List<WorkGiver> __result,
            bool __state)
        {
            if (__state)
                WorkPriorityCache.Record(__instance, true, __result);
        }
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.SetPriority))]
    internal static class Patch_Pawn_WorkSettings_SetPriority
    {
        private static void Postfix(Pawn_WorkSettings __instance)
        {
            WorkPriorityCache.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.DisableAll))]
    internal static class Patch_Pawn_WorkSettings_DisableAll
    {
        private static void Postfix(Pawn_WorkSettings __instance)
        {
            WorkPriorityCache.Invalidate(__instance);
        }
    }

    [HarmonyPatch(
        typeof(Pawn_WorkSettings),
        nameof(Pawn_WorkSettings.Notify_UseWorkPrioritiesChanged))]
    internal static class Patch_Pawn_WorkSettings_UseWorkPrioritiesChanged
    {
        private static void Postfix(Pawn_WorkSettings __instance)
        {
            WorkPriorityCache.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.EnableAndInitialize))]
    internal static class Patch_Pawn_WorkSettings_EnableAndInitialize
    {
        private static void Postfix(Pawn_WorkSettings __instance)
        {
            WorkPriorityCache.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.ExposeData))]
    internal static class Patch_Pawn_WorkSettings_ExposeData
    {
        private static void Postfix(Pawn_WorkSettings __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                WorkPriorityCache.Invalidate(__instance);
        }
    }

    [HarmonyPatch(
        typeof(WindowStack),
        nameof(WindowStack.TryRemove),
        new[] { typeof(Window), typeof(bool) })]
    internal static class Patch_WorkTabClose
    {
        private static void Postfix(Window window, bool __result)
        {
            if (__result && window is MainTabWindow_Work)
                WorkPriorityCache.WarmPlayerPawns();
        }
    }
}
