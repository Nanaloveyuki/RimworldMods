using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using RimWorld.Planet;
using HungerAndHavoc.EventMgr;
using Oxygendsl.Core.Events;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_LongFollowTests : IDisposable
    {
        readonly Game previousGame;

        static RHAH_LongFollowTests()
        {
            RimWorldAssemblies.EnsureResolved();
            bool initializing = UnityDataInitializer.initializing;
            UnityDataInitializer.initializing = true;
            try
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(UnityData).TypeHandle);
            }
            finally
            {
                UnityDataInitializer.initializing = initializing;
            }
        }

        public RHAH_LongFollowTests()
        {
            previousGame = Current.Game;
            Current.Game = null;
        }

        public void Dispose()
        {
            Current.Game = previousGame;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PreparationMigratesLongDeadlineBeforeActivationEvenForDisabledPartitions(bool disabled)
        {
            GameComponent_RHAH_Game game = SetUpGame();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { ChainId = 7, DisplayId = "I-019", BatchId = 31, MapId = 0 };
            game.LongFollows.Add(record);
            EventChainProviderState state = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 8, MigrationStatus = "completed",
                Mode = disabled ? EventChainMode.Disabled : EventChainMode.Active
            };
            state.Instances.Add(new EventChainInstance
            {
                Id = 7, DefinitionId = record.DisplayId, Stage = 1, SiteId = 0, DeadlineTick = 100,
                Payload = RHAH_EventFollowRules.Payload(record.DisplayId, record.BatchId, record.MapId), NextTick = 7
            });
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { state });
            runtime.Register(new RHAH_EventChainProvider());
            Assert.Null(runtime.Find(RHAH_EventChainProvider.ProviderKey, 7));
            RHAH_EventChainMigration.PrepareGame(runtime);
            RHAH_LongFollowRuntime.PrepareGame(runtime);
            Assert.Equal(3, state.Instances[0].Stage);
            Assert.Equal(-1, state.Instances[0].DeadlineTick);
            Assert.Equal(100, record.DueTick);
            runtime.Activate(0);
            runtime.Tick(101);
            Assert.NotEqual(EventChainEnd.Expired, state.Instances[0].End);
            Assert.Equal(disabled ? EventChainMode.Disabled : EventChainMode.Active, state.Mode);
            Assert.Equal(100, record.DueTick);
        }

        [Fact]
        public void FailedPartitionDoesNotAcquireLongRecordsOrChangeStages()
        {
            GameComponent_RHAH_Game game = SetUpGame();
            EventChainProviderState state = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 8, MigrationStatus = "failed: retained originals", Mode = EventChainMode.Paused
            };
            state.Instances.Add(new EventChainInstance { Id = 7, DefinitionId = "I-019", Stage = 1, DeadlineTick = 100, Payload = "I-019|31|0" });
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { state });
            runtime.Register(new RHAH_EventChainProvider());
            RHAH_LongFollowRuntime.PrepareGame(runtime);
            Assert.Empty(game.LongFollows);
            Assert.Equal(1, state.Instances[0].Stage);
            Assert.Equal(100, state.Instances[0].DeadlineTick);
            Assert.Equal(EventChainMode.Paused, state.Mode);
        }

        [Fact]
        public void InvalidLaterLongPayloadDoesNotPartiallyMigrateEarlierInstances()
        {
            GameComponent_RHAH_Game game = SetUpGame();
            EventChainProviderState state = new EventChainProviderState { ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 9 };
            state.Instances.Add(new EventChainInstance { Id = 7, DefinitionId = "I-019", Stage = 1, DeadlineTick = 100, Payload = "I-019|31|0" });
            state.Instances.Add(new EventChainInstance { Id = 8, DefinitionId = "I-002", Stage = 1, DeadlineTick = 200, Payload = "broken" });
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { state });
            Assert.Throws<FormatException>(() => RHAH_LongFollowRuntime.PrepareGame(runtime));
            Assert.Empty(game.LongFollows);
            Assert.Equal(1, state.Instances[0].Stage);
            Assert.Equal(100, state.Instances[0].DeadlineTick);
        }

        [Fact]
        public void TemporarilyUnreadyMapWaitsWithoutReceiptsThenCompletesWhenUnloaded()
        {
            GameComponent_RHAH_Game game = SetUpGame();
            Map map = Find.Maps[0];
            map.mapPawns = null;
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                ChainId = 7, DisplayId = "I-013", BatchId = 31, MapId = 0,
                Phase = 7, DueTick = 1, FollowupSelected = false
            };
            game.LongFollows.Add(record);
            EventChainRuntime runtime = Start(record);
            for (int i = 0; i < 100; i++) runtime.Tick(1 + i * GenDate.TicksPerHour);
            EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, 7);
            Assert.Empty(instance.Receipts);
            Assert.False(record.Completed);
            Assert.Equal(247501, record.LastTick);
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            runtime.Tick(250001);
            Assert.True(record.Completed, instance.PauseReason);
            Assert.Equal(EventChainEnd.Completed, instance.End);
            Assert.Single(instance.Receipts);
        }

        [Fact]
        public void LongReturnReadinessWaitsUntilDueThenCompletesItsAction()
        {
            GameComponent_RHAH_Game game = SetUpGame();
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                ChainId = 7, DisplayId = "I-013", BatchId = 31, MapId = 0,
                Phase = 7, DueTick = 300000, FollowupSelected = false
            };
            game.LongFollows.Add(record);
            Map map = Find.Maps[0];
            for (int i = 0; i < 100; i++)
                Assert.False(RHAH_LongAdoptionFollow.Ready(record, map, 1 + i * GenDate.TicksPerHour));
            Assert.Equal(7, record.Phase);
            Assert.Equal(300000, record.DueTick);
            Assert.True(RHAH_LongAdoptionFollow.Ready(record, map, 300001));
            Assert.True(RHAH_LongAdoptionFollow.Tick(record, map, 300001));
            Assert.True(record.Completed);
        }

        [Fact]
        public void MotherAdultReadinessWaitsThenARealDeathCompletes()
        {
            GameComponent_RHAH_Game game = SetUpGame();
            Verse.Pawn child = (Verse.Pawn)FormatterServices.GetUninitializedObject(typeof(Verse.Pawn));
            child.thingIDNumber = 91;
            child.health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(child.health, PawnHealthState.Mobile);
            Field(typeof(Thing), "mapIndexOrState").SetValue(child, (sbyte)-1);
            child.ageTracker = new Pawn_AgeTracker(child);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                ChainId = 7, DisplayId = "I-002", BatchId = 31, MapId = 0,
                Phase = 18, SubjectId = child.thingIDNumber
            };
            game.LongFollows.Add(record);
            Dictionary<int, Verse.Pawn> index = (Dictionary<int, Verse.Pawn>)Field(typeof(RHAH_PawnIndex), "byId").GetValue(null);
            Dictionary<int, Verse.Pawn> previous = new Dictionary<int, Verse.Pawn>(index);
            int previousTick = (int)Field(typeof(RHAH_PawnIndex), "builtTick").GetValue(null);
            Game previousBuiltGame = (Game)Field(typeof(RHAH_PawnIndex), "builtGame").GetValue(null);
            try
            {
                Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, Current.Game);
                index.Clear();
                index.Add(child.thingIDNumber, child);
                Map map = Find.Maps[0];
                for (int i = 0; i < 100; i++)
                {
                    int tick = 1 + i * GenDate.TicksPerHour;
                    Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, tick);
                    Assert.False(RHAH_LongMotherFollow.Ready(record, map, tick));
                }
                Assert.False(record.Completed);
                Field(typeof(Pawn_HealthTracker), "healthState").SetValue(child.health, PawnHealthState.Dead);
                Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, 250001);
                Assert.True(RHAH_LongMotherFollow.Ready(record, map, 250001));
                Assert.True(RHAH_LongMotherFollow.Tick(record, map, 250001));
                Assert.True(record.Completed);
            }
            finally
            {
                index.Clear();
                foreach (KeyValuePair<int, Verse.Pawn> pair in previous) index.Add(pair.Key, pair.Value);
                Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, previousTick);
                Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, previousBuiltGame);
            }
        }

        static GameComponent_RHAH_Game SetUpGame()
        {
            Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
            Current.Game.tickManager = new TickManager();
            Current.Game.World = (World)FormatterServices.GetUninitializedObject(typeof(World));
            Current.Game.World.factionManager = new FactionManager();
            Current.Game.World.worldPawns = new WorldPawns();
            Current.Game.letterStack = new LetterStack();
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components = new List<GameComponent> { game };
            Map map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
            map.info = new MapInfo();
            Field(typeof(MapInfo), "parent").SetValue(map.info, FormatterServices.GetUninitializedObject(typeof(MapParent)));
            map.mapPawns = (MapPawns)FormatterServices.GetUninitializedObject(typeof(MapPawns));
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map> { map });
            return game;
        }

        static EventChainRuntime Start(RHAH_LongFollowRecord record)
        {
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            EventChainInstance instance = new EventChainInstance
            {
                Id = record.ChainId, DefinitionId = record.DisplayId, SiteId = record.MapId,
                Stage = 3, Payload = RHAH_EventFollowRules.Payload(record.DisplayId, record.BatchId, record.MapId), NextTick = 1
            };
            Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, new[] { instance }, record.ChainId + 1, "completed", out string error), error);
            runtime.Activate(0);
            return runtime;
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(type.FullName + "." + name);
        }


        [Fact]
        public void NewLongClockSaturatesInsteadOfReturningAnExpiredTime()
        {
            Assert.Equal(int.MaxValue, RHAH_LongFollowRuntime.AddTicks(int.MaxValue - 5, 10));
            Assert.Equal(int.MaxValue, RHAH_EventFollowRules.LongDeadline("I-002", int.MaxValue - 5));
        }

        [Fact]
        public void LivingMotherMeetingKeepsItsSavedDepartureDeadline()
        {
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = "I-037",
                Phase = 5,
                DeadlineTick = 50000
            };

            Assert.True(RHAH_LongMotherFollow.AcknowledgeLivingMeeting(record, null, 30000));
            Assert.True(record.ClaimAcknowledged);
            Assert.Equal(50000, record.DeadlineTick);
            Assert.False(RHAH_LongMotherFollow.AcknowledgeLivingMeeting(record, null, 55000));
            Assert.Equal(50000, record.DeadlineTick);
        }

        [Theory]
        [InlineData("I-002", 2, false)]
        [InlineData("I-037", 6, false)]
        [InlineData("I-037", 7, false)]
        [InlineData("I-037", 5, true)]
        public void MeetingNotificationDoesNotRestartOtherMotherPhases(string id, int phase, bool completed)
        {
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = id,
                Phase = phase,
                Completed = completed,
                ClaimAcknowledged = true,
                DueTick = 90000
            };

            Assert.False(RHAH_LongMotherFollow.AcknowledgeLivingMeeting(record, null, 30000));
            Assert.Equal(-1, record.DeadlineTick);
            Assert.Equal(90000, record.DueTick);
        }
    }
}
