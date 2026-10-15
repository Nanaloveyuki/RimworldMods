using HarmonyLib;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 原版喂婴要求同派系 来客幼崽因此喂不了
    [HarmonyPatch(typeof(ChildcareUtility), nameof(ChildcareUtility.HasBreastfeedCompatibleFactions), typeof(Faction), typeof(Verse.Pawn))]
    internal static class RHAH_BabyCarePatch
    {
        static void Postfix(Faction faction, Verse.Pawn baby, ref bool __result)
        {
            if (__result || faction != Faction.OfPlayer || !RHAH_Api.IsOrigin(baby))
            {
                return;
            }

            __result = true;
        }
    }
}
