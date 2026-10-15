using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 原版俘虏不看来源闸门 这里只拦本模组来客
    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.CapturedBy))]
    internal static class RHAH_CaptureGatePatch
    {
        static bool Prefix(Pawn_GuestTracker __instance, Faction by)
        {
            return RHAH_ColonyGates.AllowsCapture(Guest(__instance), by);
        }

        static Verse.Pawn Guest(Pawn_GuestTracker guest)
        {
            return guest == null
                ? null
                : AccessTools.Field(typeof(Pawn_GuestTracker), "pawn")?.GetValue(guest) as Verse.Pawn;
        }
    }

    // 原版角色买卖不看来源闸门 关闸后这笔不成交
    [HarmonyPatch(typeof(Tradeable_Pawn), "ResolveTrade")]
    internal static class RHAH_TradePawnGatePatch
    {
        static bool Prefix(Tradeable_Pawn __instance, out List<Verse.Pawn> __state)
        {
            __state = null;
            if (!RHAH_ColonyGates.AllowsTrade(__instance)) return false;
            if (__instance == null || __instance.ActionToDo == TradeAction.None) return true;
            bool buy = __instance.ActionToDo == TradeAction.PlayerBuys;
            List<Thing> things = buy ? __instance.thingsTrader : __instance.thingsColony;
            int count = buy ? __instance.CountToTransferToSource : __instance.CountToTransferToDestination;
            __state = new List<Verse.Pawn>();
            for (int i = 0; things != null && i < things.Count && i < count; i++) if (things[i] is Verse.Pawn pawn) __state.Add(pawn);
            return true;
        }

        static void Postfix(Tradeable_Pawn __instance, List<Verse.Pawn> __state)
        {
            if (__state == null || __instance == null) return;
            bool buy = __instance.ActionToDo == TradeAction.PlayerBuys;
            for (int i = 0; i < __state.Count; i++)
            {
                Verse.Pawn pawn = __state[i];
                bool completed = !pawn.Destroyed && (buy ? pawn.Faction == Faction.OfPlayer || pawn.IsSlaveOfColony : pawn.Faction != Faction.OfPlayer && !pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony);
                if (completed) HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.NotifyTrade(pawn, buy);
            }
        }
    }

    internal static class RHAH_ColonyGates
    {
        internal static bool AllowsCapture(Verse.Pawn pawn, Faction by)
        {
            if (!RHAH_Api.IsOrigin(pawn) || by == null || by != Faction.OfPlayerSilentFail)
            {
                return true;
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Imprison))
            {
                return false;
            }

            return RHAH_Api.ReleaseToColony(pawn, RHAH_ReleaseReason.Imprisoned);
        }

        internal static bool AllowsTrade(Tradeable_Pawn trade)
        {
            if (trade == null || trade.ActionToDo == TradeAction.None)
            {
                return true;
            }

            int count = trade.ActionToDo == TradeAction.PlayerSells
                ? trade.CountToTransferToDestination
                : trade.CountToTransferToSource;
            List<Thing> things = trade.ActionToDo == TradeAction.PlayerSells
                ? trade.thingsColony
                : trade.thingsTrader;
            if (things == null || count <= 0)
            {
                return true;
            }

            int seen = 0;
            foreach (Thing thing in things)
            {
                if (seen >= count)
                {
                    break;
                }

                seen++;
                Verse.Pawn pawn = thing as Verse.Pawn;
                if (!RHAH_Api.IsOrigin(pawn))
                {
                    continue;
                }

                if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Transfer))
                {
                    return false;
                }
            }

            seen = 0;
            foreach (Thing thing in things)
            {
                if (seen >= count)
                {
                    break;
                }

                seen++;
                Verse.Pawn pawn = thing as Verse.Pawn;
                if (RHAH_Api.IsOrigin(pawn))
                {
                    RHAH_Api.ReleaseToColony(pawn, RHAH_ReleaseReason.ModRequest);
                }
            }

            return true;
        }
    }
}
