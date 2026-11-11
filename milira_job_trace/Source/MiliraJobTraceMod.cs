using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraJobTrace
{
    public sealed class MiliraJobTraceMod : Mod
    {
        private const string HarmonyId = "nanaloveyuki.milirajobtrace";

        public MiliraJobTraceMod(ModContentPack content) : base(content)
        {
            new Harmony(HarmonyId).PatchAll(typeof(MiliraJobTraceMod).Assembly);
            Log.Message("[MiliraJobTrace] Read-only job tracing enabled for drafted Milira colonists.");
            Log.Message("[MiliraJobTrace] Harmony patches: " +
                TraceLog.DescribePatches(AccessTools.Method(typeof(Pawn_JobTracker), "JobTrackerTickInterval")) +
                " | " + TraceLog.DescribePatches(AccessTools.Method(
                    typeof(ThinkNode_ConditionalCanDoConstantThinkTreeJobNow), "Satisfied")));
        }
    }

    internal static class TraceLog
    {
        private static readonly FieldInfo JobTrackerPawn = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");
        private static readonly FieldInfo PathFollowerPawn = AccessTools.Field(typeof(Pawn_PathFollower), "pawn");
        private static readonly HashSet<string> TreeProbesWritten = new HashSet<string>();
        private static readonly HashSet<string> TreeConditionsWritten = new HashSet<string>();

        internal static Pawn PawnFrom(Pawn_JobTracker tracker)
        {
            return JobTrackerPawn?.GetValue(tracker) as Pawn;
        }

        internal static Pawn PawnFrom(Pawn_PathFollower pather)
        {
            return PathFollowerPawn?.GetValue(pather) as Pawn;
        }

        internal static void JobStart(Pawn_JobTracker tracker, Job newJob, JobCondition previousCondition,
            ThinkNode jobGiver, ThinkTreeDef thinkTree, JobTag? tag, bool fromQueue, bool after)
        {
            Pawn pawn = PawnFrom(tracker);
            if (!IsTarget(pawn))
            {
                return;
            }

            if (newJob?.def == JobDefOf.Wait_Combat && jobGiver is JobGiver_Orders && !after)
            {
                WriteTreeProbe(pawn, thinkTree);
            }

            string phase = after ? "post" : "pre";
            Write(pawn, "StartJob " + phase + " new=" + Describe(newJob) +
                " previousCondition=" + previousCondition +
                " jobGiver=" + DescribeThinkNode(jobGiver) +
                " thinkTree=" + (thinkTree?.defName ?? "<null>") +
                " tag=" + (tag?.ToString() ?? "<null>") +
                " fromQueue=" + fromQueue +
                " current=" + Describe(pawn.CurJob) +
                " toil=" + DescribeToil(pawn.jobs?.curDriver));
        }

        internal static void ConstantTreeCondition(Pawn pawn, ThinkNode node, bool? result)
        {
            if (!IsTarget(pawn))
            {
                return;
            }

            if (!result.HasValue || !TreeConditionsWritten.Add(pawn.ThingID))
            {
                return;
            }

            Write(pawn, "ConstantTreeCondition result=" + result.Value +
                " node=" + DescribeThinkNode(node) +
                " constantTree=" + (pawn.thinker?.ConstantThinkTree?.defName ?? "<null>") +
                " mentalState=" + pawn.InMentalState +
                " downed=" + pawn.Downed +
                " burning=" + pawn.IsBurning() +
                " drafted=" + pawn.Drafted +
                " awake=" + pawn.Awake());
        }

        internal static void JobEnd(Pawn_JobTracker tracker, JobCondition condition)
        {
            Pawn pawn = PawnFrom(tracker);
            if (!IsTarget(pawn))
            {
                return;
            }

            Write(pawn, "EndCurrentJob condition=" + condition +
                " current=" + Describe(pawn.CurJob) +
                " toil=" + DescribeToil(pawn.jobs?.curDriver) +
                " pather=" + DescribePather(pawn));
        }

        internal static void JobSuspend(Pawn_JobTracker tracker, JobCondition condition)
        {
            Pawn pawn = PawnFrom(tracker);
            if (!IsTarget(pawn))
            {
                return;
            }

            Write(pawn, "SuspendCurrentJob condition=" + condition +
                " current=" + Describe(pawn.CurJob) +
                " toil=" + DescribeToil(pawn.jobs?.curDriver) +
                " pather=" + DescribePather(pawn));
        }

        internal static void DriverEnd(JobDriver driver, JobCondition condition)
        {
            Pawn pawn = driver?.pawn;
            if (!IsTarget(pawn))
            {
                return;
            }

            Write(pawn, "EndJobWith condition=" + condition +
                " driverJob=" + Describe(driver.job) +
                " toil=" + DescribeToil(driver) +
                " pather=" + DescribePather(pawn));
        }

        internal static void Cleanup(Pawn_JobTracker tracker, JobCondition condition)
        {
            Pawn pawn = PawnFrom(tracker);
            if (!IsTarget(pawn))
            {
                return;
            }

            Write(pawn, "CleanupCurrentJob condition=" + condition +
                " current=" + Describe(pawn.CurJob) +
                " toil=" + DescribeToil(pawn.jobs?.curDriver) +
                " pather=" + DescribePather(pawn));
        }

        internal static void PatherEvent(Pawn_PathFollower pather, string eventName)
        {
            Pawn pawn = PawnFrom(pather);
            if (!IsTarget(pawn))
            {
                return;
            }

            Write(pawn, eventName +
                " current=" + Describe(pawn.CurJob) +
                " toil=" + DescribeToil(pawn.jobs?.curDriver) +
                " pather=" + DescribePather(pawn));
        }

        private static bool IsTarget(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || !pawn.IsColonist || !pawn.Drafted)
            {
                return false;
            }

            string race = pawn.def?.defName ?? string.Empty;
            string body = pawn.RaceProps?.body?.defName ?? string.Empty;
            return race.IndexOf("Milira", StringComparison.OrdinalIgnoreCase) >= 0 ||
                race.IndexOf("Milian", StringComparison.OrdinalIgnoreCase) >= 0 ||
                body.IndexOf("Milira", StringComparison.OrdinalIgnoreCase) >= 0 ||
                body.IndexOf("Milian", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Describe(Job job)
        {
            if (job == null)
            {
                return "<null>";
            }

            return (job.def?.defName ?? "<no-def>") + "#" + job.loadID +
                " flying=" + job.flying + " playerForced=" + job.playerForced;
        }

        private static string DescribeToil(JobDriver driver)
        {
            return driver == null ? "<null>" : driver.GetType().Name + "[" + driver.CurToilIndex + "] " + driver.CurToilString;
        }

        private static string DescribeThinkNode(ThinkNode node)
        {
            if (node == null)
            {
                return "<null>";
            }

            string result = string.Empty;
            int count = 0;
            for (ThinkNode current = node; current != null && count < 8; current = current.parent)
            {
                if (count++ != 0)
                {
                    result += " <- ";
                }

                result += current.GetType().FullName;
            }

            return result;
        }

        private static void WriteTreeProbe(Pawn pawn, ThinkTreeDef thinkTree)
        {
            if (!TreeProbesWritten.Add(pawn.ThingID))
            {
                return;
            }

            ThinkNode root = pawn.thinker?.ConstantThinkNodeRoot;
            List<string> matches = new List<string>();
            int nodeCount = 0;
            CollectTreeMatches(root, "", matches, ref nodeCount);
            Write(pawn, "ConstantTreeProbe jobThinkTree=" + (thinkTree?.defName ?? "<null>") +
                " runtimeThinkTree=" + (pawn.thinker?.ConstantThinkTree?.defName ?? "<null>") +
                " root=" + (root?.GetType().FullName ?? "<null>") +
                " nodeCount=" + nodeCount +
                " matches=" + (matches.Count == 0 ? "<none>" : string.Join(" | ", matches)));
        }

        private static void CollectTreeMatches(ThinkNode node, string parentPath, List<string> matches,
            ref int nodeCount)
        {
            if (node == null || nodeCount >= 256)
            {
                return;
            }

            nodeCount++;
            string path = string.IsNullOrEmpty(parentPath)
                ? node.GetType().FullName
                : parentPath + " > " + node.GetType().FullName;

            if (node is JobGiver_Orders || node is ThinkNode_ConditionalCanDoConstantThinkTreeJobNow)
            {
                matches.Add(path + " [parent=" + DescribeThinkNode(node.parent) + "]");
            }

            foreach (ThinkNode child in node.subNodes)
            {
                CollectTreeMatches(child, path, matches, ref nodeCount);
            }
        }

        internal static string DescribePatches(MethodBase method)
        {
            if (method == null)
            {
                return "<method-not-found>";
            }

            Patches patches = Harmony.GetPatchInfo(method);
            if (patches == null)
            {
                return method.DeclaringType?.FullName + "." + method.Name + "=<none>";
            }

            List<string> entries = new List<string>();
            AddPatches(entries, patches.Prefixes, "prefix");
            AddPatches(entries, patches.Postfixes, "postfix");
            AddPatches(entries, patches.Transpilers, "transpiler");
            AddPatches(entries, patches.Finalizers, "finalizer");
            return method.DeclaringType?.FullName + "." + method.Name + "=" +
                (entries.Count == 0 ? "<none>" : string.Join(",", entries));
        }

        private static void AddPatches(List<string> entries, IEnumerable<Patch> patches, string kind)
        {
            foreach (Patch patch in patches)
            {
                entries.Add(kind + ":" + patch.owner + ":" +
                    (patch.PatchMethod?.DeclaringType?.FullName ?? "<unknown>"));
            }
        }

        private static string DescribePather(Pawn pawn)
        {
            if (pawn?.pather == null)
            {
                return "<null>";
            }

            return "moving=" + pawn.pather.Moving + " destination=" + pawn.pather.Destination +
                " flying=" + pawn.Flying;
        }

        private static void Write(Pawn pawn, string message)
        {
            string stack = CallerStack();
            Log.Message("[MiliraJobTrace] tick=" + GenTicks.TicksGame +
                " pawn=" + pawn.ThingID + " (" + pawn.ToString() + ")" +
                " drafted=" + pawn.Drafted + " pos=" + pawn.Position +
                " " + message + " caller=" + stack);
        }

        private static string CallerStack()
        {
            StackTrace trace = new StackTrace(2, false);
            StackFrame[] frames = trace.GetFrames();
            if (frames == null)
            {
                return "<none>";
            }

            string result = string.Empty;
            int count = 0;
            foreach (StackFrame frame in frames)
            {
                MethodBase method = frame.GetMethod();
                if (method == null || method.DeclaringType == typeof(TraceLog))
                {
                    continue;
                }

                if (count++ != 0)
                {
                    result += " <- ";
                }

                result += (method.DeclaringType?.FullName ?? "<unknown>") + "." + method.Name;
                if (count == 8)
                {
                    break;
                }
            }

            return string.IsNullOrEmpty(result) ? "<none>" : result;
        }
    }

    [HarmonyPatch(typeof(ThinkNode_ConditionalCanDoConstantThinkTreeJobNow), "Satisfied")]
    internal static class ConstantTreeConditionPatch
    {
        private static void Postfix(Pawn pawn, ThinkNode_ConditionalCanDoConstantThinkTreeJobNow __instance,
            bool __result)
        {
            TraceLog.ConstantTreeCondition(pawn, __instance, __result);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class StartJobPatch
    {
        private static void Prefix(Pawn_JobTracker __instance, Job newJob, JobCondition lastJobEndCondition,
            ThinkNode jobGiver, ThinkTreeDef thinkTree, JobTag? tag, bool fromQueue)
        {
            TraceLog.JobStart(__instance, newJob, lastJobEndCondition, jobGiver, thinkTree, tag, fromQueue, after: false);
        }

        private static void Postfix(Pawn_JobTracker __instance, Job newJob, JobCondition lastJobEndCondition,
            ThinkNode jobGiver, ThinkTreeDef thinkTree, JobTag? tag, bool fromQueue)
        {
            TraceLog.JobStart(__instance, newJob, lastJobEndCondition, jobGiver, thinkTree, tag, fromQueue, after: true);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    internal static class EndCurrentJobPatch
    {
        private static void Prefix(Pawn_JobTracker __instance, JobCondition condition)
        {
            TraceLog.JobEnd(__instance, condition);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.SuspendCurrentJob))]
    internal static class SuspendCurrentJobPatch
    {
        private static void Prefix(Pawn_JobTracker __instance, JobCondition jobPauseReason)
        {
            TraceLog.JobSuspend(__instance, jobPauseReason);
        }
    }

    [HarmonyPatch]
    internal static class CleanupCurrentJobPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_JobTracker), "CleanupCurrentJob");
        }

        private static void Prefix(Pawn_JobTracker __instance, JobCondition condition)
        {
            TraceLog.Cleanup(__instance, condition);
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.EndJobWith))]
    internal static class EndJobWithPatch
    {
        private static void Prefix(JobDriver __instance, JobCondition condition)
        {
            TraceLog.DriverEnd(__instance, condition);
        }
    }

    [HarmonyPatch]
    internal static class PatherFailedPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_PathFollower), "PatherFailed");
        }

        private static void Prefix(Pawn_PathFollower __instance)
        {
            TraceLog.PatherEvent(__instance, "PatherFailed");
        }
    }

    [HarmonyPatch]
    internal static class PatherArrivedPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_PathFollower), "PatherArrived");
        }

        private static void Prefix(Pawn_PathFollower __instance)
        {
            TraceLog.PatherEvent(__instance, "PatherArrived");
        }
    }
}
