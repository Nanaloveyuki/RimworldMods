using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_BroadcastArrivalTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousProgramState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly ScribeSaver previousSaver = Scribe.saver;
        readonly ScribeLoader previousLoader = Scribe.loader;
        readonly LoadSaveMode previousMode = Scribe.mode;
        readonly bool previousProfiling = DeepProfiler.enabled;
        readonly object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
        readonly string path = Path.GetTempFileName();
        readonly Dictionary<int, int> previousBegCooldowns = new Dictionary<int, int>(
            (Dictionary<int, int>)Field(typeof(HungerAndHavoc.Pawn.RHAH_Begging), "NextBegTickByPawnId").GetValue(null));
        readonly Dictionary<int, HashSet<int>> previousBeggedColonists = new Dictionary<int, HashSet<int>>(
            (Dictionary<int, HashSet<int>>)Field(typeof(HungerAndHavoc.Pawn.RHAH_Begging), "BeggedColonists").GetValue(null));

        public RHAH_BroadcastArrivalTests()
        {
            Current.Game = Uninitialized<Game>();
            Current.Game.components = new List<GameComponent>();
            Current.Game.tickManager = new TickManager();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            Current.ProgramState = ProgramState.Entry;
            RHAH_Mod.Settings = new RHAH_Settings { staggerGeneration = true };
            Scribe.saver = new ScribeSaver();
            Scribe.loader = new ScribeLoader();
            Scribe.mode = LoadSaveMode.Inactive;
            DeepProfiler.enabled = false;
            Field(typeof(Prefs), "data").SetValue(null, new PrefsData { devMode = false });
        }

        public void Dispose()
        {
            Scribe.ForceStop();
            Scribe.saver = previousSaver;
            Scribe.loader = previousLoader;
            Scribe.mode = previousMode;
            DeepProfiler.enabled = previousProfiling;
            Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
            Dictionary<int, int> cooldowns = (Dictionary<int, int>)Field(typeof(HungerAndHavoc.Pawn.RHAH_Begging), "NextBegTickByPawnId").GetValue(null);
            Dictionary<int, HashSet<int>> begged = (Dictionary<int, HashSet<int>>)Field(typeof(HungerAndHavoc.Pawn.RHAH_Begging), "BeggedColonists").GetValue(null);
            cooldowns.Clear();
            begged.Clear();
            foreach (KeyValuePair<int, int> entry in previousBegCooldowns)
            {
                cooldowns.Add(entry.Key, entry.Value);
            }
            foreach (KeyValuePair<int, HashSet<int>> entry in previousBeggedColonists)
            {
                begged.Add(entry.Key, entry.Value);
            }
            Current.Game = previousGame;
            Current.ProgramState = previousProgramState;
            RHAH_Mod.Settings = previousSettings;
            File.Delete(path);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(41)]
        public void ExplicitBroadcastTargetKeepsTheConsoleMapWhileUnavailable(int mapId)
        {
            Map other = Map(9);
            Map consoleMap = Map(mapId);
            Find.Maps.Add(other);
            Find.Maps.Add(consoleMap);
            int targetId = RHAH_IncidentSchedule.TargetId(false, consoleMap.uniqueID);

            RHAH_QueuedTarget target = RHAH_QueuedTarget.Resolve(RHAH_IncidentTarget.Map, targetId);

            Assert.False(target.Terminal);
            Assert.True(target.Unavailable);
            Assert.Null(target.Target);
            Assert.Same(consoleMap, RHAH_MapResolver.MapById(mapId));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(41)]
        public void MissingExplicitTargetDoesNotRedirectToAnotherHome(int mapId)
        {
            Find.Maps.Add(Map(9));
            int targetId = RHAH_IncidentSchedule.TargetId(false, mapId);

            RHAH_QueuedTarget target = RHAH_QueuedTarget.Resolve(RHAH_IncidentTarget.Map, targetId);

            Assert.True(target.Terminal);
            Assert.Null(target.Target);
        }

        [Fact]
        public void LegacyUnspecifiedZeroWaitsWithoutUsableMaps()
        {
            Find.Maps.Add(Map(9));

            Assert.True(RHAH_QueuedTarget.Resolve(RHAH_IncidentTarget.Map, 0).Unavailable);
            Assert.True(RHAH_QueuedTarget.Resolve(RHAH_IncidentTarget.Map, int.MinValue).Terminal);
        }

        [Fact]
        public void FirstMapQueueAndRemainingBatchRoundTripWithoutReplayingConsumedItem()
        {
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(Current.Game);
            game.EventChainMigrationStatus = "completed";
            Assert.True(game.QueueIncident("unknown", 40f, 9));
            Assert.True(game.QueueIncident("I-005", 260f, RHAH_IncidentSchedule.TargetId(false, 0)));
            Assert.True(game.QueueIncident("I-004", 80f, 41));
            Assert.False(game.TrySpawnPending(1));
            Assert.Equal(3, game.PendingIncidentDisplayIds.Count);
            typeof(GameComponent_RHAH_Game).GetMethod("DropPending", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { 0 });
            Assert.Equal(new[] { "I-005", "I-004" }, game.PendingIncidentDisplayIds);
            game.RegisterBatch("9:64");

            Save(game);
            GameComponent_RHAH_Game loaded = LoadGame();
            Save(loaded);
            GameComponent_RHAH_Game reloaded = LoadGame();

            Assert.Equal(new[] { "I-005", "I-004" }, reloaded.PendingIncidentDisplayIds);
            Assert.Equal(new[] { int.MinValue, 41 }, reloaded.PendingIncidentTargetIds);
            Assert.Equal(new[] { 260f, 80f },
                (List<float>)Field(typeof(GameComponent_RHAH_Game), "pendingIncidentPoints").GetValue(reloaded));
            Assert.Equal(new[] { "9:64" }, reloaded.ActiveGenerationBatches);
            Assert.False(reloaded.QueueIncident("I-005", 999f, 9));
            Map first = Map(0);
            Find.Maps.Add(Map(9));
            Find.Maps.Add(first);
            Assert.Same(first, RHAH_MapResolver.MapById(
                RHAH_IncidentSchedule.DecodeTargetId(reloaded.PendingIncidentTargetIds[0], RHAH_IncidentTarget.Map)));
        }

        [Fact]
        public void LegacyQueueWithoutTargetsKeepsUnspecifiedSemanticsAfterResave()
        {
            File.WriteAllText(path, "<record><pendingIncidentDisplayIds><li>I-005</li></pendingIncidentDisplayIds>" +
                "<pendingIncidentPoints><li>260</li></pendingIncidentPoints></record>");
            GameComponent_RHAH_Game loaded = LoadGame();
            Assert.Equal(new[] { 0 }, loaded.PendingIncidentTargetIds);
            Save(loaded);
            Assert.Equal(new[] { 0 }, LoadGame().PendingIncidentTargetIds);
        }


        [Fact]
        public void InvalidGenerationCannotPublishArrivalChoiceOrNarrativeFacts()
        {
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components.Add(game);
            Map first = Map(0);
            Find.Maps.Add(first);

            Assert.False(RHAH_IncidentFacts.Submit(new RHAH_IncidentContext
            {
                DisplayId = "I-005",
                Map = first,
                SpawnCell = IntVec3.Invalid,
                SpawnBatchId = 71,
                RelationshipGroupId = 71,
                PawnCount = 3,
                Role = RHAH_PawnRole.Refugee
            }));

            Assert.Empty(game.OpenChoices);
            Assert.Empty(game.ActiveGenerationBatches);
        }


        [Fact]
        public void BroadcastQueuesDistinctResponsesOnTheConsoleMapAndSkipsPendingCandidates()
        {
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components.Add(game);
            Assert.True(game.QueueIncident("I-005", 260f, 9));
            int targetId = RHAH_IncidentSchedule.TargetId(false, 0);
            List<string> candidates = new List<string> { "I-005", "I-004", "I-012" };

            Assert.Equal(2, HungerAndHavoc.Pawn.RHAH_BroadcastMenu.QueueResponses(game, targetId, candidates, 3));
            Assert.Empty(candidates);
            Assert.Equal(3, game.PendingIncidentDisplayIds.Count);
            Assert.Equal(9, game.PendingIncidentTargetIds[0]);
            Assert.Equal(targetId, game.PendingIncidentTargetIds[1]);
            Assert.Equal(targetId, game.PendingIncidentTargetIds[2]);
            Assert.Contains("I-004", game.PendingIncidentDisplayIds);
            Assert.Contains("I-012", game.PendingIncidentDisplayIds);
            Assert.Equal(0, HungerAndHavoc.Pawn.RHAH_BroadcastMenu.QueueResponses(game, targetId,
                new List<string> { "I-005", "I-004", "I-012" }, 3));
        }

        void Save(IExposable value)
        {
            Scribe.saver.InitSaving(path, "record");
            value.ExposeData();
            Scribe.saver.FinalizeSaving();
        }

        GameComponent_RHAH_Game LoadGame()
        {
            Scribe.loader.InitLoading(path);
            GameComponent_RHAH_Game loaded = new GameComponent_RHAH_Game(Current.Game);
            Scribe.loader.curParent = loaded;
            loaded.ExposeData();
            Scribe.loader.initer.RegisterForPostLoadInit(loaded);
            Scribe.loader.FinalizeLoading();
            return loaded;
        }


        static Map Map(int id)
        {
            Map map = Uninitialized<Map>();
            map.uniqueID = id;
            map.mapPawns = null;
            return map;
        }


        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(type.FullName + "." + name);
        }
    }
}
