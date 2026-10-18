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

        public static void RegisterAbandonedDelivery(Pawn adult, IEnumerable<Pawn> children, IntVec3 foodCell)
        {
            if (adult?.Map == null || children == null)
            {
                return;
            }

            List<int> childIds = new List<int>();
            List<Pawn> childPawns = new List<Pawn>();
            HashSet<int> seenIds = new HashSet<int>();
            foreach (Pawn child in children)
            {
                if (child == null || child.Dead)
                {
                    continue;
                }

                if (seenIds.Add(child.thingIDNumber))
                {
                    childIds.Add(child.thingIDNumber);
                }

                childPawns.Add(child);
                MouseDisasterPawnGroupUtility.HoldForDropoff(child, foodCell);
            }

            if (childIds.Count == 0)
            {
                return;
            }

            AbandonedDeliveryState state = new AbandonedDeliveryState
            {
                adult = adult,
                childPawns = childPawns,
                mapId = adult.Map.uniqueID,
                foodCell = foodCell,
                childPawnIds = childIds,
                adultHasLeft = false,
                adultArrivedAtDropoffTick = -1,
                startedTick = Find.TickManager?.TicksGame ?? 0
            };
            ActiveAbandonedDeliveryByAdultId[adult.thingIDNumber] = state;

            int spawnedChildCount = 0;
            for (int i = 0; i < childPawns.Count; i++)
            {
                if (childPawns[i] != null && childPawns[i].Spawned)
                {
                    spawnedChildCount++;
                }
            }

            if (MouseDisasterAbandonedDeliveryPolicy.ShouldAdultLeaveWhenChildrenAreOnMap(
                    adult.carryTracker?.CarriedThing is Pawn,
                    spawnedChildCount))
            {
                StartAbandonedDeliveryAdultExit(adult, state, adult.Map, timedOut: false);
            }
        }

        public static void CancelAbandonedDelivery(Pawn adult, IEnumerable<Pawn> children = null)
        {
            if (adult != null)
            {
                ActiveAbandonedDeliveryByAdultId.Remove(adult.thingIDNumber);
                TryEndLeadYourPetLeashForPet(adult);
            }

            if (children == null)
            {
                return;
            }

            foreach (Pawn child in children)
            {
                TryEndLeadYourPetLeashForPet(child);
            }
        }

        public static void ProcessAbandonedDeliveries(Map map)
        {
            if (map == null || ActiveAbandonedDeliveryByAdultId.Count == 0)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            IReadOnlyList<Pawn> mapPawns = map.mapPawns.AllPawnsSpawned;
            Dictionary<int, Pawn> pawnLookup = BuildSpawnedPawnLookup(mapPawns);
            ReusableIntList.Clear();
            foreach (int key in ActiveAbandonedDeliveryByAdultId.Keys)
            {
                ReusableIntList.Add(key);
            }

            for (int i = 0; i < ReusableIntList.Count; i++)
            {
                int adultId = ReusableIntList[i];
                if (!ActiveAbandonedDeliveryByAdultId.TryGetValue(adultId, out AbandonedDeliveryState state) || state == null || state.mapId != map.uniqueID)
                {
                    continue;
                }

                Pawn adult = ResolvePawnById(pawnLookup, adultId);
                if (!state.adultHasLeft && (adult == null || adult.Dead || !adult.Spawned))
                {
                    state.adultHasLeft = true;
                }

                if (adult?.carryTracker?.CarriedThing is Pawn carriedPawn)
                {
                    pawnLookup[carriedPawn.thingIDNumber] = carriedPawn;
                }

                ReusablePawnList.Clear();
                for (int j = 0; j < state.childPawnIds.Count; j++)
                {
                    Pawn child = ResolvePawnById(pawnLookup, state.childPawnIds[j]);
                    if (child != null && !child.Dead && !IsPlayerAffiliatedRatkin(child) &&
                        (child.Spawned || child.ParentHolder is Pawn_CarryTracker))
                    {
                        ReusablePawnList.Add(child);
                    }
                }

                if (adult != null && adult.Spawned && !adult.Dead)
                {
                    DetachFromReliefVisit(adult, IntVec3.Invalid);
                }

                for (int childIndex = 0; childIndex < ReusablePawnList.Count; childIndex++)
                {
                    Pawn child = ReusablePawnList[childIndex];
                    if (child.Spawned)
                    {
                        DetachFromReliefVisit(child, state.foodCell);
                    }
                }

                if (ReusablePawnList.Count == 0)
                {
                    if (adult != null && adult.Spawned && !adult.Dead && !IsPlayerAffiliatedRatkin(adult))
                        StartAbandonedDeliveryAdultExit(adult, state, map, timedOut: false);
                    ActiveAbandonedDeliveryByAdultId.Remove(adultId);
                    continue;
                }

                int spawnedChildCount = 0;
                bool adultCarryingChild = adult?.carryTracker?.CarriedThing is Pawn;
                foreach (Pawn child in ReusablePawnList)
                {
                    if (!child.Spawned || state.deliveredChildIds.Contains(child.thingIDNumber))
                    {
                        continue;
                    }

                    state.deliveredChildIds.Add(child.thingIDNumber);
                    TryReleaseLeadYourPetTradePawn(child);
                }

                for (int childIndex = 0; childIndex < ReusablePawnList.Count; childIndex++)
                {
                    if (ReusablePawnList[childIndex].Spawned)
                    {
                        spawnedChildCount++;
                    }
                }

                bool allChildrenArrived = spawnedChildCount > 0 &&
                                          ReusablePawnList.All(child => child.Spawned &&
                                              state.deliveredChildIds.Contains(child.thingIDNumber));
                for (int childIndex = 0; childIndex < ReusablePawnList.Count; childIndex++)
                {
                    Pawn child = ReusablePawnList[childIndex];
                    if (!child.Spawned)
                    {
                        continue;
                    }

                    if (state.deliveredChildIds.Contains(child.thingIDNumber))
                    {
                        continue;
                    }

                    if (!MouseDisasterAbandonedDeliveryPolicy.ShouldGiveChildSelfGoto(allChildrenArrived, child.Downed))
                    {
                        continue;
                    }

                    bool hasValidGoto = child.CurJobDef == JobDefOf.Goto &&
                                        child.CurJob != null &&
                                        child.CurJob.targetA.IsValid &&
                                        child.CurJob.targetA.Cell.InHorDistOf(state.foodCell, MouseDisasterAbandonedDeliveryPolicy.ChildDropoffRadius);
                    if (!hasValidGoto)
                    {
                        TryTakeAutoOrderedJob(child, CreateGotoJob(state.foodCell), JobTag.Misc, requireStarving: false);
                    }
                }

                if (!state.adultHasLeft)
                {
                    if (adult == null || adult.Dead || !adult.Spawned)
                    {
                        state.adultHasLeft = true;
                    }
                    else
                    {
                        if (state.startedTick < 0)
                        {
                            state.startedTick = nowTick;
                        }

                        if (adultCarryingChild)
                        {
                            TryDropCarriedAbandonedChild(adult);
                        }

                        int spawnedAfterDrop = 0;
                        for (int childIndex = 0; childIndex < ReusablePawnList.Count; childIndex++)
                        {
                            if (ReusablePawnList[childIndex].Spawned)
                            {
                                spawnedAfterDrop++;
                            }
                        }

                        int ticksSinceStart = nowTick - state.startedTick;
                        bool forceDismissAdult = MouseDisasterAbandonedDeliveryPolicy.ShouldForceDismissMobileNonInfant(
                            MouseDisasterMod.Settings?.forceDismissMobileNonInfantEventPawns == true,
                            CanForceDismissAsMobileNonInfant(adult),
                            adult.ageTracker?.AgeBiologicalYearsFloat ?? 0f);
                        bool approachTimeout = MouseDisasterAbandonedDeliveryPolicy.ShouldForceAdultLeaveAfterApproachStall(
                            false,
                            ticksSinceStart);
                        bool childrenOnMap = MouseDisasterAbandonedDeliveryPolicy.ShouldAdultLeaveWhenChildrenAreOnMap(
                            adult.carryTracker?.CarriedThing is Pawn,
                            spawnedAfterDrop);

                        if (childrenOnMap || forceDismissAdult || approachTimeout)
                        {
                            if (MouseDisasterAbandonedDeliveryPolicy.ShouldReleaseLeadYourPetDropoffLeashes(IsLeadYourPetEnabled, true))
                            {
                                for (int childIndex = 0; childIndex < ReusablePawnList.Count; childIndex++)
                                {
                                    TryEndLeadYourPetLeashForPet(ReusablePawnList[childIndex]);
                                }
                            }

                            LogAbandonedDeliveryWait(adult, state, map, ticksSinceStart, leaving: true,
                                forceDismissed: forceDismissAdult && !approachTimeout,
                                reason: childrenOnMap ? "children-on-map" : approachTimeout ? "approach-timeout" : "force-dismiss");
                            StartAbandonedDeliveryAdultExit(adult, state, map, timedOut: approachTimeout,
                                forceDismissed: forceDismissAdult && !childrenOnMap && !approachTimeout);
                        }
                        else
                        {
                            LogAbandonedDeliveryWait(adult, state, map, ticksSinceStart, leaving: false,
                                forceDismissed: false, reason: "waiting-to-place-children");
                            continue;
                        }
                    }
                }

                if (!allChildrenArrived && !state.adultHasLeft)
                {
                    continue;
                }

                if (!state.adultHasLeft || (adult != null && adult.Spawned && !adult.Dead))
                {
                    continue;
                }

                for (int childIndex = 0; childIndex < ReusablePawnList.Count; childIndex++)
                {
                    ConvertToAbandonedWildChild(ReusablePawnList[childIndex], state.foodCell);
                }

                ActiveAbandonedDeliveryByAdultId.Remove(adultId);
                Messages.Message("MouseDisaster_UI_AbandonedChildrenRemain".Translate().Resolve(), ReusablePawnList, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        public static bool IsPendingAbandonedChild(Pawn pawn)
        {
            return pawn != null && ActiveAbandonedDeliveryByAdultId.Values.Any(state =>
                state != null && state.childPawnIds.Contains(pawn.thingIDNumber));
        }

        public static bool IsAbandonedDeliveryPawn(Pawn pawn)
        {
            if (pawn == null || ActiveAbandonedDeliveryByAdultId == null || ActiveAbandonedDeliveryByAdultId.Count == 0)
            {
                return false;
            }

            return ActiveAbandonedDeliveryByAdultId.ContainsKey(pawn.thingIDNumber) || IsPendingAbandonedChild(pawn);
        }

        public static bool IsAbandonedDeliveryLeaving(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (pawn.GetLord()?.LordJob is LordJob_TravelAndExit ||
                pawn.GetLord()?.LordJob is LordJob_MouseDisasterFamilyExit ||
                pawn.GetLord()?.LordJob is LordJob_MouseDisasterDeparture)
            {
                return true;
            }

            return ActiveAbandonedDeliveryByAdultId != null &&
                   ActiveAbandonedDeliveryByAdultId.TryGetValue(pawn.thingIDNumber, out AbandonedDeliveryState state) &&
                   state != null &&
                   state.adultHasLeft;
        }

        private static void DetachFromReliefVisit(Pawn pawn, IntVec3 restoreDropoff)
        {
            Lord lord = pawn?.GetLord();
            bool wasVisit = lord?.LordJob is LordJob_MouseDisasterVisit;
            bool hadReliefDuty = pawn?.mindState?.duty?.def == MouseDisasterDefOf.MouseDisaster_ReliefVisit;
            if (wasVisit)
            {
                lord.RemovePawn(pawn);
                pawn.jobs?.StopAll();
            }

            if (hadReliefDuty && pawn.mindState != null)
            {
                pawn.mindState.duty = null;
            }

            if (restoreDropoff.IsValid && (wasVisit || hadReliefDuty) && pawn != null && pawn.Spawned && !pawn.Dead)
            {
                MouseDisasterPawnGroupUtility.HoldForDropoff(pawn, restoreDropoff);
            }
        }

        private static void StartAbandonedDeliveryAdultExit(Pawn adult, AbandonedDeliveryState state, Map map, bool timedOut, bool forceDismissed = false)
        {
            if (adult == null || state == null || map == null)
            {
                return;
            }

            bool alreadyLeaving = state.adultHasLeft;
            adult.GetLord()?.RemovePawn(adult);
            adult.mindState?.mentalStateHandler?.Reset();
            adult.jobs?.StopAll();
            if (adult.mindState != null)
            {
                adult.mindState.duty = null;
                adult.mindState.exitMapAfterTick = -1;
            }

            if (adult.GetLord()?.LordJob is not LordJob_MouseDisasterDeparture &&
                adult.GetLord()?.LordJob is not LordJob_TravelAndExit)
            {
                LordMaker.MakeNewLord(adult.Faction, new LordJob_MouseDisasterDeparture(), map, new[] { adult });
            }

            Job exitJob = ExitMapJob(adult, force: true);
            if (exitJob != null && adult.jobs != null)
            {
                adult.jobs.StartJob(exitJob, JobCondition.InterruptForced);
            }

            state.adultHasLeft = true;
            MouseDisasterTrace.Log("O-002 abandoning mother exit; timedOut=" + timedOut +
                "; forceDismissed=" + forceDismissed + "; alreadyLeaving=" + alreadyLeaving + "; " +
                MouseDisasterTrace.DescribePawn(adult) + "; " + MouseDisasterTrace.DescribeMap(map));
            if (alreadyLeaving)
            {
                return;
            }

            string text = forceDismissed
                ? "MouseDisaster_UI_AbandoningMotherForceDismissed".Translate().Resolve()
                : timedOut
                    ? "MouseDisaster_UI_AbandoningMotherTimedOut".Translate().Resolve()
                    : "MouseDisaster_UI_AbandoningMotherLeft".Translate().Resolve();
            Messages.Message(
                text,
                adult,
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }

        private static bool TryDropCarriedAbandonedChild(Pawn adult)
        {
            if (adult?.carryTracker?.CarriedThing is not Pawn || adult.Map == null)
            {
                return false;
            }

            return adult.carryTracker.TryDropCarriedThing(adult.Position, ThingPlaceMode.Near, out _);
        }

        private static void KeepAbandonedDeliveryPawnAtDropoff(Pawn pawn, IntVec3 foodCell)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || !foodCell.IsValid || IsAbandonedDeliveryLeaving(pawn))
            {
                return;
            }

            if (!pawn.Position.InHorDistOf(foodCell, MouseDisasterAbandonedDeliveryPolicy.AdultDropoffRadius))
            {
                TryTakeAutoOrderedJob(pawn, CreateGotoJob(foodCell), JobTag.Misc, requireStarving: false);
                return;
            }

            if (pawn.CurJobDef == JobDefOf.Wait || pawn.CurJobDef == JobDefOf.Wait_MaintainPosture)
            {
                return;
            }

            Job waitJob = JobMaker.MakeJob(JobDefOf.Wait, 1800, false);
            TryTakeAutoOrderedJob(pawn, waitJob, JobTag.Misc, requireStarving: false);
        }

        private static bool IsAbandonedDeliveryDropoffSatisfied(Pawn pawn, IntVec3 foodCell, Map map, bool forAdult)
        {
            if (pawn?.Spawned != true || pawn.Dead || !foodCell.IsValid)
            {
                return false;
            }

            Map dropoffMap = map ?? pawn.Map;
            Area_MouseDisasterRelief reliefArea = GetReliefArea(dropoffMap);
            bool dropoffInReliefArea = dropoffMap != null && reliefArea != null && reliefArea.TrueCount > 0 &&
                                       foodCell.InBounds(dropoffMap) && reliefArea[foodCell];
            bool pawnInReliefArea = reliefArea != null && pawn.Position.IsValid && reliefArea[pawn.Position];
            float radius = MouseDisasterAbandonedDeliveryPolicy.DropoffRadius(forAdult, dropoffInReliefArea, pawnInReliefArea);
            return MouseDisasterAbandonedDeliveryPolicy.IsDropoffSatisfied(pawn.Position.InHorDistOf(foodCell, radius));
        }

        private static bool CanForceDismissAsMobileNonInfant(Pawn pawn)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   pawn.Spawned &&
                   !pawn.Downed &&
                   pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) == true;
        }

        private static void LogAbandonedDeliveryWait(Pawn adult, AbandonedDeliveryState state, Map map, int ticksWaiting, bool leaving, bool forceDismissed, string reason)
        {
            if (state == null)
            {
                return;
            }

            string message = "O-002 abandoning mother; reason=" + (reason ?? "unknown") +
                             "; " + MouseDisasterTrace.DescribePawn(adult) +
                             "; foodCell=" + state.foodCell +
                             "; reliefArea=" + MapHasReliefArea(map) +
                             "; waitingTicks=" + ticksWaiting +
                             "; delivered=" + (state.deliveredChildIds?.Count ?? 0) +
                             "/" + (state.childPawnIds?.Count ?? 0) +
                             "; leaving=" + leaving +
                             "; forceDismissed=" + forceDismissed;
            MouseDisasterTrace.Log(message);
            if (!state.loggedWaitStall)
            {
                state.loggedWaitStall = true;
                Log.Message("[MouseDisaster] " + message);
            }
        }

        public static int ForceDismissMobileNonInfantEventPawns(Map map, bool onlyWaiting = false)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null)
            {
                return 0;
            }

            int dismissed = 0;
            List<Pawn> spawned = map.mapPawns.AllPawnsSpawned.ToList();
            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn pawn = spawned[i];
                if (!ShouldForceDismissMobileNonInfantEventPawn(pawn))
                {
                    continue;
                }

                if (onlyWaiting &&
                    pawn.CurJobDef != JobDefOf.Wait &&
                    pawn.CurJobDef != JobDefOf.Wait_MaintainPosture)
                {
                    continue;
                }

                if (ActiveAbandonedDeliveryByAdultId.TryGetValue(pawn.thingIDNumber, out AbandonedDeliveryState state) &&
                    state != null)
                {
                    StartAbandonedDeliveryAdultExit(pawn, state, map, timedOut: false, forceDismissed: true);
                    dismissed++;
                    continue;
                }

                pawn.GetLord()?.RemovePawn(pawn);
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
                if (pawn.mindState != null)
                {
                    pawn.mindState.duty = null;
                    pawn.mindState.exitMapAfterTick = -1;
                }

                LordMaker.MakeNewLord(pawn.Faction, new LordJob_MouseDisasterDeparture(), map, new[] { pawn });
                Job exitJob = ExitMapJob(pawn, force: true);
                if (exitJob != null && pawn.jobs != null)
                {
                    pawn.jobs.StartJob(exitJob, JobCondition.InterruptForced);
                }

                GameComponent_MouseDisasterEventBehavior.Component?.RequestDeparture(pawn);
                dismissed++;
                MouseDisasterTrace.Log("force-dismissed mobile non-infant event pawn; " +
                    MouseDisasterTrace.DescribePawn(pawn) + "; " + MouseDisasterTrace.DescribeMap(map));
            }

            return dismissed;
        }

        private static bool ShouldForceDismissMobileNonInfantEventPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || IsPlayerAffiliatedRatkin(pawn) || IsPendingAbandonedChild(pawn))
            {
                return false;
            }

            if (IsAbandonedDeliveryLeaving(pawn))
            {
                return false;
            }

            if (!MouseDisasterAbandonedDeliveryPolicy.ShouldForceDismissMobileNonInfant(
                    true,
                    CanForceDismissAsMobileNonInfant(pawn),
                    pawn.ageTracker?.AgeBiologicalYearsFloat ?? 0f))
            {
                return false;
            }

            return IsAbandonedDeliveryPawn(pawn) ||
                   GameComponent_MouseDisasterEventBehavior.Component?.TryGetGroup(pawn, out _) == true;
        }

        internal static void NotifyAbandonedChildDropped(Pawn child)
        {
            if (child?.Spawned != true || child.Dead || IsPlayerAffiliatedRatkin(child)) return;
            foreach (AbandonedDeliveryState state in ActiveAbandonedDeliveryByAdultId.Values)
            {
                if (state == null || state.mapId != child.Map.uniqueID || !state.childPawnIds.Contains(child.thingIDNumber) ||
                    state.deliveredChildIds.Contains(child.thingIDNumber) ||
                    !child.Position.InHorDistOf(state.foodCell, 3f)) continue;
                state.deliveredChildIds.Add(child.thingIDNumber);
                TryReleaseLeadYourPetTradePawn(child);
            }
        }

        private static bool TryStartAbandonedDeliveryCarryJob(Pawn adult, IReadOnlyList<Pawn> children, IntVec3 foodCell)
        {
            if (adult == null || adult.Dead || !adult.Spawned || children == null || !foodCell.IsValid)
            {
                return false;
            }

            if (adult.CurJobDef == JobDefOf.DeliverToCell &&
                adult.CurJob != null &&
                adult.CurJob.targetB.IsValid &&
                adult.CurJob.targetB.Cell.InHorDistOf(foodCell, MouseDisasterAbandonedDeliveryPolicy.AdultDropoffRadius))
            {
                return true;
            }

            Pawn targetChild = children
                .Where(child => child != null &&
                                !child.Dead &&
                                child.Spawned &&
                                MouseDisasterAbandonedDeliveryPolicy.ShouldCarryUndeliveredChild(
                                    IsLeadYourPetEnabled, false, child.Downed) &&
                                !IsAbandonedDeliveryDropoffSatisfied(child, foodCell, adult.Map, forAdult: false) &&
                                adult.CanReserveAndReach(child, PathEndMode.OnCell, Danger.Deadly))
                .OrderBy(child => adult.Position.DistanceToSquared(child.Position))
                .FirstOrDefault();
            if (targetChild == null)
            {
                return false;
            }

            Job deliverJob = JobMaker.MakeJob(JobDefOf.DeliverToCell, targetChild, foodCell);
            deliverJob.locomotionUrgency = LocomotionUrgency.Jog;
            deliverJob.count = 1;
            return TryTakeAutoOrderedJob(adult, deliverJob, JobTag.Misc, requireStarving: false);
        }

        private static bool AreAllPawnsNearCell(IReadOnlyList<Pawn> pawns, IntVec3 cell, float maxDistance)
        {
            if (pawns == null || pawns.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Dead || !pawn.Position.InHorDistOf(cell, maxDistance))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ConvertToAbandonedWildChild(Pawn pawn, IntVec3 fallbackCell)
        {
            if (pawn == null || pawn.Dead || IsPlayerAffiliatedRatkin(pawn))
            {
                return;
            }

            if (pawn.Faction != null)
            {
                pawn.SetFaction(null);
            }

            if (MouseDisasterDefOf.MouseDisaster_WildRatkinChild != null)
            {
                pawn.kindDef = MouseDisasterDefOf.MouseDisaster_WildRatkinChild;
            }

            pawn.guest?.SetGuestStatus(null, GuestStatus.Guest);
            pawn.mindState?.mentalStateHandler?.Reset();
            if (pawn.mindState != null) pawn.mindState.duty = null;
            ResetBeggarState(pawn);
            SetMouseDisasterFoodLevel(pawn);
            if (pawn.Spawned)
            {
                TryTakeAutoOrderedJob(pawn, CreateGotoJob(fallbackCell), JobTag.Misc, requireStarving: false);
            }
        }

        public static bool HasActiveAbandonedDeliveryState()
        {
            return ActiveAbandonedDeliveryByAdultId.Count > 0;
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    internal static class MouseDisasterAbandonedDeliveryGetFoodPatch
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (!MouseDisasterUtility.IsAbandonedDeliveryPawn(pawn) ||
                MouseDisasterUtility.IsAbandonedDeliveryLeaving(pawn))
            {
                return true;
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class MouseDisasterAbandonedDeliveryApparelPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo vanilla = AccessTools.Method(typeof(JobGiver_OptimizeApparel), "TryGiveJob");
            if (vanilla != null)
            {
                yield return vanilla;
            }

            Type musType = AccessTools.TypeByName("MUS_StylingStation.JobGiver_OptimizeApparel_MUS");
            MethodInfo musMethod = musType == null ? null : AccessTools.Method(musType, "TryGiveJob");
            if (musMethod != null)
            {
                yield return musMethod;
            }
        }

        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn?.outfits != null || !MouseDisasterUtility.IsAbandonedDeliveryPawn(pawn))
            {
                return true;
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class MouseDisasterAbandonedChildDropPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) });
            yield return AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                new[] { typeof(IntVec3), typeof(int), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) });
        }
        public static void Postfix(bool __result, Thing resultingThing)
        {
            if (__result && resultingThing is Pawn child) MouseDisasterUtility.NotifyAbandonedChildDropped(child);
        }
    }
}
