using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ShamblerTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.shambler.tests");
        readonly object previousAnomaly = AccessTools.Field(typeof(ModsConfig), "anomalyActive").GetValue(null);
        readonly PawnKindDef previousRatkin = RHAH_DefOf.RHAH_PawnKind_Ratkin;
        readonly MutantDef previousShambler;
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly LoadedLanguage previousDefaultLanguage = LanguageDatabase.defaultLanguage;
        readonly Map map;
        readonly List<Map> arrivals = new List<Map>();
        readonly List<int> counts = new List<int>();
        RHAH_PawnRequest generationRequest;
        EventChainRuntime runtime;
        bool moving = true;
        bool crawling;
        static RHAH_ShamblerTests active;

        public RHAH_ShamblerTests()
        {
            active = this;
            bool binding = (bool)AccessTools.Field(typeof(DefOfHelper), "bindingNow").GetValue(null);
            try
            {
                AccessTools.Field(typeof(DefOfHelper), "bindingNow").SetValue(null, true);
                RuntimeHelpers.RunClassConstructor(typeof(MutantDefOf).TypeHandle);
            }
            finally { AccessTools.Field(typeof(DefOfHelper), "bindingNow").SetValue(null, binding); }
            previousShambler = MutantDefOf.Shambler;
            MutantDefOf.Shambler = new MutantDef { defName = "Shambler" };
            AccessTools.Field(typeof(ModsConfig), "anomalyActive").SetValue(null, true);
            RHAH_DefOf.RHAH_PawnKind_Ratkin = new PawnKindDef
            {
                defName = "RHAH_PawnKind_Ratkin",
                race = new ThingDef { defName = "Ratkin", race = new RaceProperties() }
            };
            map = Find.Maps[0];
            map.uniqueID = 0;
            map.info = new MapInfo();
            map.mapPawns = new MapPawns(map);
            LoadedLanguage language = (LoadedLanguage)RuntimeHelpers.GetUninitializedObject(typeof(LoadedLanguage));
            AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
            AccessTools.Field(typeof(LoadedLanguage), "workerInt").SetValue(language, new LanguageWorker_English());
            language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
            language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            LanguageDatabase.activeLanguage = LanguageDatabase.defaultLanguage = language;
            Current.Game.letterStack = new LetterStack();
            Patch(AccessTools.PropertyGetter(typeof(OxygenEventChainHost), nameof(OxygenEventChainHost.Runtime)), nameof(ReadRuntime));
            Patch(AccessTools.Method(typeof(RHAH_Shamblers), nameof(RHAH_Shamblers.TrySpawn)), nameof(Spawn));
            Patch(AccessTools.Method(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.CapableOf)), nameof(CanMove));
            Patch(AccessTools.PropertyGetter(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CanCrawl)), nameof(CanCrawl));
            foreach (MethodInfo method in typeof(LetterStack).GetMethods())
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == nameof(LetterStack.ReceiveLetter) && parameters.Length >= 3 &&
                    parameters[0].ParameterType == typeof(TaggedString) && parameters[1].ParameterType == typeof(TaggedString))
                    Patch(method, nameof(SuppressLetter));
            }
            Rand.PushState(61);
        }

        public void Dispose()
        {
            Rand.PopState();
            harmony.UnpatchAll(harmony.Id);
            AccessTools.Field(typeof(ModsConfig), "anomalyActive").SetValue(null, previousAnomaly);
            RHAH_DefOf.RHAH_PawnKind_Ratkin = previousRatkin;
            MutantDefOf.Shambler = previousShambler;
            LanguageDatabase.activeLanguage = previousLanguage;
            LanguageDatabase.defaultLanguage = previousDefaultLanguage;
            active = null;
            fixture.Dispose();
        }

        [Theory]
        [InlineData("I-059", false, 0, 0)]
        [InlineData("I-059-01", true, 25000, 45000)]
        [InlineData("I-060", false, 0, 0)]
        [InlineData("I-060-01", true, 150000, 210000)]
        [InlineData("I-061", false, 0, 0)]
        public void LifetimeUsesVanillaRangesOnlyForExpiringVariants(string id, bool expires, int minimum, int maximum)
        {
            HediffComp_DisappearsAndKills comp = new HediffComp_DisappearsAndKills();
            RHAH_Shamblers.ConfigureLifetime(comp, id);
            Assert.Equal(!expires, comp.disabled);
            if (expires) Assert.InRange(comp.ticksToDisappear, minimum, maximum);
            Assert.IsType(id == "I-061" ? typeof(LordJob_ShamblerAssault) : typeof(LordJob_ShamblerSwarm),
                RHAH_Shamblers.CreateLordJob(id, new IntVec3(1, 0, 1), new IntVec3(9, 0, 9)));
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(id);
            Assert.Equal(RHAH_ContentRating.General, entry.ContentRating);
            Assert.False(RHAH_ApproachRules.UsesApproach(entry.Target, id));
        }

        [Fact]
        public void TwoVariantsHaveEqualDefaultChanceAndRequireAnomaly()
        {
            RHAH_ContentPreset.Apply(RHAH_Mod.Settings, RHAH_ContentRating.General);
            foreach (RHAH_IncidentEntry entry in RHAH_IncidentCatalog.All)
                RHAH_Mod.Settings.SetIncidentEnabled(entry.DisplayId, false);
            foreach (string parent in new[] { "I-059", "I-060" })
            {
                int group = RHAH_IncidentGroups.GroupOf(parent);
                Assert.Equal(2, RHAH_IncidentGroups.Scenarios(group).Count);
                Assert.Equal(RHAH_Mod.Settings.IncidentWeight(parent), RHAH_Mod.Settings.IncidentWeight(parent + "-01"));
                RHAH_Mod.Settings.SetIncidentEnabled(parent, true);
                RHAH_Mod.Settings.SetIncidentEnabled(parent + "-01", true);
                float groupWeight = RHAH_IncidentSchedule.TotalWeight(RHAH_AttitudePool.Negative, 0, RHAH_IncidentSeason.Spring, true, false);
                Assert.Equal(parent, RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Negative, 0, RHAH_IncidentSeason.Spring, true, false, groupWeight * 0.25f));
                Assert.Equal(parent + "-01", RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Negative, 0, RHAH_IncidentSeason.Spring, true, false, groupWeight * 0.75f));
                RHAH_Mod.Settings.SetIncidentEnabled(parent, false);
                RHAH_Mod.Settings.SetIncidentEnabled(parent + "-01", false);
            }
            foreach (RHAH_IncidentEntry entry in RHAH_IncidentCatalog.All)
                RHAH_Mod.Settings.SetIncidentEnabled(entry.DisplayId, RHAH_Shamblers.IsEvent(entry.DisplayId));
            float total = RHAH_IncidentSchedule.TotalWeight(RHAH_AttitudePool.Negative, 0, RHAH_IncidentSeason.Spring, true, false);
            Assert.True(total > 0f);
            AccessTools.Field(typeof(ModsConfig), "anomalyActive").SetValue(null, false);
            Assert.False(RHAH_Shamblers.Available("I-059-01"));
            Assert.Equal(0f, RHAH_IncidentSchedule.TotalWeight(RHAH_AttitudePool.Negative, 0, RHAH_IncidentSeason.Spring, true, false));
        }

        [Fact]
        public void GroupAndHordeScaleWithPointsWhileYoungRatkinStaysSingle()
        {
            Assert.Equal(1, RHAH_Shamblers.Count("I-059", 10000f, 30));
            Assert.Equal(1, RHAH_Shamblers.Count("I-059-01", 1f, 30));
            Assert.Equal(2, RHAH_Shamblers.Count("I-060", 1f, 30));
            Assert.True(RHAH_Shamblers.Count("I-060", 1000f, 30) > RHAH_Shamblers.Count("I-060", 200f, 30));
            Assert.Equal(30, RHAH_Shamblers.Count("I-060-01", 10000f, 30));
            Assert.Equal(10, RHAH_Shamblers.Count("I-061", 1f, 30));
            Assert.Equal(200, RHAH_Shamblers.Count("I-061", 10000f, 30));
        }

        [Theory]
        [InlineData("I-059")]
        [InlineData("I-059-01")]
        [InlineData("I-060")]
        [InlineData("I-060-01")]
        [InlineData("I-061")]
        public void FreshRatkinDoesNotJoinEntityFactionBeforeMutating(string displayId)
        {
            AccessTools.Field(typeof(FactionManager), "ofEntities").SetValue(Find.FactionManager,
                new Faction { def = new FactionDef { humanlikeFaction = false } });
            map.info.Size = new IntVec3(9, 1, 9);
            Patch(AccessTools.Method(typeof(RCellFinder), nameof(RCellFinder.TryFindRandomPawnEntryCell)), nameof(FindEntry));
            Patch(AccessTools.Method(typeof(RCellFinder), nameof(RCellFinder.TryFindTravelDestFrom)), nameof(FindDestination));
            Patch(AccessTools.Method(typeof(RHAH_PawnFactory), nameof(RHAH_PawnFactory.Generate)), nameof(CaptureGeneration));
            harmony.Unpatch(AccessTools.Method(typeof(RHAH_Shamblers), nameof(RHAH_Shamblers.TrySpawn)),
                HarmonyPatchType.Prefix, harmony.Id);

            Assert.False(RHAH_Shamblers.TrySpawn(map, displayId, 1, out List<Verse.Pawn> pawns));

            Assert.Empty(pawns);
            Assert.NotNull(generationRequest);
            Assert.Null(generationRequest.Faction);
            Assert.Same(RHAH_DefOf.RHAH_PawnKind_Ratkin, generationRequest.PawnKind);
            Assert.Equal(displayId, generationRequest.SourceIncidentDisplayId);
        }

        [Theory]
        [InlineData(PawnHealthState.Mobile, true, false, true)]
        [InlineData(PawnHealthState.Mobile, false, false, false)]
        [InlineData(PawnHealthState.Down, false, true, true)]
        [InlineData(PawnHealthState.Down, true, false, false)]
        [InlineData(PawnHealthState.Dead, true, true, false)]
        public void SurvivorMustBeAliveAndAbleToWalkOrCrawl(PawnHealthState health, bool canMove, bool canCrawl, bool expected)
        {
            Verse.Pawn pawn = Survivor();
            AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, health);
            moving = canMove;
            crawling = canCrawl;
            Assert.Equal(expected, RHAH_Shamblers.HasSurvivor(map));
            pawn.def = new ThingDef { race = new RaceProperties() };
            Assert.False(RHAH_Shamblers.HasSurvivor(map));
        }

        [Fact]
        public void ReinforcementChecksOriginalMapAfterThreeDaysOnceAcrossReload()
        {
            Survivor();
            List<EventChainProviderState> states = new List<EventChainProviderState>();
            runtime = StartRuntime(states, 100);
            int id = RHAH_ShamblerReinforcements.Schedule(map, 27, 100);
            int due = 100 + 3 * GenDate.TicksPerDay;
            Assert.True(id > 0);
            Assert.Equal(due, runtime.NextDueTick);
            runtime.Tick(due - 1);
            Assert.Empty(arrivals);
            runtime = StartRuntime(states, due - 1);
            runtime.Tick(due);
            Assert.Same(map, Assert.Single(arrivals));
            Assert.Equal(27, Assert.Single(counts));
            EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, id);
            Assert.Equal(0, instance.SiteId);
            Assert.Equal(EventChainEnd.Completed, instance.End);
            Assert.Equal("check_shamblers", Assert.Single(instance.Receipts).ActionId);
            Assert.Single(Assert.Single(states).Instances);
            runtime = StartRuntime(states, due);
            runtime.Tick(due + 3 * GenDate.TicksPerDay);
            Assert.Single(arrivals);
            Assert.Equal(0, runtime.ActiveCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NoSurvivorsOrMissingOriginalMapEndsWithoutMovingToAnotherMap(bool originalMissing)
        {
            Verse.Pawn pawn = Survivor();
            if (originalMissing)
            {
                Map other = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
                other.uniqueID = 42;
                other.mapPawns = map.mapPawns;
                Find.Maps.Clear();
                Find.Maps.Add(other);
            }
            else AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Dead);
            runtime = StartRuntime(new List<EventChainProviderState>(), 100);
            int id = runtime.Start(RHAH_EventChainProvider.ProviderKey, RHAH_ShamblerReinforcements.DefinitionId, 0, -1,
                100, -1, "27", 0, RHAH_ShamblerReinforcements.DueTick(100));
            runtime.Tick(RHAH_ShamblerReinforcements.DueTick(100));
            Assert.Empty(arrivals);
            Assert.Equal(EventChainEnd.Completed, runtime.Find(RHAH_EventChainProvider.ProviderKey, id).End);
        }

        [Fact]
        public void TimerRejectsOverflowAndMalformedCount()
        {
            Assert.Equal(-1, RHAH_ShamblerReinforcements.DueTick(int.MaxValue - 1));
            RHAH_ShamblerReinforcements definition = new RHAH_ShamblerReinforcements();
            foreach (string payload in new[] { "", "0", "201", "2|3" })
                Assert.Throws<FormatException>(() => definition.Prepare(new EventChainInstance
                {
                    DefinitionId = definition.Id, SiteId = 0, Payload = payload
                }));
        }

        Verse.Pawn Survivor()
        {
            Verse.Pawn pawn = new Verse.Pawn { def = RHAH_DefOf.RHAH_PawnKind_Ratkin.race };
            pawn.health = new Pawn_HealthTracker(pawn);
            pawn.mutant = new Pawn_MutantTracker(pawn);
            AccessTools.Field(typeof(Pawn_MutantTracker), "def").SetValue(pawn.mutant, MutantDefOf.Shambler);
            AccessTools.Field(typeof(Pawn_MutantTracker), "hasTurned").SetValue(pawn.mutant, true);
            AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
            ((List<Verse.Pawn>)AccessTools.Field(typeof(MapPawns), "pawnsSpawned").GetValue(map.mapPawns)).Add(pawn);
            return pawn;
        }

        static EventChainRuntime StartRuntime(List<EventChainProviderState> states, int tick)
        {
            EventChainRuntime value = new EventChainRuntime(states);
            value.Register(new RHAH_EventChainProvider());
            value.Activate(tick);
            return value;
        }

        void Patch(MethodInfo method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(RHAH_ShamblerTests), prefix));
        static bool ReadRuntime(ref EventChainRuntime __result) { __result = active.runtime; return false; }
        static bool CanMove(ref bool __result) { __result = active.moving; return false; }
        static bool CanCrawl(ref bool __result) { __result = active.crawling; return false; }
        static bool SuppressLetter() => false;
        static bool FindEntry(ref IntVec3 result, ref bool __result)
        {
            result = new IntVec3(0, 0, 4);
            __result = true;
            return false;
        }
        static bool FindDestination(ref IntVec3 travelDest, ref bool __result)
        {
            travelDest = new IntVec3(8, 0, 4);
            __result = true;
            return false;
        }
        static bool CaptureGeneration(RHAH_PawnRequest request, ref Verse.Pawn __result)
        {
            active.generationRequest = request;
            __result = null;
            return false;
        }
        static bool Spawn(Map map, int count, ref List<Verse.Pawn> pawns, ref bool __result)
        {
            active.arrivals.Add(map);
            active.counts.Add(count);
            pawns = new List<Verse.Pawn>();
            __result = true;
            return false;
        }
    }
}
