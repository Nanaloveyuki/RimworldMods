using System;
using HarmonyLib;
using Verse;

namespace AutoCutterIntervalFix;

public sealed class AutoCutterIntervalFixMod : Mod
{
    public AutoCutterIntervalFixMod(ModContentPack content) : base(content)
    {
        IntervalPatch.Install();
    }
}

internal static class IntervalPatch
{
    internal const string HarmonyId = "nanaloveyuki.autocutterintervalfix";
    private const int DefaultWorkTick = 60;

    internal static void Install()
    {
        Type cutter = AccessTools.TypeByName("LingItemCuter.Building_ItemCuter");
        if (cutter == null)
        {
            Log.Warning("[AutoCutterIntervalFix] Auto Cutter is not loaded; interval fix disabled.");
            return;
        }

        var interval = AccessTools.DeclaredField(cutter, "WorkTick");
        var tick = AccessTools.DeclaredMethod(cutter, "Tick", Type.EmptyTypes);
        var exposeData = AccessTools.DeclaredMethod(cutter, "ExposeData", Type.EmptyTypes);
        if (interval == null || interval.FieldType != typeof(int) || interval.IsStatic
            || tick == null || tick.ReturnType != typeof(void) || tick.IsStatic
            || exposeData == null || exposeData.ReturnType != typeof(void) || exposeData.IsStatic)
        {
            Log.Warning("[AutoCutterIntervalFix] Auto Cutter's interval API changed; interval fix disabled.");
            return;
        }

        try
        {
            var harmony = new Harmony(HarmonyId);
            harmony.Patch(tick, prefix: new HarmonyMethod(typeof(IntervalPatch), nameof(RepairInterval)));
            harmony.Patch(exposeData, postfix: new HarmonyMethod(typeof(IntervalPatch), nameof(RepairLoadedInterval)));
            Log.Message("[AutoCutterIntervalFix] Loaded: nonpositive work intervals recover to 60 ticks.");
        }
        catch (Exception exception)
        {
            Log.Error("[AutoCutterIntervalFix] Could not install interval fix: " + exception);
        }
    }

    private static void RepairLoadedInterval(ref int ___WorkTick)
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            RepairInterval(ref ___WorkTick);
    }

    private static void RepairInterval(ref int ___WorkTick)
    {
        // Older saves omit WorkTick; upstream's Scribe default overwrites the constructor's 60 with 0.
        if (___WorkTick <= 0)
            ___WorkTick = DefaultWorkTick;
    }
}
