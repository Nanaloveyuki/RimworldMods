using System;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Incidents;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;

namespace HungerAndHavoc.Core
{
    public sealed class GameComponent_RHAH_Game : GameComponent
    {
        List<string> activeGenerationBatches = new List<string>();
        List<string> pendingIncidentDisplayIds = new List<string>();
        List<float> pendingIncidentPoints = new List<float>();
        List<int> pendingIncidentTargetIds = new List<int>();
        List<int> begCooldownPawnIds = new List<int>();
        List<int> begCooldownTicks = new List<int>();
        List<int> beggedPawnIds = new List<int>();
        List<int> beggedColonistIds = new List<int>();
        List<RHAH_ChoiceRecord> openChoices = new List<RHAH_ChoiceRecord>();
        int nextChoiceId = 1;
        int broadcastCooldownUntilTick = -1;
        int generationCursor;
        List<RHAH_EventChainRecord> eventChains = new List<RHAH_EventChainRecord>();
        int nextEventChainId = 1;
        string eventChainMigrationStatus = "";
        bool eventChainsLoaded;
        bool newContentDisabled;
        List<RHAH_LongFollowRecord> longFollows = new List<RHAH_LongFollowRecord>();
        int longClaimChainId;

        public IReadOnlyList<string> ActiveGenerationBatches => activeGenerationBatches;
        public IReadOnlyList<string> PendingIncidentDisplayIds => pendingIncidentDisplayIds;
        public IReadOnlyList<int> PendingIncidentTargetIds => pendingIncidentTargetIds;
        public IReadOnlyList<RHAH_ChoiceRecord> OpenChoices => openChoices;
        public int BroadcastCooldownUntilTick { get => broadcastCooldownUntilTick; set => broadcastCooldownUntilTick = value; }
        internal int NextChoiceId { get => nextChoiceId; set => nextChoiceId = value; }
        internal IReadOnlyList<RHAH_EventChainRecord> LegacyEventChains => eventChains;
        internal int LegacyNextEventChainId => nextEventChainId;
        internal string EventChainMigrationStatus { get => eventChainMigrationStatus; set => eventChainMigrationStatus = value; }
        internal bool EventChainsLoaded => eventChainsLoaded;
        internal bool NewContentDisabled => newContentDisabled;
        internal List<RHAH_LongFollowRecord> LongFollows => longFollows;
        internal int LongClaimChainId { get => longClaimChainId; set => longClaimChainId = value; }


        public GameComponent_RHAH_Game(Game game)
        {
        }

        public override void FinalizeInit()
        {
            Pawn.RHAH_AttitudeFactions.LockGoodwill();
            RHAH_IncidentFacts.RestoreFamilyRelations(PawnsFinder.All_AliveOrDead);
        }

        public override void GameComponentTick()
        {
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            TickPlague();
            RHAH_ChoiceRuntime.Tick(this, tick);
            TrySpawnPending(tick);
            TickStays(tick);
            if (tick % GenDate.TicksPerHour == 0)
            {
                Pawn.RHAH_AttitudeFactions.LockGoodwill();
            }

            HungerAndHavoc.Storyteller.Suiyin.RHAH_EndingRuntime.Tick(tick);
            HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.Tick(tick);
            HungerAndHavoc.Storyteller.Suiyin.RHAH_Revisit.Tick(tick);
            HungerAndHavoc.Storyteller.Suiyin.RHAH_JournalRuntime.Tick(tick);
            HungerAndHavoc.Storyteller.Suiyin.RHAH_Envoy.Tick(tick);
            HungerAndHavoc.Storyteller.Suiyin.RHAH_RecordSite.Tick(tick);
        }

        public override void GameComponentUpdate()
        {
            if (!RHAH_Scheduler.ShouldDrainOnFrame(
                Find.TickManager != null && Find.TickManager.Paused,
                Find.WindowStack != null && Find.WindowStack.WindowsForcePause,
                pendingIncidentDisplayIds.Count))
            {
                return;
            }

            TrySpawnPending(0);
        }


        internal bool TrySpawnPending(int tick)
        {
            if (RHAH_Mod.Settings != null && RHAH_Mod.Settings.staggerGeneration && (tick & 63) != 0)
            {
                return false;
            }

            if (pendingIncidentDisplayIds.Count == 0 || Current.Game == null)
            {
                return false;
            }

            int index = NextPendingIndex();
            if (index < 0)
            {
                return false;
            }

            string displayId = pendingIncidentDisplayIds[index];
            float points = index < pendingIncidentPoints.Count
                ? pendingIncidentPoints[index]
                : PointsFor(displayId);
            int targetId = index < pendingIncidentTargetIds.Count
                ? pendingIncidentTargetIds[index]
                : RHAH_IncidentSchedule.UnspecifiedTargetId;
            HungerAndHavoc.Incidents.RHAH_IncidentEntry entry =
                HungerAndHavoc.Incidents.RHAH_IncidentCatalog.GetByDisplayId(displayId);
            if (entry == null)
            {
                Log.Warning("[RHAH] Dropped queued incident " + displayId + ". It is not in the catalog.");
                DropPending(index);
                return false;
            }

            RHAH_QueuedTarget queued = RHAH_QueuedTarget.Resolve(entry.Target, targetId);
            if (queued.Terminal)
            {
                Log.Warning("[RHAH] Dropped queued incident " + displayId + ". " + queued.Reason +
                    " points=" + points.ToString("0.##") + " target=" + targetId);
                DropPending(index);
                return false;
            }

            if (entry.Target == HungerAndHavoc.Incidents.RHAH_IncidentTarget.Map && queued.Target is Map saved)
            {
                int approachMap = saved.uniqueID;
                if (HungerAndHavoc.Incidents.RHAH_Approach.TrySend(displayId, points, approachMap))
                {
                    Log.Message("[RHAH] Approach started for " + displayId + ". points=" + points.ToString("0.##") +
                        " map=" + approachMap);
                    DropPending(index);
                    return true;
                }

                Log.Warning("[RHAH] Approach failed for " + displayId + ". Spawning on the map instead.");
            }

            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(entry.DefName);
            if (def == null || def.Worker == null)
            {
                Log.Warning("[RHAH] Dropped queued incident " + displayId + ". Def " + entry.DefName + " or its worker is missing.");
                DropPending(index);
                return false;
            }

            IncidentParms parms = new IncidentParms { target = queued.Target, points = points };
            return ExecutePending(index, displayId, entry, points, parms, queued.Target);
        }

        internal RHAH_ArrivalResult SpawnArrived(string displayId, float points, int mapId)
        {
            if (Current.Game == null)
            {
                return RHAH_ArrivalResult.Waiting;
            }

            HungerAndHavoc.Incidents.RHAH_IncidentEntry entry =
                HungerAndHavoc.Incidents.RHAH_IncidentCatalog.GetByDisplayId(displayId);
            if (entry == null || entry.Target != HungerAndHavoc.Incidents.RHAH_IncidentTarget.Map ||
                NewContentDisabled)
            {
                Log.Warning("[RHAH] Arrived approach cancelled " + displayId + ". map=" + mapId);
                return RHAH_ArrivalResult.Terminal;
            }

            Map map = null;
            List<Map> maps = Find.Maps;
            for (int i = 0; maps != null && i < maps.Count; i++)
            {
                if (maps[i] != null && maps[i].uniqueID == mapId)
                {
                    map = maps[i];
                    break;
                }
            }

            if (map == null)
            {
                Log.Warning("[RHAH] Arrived approach cancelled " + displayId + ". Saved map is gone. map=" + mapId);
                return RHAH_ArrivalResult.Terminal;
            }

            if (map.mapPawns == null || map.Parent == null || map.Parent.Destroyed || !RHAH_Runtime.AllowsNewContent)
            {
                return RHAH_ArrivalResult.Waiting;
            }

            IncidentDef arrivedDef = DefDatabase<IncidentDef>.GetNamedSilentFail(entry.DefName);
            if (arrivedDef == null || arrivedDef.Worker == null)
            {
                Log.Warning("[RHAH] Arrived approach cancelled " + displayId + ". Worker is missing.");
                return RHAH_ArrivalResult.Terminal;
            }

            IncidentParms parms = new IncidentParms { target = map, points = points };
            try
            {
                if (arrivedDef.requireColonistsPresent && map.mapPawns.FreeColonistsSpawnedCount == 0)
                {
                    return RHAH_ArrivalResult.Waiting;
                }

                if (!arrivedDef.Worker.TryExecute(parms))
                {
                    return RHAH_ArrivalResult.Waiting;
                }
            }
            catch (Exception exception)
            {
                // 异常可能已产生部分角色 不能自动重跑整批
                Log.Error("[RHAH] Arrived approach failed " + displayId + ". map=" + mapId + "\n" + exception);
                return RHAH_ArrivalResult.Terminal;
            }

            Log.Message("[RHAH] Spawned arrived " + displayId + ". points=" + points.ToString("0.##") + " map=" + mapId);
            return RHAH_ArrivalResult.Spawned;
        }

        internal bool SpawnDebugOnMap(HungerAndHavoc.Incidents.RHAH_IncidentEntry entry, float points, Map map)
        {
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(entry.DefName);
            if (def == null || def.Worker == null || map == null || !GameComponent_RHAH_FamineYear.Available(entry.DisplayId))
            {
                return false;
            }

            IncidentParms parms = new IncidentParms { target = map, points = points, forced = true };
            bool spawned = ExecutePending(-1, entry.DisplayId, entry, points, parms, map);
            if (spawned)
            {
                GameComponent_RHAH_FamineYear.NoteQueued(entry.DisplayId);
            }
            return spawned;
        }

        bool ExecutePending(
            int index,
            string displayId,
            HungerAndHavoc.Incidents.RHAH_IncidentEntry entry,
            float points,
            IncidentParms parms,
            IIncidentTarget target)
        {
            bool executed;
            try
            {
                executed = DefDatabase<IncidentDef>.GetNamedSilentFail(entry.DefName).Worker.TryExecute(parms);
            }
            catch (Exception exception)
            {
                Log.Error("[RHAH] Queued " + displayId + " threw while spawning. def=" + entry.DefName +
                    " points=" + points.ToString("0.##") + " target=" + TargetText(entry, target) + "\n" + exception);
                DropPending(index);
                return false;
            }

            if (!executed)
            {
                Log.Warning("[RHAH] Dropped queued " + displayId + ". Worker did not spawn. def=" + entry.DefName +
                    " points=" + points.ToString("0.##") + " target=" + TargetText(entry, target));
                DropPending(index);
                return false;
            }

            Log.Message("[RHAH] Spawned queued " + displayId + ". def=" + entry.DefName +
                " points=" + points.ToString("0.##") + " target=" + TargetText(entry, target));
            DropPending(index);
            return true;
        }


        int NextPendingIndex()
        {
            int count = pendingIncidentDisplayIds.Count;
            bool[] terminal = new bool[count];
            bool[] ready = new bool[count];
            for (int i = 0; i < count; i++)
            {
                HungerAndHavoc.Incidents.RHAH_IncidentEntry entry =
                    HungerAndHavoc.Incidents.RHAH_IncidentCatalog.GetByDisplayId(pendingIncidentDisplayIds[i]);
                if (entry == null)
                {
                    terminal[i] = true;
                    continue;
                }

                int targetId = i < pendingIncidentTargetIds.Count
                    ? pendingIncidentTargetIds[i]
                    : RHAH_IncidentSchedule.UnspecifiedTargetId;
                RHAH_QueuedTarget queued = RHAH_QueuedTarget.Resolve(entry.Target, targetId);
                if (queued.Terminal)
                {
                    terminal[i] = true;
                    continue;
                }

                if (queued.Unavailable)
                {
                    continue;
                }

                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(entry.DefName);
                ready[i] = def != null && def.Worker != null;
                terminal[i] = !ready[i];
            }

            return RHAH_IncidentSchedule.NextExecutable(terminal, ready);
        }

        static string TargetText(HungerAndHavoc.Incidents.RHAH_IncidentEntry entry, IIncidentTarget target)
        {
            if (entry.Target == HungerAndHavoc.Incidents.RHAH_IncidentTarget.Caravan)
            {
                RimWorld.Planet.Caravan caravan = target as RimWorld.Planet.Caravan;
                return caravan == null ? "caravan:none" : "caravan:" + caravan.ID;
            }

            Map map = target as Map;
            return map == null ? "map:none" : "map:" + map.uniqueID;
        }

        public void RegisterBatch(string batchKey)
        {
            if (!string.IsNullOrEmpty(batchKey) && !activeGenerationBatches.Contains(batchKey))
            {
                activeGenerationBatches.Add(batchKey);
            }
        }

        public bool QueueIncident(string displayId)
        {
            return QueueIncident(displayId, PointsFor(displayId), RHAH_IncidentSchedule.UnspecifiedTargetId);
        }

        public bool QueueIncident(string displayId, float points)
        {
            return QueueIncident(displayId, points, RHAH_IncidentSchedule.UnspecifiedTargetId);
        }

        public bool QueueIncident(string displayId, float points, int targetId)
        {
            if (string.IsNullOrEmpty(displayId) || pendingIncidentDisplayIds.Contains(displayId) ||
                !GameComponent_RHAH_FamineYear.Available(displayId))
            {
                return false;
            }

            pendingIncidentDisplayIds.Add(displayId);
            pendingIncidentPoints.Add(RHAH_IncidentTuning.ClampPoints(points));
            pendingIncidentTargetIds.Add(targetId);
            GameComponent_RHAH_FamineYear.NoteQueued(displayId);
            return true;
        }

        internal static float PointsFor(string displayId)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            float catalog = entry == null ? RHAH_IncidentTuning.MinDebugPoints : entry.DebugPoints;
            return RHAH_Mod.Settings == null
                ? catalog
                : RHAH_Mod.Settings.IncidentDebugPoints(displayId, catalog);
        }

        void DropPending(int index)
        {
            if (index < 0)
            {
                return;
            }

            if (index < pendingIncidentDisplayIds.Count)
            {
                pendingIncidentDisplayIds.RemoveAt(index);
            }

            if (index < pendingIncidentPoints.Count)
            {
                pendingIncidentPoints.RemoveAt(index);
            }

            if (index < pendingIncidentTargetIds.Count)
            {
                pendingIncidentTargetIds.RemoveAt(index);
            }
        }
        void TickPlague()
        {
            if (Find.TickManager == null || Find.Maps == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick % GenDate.TicksPerHour != 0)
            {
                return;
            }

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (map == null || !map.IsPlayerHome)
                {
                    continue;
                }

                RHAH_Settings settings = RHAH_Mod.Settings;
                int spreadHour = settings == null ? Identity.RHAH_Plague.SpreadHour : settings.plagueSpreadHour;
                if (GenLocalDate.HourInteger(map) != spreadHour)
                {
                    continue;
                }

                map.GetComponent<MapComponent_RHAH_Map>()?.TickPlague();
            }
        }


        public override void ExposeData()
        {
            Scribe_Collections.Look(ref activeGenerationBatches, "activeGenerationBatches", LookMode.Value);
            Scribe_Collections.Look(ref pendingIncidentTargetIds, "pendingIncidentTargetIds", LookMode.Value);
            Scribe_Collections.Look(ref pendingIncidentDisplayIds, "pendingIncidentDisplayIds", LookMode.Value);
            Scribe_Collections.Look(ref pendingIncidentPoints, "pendingIncidentPoints", LookMode.Value);
            Scribe_Collections.Look(ref openChoices, "openChoices", LookMode.Deep);
            Scribe_Values.Look(ref nextChoiceId, "nextChoiceId", 1);
            Scribe_Values.Look(ref broadcastCooldownUntilTick, "broadcastCooldownUntilTick", -1);
            Scribe_Values.Look(ref generationCursor, "generationCursor", 0);
            Scribe_Collections.Look(ref begCooldownPawnIds, "begCooldownPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref begCooldownTicks, "begCooldownTicks", LookMode.Value);
            Scribe_Collections.Look(ref beggedPawnIds, "beggedPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref beggedColonistIds, "beggedColonistIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.Saving && eventChainMigrationStatus == "")
            {
                eventChainMigrationStatus = "failed: ODF provider initialization unavailable; original data retained";
            }
            if (Scribe.mode != LoadSaveMode.Saving || eventChainMigrationStatus.StartsWith("failed:", StringComparison.Ordinal))
            {
                Scribe_Collections.Look(ref eventChains, "eventChains", LookMode.Deep);
                Scribe_Values.Look(ref nextEventChainId, "nextEventChainId", 1);
            }

            Scribe_Values.Look(ref eventChainMigrationStatus, "eventChainMigrationStatus", "");
            Scribe_Values.Look(ref newContentDisabled, "newContentDisabled", false);
            Scribe_Collections.Look(ref longFollows, "longFollows", LookMode.Deep);
            Scribe_Values.Look(ref longClaimChainId, "longClaimChainId", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                activeGenerationBatches = activeGenerationBatches ?? new List<string>();
                pendingIncidentDisplayIds = pendingIncidentDisplayIds ?? new List<string>();
                pendingIncidentPoints = pendingIncidentPoints ?? new List<float>();
                pendingIncidentTargetIds = pendingIncidentTargetIds ?? new List<int>();
                while (pendingIncidentPoints.Count < pendingIncidentDisplayIds.Count)
                {
                    pendingIncidentPoints.Add(PointsFor(pendingIncidentDisplayIds[pendingIncidentPoints.Count]));
                }

                while (pendingIncidentTargetIds.Count < pendingIncidentDisplayIds.Count)
                {
                    pendingIncidentTargetIds.Add(RHAH_IncidentSchedule.UnspecifiedTargetId);
                }

                while (pendingIncidentPoints.Count > pendingIncidentDisplayIds.Count)
                {
                    pendingIncidentPoints.RemoveAt(pendingIncidentPoints.Count - 1);
                }

                while (pendingIncidentTargetIds.Count > pendingIncidentDisplayIds.Count)
                {
                    pendingIncidentTargetIds.RemoveAt(pendingIncidentTargetIds.Count - 1);
                }
                openChoices = openChoices ?? new List<RHAH_ChoiceRecord>();
                begCooldownPawnIds = begCooldownPawnIds ?? new List<int>();
                begCooldownTicks = begCooldownTicks ?? new List<int>();
                beggedPawnIds = beggedPawnIds ?? new List<int>();
                beggedColonistIds = beggedColonistIds ?? new List<int>();
                Align(begCooldownPawnIds, begCooldownTicks, -1);
                Align(beggedPawnIds, beggedColonistIds, 0);
                Pawn.RHAH_Begging.Load(begCooldownPawnIds, begCooldownTicks, beggedPawnIds, beggedColonistIds);
                eventChains = eventChains ?? new List<RHAH_EventChainRecord>();
                eventChainMigrationStatus = eventChainMigrationStatus ?? "";
                eventChainsLoaded = true;
                longFollows = longFollows ?? new List<RHAH_LongFollowRecord>();
                for (int i = longFollows.Count - 1; i >= 0; i--)
                {
                    if (longFollows[i] == null) longFollows.RemoveAt(i);
                }
                for (int i = openChoices.Count - 1; i >= 0; i--)
                {
                    if (openChoices[i] == null)
                    {
                        openChoices.RemoveAt(i);
                        continue;
                    }

                    openChoices[i].PawnLoadIds = openChoices[i].PawnLoadIds ?? new List<int>();
                }
            }
        }
        internal void DisableNewContent()
        {
            Current.Game?.GetComponent<HungerAndHavoc.Narrative.NarrativeState>()?.Kingdom.Disable();
            newContentDisabled = true;
            OxygenEventChainHost.Runtime?.SetMode(RHAH_EventChainProvider.ProviderKey, EventChainMode.Disabled,
                Find.TickManager == null ? 0 : Find.TickManager.TicksGame, "Current save new content disabled");
        }
        static void TickStays(int tick)
        {
            if (tick % (GenDate.TicksPerHour * 2) != 0 || Find.Maps == null)
            {
                return;
            }

            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                Map map = Find.Maps[mapIndex];
                if (map?.mapPawns?.AllPawnsSpawned == null)
                {
                    continue;
                }

                List<Verse.Pawn> pawns = SimplePool<List<Verse.Pawn>>.Get();
                try
                {
                    IReadOnlyList<Verse.Pawn> spawned = map.mapPawns.AllPawnsSpawned;
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        pawns.Add(spawned[i]);
                        Verse.Pawn carried = spawned[i].carryTracker?.CarriedThing as Verse.Pawn;
                        if (carried != null && !pawns.Contains(carried))
                        {
                            pawns.Add(carried);
                        }
                    }
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        HungerAndHavoc.Pawn.RHAH_VisitorStay.Tick(pawns[i], tick);
                    }
                    HungerAndHavoc.Pawn.RHAH_VisitorStay.RebuildDepartureGroups(pawns, map);
                }
                finally
                {
                    pawns.Clear();
                    SimplePool<List<Verse.Pawn>>.Return(pawns);
                }
            }
        }

        internal void StoreBegging(Dictionary<int, int> cooldowns, Dictionary<int, HashSet<int>> begged)
        {
            begCooldownPawnIds.Clear();
            begCooldownTicks.Clear();
            beggedPawnIds.Clear();
            beggedColonistIds.Clear();
            if (cooldowns != null)
            {
                foreach (KeyValuePair<int, int> pair in cooldowns)
                {
                    begCooldownPawnIds.Add(pair.Key);
                    begCooldownTicks.Add(pair.Value);
                }
            }

            if (begged == null)
            {
                return;
            }

            foreach (KeyValuePair<int, HashSet<int>> pair in begged)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                foreach (int colonist in pair.Value)
                {
                    beggedPawnIds.Add(pair.Key);
                    beggedColonistIds.Add(colonist);
                }
            }
        }

        static void Align(List<int> left, List<int> right, int fill)
        {
            while (right.Count < left.Count)
            {
                right.Add(fill);
            }

            while (right.Count > left.Count)
            {
                right.RemoveAt(right.Count - 1);
            }
        }

        internal void ClearLegacyEventChains()
        {
            eventChains.Clear();
            nextEventChainId = 1;
        }
    }

    public sealed class MapComponent_RHAH_Map : MapComponent
    {
        List<int> visitorPawnLoadIds = new List<int>();
        Dictionary<int, int> foodSearchTicks = new Dictionary<int, int>();
        List<Verse.Thing> cachedFoods;
        List<Verse.Thing> cachedPlants;
        int foodListTick = -1;
        Dictionary<int, int> wallGnawCounts = new Dictionary<int, int>();
        int plagueLastSpreadDay = -1;
        List<Verse.Pawn> predationPrey = new List<Verse.Pawn>();
        List<HungerAndHavoc.Incidents.RHAH_PredatorRecord> predators = new List<HungerAndHavoc.Incidents.RHAH_PredatorRecord>();
        bool predationRolled;
        bool predationSelected;
        int predationPendingTick = -1;
        int predationArrivalInstanceId;
        int predationNextTick = -1;
        int holeThingId;

        public IReadOnlyList<int> VisitorPawnLoadIds => visitorPawnLoadIds;
        public int PlagueLastSpreadDay { get => plagueLastSpreadDay; set => plagueLastSpreadDay = value; }
        public bool PredationRolled { get => predationRolled; set => predationRolled = value; }
        public bool PredationSelected { get => predationSelected; set => predationSelected = value; }
        internal int LegacyPredationPendingTick { get => predationPendingTick; set => predationPendingTick = value; }
        internal int PredationArrivalInstanceId { get => predationArrivalInstanceId; set => predationArrivalInstanceId = value; }
        public int PredationNextTick { get => predationNextTick; set => predationNextTick = value; }
        public int HoleThingId { get => holeThingId; set => holeThingId = value; }
        public List<Verse.Pawn> PredationPrey => predationPrey;
        public List<HungerAndHavoc.Incidents.RHAH_PredatorRecord> Predators => predators;
        public MapComponent_RHAH_Map(Map map) : base(map)
        {
        }

        public override void FinalizeInit()
        {
            Pawn.RHAH_ReliefArea.Ensure(map);
        }

        public void RegisterVisitor(int pawnLoadId)
        {
            if (pawnLoadId > 0 && !visitorPawnLoadIds.Contains(pawnLoadId))
            {
                visitorPawnLoadIds.Add(pawnLoadId);
            }
        }

        public void RemoveVisitor(int pawnLoadId)
        {
            visitorPawnLoadIds.Remove(pawnLoadId);
            foodSearchTicks.Remove(pawnLoadId);
            wallGnawCounts.Remove(pawnLoadId);
        }

        public bool FoodSearchReady(int pawnLoadId, int tick)
        {
            int next;
            if (!foodSearchTicks.TryGetValue(pawnLoadId, out next))
            {
                return true;
            }

            return tick >= next;
        }

        public void SetFoodSearchTick(int pawnLoadId, int tick)
        {
            if (pawnLoadId <= 0)
            {
                return;
            }

            foodSearchTicks[pawnLoadId] = tick;
        }

        public void InvalidateFoodSearch()
        {
            foodSearchTicks.Clear();
            InvalidateFoodLists();
        }

        public void FoodLists(int tick, out List<Verse.Thing> foods, out List<Verse.Thing> plants)
        {
            if (cachedFoods == null || foodListTick != tick)
            {
                foodListTick = tick;
                cachedFoods = map?.listerThings?.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
                cachedPlants = map?.listerThings?.ThingsInGroup(ThingRequestGroup.HarvestablePlant);
            }

            foods = cachedFoods;
            plants = cachedPlants;
        }

        public void InvalidateFoodLists()
        {
            foodListTick = -1;
            cachedFoods = null;
            cachedPlants = null;
        }

        public int NextWallGnaw(int pawnLoadId)
        {
            return HungerAndHavoc.Pawn.RHAH_GnawHealth.NextWallCount(wallGnawCounts, pawnLoadId);
        }

        public void ForgetWallGnaw(int pawnLoadId)
        {
            HungerAndHavoc.Pawn.RHAH_GnawHealth.Forget(wallGnawCounts, pawnLoadId);
        }
        public void TickPlague()
        {
            Identity.RHAH_PlagueRuntime.TickMap(this, map);
        }
        public void RememberPredationPrey(Verse.Pawn pawn)
        {
            if (pawn != null && !predationPrey.Contains(pawn))
            {
                predationPrey.Add(pawn);
            }
        }

        public bool IsPredationPrey(Verse.Pawn pawn)
        {
            return pawn != null && predationPrey.Contains(pawn);
        }

        public void TrackPredator(Verse.Pawn pawn, bool outside, int tick)
        {
            if (pawn == null)
            {
                return;
            }

            for (int i = 0; i < predators.Count; i++)
            {
                if (predators[i]?.pawn == pawn)
                {
                    predators[i].outside = outside;
                    predators[i].nextSearchTick = tick;
                    return;
                }
            }

            predators.Add(new HungerAndHavoc.Incidents.RHAH_PredatorRecord
            {
                pawn = pawn,
                outside = outside,
                nextSearchTick = tick
            });
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            bool predatorsDue = Predators.Count > 0 && tick >= PredationNextTick;
            if (predatorsDue)
            {
                bool camp = map.Parent is WorldObject_RHAH_RefugeeCamp;
                HungerAndHavoc.Incidents.RHAH_CampPredation.TickSite(this, map, camp || predationPrey.Count > 0);
            }

            if (tick % (GenDate.TicksPerHour * 2) == map.uniqueID % (GenDate.TicksPerHour * 2))
            {
                HungerAndHavoc.Incidents.RHAH_GrainHole.Tick(map);
            }
        }


        public override void ExposeData()
        {
            Scribe_Collections.Look(ref visitorPawnLoadIds, "visitorPawnLoadIds", LookMode.Value);
            Scribe_Collections.Look(ref foodSearchTicks, "foodSearchTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref wallGnawCounts, "wallGnawCounts", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref plagueLastSpreadDay, "plagueLastSpreadDay", -1);
            Scribe_Collections.Look(ref predationPrey, "predationPrey", LookMode.Reference);
            Scribe_Collections.Look(ref predators, "predators", LookMode.Deep);
            Scribe_Values.Look(ref predationRolled, "predationRolled", false);
            Scribe_Values.Look(ref predationSelected, "predationSelected", false);
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (Scribe.mode != LoadSaveMode.Saving || (game?.EventChainMigrationStatus ?? "").StartsWith("failed:", StringComparison.Ordinal))
            {
                Scribe_Values.Look(ref predationPendingTick, "predationPendingTick", -1);
            }

            Scribe_Values.Look(ref predationArrivalInstanceId, "predationArrivalInstanceId", 0);
            Scribe_Values.Look(ref predationNextTick, "predationNextTick", -1);
            Scribe_Values.Look(ref holeThingId, "holeThingId", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                visitorPawnLoadIds = visitorPawnLoadIds ?? new List<int>();
                foodSearchTicks = foodSearchTicks ?? new Dictionary<int, int>();
                wallGnawCounts = wallGnawCounts ?? new Dictionary<int, int>();
                predationPrey = predationPrey ?? new List<Verse.Pawn>();
                predators = predators ?? new List<HungerAndHavoc.Incidents.RHAH_PredatorRecord>();
            }
        }
    }
}