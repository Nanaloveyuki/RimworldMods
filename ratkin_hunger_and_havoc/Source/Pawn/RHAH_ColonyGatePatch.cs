using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
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

    [HarmonyPatch(typeof(Designator_Adopt), nameof(Designator_Adopt.DesignateThing))]
    internal static class RHAH_AdoptPatch
    {
        static bool Prefix(Thing t, out bool __state)
        {
            __state = RHAH_Api.IsVisitor(t as Verse.Pawn);
            return RHAH_ColonyGates.AllowsJoin(t as Verse.Pawn);
        }

        static void Postfix(Thing t, bool __state)
        {
            if (!__state || !(t is Verse.Pawn pawn) || pawn.Faction != Faction.OfPlayer)
            {
                return;
            }

            RHAH_VisitorRules.ApplyPlayerIdeo(pawn,
                Core.RHAH_Mod.Settings == null ? 100 : Core.RHAH_Mod.Settings.playerIdeoPercent,
                Rand.Value);
            RHAH_VisitorMood.NotifySheltered(pawn);
        }
    }

    internal static class RHAH_ColonyGates
    {
        internal static bool AllowsJoin(Verse.Pawn pawn)
        {
            return !RHAH_Api.IsOrigin(pawn) ||
                RHAH_Api.Allows(pawn, RHAH_BehaviorGate.JoinColony) &&
                RHAH_Api.ReleaseToColony(pawn, RHAH_ReleaseReason.JoinedPlayerFaction);
        }

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

                if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Transfer) ||
                    !RHAH_ApiHost.CanReleaseToColony(pawn, RHAH_ReleaseReason.ModRequest))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
