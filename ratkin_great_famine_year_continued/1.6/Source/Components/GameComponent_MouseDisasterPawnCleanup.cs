using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    public partial class GameComponent_MouseDisasterNarrative
    {
        private const int MouseDisasterPawnCleanupIntervalTicks = GenDate.TicksPerDay;
        private const int MouseDisasterWorldPawnGraceTicks = GenDate.TicksPerDay * 30;
        private const int CompletedN004RecordCleanupGraceTicks = GenDate.TicksPerDay;
        private const int N004RescueRewardGraceTicks = GenDate.TicksPerYear;

        private int lastMouseDisasterPawnCleanupDay = -1;

        private void TryRunDailyMouseDisasterPawnCleanup()
        {
            TickManager tickManager = Find.TickManager;
            if (tickManager == null)
            {
                return;
            }

            int now = tickManager.TicksGame;
            int day = now / MouseDisasterPawnCleanupIntervalTicks;
            if (day <= 0 || day == lastMouseDisasterPawnCleanupDay)
            {
                return;
            }

            lastMouseDisasterPawnCleanupDay = day;
            CleanupExpiredMouseDisasterPawns(now);
        }

        private void CleanupExpiredMouseDisasterPawns(int now)
        {
            CleanupN004Records(now);
            CleanupN005Records();
            CleanupN007Records();
            CleanupCompletedStoryTaskReferences();
            MouseDisasterUtility.CleanupExpiredPendingState();
            MouseDisasterPhase2Utility.CleanupExpiredPendingState();
            Current.Game?.GetComponent<GameComponent_MouseDisasterGeneRestoreState>()?.CleanupExpiredMarkers();

            HashSet<Pawn> protectedPawns = new HashSet<Pawn>();
            AddActiveNarrativePawnReferences(protectedPawns);
            CleanupExpiredWorldPawns(protectedPawns);
        }

        private void CleanupN004Records(int now)
        {
            if (n004Records == null)
            {
                return;
            }

            HashSet<Pawn> regretMothers = new HashSet<Pawn>();
            for (int i = n004Records.Count - 1; i >= 0; i--)
            {
                MouseDisasterN004Record record = n004Records[i];
                if (record == null)
                {
                    n004Records.RemoveAt(i);
                    continue;
                }

                if (record.outcome != MouseDisasterN004Outcome.Pending)
                {
                    record.children?.RemoveAll(child => child == null || child.Dead || child.Destroyed);
                    if (record.outcome == MouseDisasterN004Outcome.FamilySeparated &&
                        (record.mother?.Dead == true || record.mother?.Destroyed == true))
                    {
                        record.mother = null;
                    }
                }

                if (!ShouldRetainN004Record(record, now))
                {
                    n004Records.RemoveAt(i);
                    continue;
                }

                if (record.outcome == MouseDisasterN004Outcome.ChildDied &&
                    record.mother != null && !regretMothers.Add(record.mother))
                {
                    n004Records.RemoveAt(i);
                }
            }
        }

        private static bool ShouldRetainN004Record(MouseDisasterN004Record record, int now)
        {
            if (record == null)
            {
                return false;
            }

            switch (record.outcome)
            {
                case MouseDisasterN004Outcome.Pending:
                    return true;
                case MouseDisasterN004Outcome.FamilySurvived:
                    return IsLivePawn(record.mother) && record.children?.Any(child =>
                        IsLivePawn(child) && child.ageTracker != null && child.ageTracker.AgeBiologicalYearsFloat < 18f) == true;
                case MouseDisasterN004Outcome.ChildDied:
                    return IsLivePawn(record.mother);
                case MouseDisasterN004Outcome.ChildSurvived:
                {
                    int breakWindowEnd = record.missingMotherBreakWindowUntilTick >= 0
                        ? record.missingMotherBreakWindowUntilTick
                        : record.createdTick + GenDate.TicksPerDay;
                    return now < breakWindowEnd + CompletedN004RecordCleanupGraceTicks;
                }
                case MouseDisasterN004Outcome.FamilySeparated:
                    if (record.revisitDecision == MouseDisasterN004RevisitDecision.Rescued)
                    {
                        if (record.rescueRewardGranted)
                        {
                            return false;
                        }

                        int rewardDueTick = record.rescueRewardDueTick >= 0
                            ? record.rescueRewardDueTick
                            : record.createdTick + GenDate.TicksPerYear * 4;
                        return now < rewardDueTick + N004RescueRewardGraceTicks;
                    }

                    if (record.revisitDecision == MouseDisasterN004RevisitDecision.Pending)
                    {
                        int revisitDeadline = record.revisitDeadlineTick >= 0
                            ? record.revisitDeadlineTick
                            : record.createdTick + GenDate.TicksPerYear * 4;
                        return now < revisitDeadline + CompletedN004RecordCleanupGraceTicks &&
                               (IsLivePawn(record.mother) || record.children?.Count > 0);
                    }

                    return false;
                default:
                    return false;
            }
        }

        private void CleanupN005Records()
        {
            if (n005Records == null)
            {
                return;
            }

            for (int i = n005Records.Count - 1; i >= 0; i--)
            {
                MouseDisasterN005Record record = n005Records[i];
                if (record == null)
                {
                    n005Records.RemoveAt(i);
                    continue;
                }

                if (record.outcome == MouseDisasterN005Outcome.Pending ||
                    record.outcome == MouseDisasterN005Outcome.Accepted && !record.careResolved)
                {
                    continue;
                }

                n005Records.RemoveAt(i);
            }
        }

        private void CleanupN007Records()
        {
            if (n007Records == null)
            {
                return;
            }

            for (int i = n007Records.Count - 1; i >= 0; i--)
            {
                MouseDisasterN007Record record = n007Records[i];
                if (record == null || record.phase == MouseDisasterN007Phase.Resolved)
                {
                    n007Records.RemoveAt(i);
                    continue;
                }

                CleanupN007PawnReferences(record);
            }
        }

        private static void CleanupN007PawnReferences(MouseDisasterN007Record record)
        {
            if (record?.pawnRecords == null)
            {
                return;
            }

            for (int i = record.pawnRecords.Count - 1; i >= 0; i--)
            {
                MouseDisasterN007PawnRecord pawnRecord = record.pawnRecords[i];
                if (pawnRecord == null || (pawnRecord.pawn == null && pawnRecord.pawnId < 0))
                {
                    record.pawnRecords.RemoveAt(i);
                    continue;
                }

                if (pawnRecord.died || pawnRecord.handled || pawnRecord.leftMap && !pawnRecord.recovered)
                {
                    pawnRecord.pawn = null;
                }
            }
        }

        private void CleanupCompletedStoryTaskReferences()
        {
            if (returnPhase >= 3)
            {
                recoveredVisitor = null;
                returnMapId = -1;
                returnDueTick = 0;
                returnDeadline = 0;
            }

            if (envoyPhase >= 4 && (!HasEnvoyContact || (relicStarted && relicOutcome != 0) || !NarrativeEnabled("N009")))
            {
                envoy = null;
                envoyMapId = -1;
            }
        }

        private void AddActiveNarrativePawnReferences(HashSet<Pawn> protectedPawns)
        {
            if (protectedPawns == null)
            {
                return;
            }

            if (n004Records != null)
            {
                foreach (MouseDisasterN004Record record in n004Records)
                {
                    AddPawnReference(protectedPawns, record?.mother);
                    if (record?.children != null)
                        foreach (Pawn child in record.children) AddPawnReference(protectedPawns, child);
                }
            }

            if (n005Records != null)
            {
                foreach (MouseDisasterN005Record record in n005Records.Where(record =>
                    record != null && (record.outcome == MouseDisasterN005Outcome.Pending ||
                    record.outcome == MouseDisasterN005Outcome.Accepted && !record.careResolved)))
                {
                    AddPawnReference(protectedPawns, record.trader);
                    AddPawnReference(protectedPawns, record.offeredBaby);
                    if (record.exchangeChildren != null)
                        foreach (Pawn child in record.exchangeChildren) AddPawnReference(protectedPawns, child);
                    if (record.care != null)
                        foreach (NarrativePawnObservation observation in record.care)
                            AddPawnReference(protectedPawns, observation?.pawn);
                }
            }

            if (n007Records != null)
            {
                foreach (MouseDisasterN007Record record in n007Records.Where(record =>
                    record != null && record.phase != MouseDisasterN007Phase.Resolved))
                {
                    if (record.pawnRecords == null) continue;
                    foreach (MouseDisasterN007PawnRecord pawnRecord in record.pawnRecords)
                    {
                        if (pawnRecord == null || pawnRecord.died || pawnRecord.handled ||
                            pawnRecord.leftMap && !pawnRecord.recovered)
                        {
                            continue;
                        }

                        AddPawnReference(protectedPawns, pawnRecord.pawn);
                    }
                }
            }

            if (narrativeVisits != null)
            {
                foreach (NarrativeVisit visit in narrativeVisits)
                {
                    if (visit?.people == null) continue;
                    foreach (NarrativePawnObservation observation in visit.people)
                        AddPawnReference(protectedPawns, observation?.pawn);
                }
            }

            if (envoyPhase > 0 && envoyPhase < 4)
                AddPawnReference(protectedPawns, envoy);
            if (relicStarted && relicOutcome == 0)
                AddPawnReference(protectedPawns, envoy);
            if (returnPhase == 1 || returnPhase == 2)
                AddPawnReference(protectedPawns, recoveredVisitor);
        }

        private static void AddPawnReference(HashSet<Pawn> protectedPawns, Pawn pawn)
        {
            if (pawn != null)
            {
                protectedPawns.Add(pawn);
            }
        }

        private static bool IsLivePawn(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed;
        }

        private static void CleanupExpiredWorldPawns(HashSet<Pawn> protectedPawns)
        {
            WorldPawns worldPawns = Find.WorldPawns;
            if (worldPawns == null || worldPawns.gc == null || worldPawns.AllPawnsAliveOrDead == null)
            {
                return;
            }

            int nowAbs = GenTicks.TicksAbs;
            List<Pawn> candidates = worldPawns.AllPawnsAliveOrDead
                .Where(pawn => IsExpiredWorldPawnCandidate(pawn, nowAbs))
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            Dictionary<Pawn, string> vanillaKeptPawns;
            try
            {
                vanillaKeptPawns = worldPawns.gc.AccumulatePawnGCDataImmediate();
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Daily world pawn cleanup could not inspect vanilla GC state: " + exception);
                return;
            }

            int discardedCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn pawn = candidates[i];
                if (protectedPawns.Contains(pawn) || IsProtectedByMouseDisasterState(pawn) ||
                    vanillaKeptPawns.ContainsKey(pawn) || !worldPawns.Contains(pawn))
                {
                    continue;
                }

                try
                {
                    worldPawns.RemoveAndDiscardPawnViaGC(pawn);
                    discardedCount++;
                }
                catch (Exception exception)
                {
                    Log.Error("[MouseDisaster] Daily world pawn cleanup failed for " + pawn + ": " + exception);
                }
            }

            if (discardedCount > 0)
            {
                Log.Message("[MouseDisaster] Daily cleanup discarded " + discardedCount + " expired world pawns.");
            }
        }

        private static bool IsExpiredWorldPawnCandidate(Pawn pawn, int nowAbs)
        {
            return pawn != null &&
                   !pawn.Discarded &&
                   MouseDisasterUtility.IsMouseDisasterPawn(pawn) &&
                   !MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn) &&
                   !pawn.Spawned &&
                   !pawn.SpawnedOrAnyParentSpawned &&
                   pawn.MapHeld == null &&
                   !pawn.IsCaravanMember() &&
                   pawn.becameWorldPawnTickAbs >= 0 &&
                   nowAbs >= pawn.becameWorldPawnTickAbs &&
                   nowAbs - pawn.becameWorldPawnTickAbs >= MouseDisasterWorldPawnGraceTicks &&
                   Find.WorldPawns != null &&
                   Find.WorldPawns.Contains(pawn) &&
                   !Find.WorldPawns.ForcefullyKeptPawns.Contains(pawn);
        }

        private static bool IsProtectedByMouseDisasterState(Pawn pawn)
        {
            if (pawn == null)
            {
                return true;
            }

            if (MouseDisasterUtility.HasActivePendingPawn(pawn) ||
                MouseDisasterPhase2Utility.HasActivePendingPawn(pawn))
            {
                return true;
            }

            bool activePawn = pawn.Spawned || pawn.MapHeld != null || pawn.GetCaravan() != null;
            if (activePawn && (MouseDisasterUtility.IsMouseDisasterTradePawn(pawn) ||
                MouseDisasterUtility.HasChildExchangeMoodMarker(pawn) ||
                MouseDisasterUtility.IsMouseDisasterIncidentChild(pawn)))
            {
                return true;
            }

            GameComponent_MouseDisasterEventBehavior behavior = GameComponent_MouseDisasterEventBehavior.Component;
            if (behavior?.HasPawnReference(pawn) == true)
            {
                return true;
            }

            GameComponent_MouseDisasterVisitorControl visitors =
                Current.Game?.GetComponent<GameComponent_MouseDisasterVisitorControl>();
            if (visitors?.IsManagedVisitor(pawn) == true)
            {
                return true;
            }

            GameComponent_MouseDisasterGeneRestoreState geneState =
                Current.Game?.GetComponent<GameComponent_MouseDisasterGeneRestoreState>();
            return geneState?.HasManualRemovalMarker(pawn) == true;
        }
    }
}
