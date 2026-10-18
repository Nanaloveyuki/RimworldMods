using RimWorld;
using Verse;

namespace WashingMachine;

[DefOf]
public static class WashingMachineDefOf
{
    public static ThingDef PWM_WashingMachine;
    public static JobDef PWM_LoadApparel;
    public static JobDef PWM_CarryToWasher;
    public static JobDef PWM_BeingWashed;
    public static ThoughtDef PWM_Washed;

    static WashingMachineDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(WashingMachineDefOf));
}
