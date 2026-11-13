using System;
using System.Collections.Generic;
using System.Globalization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class RHAH_AllyDeparture : IEventChainDefinition
    {
        internal const string DefinitionId = "RHAH_AllyDeparture";
        internal const string ProgressKey = "rhah:allyExitProgress";
        internal const int StalledTicks = 4 * GenDate.TicksPerHour;

        public string Id => DefinitionId;
        public int Version => 1;

        internal static void Start(RHAH_ChoiceRecord record, List<Verse.Pawn> pawns, int tick)
        {
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            if (runtime == null) throw new InvalidOperationException("ODF ally departure queue is not ready.");
            for (int i = 0; i < pawns.Count; i++) NoteProgress(pawns[i], tick);
            runtime.Start(RHAH_EventChainProvider.ProviderKey, DefinitionId, record.MapId,
                RHAH_EventChainMigration.SiteTile(record.MapId), tick, -1,
                record.Id.ToString(CultureInfo.InvariantCulture), 0,
                RHAH_WarlordBombardment.NextTick(tick, GenDate.TicksPerHour));
        }

        public void Prepare(EventChainInstance instance)
        {
            if (instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.SiteId < 0 || instance.Stage != 0)
                throw new FormatException("Invalid RHAH ally departure instance.");
            if (instance.End != EventChainEnd.None) return;
            if (!int.TryParse(instance.Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int choiceId) || choiceId <= 0)
                throw new FormatException("Invalid RHAH ally departure payload.");
            instance.PreparedState = choiceId;
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            return EventChainDecision.Action("check_departure", RHAH_WarlordBombardment.NextTick(tick, GenDate.TicksPerHour));
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "check_departure" || !(instance.PreparedState is int choiceId))
                return EventChainActionResult.Failed("Ally departure is not prepared or has an unknown action.");
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            RHAH_ChoiceRecord record = game == null ? null : RHAH_ChoiceRuntime.FindRecord(game, choiceId);
            if (record == null || record.AllyPawnIds.Count == 0)
                return EventChainActionResult.Applied("no_pending_transfers", EventChainEnd.Completed);
            if (record.MapId != instance.SiteId)
                return EventChainActionResult.Failed("Ally departure map does not match the transfer record.");
            Map map = RHAH_ChoiceRuntime.FindMap(record.MapId);
            RimWorld.Faction destination = RHAH_ChoiceRuntime.FindFaction(record.AllyFactionId);
            if (map == null || destination == null || destination.defeated)
                return EventChainActionResult.Applied("destination_unavailable", EventChainEnd.Completed);

            for (int i = record.AllyPawnIds.Count - 1; i >= 0; i--)
            {
                int pawnId = record.AllyPawnIds[i];
                Verse.Pawn pawn = RHAH_PawnIndex.Find(pawnId, tick);
                IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.MapHeld != map || pawn.IsPrisoner || pawn.IsSlave ||
                    pawn.Faction == RimWorld.Faction.OfPlayer || snapshot == null || snapshot.SpawnBatchId != record.BatchId ||
                    snapshot.SourceIncidentDisplayId != record.DisplayId || snapshot.Lifecycle != RHAH_Lifecycle.Leaving)
                {
                    record.AllyPawnIds.RemoveAt(i);
                    if (pawn != null) RHAH_Api.SetExtra(pawn, ProgressKey, null);
                    continue;
                }
                if (!RHAH_ApiHost.CanTransfer(pawn)) continue;
                if (!ReadProgress(pawn, out IntVec3 previous, out int lastTick) || previous != pawn.PositionHeld || tick < lastTick)
                {
                    NoteProgress(pawn, tick);
                    continue;
                }
                if (!Stalled(tick, lastTick) || !pawn.Spawned) continue;
                pawn.ExitMap(false, Rot4.Invalid);
                if (!pawn.SpawnedOrAnyParentSpawned && record.AllyPawnIds.Contains(pawnId))
                    RHAH_ChoiceRuntime.TryJoinAlly(pawn);
            }
            return EventChainActionResult.Applied("departure_checked",
                record.AllyPawnIds.Count == 0 ? EventChainEnd.Completed : EventChainEnd.None);
        }

        internal static bool Stalled(int tick, int lastProgressTick)
        {
            return (long)tick - lastProgressTick > StalledTicks;
        }

        static void NoteProgress(Verse.Pawn pawn, int tick)
        {
            IntVec3 cell = pawn.PositionHeld;
            RHAH_Api.SetExtra(pawn, ProgressKey, cell.x.ToString(CultureInfo.InvariantCulture) + "|" +
                cell.z.ToString(CultureInfo.InvariantCulture) + "|" + tick.ToString(CultureInfo.InvariantCulture));
        }

        static bool ReadProgress(Verse.Pawn pawn, out IntVec3 cell, out int tick)
        {
            cell = IntVec3.Invalid;
            tick = -1;
            if (!RHAH_Api.TryGetExtra(pawn, ProgressKey, out string saved)) return false;
            string[] parts = saved.Split('|');
            if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out tick) || tick < 0) return false;
            cell = new IntVec3(x, 0, z);
            return true;
        }
    }
}
