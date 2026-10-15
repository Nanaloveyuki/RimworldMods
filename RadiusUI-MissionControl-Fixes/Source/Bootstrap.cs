using System;
using HarmonyLib;
using Verse;

namespace RadiusUI.MissionControl.Fixes
{
    [StaticConstructorOnStartup]
    internal static class Bootstrap
    {
        private const string HarmonyId = "nanaloveyuki.radiusui.missioncontrol.fixes";

        static Bootstrap()
        {
            if (ModLister.GetActiveModWithIdentifier("astryl.RadiusUI.MissionControl") == null ||
                ModLister.GetActiveModWithIdentifier("astryl.ModernNotifications") != null)
                return;

            var harmony = new Harmony(HarmonyId);
            try
            {
                LetterPatches.Install(harmony);
                MinimapPatches.Install(harmony);
                HoverPatches.Install(harmony);
                Log.Message("[Mission Control Fixes] Letter routing, minimap persistence and full-text hover patches installed.");
            }
            catch (Exception exception)
            {
                harmony.UnpatchAll(HarmonyId);
                Log.Error("[Mission Control Fixes] Could not patch this Mission Control version. No fixes remain installed. " + exception);
            }
        }
    }
}
