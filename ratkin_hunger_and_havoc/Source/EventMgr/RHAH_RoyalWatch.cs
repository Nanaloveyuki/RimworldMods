using System;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class RHAH_RoyalWatch : IEventChainDefinition
    {
        internal const string DefinitionId = "RHAH_RoyalWatch";
        public string Id => DefinitionId;
        public int Version => 1;

        internal static int NextCheck(int tick, int localDayTick)
        {
            int delay = (2 * GenDate.TicksPerHour - localDayTick + GenDate.TicksPerDay) % GenDate.TicksPerDay;
            long next = (long)tick + (delay == 0 ? GenDate.TicksPerDay : delay);
            return next > int.MaxValue ? -1 : (int)next;
        }

        internal static int NextCheck(int tick)
        {
            int earliest = -1;
            if (Find.Maps != null)
            {
                for (int i = 0; i < Find.Maps.Count; i++)
                {
                    Map map = Find.Maps[i];
                    if (!map.IsPlayerHome) continue;
                    int next = NextCheck(tick, GenLocalDate.DayTick(map));
                    if (next >= 0 && (earliest < 0 || next < earliest)) earliest = next;
                }
            }
            return earliest >= 0 ? earliest :
                (long)tick + GenDate.TicksPerHour > int.MaxValue ? -1 : tick + GenDate.TicksPerHour;
        }

        internal static void PrepareGame(EventChainRuntime runtime)
        {
            EventChainProviderState state = runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey);
            if (state == null || state.SchemaVersion != EventChainRuntime.SchemaVersion || state.StateVersion != 1 ||
                (state.MigrationStatus ?? "").StartsWith("failed:", StringComparison.Ordinal)) return;
            for (int i = 0; i < state.Instances.Count; i++)
                if (state.Instances[i]?.DefinitionId == DefinitionId) return;
            if (state.NextId <= 0 || state.NextId == int.MaxValue)
                throw new InvalidOperationException("RHAH royal watch instance IDs are exhausted.");
            int tick = Find.TickManager?.TicksGame ?? 0;
            state.Instances.Add(new EventChainInstance
            {
                Id = state.NextId++,
                DefinitionId = DefinitionId,
                SiteId = -1,
                Tile = -1,
                StartedTick = tick,
                NextTick = NextCheck(tick)
            });
        }

        internal static void NoteHomeAvailable(Map map)
        {
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            EventChainProviderState state = runtime?.GetProviderState(RHAH_EventChainProvider.ProviderKey);
            if (map?.IsPlayerHome != true || state == null) return;
            int tick = Find.TickManager.TicksGame;
            int next = NextCheck(tick, GenLocalDate.DayTick(map));
            for (int i = 0; i < state.Instances.Count; i++)
            {
                EventChainInstance instance = state.Instances[i];
                if (instance.DefinitionId != DefinitionId || instance.End != EventChainEnd.None || next < 0 ||
                    instance.NextTick >= 0 && instance.NextTick <= next) continue;
                runtime.SetStage(RHAH_EventChainProvider.ProviderKey, instance.Id, 0, -1, string.Empty, next);
                return;
            }
        }

        public void Prepare(EventChainInstance instance)
        {
            if (instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.Stage != 0 ||
                instance.SiteId != -1 || instance.Payload.Length != 0)
                throw new FormatException("Invalid RHAH royal wealth watch instance.");
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            return EventChainDecision.Action("check_silver", NextCheck(tick));
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "check_silver") return EventChainActionResult.Failed("Unknown royal wealth watch action.");
            if (!RHAH_Runtime.AllowsNewContent || RHAH_Mod.Settings?.IsIncidentEnabled(RHAH_SuspiciousRoyal.DisplayId) == false)
                return EventChainActionResult.Applied("content_disabled");
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (game == null || Find.Maps == null) return EventChainActionResult.Applied("no_game");
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (!map.IsPlayerHome) continue;
                int dayTick = GenLocalDate.DayTick(map);
                if (dayTick < 2 * GenDate.TicksPerHour || dayTick >= 3 * GenDate.TicksPerHour) continue;
                MapComponent_RHAH_Map component = map.GetComponent<MapComponent_RHAH_Map>();
                int day = GenLocalDate.Year(map) * GenDate.DaysPerYear + GenLocalDate.DayOfYear(map);
                if (component.RoyalWealthCheckDay == day) continue;
                component.RoyalWealthCheckDay = day;
                if (!RHAH_SuspiciousRoyal.Available(game.LastRoyalArrivalTick, tick, RHAH_RoyalSilver.Stored(map))) continue;
                IncidentDef def = DefDatabase<IncidentDef>.GetNamed("RHAH_SuspiciousRoyal");
                if (def.Worker.TryExecute(new IncidentParms { target = map, points = 200f }))
                    return EventChainActionResult.Applied("royal_arrived");
            }
            return EventChainActionResult.Applied("silver_checked");
        }
    }
}
