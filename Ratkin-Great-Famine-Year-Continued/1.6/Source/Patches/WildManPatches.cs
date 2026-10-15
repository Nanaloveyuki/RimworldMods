using HarmonyLib;
using Verse;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(WildManUtility), nameof(WildManUtility.IsWildMan))]
    public static class WildManPatches
    {
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (!__result &&
                MouseDisasterUtility.IsWildMouseDisasterKind(p) &&
                p?.Faction == null &&
                !MouseDisasterVisitorUtility.IsManagedVisitor(p) &&
                (p.apparel == null || p.apparel.WornApparelCount == 0))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetAcceptArrestChance))]
    public static class WildRatkinArrestPatch
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (MouseDisasterUtility.IsWildMouseDisasterKind(__instance))
            {
                __result = 1f;
            }
        }
    }
}
