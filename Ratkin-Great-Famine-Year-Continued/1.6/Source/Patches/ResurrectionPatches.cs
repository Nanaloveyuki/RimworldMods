using HarmonyLib;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(ResurrectionUtility), nameof(ResurrectionUtility.TryResurrect))]
    public static class MouseDisasterResurrectionPatch
    {
        public static void Prefix(Pawn pawn, ref ResurrectionParams parms)
        {
            if (!MouseDisasterUtility.IsRatkin(pawn))
            {
                return;
            }

            if (parms == null)
            {
                parms = new ResurrectionParams
                {
                    restoreMissingParts = false
                };
                return;
            }

            parms.restoreMissingParts = false;
        }
    }
}
