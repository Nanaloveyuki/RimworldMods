using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ChoiceLetterLifecycleTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousProgramState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly LoadedLanguage previousDefaultLanguage = LanguageDatabase.defaultLanguage;
        readonly int previousMainThreadId;
        readonly object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
        readonly LetterDef previousVisitors;
        readonly LetterDef previousVisitorsGreen;
        readonly GameComponent_RHAH_Game game;

        public RHAH_ChoiceLetterLifecycleTests()
        {
            bool binding = (bool)Field(typeof(DefOfHelper), "bindingNow").GetValue(null);
            bool initializing = UnityDataInitializer.initializing;
            try
            {
                Field(typeof(DefOfHelper), "bindingNow").SetValue(null, true);
                RuntimeHelpers.RunClassConstructor(typeof(RHAH_DefOf).TypeHandle);
                UnityDataInitializer.initializing = true;
                RuntimeHelpers.RunClassConstructor(typeof(UnityData).TypeHandle);
            }
            finally
            {
                Field(typeof(DefOfHelper), "bindingNow").SetValue(null, binding);
                UnityDataInitializer.initializing = initializing;
            }
            previousMainThreadId = (int)Field(typeof(UnityData), "mainThreadId").GetValue(null);
            Field(typeof(UnityData), "mainThreadId").SetValue(null, Thread.CurrentThread.ManagedThreadId);
            previousVisitors = RHAH_DefOf.RHAH_ChoiceVisitors;
            previousVisitorsGreen = RHAH_DefOf.RHAH_ChoiceVisitorsGreen;
            RHAH_DefOf.RHAH_ChoiceVisitors = VisitorDef("RHAH_ChoiceVisitors");
            RHAH_DefOf.RHAH_ChoiceVisitorsGreen = VisitorDef("RHAH_ChoiceVisitorsGreen");
            Field(typeof(Prefs), "data").SetValue(null, new PrefsData { devMode = false });
            RHAH_Mod.Settings = new RHAH_Settings();
            LanguageDatabase.activeLanguage = Uninitialized<LoadedLanguage>();
            Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(LanguageDatabase.activeLanguage, true);
            LanguageDatabase.activeLanguage.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            Field(typeof(LoadedLanguage), "workerInt").SetValue(LanguageDatabase.activeLanguage, new LanguageWorker_English());
            LanguageDatabase.defaultLanguage = LanguageDatabase.activeLanguage;
            Current.ProgramState = ProgramState.Entry;
            Current.Game = Uninitialized<Game>();
            Current.Game.tickManager = new TickManager();
            Field(typeof(TickManager), "curTimeSpeed").SetValue(Current.Game.tickManager, TimeSpeed.Paused);
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.letterStack = new LetterStack();
            Current.Game.history = Uninitialized<History>();
            Current.Game.history.archive = new Archive();
            World world = Uninitialized<World>();
            world.factionManager = new FactionManager();
            world.worldObjects = new WorldObjectsHolder();
            Current.Game.World = world;
            Map map = Uninitialized<Map>();
            map.uniqueID = 41;
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map> { map });
            game = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components = new List<GameComponent> { game };
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            RHAH_Mod.Settings = previousSettings;
            Current.ProgramState = previousProgramState;
            LanguageDatabase.activeLanguage = previousLanguage;
            LanguageDatabase.defaultLanguage = previousDefaultLanguage;
            Field(typeof(UnityData), "mainThreadId").SetValue(null, previousMainThreadId);
            Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
            RHAH_DefOf.RHAH_ChoiceVisitors = previousVisitors;
            RHAH_DefOf.RHAH_ChoiceVisitorsGreen = previousVisitorsGreen;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LargeRefugeeWaveSendsActualVisitorChoiceInEitherColor(bool green)
        {
            RHAH_Mod.Settings.greenIncidentLetters = green;
            Verse.Pawn pawn = Uninitialized<Verse.Pawn>();
            pawn.thingIDNumber = 91;
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)-1);
            List<Verse.Pawn> arrived = new List<Verse.Pawn> { pawn };
            RHAH_IncidentContext context = Context("I-001");

            bool sent = (bool)typeof(RHAH_IncidentFacts).GetMethod("OpenChoice", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { context, new List<RHAH_PawnCreationResult> { RHAH_PawnCreationResult.Success(arrived) }, arrived });

            Assert.True(sent);
            ChoiceLetter_RHAH_Visitors letter = Assert.IsType<ChoiceLetter_RHAH_Visitors>(Assert.Single(Find.LetterStack.LettersListForReading));
            RHAH_ChoiceRecord record = Assert.Single(game.OpenChoices);
            Assert.Equal(RHAH_ChoiceKind.Refugees, record.Choice);
            Assert.Equal(record.Id, letter.choiceId);
            Assert.Equal(new[] { pawn.thingIDNumber }, record.PawnLoadIds);
            Assert.Same(green ? RHAH_DefOf.RHAH_ChoiceVisitorsGreen : RHAH_DefOf.RHAH_ChoiceVisitors, letter.def);
            Assert.False(letter.CanDismissWithRightClick);
            Assert.True(RHAH_RequestRules.ShowsRecruit(context.DisplayId, true));
            Assert.True(RHAH_RequestRules.ShowsJoin(context.DisplayId, true));
            Assert.True(RHAH_RequestRules.ShowsHire(context.DisplayId, true));
        }

        [Theory]
        [InlineData("I-008", false)]
        [InlineData("I-008", true)]
        [InlineData("I-009", false)]
        [InlineData("I-009", true)]
        [InlineData("I-010", false)]
        [InlineData("I-010", true)]
        public void WildVisitorLetterOffersActionsUntilItsDeadlineInEitherColor(string id, bool green)
        {
            RHAH_Mod.Settings.greenIncidentLetters = green;
            bool sent = (bool)typeof(RHAH_IncidentFacts).GetMethod("OpenChoice", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Context(id), new List<RHAH_PawnCreationResult>(), new List<Verse.Pawn>() });

            Assert.True(sent);
            ChoiceLetter_RHAH_Visitors letter = Assert.IsType<ChoiceLetter_RHAH_Visitors>(Assert.Single(Find.LetterStack.LettersListForReading));
            RHAH_ChoiceRecord record = Assert.Single(game.OpenChoices);
            Assert.Equal(RHAH_ChoiceKind.Visitors, record.Choice);
            Assert.Equal(record.Id, letter.choiceId);
            Assert.Same(green ? RHAH_DefOf.RHAH_ChoiceVisitorsGreen : RHAH_DefOf.RHAH_ChoiceVisitors, letter.def);

            List<DiaOption> options = new List<DiaOption>(letter.Choices);
            foreach (string key in new[] { "RHAH_Choice_Feed", "RHAH_Choice_Reject", "RHAH_Choice_Ignore" })
            {
                DiaOption option = Assert.Single(options.FindAll(item =>
                    (string)Field(typeof(DiaOption), "text").GetValue(item) == key.Translate().ToString()));
                Assert.False(option.disabled);
                Assert.NotNull(option.action);
            }
            DiaOption join = Assert.Single(options.FindAll(item =>
                (string)Field(typeof(DiaOption), "text").GetValue(item) == "RHAH_Choice_Join".Translate().ToString()));
            Assert.True(join.disabled);
            Assert.NotNull(join.disabledReason);

            Find.TickManager.DebugSetTicksGame(record.ExpireTick);
            Assert.Single(letter.Choices);
            Assert.True(letter.CanDismissWithRightClick);
        }

        [Theory]
        [InlineData("I-001", RHAH_ChoiceKind.Refugees)]
        [InlineData("I-011", RHAH_ChoiceKind.Refugees)]
        [InlineData("I-040", RHAH_ChoiceKind.Refugees)]
        [InlineData("I-008", RHAH_ChoiceKind.Visitors)]
        [InlineData("I-009", RHAH_ChoiceKind.Visitors)]
        [InlineData("I-010", RHAH_ChoiceKind.Visitors)]
        [InlineData("I-015", RHAH_ChoiceKind.Aid)]
        [InlineData("I-020", RHAH_ChoiceKind.Intel)]
        public void DisabledDecisionSettingLeavesArrivalRouteWithoutOpeningARequest(string id, RHAH_ChoiceKind kind)
        {
            RHAH_Mod.Settings.visitorChoicesEnabled = false;
            RHAH_Mod.Settings.aidRequestsEnabled = kind != RHAH_ChoiceKind.Aid;
            RHAH_Mod.Settings.intelTradesEnabled = kind != RHAH_ChoiceKind.Intel;

            bool sent = (bool)typeof(RHAH_IncidentFacts).GetMethod("OpenChoice", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Context(id), new List<RHAH_PawnCreationResult>(), new List<Verse.Pawn>() });

            Assert.False(sent);
            Assert.Empty(game.OpenChoices);
            Assert.Empty(Find.LetterStack.LettersListForReading);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void HourlyCleanupRemovesHandledExpiredAndOrphanChoicesWithoutTouchingOtherLetters(bool visitors)
        {
            RHAH_ChoiceRecord handled = AddChoice(1, -1, RHAH_ChoiceAction.Deliver);
            RHAH_ChoiceRecord expired = AddChoice(2, GenDate.TicksPerHour);
            RHAH_ChoiceRecord active = AddChoice(3, GenDate.TicksPerDay);
            ChoiceLetter handledLetter = Letter(visitors, handled.Id);
            ChoiceLetter expiredLetter = Letter(visitors, expired.Id);
            ChoiceLetter orphan = Letter(visitors, 99);
            ChoiceLetter activeLetter = Letter(visitors, active.Id);
            StandardLetter vanilla = new StandardLetter { Label = "Same title", Text = "Same text" };
            ForeignChoiceLetter foreign = new ForeignChoiceLetter();
            ChoiceLetter_RHAH_BanditAttack notification = new ChoiceLetter_RHAH_BanditAttack();
            Find.LetterStack.LettersListForReading.AddRange(new Letter[] { handledLetter, expiredLetter, orphan, activeLetter, vanilla, foreign, notification });
            Assert.True(handledLetter.CanDismissWithRightClick);
            Assert.True(orphan.CanDismissWithRightClick);
            Assert.False(activeLetter.CanDismissWithRightClick);

            RHAH_ChoiceRuntime.Tick(game, GenDate.TicksPerHour - 1);
            Assert.Contains(handledLetter, Find.LetterStack.LettersListForReading);
            Assert.Contains(orphan, Find.LetterStack.LettersListForReading);
            Find.TickManager.DebugSetTicksGame(GenDate.TicksPerHour);
            Assert.True(expiredLetter.CanDismissWithRightClick);
            RHAH_ChoiceRuntime.Tick(game, GenDate.TicksPerHour);

            Assert.DoesNotContain(handledLetter, Find.LetterStack.LettersListForReading);
            Assert.DoesNotContain(expiredLetter, Find.LetterStack.LettersListForReading);
            Assert.DoesNotContain(orphan, Find.LetterStack.LettersListForReading);
            Assert.Contains(activeLetter, Find.LetterStack.LettersListForReading);
            Assert.Contains(vanilla, Find.LetterStack.LettersListForReading);
            Assert.Contains(foreign, Find.LetterStack.LettersListForReading);
            Assert.Contains(notification, Find.LetterStack.LettersListForReading);
            Assert.True(notification.CanDismissWithRightClick);
            Assert.Equal(RHAH_ChoiceAction.Deliver, handled.Settled);
            Assert.Equal(RHAH_ChoiceAction.Timeout, expired.Settled);
            Assert.Equal(RHAH_ChoiceAction.None, active.Settled);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MissingMapChoiceCanBeDismissedAndCleanedWithoutSubmittingAnOutcome(bool visitors)
        {
            RHAH_ChoiceRecord record = AddChoice(1, -1);
            record.MapId = 42;
            ChoiceLetter letter = Letter(visitors, record.Id);
            Find.LetterStack.LettersListForReading.Add(letter);

            Assert.True(letter.CanDismissWithRightClick);
            RHAH_ChoiceRuntime.Tick(game, GenDate.TicksPerHour);

            Assert.DoesNotContain(letter, Find.LetterStack.LettersListForReading);
            Assert.Equal(RHAH_ChoiceAction.None, record.Settled);
        }

        [Theory]
        [InlineData("completed")]
        [InlineData("phase")]
        [InlineData("closed")]
        [InlineData("orphan")]
        [InlineData("map")]
        public void HourlyCleanupRemovesStaleLongFollowLettersButPreservesPendingPhases(string stale)
        {
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { ChainId = 7, MapId = 41, Phase = 2, LetterOpen = true };
            if (stale == "completed") record.Completed = true;
            if (stale == "phase") record.Phase = 3;
            if (stale == "closed") record.LetterOpen = false;
            if (stale == "map") record.MapId = 42;
            if (stale != "orphan") game.LongFollows.Add(record);
            RHAH_LongFollowRecord active = new RHAH_LongFollowRecord { ChainId = 8, MapId = 41, Phase = 4, LetterOpen = true, DeadlineTick = 1 };
            game.LongFollows.Add(active);
            ChoiceLetter_RHAH_LongFollow staleLetter = new ChoiceLetter_RHAH_LongFollow { chainId = 7, phase = 2 };
            ChoiceLetter_RHAH_LongFollow activeLetter = new ChoiceLetter_RHAH_LongFollow { chainId = 8, phase = 4 };
            Find.LetterStack.LettersListForReading.AddRange(new Letter[] { staleLetter, activeLetter });
            Assert.True(staleLetter.CanDismissWithRightClick);
            Assert.False(activeLetter.CanDismissWithRightClick);

            RHAH_ChoiceRuntime.Tick(game, GenDate.TicksPerHour);

            Assert.DoesNotContain(staleLetter, Find.LetterStack.LettersListForReading);
            Assert.Contains(activeLetter, Find.LetterStack.LettersListForReading);
            Assert.False(active.Completed);
            Assert.True(active.LetterOpen);
            Assert.Equal(4, active.Phase);
            Assert.Equal(1, active.DeadlineTick);
        }

        [Theory]
        [InlineData(EventChainEnd.Completed)]
        [InlineData(EventChainEnd.Expired)]
        [InlineData(EventChainEnd.Cancelled)]
        [InlineData(EventChainEnd.Failed)]
        public void AuthoritativeTerminalChainRemovesLeftoverFollowWithoutChangingItsRecord(EventChainEnd end)
        {
            Current.ProgramState = ProgramState.Playing;
            RHAH_LongFollowRecord record = new RHAH_LongFollowRecord { ChainId = 7, MapId = 41, Phase = 2, LetterOpen = true };
            game.LongFollows.Add(record);
            EventChainProviderState state = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 8,
                Instances = new List<EventChainInstance> { new EventChainInstance { Id = 7, DefinitionId = "I-002", End = end } }
            };
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { state });
            runtime.Activate(0);
            GameComponent_Oxygendsl host = new GameComponent_Oxygendsl(Current.Game);
            Field(typeof(GameComponent_Oxygendsl), "eventChainRuntime").SetValue(host, runtime);
            Field(typeof(GameComponent_Oxygendsl), "eventChainReady").SetValue(host, true);
            Current.Game.components.Add(host);
            ChoiceLetter_RHAH_LongFollow letter = new ChoiceLetter_RHAH_LongFollow { chainId = 7, phase = 2 };
            Find.LetterStack.LettersListForReading.Add(letter);

            Assert.True(letter.CanDismissWithRightClick);
            RHAH_ChoiceRuntime.Tick(game, GenDate.TicksPerHour);

            Assert.DoesNotContain(letter, Find.LetterStack.LettersListForReading);
            Assert.False(record.Completed);
            Assert.True(record.LetterOpen);
            Assert.Equal(end, runtime.Find(RHAH_EventChainProvider.ProviderKey, 7).End);
        }

        static RHAH_IncidentContext Context(string id)
        {
            return new RHAH_IncidentContext { DisplayId = id, Map = Find.Maps[0], SpawnBatchId = 7 };
        }

        RHAH_ChoiceRecord AddChoice(int id, int expires, RHAH_ChoiceAction settled = RHAH_ChoiceAction.None)
        {
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord { Id = id, DisplayId = "I-001", Choice = RHAH_ChoiceKind.Refugees,
                MapId = 41, ExpireTick = expires, Settled = settled };
            ((List<RHAH_ChoiceRecord>)game.OpenChoices).Add(record);
            return record;
        }

        static ChoiceLetter Letter(bool visitors, int id)
        {
            return visitors ? (ChoiceLetter)new ChoiceLetter_RHAH_Visitors { choiceId = id }
                : new ChoiceLetter_RHAH_Request { choiceId = id };
        }

        static LetterDef VisitorDef(string name)
        {
            LetterDef def = Uninitialized<LetterDef>();
            def.defName = name;
            def.letterClass = typeof(ChoiceLetter_RHAH_Visitors);
            return def;
        }

        static T Uninitialized<T>()
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(type.FullName + "." + name);
        }

        sealed class ForeignChoiceLetter : ChoiceLetter
        {
            public override IEnumerable<DiaOption> Choices
            {
                get { yield return Option_Close; }
            }
        }
    }
}
