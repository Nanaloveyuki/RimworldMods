using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace OhYouAreSoFast
{
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    internal static class Patch_JobGiver_Work
    {
        private sealed class PawnState
        {
            public readonly Dictionary<JobGiver_Work, int> NextScanTick =
                new Dictionary<JobGiver_Work, int>();
            public int WorkPriorityRevision = -1;
        }

        private static readonly ConditionalWeakTable<Pawn, PawnState> States =
            new ConditionalWeakTable<Pawn, PawnState>();

        private static bool Prefix(
            JobGiver_Work __instance,
            Pawn pawn,
            ref ThinkResult __result,
            ref bool __state)
        {
            __state = false;
            OhYouAreSoFastSettings settings = OhYouAreSoFastMod.Settings;
            if (settings == null
                || !settings.workScanThrottle
                || __instance.emergency
                || pawn == null
                || pawn.CurJob != null
                || !pawn.Spawned
                || pawn.Dead
                || pawn.Map == null
                || pawn.Map.cellIndices.NumGridCells < settings.idleThinkMinMapCells)
            {
                return true;
            }

            PawnState state = States.GetOrCreateValue(pawn);
            int workPriorityRevision = WorkPriorityCache.Revision;
            if (state.WorkPriorityRevision != workPriorityRevision)
            {
                state.NextScanTick.Clear();
                state.WorkPriorityRevision = workPriorityRevision;
            }

            if (state.NextScanTick.TryGetValue(__instance, out var nextScanTick)
                && GenTicks.TicksGame < nextScanTick)
            {
                __result = ThinkResult.NoJob;
                return false;
            }

            __state = true;
            return true;
        }

        private static void Postfix(
            JobGiver_Work __instance,
            Pawn pawn,
            ThinkResult __result,
            bool __state)
        {
            if (!__state)
                return;

            OhYouAreSoFastSettings settings = OhYouAreSoFastMod.Settings;
            if (settings == null
                || !settings.workScanThrottle
                || __instance.emergency
                || pawn == null
                || pawn.CurJob != null
                || !pawn.Spawned
                || pawn.Dead
                || pawn.Map == null
                || pawn.Map.cellIndices.NumGridCells < settings.idleThinkMinMapCells)
            {
                return;
            }

            PawnState state = States.GetOrCreateValue(pawn);
            if (__result.IsValid)
                state.NextScanTick.Remove(__instance);
            else
                state.NextScanTick[__instance] =
                    GenTicks.TicksGame + settings.workScanIntervalSeconds * 60;
        }
    }
}
