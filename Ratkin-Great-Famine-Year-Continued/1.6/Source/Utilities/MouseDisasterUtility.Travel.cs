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

        public static bool TryFindEntryCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReachColony(c), map, CellFinder.EdgeRoadChance_Ignore, out cell);
        }

        public static Job ExitMapJob(Pawn pawn, bool force = false)
        {
            if (!force && ShouldBlockIdleDeparture(pawn))
            {
                return null;
            }

            if (IsPendingAbandonedChild(pawn)) return null;
            if (pawn?.Map == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
            {
                return null;
            }

            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby)
            {
                return CreateGotoJob(pawn.Map.Center);
            }

            if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exitSpot))
            {
                if (!TryFindFarEdgeCell(pawn.Map, pawn.Position, out exitSpot))
                {
                    return CreateGotoJob(pawn.Map.Center);
                }
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, exitSpot);
            job.exitMapOnArrival = true;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }

        public static bool ShouldBlockIdleDeparture(Pawn pawn)
        {
            if (IsPendingAbandonedChild(pawn)) return true;
            if (IsAbandonedDeliveryPawn(pawn)) return !IsAbandonedDeliveryLeaving(pawn);
            var state = GameComponent_MouseDisasterEventBehavior.Component;
            if (state?.ManagesFoodVisit(pawn) == true) return !state.IsDeparting(pawn);
            return pawn != null &&
                   !IsPlayerAffiliatedRatkin(pawn) &&
                   IsMouseDisasterNeutralFaction(pawn.Faction) &&
                   MouseDisasterMod.Settings?.allowMouseDisasterFactionToLeaveWhenIdle != true;
        }

        public static Job TryCreatePathRecoveryJob(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Downed)
            {
                return null;
            }

            Thing tree = FindGnawableTree(pawn);
            if (tree != null)
            {
                return JobMaker.MakeJob(MouseDisasterDefOf.MouseDisaster_GnawTreeBark, tree);
            }

            Thing wall = FindGnawableWall(pawn);
            if (wall != null)
            {
                return JobMaker.MakeJob(MouseDisasterDefOf.MouseDisaster_GnawWall, wall);
            }

            return null;
        }

        public static Job CreateGotoJob(IntVec3 cell, bool exitMapOnArrival = false)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.exitMapOnArrival = exitMapOnArrival;
            return job;
        }

        public static bool TryFindFarEdgeCell(Map map, IntVec3 awayFrom, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(
                candidate => candidate.Walkable(map) && map.reachability.CanReach(awayFrom, candidate, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)) && (candidate - awayFrom).LengthHorizontalSquared > 400,
                map,
                CellFinder.EdgeRoadChance_Ignore,
                out cell);
        }

        public static void DestroyFailedIncidentPawns(IEnumerable<Pawn> pawns)
        {
            if (pawns == null)
            {
                return;
            }

            foreach (Pawn pawn in pawns)
            {
                if (pawn == null || pawn.Destroyed)
                {
                    continue;
                }

                MouseDisasterVisitorUtility.RemoveVisitorRecord(pawn);
                ClearTradeLeaderState(pawn);
                ResetBeggarState(pawn);
                UnmarkTradableChattel(pawn);
                ConsumeForcedPrisonerOnPurchase(pawn);
                ChildExchangeMoodPawnIds.Remove(pawn.thingIDNumber);
                ActiveChildExchangeByTraderId.Remove(pawn.thingIDNumber);
                ActiveAbandonedDeliveryByAdultId.Remove(pawn.thingIDNumber);

                if (pawn.Spawned)
                {
                    pawn.DeSpawnOrDeselect();
                }

                if (!pawn.Destroyed)
                {
                    pawn.Destroy();
                }
            }
        }

        public static void MakeTravelAndExitLord(Map map, IEnumerable<Pawn> pawns, IntVec3 travelDest, bool includeBabiesInExit = true)
        {
            List<Pawn> pawnList = pawns?.Where(pawn => pawn != null && pawn.Spawned).ToList();
            if (pawnList == null || pawnList.Count == 0)
            {
                return;
            }

            if (!includeBabiesInExit)
            {
                for (int i = 0; i < pawnList.Count; i++)
                {
                    if (pawnList[i].DevelopmentalStage == DevelopmentalStage.Baby)
                    {
                        PrepareNonCaravanBabyPawn(pawnList[i], travelDest);
                    }
                }

                pawnList = pawnList.Where(pawn => pawn.DevelopmentalStage != DevelopmentalStage.Baby).ToList();
                if (pawnList.Count == 0)
                {
                    return;
                }
            }

            Faction faction = pawnList
                .Select(pawn => pawn.Faction)
                .FirstOrDefault(currentFaction => currentFaction != null);
            foreach (Pawn pawn in pawnList) pawn.GetLord()?.RemovePawn(pawn);
            Lord lord = LordMaker.MakeNewLord(faction, new LordJob_TravelAndExit(travelDest), map, pawnList);
            if (lord != null)
            {
                TryAssignLeadYourPetTravelMouseEggs(lord);
            }
        }
    }
}
