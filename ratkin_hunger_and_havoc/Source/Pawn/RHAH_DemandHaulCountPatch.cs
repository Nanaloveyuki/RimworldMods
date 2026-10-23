using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    // 原版把剩余数量直接当作件数，索粮需在确定搬运量时按本次食物折算
    [HarmonyPatch(typeof(JobDriver_GiveToPawn), "DetermineNumToHaul")]
    internal static class RHAH_DemandHaulCountPatch
    {
        static void Postfix(JobDriver_GiveToPawn __instance, Toil __result)
        {
            if (RHAH_ArmedDemand.FoodRequest(__instance.job?.targetB.Pawn) == null) return;
            __result.initAction = () =>
            {
                Verse.Pawn giver = __result.actor;
                Job job = giver.CurJob;
                Verse.Pawn receiver = job.targetB.Pawn;
                HungerAndHavoc.Incidents.RHAH_ChoiceRecord request = RHAH_ArmedDemand.FoodRequest(receiver);
                int count = request == null ? 0 : RHAH_DemandFood.Units(
                    RHAH_DemandFood.Remaining(request) - RHAH_DemandFood.BeingHauledTo(receiver),
                    RHAH_DemandFood.Nutrition(job.targetA.Thing));
                if (count <= 0) giver.jobs.EndCurrentJob(JobCondition.Succeeded);
                else job.count = count;
            };
        }
    }
}
