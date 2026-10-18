using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public enum MouseDisasterN007Phase
    {
        Decision,
        Quarantined,
        Monitoring,
        ReleasePending,
        RecoveryDecision,
        Resolved
    }

    public enum MouseDisasterN007Outcome
    {
        Pending,
        RecoveredAndLeft,
        RecoveredAndStayed,
        Released,
        QuarantineBroken,
        Died,
        Handled
    }

    public enum MouseDisasterN007InitialDecision
    {
        Quarantine,
        Release,
        Defer
    }

    public enum MouseDisasterN007RecoveryDecision
    {
        Release,
        Accept
    }

    public enum MouseDisasterN007LetterStage
    {
        Entry,
        Recovery
    }

    public sealed class MouseDisasterN007PawnRecord : IExposable
    {
        public Pawn pawn;
        public int pawnId = -1;
        public bool recovered;
        public bool died;
        public bool handled;
        public bool leftMap;
        public int unresolvedSinceTick = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref pawnId, "pawnId", -1);
            Scribe_Values.Look(ref recovered, "recovered", false);
            Scribe_Values.Look(ref died, "died", false);
            Scribe_Values.Look(ref handled, "handled", false);
            Scribe_Values.Look(ref leftMap, "leftMap", false);
            Scribe_Values.Look(ref unresolvedSinceTick, "unresolvedSinceTick", -1);
        }
    }

    public sealed class MouseDisasterN007Record : IExposable
    {
        public int mapId = -1;
        public int createdTick;
        public MouseDisasterN007Phase phase = MouseDisasterN007Phase.Decision;
        public MouseDisasterN007Outcome outcome = MouseDisasterN007Outcome.Pending;
        public List<MouseDisasterN007PawnRecord> pawnRecords = new List<MouseDisasterN007PawnRecord>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            Scribe_Values.Look(ref phase, "phase", MouseDisasterN007Phase.Decision);
            Scribe_Values.Look(ref outcome, "outcome", MouseDisasterN007Outcome.Pending);
            Scribe_Collections.Look(ref pawnRecords, "pawnRecords", LookMode.Deep);
        }
    }

    public partial class GameComponent_MouseDisasterNarrative
    {
        private const int N007ReferenceGraceTicks = GenDate.TicksPerDay;
        private static readonly HashSet<string> N007PlagueVisitorIncidentDefNames =
            new HashSet<string>(MouseDisasterNarrativePolicy.GetAttitudeSources("N007"), StringComparer.OrdinalIgnoreCase);

        private List<MouseDisasterN007Record> n007Records = new List<MouseDisasterN007Record>();

        private void ExposeN007Data()
        {
            Scribe_Collections.Look(ref n007Records, "mouseDisaster_narrativeN007Records", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }

            n007Records ??= new List<MouseDisasterN007Record>();
            n007Records.RemoveAll(record => record == null || record.mapId < 0);
            for (int i = 0; i < n007Records.Count; i++)
            {
                MouseDisasterN007Record record = n007Records[i];
                record.pawnRecords ??= new List<MouseDisasterN007PawnRecord>();
                record.pawnRecords.RemoveAll(pawnRecord => pawnRecord == null ||
                    (pawnRecord.pawn == null && pawnRecord.pawnId < 0));
                for (int j = 0; j < record.pawnRecords.Count; j++)
                {
                    MouseDisasterN007PawnRecord pawnRecord = record.pawnRecords[j];
                    if (pawnRecord.pawn != null && pawnRecord.pawnId < 0)
                    {
                        pawnRecord.pawnId = pawnRecord.pawn.thingIDNumber;
                    }
                }
            }
        }

        public void NotifyN007PlagueIncident(IncidentDef incidentDef, IncidentParms parms, IEnumerable<Pawn> participants = null)
        {
            if (!NarrativeEnabled("N007") || incidentDef == null ||
                !N007PlagueVisitorIncidentDefNames.Contains(incidentDef.defName))
            {
                return;
            }

            Map map = parms?.target as Map;
            if (map == null || !map.IsPlayerHome || n007Records == null)
            {
                return;
            }

            List<Pawn> infectedVisitors = participants
                ?.Where(IsN007PlagueVisitor)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (infectedVisitors.Count == 0)
            {
                return;
            }

            MouseDisasterN007Record record = FindActiveN007Record(map.uniqueID);
            if (record == null)
            {
                record = new MouseDisasterN007Record
                {
                    mapId = map.uniqueID,
                    createdTick = CurrentNarrativeTick,
                    phase = MouseDisasterN007Phase.Decision,
                    outcome = MouseDisasterN007Outcome.Pending
                };
                n007Records.Add(record);
            }

            int addedCount = 0;
            List<Pawn> addedPawns = new List<Pawn>();
            for (int i = 0; i < infectedVisitors.Count; i++)
            {
                Pawn pawn = infectedVisitors[i];
                if (FindN007PawnRecord(record, pawn) != null)
                {
                    continue;
                }

                record.pawnRecords.Add(new MouseDisasterN007PawnRecord
                {
                    pawn = pawn,
                    pawnId = pawn.thingIDNumber
                });
                addedCount++;
                addedPawns.Add(pawn);
            }

            if (addedCount == 0)
            {
                return;
            }

            if (record.phase == MouseDisasterN007Phase.Quarantined)
            {
                HoldN007Pawns(addedPawns);
            }
            else if (record.phase == MouseDisasterN007Phase.ReleasePending)
            {
                StartN007Release(map, addedPawns);
            }
            else if (record.phase == MouseDisasterN007Phase.RecoveryDecision)
            {
                record.phase = MouseDisasterN007Phase.Quarantined;
                HoldN007Pawns(addedPawns);
            }

            if (record.phase == MouseDisasterN007Phase.Decision)
            {
                TrySendN007ChoiceLetter(record, map, MouseDisasterN007LetterStage.Entry);
            }
        }

        public bool CanShowN007Letter(int mapId, MouseDisasterN007LetterStage stage)
        {
            MouseDisasterN007Record record = FindActiveN007Record(mapId);
            if (record == null)
            {
                return false;
            }

            return stage == MouseDisasterN007LetterStage.Entry
                ? record.phase == MouseDisasterN007Phase.Decision
                : record.phase == MouseDisasterN007Phase.RecoveryDecision;
        }

        public bool IsN007VisitorControlBlocked(Map map, IEnumerable<Pawn> visitors)
        {
            if (map == null || visitors == null)
            {
                return false;
            }

            MouseDisasterN007Record record = FindActiveN007Record(map.uniqueID);
            if (record == null || !IsN007VisitorControlBlockingPhase(record.phase))
            {
                return false;
            }

            List<Pawn> visitorList = visitors.Where(pawn => pawn != null).Distinct().ToList();
            for (int i = 0; i < record.pawnRecords.Count; i++)
            {
                MouseDisasterN007PawnRecord pawnRecord = record.pawnRecords[i];
                if (pawnRecord?.pawn != null && !pawnRecord.died && !pawnRecord.handled &&
                    !pawnRecord.leftMap && visitorList.Contains(pawnRecord.pawn))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ResolveN007Initial(int mapId, MouseDisasterN007InitialDecision decision, out string message)
        {
            message = "MouseDisaster_N007_ActionFailed".Translate().ToString();
            MouseDisasterN007Record record = FindActiveN007Record(mapId);
            if (record == null || record.phase != MouseDisasterN007Phase.Decision)
            {
                return false;
            }

            Map map = ResolveN007Map(record);
            if (map == null)
            {
                return false;
            }
            ScanN007Record(record, map);
            switch (decision)
            {
                case MouseDisasterN007InitialDecision.Quarantine:
                {
                    List<Pawn> pawns = GetActiveN007Pawns(record, map);
                    int heldCount = HoldN007Pawns(pawns);
                    if (heldCount == 0)
                    {
                        return false;
                    }

                    record.phase = MouseDisasterN007Phase.Quarantined;
                    ChangeNarratorTrust(1);
                    message = "MouseDisaster_N007_Quarantine_Message".Translate(heldCount).ToString();
                    return true;
                }
                case MouseDisasterN007InitialDecision.Release:
                {
                    int releasedCount = StartN007Release(map, GetActiveN007Pawns(record, map));
                    if (releasedCount == 0)
                    {
                        return false;
                    }

                    record.phase = MouseDisasterN007Phase.ReleasePending;
                    message = "MouseDisaster_N007_Release_Message".Translate(releasedCount).ToString();
                    return true;
                }
                case MouseDisasterN007InitialDecision.Defer:
                    record.phase = MouseDisasterN007Phase.Monitoring;
                    message = "MouseDisaster_N007_Defer_Message".Translate().ToString();
                    return true;
                default:
                    return false;
            }
        }

        public bool ResolveN007Recovery(int mapId, MouseDisasterN007RecoveryDecision decision, out string message)
        {
            message = "MouseDisaster_N007_ActionFailed".Translate().ToString();
            MouseDisasterN007Record record = FindActiveN007Record(mapId);
            if (record == null || record.phase != MouseDisasterN007Phase.RecoveryDecision)
            {
                return false;
            }

            Map map = ResolveN007Map(record);
            if (map == null)
            {
                return false;
            }
            N007ScanSummary summary = ScanN007Record(record, map);
            if (summary.HasActiveInfected || !summary.AllTerminal)
            {
                return false;
            }

            List<Pawn> recoveredPawns = GetActiveN007Pawns(record, map, recoveredOnly: true);
            if (decision == MouseDisasterN007RecoveryDecision.Release)
            {
                if (recoveredPawns.Count == 0)
                {
                    ResolveN007WithoutActivePawns(record, map, summary);
                    message = "MouseDisaster_N007_Recovered_Message".Translate().ToString();
                    return true;
                }

                StartN007Release(map, recoveredPawns);
                record.phase = MouseDisasterN007Phase.ReleasePending;
                message = "MouseDisaster_N007_RecoveredRelease_Message".Translate(recoveredPawns.Count).ToString();
                return true;
            }

            if (decision != MouseDisasterN007RecoveryDecision.Accept || recoveredPawns.Count == 0 ||
                Current.Game?.GetComponent<GameComponent_MouseDisasterVisitorControl>() == null)
            {
                return false;
            }

            // The recovery choice is the sole entry allowed to join quarantined visitors.
            if (!Current.Game.GetComponent<GameComponent_MouseDisasterVisitorControl>().TryJoin(recoveredPawns, out int joinedCount) ||
                joinedCount != recoveredPawns.Count)
            {
                return false;
            }

            for (int i = 0; i < record.pawnRecords.Count; i++)
            {
                MouseDisasterN007PawnRecord pawnRecord = record.pawnRecords[i];
                if (pawnRecord?.pawn != null && recoveredPawns.Contains(pawnRecord.pawn) &&
                    !MouseDisasterVisitorUtility.IsManagedVisitor(pawnRecord.pawn))
                {
                    pawnRecord.handled = true;
                }
            }

            ResolveN007(record, MouseDisasterN007Outcome.RecoveredAndStayed, map);
            message = "MouseDisaster_N007_RecoveredAccept_Message".Translate(joinedCount).ToString();
            return true;
        }

        private void ProcessN007()
        {
            if (n007Records == null || n007Records.Count == 0)
            {
                return;
            }

            for (int i = 0; i < n007Records.Count; i++)
            {
                MouseDisasterN007Record record = n007Records[i];
                if (record == null || record.phase == MouseDisasterN007Phase.Resolved)
                {
                    continue;
                }

                Map map = ResolveN007Map(record);
                if (map == null)
                {
                    ResolveN007(record, MouseDisasterN007Outcome.Handled, null);
                    continue;
                }

                N007ScanSummary summary = ScanN007Record(record, map);
                switch (record.phase)
                {
                    case MouseDisasterN007Phase.Decision:
                        if (summary.HasActiveInfected)
                        {
                            TrySendN007ChoiceLetter(record, map, MouseDisasterN007LetterStage.Entry);
                        }
                        else if (summary.HasActiveRecovered && summary.AllTerminal)
                        {
                            BeginN007RecoveryDecision(record, map);
                        }
                        else if (summary.AllTerminal)
                        {
                            ResolveN007WithoutActivePawns(record, map, summary);
                        }
                        break;
                    case MouseDisasterN007Phase.Quarantined:
                        if (summary.HasInfectedLeft)
                        {
                            ResolveN007(record, MouseDisasterN007Outcome.QuarantineBroken, map);
                        }
                        else if (summary.AllTerminal)
                        {
                            if (summary.HasActiveRecovered)
                            {
                                BeginN007RecoveryDecision(record, map);
                            }
                            else
                            {
                                ResolveN007WithoutActivePawns(record, map, summary);
                            }
                        }
                        break;
                    case MouseDisasterN007Phase.Monitoring:
                        if (summary.AllTerminal && summary.HasActiveRecovered)
                        {
                            BeginN007RecoveryDecision(record, map);
                        }
                        else if (summary.AllTerminal)
                        {
                            ResolveN007WithoutActivePawns(record, map, summary);
                        }
                        break;
                    case MouseDisasterN007Phase.ReleasePending:
                        if (summary.AllLeftOrDead)
                        {
                            ResolveN007(record, summary.HasHandled ? MouseDisasterN007Outcome.Handled :
                                summary.HasLeft ? (summary.AllRecoveredOrDied ? MouseDisasterN007Outcome.RecoveredAndLeft :
                                    MouseDisasterN007Outcome.Released) : MouseDisasterN007Outcome.Died, map);
                        }
                        break;
                    case MouseDisasterN007Phase.RecoveryDecision:
                        if (summary.HasActiveInfected)
                        {
                            record.phase = MouseDisasterN007Phase.Quarantined;
                            HoldN007Pawns(GetActiveN007Pawns(record, map));
                        }
                        else if (summary.HasActiveRecovered)
                        {
                            TrySendN007ChoiceLetter(record, map, MouseDisasterN007LetterStage.Recovery);
                        }
                        else if (summary.AllTerminal)
                        {
                            ResolveN007WithoutActivePawns(record, map, summary);
                        }
                        break;
                }
            }
        }

        private bool TrySendN007ChoiceLetter(MouseDisasterN007Record record, Map map, MouseDisasterN007LetterStage stage)
        {
            if (record == null || map == null || MouseDisasterDefOf.MouseDisaster_N007Letter == null ||
                HasN007ChoiceLetter(record.mapId, stage))
            {
                return HasN007ChoiceLetter(record?.mapId ?? -1, stage);
            }

            string labelKey = stage == MouseDisasterN007LetterStage.Entry
                ? "MouseDisaster_N007_EntryLabel"
                : "MouseDisaster_N007_RecoveryLabel";
            string textKey = stage == MouseDisasterN007LetterStage.Entry
                ? "MouseDisaster_N007_EntryText"
                : "MouseDisaster_N007_RecoveryText";
            ChoiceLetter_MouseDisasterN007 letter = LetterMaker.MakeLetter(
                labelKey.Translate(),
                textKey.Translate() + "\n\n" + N007Counts(record),
                MouseDisasterDefOf.MouseDisaster_N007Letter,
                new TargetInfo(map.Center, map)) as ChoiceLetter_MouseDisasterN007;
            if (letter == null)
            {
                Log.Error("[MouseDisaster] Could not create N-007 choice letter.");
                return false;
            }

            letter.map = map;
            letter.mapId = record.mapId;
            letter.stage = stage;
            Find.LetterStack.ReceiveLetter(letter, null);
            return Find.LetterStack.LettersListForReading.Contains(letter);
        }

        private void BeginN007RecoveryDecision(MouseDisasterN007Record record, Map map)
        {
            if (record == null || record.phase != MouseDisasterN007Phase.Quarantined &&
                record.phase != MouseDisasterN007Phase.Monitoring && record.phase != MouseDisasterN007Phase.Decision)
            {
                return;
            }

            record.phase = MouseDisasterN007Phase.RecoveryDecision;
            TrySendN007ChoiceLetter(record, map, MouseDisasterN007LetterStage.Recovery);
        }

        private void ResolveN007WithoutActivePawns(MouseDisasterN007Record record, Map map, N007ScanSummary summary)
        {
            if (summary.HasInfectedLeft)
            {
                ResolveN007(record, MouseDisasterN007Outcome.Handled, map);
            }
            else if (summary.HasHandled)
            {
                ResolveN007(record, MouseDisasterN007Outcome.Handled, map);
            }
            else if (summary.HasRecovered && summary.HasLeft)
            {
                ResolveN007(record, MouseDisasterN007Outcome.RecoveredAndLeft, map);
            }
            else
            {
                ResolveN007(record, MouseDisasterN007Outcome.Died, map);
            }
        }

        private bool ResolveN007(MouseDisasterN007Record record, MouseDisasterN007Outcome outcome, Map map)
        {
            if (record == null || record.phase == MouseDisasterN007Phase.Resolved)
            {
                return false;
            }

            record.outcome = outcome;
            CompleteNarrativeFlag("N007");
            if (outcome == MouseDisasterN007Outcome.RecoveredAndLeft || outcome == MouseDisasterN007Outcome.RecoveredAndStayed)
                CompleteNarrativeFlag("N007Recovered");
            if (outcome == MouseDisasterN007Outcome.RecoveredAndLeft) ScheduleRecoveredReturn(record);
            if (record.phase == MouseDisasterN007Phase.Quarantined || record.phase == MouseDisasterN007Phase.RecoveryDecision)
            {
                StartN007Release(map, GetActiveN007Pawns(record, map));
            }
            record.phase = MouseDisasterN007Phase.Resolved;
            List<ChoiceLetter_MouseDisasterN007> letters = Find.LetterStack.LettersListForReading
                .OfType<ChoiceLetter_MouseDisasterN007>().Where(letter => letter.mapId == record.mapId).ToList();
            for (int i = 0; i < letters.Count; i++)
            {
                Find.LetterStack.RemoveLetter(letters[i]);
            }
            ChangeNarratorTrust(TrustDeltaForN007Outcome(outcome));
            if (IsNarratorActive())
            {
                string suffix = outcome switch
                {
                    MouseDisasterN007Outcome.RecoveredAndLeft => "Recovered",
                    MouseDisasterN007Outcome.RecoveredAndStayed => "RecoveredStayed",
                    MouseDisasterN007Outcome.Released => "Released",
                    MouseDisasterN007Outcome.QuarantineBroken => "QuarantineBroken",
                    MouseDisasterN007Outcome.Died => "Died",
                    _ => "Handled"
                };
                ReceiveNarrativeLetterText(
                    ("MouseDisaster_N007_" + suffix + "_Label").Translate(),
                    ("MouseDisaster_N007_" + suffix + "_Text").Translate() + "\n\n" + N007Counts(record), map);
            }

            return true;
        }

        private static int TrustDeltaForN007Outcome(MouseDisasterN007Outcome outcome)
        {
            switch (outcome)
            {
                case MouseDisasterN007Outcome.RecoveredAndLeft:
                    return 2;
                case MouseDisasterN007Outcome.RecoveredAndStayed:
                    return 1;
                case MouseDisasterN007Outcome.QuarantineBroken:
                    return -2;
                default:
                    return 0;
            }
        }

        private static string N007Counts(MouseDisasterN007Record record)
        {
            return "MouseDisaster_Story_N007Counts".Translate(record.pawnRecords.Count(p => p.recovered && !p.died),
                record.pawnRecords.Count(p => p.died), record.pawnRecords.Count(p => p.leftMap),
                record.pawnRecords.Count(p => p.handled));
        }

        private MouseDisasterN007Record FindActiveN007Record(int mapId)
        {
            return n007Records?.FirstOrDefault(record => record != null && record.mapId == mapId &&
                record.phase != MouseDisasterN007Phase.Resolved && record.outcome == MouseDisasterN007Outcome.Pending);
        }

        private static Map ResolveN007Map(MouseDisasterN007Record record)
        {
            return Find.Maps?.FirstOrDefault(map => map != null && map.uniqueID == record?.mapId);
        }

        private bool HasN007ChoiceLetter(int mapId, MouseDisasterN007LetterStage stage)
        {
            if (Find.LetterStack?.LettersListForReading == null)
            {
                return false;
            }

            for (int i = 0; i < Find.LetterStack.LettersListForReading.Count; i++)
            {
                ChoiceLetter_MouseDisasterN007 letter = Find.LetterStack.LettersListForReading[i] as ChoiceLetter_MouseDisasterN007;
                if (letter != null && letter.mapId == mapId && letter.stage == stage)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsN007PlagueVisitor(Pawn pawn)
        {
            return pawn != null && pawn.Faction != Faction.OfPlayer &&
                   MouseDisasterVisitorUtility.IsManagedVisitor(pawn) &&
                   MouseDisasterPlagueUtility.IsPlagueCarrierMouseDisasterPawn(pawn);
        }

        private static bool IsN007VisitorControlBlockingPhase(MouseDisasterN007Phase phase)
        {
            return phase == MouseDisasterN007Phase.Decision ||
                   phase == MouseDisasterN007Phase.Quarantined ||
                   phase == MouseDisasterN007Phase.ReleasePending ||
                   phase == MouseDisasterN007Phase.RecoveryDecision;
        }

        private static MouseDisasterN007PawnRecord FindN007PawnRecord(MouseDisasterN007Record record, Pawn pawn)
        {
            if (record?.pawnRecords == null || pawn == null)
            {
                return null;
            }

            for (int i = 0; i < record.pawnRecords.Count; i++)
            {
                MouseDisasterN007PawnRecord pawnRecord = record.pawnRecords[i];
                if (pawnRecord?.pawn == pawn || pawnRecord?.pawnId == pawn.thingIDNumber)
                {
                    return pawnRecord;
                }
            }

            return null;
        }

        private static List<Pawn> GetActiveN007Pawns(MouseDisasterN007Record record, Map map, bool recoveredOnly = false)
        {
            List<Pawn> result = new List<Pawn>();
            if (record?.pawnRecords == null || map == null)
            {
                return result;
            }

            for (int i = 0; i < record.pawnRecords.Count; i++)
            {
                MouseDisasterN007PawnRecord pawnRecord = record.pawnRecords[i];
                Pawn pawn = ResolveN007Pawn(pawnRecord, map);
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Map != map || !pawn.Spawned ||
                    pawnRecord.handled || pawnRecord.leftMap || MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn) ||
                    !MouseDisasterVisitorUtility.IsManagedVisitor(pawn) ||
                    (recoveredOnly && (!pawnRecord.recovered || HasN007Plague(pawn))))
                {
                    continue;
                }

                result.Add(pawn);
            }

            return result.Distinct().ToList();
        }

        private static Pawn ResolveN007Pawn(MouseDisasterN007PawnRecord pawnRecord, Map map)
        {
            if (pawnRecord == null)
            {
                return null;
            }

            if (pawnRecord.pawn != null && (pawnRecord.pawnId < 0 || pawnRecord.pawn.thingIDNumber == pawnRecord.pawnId))
            {
                return pawnRecord.pawn;
            }

            Pawn pawn = map?.mapPawns?.AllPawnsSpawned?.FirstOrDefault(candidate =>
                candidate != null && candidate.thingIDNumber == pawnRecord.pawnId);
            if (pawn == null)
            {
                pawn = Find.WorldPawns?.AllPawnsAliveOrDead?.FirstOrDefault(candidate =>
                    candidate != null && candidate.thingIDNumber == pawnRecord.pawnId);
            }

            if (pawn != null)
            {
                pawnRecord.pawn = pawn;
            }

            return pawn;
        }

        private static int HoldN007Pawns(IEnumerable<Pawn> pawns)
        {
            int heldCount = 0;
            foreach (Pawn pawn in pawns?.Where(pawn => pawn != null && pawn.Spawned).Distinct() ?? Enumerable.Empty<Pawn>())
            {
                pawn.GetLord()?.RemovePawn(pawn);
                pawn.jobs?.StopAll();
                if (pawn.mindState != null)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.Defend, pawn.Position, 3f);
                    pawn.mindState.duty.wanderRadius = 3f;
                    pawn.mindState.canFleeIndividual = false;
                    pawn.mindState.exitMapAfterTick = -1;
                }

                heldCount++;
            }

            return heldCount;
        }

        private static int StartN007Release(Map map, IEnumerable<Pawn> pawns)
        {
            List<Pawn> pawnList = pawns?.Where(pawn => pawn != null && pawn.Spawned && !pawn.Dead && pawn.Map == map)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (map == null || pawnList.Count == 0)
            {
                return 0;
            }

            for (int i = 0; i < pawnList.Count; i++)
            {
                Pawn pawn = pawnList[i];
                pawn.GetLord()?.RemovePawn(pawn);
                pawn.jobs?.StopAll();
                if (pawn.mindState != null)
                {
                    pawn.mindState.duty = null;
                    pawn.mindState.canFleeIndividual = true;
                    pawn.mindState.exitMapAfterTick = -1;
                }
            }

            MouseDisasterUtility.MakeTravelAndExitLord(map, pawnList, map.Center);
            return pawnList.Count;
        }

        private N007ScanSummary ScanN007Record(MouseDisasterN007Record record, Map map)
        {
            N007ScanSummary summary = new N007ScanSummary
            {
                AllRecoveredOrDied = true,
                AllTerminal = true,
                AllLeftOrDead = true,
                AllDied = true
            };
            int now = CurrentNarrativeTick;

            for (int i = 0; i < record.pawnRecords.Count; i++)
            {
                MouseDisasterN007PawnRecord pawnRecord = record.pawnRecords[i];
                if (pawnRecord.died || pawnRecord.handled || pawnRecord.leftMap)
                {
                    summary.HasDied |= pawnRecord.died;
                    summary.HasHandled |= pawnRecord.handled;
                    summary.HasLeft |= pawnRecord.leftMap;
                    summary.HasRecovered |= pawnRecord.recovered;
                    summary.HasInfectedLeft |= pawnRecord.leftMap && !pawnRecord.recovered && !pawnRecord.died;
                    summary.AllRecoveredOrDied &= pawnRecord.recovered || pawnRecord.died;
                    summary.AllDied &= pawnRecord.died;
                    continue;
                }

                Pawn pawn = ResolveN007Pawn(pawnRecord, map);
                if (pawn == null)
                {
                    if (pawnRecord.unresolvedSinceTick < 0)
                    {
                        pawnRecord.unresolvedSinceTick = now;
                    }

                    if (now - pawnRecord.unresolvedSinceTick >= N007ReferenceGraceTicks)
                    {
                        pawnRecord.handled = true;
                        summary.HasHandled = true;
                        summary.AllRecoveredOrDied = false;
                        summary.AllDied = false;
                        continue;
                    }

                    summary.AllRecoveredOrDied = false;
                    summary.AllTerminal = false;
                    summary.AllLeftOrDead = false;
                    summary.AllDied = false;
                    continue;
                }

                pawnRecord.unresolvedSinceTick = -1;
                if (pawn.Dead || pawn.Destroyed)
                {
                    pawnRecord.died = true;
                    summary.HasDied = true;
                    continue;
                }

                if (MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn))
                {
                    pawnRecord.handled = true;
                    pawnRecord.recovered = !HasN007Plague(pawn);
                    summary.HasHandled = true;
                    summary.HasRecovered |= pawnRecord.recovered;
                    summary.AllRecoveredOrDied &= pawnRecord.recovered;
                    summary.AllDied = false;
                    continue;
                }

                bool activeOnMap = pawn.Spawned && pawn.Map == map;
                if (activeOnMap)
                {
                    summary.AllDied = false;
                    if (!MouseDisasterVisitorUtility.IsManagedVisitor(pawn))
                    {
                        pawnRecord.handled = true;
                        summary.HasHandled = true;
                        summary.AllRecoveredOrDied &= pawnRecord.recovered;
                        continue;
                    }

                    summary.AllLeftOrDead = false;
                    if (HasN007Plague(pawn))
                    {
                        pawnRecord.recovered = false;
                        summary.HasActiveInfected = true;
                        summary.AllRecoveredOrDied = false;
                        summary.AllTerminal = false;
                    }
                    else
                    {
                        pawnRecord.recovered = true;
                        summary.HasRecovered = true;
                        summary.HasActiveRecovered = true;
                    }

                    continue;
                }

                // A carried patient is still on this map, not a quarantine escape.
                if (pawn.MapHeld == map)
                {
                    summary.AllRecoveredOrDied = false;
                    summary.AllTerminal = false;
                    summary.AllLeftOrDead = false;
                    summary.AllDied = false;
                    continue;
                }

                pawnRecord.leftMap = true;
                summary.HasLeft = true;
                summary.AllDied = false;
                if (HasN007Plague(pawn))
                {
                    pawnRecord.recovered = false;
                    summary.HasInfectedLeft = true;
                    summary.AllRecoveredOrDied = false;
                }
                else
                {
                    pawnRecord.recovered = true;
                    summary.HasRecovered = true;
                }
            }

            return summary;
        }

        private static bool HasN007Plague(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.HasHediff(MouseDisasterDefOf.MouseDisaster_Plague) == true;
        }

        private sealed class N007ScanSummary
        {
            public bool HasActiveInfected;
            public bool HasActiveRecovered;
            public bool HasInfectedLeft;
            public bool HasRecovered;
            public bool HasLeft;
            public bool HasDied;
            public bool HasHandled;
            public bool AllRecoveredOrDied;
            public bool AllTerminal;
            public bool AllLeftOrDead;
            public bool AllDied;
        }
    }
}
