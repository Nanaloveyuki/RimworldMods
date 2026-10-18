using System;
using System.Collections.Generic;
using System.Globalization;
using HungerAndHavoc.Core;
using Oxygendsl.Core.Events;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal static class RHAH_EventChainMigration
    {
        internal static int NextLongTick(int tick, int id)
        {
            const int interval = 2500;
            if (tick < 0 || id <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            int delta = (id % interval - tick % interval + interval) % interval;
            long next = (long)tick + (delta == 0 ? interval : delta);
            return next > int.MaxValue ? -1 : (int)next;
        }

        internal static List<EventChainInstance> Convert(IReadOnlyList<RHAH_EventChainRecord> records, int nextId, int tick, out int importedNextId)
        {
            if (records == null || nextId <= 0 || tick < 0)
            {
                throw new FormatException("Invalid legacy event chain collection, counter or tick.");
            }

            HashSet<int> ids = new HashSet<int>();
            List<EventChainInstance> converted = new List<EventChainInstance>(records.Count);
            int maximum = 0;
            for (int i = 0; i < records.Count; i++)
            {
                RHAH_EventChainRecord record = records[i];
                if (record == null || record.id <= 0 || record.id == int.MaxValue || !ids.Add(record.id) ||
                    !IsIncidentId(record.displayId) || record.stage < 0 || record.stage > 2 || record.startedTick < 0 ||
                    record.deadlineTick < -1 || record.end < 0 || record.end > 4 || (!record.closed && record.end != 0) ||
                    !RHAH_EventFollowRules.ReadPayload(record.payload, out string payloadId, out int batch, out int site) ||
                    payloadId != record.displayId || site != record.siteId)
                {
                    throw new FormatException("Invalid legacy RHAH event chain at index " + i + ". Originals retained; no actions executed.");
                }

                EventChainEnd end = record.closed ? (record.end == 0 ? EventChainEnd.Completed : (EventChainEnd)record.end) : EventChainEnd.None;
                converted.Add(new EventChainInstance
                {
                    Id = record.id,
                    DefinitionId = record.displayId,
                    DefinitionVersion = 1,
                    SiteId = record.siteId,
                    Tile = SiteTile(record.siteId),
                    Stage = record.stage,
                    StartedTick = record.startedTick,
                    DeadlineTick = record.deadlineTick,
                    NextTick = end != EventChainEnd.None || record.stage == 2 ? -1 :
                        record.stage == 1 ? NextLongTick(tick, record.id) : (tick == int.MaxValue ? -1 : tick + 1),
                    Payload = record.payload,
                    End = end
                });
                maximum = Math.Max(maximum, record.id);
            }

            importedNextId = Math.Max(nextId, maximum + 1);
            return converted;
        }

        static bool IsIncidentId(string value)
        {
            return value != null && value.Length == 5 && value[0] == 'I' && value[1] == '-' &&
                int.TryParse(value.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= 54;
        }

        internal static int SiteTile(int siteId)
        {
            if (siteId >= 0)
            {
                Map map = RHAH_EventFollowRuntime.FindMap(siteId);
                return map == null ? -1 : map.Tile.tileId;
            }

            List<WorldObject> objects = Current.Game == null ? null : Find.WorldObjects?.AllWorldObjects;
            if (siteId < 0 && siteId != int.MinValue && objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i] is RimWorld.Planet.Caravan caravan && caravan.ID == -siteId)
                    {
                        return caravan.Tile.tileId;
                    }
                }
            }

            return -1;
        }

        internal static void PrepareGame(EventChainRuntime runtime)
        {
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (game == null)
            {
                return;
            }

            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            if (runtime.HasProviderState(RHAH_EventChainProvider.ProviderKey))
            {
                EventChainProviderState existing = runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey);
                if ((existing.MigrationStatus ?? "").StartsWith("failed:", StringComparison.Ordinal))
                {
                    game.EventChainMigrationStatus = existing.MigrationStatus;
                    runtime.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Paused, tick, existing.MigrationStatus);
                    Log.Error("[RHAH] " + existing.MigrationStatus);
                    return;
                }

                try
                {
                    RHAH_LongFollowRuntime.PrepareGame(runtime);
                }
                catch (Exception exception)
                {
                    string reason = "failed: " + exception.Message;
                    existing.MigrationStatus = reason;
                    game.EventChainMigrationStatus = reason;
                    runtime.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Paused, tick, reason);
                    Log.Error("[RHAH] Event chain migration " + reason + ". Original chains and map arrivals remain saved and never execute.");
                    return;
                }
                ClearOriginals(game);
                if (game.NewContentDisabled)
                {
                    runtime.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Disabled, tick, "Current save new content disabled");
                }

                return;
            }

            try
            {
                List<EventChainInstance> instances = Convert(game.LegacyEventChains, game.LegacyNextEventChainId, tick, out int nextId);
                List<Map> maps = Find.Maps;
                Dictionary<MapComponent_RHAH_Map, int> arrivals = new Dictionary<MapComponent_RHAH_Map, int>();
                if (maps != null)
                {
                    for (int i = 0; i < maps.Count; i++)
                    {
                        Map map = maps[i];
                        MapComponent_RHAH_Map component = map?.GetComponent<MapComponent_RHAH_Map>();
                        if (component != null && component.LegacyPredationPendingTick < -1)
                        {
                            throw new FormatException("Invalid legacy RHAH predation arrival tick.");
                        }

                        if (component == null || component.LegacyPredationPendingTick < 0)
                        {
                            continue;
                        }

                        if (nextId == int.MaxValue)
                        {
                            throw new FormatException("RHAH arrival migration instance counter overflow.");
                        }

                        int id = nextId++;
                        instances.Add(new EventChainInstance
                        {
                            Id = id,
                            DefinitionId = RHAH_EventChainProvider.ArrivalId,
                            DefinitionVersion = 1,
                            SiteId = map.uniqueID,
                            Tile = map.Tile.tileId,
                            StartedTick = Math.Max(0, component.LegacyPredationPendingTick - RHAH_PredationDelay),
                            NextTick = component.LegacyPredationPendingTick,
                            Payload = map.uniqueID.ToString(CultureInfo.InvariantCulture)
                        });
                        arrivals.Add(component, id);
                    }
                }

                if (!runtime.Import(RHAH_EventChainProvider.ProviderKey, instances, nextId, "completed", out string error))
                {
                    throw new FormatException(error);
                }

                RHAH_LongFollowRuntime.PrepareGame(runtime);
                foreach (KeyValuePair<MapComponent_RHAH_Map, int> pair in arrivals)
                {
                    pair.Key.PredationArrivalInstanceId = pair.Value;
                }

                ClearOriginals(game);
                if (game.NewContentDisabled)
                {
                    runtime.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Disabled, tick, "Current save new content disabled");
                }
            }
            catch (Exception exception)
            {
                string reason = "failed: " + exception.Message;
                game.EventChainMigrationStatus = reason;
                if (!runtime.HasProviderState(RHAH_EventChainProvider.ProviderKey))
                {
                    runtime.Import(RHAH_EventChainProvider.ProviderKey, new List<EventChainInstance>(), 1, reason, out string ignored);
                }

                runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey).MigrationStatus = reason;
                runtime.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Paused, tick, reason);
                Log.Error("[RHAH] Event chain migration " + reason + ". Original chains and map arrivals remain saved and never execute.");
            }
        }

        const int RHAH_PredationDelay = 120;

        static void ClearOriginals(GameComponent_RHAH_Game game)
        {
            game.ClearLegacyEventChains();
            game.EventChainMigrationStatus = "completed";
            List<Map> maps = Find.Maps;
            if (maps != null)
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    MapComponent_RHAH_Map component = maps[i]?.GetComponent<MapComponent_RHAH_Map>();
                    if (component != null)
                    {
                        component.LegacyPredationPendingTick = -1;
                    }
                }
            }
        }
    }
}
