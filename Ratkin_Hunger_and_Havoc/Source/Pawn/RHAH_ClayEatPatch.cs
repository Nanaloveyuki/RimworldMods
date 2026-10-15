using HarmonyLib;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 十五天吃满三块后原版仍会把它当食物 这里只拒观音土
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.WillEat), typeof(Verse.Pawn), typeof(Thing), typeof(Verse.Pawn), typeof(bool), typeof(bool))]
    internal static class RHAH_ClayEatThingPatch
    {
        static void Postfix(Verse.Pawn p, Thing food, ref bool __result)
        {
            if (__result && !Hediff_RHAH_ClaySatiety.CanEat(p, food))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.WillEat), typeof(Verse.Pawn), typeof(ThingDef), typeof(Verse.Pawn), typeof(bool), typeof(bool))]
    internal static class RHAH_ClayEatDefPatch
    {
        static void Postfix(Verse.Pawn p, ThingDef food, ref bool __result)
        {
            if (__result && !Hediff_RHAH_ClaySatiety.CanEat(p, food))
            {
                __result = false;
            }
        }
    }

    // 强制命令和直接调用同样在消耗前拒绝第四次
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    internal static class RHAH_ClayIngestPatch
    {
        internal static bool Prefix(Thing __instance, Verse.Pawn ingester, ref float __result)
        {
            if (Hediff_RHAH_ClaySatiety.CanEat(ingester, __instance)) return true;
            __result = 0f;
            return false;
        }
    }
}
