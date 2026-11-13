using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HungerAndHavoc.Core;
using RimWorld;

namespace HungerAndHavoc.Pawn
{
    [HarmonyPatch]
    internal static class RHAH_NegativeMoodPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Thought_Memory), nameof(Thought_Memory.MoodOffset));
            yield return AccessTools.Method(typeof(Thought), nameof(Thought.MoodOffset));
        }

        static void Postfix(Thought __instance, MethodBase __originalMethod, ref float __result)
        {
            if (__originalMethod.DeclaringType == typeof(Thought) && __instance is Thought_Memory) return;
            if (RHAH_Mod.Settings?.Mod != null && __instance.def?.modContentPack == RHAH_Mod.Settings.Mod.Content && __result < 0f)
                __result = RHAH_ContentPreset.NegativeEffect(__result);
        }
    }
}
