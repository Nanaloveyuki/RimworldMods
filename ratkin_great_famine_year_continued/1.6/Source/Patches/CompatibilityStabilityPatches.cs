using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    internal static class MouseDisasterMapJobDiagnostics
    {
        private sealed class TraceState
        {
            public int lastTick;
            public int suppressed;
        }

        private static readonly ConditionalWeakTable<Pawn, TraceState> Traces = new ConditionalWeakTable<Pawn, TraceState>();

        internal static bool Applies(Pawn pawn, JobDriver driver)
        {
            return pawn != null && (driver is JobDriver_Wait || driver is JobDriver_Vomit) &&
                ((pawn.kindDef?.defName?.StartsWith("MouseDisaster_", StringComparison.Ordinal) ?? false) ||
                 MouseDisasterUtility.IsMouseDisasterPawn(pawn));
        }

        internal static bool MissingMap(Pawn pawn, JobDriver driver)
        {
            // Wait uses MapHeld; Vomit explicitly reads pawn.Map. Do not reject held Wait jobs.
            return driver is JobDriver_Vomit ? pawn.Map == null : pawn.MapHeld == null;
        }

        internal static void Trace(Pawn pawn, JobDriver driver, string reason)
        {
            int tick = Find.TickManager?.TicksGame ?? Environment.TickCount;
            if (Traces.TryGetValue(pawn, out TraceState state))
            {
                if (tick >= state.lastTick && (long)tick - state.lastTick < 600)
                {
                    state.suppressed++;
                    return;
                }
            }
            else
            {
                state = new TraceState();
                Traces.Add(pawn, state);
            }
            int suppressed = state.suppressed;
            state.lastTick = tick;
            state.suppressed = 0;
            Log.Warning("[MouseDisaster][MapJobGuard] " + reason +
                "; tick=" + tick + "; pawn=" + pawn.ThingID + "; kind=" + pawn.kindDef?.defName +
                "; spawned=" + pawn.Spawned + "; dead=" + pawn.Dead + "; destroyed=" + pawn.Destroyed +
                "; position=" + pawn.Position + "; map=" + pawn.Map + "; heldMap=" + pawn.MapHeld +
                "; holder=" + pawn.ParentHolder + "; faction=" + pawn.Faction +
                "; driver=" + driver?.GetType().FullName + "; job=" + driver?.job +
                "; currentJob=" + pawn.CurJob + "; suppressed=" + suppressed +
                "\nCaller stack:\n" + Environment.StackTrace);
        }
    }

    [HarmonyPatch(typeof(JobDriver), "TryActuallyStartNextToil")]
    internal static class MouseDisasterMapJobToilGuardPatch
    {
        internal static bool Prefix(JobDriver __instance)
        {
            Pawn pawn = __instance.pawn;
            if (__instance.job == null || pawn?.CurJob != __instance.job ||
                !MouseDisasterMapJobDiagnostics.Applies(pawn, __instance) ||
                !MouseDisasterMapJobDiagnostics.MissingMap(pawn, __instance)) return true;

            MouseDisasterMapJobDiagnostics.Trace(pawn, __instance, "Prevented map-dependent toil without map");
            // Do not enter vanilla's Errored -> Wait recovery loop, or pool a job still on the call stack.
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false, canReturnToPool: false);
            return false;
        }
    }

    [HarmonyPatch(typeof(JobUtility), nameof(JobUtility.TryStartErrorRecoverJob))]
    internal static class MouseDisasterMapJobErrorTracePatch
    {
        internal static void Prefix(Pawn pawn, string message, Exception exception, JobDriver concreteDriver)
        {
            JobDriver driver = concreteDriver ?? pawn?.jobs?.curDriver;
            if (MouseDisasterMapJobDiagnostics.Applies(pawn, driver))
                MouseDisasterMapJobDiagnostics.Trace(pawn, driver, "Job error: " + message + "\n" + exception);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class MouseDisasterStartJobGuardPatch
    {
        private static readonly System.Reflection.FieldInfo PawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");

        public static bool Prefix(Pawn_JobTracker __instance, Job newJob)
        {
            if (!MouseDisasterUtility.IsExtremeCompatJobModeEnabled || newJob?.def?.defName == null)
            {
                return true;
            }

            if (newJob.def.defName.IndexOf("BePlayedWith", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return true;
            }

            Pawn pawn = PawnField?.GetValue(__instance) as Pawn;
            if (pawn == null)
            {
                return true;
            }

            if ((newJob.targetA.IsValid && newJob.targetA.Thing == pawn) ||
                (newJob.targetB.IsValid && newJob.targetB.Thing == pawn))
            {
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Log), nameof(Log.Error), new[] { typeof(string) })]
    public static class MouseDisasterErrorLogThrottlePatch
    {
        private static readonly Dictionary<string, int> LastEmitTickByKey = new Dictionary<string, int>();

        public static bool Prefix(string text)
        {
            if (Current.CreatingWorld != null || Current.ProgramState != ProgramState.Playing ||
                !MouseDisasterUtility.IsExtremeCompatJobModeEnabled || text.NullOrEmpty())
            {
                return true;
            }

            string key = null;
            if (text.IndexOf("Error in PotentialWorkThingsGlobal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                key = "trainingfacility_potentialwork";
            }
            else if (text.IndexOf("Constructed TargetInfo with cell=", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     text.IndexOf("null map", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                key = "null_map_targetinfo";
            }

            if (key == null)
            {
                return true;
            }

            int nowTick = Find.TickManager?.TicksGame ?? Environment.TickCount;
            if (LastEmitTickByKey.TryGetValue(key, out int lastTick) && nowTick >= lastTick && nowTick - lastTick < 600)
            {
                return false;
            }

            LastEmitTickByKey[key] = nowTick;
            return true;
        }
    }
}
