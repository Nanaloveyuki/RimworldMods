using HarmonyLib;
using Verse;

namespace MouseDisaster
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        private static bool patched;

        static HarmonyBootstrap()
        {
            LongEventHandler.ExecuteWhenFinished(PatchAllSafe);
        }

        private static void PatchAllSafe()
        {
            if (patched)
            {
                return;
            }

            patched = true;
            Harmony harmony = new Harmony("nanaloveyuki.mouse.disaster.famine.continued");
            harmony.PatchAll();
        }
    }
}
