using HarmonyLib;
using HungerAndHavoc.Core;
using Verse;

namespace HungerAndHavoc.Identity
{
    [HarmonyPatch(typeof(HediffComp_Immunizable), nameof(HediffComp_Immunizable.SeverityChangePerDay))]
    internal static class RHAH_PlagueProgressionPatch
    {
        static void Postfix(HediffComp_Immunizable __instance, ref float __result)
        {
            if (__instance.parent?.def?.defName == "RHAH_Plague" && __result > 0f)
                __result *= RHAH_Mod.Settings?.plagueProgressionFactor ?? 1f;
        }
    }
}
