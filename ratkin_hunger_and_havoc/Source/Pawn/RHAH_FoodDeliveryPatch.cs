using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    // 只记殖民者 GiveToPawn 的实际入包量 不把预存食物或未完成搬运算成交付
    [HarmonyPatch(typeof(ThingOwner), nameof(ThingOwner.TryTransferToContainer),
        new[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(bool) })]
    internal static class RHAH_FoodDeliveryPatch
    {
        static void Postfix(ThingOwner __instance, Thing item, ThingOwner otherContainer, int __result)
        {
            if (__result <= 0 || !(otherContainer?.Owner is Pawn_InventoryTracker inventory) ||
                item == null || !(__instance.Owner is Pawn_CarryTracker carry)) return;
            Verse.Pawn giver = carry.pawn;
            Job job = giver?.CurJob;
            if (giver?.Faction != Faction.OfPlayer || job?.def != JobDefOf.GiveToPawn ||
                job.GetTarget(TargetIndex.B).Pawn != inventory.pawn) return;
            if (RHAH_ArmedDemand.FoodRequest(inventory.pawn) != null)
            {
                RHAH_ArmedDemand.NoteFood(inventory.pawn, item, __result);
                return;
            }

            LordJob_RHAH_Visitor visitor = inventory.pawn.GetLord()?.LordJob as LordJob_RHAH_Visitor;
            visitor?.NoteFoodDelivered(inventory.pawn, item, __result);
        }
    }
}
