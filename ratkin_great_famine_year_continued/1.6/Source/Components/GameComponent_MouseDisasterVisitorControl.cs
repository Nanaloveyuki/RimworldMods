using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public enum MouseDisasterVisitorStatus
    {
        Visitor = 0,
        TemporaryRecruit = 1,
        HiredWorker = 2
    }

    public class MouseDisasterVisitorRecord : IExposable
    {
        public Pawn pawn;
        public Faction originalFaction;
        public PawnKindDef originalKindDef;
        public MouseDisasterVisitorStatus status;
        public int temporaryUntilTick = -1;
        public bool employmentTimerPaused;
        internal int slot = -1;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref originalFaction, "originalFaction");
            Scribe_Defs.Look(ref originalKindDef, "originalKindDef");
            Scribe_Values.Look(ref status, "status", MouseDisasterVisitorStatus.Visitor);
            Scribe_Values.Look(ref temporaryUntilTick, "temporaryUntilTick", -1);
            Scribe_Values.Look(ref employmentTimerPaused, "employmentTimerPaused", false);
        }
    }

    public class GameComponent_MouseDisasterVisitorControl : GameComponent
    {
        private const int MaintenanceIntervalTicks = 60;

        private List<MouseDisasterVisitorRecord> visitorRecords = new List<MouseDisasterVisitorRecord>();
        private readonly Dictionary<Pawn, MouseDisasterVisitorRecord> recordsByPawn = new Dictionary<Pawn, MouseDisasterVisitorRecord>();

        public GameComponent_MouseDisasterVisitorControl(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref visitorRecords, "mouseDisaster_visitorRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                visitorRecords ??= new List<MouseDisasterVisitorRecord>();
                RebuildRecordIndex();
                NormalizeLoadedRecords();
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager == null || Find.TickManager.TicksGame % MaintenanceIntervalTicks != 0)
            {
                return;
            }

            CleanupRecords(processTemporaryExpiry: true);
        }

        public void RegisterVisitors(IEnumerable<Pawn> pawns)
        {
            if (pawns == null)
            {
                return;
            }

            visitorRecords ??= new List<MouseDisasterVisitorRecord>();
            foreach (Pawn pawn in pawns)
            {
                if (!CanTrackPawn(pawn) || GetRecord(pawn) != null)
                {
                    continue;
                }

                var record = new MouseDisasterVisitorRecord
                {
                    pawn = pawn,
                    originalFaction = pawn.Faction,
                    originalKindDef = pawn.kindDef,
                    slot = visitorRecords.Count,
                    status = MouseDisasterVisitorStatus.Visitor,
                    temporaryUntilTick = -1
                };
                visitorRecords.Add(record);
                recordsByPawn[pawn] = record;
            }
        }

        public bool IsManagedVisitor(Pawn pawn)
        {
            return GetRecord(pawn) != null;
        }

        internal bool ReferencesFaction(Faction faction) => visitorRecords.Any(r => r?.originalFaction == faction);

        public void NotifyPawnIdentityChanged(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            if (record == null ||
                (record.status != MouseDisasterVisitorStatus.TemporaryRecruit &&
                 record.status != MouseDisasterVisitorStatus.HiredWorker) ||
                !IsIdentityChangedEmployment(pawn))
            {
                return;
            }

            if (!record.employmentTimerPaused)
            {
                record.employmentTimerPaused = true;
                record.temporaryUntilTick = -1;
                PauseEmploymentMarkerTimer(pawn);
            }

            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyNarrativeVisitorIdentityChanged(pawn);
        }

        public void RemoveVisitorRecord(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            if (record != null)
            {
                ForgetRecord(record);
            }
        }

        public bool IsShelteredVisitor(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            return record != null &&
                   (record.status == MouseDisasterVisitorStatus.TemporaryRecruit || record.status == MouseDisasterVisitorStatus.HiredWorker) &&
                   pawn != null &&
                   pawn.Faction == Faction.OfPlayer &&
                   !pawn.IsPrisonerOfColony &&
                   !pawn.IsSlaveOfColony;
        }

        public bool CanHire(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            return record != null &&
                   record.status != MouseDisasterVisitorStatus.HiredWorker &&
                   CanTrackPawn(pawn);
        }

        public bool CanEndHire(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            return record != null &&
                   record.status == MouseDisasterVisitorStatus.HiredWorker &&
                   CanTrackPawn(pawn);
        }

        public bool CanJoin(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            return record != null && CanTrackPawn(pawn);
        }

        public List<Pawn> GetActiveVisitors(IEnumerable<Pawn> pawns)
        {
            if (pawns == null)
            {
                return new List<Pawn>();
            }

            return pawns
                .Where(CanTrackPawn)
                .Where(pawn => GetRecord(pawn) != null)
                .Distinct()
                .ToList();
        }

        public List<Pawn> GetRecruitableVisitors(IEnumerable<Pawn> pawns)
        {
            return GetActiveVisitors(pawns)
                .Where(pawn =>
                {
                    MouseDisasterVisitorRecord record = GetRecord(pawn);
                    return record != null &&
                           record.status == MouseDisasterVisitorStatus.Visitor &&
                           !MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn);
                })
                .ToList();
        }

        public bool TryTemporaryRecruit(IEnumerable<Pawn> pawns, int durationTicks, out int recruitedCount)
        {
            recruitedCount = 0;
            List<Pawn> validPawns = GetRecruitableVisitors(pawns);
            if (validPawns.Count == 0)
            {
                return false;
            }

            int untilTick = (Find.TickManager?.TicksGame ?? 0) + System.Math.Max(600, durationTicks);
            for (int i = 0; i < validPawns.Count; i++)
            {
                MouseDisasterVisitorRecord record = GetRecord(validPawns[i]);
                if (record == null)
                {
                    continue;
                }

                BringPawnUnderPlayerProtection(record.pawn);
                record.status = MouseDisasterVisitorStatus.TemporaryRecruit;
                record.temporaryUntilTick = untilTick;
                record.employmentTimerPaused = false;
                SyncEmploymentMarkers(record.pawn, record.status, GetRemainingTimerTicks(record), false);
                recruitedCount++;
            }

            return recruitedCount > 0;
        }

        public bool TryHire(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            if (record == null || record.status == MouseDisasterVisitorStatus.HiredWorker || !CanTrackPawn(pawn))
            {
                return false;
            }

            BringPawnUnderPlayerProtection(pawn);
            record.status = MouseDisasterVisitorStatus.HiredWorker;
            record.temporaryUntilTick = (Find.TickManager?.TicksGame ?? 0) +
                System.Math.Max(600, MouseDisasterVisitorUtility.HiredWorkerDurationTicks);
            record.employmentTimerPaused = false;
            SyncEmploymentMarkers(pawn, record.status, GetRemainingTimerTicks(record), false);
            return true;
        }

        public bool TryHire(IEnumerable<Pawn> pawns, out int hiredCount)
        {
            hiredCount = 0;
            List<Pawn> validPawns = GetRecruitableVisitors(pawns);
            if (validPawns.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < validPawns.Count; i++)
            {
                if (TryHire(validPawns[i]))
                {
                    hiredCount++;
                }
            }

            return hiredCount > 0;
        }

        public bool TryEndHire(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            if (record == null || record.status != MouseDisasterVisitorStatus.HiredWorker || !CanTrackPawn(pawn))
            {
                return false;
            }

            RestoreOriginalIdentityAndForceLeave(record);
            ForgetRecord(record);
            return true;
        }

        public bool TryJoin(Pawn pawn)
        {
            MouseDisasterVisitorRecord record = GetRecord(pawn);
            if (record == null || !CanTrackPawn(pawn))
            {
                return false;
            }

            BringPawnUnderPlayerProtection(pawn);
            SyncEmploymentMarkers(pawn, MouseDisasterVisitorStatus.Visitor);
            ForgetRecord(record);
            return true;
        }

        public bool TryJoin(IEnumerable<Pawn> pawns, out int joinedCount)
        {
            joinedCount = 0;
            List<Pawn> validPawns = GetActiveVisitors(pawns);
            if (validPawns.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < validPawns.Count; i++)
            {
                if (TryJoin(validPawns[i]))
                {
                    joinedCount++;
                }
            }

            return joinedCount > 0;
        }

        public bool TryEnslave(IEnumerable<Pawn> pawns, out int enslavedCount, out string failureMessage)
        {
            enslavedCount = 0;
            failureMessage = "MouseDisaster_VisitorControl_NoValidTarget".Translate();
            if (!ModsConfig.IdeologyActive)
            {
                return false;
            }

            List<Pawn> validPawns = GetRecruitableVisitors(pawns);
            if (validPawns.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < validPawns.Count; i++)
            {
                Pawn pawn = validPawns[i];
                MouseDisasterVisitorRecord record = GetRecord(pawn);
                if (record == null || !TrySetVisitorCaptiveStatus(pawn, GuestStatus.Slave))
                {
                    continue;
                }

                ForgetRecord(record);
                enslavedCount++;
            }

            return enslavedCount > 0;
        }

        public bool TryCapture(IEnumerable<Pawn> pawns, out int capturedCount, out string failureMessage)
        {
            capturedCount = 0;
            failureMessage = "MouseDisaster_VisitorControl_NoValidTarget".Translate();

            List<Pawn> validPawns = GetRecruitableVisitors(pawns);
            if (validPawns.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < validPawns.Count; i++)
            {
                Pawn pawn = validPawns[i];
                MouseDisasterVisitorRecord record = GetRecord(pawn);
                if (record == null || !TrySetVisitorCaptiveStatus(pawn, GuestStatus.Prisoner))
                {
                    continue;
                }

                ForgetRecord(record);
                capturedCount++;
            }

            return capturedCount > 0;
        }

        public bool TrySendToPrison(IEnumerable<Pawn> pawns, Map map, out int imprisonedCount, out string failureMessage)
        {
            imprisonedCount = 0;
            failureMessage = "MouseDisaster_VisitorControl_NoValidTarget".Translate();

            List<Pawn> validPawns = GetActiveVisitors(pawns);
            if (map == null || validPawns.Count == 0)
            {
                return false;
            }

            if (!MouseDisasterPrisonTransferUtility.TryFindAvailablePrisonCells(map, validPawns.Count, out List<IntVec3> prisonCells))
            {
                failureMessage = "MouseDisaster_VisitorControl_SendToPrison_NoPrisonArea".Translate();
                return false;
            }

            int prisonCellIndex = 0;
            for (int i = 0; i < validPawns.Count && prisonCellIndex < prisonCells.Count; i++)
            {
                Pawn pawn = validPawns[i];
                MouseDisasterVisitorRecord record = GetRecord(pawn);
                if (record == null || !TrySendPawnToPrison(pawn, map, prisonCells[prisonCellIndex]))
                {
                    continue;
                }

                ForgetRecord(record);
                imprisonedCount++;
                prisonCellIndex++;
            }

            return imprisonedCount > 0;
        }

        public bool TryMakeHostile(IEnumerable<Pawn> pawns, out int hostileCount)
        {
            if (GameComponent_MouseDisasterEventBehavior.Component?.React(pawns, true, out int affectedCount) == true)
            {
                hostileCount = affectedCount;
                return true;
            }
            hostileCount = 0;
            List<Pawn> validPawns = GetActiveVisitors(pawns);
            if (validPawns.Count == 0)
            {
                return false;
            }

            Map map = validPawns.FirstOrDefault()?.Map;
            if (map == null)
            {
                return false;
            }

            Faction hostileFaction = MouseDisasterUtility.GetEventFaction(hostile: true, friendly: false);
            if (hostileFaction == null && !MouseDisasterUtility.TryFindFormerFaction(out hostileFaction))
            {
                return false;
            }

            List<Pawn> attackers = new List<Pawn>();
            for (int i = 0; i < validPawns.Count; i++)
            {
                Pawn pawn = validPawns[i];
                MouseDisasterVisitorRecord record = GetRecord(pawn);
                if (record == null || pawn == null || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }

                ReleaseGuestState(pawn);
                MouseDisasterUtility.UnmarkTradableChattel(pawn);
                MouseDisasterUtility.ConsumeForcedPrisonerOnPurchase(pawn);
                MouseDisasterUtility.ClearTradeLeaderState(pawn);
                MouseDisasterUtility.RemoveAllIncidentVisitorHediffs(pawn);
                pawn.mindState?.mentalStateHandler?.Reset();
                if (pawn.mindState != null)
                {
                    pawn.mindState.duty = null;
                    pawn.mindState.canFleeIndividual = true;
                    pawn.mindState.exitMapAfterTick = -1;
                }
                pawn.jobs?.StopAll();
                pawn.GetLord()?.RemovePawn(pawn);

                if (record.originalKindDef != null && pawn.kindDef != record.originalKindDef)
                {
                    pawn.ChangeKind(record.originalKindDef);
                }

                if (pawn.Faction != hostileFaction)
                {
                    pawn.SetFaction(hostileFaction);
                }

                MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
                ForgetRecord(record);
                hostileCount++;

                if (!CanLeaveMapUnderOwnPower(pawn))
                {
                    pawn.DeSpawnOrDeselect();
                    if (!Find.WorldPawns.Contains(pawn))
                    {
                        Find.WorldPawns.PassToWorld(pawn);
                    }

                    continue;
                }

                attackers.Add(pawn);
            }

            if (attackers.Count == 0 && hostileCount == 0)
            {
                return false;
            }

            MouseDisasterUtility.MakeFactionHostileToPlayer(hostileFaction, explicitDriveAway: true);
            if (attackers.Count > 0)
            {
                StartHostileDriveAwayLord(hostileFaction, map, attackers);
            }

            return true;
        }

        private static void StartHostileDriveAwayLord(Faction hostileFaction, Map map, List<Pawn> pawns)
        {
            if (hostileFaction == null || map == null || pawns == null || pawns.Count == 0)
            {
                return;
            }

            MouseDisasterVisitorHostilityLordKind lordKind = MouseDisasterVisitorHostilityPolicy.ResolveLordKind(
                explicitDriveAway: true,
                Rand.Value);
            if (lordKind == MouseDisasterVisitorHostilityLordKind.AssaultColony)
            {
                LordMaker.MakeNewLord(hostileFaction, new LordJob_AssaultColony(hostileFaction, canKidnap: false, canTimeoutOrFlee: false, canSteal: true, breachers: true), map, pawns);
                return;
            }

            Pawn anchorPawn = pawns.FirstOrDefault(pawn => pawn != null && pawn.Spawned);
            IntVec3 exitSpot = map.Center;
            if (anchorPawn != null &&
                !RCellFinder.TryFindBestExitSpot(anchorPawn, out exitSpot) &&
                !MouseDisasterUtility.TryFindFarEdgeCell(map, anchorPawn.Position, out exitSpot))
            {
                exitSpot = map.Center;
            }

            MouseDisasterUtility.MakeTravelAndExitLord(map, pawns, exitSpot, includeBabiesInExit: false);
        }

        private void CleanupRecords(bool processTemporaryExpiry)
        {
            visitorRecords ??= new List<MouseDisasterVisitorRecord>();
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            for (int i = visitorRecords.Count - 1; i >= 0; i--)
            {
                MouseDisasterVisitorRecord record = visitorRecords[i];
                Pawn pawn = record?.pawn;
                if (record == null || pawn == null || pawn.Dead || pawn.Destroyed || pawn.mindState == null ||
                    (!pawn.Spawned && pawn.MapHeld == null && !pawn.IsCaravanMember()))
                {
                    RemoveRecordAt(i);
                    continue;
                }

                if (processTemporaryExpiry &&
                    record.status == MouseDisasterVisitorStatus.TemporaryRecruit &&
                    !record.employmentTimerPaused &&
                    record.temporaryUntilTick > 0 &&
                    nowTick >= record.temporaryUntilTick)
                {
                    RestoreOriginalIdentityAndForceLeave(record);
                    Messages.Message("MouseDisaster_VisitorControl_EmploymentExpired".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                    RemoveRecordAt(i);
                    continue;
                }

                if (processTemporaryExpiry &&
                    record.status == MouseDisasterVisitorStatus.HiredWorker &&
                    !record.employmentTimerPaused &&
                    record.temporaryUntilTick > 0 &&
                    nowTick >= record.temporaryUntilTick)
                {
                    RestoreOriginalIdentityAndForceLeave(record);
                    Messages.Message("MouseDisaster_VisitorControl_EmploymentExpired".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                    RemoveRecordAt(i);
                    continue;
                }

                if (!pawn.Spawned && pawn.MapHeld == null)
                {
                    RemoveRecordAt(i);
                }
            }
        }

        private MouseDisasterVisitorRecord GetRecord(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            if (recordsByPawn.TryGetValue(pawn, out var record))
            {
                if (record != null && record.pawn == pawn && record.slot >= 0 && record.slot < visitorRecords.Count &&
                    visitorRecords[record.slot] == record)
                {
                    return record;
                }

                RebuildRecordIndex();
            }

            return recordsByPawn.TryGetValue(pawn, out record) ? record : null;
        }

        private void ForgetRecord(MouseDisasterVisitorRecord record)
        {
            if (record != null && record.slot >= 0 && record.slot < visitorRecords.Count && visitorRecords[record.slot] == record)
                RemoveRecordAt(record.slot);
        }

        private void RemoveRecordAt(int index)
        {
            if (visitorRecords == null || index < 0 || index >= visitorRecords.Count)
            {
                return;
            }

            MouseDisasterVisitorRecord record = visitorRecords[index];
            if (record?.pawn != null && recordsByPawn.TryGetValue(record.pawn, out var indexedRecord) && indexedRecord == record)
            {
                recordsByPawn.Remove(record.pawn);
            }

            int last = visitorRecords.Count - 1;
            visitorRecords[index] = visitorRecords[last];
            if (visitorRecords[index] != null) visitorRecords[index].slot = index;
            visitorRecords.RemoveAt(last);
            if (record != null) record.slot = -1;
        }

        private void RebuildRecordIndex()
        {
            visitorRecords ??= new List<MouseDisasterVisitorRecord>();
            recordsByPawn.Clear();
            for (int i = 0; i < visitorRecords.Count; i++)
            {
                var record = visitorRecords[i];
                if (record == null) continue;
                record.slot = i;
                if (record.pawn != null) recordsByPawn[record.pawn] = record;
            }
        }

        private void NormalizeLoadedRecords()
        {
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            var seenPawns = new HashSet<Pawn>();
            for (int i = visitorRecords.Count - 1; i >= 0; i--)
            {
                MouseDisasterVisitorRecord record = visitorRecords[i];
                Pawn pawn = record?.pawn;
                if (record == null || pawn == null || pawn.Dead || pawn.Destroyed || pawn.mindState == null ||
                    (!pawn.Spawned && pawn.MapHeld == null && !pawn.IsCaravanMember()) || !seenPawns.Add(pawn))
                {
                    RemoveRecordAt(i);
                    continue;
                }

                try
                {
                    if (record.status != MouseDisasterVisitorStatus.TemporaryRecruit &&
                        record.status != MouseDisasterVisitorStatus.HiredWorker)
                    {
                        record.temporaryUntilTick = -1;
                        record.employmentTimerPaused = false;
                        SyncEmploymentMarkers(record.pawn, MouseDisasterVisitorStatus.Visitor);
                        continue;
                    }

                    if (record.employmentTimerPaused || IsIdentityChangedEmployment(record.pawn))
                    {
                        record.employmentTimerPaused = true;
                        record.temporaryUntilTick = -1;
                        SyncEmploymentMarkers(record.pawn, record.status, GenDate.TicksPerDay, true);
                        Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyNarrativeVisitorIdentityChanged(record.pawn);
                        continue;
                    }

                    if (record.temporaryUntilTick <= 0)
                    {
                        record.temporaryUntilTick = nowTick + System.Math.Max(600, DurationTicksForStatus(record.status));
                    }

                    SyncEmploymentMarkers(record.pawn, record.status, GetRemainingTimerTicks(record), false);
                }
                catch (System.Exception exception)
                {
                    // Drop one malformed record during load instead of retrying it every tick.
                    Log.Warning("[MouseDisaster] Removed an invalid visitor record after load: " + exception.Message);
                    RemoveRecordAt(i);
                }
            }

            RebuildRecordIndex();
        }

        private static bool CanTrackPawn(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map != null && MouseDisasterUtility.IsRatkin(pawn);
        }

        private static void BringPawnUnderPlayerProtection(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            MouseDisasterUtility.TryReleaseLeadYourPetTradePawn(pawn);
            ReleaseGuestState(pawn);
            MouseDisasterUtility.UnmarkTradableChattel(pawn);
            MouseDisasterUtility.ConsumeForcedPrisonerOnPurchase(pawn);
            MouseDisasterUtility.ClearTradeLeaderState(pawn);
            MouseDisasterUtility.ResetBeggarState(pawn);
            pawn.mindState?.mentalStateHandler?.Reset();
            if (pawn.mindState != null)
            {
                pawn.mindState.duty = null;
                pawn.mindState.canFleeIndividual = true;
                pawn.mindState.exitMapAfterTick = -1;
            }
            pawn.jobs?.StopAll();

            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            if (pawn.guest != null)
            {
                pawn.guest.joinStatus = JoinStatus.JoinAsColonist;
            }

            MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
            MouseDisasterUtility.RemoveChildExchangeTrackingForPurchasedPawn(pawn);
        }

        private static bool TrySetVisitorCaptiveStatus(Pawn pawn, GuestStatus status)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, actAsIfSpawned: true);
            if (pawn.guest == null)
            {
                PawnComponentsUtility.CreateInitialComponents(pawn);
            }

            if (pawn.guest == null)
            {
                return false;
            }

            MouseDisasterUtility.TryReleaseLeadYourPetTradePawn(pawn);
            ReleaseGuestState(pawn);
            MouseDisasterUtility.UnmarkTradableChattel(pawn);
            MouseDisasterUtility.ConsumeForcedPrisonerOnPurchase(pawn);
            MouseDisasterUtility.ClearTradeLeaderState(pawn);
            MouseDisasterUtility.ResetBeggarState(pawn);
            MouseDisasterUtility.RemoveAllIncidentVisitorHediffs(pawn);
            pawn.mindState?.mentalStateHandler?.Reset();
            if (pawn.mindState != null)
            {
                pawn.mindState.duty = null;
                pawn.mindState.canFleeIndividual = false;
                pawn.mindState.exitMapAfterTick = -1;
            }

            pawn.jobs?.StopAll();
            pawn.GetLord()?.RemovePawn(pawn);
            pawn.guest.joinStatus = status == GuestStatus.Slave
                ? JoinStatus.JoinAsSlave
                : JoinStatus.JoinAsColonist;

            if (status == GuestStatus.Prisoner)
            {
                if (pawn.Faction == Faction.OfPlayer)
                {
                    pawn.SetFaction(null);
                }

                pawn.guest.CapturedBy(Faction.OfPlayer);
            }
            else
            {
                pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
            }

            MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
            MouseDisasterUtility.RemoveChildExchangeTrackingForPurchasedPawn(pawn);
            return status == GuestStatus.Slave ? pawn.IsSlaveOfColony : pawn.IsPrisonerOfColony;
        }

        private static bool TrySendPawnToPrison(Pawn pawn, Map map, IntVec3 prisonCell)
        {
            if (pawn == null || map == null || !prisonCell.IsValid || !prisonCell.InBounds(map))
            {
                return false;
            }

            if (prisonCell.GetDoor(map) != null)
            {
                return false;
            }

            List<Thing> cellThings = prisonCell.GetThingList(map);
            for (int i = 0; i < cellThings.Count; i++)
            {
                Thing thing = cellThings[i];
                if (thing is Pawn || (thing.def?.building?.isSittable ?? false))
                {
                    return false;
                }
            }

            MouseDisasterUtility.TryReleaseLeadYourPetTradePawn(pawn);
            ReleaseGuestState(pawn);
            MouseDisasterUtility.UnmarkTradableChattel(pawn);
            MouseDisasterUtility.ConsumeForcedPrisonerOnPurchase(pawn);
            MouseDisasterUtility.ResetBeggarState(pawn);
            MouseDisasterUtility.RemoveAllIncidentVisitorHediffs(pawn);
            pawn.mindState?.mentalStateHandler?.Reset();
            if (pawn.mindState != null)
            {
                pawn.mindState.duty = null;
                pawn.mindState.canFleeIndividual = false;
                pawn.mindState.exitMapAfterTick = -1;
            }

            pawn.jobs?.StopAll();
            pawn.GetLord()?.RemovePawn(pawn);
            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, actAsIfSpawned: true);
            if (pawn.guest == null)
            {
                return false;
            }

            if (pawn.Faction == Faction.OfPlayer)
            {
                pawn.SetFaction(null);
            }

            pawn.guest.joinStatus = JoinStatus.JoinAsColonist;
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);

            if (pawn.Spawned && pawn.Map == map)
            {
                pawn.DeSpawnOrDeselect();
            }

            if (!pawn.Spawned)
            {
                GenSpawn.Spawn(pawn, prisonCell, map, WipeMode.Vanish);
            }

            MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
            MouseDisasterUtility.RemoveChildExchangeTrackingForPurchasedPawn(pawn);
            return pawn.IsPrisonerOfColony;
        }

        private static void RestoreOriginalIdentityAndForceLeave(MouseDisasterVisitorRecord record)
        {
            Pawn pawn = record?.pawn;
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            // Identity callbacks during restoration must not pause an already expired contract.
            record.status = MouseDisasterVisitorStatus.Visitor;
            record.temporaryUntilTick = -1;
            record.employmentTimerPaused = false;

            ReleaseGuestState(pawn);
            MouseDisasterUtility.UnmarkTradableChattel(pawn);
            MouseDisasterUtility.ConsumeForcedPrisonerOnPurchase(pawn);
            MouseDisasterUtility.ClearTradeLeaderState(pawn);
            pawn.mindState?.mentalStateHandler?.Reset();
            pawn.mindState?.duty = null;
            pawn.jobs?.StopAll();
            pawn.GetLord()?.RemovePawn(pawn);

            if (record.originalKindDef != null && pawn.kindDef != record.originalKindDef)
            {
                pawn.ChangeKind(record.originalKindDef);
            }

            if (pawn.Faction != record.originalFaction)
            {
                pawn.SetFaction(record.originalFaction);
            }

            if (record.originalFaction != null)
            {
                MouseDisasterUtility.MakeFactionNeutralToPlayer(record.originalFaction);
            }

            SyncEmploymentMarkers(pawn, MouseDisasterVisitorStatus.Visitor);
            MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);

            GameComponent_MouseDisasterEventBehavior.Component?.RequestDeparture(pawn);
        }

        private static void SyncEmploymentMarkers(Pawn pawn, MouseDisasterVisitorStatus status,
            int remainingTimerTicks = -1, bool timerPaused = false)
        {
            if (pawn == null)
            {
                return;
            }

            MouseDisasterUtility.RemoveHediffByDef(pawn, MouseDisasterDefOf.MouseDisaster_TemporaryShelterMark);
            MouseDisasterUtility.RemoveHediffByDef(pawn, MouseDisasterDefOf.MouseDisaster_HiredWorkerMark);

            if (status == MouseDisasterVisitorStatus.TemporaryRecruit)
            {
                MouseDisasterUtility.AddOrRefreshHediff(pawn, MouseDisasterDefOf.MouseDisaster_TemporaryShelterMark, 1f);
            }
            else if (status == MouseDisasterVisitorStatus.HiredWorker)
            {
                MouseDisasterUtility.AddOrRefreshHediff(pawn, MouseDisasterDefOf.MouseDisaster_HiredWorkerMark, 1f);
            }

            if (status == MouseDisasterVisitorStatus.TemporaryRecruit ||
                status == MouseDisasterVisitorStatus.HiredWorker)
            {
                HediffDef markerDef = status == MouseDisasterVisitorStatus.TemporaryRecruit
                    ? MouseDisasterDefOf.MouseDisaster_TemporaryShelterMark
                    : MouseDisasterDefOf.MouseDisaster_HiredWorkerMark;
                Hediff marker = pawn.health?.hediffSet?.GetFirstHediffOfDef(markerDef);
                HediffComp_MouseDisasterEmploymentTimer timer =
                    marker?.TryGetComp<HediffComp_MouseDisasterEmploymentTimer>();
                if (timer != null)
                {
                    timer.SetDuration(System.Math.Max(1, remainingTimerTicks > 0
                        ? remainingTimerTicks
                        : GenDate.TicksPerDay));
                    timer.disabled = timerPaused;
                }
            }
        }

        private static void PauseEmploymentMarkerTimer(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffComp_MouseDisasterEmploymentTimer timer = pawn.health.hediffSet
                .GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_TemporaryShelterMark)?
                .TryGetComp<HediffComp_MouseDisasterEmploymentTimer>();
            timer ??= pawn.health.hediffSet
                .GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_HiredWorkerMark)?
                .TryGetComp<HediffComp_MouseDisasterEmploymentTimer>();
            if (timer == null)
            {
                return;
            }

            if (timer.ticksToDisappear <= 0)
            {
                timer.SetDuration(1);
            }

            timer.disabled = true;
        }

        private static int GetRemainingTimerTicks(MouseDisasterVisitorRecord record)
        {
            if (record == null || record.employmentTimerPaused)
            {
                return GenDate.TicksPerDay;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            return record.temporaryUntilTick > 0
                ? System.Math.Max(1, record.temporaryUntilTick - nowTick)
                : System.Math.Max(600, DurationTicksForStatus(record.status));
        }

        private static int DurationTicksForStatus(MouseDisasterVisitorStatus status)
        {
            return status == MouseDisasterVisitorStatus.HiredWorker
                ? MouseDisasterVisitorUtility.HiredWorkerDurationTicks
                : MouseDisasterVisitorUtility.TemporaryRecruitDurationTicks;
        }

        private static bool IsIdentityChangedEmployment(Pawn pawn)
        {
            return pawn != null && (pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony);
        }

        private static bool CanLeaveMapUnderOwnPower(Pawn pawn)
        {
            return pawn != null &&
                   !pawn.Downed &&
                   pawn.DevelopmentalStage != DevelopmentalStage.Baby;
        }

        private static void ReleaseGuestState(Pawn pawn)
        {
            if (pawn?.guest?.HostFaction == null)
            {
                return;
            }

            pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
        }
    }

    public static class MouseDisasterVisitorUtility
    {
        public static int TemporaryRecruitDurationDays => MouseDisasterMod.Settings?.GetTemporaryRecruitDurationDays() ??
            MouseDisasterSettings.DefaultTemporaryRecruitDurationDays;
        public static int TemporaryRecruitDurationTicks => GenDate.DaysToTicks(TemporaryRecruitDurationDays);
        public static int HiredWorkerDurationDays => MouseDisasterMod.Settings?.GetHiredWorkerDurationDays() ??
            MouseDisasterSettings.DefaultHiredWorkerDurationDays;
        public static int HiredWorkerDurationTicks => GenDate.DaysToTicks(HiredWorkerDurationDays);
        public static string TemporaryRecruitDurationLabel =>
            FormatDurationLabel(TemporaryRecruitDurationTicks);
        public static string HiredWorkerDurationLabel =>
            FormatDurationLabel(HiredWorkerDurationTicks);

        public static string FormatDurationLabel(int ticks)
        {
            int years;
            int quadrums;
            int days;
            float hours;
            System.Math.Max(0, ticks).TicksToPeriod(out years, out quadrums, out days, out hours);
            int totalDays = days + quadrums * GenDate.DaysPerQuadrum;
            List<string> parts = new List<string>(2);
            if (years > 0)
            {
                parts.Add(years == 1
                    ? "Period1Year".Translate().ToString()
                    : "PeriodYears".Translate(years).ToString());
            }
            if (totalDays > 0)
            {
                parts.Add(totalDays == 1
                    ? "Period1Day".Translate().ToString()
                    : "PeriodDays".Translate(totalDays).ToString());
            }
            if (parts.Count > 0)
            {
                return string.Join(", ", parts);
            }

            if (System.Math.Round(hours, 1) == 1.0)
            {
                return "Period1Hour".Translate().ToString();
            }

            return "PeriodHours".Translate(hours.ToString("0.#")).ToString();
        }

        private static GameComponent_MouseDisasterVisitorControl Component => Current.Game?.GetComponent<GameComponent_MouseDisasterVisitorControl>();

        public static void RegisterVisitors(IEnumerable<Pawn> pawns)
        {
            Component?.RegisterVisitors(pawns);
        }

        public static bool IsManagedVisitor(Pawn pawn)
        {
            return Component?.IsManagedVisitor(pawn) ?? false;
        }

        public static void NotifyPawnIdentityChanged(Pawn pawn)
        {
            Component?.NotifyPawnIdentityChanged(pawn);
        }

        public static void RemoveVisitorRecord(Pawn pawn)
        {
            Component?.RemoveVisitorRecord(pawn);
        }

        public static bool IsShelteredVisitor(Pawn pawn)
        {
            return Component?.IsShelteredVisitor(pawn) ?? false;
        }

        public static bool CanHire(Pawn pawn)
        {
            return !IsN007ControlBlocked(new[] { pawn }) && (Component?.CanHire(pawn) ?? false);
        }

        public static bool CanEndHire(Pawn pawn)
        {
            return !IsN007ControlBlocked(new[] { pawn }) && (Component?.CanEndHire(pawn) ?? false);
        }

        public static bool CanJoin(Pawn pawn)
        {
            return !IsN007ControlBlocked(new[] { pawn }) && (Component?.CanJoin(pawn) ?? false);
        }

        public static bool HasShelterMood(Pawn pawn)
        {
            return IsShelteredVisitor(pawn);
        }

        public static List<Pawn> GetActiveVisitors(IEnumerable<Pawn> pawns)
        {
            return Component?.GetActiveVisitors(pawns) ?? new List<Pawn>();
        }

        public static List<Pawn> GetRecruitableVisitors(IEnumerable<Pawn> pawns)
        {
            return Component?.GetRecruitableVisitors(pawns) ?? new List<Pawn>();
        }

        public static bool TryTemporaryRecruitAll(IEnumerable<Pawn> pawns, out int recruitedCount)
        {
            recruitedCount = 0;
            return !IsN007ControlBlocked(pawns) && Component != null && Component.TryTemporaryRecruit(pawns, TemporaryRecruitDurationTicks, out recruitedCount);
        }

        public static bool TryHire(Pawn pawn)
        {
            return !IsN007ControlBlocked(new[] { pawn }) && Component != null && Component.TryHire(pawn);
        }

        public static bool TryHireAll(IEnumerable<Pawn> pawns, out int hiredCount)
        {
            hiredCount = 0;
            return !IsN007ControlBlocked(pawns) && Component != null && Component.TryHire(pawns, out hiredCount);
        }

        public static bool TryEndHire(Pawn pawn)
        {
            return !IsN007ControlBlocked(new[] { pawn }) && Component != null && Component.TryEndHire(pawn);
        }

        public static bool TryJoin(Pawn pawn)
        {
            return !IsN007ControlBlocked(new[] { pawn }) && Component != null && Component.TryJoin(pawn);
        }

        public static bool TryJoinAll(IEnumerable<Pawn> pawns, out int joinedCount)
        {
            joinedCount = 0;
            return !IsN007ControlBlocked(pawns) && Component != null && Component.TryJoin(pawns, out joinedCount);
        }

        public static bool TryEnslaveAll(IEnumerable<Pawn> pawns, out int enslavedCount, out string failureMessage)
        {
            enslavedCount = 0;
            failureMessage = "MouseDisaster_VisitorControl_NoValidTarget".Translate();
            if (IsN007ControlBlocked(pawns))
            {
                failureMessage = "MouseDisaster_N007_VisitorControlBlocked".Translate();
                return false;
            }

            return ModsConfig.IdeologyActive &&
                   Component != null &&
                   Component.TryEnslave(pawns, out enslavedCount, out failureMessage);
        }

        public static bool TryCaptureAll(IEnumerable<Pawn> pawns, out int capturedCount, out string failureMessage)
        {
            capturedCount = 0;
            failureMessage = "MouseDisaster_VisitorControl_NoValidTarget".Translate();
            if (IsN007ControlBlocked(pawns))
            {
                failureMessage = "MouseDisaster_N007_VisitorControlBlocked".Translate();
                return false;
            }

            return Component != null &&
                   Component.TryCapture(pawns, out capturedCount, out failureMessage);
        }

        public static bool HasAvailablePrisonArea(Map map)
        {
            return MouseDisasterPrisonTransferUtility.HasAvailablePrisonArea(map);
        }

        public static bool IsPrisonIntegrationEnabled()
        {
            return MouseDisasterPrisonTransferUtility.IsPrisonIntegrationEnabled();
        }

        public static bool TrySendToPrison(IEnumerable<Pawn> pawns, Map map, out int imprisonedCount, out string failureMessage)
        {
            imprisonedCount = 0;
            failureMessage = "MouseDisaster_VisitorControl_NoValidTarget".Translate();
            if (IsN007ControlBlocked(pawns))
            {
                failureMessage = "MouseDisaster_N007_VisitorControlBlocked".Translate();
                return false;
            }
            return Component != null && Component.TrySendToPrison(pawns, map, out imprisonedCount, out failureMessage);
        }

        public static bool TryMakeHostile(IEnumerable<Pawn> pawns, out int hostileCount)
        {
            hostileCount = 0;
            List<Pawn> targets = pawns?.ToList() ?? new List<Pawn>();
            if (IsN007ControlBlocked(targets) || Component == null || !Component.TryMakeHostile(targets, out hostileCount)) return false;
            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyNarrativeForce(targets);
            return true;
        }

        public static bool IsN007ControlBlocked(IEnumerable<Pawn> pawns)
        {
            GameComponent_MouseDisasterNarrative narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
            return narrative != null && pawns != null && pawns.Any(pawn => pawn != null &&
                narrative.IsN007VisitorControlBlocked(pawn.MapHeld, new[] { pawn }));
        }

        public static bool SendVisitorChoiceLetter(IncidentDef incidentDef, IncidentParms parms, Map map, IEnumerable<Pawn> pawns)
        {
            if (incidentDef == null || map == null)
            {
                return false;
            }

            List<Pawn> candidates = pawns?
                .Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map == map)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            RegisterVisitors(candidates);
            List<Pawn> visitorList = GetActiveVisitors(candidates);
            if (visitorList.Count == 0)
            {
                return false;
            }

            try
            {
                ChoiceLetter_MouseDisasterVisitorControl letter =
                    LetterMaker.MakeLetter(incidentDef.letterLabel, incidentDef.letterText, MouseDisasterDefOf.MouseDisaster_VisitorControlLetter, visitorList) as ChoiceLetter_MouseDisasterVisitorControl;
                if (letter == null)
                {
                    Log.Warning("[MouseDisaster] Visitor choice letter could not be created for " + incidentDef.defName + ".");
                    return false;
                }

                letter.map = map;
                letter.visitors = visitorList;
                letter.incidentDefName = incidentDef.defName;
                Find.LetterStack.ReceiveLetter(letter, null);
                return true;
            }
            catch (System.Exception exception)
            {
                Log.Error("[MouseDisaster] Visitor choice letter failed for " + incidentDef.defName + ": " + exception);
                return false;
            }
        }

        public static bool RegisterAndSendVisitorChoiceLetter(IncidentDef incidentDef, IncidentParms parms, Map map, IEnumerable<Pawn> pawns)
        {
            List<Pawn> pawnList = pawns?
                .Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map == map)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (pawnList.Count == 0)
            {
                return false;
            }

            RegisterVisitors(pawnList);
            return SendVisitorChoiceLetter(incidentDef, parms, map, pawnList);
        }
    }
}
