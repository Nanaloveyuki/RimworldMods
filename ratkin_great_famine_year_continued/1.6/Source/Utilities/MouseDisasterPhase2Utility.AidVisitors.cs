using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterPhase2Utility
    {
        private const int AidRequestVisitDurationTicks = 60000;

        private sealed class AidRequestState : IExposable
        {
            public Pawn targetPawn;
            public List<Pawn> trackedPawns = new List<Pawn>();
            public int mapId;
            public int targetPawnId;
            public List<int> pawnIds;
            public MouseDisasterRequestKind requestKind;
            public int amount;
            public bool createsIntelSite;
            public MouseDisasterIntelSiteKind intelSiteKind;
            public int expireTick;

            public void ExposeData()
            {
                Scribe_References.Look(ref targetPawn, "targetPawn");
                Scribe_Collections.Look(ref trackedPawns, "trackedPawns", LookMode.Reference);
                Scribe_Values.Look(ref mapId, "mapId", -1);
                Scribe_Values.Look(ref targetPawnId, "targetPawnId", -1);
                Scribe_Collections.Look(ref pawnIds, "pawnIds", LookMode.Value);
                Scribe_Values.Look(ref requestKind, "requestKind", MouseDisasterRequestKind.SimpleMeal);
                Scribe_Values.Look(ref amount, "amount", 0);
                Scribe_Values.Look(ref createsIntelSite, "createsIntelSite", false);
                Scribe_Values.Look(ref intelSiteKind, "intelSiteKind", MouseDisasterIntelSiteKind.Treasure);
                Scribe_Values.Look(ref expireTick, "expireTick", 0);

                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    trackedPawns ??= new List<Pawn>();
                    pawnIds ??= new List<int>();
                    trackedPawns.RemoveAll(pawn => pawn == null);
                    pawnIds = pawnIds
                        .Concat(trackedPawns.Where(pawn => pawn != null).Select(pawn => pawn.thingIDNumber))
                        .Distinct()
                        .ToList();
                    if (targetPawn != null)
                    {
                        targetPawnId = targetPawn.thingIDNumber;
                    }
                }
            }
        }

        private static Dictionary<int, AidRequestState> ActiveAidRequestsByTargetPawnId = new Dictionary<int, AidRequestState>();

        internal static void ExposePendingStateData()
        {
            Scribe_Collections.Look(ref ActiveAidRequestsByTargetPawnId, "mouseDisaster_activeAidRequests", LookMode.Value, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ActiveAidRequestsByTargetPawnId ??= new Dictionary<int, AidRequestState>();
                List<int> invalidTargetIds = ActiveAidRequestsByTargetPawnId
                    .Where(pair => pair.Value == null || pair.Value.pawnIds.NullOrEmpty() || pair.Value.mapId < 0)
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < invalidTargetIds.Count; i++)
                {
                    ActiveAidRequestsByTargetPawnId.Remove(invalidTargetIds[i]);
                }
            }
        }

        internal static void ResetPendingState()
        {
            ActiveAidRequestsByTargetPawnId.Clear();
        }

        internal static void CleanupLoadedPendingState()
        {
            if (ActiveAidRequestsByTargetPawnId == null || ActiveAidRequestsByTargetPawnId.Count == 0)
            {
                return;
            }

            List<int> invalidTargetIds = ActiveAidRequestsByTargetPawnId
                .Where(pair => pair.Value == null || pair.Value.pawnIds.NullOrEmpty() || pair.Value.mapId < 0)
                .Select(pair => pair.Key)
                .ToList();
            for (int i = 0; i < invalidTargetIds.Count; i++)
            {
                ActiveAidRequestsByTargetPawnId.Remove(invalidTargetIds[i]);
            }
        }

        internal static void CleanupExpiredPendingState()
        {
            CleanupLoadedPendingState();
            ActiveAidRequestsByTargetPawnId ??= new Dictionary<int, AidRequestState>();

            HashSet<int> activeMapIds = new HashSet<int>(
                Find.Maps?.Where(map => map != null && !map.Disposed).Select(map => map.uniqueID) ??
                Enumerable.Empty<int>());
            List<int> staleTargetIds = ActiveAidRequestsByTargetPawnId
                .Where(pair => pair.Value == null || !activeMapIds.Contains(pair.Value.mapId))
                .Select(pair => pair.Key)
                .ToList();
            for (int i = 0; i < staleTargetIds.Count; i++)
            {
                if (ActiveAidRequestsByTargetPawnId.TryGetValue(staleTargetIds[i], out AidRequestState state))
                {
                    state.targetPawn = null;
                    state.trackedPawns?.Clear();
                    state.pawnIds?.Clear();
                }

                ActiveAidRequestsByTargetPawnId.Remove(staleTargetIds[i]);
            }
        }

        internal static bool HasActivePendingPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            int pawnId = pawn.thingIDNumber;
            return ActiveAidRequestsByTargetPawnId?.Values.Any(state => state != null &&
                (state.targetPawn == pawn || state.targetPawnId == pawnId || state.pawnIds?.Contains(pawnId) == true)) == true;
        }

        public static bool SupportsVisitorDelivery(MouseDisasterRequestKind kind)
        {
            return ResolveRequestedThingDef(kind) != null;
        }

        public static bool HasActiveAidRequestVisitors()
        {
            return ActiveAidRequestsByTargetPawnId.Count > 0;
        }

        public static string BuildVisitorDeliveryLetterText(IncidentDef incidentDef, Pawn target, MouseDisasterRequestKind kind, int amount, bool createsIntelSite)
        {
            ThingDef requestedThingDef = ResolveRequestedThingDef(kind);
            string baseText = incidentDef?.letterText ?? string.Empty;
            if (target == null || requestedThingDef == null)
            {
                return baseText;
            }

            string interactionText = "MouseDisaster_UI_AidDeliveryHint".Translate(target.LabelShortCap, amount, requestedThingDef.LabelCap).Resolve();
            string followupText = createsIntelSite
                ? "MouseDisaster_UI_AidIntelHint".Translate().Resolve()
                : "MouseDisaster_UI_AidDepartureHint".Translate().Resolve();
            string timeoutText = "MouseDisaster_UI_AidTimeoutHint".Translate().Resolve();
            return baseText + interactionText + followupText + timeoutText;
        }

        public static bool TrySpawnAidRequestVisitors(Map map, MouseDisasterRequestKind kind, int amount, bool createsIntelSite, MouseDisasterIntelSiteKind intelSiteKind, out Pawn targetPawn, out List<Pawn> allPawns, out string failureReason)
        {
            targetPawn = null;
            allPawns = new List<Pawn>();
            failureReason = string.Empty;

            ThingDef requestedThingDef = ResolveRequestedThingDef(kind);
            if (map == null || requestedThingDef == null)
            {
                failureReason = "MouseDisaster_UI_AidVisitorGenerationFailed".Translate().Resolve();
                return false;
            }

            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 entryCell) || !MouseDisasterUtility.TryFindFormerFaction(out Faction faction))
            {
                failureReason = "MouseDisaster_UI_VisitorEntryMissing".Translate().Resolve();
                return false;
            }

            MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
            MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(map, faction);

            int adultCount = Mathf.Clamp(1 + Mathf.RoundToInt(map.mapPawns.FreeColonistsSpawnedCount * 0.15f), 1, 3);
            int childCount = Find.Storyteller.difficulty.ChildrenAllowed ? Mathf.Clamp(Mathf.RoundToInt(adultCount * 0.5f), 0, 2) : 0;
            int limit = MouseDisasterUtility.LimitEventPawnCount(adultCount + childCount, childCount > 0 ? 2 : 1);
            adultCount = Mathf.Min(adultCount, limit - (childCount > 0 ? 1 : 0));
            childCount = Mathf.Min(childCount, limit - adultCount);

            targetPawn = MouseDisasterUtility.GenerateFactionRatkinPawn(
                MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult,
                faction,
                DevelopmentalStage.Adult,
                0.3f,
                allowViolenceDisabledTraits: false,
                fixedGender: Gender.Female);
            if (targetPawn == null)
            {
                failureReason = "MouseDisaster_UI_AidPawnGenerationFailed".Translate().Resolve();
                return false;
            }

            GenSpawn.Spawn(targetPawn, CellFinder.RandomClosewalkCellNear(entryCell, map, 5), map);
            PrepareAidVisitorPawn(targetPawn, requestedThingDef);
            allPawns.Add(targetPawn);

            for (int i = 1; i < adultCount; i++)
            {
                Pawn adult = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult, faction, DevelopmentalStage.Adult, 0.32f);
                if (adult == null)
                {
                    continue;
                }

                GenSpawn.Spawn(adult, CellFinder.RandomClosewalkCellNear(entryCell, map, 6), map);
                PrepareAidVisitorPawn(adult, requestedThingDef);
                allPawns.Add(adult);
            }

            List<Pawn> children = new List<Pawn>();
            for (int i = 0; i < childCount; i++)
            {
                Pawn child = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, faction, DevelopmentalStage.Child, 0.34f);
                if (child == null)
                {
                    continue;
                }

                GenSpawn.Spawn(child, CellFinder.RandomClosewalkCellNear(entryCell, map, 6), map);
                PrepareAidVisitorPawn(child, requestedThingDef);
                children.Add(child);
                allPawns.Add(child);
            }

            if (children.Count > 0)
            {
                MouseDisasterUtility.LinkIncidentParentToChildren(targetPawn, children);
            }

            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(targetPawn, out IntVec3 idleSpot))
            {
                idleSpot = map.Center;
            }

            LordJob_MouseDisasterBegForItems.MakeOrReplaceLord(
                faction,
                idleSpot,
                targetPawn,
                requestedThingDef,
                amount,
                map,
                allPawns);
            RegisterAidRequest(targetPawn, allPawns, kind, amount, createsIntelSite, intelSiteKind);
            return true;
        }

        public static void ProcessAidRequestVisitors(Map map)
        {
            if (map == null || ActiveAidRequestsByTargetPawnId.Count == 0)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            Dictionary<int, Pawn> pawnLookup = BuildSpawnedPawnLookup(map.mapPawns?.AllPawnsSpawned);
            List<int> targetIds = ActiveAidRequestsByTargetPawnId.Keys.ToList();
            for (int i = 0; i < targetIds.Count; i++)
            {
                int targetId = targetIds[i];
                if (!ActiveAidRequestsByTargetPawnId.TryGetValue(targetId, out AidRequestState state) || state == null || state.mapId != map.uniqueID)
                {
                    continue;
                }

                if (!pawnLookup.TryGetValue(targetId, out Pawn targetPawn) || targetPawn == null || targetPawn.Dead || !targetPawn.Spawned)
                {
                    List<Pawn> orphanedPawns = ResolveAidRequestPawns(state, pawnLookup);
                    if (orphanedPawns.Count > 0)
                    {
                        MouseDisasterUtility.MakeTravelAndExitLord(map, orphanedPawns, map.Center);
                    }

                    ActiveAidRequestsByTargetPawnId.Remove(targetId);
                    continue;
                }

                ThingDef requestedThingDef = ResolveRequestedThingDef(state.requestKind);
                if (requestedThingDef == null)
                {
                    List<Pawn> orphanedPawns = ResolveAidRequestPawns(state, pawnLookup);
                    if (orphanedPawns.Count > 0)
                    {
                        MouseDisasterUtility.MakeTravelAndExitLord(map, orphanedPawns, map.Center);
                    }

                    ActiveAidRequestsByTargetPawnId.Remove(targetId);
                    continue;
                }

                if (GiveItemsToPawnUtility.GetCountRemaining(targetPawn, requestedThingDef, state.amount) <= 0)
                {
                    NotifyAidRequestCompleted(map, state, targetPawn);
                    ActiveAidRequestsByTargetPawnId.Remove(targetId);
                    continue;
                }

                if (nowTick < state.expireTick)
                {
                    continue;
                }

                List<Pawn> pawns = ResolveAidRequestPawns(state, pawnLookup);
                if (pawns.Count > 0)
                {
                    if (!MouseDisasterUtility.TryFindFarEdgeCell(map, targetPawn.Position, out IntVec3 exitCell))
                    {
                        exitCell = map.Center;
                    }

                    MouseDisasterUtility.MakeTravelAndExitLord(map, pawns, exitCell);
                }

                Messages.Message("MouseDisaster_UI_AidVisitorTimedOut".Translate().Resolve(), targetPawn, MessageTypeDefOf.NeutralEvent, historical: false);
                ActiveAidRequestsByTargetPawnId.Remove(targetId);
            }
        }

        private static void RegisterAidRequest(Pawn targetPawn, IEnumerable<Pawn> pawns, MouseDisasterRequestKind requestKind, int amount, bool createsIntelSite, MouseDisasterIntelSiteKind intelSiteKind)
        {
            if (targetPawn?.Map == null)
            {
                return;
            }

            List<int> pawnIds = pawns?
                .Where(pawn => pawn != null && !pawn.Dead)
                .Select(pawn => pawn.thingIDNumber)
                .Distinct()
                .ToList() ?? new List<int>();
            if (pawnIds.Count == 0)
            {
                return;
            }

            ActiveAidRequestsByTargetPawnId[targetPawn.thingIDNumber] = new AidRequestState
            {
                targetPawn = targetPawn,
                trackedPawns = pawns?
                    .Where(pawn => pawn != null && !pawn.Dead)
                    .Distinct()
                    .ToList() ?? new List<Pawn>(),
                mapId = targetPawn.Map.uniqueID,
                targetPawnId = targetPawn.thingIDNumber,
                pawnIds = pawnIds,
                requestKind = requestKind,
                amount = amount,
                createsIntelSite = createsIntelSite,
                intelSiteKind = intelSiteKind,
                expireTick = (Find.TickManager?.TicksGame ?? 0) + AidRequestVisitDurationTicks
            };
        }

        private static void PrepareAidVisitorPawn(Pawn pawn, ThingDef requestedThingDef)
        {
            if (pawn == null)
            {
                return;
            }

            pawn.jobs?.StopAll();
            pawn.mindState?.mentalStateHandler?.Reset();
            pawn.mindState?.duty = null;
            pawn.guest?.SetGuestStatus(null, GuestStatus.Guest);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            RemoveRequestedItemsFromInventory(pawn, requestedThingDef);
        }

        private static void RemoveRequestedItemsFromInventory(Pawn pawn, ThingDef requestedThingDef)
        {
            if (pawn?.inventory?.innerContainer == null || requestedThingDef == null)
            {
                return;
            }

            for (int i = pawn.inventory.innerContainer.Count - 1; i >= 0; i--)
            {
                Thing thing = pawn.inventory.innerContainer[i];
                if (thing?.def == requestedThingDef)
                {
                    thing.Destroy();
                }
            }
        }

        private static Dictionary<int, Pawn> BuildSpawnedPawnLookup(IReadOnlyList<Pawn> pawns)
        {
            return MouseDisasterPawnLookup.Populate(pawns, new Dictionary<int, Pawn>());
        }

        private static List<Pawn> ResolveAidRequestPawns(AidRequestState state, Dictionary<int, Pawn> pawnLookup)
        {
            if (state?.pawnIds == null || pawnLookup == null)
            {
                return new List<Pawn>();
            }

            return state.pawnIds
                .Where(id => pawnLookup.TryGetValue(id, out Pawn pawn) && pawn != null && pawn.Spawned && !pawn.Dead)
                .Select(id => pawnLookup[id])
                .ToList();
        }

        private static void NotifyAidRequestCompleted(Map map, AidRequestState state, Pawn targetPawn)
        {
            if (state == null || targetPawn == null)
            {
                return;
            }

            if (state.createsIntelSite)
            {
                if (TryCreateIntelSite(map, state.intelSiteKind, out Site site, out string intelFailure))
                {
                    Find.LetterStack.ReceiveLetter("MouseDisaster_UI_IntelLetterLabel".Translate().Resolve(), "MouseDisaster_UI_AidIntelCompleted".Translate().Resolve(), LetterDefOf.PositiveEvent, site);
                }
                else
                {
                    Messages.Message(intelFailure, targetPawn, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            else
            {
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyNarrativeDelivery(state.trackedPawns);
                Messages.Message("MouseDisaster_UI_AidCompleted".Translate().Resolve(), targetPawn, MessageTypeDefOf.PositiveEvent, historical: false);
            }
        }
    }
}
