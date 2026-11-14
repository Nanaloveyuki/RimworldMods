using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Trade
{
    [HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
    internal static class RHAH_TradeGatePatch
    {
        internal sealed class TradeState
        {
            internal bool Blocked;
            internal readonly List<SelectedPawn> Pawns = new List<SelectedPawn>();
        }

        internal readonly struct SelectedPawn
        {
            internal readonly Verse.Pawn Pawn;
            internal readonly bool Bought;

            internal SelectedPawn(Verse.Pawn pawn, bool bought)
            {
                Pawn = pawn;
                Bought = bought;
            }
        }

        // 原版按整单总价交割白银 必须在任何货物成交前检查全部人物
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(TradeDeal __instance, ref bool actuallyTraded,
            ref bool __result, out TradeState __state)
        {
            __state = new TradeState();
            List<Tradeable> tradeables = __instance.AllTradeables;
            for (int i = 0; i < tradeables.Count; i++)
            {
                if (tradeables[i] is not Tradeable_Pawn trade || trade.ActionToDo == TradeAction.None) continue;
                if (!RHAH_ColonyGates.AllowsTrade(trade))
                {
                    __state.Blocked = true;
                    actuallyTraded = false;
                    __result = false;
                    return false;
                }

                bool bought = trade.ActionToDo == TradeAction.PlayerBuys;
                List<Thing> things = bought ? trade.thingsTrader : trade.thingsColony;
                int count = bought ? trade.CountToTransferToSource : trade.CountToTransferToDestination;
                for (int j = 0; things != null && j < things.Count && j < count; j++)
                {
                    if (things[j] is Verse.Pawn pawn) __state.Pawns.Add(new SelectedPawn(pawn, bought));
                }
            }

            return true;
        }

        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(ref bool actuallyTraded, ref bool __result, TradeState __state)
        {
            if (__state == null) return;
            if (__state.Blocked)
            {
                actuallyTraded = false;
                __result = false;
                return;
            }
            if (!__result) return;

            for (int i = 0; i < __state.Pawns.Count; i++)
            {
                SelectedPawn selected = __state.Pawns[i];
                Verse.Pawn pawn = selected.Pawn;
                bool completed = !pawn.Destroyed && (selected.Bought
                    ? pawn.Faction == Faction.OfPlayer || pawn.IsSlaveOfColony
                    : pawn.Faction != Faction.OfPlayer && !pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony);
                if (!completed) continue;
                if (RHAH_Api.IsOrigin(pawn))
                    RHAH_ApiHost.CommitReleaseToColony(pawn, RHAH_ReleaseReason.ModRequest);
                HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.NotifyTrade(pawn, selected.Bought);
            }
        }
    }
}
