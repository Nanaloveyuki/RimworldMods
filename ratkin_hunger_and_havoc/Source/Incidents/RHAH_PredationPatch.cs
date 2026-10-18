using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Incidents
{
    // 原版觅食不认识安居点猎物 这里只改被跟踪的营地捕食者
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    internal static class RHAH_PredationFoodPatch
    {
        static bool Prefix(Verse.Pawn pawn, ref Job __result)
        {
            if (pawn == null || !pawn.RaceProps.Animal || !pawn.RaceProps.predator || pawn.Faction != null)
            {
                return true;
            }

            Job job;
            if (!RHAH_CampPredation.TryFoodJob(pawn, out job))
            {
                return true;
            }

            __result = job;
            return job == null;
        }
    }

    [HarmonyPatch(typeof(JobGiver_ReactToCloseMeleeThreat), "TryGiveJob")]
    internal static class RHAH_PredationFleePatch
    {
        static bool Prefix(Verse.Pawn pawn, ref Job __result)
        {
            Verse.Pawn predator = pawn?.mindState?.meleeThreat;
            if (!RHAH_CampPredation.ShouldFlee(predator, pawn))
            {
                return true;
            }

            __result = FleeUtility.FleeJob(pawn, predator, 16);
            return false;
        }
    }
}
