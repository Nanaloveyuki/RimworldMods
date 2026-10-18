using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static void MarkForcedPrisonerOnPurchase(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            ForcePrisonerOnPurchasePawnIds.Add(pawn.thingIDNumber);
        }

        public static void MarkTradableChattel(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            TradableChattelPawnIds.Add(pawn.thingIDNumber);
        }

        public static bool IsMarkedTradableChattel(Pawn pawn)
        {
            return pawn != null && TradableChattelPawnIds.Contains(pawn.thingIDNumber);
        }

        public static void UnmarkTradableChattel(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            TradableChattelPawnIds.Remove(pawn.thingIDNumber);
        }

        public static bool ConsumeForcedPrisonerOnPurchase(Pawn pawn)
        {
            return pawn != null && ForcePrisonerOnPurchasePawnIds.Remove(pawn.thingIDNumber);
        }

        public static bool IsForcedPrisonerOnPurchase(Pawn pawn)
        {
            return pawn != null && ForcePrisonerOnPurchasePawnIds.Contains(pawn.thingIDNumber);
        }

        public static bool IsMouseDisasterTradePawn(Pawn pawn)
        {
            return pawn != null && (IsMarkedTradableChattel(pawn) || IsForcedPrisonerOnPurchase(pawn) || IsPersistedMouseDisasterTradePawn(pawn));
        }

        private static bool IsPersistedMouseDisasterTradePawn(Pawn pawn)
        {
            if (pawn.kindDef == null || pawn.kindDef != MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild ||
                pawn.Faction == null || pawn.guest == null || !pawn.guest.IsPrisoner || pawn.guest.HostFaction != pawn.Faction)
            {
                return false;
            }

            Lord lord = pawn.GetLord();
            return lord?.LordJob is LordJob_TradeWithColony &&
                   lord.ownedPawns != null && lord.ownedPawns.Any(IsMouseDisasterTraderAdult);
        }

        public static MouseDisasterTradePawnJoinMode GetRatkinYoungTradeJoinMode()
        {
            return MouseDisasterMod.Settings?.GetRatkinYoungTradeJoinMode() ?? MouseDisasterTradePawnJoinMode.Slave;
        }

        public static void PreparePurchasedTradePawnJoinStatus(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, actAsIfSpawned: true);
            if (pawn.guest == null)
            {
                return;
            }

            pawn.guest.joinStatus = GetRatkinYoungTradeJoinMode() == MouseDisasterTradePawnJoinMode.Slave
                ? JoinStatus.JoinAsSlave
                : JoinStatus.JoinAsColonist;
        }

        public static void ApplyPurchasedTradePawnState(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            MouseDisasterTradePawnJoinMode joinMode = GetRatkinYoungTradeJoinMode();
            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, actAsIfSpawned: true);
            if (pawn.guest == null && joinMode != MouseDisasterTradePawnJoinMode.Colonist)
            {
                PawnComponentsUtility.CreateInitialComponents(pawn);
            }

            TryReleaseLeadYourPetTradePawn(pawn);
            RemoveAllIncidentVisitorHediffs(pawn);
            ClearTradeLeaderState(pawn);
            ResetBeggarState(pawn);
            RemoveChildExchangeTrackingForPurchasedPawn(pawn);
            ChildExchangeMoodPawnIds.Remove(pawn.thingIDNumber);
            pawn.jobs?.StopAll();
            pawn.GetLord()?.RemovePawn(pawn);

            if (pawn.guest == null)
            {
                if (joinMode == MouseDisasterTradePawnJoinMode.Colonist && pawn.Faction != Faction.OfPlayer)
                {
                    pawn.SetFaction(Faction.OfPlayer);
                }

                NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
                return;
            }

            switch (joinMode)
            {
                case MouseDisasterTradePawnJoinMode.Slave:
                    pawn.guest.joinStatus = JoinStatus.JoinAsSlave;
                    if (pawn.Faction != Faction.OfPlayer || pawn.guest.GuestStatus != GuestStatus.Slave)
                    {
                        pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                    }
                    break;
                case MouseDisasterTradePawnJoinMode.Colonist:
                    pawn.guest.joinStatus = JoinStatus.JoinAsColonist;
                    if (pawn.Faction != Faction.OfPlayer)
                    {
                        pawn.SetFaction(Faction.OfPlayer);
                    }
                    if (pawn.guest.GuestStatus != GuestStatus.Guest)
                    {
                        pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
                    }
                    break;
                default:
                    pawn.guest.joinStatus = JoinStatus.JoinAsColonist;
                    if (pawn.Faction == Faction.OfPlayer)
                    {
                        pawn.SetFaction(null);
                    }
                    pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                    break;
            }

            NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
        }

        public static void MarkChildExchangeMoodChild(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            ChildExchangeMoodPawnIds.Add(pawn.thingIDNumber);
            MarkMapPawnCacheDirty(pawn);
        }

        public static void MarkChildExchangeMoodChildren(IEnumerable<Pawn> pawns)
        {
            if (pawns == null)
            {
                return;
            }

            foreach (Pawn pawn in pawns)
            {
                MarkChildExchangeMoodChild(pawn);
            }
        }

        public static bool HasChildExchangeMoodMarker(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (pawn.Dead || !IsMouseEggOrChild(pawn))
            {
                ChildExchangeMoodPawnIds.Remove(pawn.thingIDNumber);
                return false;
            }

            return ChildExchangeMoodPawnIds.Contains(pawn.thingIDNumber);
        }

        public static void RegisterChildExchange(Pawn trader, IEnumerable<Pawn> children, int durationTicks, IEnumerable<Pawn> escorts = null)
        {
            if (trader?.Map == null)
            {
                return;
            }

            List<int> childIds = children?
                .Where(child => child != null && !child.Dead)
                .Select(child => child.thingIDNumber)
                .Distinct()
                .ToList() ?? new List<int>();
            if (childIds.Count == 0)
            {
                return;
            }

            List<int> escortIds = escorts?
                .Where(escort => escort != null && !escort.Dead)
                .Select(escort => escort.thingIDNumber)
                .Distinct()
                .ToList() ?? new List<int>();

            ActiveChildExchangeByTraderId[trader.thingIDNumber] = new ChildExchangeState
            {
                trader = trader,
                escortPawns = escorts?
                    .Where(escort => escort != null && !escort.Dead)
                    .Distinct()
                    .ToList() ?? new List<Pawn>(),
                childPawns = children?
                    .Where(child => child != null && !child.Dead)
                    .Distinct()
                    .ToList() ?? new List<Pawn>(),
                mapId = trader.Map.uniqueID,
                expireTick = Find.TickManager.TicksGame + Mathf.Max(600, durationTicks),
                escortPawnIds = escortIds,
                childPawnIds = childIds
            };
        }

        public static void RemoveChildExchangeTrackingForPurchasedPawn(Pawn pawn)
        {
            if (pawn == null || ActiveChildExchangeByTraderId.Count == 0)
            {
                return;
            }

            ReusableIntList.Clear();
            foreach (KeyValuePair<int, ChildExchangeState> pair in ActiveChildExchangeByTraderId)
            {
                ChildExchangeState state = pair.Value;
                if (state == null) continue;
                if (state.trader == pawn)
                {
                    ReusableIntList.Add(pair.Key);
                    continue;
                }
                state.escortPawnIds?.Remove(pawn.thingIDNumber);
                state.escortPawns?.Remove(pawn);
                if (state.childPawnIds == null || !state.childPawnIds.Remove(pawn.thingIDNumber))
                {
                    continue;
                }

                state.childPawns?.Remove(pawn);
                if (state.childPawnIds.Count == 0)
                {
                    ReusableIntList.Add(pair.Key);
                }
            }

            for (int i = 0; i < ReusableIntList.Count; i++)
            {
                int traderId = ReusableIntList[i];
                if (!ActiveChildExchangeByTraderId.TryGetValue(traderId, out ChildExchangeState state) || state == null)
                {
                    continue;
                }

                Pawn trader = state.trader;
                Map map = trader?.Map;
                List<Pawn> exchangedChildren = state.childPawns?.Where(child => child != null).Distinct().ToList() ?? new List<Pawn>();
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordN005Outcome(
                    traderId,
                    trader,
                    exchangedChildren,
                    null,
                    map,
                    MouseDisasterN005Outcome.Broken);
                ActiveChildExchangeByTraderId.Remove(traderId);

                if (map == null)
                {
                    continue;
                }

                ReusablePawnList.Clear();
                if (trader != null && trader.Spawned && !trader.Dead && !IsPlayerAffiliatedRatkin(trader))
                {
                    ReusablePawnList.Add(trader);
                }

                if (state.escortPawns != null)
                {
                    ReusablePawnList.AddRange(state.escortPawns.Where(escort => escort != null && escort.Spawned && !escort.Dead && !IsPlayerAffiliatedRatkin(escort) && !ReusablePawnList.Contains(escort)));
                }

                if (ReusablePawnList.Count > 0)
                {
                    MakeTravelAndExitLord(map, ReusablePawnList, map.Center);
                    EnsureMouseDisasterFactionNeutralOnMap(map, trader?.Faction);
                }
            }
        }

        public static bool IsChildExchangeTrader(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (!ActiveChildExchangeByTraderId.TryGetValue(pawn.thingIDNumber, out ChildExchangeState state))
            {
                return false;
            }

            if (pawn.Map == null || pawn.Map.uniqueID != state.mapId)
            {
                ActiveChildExchangeByTraderId.Remove(pawn.thingIDNumber);
                return false;
            }

            return true;
        }

        internal static bool HasActiveChildExchangeForTrader(Pawn pawn)
        {
            return pawn != null && ActiveChildExchangeByTraderId.ContainsKey(pawn.thingIDNumber);
        }

        public static bool TryExecuteChildExchange(Pawn trader, int mode, out string message)
        {
            message = "MouseDisaster_UI_ExchangeFailed".Translate().Resolve();
            if (trader?.Map == null || !IsChildExchangeTrader(trader))
            {
                return false;
            }

            Pawn offeredBaby = FindExchangeOfferBaby(trader.Map, mode);
            if (offeredBaby == null)
            {
                message = "MouseDisaster_UI_ExchangeBabyMissing".Translate().Resolve();
                return false;
            }

            return TryResolveChildExchange(trader, offeredBaby, out message);
        }

        public static bool TryRejectChildExchange(Pawn trader, out string message)
        {
            message = "MouseDisaster_UI_ExchangeRejectionFailed".Translate().Resolve();
            if (trader?.Map == null || !ActiveChildExchangeByTraderId.TryGetValue(trader.thingIDNumber, out ChildExchangeState state) || !IsChildExchangeTrader(trader))
            {
                return false;
            }

            Map map = trader.Map;
            Dictionary<int, Pawn> pawnLookup = BuildSpawnedPawnLookup(map.mapPawns.AllPawnsSpawned);
            List<Pawn> exchangeChildren = ResolveChildExchangeChildren(trader, pawnLookup).Where(child => child.Spawned && !child.Dead).ToList();
            List<Pawn> exchangeEscorts = ResolveChildExchangeEscorts(trader, pawnLookup).Where(escort => escort.Spawned && !escort.Dead).ToList();
            if (exchangeChildren.Count == 0)
            {
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordN005Outcome(
                    trader.thingIDNumber,
                    trader,
                    state.childPawns,
                    null,
                    map,
                    MouseDisasterN005Outcome.Broken);
                ActiveChildExchangeByTraderId.Remove(trader.thingIDNumber);
                List<Pawn> failedLeaving = new List<Pawn> { trader };
                failedLeaving.AddRange(exchangeEscorts);
                MakeTravelAndExitLord(map, failedLeaving, map.Center);
                EnsureMouseDisasterFactionNeutralOnMap(map, trader.Faction);
                message = "MouseDisaster_UI_ExchangeStockMissing".Translate().Resolve();
                return false;
            }

            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordN005Outcome(
                trader.thingIDNumber,
                trader,
                exchangeChildren,
                null,
                map,
                MouseDisasterN005Outcome.Rejected);
            ActiveChildExchangeByTraderId.Remove(trader.thingIDNumber);
            List<Pawn> leaving = new List<Pawn> { trader };
            leaving.AddRange(exchangeEscorts);
            leaving.AddRange(exchangeChildren);
            MouseDisasterPawnGroupUtility.SendFamilyAway(map, trader, leaving, exchangeChildren);
            EnsureMouseDisasterFactionNeutralOnMap(map, trader.Faction);
            message = "MouseDisaster_Story_ExchangeRejected".Translate();
            return true;
        }

        public static Pawn FindExchangeOfferBaby(Map map, int mode)
        {
            if (map == null)
            {
                return null;
            }

            IEnumerable<Pawn> candidates = map.mapPawns.AllPawnsSpawned
                .Where(pawn => IsMouseEggBaby(pawn) && !pawn.Dead)
                .OrderBy(pawn => pawn.ageTracker.AgeBiologicalTicks);

            switch (mode)
            {
                case ChildExchangeModeColonist:
                    return candidates.FirstOrDefault(pawn => pawn.Faction == Faction.OfPlayer && !pawn.IsPrisonerOfColony && !pawn.IsSlaveOfColony);
                case ChildExchangeModeSlave:
                    return candidates.FirstOrDefault(pawn => pawn.IsSlaveOfColony);
                case ChildExchangeModePrisoner:
                    return candidates.FirstOrDefault(pawn => pawn.IsPrisonerOfColony);
                default:
                    return null;
            }
        }

        private static bool TryResolveChildExchange(Pawn trader, Pawn offeredBaby, out string message)
        {
            message = "MouseDisaster_UI_ExchangeFailed".Translate().Resolve();
            if (trader?.Map == null || offeredBaby == null || !IsChildExchangeTrader(trader))
            {
                return false;
            }

            Dictionary<int, Pawn> pawnLookup = BuildSpawnedPawnLookup(trader.Map.mapPawns.AllPawnsSpawned);
            List<Pawn> exchangeChildren = ResolveChildExchangeChildren(trader, pawnLookup).Where(child => child.Spawned && !child.Dead).ToList();
            List<Pawn> exchangeEscorts = ResolveChildExchangeEscorts(trader, pawnLookup).Where(escort => escort.Spawned && !escort.Dead).ToList();
            if (exchangeChildren.Count == 0)
            {
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordN005Outcome(
                    trader.thingIDNumber,
                    trader,
                    null,
                    offeredBaby,
                    trader.Map,
                    MouseDisasterN005Outcome.Broken);
                ActiveChildExchangeByTraderId.Remove(trader.thingIDNumber);
                List<Pawn> failedLeaving = new List<Pawn> { trader };
                failedLeaving.AddRange(exchangeEscorts);
                MakeTravelAndExitLord(trader.Map, failedLeaving, trader.Map.Center);
                EnsureMouseDisasterFactionNeutralOnMap(trader.Map, trader.Faction);
                message = "MouseDisaster_UI_ExchangeStockMissing".Translate().Resolve();
                return false;
            }

            if (!TransferOfferedBabyToTrader(offeredBaby, trader))
            {
                message = "MouseDisaster_UI_ExchangeTransferFailed".Translate().Resolve();
                return false;
            }

            for (int i = 0; i < exchangeChildren.Count; i++)
            {
                Pawn child = exchangeChildren[i];
                TryReleaseLeadYourPetTradePawn(child);
                UnmarkTradableChattel(child);
                ConsumeForcedPrisonerOnPurchase(child);
                MouseDisasterVisitorUtility.RemoveVisitorRecord(child);
                child.GetLord()?.RemovePawn(child);
                child.jobs?.StopAll();
                if (child.Faction == Faction.OfPlayer)
                {
                    child.SetFaction(null);
                }

                child.guest?.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                NotifyMouseDisasterPawnIdentityOrLifeStageChanged(child);
            }

            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordN005Outcome(
                trader.thingIDNumber,
                trader,
                exchangeChildren,
                offeredBaby,
                trader.Map,
                MouseDisasterN005Outcome.Accepted);
            ActiveChildExchangeByTraderId.Remove(trader.thingIDNumber);
            List<Pawn> leaving = new List<Pawn> { trader };
            leaving.AddRange(exchangeEscorts);
            if (offeredBaby.Spawned && !offeredBaby.Dead)
            {
                leaving.Add(offeredBaby);
            }

            MouseDisasterPawnGroupUtility.SendFamilyAway(trader.Map, trader, leaving, new[] { offeredBaby });
            EnsureMouseDisasterFactionNeutralOnMap(trader.Map, trader.Faction);
            message = "MouseDisaster_Story_ExchangeAccepted".Translate();
            return true;
        }

        private static bool TransferOfferedBabyToTrader(Pawn offeredBaby, Pawn trader)
        {
            if (offeredBaby == null || trader?.Faction == null || offeredBaby.Dead)
            {
                return false;
            }

            TryReleaseLeadYourPetTradePawn(offeredBaby);
            MouseDisasterVisitorUtility.RemoveVisitorRecord(offeredBaby);
            offeredBaby.GetLord()?.RemovePawn(offeredBaby);
            if (offeredBaby.Faction != trader.Faction)
            {
                offeredBaby.SetFaction(trader.Faction);
            }

            offeredBaby.guest?.SetGuestStatus(null, GuestStatus.Guest);
            offeredBaby.jobs?.StopAll();
            StripRatEggInventory(offeredBaby);

            return true;
        }

        public static void ProcessChildExchangeTimeouts(Map map)
        {
            if (map == null || ActiveChildExchangeByTraderId.Count == 0)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            int mapId = map.uniqueID;
            Dictionary<int, Pawn> pawnLookup = BuildSpawnedPawnLookup(map.mapPawns.AllPawnsSpawned);
            ReusableIntList.Clear();
            foreach (KeyValuePair<int, ChildExchangeState> pair in ActiveChildExchangeByTraderId)
            {
                ChildExchangeState state = pair.Value;
                if (state == null || state.mapId != mapId)
                {
                    continue;
                }

                bool traderMissing = state.trader == null || state.trader.Dead || state.trader.Map == null || state.trader.Map.uniqueID != mapId;
                bool childrenMissing = state.childPawnIds == null || state.childPawnIds.Count == 0 ||
                    !state.childPawnIds.Any(childId => pawnLookup.TryGetValue(childId, out Pawn child) && child != null && !child.Dead);
                if (traderMissing || childrenMissing || now >= state.expireTick)
                {
                    ReusableIntList.Add(pair.Key);
                }
            }

            if (ReusableIntList.Count == 0)
            {
                return;
            }

            for (int i = 0; i < ReusableIntList.Count; i++)
            {
                int traderId = ReusableIntList[i];
                if (!ActiveChildExchangeByTraderId.TryGetValue(traderId, out ChildExchangeState state) || state == null || state.mapId != mapId)
                {
                    continue;
                }

                Pawn trader = state.trader;
                bool traderMissing = trader == null || trader.Dead || trader.Map == null || trader.Map.uniqueID != mapId;
                bool childrenMissing = state.childPawnIds == null || state.childPawnIds.Count == 0 ||
                    !state.childPawnIds.Any(childId => pawnLookup.TryGetValue(childId, out Pawn child) && child != null && !child.Dead);
                bool broken = traderMissing || childrenMissing;
                List<Pawn> exchangeChildren = ResolveChildExchangeStatePawns(state.childPawnIds, pawnLookup);
                List<Pawn> exchangeEscorts = ResolveChildExchangeStatePawns(state.escortPawnIds, pawnLookup);
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordN005Outcome(
                    traderId,
                    trader,
                    exchangeChildren,
                    null,
                    map,
                    broken ? MouseDisasterN005Outcome.Broken : MouseDisasterN005Outcome.TimedOut);
                ActiveChildExchangeByTraderId.Remove(traderId);
                ReusablePawnList.Clear();
                if (trader != null && trader.Spawned && !trader.Dead)
                {
                    ReusablePawnList.Add(trader);
                }

                ReusablePawnList.AddRange(exchangeEscorts.Where(escort => escort != null && escort.Spawned && !escort.Dead && !ReusablePawnList.Contains(escort)));
                ReusablePawnList.AddRange(exchangeChildren.Where(child => child != null && child.Spawned && !child.Dead && !ReusablePawnList.Contains(child)));
                if (trader != null && trader.Spawned && !trader.Dead)
                    MouseDisasterPawnGroupUtility.SendFamilyAway(map, trader, ReusablePawnList, exchangeChildren);
                else
                    MakeTravelAndExitLord(map, ReusablePawnList, map.Center);
                if (!broken)
                {
                    Messages.Message("MouseDisaster_Story_ExchangeTimedOut".Translate(), trader, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            }
        }

        private static List<Pawn> ResolveChildExchangeStatePawns(List<int> pawnIds, Dictionary<int, Pawn> pawnLookup)
        {
            List<Pawn> result = new List<Pawn>();
            if (pawnIds == null || pawnLookup == null)
            {
                return result;
            }

            for (int i = 0; i < pawnIds.Count; i++)
            {
                Pawn pawn = ResolvePawnById(pawnLookup, pawnIds[i]);
                if (pawn != null && !result.Contains(pawn))
                {
                    result.Add(pawn);
                }
            }

            return result;
        }

        private static List<Pawn> ResolveChildExchangeEscorts(Pawn trader, Dictionary<int, Pawn> pawnLookup)
        {
            List<Pawn> result = new List<Pawn>();
            ResolveChildExchangeEscortsInto(trader, pawnLookup, result);
            return result;
        }

        private static void ResolveChildExchangeEscortsInto(Pawn trader, Dictionary<int, Pawn> pawnLookup, List<Pawn> result)
        {
            if (trader?.Map == null || !ActiveChildExchangeByTraderId.TryGetValue(trader.thingIDNumber, out ChildExchangeState state) || state?.escortPawnIds == null)
            {
                return;
            }

            for (int i = 0; i < state.escortPawnIds.Count; i++)
            {
                Pawn pawn = ResolvePawnById(pawnLookup, state.escortPawnIds[i]);
                if (pawn != null)
                {
                    result.Add(pawn);
                }
            }
        }

        private static List<Pawn> ResolveChildExchangeChildren(Pawn trader, Dictionary<int, Pawn> pawnLookup)
        {
            List<Pawn> result = new List<Pawn>();
            ResolveChildExchangeChildrenInto(trader, pawnLookup, result);
            return result;
        }

        private static void ResolveChildExchangeChildrenInto(Pawn trader, Dictionary<int, Pawn> pawnLookup, List<Pawn> result)
        {
            if (trader?.Map == null || !ActiveChildExchangeByTraderId.TryGetValue(trader.thingIDNumber, out ChildExchangeState state) || state?.childPawnIds == null)
            {
                return;
            }

            for (int i = 0; i < state.childPawnIds.Count; i++)
            {
                Pawn pawn = ResolvePawnById(pawnLookup, state.childPawnIds[i]);
                if (pawn != null)
                {
                    result.Add(pawn);
                }
            }
        }

        public static Pawn FindPlayerOwnedBabyForExchange(Map map)
        {
            if (map == null)
            {
                return null;
            }

            return map.mapPawns.AllPawnsSpawned
                .Where(pawn => IsMouseEggBaby(pawn) &&
                               (pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony))
                .OrderBy(pawn => pawn.ageTracker.AgeBiologicalTicks)
                .FirstOrDefault();
        }

        public static bool HasActiveChildExchangeState()
        {
            return ActiveChildExchangeByTraderId.Count > 0;
        }
    }
}
