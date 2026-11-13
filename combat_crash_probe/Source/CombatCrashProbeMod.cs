using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace CombatCrashProbe;

public sealed class CombatCrashProbeMod : Mod
{
    public CombatCrashProbeMod(ModContentPack content) : base(content)
    {
        try
        {
            TraceWriter.Initialize(Path.Combine(GenFilePaths.SaveDataFolderPath, "CombatCrashProbe"));
            Log.Message("[CombatCrashProbe] Evidence: " + TraceWriter.LogPath);
            LongEventHandler.ExecuteWhenFinished(Install);
        }
        catch (Exception error)
        {
            Log.Error("[CombatCrashProbe] Initialization failed: " + error);
        }
    }

    private static void Install()
    {
        var harmony = new Harmony(ProbeRuntime.Owner);
        var seen = new HashSet<MethodBase>();
        foreach (var (typeName, names) in Targets)
        {
            Type? type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                TraceWriter.Write("UNAVAILABLE type=" + typeName);
                continue;
            }
            foreach (string name in names.Split(','))
            {
                MethodInfo[] methods = type.GetMethods(AccessTools.allDeclared)
                    .Where(m => m.Name == name && !m.IsAbstract && !m.ContainsGenericParameters).ToArray();
                if (methods.Length == 0) TraceWriter.Write("UNAVAILABLE method=" + typeName + "." + name);
                foreach (MethodInfo method in methods)
                {
                    if (!seen.Add(method)) continue;
                    try
                    {
                        ProbeRuntime.Register(method);
                        bool pawnArgument = method.GetParameters().FirstOrDefault()?.ParameterType == typeof(Pawn);
                        string prefix = pawnArgument ? nameof(ProbeRuntime.BeginWithPawn) : nameof(ProbeRuntime.Begin);
                        if (method.GetParameters().FirstOrDefault()?.ParameterType == typeof(Thing))
                            prefix = method.IsStatic ? nameof(ProbeRuntime.BeginStaticThing) : nameof(ProbeRuntime.BeginWithThing);
                        harmony.Patch(method,
                            prefix: new HarmonyMethod(typeof(ProbeRuntime), prefix) { priority = int.MaxValue },
                            postfix: typeName == "CM_No_Pawn_Left_Behind.JobGiver_Rescue" && name == "TryGiveJob"
                                ? new HarmonyMethod(typeof(ProbeRuntime), nameof(ProbeRuntime.RescueResult)) : null,
                            finalizer: new HarmonyMethod(typeof(ProbeRuntime), nameof(ProbeRuntime.End)) { priority = int.MinValue });
                        TraceWriter.Write("MONITOR " + ProbeRuntime.DescribeMethod(method));
                        TraceWriter.Write(ProbeRuntime.PatchOwners(method));
                    }
                    catch (Exception error)
                    {
                        TraceWriter.Write("PATCH FAILED " + method + " " + error);
                        Log.Warning("[CombatCrashProbe] Could not monitor " + typeName + "." + name);
                    }
                }
            }
        }
        Log.Message("[CombatCrashProbe] Monitoring " + seen.Count + " methods. Log: " + TraceWriter.LogPath);
    }

    private static readonly (string, string)[] Targets =
    {
        ("CM_No_Pawn_Left_Behind.JobGiver_Rescue", "TryGiveJob,TryFindRescuableAlly"),
        ("Verse.Thing", "TakeDamage,Kill,Destroy,get_MarketValue"),
        ("Verse.Pawn", "Kill,Destroy,DeSpawn,ExitMap,DropBeforeDying,DropAndForbidEverything,RemoveFromHoldingContainer"),
        ("Verse.Pawn_HealthTracker", "AddHediff,CheckForStateChange"),
        ("Verse.Pawn_CarryTracker", "TryStartCarry,TryDropCarriedThing"),
        ("Verse.AI.Pawn_JobTracker", "StartJob,EndCurrentJob,TryFindAndStartJob"),
        ("Verse.AI.ThinkNode_JobGiver", "TryIssueJobPackage"),
        ("Verse.AI.JobDriver", "EndJobWith"),
        ("RimWorld.Pawn_RelationsTracker", "OpinionOf"),
        ("RimWorld.StatExtension", "GetStatValue"),
        ("RimWorld.StatWorker", "GetValue"),
        ("RimWorld.JobDriver_TakeAndExitMap", "TryMakePreToilReservations"),
        ("RimWorld.Bullet", "Impact")
    };
}
