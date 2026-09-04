using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Compatibility
{
    internal static class InspectStringCompatibility
    {
        private const string UfSuperWorkbenchTypeName = "UFRecipes.Building_SuperWorkbench";
        private const string UfRecipesAssemblyName = "UFRecipes";
        private const string UfRecipesHarmonyId = "UFRecipes.Mod";
        private const string TurnOnOffPatchTypeName =
            "TurnOnOffRePowered.HarmonyPatches.CompPowerTrader_CompInspectStringExtra";
        private const string TurnOnOffAssemblyName = "TurnOnOffRePowered";
        private const string TurnOnOffHarmonyId = "Mlie.TurnOnOffRePowered";

        private static Type ufSuperWorkbenchType;

        public static void TryApply(Harmony harmony)
        {
            var superWorkbenchType = AccessTools.TypeByName(UfSuperWorkbenchTypeName);
            var turnOnOffPatchType = AccessTools.TypeByName(TurnOnOffPatchTypeName);
            if (!IsExpectedType(superWorkbenchType, UfRecipesAssemblyName)
                || !IsExpectedType(turnOnOffPatchType, TurnOnOffAssemblyName))
            {
                return;
            }

            try
            {
                var target = AccessTools.Method(
                    typeof(CompPowerTrader),
                    nameof(CompPowerTrader.CompInspectStringExtra));
                var patchMethod = AccessTools.Method(
                    typeof(InspectStringCompatibility),
                    nameof(NormalizeUfPowerInspectString));
                if (target == null || patchMethod == null)
                {
                    Log.Warning(
                        "[SaveYourStorage] UF Recipes / Turn It On and Off inspect-string "
                        + "compatibility target was not found; the patch remains inactive.");
                    return;
                }

                ufSuperWorkbenchType = superWorkbenchType;
                var postfix = new HarmonyMethod(patchMethod)
                {
                    after = new[] { UfRecipesHarmonyId },
                    before = new[] { TurnOnOffHarmonyId }
                };
                harmony.Patch(target, postfix: postfix);
                Log.Message(
                    "[SaveYourStorage] UF Recipes / Turn It On and Off inspect-string "
                    + "compatibility applied.");
            }
            catch (Exception exception)
            {
                ufSuperWorkbenchType = null;
                Log.Warning(
                    "[SaveYourStorage] UF Recipes / Turn It On and Off inspect-string "
                    + "compatibility failed and remains inactive: " + exception);
            }
        }

        private static bool IsExpectedType(Type type, string assemblyName)
        {
            return type != null
                && string.Equals(
                    type.Assembly.GetName().Name,
                    assemblyName,
                    StringComparison.Ordinal);
        }

        private static void NormalizeUfPowerInspectString(
            CompPowerTrader __instance,
            ref string __result)
        {
            if (__result != null || __instance?.parent == null)
            {
                return;
            }

            var superWorkbenchType = ufSuperWorkbenchType;
            if (superWorkbenchType != null
                && superWorkbenchType.IsInstanceOfType(__instance.parent))
            {
                __result = string.Empty;
            }
        }
    }
}
