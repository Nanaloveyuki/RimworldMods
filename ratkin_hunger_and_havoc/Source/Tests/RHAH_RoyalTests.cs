using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_RoyalTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.royal.tests");
        readonly ThingDef previousSilver = ThingDefOf.Silver;
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly LoadedLanguage previousDefaultLanguage = LanguageDatabase.defaultLanguage;
        readonly Map map;
        readonly List<int> dropped = new List<int>();
        readonly List<string> replies = new List<string>();
        readonly List<Verse.Pawn> discarded = new List<Verse.Pawn>();
        EventChainRuntime mapRuntime;
        static RHAH_RoyalTests active;

        public RHAH_RoyalTests()
        {
            active = this;
            map = (Map)AccessTools.Field(typeof(RHAH_AttackChoiceTests), "map").GetValue(fixture);
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.letterStack = new LetterStack();
            LoadedLanguage language = (LoadedLanguage)RuntimeHelpers.GetUninitializedObject(typeof(LoadedLanguage));
            AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
            AccessTools.Field(typeof(LoadedLanguage), "workerInt").SetValue(language, new LanguageWorker_English());
            language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
            language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            LanguageDatabase.activeLanguage = language;
            LanguageDatabase.defaultLanguage = language;
            Find.FactionManager.goodwillSituationManager = new GoodwillSituationManager();
            ThingDefOf.Silver = new ThingDef
            {
                defName = "Silver", category = ThingCategory.Item, thingClass = typeof(Thing), stackLimit = 500, useHitPoints = false
            };
            Patch(AccessTools.Method(typeof(DropCellFinder), nameof(DropCellFinder.TradeDropSpot)), nameof(DropCell));
            Patch(AccessTools.Method(typeof(DropPodUtility), nameof(DropPodUtility.DropThingsNear)), nameof(Drop));
            Patch(AccessTools.Method(typeof(GoodwillSituationManager), nameof(GoodwillSituationManager.GetMaxGoodwill)), nameof(MaxGoodwill));
            foreach (MethodInfo method in typeof(LetterStack).GetMethods())
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == nameof(LetterStack.ReceiveLetter) && parameters.Length >= 3 &&
                    parameters[0].ParameterType == typeof(TaggedString) && parameters[1].ParameterType == typeof(TaggedString))
                    Patch(method, nameof(Receive));
            }
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            ThingDefOf.Silver = previousSilver;
            LanguageDatabase.activeLanguage = previousLanguage;
            LanguageDatabase.defaultLanguage = previousDefaultLanguage;
            active = null;
            fixture.Dispose();
        }

        [Theory]
        [InlineData(-1, 0, 2500, false)]
        [InlineData(-1, 0, 2501, true)]
        [InlineData(0, 53999999, 5001, false)]
        [InlineData(0, 54000000, 5000, false)]
        [InlineData(0, 54000000, 5001, true)]
        public void WealthThresholdIsStrictAndCooldownIsFifteenFullYears(int last, int tick, int silver, bool available)
        {
            Assert.Equal(available, RHAH_SuspiciousRoyal.Available(last, tick, silver));
        }

        [Theory]
        [InlineData(0, 0, 5000)]
        [InlineData(4999, 4999, 5000)]
        [InlineData(5000, 5000, 65000)]
        [InlineData(5001, 5001, 65000)]
        [InlineData(59999, 59999, 65000)]
        [InlineData(int.MaxValue - 1, 5000, -1)]
        public void WealthCheckSchedulesTheNextLocalTwoAmWithoutOverflow(int tick, int local, int expected)
        {
            Assert.Equal(expected, RHAH_RoyalWatch.NextCheck(tick, local));
        }

        [Theory]
        [InlineData(0, 45000)]
        [InlineData(1, 37500)]
        [InlineData(2, 30000)]
        [InlineData(3, 20000)]
        [InlineData(4, 12500)]
        public void EveryTitleHasTheSpecifiedRewardAndOnlyExcessGoodwillBecomesSilver(int rank, int amount)
        {
            Assert.Equal(amount, RHAH_RoyalRepayment.Reward(rank, -100));
            Assert.Equal(amount, RHAH_RoyalRepayment.Reward(rank, 85));
            Assert.Equal(amount + 5000, RHAH_RoyalRepayment.Reward(rank, 90));
            Assert.Equal(amount + 15000, RHAH_RoyalRepayment.Reward(rank, 100));
        }

        [Fact]
        public void LargestRewardSplitsIntoLegalStacksWithNoLostSilver()
        {
            List<Thing> silver = RHAH_RoyalRepayment.SilverStacks(RHAH_RoyalRepayment.Reward(0, 100));
            Assert.Equal(60000, silver.Sum(stack => stack.stackCount));
            Assert.Equal(120, silver.Count);
            Assert.All(silver, stack => Assert.InRange(stack.stackCount, 1, ThingDefOf.Silver.stackLimit));
        }

        [Fact]
        public void RoyalIsAvailableInEveryPresetAndAlwaysArrivesNeutral()
        {
            foreach (RHAH_ContentRating rating in Enum.GetValues(typeof(RHAH_ContentRating)))
                Assert.True(RHAH_ContentPreset.Allows(rating, RHAH_SuspiciousRoyal.DisplayId));
            RHAH_Mod.Settings.SetIncidentAttitude(RHAH_SuspiciousRoyal.DisplayId, Api.RHAH_Attitude.Hostile);
            Assert.Equal(Api.RHAH_Attitude.Neutral, RHAH_IncidentArrival.For(RHAH_SuspiciousRoyal.DisplayId));
            Assert.False(RHAH_IncidentCatalog.GetByDisplayId(RHAH_SuspiciousRoyal.DisplayId).BroadcastEligible);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void OneYearPromiseSurvivesReloadAndCannotReplyTwice(bool genuine, bool survivingKingdom)
        {
            if (survivingKingdom) AddKingdom(95, FactionRelationKind.Ally);
            RHAH_ChoiceRecord record = Promise(genuine);
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            EventChainRuntime runtime = new EventChainRuntime(states);
            runtime.Register(new RHAH_EventChainProvider());
            runtime.Activate(100);
            int due = RHAH_RoyalRepayment.DueTick(100);
            record.RoyalRepaymentInstanceId = runtime.Start(RHAH_EventChainProvider.ProviderKey, "I-057", map.uniqueID,
                -1, 100, -1, record.Id.ToString(), 0, due);
            runtime.Tick(due - 1);
            Assert.Empty(replies);
            Assert.Empty(dropped);

            runtime = new EventChainRuntime(states);
            runtime.Register(new RHAH_EventChainProvider());
            runtime.Activate(due - 1);
            runtime.Tick(due);
            Assert.Single(replies);
            Assert.Equal(survivingKingdom ? 30000 : 0, dropped.Sum());
            Assert.True(record.RoyalReplySent);
            Assert.Equal(EventChainEnd.Completed, runtime.Find(RHAH_EventChainProvider.ProviderKey, record.RoyalRepaymentInstanceId).End);
            runtime.Tick(due + GenDate.TicksPerYear);
            Assert.Single(replies);
            Assert.Equal(survivingKingdom ? 30000 : 0, dropped.Sum());
        }

        [Theory]
        [InlineData(-90, FactionRelationKind.Hostile, 0, 2)]
        [InlineData(10, FactionRelationKind.Neutral, 25, 1)]
        [InlineData(95, FactionRelationKind.Ally, 100, 1)]
        public void RepaymentChangesBothGoodwillDirectionsAndEndsHostility(int goodwill, FactionRelationKind kind, int expected, int letterCount)
        {
            Faction kingdom = AddKingdom(goodwill, kind);
            RHAH_ChoiceRecord record = Promise(true);
            EventChainInstance instance = Instance(record);
            RHAH_RoyalRepayment definition = new RHAH_RoyalRepayment();
            definition.Prepare(instance);
            Assert.Equal(EventChainEnd.Completed, definition.Apply(instance, GenDate.TicksPerYear, "royal_reply").End);
            Assert.Equal(expected, kingdom.BaseGoodwillWith(Faction.OfPlayer));
            Assert.Equal(expected, Faction.OfPlayer.BaseGoodwillWith(kingdom));
            Assert.False(kingdom.HostileTo(Faction.OfPlayer));
            Assert.Equal(letterCount, replies.Count);
            Assert.Equal(goodwill == 95 ? 30000 : 20000, dropped.Sum());
        }

        [Fact]
        public void MissingOriginalMapDoesNotRedirectTheRewardToAnotherHome()
        {
            AddKingdom(0, FactionRelationKind.Neutral);
            RHAH_ChoiceRecord record = Promise(true);
            record.MapId = 999;
            EventChainInstance instance = Instance(record);
            RHAH_RoyalRepayment definition = new RHAH_RoyalRepayment();
            definition.Prepare(instance);
            Assert.Equal(EventChainEnd.Completed, definition.Apply(instance, GenDate.TicksPerYear, "royal_reply").End);
            Assert.Empty(dropped);
        }

        [Fact]
        public void WealthWatcherRegistrationIsIdempotentAcrossLoads()
        {
            Find.Maps.Clear();
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            EventChainRuntime runtime = new EventChainRuntime(states);
            runtime.Register(new RHAH_EventChainProvider());
            Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, new List<EventChainInstance>(), 1, "completed", out string error), error);
            RHAH_RoyalWatch.PrepareGame(runtime);
            RHAH_RoyalWatch.PrepareGame(runtime);
            Assert.Single(states[0].Instances);
            runtime.Activate(0);
            Assert.Equal(1, runtime.ActiveCountFor(RHAH_EventChainProvider.ProviderKey, RHAH_RoyalWatch.DefinitionId));
            runtime.Tick(GenDate.TicksPerHour);
            Assert.Empty(dropped);
        }

        [Fact]
        public void NewHomeAdvancesTheExistingWatchWithoutResettingTheCooldown()
        {
            mapRuntime = new EventChainRuntime(new List<EventChainProviderState>());
            mapRuntime.Register(new RHAH_EventChainProvider());
            mapRuntime.Activate(0);
            int id = mapRuntime.Start(RHAH_EventChainProvider.ProviderKey, RHAH_RoyalWatch.DefinitionId,
                -1, -1, 0, -1, string.Empty, 0, 50000);
            Patch(AccessTools.PropertyGetter(typeof(OxygenEventChainHost), nameof(OxygenEventChainHost.Runtime)), nameof(CurrentRuntime));
            Patch(AccessTools.PropertyGetter(typeof(Map), nameof(Map.IsPlayerHome)), nameof(Home));
            Patch(AccessTools.Method(typeof(GenLocalDate), nameof(GenLocalDate.DayTick), new[] { typeof(Map) }), nameof(LocalTime));
            GameComponent_RHAH_Game game = Current.Game.GetComponent<GameComponent_RHAH_Game>();
            game.LastRoyalArrivalTick = 123;
            RHAH_RoyalWatch.NoteHomeAvailable(map);
            RHAH_RoyalWatch.NoteHomeAvailable(map);
            Assert.Equal(1000, mapRuntime.Find(RHAH_EventChainProvider.ProviderKey, id).NextTick);
            Assert.Equal(1, mapRuntime.ActiveCountFor(RHAH_EventChainProvider.ProviderKey, RHAH_RoyalWatch.DefinitionId));
            Assert.Equal(123, game.LastRoyalArrivalTick);
        }

        [Fact]
        public void FraudCleanupSkipsMissingReferencesAndOnlyDiscardsRecordedWorldPawns()
        {
            Find.Maps.Clear();
            RHAH_PawnIndex.Clear();
            HashSet<Verse.Pawn> worldPawns = (HashSet<Verse.Pawn>)AccessTools.Field(typeof(WorldPawns), "pawnsAlive").GetValue(Find.WorldPawns);
            ThingDef race = new ThingDef { defName = "Ratkin", category = ThingCategory.Pawn };
            Verse.Pawn principal = new Verse.Pawn { thingIDNumber = 901, def = race };
            Verse.Pawn follower = new Verse.Pawn { thingIDNumber = 902, def = race };
            Verse.Pawn unrelated = new Verse.Pawn { thingIDNumber = 903, def = race };
            worldPawns.Add(principal);
            worldPawns.Add(follower);
            worldPawns.Add(unrelated);
            Patch(AccessTools.Method(typeof(WorldPawns), nameof(WorldPawns.RemoveAndDiscardPawnViaGC)), nameof(Discard));
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord { PawnLoadIds = new List<int> { 901, 999, 902 } };
            RHAH_RoyalRepayment.CleanupImpostors(record, 100);
            Assert.Equal(new[] { principal, follower }, discarded);
            Assert.DoesNotContain(unrelated, discarded);
            RHAH_PawnIndex.Clear();
        }

        RHAH_ChoiceRecord Promise(bool genuine)
        {
            GameComponent_RHAH_Game game = Current.Game.GetComponent<GameComponent_RHAH_Game>();
            return RHAH_ChoiceRuntime.Open(game, new RHAH_ChoiceRecord
            {
                DisplayId = "I-057", BatchId = 99, MapId = map.uniqueID, Kind = RHAH_RequestKind.Silver,
                Amount = 2500, Settled = RHAH_ChoiceAction.Deliver, RoyalGenuine = genuine, RoyalRank = 3
            });
        }

        static EventChainInstance Instance(RHAH_ChoiceRecord record)
        {
            record.RoyalRepaymentInstanceId = 10;
            return new EventChainInstance { Id = 10, DefinitionId = "I-057", SiteId = record.MapId, Payload = record.Id.ToString() };
        }

        static Faction AddKingdom(int goodwill, FactionRelationKind kind)
        {
            Faction kingdom = new Faction { def = new FactionDef { defName = "Rakinia" }, loadID = 80 };
            Find.FactionManager.AllFactionsListForReading.Add(kingdom);
            kingdom.SetRelation(new FactionRelation { other = Faction.OfPlayer, baseGoodwill = goodwill, kind = kind });
            Faction.OfPlayer.RelationWith(kingdom).baseGoodwill = goodwill;
            Settlement settlement = new Settlement { def = new WorldObjectDef { canHaveFaction = true } };
            settlement.SetFaction(kingdom);
            Find.WorldObjects.Settlements.Add(settlement);
            return kingdom;
        }

        void Patch(MethodInfo target, string name) => harmony.Patch(target,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(RHAH_RoyalTests), name)));

        static bool DropCell(ref IntVec3 __result)
        {
            __result = new IntVec3(10, 0, 10);
            return false;
        }

        static bool Drop(Map __1, IEnumerable<Thing> __2)
        {
            Assert.Same(active.map, __1);
            foreach (Thing stack in __2)
            {
                Assert.InRange(stack.stackCount, 1, 500);
                active.dropped.Add(stack.stackCount);
            }
            return false;
        }

        static bool Receive(TaggedString __1)
        {
            active.replies.Add(__1.ToString());
            return false;
        }

        static bool MaxGoodwill(ref int __result)
        {
            __result = 100;
            return false;
        }

        static bool CurrentRuntime(ref EventChainRuntime __result)
        {
            __result = active.mapRuntime;
            return false;
        }

        static bool Home(ref bool __result)
        {
            __result = true;
            return false;
        }

        static bool LocalTime(ref int __result)
        {
            __result = 4000;
            return false;
        }

        static bool Discard(Verse.Pawn __0)
        {
            active.discarded.Add(__0);
            return false;
        }
    }
}
