using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UIRootGuiTrace;

public sealed class UIRootGuiTraceMod : Mod
{
    public UIRootGuiTraceMod(ModContentPack content) : base(content)
    {
        TraceRuntime.Initialize();
    }
}

internal static class TraceRuntime
{
    private const string Owner = "nanaloveyuki.uirootguitrace";
    private const string LogPrefix = "[UIRootTrace]";
    private const int MaxFrameEvents = 80;
    private const int MaxProbeEvents = 512;
    private const long MaxProbeFileBytes = 2 * 1024 * 1024;
    private const long RetryRecacheAfterFrames = 120;

    private static readonly Harmony RuntimeHarmony = new Harmony(Owner);
    private static readonly Harmony ObserverHarmony = new Harmony(Owner + ".observer");
    private static readonly MethodBase UiRootOnGui = AccessTools.Method(typeof(UIRoot_Play), "UIRootOnGUI");
    private static readonly MethodBase ColonistBarOnGui = AccessTools.Method(typeof(ColonistBar), "ColonistBarOnGUI");
    private static readonly MethodBase CheckRecacheEntries = AccessTools.Method(typeof(ColonistBar), "CheckRecacheEntries");
    private static readonly MethodBase AddAndRemoveDynamicComponents = AccessTools.Method(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents");
    private static readonly MethodBase PawnSetFaction = AccessTools.Method(typeof(Pawn), "SetFaction", new[] { typeof(Faction), typeof(Pawn) });
    private static readonly MethodBase ThingSetFactionDirect = AccessTools.Method(typeof(Thing), "SetFactionDirect", new[] { typeof(Faction) });
    private static readonly MethodBase PawnDraftedSetter = AccessTools.PropertySetter(typeof(Pawn_DraftController), "Drafted");
    private static readonly MethodBase PawnSpawnSetup = AccessTools.Method(typeof(Pawn), "SpawnSetup", new[] { typeof(Map), typeof(bool) });
    private static readonly MethodBase PawnDeSpawn = AccessTools.Method(typeof(Pawn), "DeSpawn", new[] { typeof(DestroyMode) });
    private static readonly MethodBase PawnExposeData = AccessTools.Method(typeof(Pawn), "ExposeData");
    private static readonly MethodBase PawnTickInterval = AccessTools.Method(typeof(Pawn), "TickInterval", new[] { typeof(int) });
    private static readonly FieldInfo EntriesDirtyField = AccessTools.Field(typeof(ColonistBar), "entriesDirty");
    private static readonly FieldInfo CachedEntriesField = AccessTools.Field(typeof(ColonistBar), "cachedEntries");
    private static readonly FieldInfo CachedDrawLocsField = AccessTools.Field(typeof(ColonistBar), "cachedDrawLocs");
    private static readonly FieldInfo CachedReorderableGroupsField = AccessTools.Field(typeof(ColonistBar), "cachedReorderableGroups");
    private static readonly FieldInfo CachedScaleField = AccessTools.Field(typeof(ColonistBar), "cachedScale");
    private static readonly FieldInfo DrawLocsFinderField = AccessTools.Field(typeof(ColonistBar), "drawLocsFinder");
    private static readonly FieldInfo TemporaryPawnsField = AccessTools.Field(typeof(ColonistBar), "tmpPawns");
    private static readonly FieldInfo TemporaryMapsField = AccessTools.Field(typeof(ColonistBar), "tmpMaps");
    private static readonly FieldInfo TemporaryCaravansField = AccessTools.Field(typeof(ColonistBar), "tmpCaravans");
    private static readonly MethodInfo CalculateDrawLocsMethod = AccessTools.Method(typeof(ColonistBarDrawLocsFinder), "CalculateDrawLocs");
    private static readonly HashSet<MethodBase> InstrumentedMethods = new HashSet<MethodBase>();
    private static readonly Dictionary<MethodBase, List<PatchSource>> PatchSources = new Dictionary<MethodBase, List<PatchSource>>();
    private static readonly HashSet<string> ReportedFailures = new HashSet<string>();
    private static readonly Dictionary<int, bool> LastPawnSettingsState = new Dictionary<int, bool>();
    private static readonly Dictionary<ColonistBar, ColonistBarCache> LastValidColonistBarCache = new Dictionary<ColonistBar, ColonistBarCache>();
    private static readonly Dictionary<ColonistBar, long> RetryCacheAtFrame = new Dictionary<ColonistBar, long>();
    private static readonly Queue<string> ProbeHistory = new Queue<string>();
    private static readonly object Gate = new object();

    [ThreadStatic]
    private static TraceFrame currentFrame;

    [ThreadStatic]
    private static ColonistBarGuiState currentColonistBarGui;

    [ThreadStatic]
    private static PawnSettingsTraceState currentPawnLifecycle;

    private static long nextFrameId;
    private static string probePath;

    internal static void Initialize()
    {
        if (UiRootOnGui == null)
        {
            Log.Error(LogPrefix + " Could not find UIRoot_Play.UIRootOnGUI.");
            return;
        }

        RuntimeHarmony.Patch(
            UiRootOnGui,
            prefix: new HarmonyMethod(typeof(TraceRuntime), nameof(BeginUiRoot)),
            finalizer: new HarmonyMethod(typeof(TraceRuntime), nameof(FinishUiRoot)));

        if (ColonistBarOnGui == null || CheckRecacheEntries == null)
        {
            Log.Warning(LogPrefix + " Could not find ColonistBar diagnostic methods.");
        }
        else
        {
            RuntimeHarmony.Patch(
                ColonistBarOnGui,
                prefix: new HarmonyMethod(typeof(TraceRuntime), nameof(BeginColonistBarGui)),
                transpiler: new HarmonyMethod(typeof(TraceRuntime), nameof(ColonistBarOnGuiTranspiler)),
                finalizer: new HarmonyMethod(typeof(TraceRuntime), nameof(FinishColonistBarGui)));
            RuntimeHarmony.Patch(
                CheckRecacheEntries,
                prefix: new HarmonyMethod(typeof(TraceRuntime), nameof(BeginColonistBarRecache)),
                finalizer: new HarmonyMethod(typeof(TraceRuntime), nameof(FinishColonistBarRecache)));
        }

        PatchPawnLifecycle(AddAndRemoveDynamicComponents, "PawnComponentsUtility.AddAndRemoveDynamicComponents");
        PatchPawnLifecycle(PawnSetFaction, "Pawn.SetFaction");
        PatchPawnLifecycle(ThingSetFactionDirect, "Thing.SetFactionDirect");
        PatchPawnLifecycle(PawnDraftedSetter, "Pawn_DraftController.Drafted");
        PatchPawnLifecycle(PawnSpawnSetup, "Pawn.SpawnSetup");
        PatchPawnLifecycle(PawnDeSpawn, "Pawn.DeSpawn");
        PatchPawnLifecycle(PawnExposeData, "Pawn.ExposeData");
        PatchPawnLifecycle(PawnTickInterval, "Pawn.TickInterval");

        LongEventHandler.ExecuteWhenFinished(InstrumentCurrentPatches);
        Log.Message(LogPrefix + " Loaded; waiting for UI root callbacks to be available.");
    }

    private static void PatchPawnLifecycle(MethodBase method, string label)
    {
        if (method == null)
        {
            Log.Warning(LogPrefix + " Could not find " + label + " for pawn component tracing.");
            return;
        }

        RuntimeHarmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(TraceRuntime), nameof(BeginPawnLifecycle)),
            finalizer: new HarmonyMethod(typeof(TraceRuntime), nameof(FinishPawnLifecycle)));
    }

    private static void BeginPawnLifecycle(MethodBase __originalMethod, object __instance, object[] __args, out PawnSettingsTraceState __state)
    {
        Pawn pawn = __instance as Pawn;
        if (pawn == null && __instance is Pawn_DraftController draftController)
        {
            pawn = draftController.pawn;
        }
        if (pawn == null && __args != null && __args.Length > 0)
        {
            pawn = __args[0] as Pawn;
        }

        if (!IsMiliraPawn(pawn))
        {
            __state = null;
            return;
        }

        __state = new PawnSettingsTraceState(pawn, pawn.playerSettings == null, pawn.drafter?.Drafted ?? false, pawn.Faction, Scribe.mode.ToString(), currentPawnLifecycle);
        currentPawnLifecycle = __state;
    }

    private static Exception FinishPawnLifecycle(MethodBase __originalMethod, PawnSettingsTraceState __state, Exception __exception)
    {
        if (__state == null)
        {
            return __exception;
        }

        try
        {
            Pawn pawn = __state.Pawn;
            bool settingsNull = pawn.playerSettings == null;
            bool drafted = pawn.drafter?.Drafted ?? false;
            Faction faction = pawn.Faction;
            bool factionChanged = !ReferenceEquals(__state.FactionBefore, faction);
            bool draftChanged = __state.DraftedBefore != drafted;
            bool observedChange = ObservePawnSettingsState(pawn.thingIDNumber, settingsNull, out bool firstObservation);
            bool exposeData = __originalMethod == PawnExposeData;
            bool relevantPawn = pawn.thingIDNumber == 1206 || faction?.IsPlayer == true || __state.FactionBefore?.IsPlayer == true;
            if (relevantPawn && (__state.SettingsNullBefore != settingsNull || factionChanged || draftChanged || observedChange || (firstObservation && settingsNull) || exposeData || __exception != null))
            {
                string stage = __originalMethod.Name + (exposeData ? " mode=" + __state.ScribeMode : string.Empty);
                Log.Warning(LogPrefix + " pawn component state stage=" + stage
                    + " before={" + PawnState(__state.Pawn, __state.SettingsNullBefore, __state.FactionBefore) + "}"
                    + " after={" + PawnState(pawn, settingsNull, faction) + "}"
                    + (observedChange ? " observedTransition=True" : string.Empty)
                    + (draftChanged ? " draftedTransition=True" : string.Empty)
                    + " patches=" + DescribePatches(__originalMethod)
                    + (__exception == null ? string.Empty : " exception=" + __exception.GetType().Name + ": " + __exception.Message)
                    + Environment.NewLine + new StackTrace(1, true));
            }
        }
        catch (Exception traceException)
        {
            Log.Error(LogPrefix + " Pawn lifecycle trace failed for " + __originalMethod + ": " + traceException);
        }
        finally
        {
            currentPawnLifecycle = __state.Previous;
        }

        return __exception;
    }

    private static bool IsMiliraPawn(Pawn pawn)
    {
        return pawn?.def?.defName?.StartsWith("Milira_", StringComparison.Ordinal) == true;
    }

    private static bool ObservePawnSettingsState(int pawnId, bool settingsNull, out bool firstObservation)
    {
        firstObservation = !LastPawnSettingsState.TryGetValue(pawnId, out bool previous);
        LastPawnSettingsState[pawnId] = settingsNull;
        return !firstObservation && previous != settingsNull;
    }

    private static string PawnState(Pawn pawn, bool settingsNull, Faction faction)
    {
        int tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
        return "tick=" + tick
            + " pawn#" + pawn.thingIDNumber
            + " def=" + pawn.def?.defName
            + " faction=" + (faction?.def?.defName ?? "null")
            + " playerFaction=" + (faction?.IsPlayer ?? false)
            + " spawned=" + pawn.Spawned
            + " dead=" + pawn.Dead
            + " drafted=" + (pawn.drafter?.Drafted ?? false)
            + " holder=" + (pawn.ParentHolder?.GetType().Name ?? "null")
            + " playerSettings=" + !settingsNull;
    }

    private static string DescribePatches(MethodBase method)
    {
        Patches patches = Harmony.GetPatchInfo(method);
        if (patches == null)
        {
            return "[]";
        }

        List<string> labels = new List<string>();
        AddPatchLabels(labels, patches.Prefixes, "pre");
        AddPatchLabels(labels, patches.Postfixes, "post");
        AddPatchLabels(labels, patches.Transpilers, "trans");
        AddPatchLabels(labels, patches.Finalizers, "final");
        return "[" + string.Join(",", labels) + "]";
    }

    private static void AddPatchLabels(List<string> labels, IEnumerable<Patch> patches, string kind)
    {
        if (patches == null)
        {
            return;
        }

        foreach (Patch patch in patches)
        {
            MethodInfo method = patch?.PatchMethod;
            if (method != null)
            {
                labels.Add(kind + ":" + patch.owner + "/" + method.DeclaringType?.Name + "." + method.Name);
            }
        }
    }

    private static void BeginUiRoot(out TraceFrame __state)
    {
        __state = new TraceFrame(Interlocked.Increment(ref nextFrameId), currentFrame);
        currentFrame = __state;
    }

    private static Exception FinishUiRoot(Exception __exception, TraceFrame __state)
    {
        try
        {
            InstrumentCurrentPatches();
            if (__exception != null)
            {
                string callbackTrace = __state?.Describe() ?? "trace frame unavailable";
                string key = FailureKey(__exception) + "|" + (__state?.EventSequence() ?? "trace frame unavailable");
                if (MarkFailureReported(key))
                {
                    Log.Warning(LogPrefix + " UIRoot_Play.UIRootOnGUI threw an exception. " + callbackTrace + Environment.NewLine + __exception);
                }
            }
        }
        catch (Exception traceException)
        {
            Log.Error(LogPrefix + " Trace finalizer failed: " + traceException);
        }
        finally
        {
            if (__state != null)
            {
                currentFrame = __state.Previous;
            }
        }

        return __exception;
    }

    private static void BeginColonistBarRecache(ColonistBar __instance, out string __state)
    {
        __state = BuildColonistBarSnapshot(__instance);
        TraceFrame frame = currentFrame;
        if (frame != null)
        {
            frame.Record("ColonistBar.CheckRecacheEntries enter event=" + CurrentEventType() + " " + __state);
        }
    }

    private static Exception FinishColonistBarRecache(ColonistBar __instance, Exception __exception, string __state)
    {
        if (__exception == null)
        {
            return null;
        }

        try
        {
            string snapshot = "before={" + (__state ?? "unavailable") + "} after={" + BuildColonistBarSnapshot(__instance) + "}";
            currentFrame?.Record("ColonistBar.CheckRecacheEntries threw event=" + CurrentEventType() + " " + snapshot);
            LogColonistBarFailure("CheckRecacheEntries", __instance, snapshot, __exception);
        }
        catch (Exception traceException)
        {
            Log.Error(LogPrefix + " CheckRecacheEntries finalizer failed: " + traceException);
        }

        return __exception;
    }

    private static void BeginColonistBarGui(ColonistBar __instance, out ColonistBarGuiState __state)
    {
        string before = BuildColonistBarSnapshot(__instance);
        __state = new ColonistBarGuiState(__instance, currentColonistBarGui, currentFrame, before);
        currentColonistBarGui = __state;
        __state.Frame?.Record("ColonistBarOnGUI enter event=" + CurrentEventType() + " " + before);
    }

    private static Exception FinishColonistBarGui(ColonistBar __instance, Exception __exception, ColonistBarGuiState __state)
    {
        try
        {
            if (__exception != null)
            {
                string snapshot = "before={" + (__state?.InitialSnapshot ?? "unavailable") + "} after={" + BuildColonistBarSnapshot(__instance) + "}";
                string phases = __state?.Describe() ?? "phase trace unavailable";
                __state?.Frame?.Record("ColonistBarOnGUI threw event=" + CurrentEventType() + " " + snapshot);
                LogColonistBarFailure("ColonistBarOnGUI " + phases, __instance, snapshot, __exception);
            }
        }
        catch (Exception traceException)
        {
            Log.Error(LogPrefix + " ColonistBarOnGUI finalizer failed: " + traceException);
        }
        finally
        {
            if (__state != null)
            {
                currentColonistBarGui = __state.Previous;
            }
        }

        return __exception;
    }

    private static IEnumerable<CodeInstruction> ColonistBarOnGuiTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        int entryIndexers = 0;
        MethodInfo recordPhase = AccessTools.Method(typeof(TraceRuntime), nameof(RecordColonistBarPhase));
        foreach (CodeInstruction instruction in instructions)
        {
            if (IsColonistBarEntryIndexer(instruction))
            {
                entryIndexers++;
                int phase = entryIndexers == 1 ? 1 : 2;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldc_I4, phase);
                yield return new CodeInstruction(OpCodes.Call, recordPhase);
            }

            yield return instruction;
        }

        if (entryIndexers != 2)
        {
            Log.Warning(LogPrefix + " Expected two ColonistBar.Entry indexers in ColonistBarOnGUI, found " + entryIndexers + ". Phase markers may be incomplete.");
        }
    }

    private static bool IsColonistBarEntryIndexer(CodeInstruction instruction)
    {
        MethodInfo method = instruction.operand as MethodInfo;
        Type declaringType = method?.DeclaringType;
        return instruction.opcode == OpCodes.Callvirt
            && method?.Name == "get_Item"
            && declaringType != null
            && declaringType.IsGenericType
            && declaringType.GetGenericTypeDefinition() == typeof(List<>)
            && declaringType.GetGenericArguments()[0] == typeof(ColonistBar.Entry);
    }

    private static void RecordColonistBarPhase(ColonistBar colonistBar, int phase)
    {
        ColonistBarGuiState state = currentColonistBarGui;
        if (state == null || state.ColonistBar != colonistBar || state.Frame == null)
        {
            return;
        }

        if (phase == 1 && !state.FirstDrawLoopRecorded)
        {
            state.FirstDrawLoopRecorded = true;
            state.Frame.Record("ColonistBar first draw loop event=" + CurrentEventType() + " " + BuildColonistBarSnapshot(colonistBar));
        }
        else if (phase == 2 && !state.GroupFrameLoopRecorded)
        {
            state.GroupFrameLoopRecorded = true;
            state.Frame.Record("ColonistBar group-frame loop (after first draw loop) event=" + CurrentEventType() + " " + BuildColonistBarSnapshot(colonistBar));
        }
    }

    private static void LogColonistBarFailure(string stage, ColonistBar colonistBar, string snapshot, Exception exception)
    {
        string key = "colonistbar|" + stage + "|" + FailureKey(exception);
        if (MarkFailureReported(key))
        {
            snapshot += EntryGroups(colonistBar) + MapInputIssues();
            Log.Warning(LogPrefix + " " + stage + " failed event=" + CurrentEventType() + " " + snapshot + Environment.NewLine + exception);
        }
    }

    private static string EntryGroups(ColonistBar colonistBar)
    {
        try
        {
            object value = CachedEntriesField?.GetValue(colonistBar);
            if (value is not IList entries || entries.Count == 0)
            {
                return string.Empty;
            }

            FieldInfo groupField = AccessTools.Field(typeof(ColonistBar.Entry), "group");
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < entries.Count; i++)
            {
                object entry = entries[i];
                if (entry == null || groupField == null)
                {
                    continue;
                }

                int group = (int)groupField.GetValue(entry);
                counts[group] = counts.TryGetValue(group, out int count) ? count + 1 : 1;
            }

            List<string> groups = new List<string>(counts.Count);
            foreach (KeyValuePair<int, int> pair in counts)
            {
                groups.Add(pair.Key + ":" + pair.Value);
            }

            return groups.Count == 0 ? string.Empty : " entryGroups=[" + string.Join(",", groups) + "]";
        }
        catch (Exception exception)
        {
            return " entryGroups=unavailable(" + exception.GetType().Name + ")";
        }
    }

    private static string MapInputIssues()
    {
        try
        {
            object value = TemporaryMapsField?.GetValue(null);
            if (value is not IList maps || maps.Count == 0)
            {
                return string.Empty;
            }

            List<string> issues = new List<string>();
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                if (maps[mapIndex] is not Map map)
                {
                    issues.Add("map[" + mapIndex + "]=" + (maps[mapIndex] == null ? "null" : maps[mapIndex].GetType().Name));
                    continue;
                }

                List<string> mapIssues = new List<string>();
                if (map.mapPawns == null)
                {
                    mapIssues.Add("mapPawns=null");
                }
                else
                {
                    IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
                    for (int pawnIndex = 0; pawnIndex < spawned.Count; pawnIndex++)
                    {
                        Pawn pawn = spawned[pawnIndex];
                        if (pawn == null)
                        {
                            mapIssues.Add("spawned[" + pawnIndex + "]=null");
                            continue;
                        }

                        if (pawn.carryTracker == null)
                        {
                            mapIssues.Add("spawned[" + pawnIndex + "]=" + pawn.thingIDNumber + ":carryTracker=null");
                            continue;
                        }

                        if (pawn.carryTracker.CarriedThing is Corpse carriedCorpse && carriedCorpse.InnerPawn == null)
                        {
                            mapIssues.Add("spawned[" + pawnIndex + "]=" + pawn.thingIDNumber + ":carriedCorpse.InnerPawn=null");
                        }
                    }
                }

                if (map.listerThings == null)
                {
                    mapIssues.Add("listerThings=null");
                }
                else
                {
                    List<Thing> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
                    if (corpses == null)
                    {
                        mapIssues.Add("corpseList=null");
                    }
                    else
                    {
                        for (int corpseIndex = 0; corpseIndex < corpses.Count; corpseIndex++)
                        {
                            Thing corpseThing = corpses[corpseIndex];
                            if (corpseThing == null)
                            {
                                mapIssues.Add("corpse[" + corpseIndex + "]=null");
                            }
                            else if (corpseThing is Corpse corpse && corpse.InnerPawn == null)
                            {
                                mapIssues.Add("corpse[" + corpseIndex + "].InnerPawn=null");
                            }
                        }
                    }
                }

                if (mapIssues.Count > 0)
                {
                    issues.Add("map#" + map.uniqueID + "{" + string.Join(",", mapIssues) + "}");
                }
            }

            return issues.Count == 0 ? string.Empty : " mapInputIssues=[" + string.Join(";", issues) + "]";
        }
        catch (Exception exception)
        {
            return " mapInputCheck=failed(" + exception.GetType().Name + ":" + exception.Message + ")";
        }
    }

    private static string BuildColonistBarSnapshot(ColonistBar colonistBar)
    {
        try
        {
            if (colonistBar == null)
            {
                return "colonistBar=null";
            }

            object temporaryPawns = TemporaryPawnsField?.GetValue(null);
            object temporaryMaps = TemporaryMapsField?.GetValue(null);
            object temporaryCaravans = TemporaryCaravansField?.GetValue(null);
            return "dirty=" + ReadField(EntriesDirtyField, colonistBar)
                + " entries=" + CollectionCount(CachedEntriesField?.GetValue(colonistBar))
                + " drawLocs=" + CollectionCount(CachedDrawLocsField?.GetValue(colonistBar))
                + " reorderableGroups=" + CollectionCount(CachedReorderableGroupsField?.GetValue(colonistBar))
                + " tmpPawns=" + CollectionCount(temporaryPawns) + PawnIssues(temporaryPawns)
                + " tmpMaps=" + CollectionCount(temporaryMaps) + NullIndexes(temporaryMaps)
                + " tmpCaravans=" + CollectionCount(temporaryCaravans) + NullIndexes(temporaryCaravans);
        }
        catch (Exception exception)
        {
            return "cache snapshot failed: " + exception.GetType().Name + ": " + exception.Message;
        }
    }

    private static object ReadField(FieldInfo field, object instance)
    {
        return field?.GetValue(instance) ?? "unavailable";
    }

    private static int CollectionCount(object value)
    {
        return value is ICollection collection ? collection.Count : -1;
    }

    private static string PawnIssues(object value)
    {
        if (value is not IList list || list.Count == 0)
        {
            return string.Empty;
        }

        List<string> issues = new List<string>();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is not Pawn pawn)
            {
                if (list[i] == null)
                {
                    issues.Add(i + "=null");
                }
                continue;
            }

            if (pawn.playerSettings == null || pawn.carryTracker == null)
            {
                issues.Add(i + "=pawn#" + pawn.thingIDNumber + "(playerSettings=" + (pawn.playerSettings != null) + ",carryTracker=" + (pawn.carryTracker != null) + ")");
            }
        }

        return issues.Count == 0 ? string.Empty : " pawnIssues=[" + string.Join(",", issues) + "]";
    }

    private static string NullIndexes(object value)
    {
        if (value is not IList list || list.Count == 0)
        {
            return string.Empty;
        }

        List<int> indexes = new List<int>();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null)
            {
                indexes.Add(i);
            }
        }

        return indexes.Count == 0 ? string.Empty : " nullIndexes=[" + string.Join(",", indexes) + "]";
    }

    private static string CurrentEventType()
    {
        try
        {
            return Event.current == null ? "null" : Event.current.type.ToString();
        }
        catch (Exception exception)
        {
            return "unavailable(" + exception.GetType().Name + ")";
        }
    }

    private static void InstrumentCurrentPatches()
    {
        if (UiRootOnGui == null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                Patches info = Harmony.GetPatchInfo(UiRootOnGui);
                if (info == null)
                {
                    return;
                }

                int added = 0;
                added += Instrument(info.Prefixes, "prefix");
                added += Instrument(info.Postfixes, "postfix");
                added += Instrument(info.Finalizers, "finalizer");
                added += Instrument(info.InnerPrefixes, "inner prefix");
                added += Instrument(info.InnerPostfixes, "inner postfix");
                added += Instrument(info.InnerFinalizers, "inner finalizer");

                if (added > 0)
                {
                    Log.Message(LogPrefix + " Instrumented " + added + " UI callback method(s).");
                }
            }
        }
        catch (Exception exception)
        {
            Log.Error(LogPrefix + " Could not inspect UI callbacks: " + exception);
        }
    }

    private static int Instrument(IEnumerable<Patch> patches, string callbackKind)
    {
        if (patches == null)
        {
            return 0;
        }

        int added = 0;
        foreach (Patch patch in patches)
        {
            MethodInfo patchMethod = patch?.PatchMethod;
            if (patchMethod == null || patch.owner == Owner || patch.owner == Owner + ".observer")
            {
                continue;
            }

            if (!PatchSources.TryGetValue(patchMethod, out List<PatchSource> sources))
            {
                sources = new List<PatchSource>();
                PatchSources.Add(patchMethod, sources);
            }

            PatchSource source = new PatchSource(patch.owner, callbackKind, patchMethod);
            if (!sources.Contains(source))
            {
                sources.Add(source);
            }

            if (!InstrumentedMethods.Add(patchMethod))
            {
                continue;
            }

            try
            {
                ObserverHarmony.Patch(
                    patchMethod,
                    prefix: new HarmonyMethod(typeof(PatchObserver), nameof(PatchObserver.Prefix)),
                    finalizer: new HarmonyMethod(typeof(PatchObserver), nameof(PatchObserver.Finalizer)));
                added++;
            }
            catch (Exception exception)
            {
                InstrumentedMethods.Remove(patchMethod);
                Log.Warning(LogPrefix + " Could not instrument " + source.Label + ": " + exception.Message);
            }
        }

        return added;
    }

    private static PatchSource[] SourcesFor(MethodBase method)
    {
        lock (Gate)
        {
            return PatchSources.TryGetValue(method, out List<PatchSource> sources)
                ? sources.ToArray()
                : Array.Empty<PatchSource>();
        }
    }

    private static bool MarkFailureReported(string key)
    {
        lock (Gate)
        {
            return ReportedFailures.Add(key);
        }
    }

    private static string FailureKey(Exception exception)
    {
        string firstStackLine = exception.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0] ?? "no stack";
        return exception.GetType().FullName + "|" + exception.Message + "|" + firstStackLine;
    }

    internal sealed class TraceFrame
    {
        private readonly Queue<string> events = new Queue<string>();

        internal TraceFrame(long id, TraceFrame previous)
        {
            Id = id;
            Previous = previous;
        }

        internal long Id { get; }
        internal TraceFrame Previous { get; }

        internal void Record(string entry)
        {
            if (events.Count == MaxFrameEvents)
            {
                events.Dequeue();
            }

            events.Enqueue(entry);
        }

        internal string Describe()
        {
            return "frame=" + Id + " callbacks=[" + EventSequence() + "]";
        }

        internal string EventSequence()
        {
            return string.Join(" | ", events);
        }
    }

    private sealed class ColonistBarGuiState
    {
        internal ColonistBarGuiState(ColonistBar colonistBar, ColonistBarGuiState previous, TraceFrame frame, string initialSnapshot)
        {
            ColonistBar = colonistBar;
            Previous = previous;
            Frame = frame;
            InitialSnapshot = initialSnapshot;
        }

        internal ColonistBar ColonistBar { get; }
        internal ColonistBarGuiState Previous { get; }
        internal TraceFrame Frame { get; }
        internal string InitialSnapshot { get; }
        internal bool FirstDrawLoopRecorded { get; set; }
        internal bool GroupFrameLoopRecorded { get; set; }

        internal string Describe()
        {
            return "phases=" + (FirstDrawLoopRecorded ? "first-draw" : "no-draw-loop")
                + "," + (GroupFrameLoopRecorded ? "group-frame" : "no-group-frame-loop");
        }
    }

    private sealed class PawnSettingsTraceState
    {
        internal PawnSettingsTraceState(Pawn pawn, bool settingsNullBefore, bool draftedBefore, Faction factionBefore, string scribeMode, PawnSettingsTraceState previous)
        {
            Pawn = pawn;
            SettingsNullBefore = settingsNullBefore;
            DraftedBefore = draftedBefore;
            FactionBefore = factionBefore;
            ScribeMode = scribeMode;
            Previous = previous;
        }

        internal Pawn Pawn { get; }
        internal bool SettingsNullBefore { get; }
        internal bool DraftedBefore { get; }
        internal Faction FactionBefore { get; }
        internal string ScribeMode { get; }
        internal PawnSettingsTraceState Previous { get; }
    }

    internal sealed class PatchSource : IEquatable<PatchSource>
    {
        internal PatchSource(string owner, string callbackKind, MethodInfo method)
        {
            Owner = owner ?? "unknown";
            CallbackKind = callbackKind;
            Method = method;
            Label = Owner + " " + method.DeclaringType?.FullName + "." + method.Name + " (" + callbackKind + ")";
        }

        internal string Owner { get; }
        internal string CallbackKind { get; }
        internal MethodInfo Method { get; }
        internal string Label { get; }

        public bool Equals(PatchSource other)
        {
            return other != null && Owner == other.Owner && CallbackKind == other.CallbackKind && Method == other.Method;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PatchSource);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Owner, CallbackKind, Method);
        }
    }

    internal sealed class CallbackState
    {
        internal CallbackState(TraceFrame frame, PatchSource[] sources)
        {
            Frame = frame;
            Sources = sources;
        }

        internal TraceFrame Frame { get; }
        internal PatchSource[] Sources { get; }
    }

    private static class PatchObserver
    {
        internal static void Prefix(MethodBase __originalMethod, out CallbackState __state)
        {
            PatchSource[] sources = SourcesFor(__originalMethod);
            TraceFrame frame = currentFrame;
            __state = new CallbackState(frame, sources);
            if (frame == null)
            {
                return;
            }

            foreach (PatchSource source in sources)
            {
                frame.Record("enter " + source.Label);
            }
        }

        internal static Exception Finalizer(MethodBase __originalMethod, CallbackState __state, Exception __exception)
        {
            try
            {
                if (__state?.Frame != null)
                {
                    foreach (PatchSource source in __state.Sources)
                    {
                        __state.Frame.Record((__exception == null ? "exit " : "throw ") + source.Label);
                    }
                }

                if (__exception != null && __state?.Sources.Length > 0)
                {
                    string labels = string.Join("; ", Array.ConvertAll(__state.Sources, source => source.Label));
                    string key = "callback|" + labels + "|" + FailureKey(__exception);
                    if (MarkFailureReported(key))
                    {
                        Log.Warning(LogPrefix + " Harmony callback threw: " + labels + Environment.NewLine + __exception);
                    }
                }
            }
            catch (Exception traceException)
            {
                Log.Error(LogPrefix + " Callback observer failed for " + __originalMethod + ": " + traceException);
            }

            return __exception;
        }
    }
}
