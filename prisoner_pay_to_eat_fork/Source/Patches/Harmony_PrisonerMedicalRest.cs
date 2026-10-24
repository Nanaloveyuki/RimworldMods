using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    [HarmonyPatch(typeof(JobGiver_PatientGoToBed), "TryGiveJob")]
    internal static class Patch_PrisonerVoluntaryMedicalRest
    {
        [HarmonyPrefix]
        private static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (PrisonerMedicalRestPolicy.AllowsMedicalRest(pawn))
                return true;
            __result = null;
            return false;
        }
    }
}
