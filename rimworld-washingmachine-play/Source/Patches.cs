using HarmonyLib;
using Verse;
using Verse.AI;

namespace WashingMachine;

[StaticConstructorOnStartup]
public static class WashingMachinePatches
{
    static WashingMachinePatches() => new Harmony("nanaloveyuki.washingmachine").PatchAll();
}

// A held pawn still ticks normally, but hunger, mental states and constant think trees cannot escape the drum.
[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
public static class HeldPawnStartJobPatch
{
    public static bool Prefix(Pawn ___pawn, Job newJob) =>
        !(___pawn.ParentHolder is Building_WashingMachine washer && washer.HoldingPawn)
        || newJob?.def == WashingMachineDefOf.PWM_BeingWashed;
}

[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.JobTrackerTickInterval))]
public static class HeldPawnThinkPatch
{
    public static bool Prefix(Pawn_JobTracker __instance, Pawn ___pawn, int delta)
    {
        if (!(___pawn.ParentHolder is Building_WashingMachine washer && washer.HoldingPawn))
            return true;
        __instance.curDriver?.DriverTickInterval(delta);
        return false;
    }
}
