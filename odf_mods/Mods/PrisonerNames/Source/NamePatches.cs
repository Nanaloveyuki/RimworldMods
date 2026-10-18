using HarmonyLib;
using Verse;

namespace Odf.PrisonerNames
{
    public static class NamePatches
    {
        static Harmony harmony;

        public static void Install()
        {
            if (harmony != null) return;
            harmony = new Harmony("odf.nanaloveyuki.prisonernames");
            harmony.PatchAll(typeof(NamePatches).Assembly);
        }

        [HarmonyPatch(typeof(Pawn), "set_Name")]
        static class NameChangedPatch
        {
            static void Postfix(Pawn __instance)
            {
                GameComponent_PrisonerNames.Current?.NotifyNameChanged(__instance);
            }
        }
    }
}
