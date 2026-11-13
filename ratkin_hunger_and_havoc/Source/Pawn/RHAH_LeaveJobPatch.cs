using HarmonyLib;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    [HarmonyPatch(typeof(Job), nameof(Job.MakeDriver))]
    internal static class RHAH_LeaveJobPatch
    {
        internal static void Postfix(Job __instance, JobDriver __result)
        {
            if (!(__result is JobDriver_Goto) || !__instance.exitMapOnArrival ||
                !JobGiver_RHAH_Leave.IsDeparting(__result.pawn)) return;
            Verse.Pawn pawn = __result.pawn;
            __result.AddFinishAction(condition => JobGiver_RHAH_Leave.NotifyExitJobEnded(pawn, condition));
        }
    }
}
