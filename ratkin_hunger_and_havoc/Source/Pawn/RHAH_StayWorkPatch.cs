using HungerAndHavoc.Api;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    // 原版饥饿觅食找不到食物时 才允许啃树皮或墙皮
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    internal static class RHAH_GnawFoodPatch
    {
        static bool Prefix(Verse.Pawn pawn, ref Job __result)
        {
            if (!JobGiver_RHAH_Leave.IsDeparting(pawn)) return true;
            __result = null;
            return false;
        }

        static void Postfix(Verse.Pawn pawn, ThinkNode __instance, ref Job __result)
        {
            if (__result != null || !RHAH_VisitorRules.IsNeedFood(__instance) || JobGiver_RHAH_Leave.IsDeparting(pawn))
            {
                return;
            }

            Job gnaw = JobGiver_RHAH_Gnaw.TryCreate(pawn, true);
            if (gnaw != null)
            {
                __result = gnaw;
            }
        }
    }

    // 停留到期拒绝普通工作；已获准离图的来客也不再接觅食。
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    internal static class RHAH_StayWorkPatch
    {
        static bool Prefix(Verse.Pawn ___pawn, Job newJob, ThinkNode jobGiver)
        {
            if (JobGiver_RHAH_Leave.RejectsFoodJob(___pawn, newJob, jobGiver)) return false;

            if (RHAH_VisitorRules.RejectsSelfExit(
                RHAH_Api.IsVisitor(___pawn),
                newJob != null && newJob.exitMapOnArrival,
                Compat.RHAH_ChildMovement.CanWalkOut(___pawn)))
            {
                return false;
            }

            return !RHAH_VisitorStay.RejectsAssignedJob(___pawn, newJob, jobGiver);
        }
    }
}
