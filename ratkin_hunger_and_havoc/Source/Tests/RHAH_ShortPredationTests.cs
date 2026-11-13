using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ShortPredationTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly int previousMainThreadId;
        readonly JobDef previousPredatorHunt;
        readonly JobDef previousIngest;
        readonly Map map;
        readonly MapComponent_RHAH_Map component;
        readonly ActiveTransporterInfo held;
        readonly EventChainRuntime runtime;
        int nextThingId = 100;

        public RHAH_ShortPredationTests()
        {
            bool initializing = UnityDataInitializer.initializing;
            try
            {
                UnityDataInitializer.initializing = true;
                RuntimeHelpers.RunClassConstructor(typeof(UnityData).TypeHandle);
            }
            finally
            {
                UnityDataInitializer.initializing = initializing;
            }
            previousMainThreadId = (int)Field(typeof(UnityData), "mainThreadId").GetValue(null);
            Field(typeof(UnityData), "mainThreadId").SetValue(null, Thread.CurrentThread.ManagedThreadId);
            previousPredatorHunt = JobDefOf.PredatorHunt;
            previousIngest = JobDefOf.Ingest;
            JobDefOf.PredatorHunt = new JobDef { defName = "PredatorHunt" };
            JobDefOf.Ingest = new JobDef { defName = "Ingest" };
            RHAH_Mod.Settings = new RHAH_Settings { followPredatorPercent = 100 };
            Current.Game = Uninitialized<Game>();
            Current.Game.tickManager = new TickManager();
            Current.Game.components = new List<GameComponent>();
            Current.Game.World = Uninitialized<World>();
            Current.Game.World.factionManager = new FactionManager();
            Field(typeof(FactionManager), "ofPlayer").SetValue(Find.FactionManager,
                new Faction { def = new FactionDef { isPlayer = true } });
            map = Uninitialized<Map>();
            map.uniqueID = 41;
            map.info = new MapInfo();
            map.components = new List<MapComponent>();
            map.mapPawns = new MapPawns(map);
            map.listerThings = new ListerThings(ListerThingsUse.Global);
            map.passingShipManager = new PassingShipManager(map);
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map> { map });
            component = new MapComponent_RHAH_Map(map);
            held = new ActiveTransporterInfo();
            ActiveTransporter pod = new ActiveTransporter { Contents = held, thingIDNumber = nextThingId++ };
            pod.def = Uninitialized<ThingDef>();
            pod.def.comps = new List<CompProperties>();
            pod.def.category = ThingCategory.Ethereal;
            pod.def.thingClass = typeof(ActiveTransporter);
            DropPodIncoming incoming = new DropPodIncoming { thingIDNumber = nextThingId++ };
            incoming.def = Uninitialized<ThingDef>();
            incoming.def.comps = new List<CompProperties>();
            incoming.def.category = ThingCategory.Ethereal;
            incoming.def.thingClass = typeof(DropPodIncoming);
            ((ThingOwner<Thing>)incoming.innerContainer).InnerListForReading.Add(pod);
            pod.holdingOwner = incoming.innerContainer;
            Field(typeof(Thing), "mapIndexOrState").SetValue(incoming, (sbyte)0);
            incoming.holdingOwner = new ThingOwner<Thing>(map);
            map.listerThings.Add(incoming);
            map.components.Add(component);
            runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            runtime.Activate(0);
            GameComponent_Oxygendsl oxygen = Uninitialized<GameComponent_Oxygendsl>();
            Field(typeof(GameComponent_Oxygendsl), "eventChainRuntime").SetValue(oxygen, runtime);
            Field(typeof(GameComponent_Oxygendsl), "eventChainReady").SetValue(oxygen, true);
            Current.Game.components.Add(oxygen);
            Rand.PushState(7231);
        }

        public void Dispose()
        {
            Rand.PopState();
            Current.Game = previousGame;
            RHAH_Mod.Settings = previousSettings;
            Field(typeof(UnityData), "mainThreadId").SetValue(null, previousMainThreadId);
            JobDefOf.PredatorHunt = previousPredatorHunt;
            JobDefOf.Ingest = previousIngest;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SelectedShortChainRemembersTheWholeBatchBeforeOrAfterLeavingItsContainer(bool inContainer)
        {
            Verse.Pawn first = Arrival(7, inContainer);
            Verse.Pawn second = Arrival(7, inContainer);
            Arrival(8, inContainer);
            Verse.Pawn predator = Arrival(0, false);
            predator.def.race.predator = true;
            predator.def.race.intelligence = Intelligence.Animal;
            predator.mindState = Uninitialized<Pawn_MindState>();
            predator.mindState.mentalStateHandler = new MentalStateHandler(predator);
            int id = runtime.Start(RHAH_EventChainProvider.ProviderKey, "I-032", map.uniqueID, 0, 0, -1,
                RHAH_EventFollowRules.Payload("I-032", 7, map.uniqueID), 0, 1);

            Find.TickManager.DebugSetTicksGame(1);
            runtime.Tick(1);

            Assert.Equal(new[] { first, second }, component.PredationPrey);
            Assert.True(component.PredationSelected);
            EventChainInstance chain = runtime.Find(RHAH_EventChainProvider.ProviderKey, id);
            Assert.Equal(2, chain.Stage);
            Assert.Equal("applied", Assert.Single(chain.Receipts).Status);
            int arrivalId = component.PredationArrivalInstanceId;
            EventChainInstance arrival = runtime.Find(RHAH_EventChainProvider.ProviderKey, arrivalId);
            Assert.NotNull(arrival);
            Assert.Equal(map.uniqueID, arrival.SiteId);
            Assert.Equal(121, arrival.NextTick);

            runtime.Tick(2);
            runtime.Tick(120);

            Assert.Single(chain.Receipts);
            Assert.Equal(arrivalId, component.PredationArrivalInstanceId);
            Assert.Equal(121, arrival.NextTick);

            Find.TickManager.DebugSetTicksGame(121);
            Assert.True(predator.RaceProps.Animal, "fixture predator is not animal");
            Assert.Same(map, first.MapHeld);
            Assert.False(predator.InMentalState);
            runtime.Tick(121);

            Assert.True(arrival.End == EventChainEnd.Completed, runtime.Describe() + " " + arrival.PauseReason);
            Assert.Equal("arrival_consumed", Assert.Single(arrival.Receipts).Data);
            Assert.Same(predator, Assert.Single(component.Predators).pawn);
            Assert.Equal(0, component.PredationArrivalInstanceId);
            runtime.Tick(122);
            Assert.Single(chain.Receipts);
            Assert.Single(arrival.Receipts);
            Assert.Single(component.Predators);
        }

        [Fact]
        public void LaterSelectedBatchMergesPreyWithoutPostponingTheFirstArrival()
        {
            Verse.Pawn first = Arrival(7, false);
            Verse.Pawn second = Arrival(8, true);
            Find.TickManager.DebugSetTicksGame(1);
            RHAH_EventFollowRuntime.TryPredator(map.uniqueID, 7);
            int arrivalId = component.PredationArrivalInstanceId;
            EventChainInstance arrival = runtime.Find(RHAH_EventChainProvider.ProviderKey, arrivalId);
            Assert.Equal(121, arrival.NextTick);

            Find.TickManager.DebugSetTicksGame(11);
            RHAH_EventFollowRuntime.TryPredator(map.uniqueID, 8);

            Assert.Equal(new[] { first, second }, component.PredationPrey);
            Assert.Equal(arrivalId, component.PredationArrivalInstanceId);
            Assert.Equal(121, arrival.NextTick);
            Assert.Equal(arrivalId, RHAH_EventChainEntry.QueueArrival(map, 111));
            Assert.Equal(111, arrival.NextTick);
        }

        [Fact]
        public void BeforeActivationArrivalRequestsKeepTheEarliestDueTick()
        {
            Current.Game.components.Clear();
            Current.Game.components.Add(new GameComponent_RHAH_Game(Current.Game));

            Assert.Equal(-1, RHAH_EventChainEntry.QueueArrival(map, 121));
            Assert.Equal(-1, RHAH_EventChainEntry.QueueArrival(map, 131));
            Assert.Equal(121, component.LegacyPredationPendingTick);
            Assert.Equal(-1, RHAH_EventChainEntry.QueueArrival(map, 111));
            Assert.Equal(111, component.LegacyPredationPendingTick);
        }

        [Fact]
        public void AShortFollowDoesNotCreateAnInteractiveLongRecord()
        {
            RHAH_Mod.Settings.SetIncidentLongChain("I-032", false);
            GameComponent_RHAH_Game game = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components.Add(game);
            Verse.Pawn pawn = Arrival(7, true);
            CompRHAH_Pawn.TryGet(pawn).State.role = HungerAndHavoc.Api.RHAH_PawnRole.RatkinYoung;

            RHAH_LongFollowRuntime.NoteArrival("I-032", 7, map, new[] { pawn });

            Assert.Empty(game.LongFollows);
        }

        [Fact]
        public void APredatorWaitsForHeldPreyWithoutOrderingAnAttackOrDeparture()
        {
            Verse.Pawn prey = Arrival(7, true);
            Verse.Pawn predator = Arrival(0, false);
            component.RememberPredationPrey(prey);
            RHAH_PredatorRecord record = new RHAH_PredatorRecord { pawn = predator, outside = true };
            MethodInfo apply = typeof(RHAH_CampPredation).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(MapComponent_RHAH_Map), typeof(RHAH_PredatorRecord), typeof(int), typeof(Job).MakeByRefType() }, null);
            object[] args = { component, record, 121, null };

            Assert.False((bool)apply.Invoke(null, args));

            Assert.Null(args[3]);
            Assert.Equal(121 + RHAH_PredationRules.SearchIntervalTicks, record.nextSearchTick);
        }

        [Fact]
        public void UnselectedShortChainDoesNotEnumerateMapContainers()
        {
            RHAH_Mod.Settings.followPredatorPercent = 0;
            map.listerThings = null;

            Assert.Contains("selected=false", RHAH_EventFollowRuntime.TryPredator(map.uniqueID, 7));

            Assert.Empty(component.PredationPrey);
            Assert.Equal(0, component.PredationArrivalInstanceId);
        }

        Verse.Pawn Arrival(int batchId, bool inContainer)
        {
            Verse.Pawn pawn = new Verse.Pawn();
            pawn.thingIDNumber = nextThingId++;
            pawn.def = Uninitialized<ThingDef>();
            pawn.def.category = ThingCategory.Pawn;
            pawn.def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
            Field(typeof(RaceProperties), "fleshType").SetValue(pawn.def.race, new FleshTypeDef { isOrganic = true });
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn };
            CompRHAH_Pawn origin = new CompRHAH_Pawn { parent = mark };
            origin.State.spawnBatchId = batchId;
            mark.comps = new List<HediffComp> { origin };
            pawn.health.hediffSet.hediffs.Add(mark);
            if (inContainer)
            {
                ((ThingOwner<Thing>)held.innerContainer).InnerListForReading.Add(pawn);
                pawn.holdingOwner = held.innerContainer;
            }
            else
            {
                ((List<Verse.Pawn>)map.mapPawns.AllPawnsSpawned).Add(pawn);
                Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
            }
            return pawn;
        }

        static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(type.FullName + "." + name);
        }

    }
}
