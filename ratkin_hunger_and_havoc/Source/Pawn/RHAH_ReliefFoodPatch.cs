using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    // 原版紧急觅食也必须遵守来客的赈灾规则
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    internal static class RHAH_ReliefFoodPatch
    {
        static void Postfix(Verse.Pawn pawn, ref Job __result)
        {
            if (RHAH_MouseSwarm.IsProvoked(pawn) || JobGiver_RHAH_Leave.IsDeparting(pawn))
            {
                __result = null;
                return;
            }

            if (__result == null || !RHAH_BatchAttitude.CanOrderLeave(pawn))
            {
                return;
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.FeedFromRelief))
            {
                __result = null;
                return;
            }

            if (!RHAH_ReliefFood.ReliefRulesApply)
            {
                return;
            }

            if (__result.def != JobDefOf.Ingest && __result.def != JobDefOf.Harvest &&
                __result.def != JobDefOf.TakeFromOtherInventory)
            {
                return;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            RHAH_Settings settings = RHAH_Mod.Settings;
            bool ignoreZone = snapshot.HasBeenFed && settings != null && settings.ignoreReliefAfterFed;
            Thing food = __result.targetA.Thing;
            RHAH_FoodReject rejection = RHAH_ReliefFood.Reject(pawn, food, true, ignoreZone, false);
            if (rejection == RHAH_FoodReject.Zone && RHAH_ReliefFood.MayEatOutside(pawn))
            {
                rejection = RHAH_ReliefFood.Reject(pawn, food, false, false, false);
            }

            if (rejection != RHAH_FoodReject.None)
            {
                __result = JobGiver_RHAH_Feed.TryCreate(pawn);
            }
        }
    }
}
