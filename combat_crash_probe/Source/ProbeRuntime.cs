using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace CombatCrashProbe;

internal static class ProbeRuntime
{
    internal const string Owner = "nanaloveyuki.combatcrashprobe";
    private const int FrameLimit = 512;
    private const int HistoryLimit = 96;
    private static readonly Dictionary<MethodBase, Target> Targets = new();
    private static readonly FieldInfo? MapIndex = AccessTools.Field(typeof(Thing), "mapIndexOrState");
    [ThreadStatic] private static ThreadTrace? current;
    [ThreadStatic] private static bool writing;

    internal static void Register(MethodBase method)
    {
        bool important = method.Name is "Kill" or "Destroy" or "ExitMap" or "TryStartCarry" or "TryDropCarriedThing";
        bool nestedOnly = method.DeclaringType?.FullName is "RimWorld.StatExtension" or "RimWorld.StatWorker";
        Targets[method] = new Target(method, AccessTools.Field(method.DeclaringType, "pawn"),
            AccessTools.Field(method.DeclaringType, "stat"), important, nestedOnly);
    }

    internal static void Begin(MethodBase __originalMethod, object __instance, out int __state)
        => Enter(__originalMethod, __instance, null, out __state);

    internal static void BeginWithPawn(MethodBase __originalMethod, object __instance, Pawn __0, out int __state)
        => Enter(__originalMethod, __instance, __0, out __state);

    internal static void BeginWithThing(MethodBase __originalMethod, object __instance, Thing __0, out int __state)
        => Enter(__originalMethod, __instance, __0 as Pawn, out __state);

    internal static void BeginStaticThing(MethodBase __originalMethod, Thing __0, out int __state)
        => Enter(__originalMethod, null, __0 as Pawn, out __state);

    private static void Enter(MethodBase method, object? instance, Pawn? pawn, out int state)
    {
        state = 0;
        if (writing) return;
        try
        {
            Target target = Targets[method];
            if (target.NestedOnly && (current == null || current.Depth == 0)) return;
            ThreadTrace trace = current ??= new ThreadTrace();
            if (pawn == null && instance is Pawn_CarryTracker carry) pawn = carry.CarriedThing as Pawn;
            var frame = new Frame(target, instance, pawn);
            state = ++trace.Depth;
            if (state <= FrameLimit) trace.Frames[state - 1] = frame;
            trace.History[trace.Events++ % HistoryLimit] = new Event(frame, state, true);
            if (state is 16 or 32 or 64 or 128 or 256 or 512)
                Snapshot(trace, "NESTING depth=" + state);
            else if (IsImportant(frame) && state < 16)
                WriteGuarded("ENTER depth=" + state + " " + DescribeFrame(frame));
        }
        catch (Exception) { }
    }

    internal static Exception? End(int __state, Exception? __exception)
    {
        if (__state == 0 || current == null) return __exception;
        ThreadTrace trace = current;
        try
        {
            int index = __state - 1;
            if (index < FrameLimit)
            {
                Frame frame = trace.Frames[index];
                trace.History[trace.Events++ % HistoryLimit] = new Event(frame, __state, false);
                if (__exception != null && trace.Failures++ < 8)
                    Snapshot(trace, "EXCEPTION " + __exception);
                else if (IsImportant(frame) && __state < 16)
                    WriteGuarded("EXIT depth=" + __state + " " + DescribeFrame(frame));
                trace.Frames[index] = default;
            }
        }
        catch (Exception) { }
        finally
        {
            trace.Depth = __state - 1;
            if (trace.Depth == 0) trace.Failures = 0;
        }
        return __exception;
    }

    internal static void RescueResult(Pawn pawn, Job? __result)
    {
        if (writing || __result == null) return;
        writing = true;
        try
        {
            TraceWriter.Write("RESCUE_SELECTED rescuer=" + DescribePawn(pawn) +
                " target=" + DescribeThing(__result.targetA.Thing) + " exit=" + __result.targetB.Cell +
                " job=" + __result.def?.defName);
        }
        catch (Exception) { }
        finally { writing = false; }
    }

    private static void Snapshot(ThreadTrace trace, string reason)
    {
        if (writing) return;
        writing = true;
        try
        {
            var text = new StringBuilder();
            text.AppendLine(reason + " thread=" + Environment.CurrentManagedThreadId);
            text.AppendLine("ACTIVE CALLS");
            int count = Math.Min(trace.Depth, FrameLimit);
            for (int i = 0; i < count; i++) text.AppendLine(i + " " + DescribeFrame(trace.Frames[i]));
            text.AppendLine("RECENT ENTER/EXIT");
            long start = Math.Max(0, trace.Events - HistoryLimit);
            for (long i = start; i < trace.Events; i++)
            {
                Event item = trace.History[i % HistoryLimit];
                text.AppendLine(i + (item.Enter ? " > " : " < ") + item.Depth + " " + DescribeFrame(item.Frame));
            }
            text.AppendLine("CURRENT PATCH OWNERS");
            foreach (MethodBase method in trace.Frames.Take(count).Select(f => f.Target.Method).Distinct())
                text.AppendLine(PatchOwners(method));
            text.AppendLine("MANAGED STACK");
            text.AppendLine(new StackTrace(1, true).ToString());
            TraceWriter.Write(text.ToString());
            Log.Warning("[CombatCrashProbe] " + reason + "; stack and patch owners saved to " + TraceWriter.LogPath);
        }
        catch (Exception) { }
        finally { writing = false; }
    }

    private static void WriteGuarded(string text)
    {
        if (writing) return;
        writing = true;
        try { TraceWriter.Write("thread=" + Environment.CurrentManagedThreadId + " " + text); }
        finally { writing = false; }
    }

    private static string DescribeFrame(Frame frame)
    {
        Pawn? pawn = frame.Pawn ?? frame.Instance as Pawn ?? frame.Target.PawnField?.GetValue(frame.Instance) as Pawn;
        string context = pawn != null ? DescribePawn(pawn) : DescribeThing(frame.Instance as Thing);
        if (frame.Instance is Pawn_CarryTracker carry)
            context += " carrier=" + DescribePawn(carry.pawn) + " carried=" + DescribeThing(carry.CarriedThing);
        if (frame.Target.StatField?.GetValue(frame.Instance) is Def stat)
            context += " stat=" + stat.defName;
        return DescribeMethod(frame.Target.Method) + " " + context;
    }

    private static bool IsImportant(Frame frame)
        => frame.Target.Important && (frame.Instance is Pawn or Corpse ||
            frame.Instance is Pawn_CarryTracker && frame.Pawn != null);

    private static string DescribePawn(Pawn pawn)
    {
        // Avoid labels, stats and opinions: mods can recurse while computing those properties.
        object? owner = pawn.holdingOwner?.Owner;
        string holder = owner?.GetType().FullName ?? "none";
        if (owner is Pawn_CarryTracker carry) holder += ":pawn#" + carry.pawn.thingIDNumber;
        return DescribeThing(pawn) + " mapState=" + MapIndex?.GetValue(pawn) + " holder=" + holder +
            " job=" + pawn.CurJob?.def?.defName;
    }

    private static string DescribeThing(Thing? thing)
        => thing == null ? "none" : thing.GetType().Name + "#" + thing.thingIDNumber + ":" + thing.def?.defName;

    internal static string DescribeMethod(MethodBase method)
        => method.DeclaringType?.FullName + "." + method.Name + "(" +
            string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name)) + ")";

    internal static string PatchOwners(MethodBase method)
    {
        Patches? patches = Harmony.GetPatchInfo(method);
        var text = new StringBuilder("PATCHES " + DescribeMethod(method));
        if (patches == null) return text.Append(" none").ToString();
        Append("prefix", patches.Prefixes);
        Append("postfix", patches.Postfixes);
        Append("transpiler", patches.Transpilers);
        Append("finalizer", patches.Finalizers);
        return text.ToString();

        void Append(string kind, IEnumerable<Patch> entries)
        {
            foreach (Patch patch in entries)
                text.AppendLine().Append("  ").Append(kind).Append(" owner=").Append(patch.owner)
                    .Append(" priority=").Append(patch.priority).Append(" method=").Append(DescribeMethod(patch.PatchMethod))
                    .Append(" assembly=").Append(patch.PatchMethod.DeclaringType?.Assembly.Location);
        }
    }

    private sealed record Target(MethodBase Method, FieldInfo? PawnField, FieldInfo? StatField, bool Important, bool NestedOnly);
    private readonly record struct Frame(Target Target, object? Instance, Pawn? Pawn);
    private readonly record struct Event(Frame Frame, int Depth, bool Enter);
    private sealed class ThreadTrace
    {
        internal readonly Frame[] Frames = new Frame[FrameLimit];
        internal readonly Event[] History = new Event[HistoryLimit];
        internal int Depth;
        internal int Failures;
        internal long Events;
    }
}
