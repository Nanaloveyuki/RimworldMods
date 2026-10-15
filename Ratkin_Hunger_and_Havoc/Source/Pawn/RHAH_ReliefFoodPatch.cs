using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    // 饱食后的下一次原版进食可忽略赈灾区 不改全局 WillEat
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    internal static class RHAH_ReliefFoodPatch
    {
        static void Postfix(Verse.Pawn pawn, ref Job __result)
        {
            if (RHAH_MouseSwarm.IsProvoked(pawn))
            {
                __result = null;
                return;
            }

            if (__result == null || __result.def != JobDefOf.Ingest || !RHAH_Api.IsVisitor(pawn) ||
                !RHAH_VisitorRules.AllowsModBehavior(RHAH_VisitorStay.Kind(pawn)))
            {
                return;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (snapshot == null || !snapshot.HasBeenFed || settings == null ||
                !settings.reliefEnabled || !settings.ignoreReliefAfterFed)
            {
                return;
            }

            Thing food = __result.targetA.Thing;
            if (food == null || food.MapHeld != pawn.Map)
            {
                return;
            }

            if (RHAH_ReliefFood.Reject(pawn, food, true, true) != RHAH_FoodReject.None)
            {
                __result = null;
            }
        }
    }
}
