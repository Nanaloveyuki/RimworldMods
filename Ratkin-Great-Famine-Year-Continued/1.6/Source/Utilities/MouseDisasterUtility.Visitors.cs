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

        public static void RegisterSiegeBeggar(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            int pawnId = pawn.thingIDNumber;
            SiegeBeggarPawnIds.Add(pawnId);
            SiegeBeggarStoleFoodSuccess.Remove(pawnId);
        }

        public static void RegisterStrongSiegePawn(Pawn pawn)
        {
            if (pawn != null)
            {
                StrongSiegePawnIds.Add(pawn.thingIDNumber);
            }
        }

        public static bool IsStrongSiegePawn(Pawn pawn)
        {
            return pawn != null && StrongSiegePawnIds.Contains(pawn.thingIDNumber);
        }

        public static void RegisterAirDropStayPawn(Pawn pawn, int stayTicks)
        {
            if (pawn == null)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            AirDropStayUntilTickByPawnId[pawn.thingIDNumber] = nowTick + Mathf.Max(1, stayTicks);
        }

        public static bool MustStayForAirDropError(Pawn pawn)
        {
            if (pawn == null || !AirDropStayUntilTickByPawnId.TryGetValue(pawn.thingIDNumber, out int untilTick))
            {
                return false;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (nowTick < untilTick)
            {
                return true;
            }

            AirDropStayUntilTickByPawnId.Remove(pawn.thingIDNumber);
            return false;
        }

        public static void MarkSiegeBeggarStoleFood(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            SiegeBeggarStoleFoodSuccess.Add(pawn.thingIDNumber);
        }

        public static bool IsSiegeBeggar(Pawn pawn)
        {
            return pawn != null && SiegeBeggarPawnIds.Contains(pawn.thingIDNumber);
        }

        public static bool HasSiegeBeggarLeaveCondition(Pawn pawn)
        {
            if (pawn == null)
            {
                return true;
            }

            if (pawn.Dead || pawn.MapHeld == null)
            {
                ResetBeggarState(pawn);
                return true;
            }

            int pawnId = pawn.thingIDNumber;
            if (SiegeBeggarStoleFoodSuccess.Contains(pawnId))
            {
                return true;
            }

            return CountFoodInInventory(pawn) >= SiegeBeggarLeaveFoodUnits;
        }

        private static void RemoveSiegeBeggarState(int pawnId)
        {
            SiegeBeggarPawnIds.Remove(pawnId);
            SiegeBeggarStoleFoodSuccess.Remove(pawnId);
            StrongSiegePawnIds.Remove(pawnId);
        }

        public static bool TryStartVisitorAssault(Pawn triggerPawn)
        {
            if (GameComponent_MouseDisasterEventBehavior.Component?.TryGetGroup(triggerPawn, out _) == true) return false;
            if (triggerPawn?.Map == null || triggerPawn.Downed || triggerPawn.Dead || !triggerPawn.Spawned)
            {
                return false;
            }

            Map map = triggerPawn.Map;
            Faction faction = triggerPawn.Faction;
            if (faction == null && !TryFindFormerFaction(out faction))
            {
                return false;
            }

            if (faction == null)
            {
                return false;
            }

            List<Pawn> attackers = map.mapPawns.AllPawnsSpawned
                .Where(pawn =>
                    pawn != null &&
                    !pawn.Dead &&
                    !pawn.Downed &&
                    pawn.Spawned &&
                    pawn.Faction == faction &&
                    (IsStrongSiegePawn(pawn) || IsThiefPawn(pawn) || IsBeggarPawn(pawn)))
                .ToList();
            if (attackers.Count == 0)
            {
                return false;
            }

            MakeFactionHostileToPlayer(faction);
            for (int i = 0; i < attackers.Count; i++)
            {
                Pawn pawn = attackers[i];
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
            }

            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false, canSteal: true, breachers: true), map, attackers);
            return true;
        }

        public static void PrepareNonCaravanBabyPawn(Pawn pawn, IntVec3 anchorCell)
        {
            if (pawn == null || pawn.DevelopmentalStage != DevelopmentalStage.Baby)
            {
                return;
            }

            if (pawn.mindState != null)
            {
                pawn.mindState.canFleeIndividual = false;
                pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 20;
            }

            if (pawn.Spawned && anchorCell.IsValid)
            {
                TryTakeAutoOrderedJob(pawn, CreateGotoJob(anchorCell), JobTag.Misc, requireStarving: false);
            }
        }
    }
}
