using HarmonyLib;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    public static class MouseDisasterNarrativeExitPatch
    {
        public static void Prefix(Pawn __instance, out int __state)
        {
            __state = __instance.MapHeld?.uniqueID ?? -1;
        }

        public static void Postfix(Pawn __instance, int __state)
        {
            if (__state >= 0 && !__instance.Dead && __instance.MapHeld?.uniqueID != __state)
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyNarrativeExit(__instance, __state);
        }
    }

    [HarmonyPatch(typeof(LordToil_WaitForItems), "get_HasAllRequestedItems")]
    public static class MouseDisasterNarrativeDeliveryPatch
    {
        public static void Postfix(LordToil_WaitForItems __instance, bool __result)
        {
            if (__result && __instance.target != null)
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyNarrativeDelivery(new[] { __instance.target });
        }
    }
}
