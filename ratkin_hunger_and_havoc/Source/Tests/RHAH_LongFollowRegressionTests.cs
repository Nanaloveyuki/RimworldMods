using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_LongFollowRegressionTests : IDisposable
    {
        const int Tick = 741;
        readonly Game previousGame;
        readonly Dictionary<int, Verse.Pawn> indexed;
        readonly Dictionary<int, Verse.Pawn> previousIndex;
        readonly int previousBuiltTick;
        readonly Game previousBuiltGame;
        readonly Map map;

        public RHAH_LongFollowRegressionTests()
        {
            RimWorldAssemblies.EnsureResolved();
            previousGame = Current.Game;
            Current.Game = Uninitialized<Game>();
            Current.Game.tickManager = new TickManager();
            Current.Game.letterStack = new LetterStack();
            map = Uninitialized<Map>();
            indexed = (Dictionary<int, Verse.Pawn>)Field(typeof(RHAH_PawnIndex), "byId").GetValue(null);
            previousIndex = new Dictionary<int, Verse.Pawn>(indexed);
            previousBuiltTick = (int)Field(typeof(RHAH_PawnIndex), "builtTick").GetValue(null);
            previousBuiltGame = (Game)Field(typeof(RHAH_PawnIndex), "builtGame").GetValue(null);
            Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, Current.Game);
            Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, Tick);
            indexed.Clear();
        }

        public void Dispose()
        {
            indexed.Clear();
            foreach (KeyValuePair<int, Verse.Pawn> entry in previousIndex) indexed.Add(entry.Key, entry.Value);
            Field(typeof(RHAH_PawnIndex), "builtTick").SetValue(null, previousBuiltTick);
            Field(typeof(RHAH_PawnIndex), "builtGame").SetValue(null, previousBuiltGame);
            Current.Game = previousGame;
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void MotherMedicalHoldWaitsForAcuteInjuriesButNotPermanentScars(bool permanent, bool ready)
        {
            Verse.Pawn mother = Pawn(91, PawnHealthState.Mobile);
            mother.health.hediffSet = new HediffSet(mother);
            Hediff_Injury injury = new Hediff_Injury
            {
                pawn = mother,
                def = new HediffDef { isBad = true, tendable = true },
                comps = new List<HediffComp>()
            };
            injury.comps.Add(new HediffComp_GetsPermanent { parent = injury, isPermanentInt = permanent });
            mother.health.hediffSet.hediffs.Add(injury);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = "I-037", SourceAdultId = mother.thingIDNumber, Phase = 5, MedicalHold = true
            };

            Assert.Equal(!ready, RHAH_LongFollowRuntime.Sick(mother));
            Assert.Equal(ready, RHAH_LongMotherFollow.Ready(record, map, Tick));
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(mother.health, PawnHealthState.Down);
            Assert.False(RHAH_LongMotherFollow.Ready(record, map, Tick));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(7)]
        public void MotherTimeoutDoesNotRequireStandingAtTheMeetingSpot(int phase)
        {
            Verse.Pawn mother = Pawn(91, PawnHealthState.Mobile);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = "I-002", SourceAdultId = mother.thingIDNumber,
                Phase = phase, DeadlineTick = Tick - 1, DueTick = Tick - 1
            };

            Assert.False(RHAH_LongFollowRuntime.AtOriginalEdge(mother, map));
            Assert.True(RHAH_LongMotherFollow.Ready(record, map, Tick));
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(mother.health, PawnHealthState.Down);
            record.LetterOpen = true;
            Assert.False(RHAH_LongMotherFollow.Ready(record, map, Tick));
        }

        [Theory]
        [InlineData("I-032", false)]
        [InlineData("I-032", true)]
        [InlineData("I-046", false)]
        [InlineData("I-046", true)]
        public void AskingAnUnrememberingChildEndsWhenTheChildDiesOrIsLost(string displayId, bool missing)
        {
            if (!missing) Pawn(91, PawnHealthState.Dead);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = displayId, SubjectId = 91, Phase = 4, Opened = true
            };

            Assert.True(RHAH_LongAdoptionFollow.Ready(record, map, Tick));
            Assert.True(RHAH_LongAdoptionFollow.Tick(record, map, Tick));
            Assert.True(record.Completed);
            Assert.Empty(Current.Game.letterStack.LettersListForReading);
        }

        [Fact]
        public void AskingAnUnrememberingLivingChildStillWaitsForAdulthood()
        {
            Pawn(91, PawnHealthState.Mobile);
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { DisplayId = "I-032", SubjectId = 91, Phase = 4 };

            Assert.False(RHAH_LongAdoptionFollow.Ready(record, map, Tick));
            Assert.False(RHAH_LongAdoptionFollow.Tick(record, map, Tick));
            Assert.False(record.Completed);
        }

        [Theory]
        [InlineData(0, 0, false, 0, true)]
        [InlineData(2, 0, false, 0, true)]
        [InlineData(0, 7, false, 0, false)]
        [InlineData(0, 0, true, 0, false)]
        [InlineData(0, 0, false, 1, false)]
        public void PreparingShortStagesRemovesOnlyUntouchedUnregisteredLongRecords(int stage, int chainId, bool opened, int phase, bool removed)
        {
            GameComponent_RHAH_Game game = SetUpComponent();
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord
            {
                DisplayId = "I-019", BatchId = 31, MapId = 0,
                ChainId = chainId, Opened = opened, Phase = phase
            };
            game.LongFollows.Add(record);
            EventChainProviderState state = ProviderState();
            state.Instances.Add(new EventChainInstance
            {
                Id = 7, DefinitionId = "I-019", SiteId = 0, Stage = stage, Payload = "I-019|31|0"
            });

            RHAH_LongFollowRuntime.PrepareGame(new EventChainRuntime(new List<EventChainProviderState> { state }));

            Assert.Equal(removed ? 0 : 1, game.LongFollows.Count);
            if (!removed) Assert.Same(record, game.LongFollows[0]);
            Assert.Equal(stage, state.Instances[0].Stage);
        }

        [Fact]
        public void PreparingRegistersMatchingLongStageBeforeCleaningUpShortRecords()
        {
            GameComponent_RHAH_Game game = SetUpComponent();
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { DisplayId = "I-019", BatchId = 31, MapId = 0 };
            game.LongFollows.Add(record);
            EventChainProviderState state = ProviderState();
            state.Instances.Add(new EventChainInstance
            {
                Id = 7, DefinitionId = "I-019", SiteId = 0, Stage = 0, Payload = "I-019|31|0"
            });
            state.Instances.Add(new EventChainInstance
            {
                Id = 8, DefinitionId = "I-019", SiteId = 0, Stage = 3, Payload = "I-019|31|0", DeadlineTick = 100
            });

            RHAH_LongFollowRuntime.PrepareGame(new EventChainRuntime(new List<EventChainProviderState> { state }));

            Assert.Same(record, Assert.Single(game.LongFollows));
            Assert.Equal(8, record.ChainId);
            Assert.Equal(100, record.DueTick);
            Assert.Equal(3, state.Instances[1].Stage);
        }

        [Fact]
        public void PreparingKeepsSimultaneousBatchesOnDifferentMapsSeparate()
        {
            GameComponent_RHAH_Game game = SetUpComponent();
            RHAH_LongFollowRecord first = new RHAH_LongFollowRecord { DisplayId = "I-019", BatchId = 31, MapId = 0 };
            RHAH_LongFollowRecord second = new RHAH_LongFollowRecord { DisplayId = "I-019", BatchId = 31, MapId = 1 };
            game.LongFollows.Add(first);
            game.LongFollows.Add(second);
            EventChainProviderState state = ProviderState();
            state.Instances.Add(new EventChainInstance
            {
                Id = 7, DefinitionId = "I-019", SiteId = 0, Stage = 3, Payload = "I-019|31|0", DeadlineTick = 100
            });
            state.Instances.Add(new EventChainInstance
            {
                Id = 8, DefinitionId = "I-019", SiteId = 1, Stage = 3, Payload = "I-019|31|1", DeadlineTick = 200
            });

            RHAH_LongFollowRuntime.PrepareGame(new EventChainRuntime(new List<EventChainProviderState> { state }));

            Assert.Equal(2, game.LongFollows.Count);
            Assert.Equal(7, first.ChainId);
            Assert.Equal(100, first.DueTick);
            Assert.Equal(8, second.ChainId);
            Assert.Equal(200, second.DueTick);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("unknown")]
        [InlineData("I-019|99|0")]
        [InlineData("I-019|31|1")]
        public void PreparingPreservesRecordsWithoutAKnownMatchingShortPayload(string payload)
        {
            GameComponent_RHAH_Game game = SetUpComponent();
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { DisplayId = "I-019", BatchId = 31, MapId = 0 };
            game.LongFollows.Add(record);
            EventChainProviderState state = ProviderState();
            if (payload != null)
                state.Instances.Add(new EventChainInstance
                {
                    Id = 7, DefinitionId = "I-019", SiteId = 0, Stage = 0, Payload = payload
                });

            RHAH_LongFollowRuntime.PrepareGame(new EventChainRuntime(new List<EventChainProviderState> { state }));

            Assert.Same(record, Assert.Single(game.LongFollows));
            Assert.Equal(0, record.ChainId);
        }

        [Fact]
        public void FailedProviderPreparationPreservesShortRecordsAndLongRegistration()
        {
            GameComponent_RHAH_Game game = SetUpComponent();
            RHAH_LongFollowRecord shortRecord = new RHAH_LongFollowRecord { DisplayId = "I-019", BatchId = 31, MapId = 0 };
            RHAH_LongFollowRecord longRecord = new RHAH_LongFollowRecord { DisplayId = "I-019", BatchId = 32, MapId = 0 };
            game.LongFollows.Add(shortRecord);
            game.LongFollows.Add(longRecord);
            EventChainProviderState state = ProviderState();
            state.MigrationStatus = "failed: retained originals";
            state.Instances.Add(new EventChainInstance
            {
                Id = 7, DefinitionId = "I-019", SiteId = 0, Stage = 0, Payload = "I-019|31|0"
            });
            state.Instances.Add(new EventChainInstance
            {
                Id = 8, DefinitionId = "I-019", SiteId = 0, Stage = 3, Payload = "I-019|32|0", DeadlineTick = 100
            });

            RHAH_LongFollowRuntime.PrepareGame(new EventChainRuntime(new List<EventChainProviderState> { state }));

            Assert.Equal(new[] { shortRecord, longRecord }, game.LongFollows);
            Assert.Equal(0, longRecord.ChainId);
            Assert.Equal(-1, longRecord.DueTick);
            Assert.Equal("failed: retained originals", state.MigrationStatus);
        }

        static GameComponent_RHAH_Game SetUpComponent()
        {
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components = new List<GameComponent> { game };
            return game;
        }

        static EventChainProviderState ProviderState()
        {
            return new EventChainProviderState { ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 9, MigrationStatus = "completed" };
        }

        Verse.Pawn Pawn(int id, PawnHealthState state)
        {
            Verse.Pawn pawn = Uninitialized<Verse.Pawn>();
            pawn.thingIDNumber = id;
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, state);
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)-1);
            indexed.Add(id, pawn);
            return pawn;
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
