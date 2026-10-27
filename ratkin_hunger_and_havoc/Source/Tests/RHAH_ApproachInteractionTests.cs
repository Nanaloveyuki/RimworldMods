using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ApproachInteractionTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
        readonly bool previousProfiling = DeepProfiler.enabled;
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly LoadedLanguage previousDefaultLanguage = LanguageDatabase.defaultLanguage;
        readonly Faction player;
        readonly RimWorld.Planet.Caravan caravan;

        static RHAH_ApproachInteractionTests()
        {
            RuntimeHelpers.RunClassConstructor(typeof(RHAH_ContentApplierTests).TypeHandle);
        }

        public RHAH_ApproachInteractionTests()
        {
            Current.Game = Empty<Game>();
            Current.Game.components = new List<GameComponent>();
            Current.Game.tickManager = new TickManager();
            Current.ProgramState = ProgramState.Entry;
            RHAH_Mod.Settings = new RHAH_Settings();
            DeepProfiler.enabled = false;
            Field(typeof(Prefs), "data").SetValue(null, new PrefsData { devMode = false });
            LanguageDatabase.activeLanguage = Empty<LoadedLanguage>();
            Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(LanguageDatabase.activeLanguage, true);
            Field(typeof(LoadedLanguage), "workerInt").SetValue(LanguageDatabase.activeLanguage, new LanguageWorker_English());
            LanguageDatabase.activeLanguage.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            LanguageDatabase.defaultLanguage = LanguageDatabase.activeLanguage;
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            World world = Empty<World>();
            Current.Game.World = world;
            world.grid = new WorldGrid();
            world.worldObjects = new WorldObjectsHolder();
            world.factionManager = new FactionManager();
            PlanetLayer layer = new PlanetLayer();
            Field(typeof(PlanetLayer), "def").SetValue(layer, new PlanetLayerDef());
            Field(typeof(PlanetLayer), "layerId").SetValue(layer, 0);
            ((Dictionary<int, PlanetLayer>)Field(typeof(WorldGrid), "planetLayers").GetValue(world.grid)).Add(0, layer);
            for (int i = 0; i < 4; i++) layer.Tiles.Add(new Tile { PrimaryBiome = new BiomeDef { implemented = true } });
            player = new Faction { def = new FactionDef { isPlayer = true }, loadID = 1 };
            world.factionManager.AllFactionsListForReading.Add(player);
            Field(typeof(FactionManager), "ofPlayer").SetValue(world.factionManager, player);
            caravan = new RimWorld.Planet.Caravan { ID = 11, def = new WorldObjectDef { allowCaravanIncidentsWhichGenerateMap = true } };
            Field(typeof(WorldObject), "factionInt").SetValue(caravan, player);
            caravan.PawnsListForReading.Add(new Verse.Pawn());
            Add(caravan, 3);
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            Current.ProgramState = previousState;
            RHAH_Mod.Settings = previousSettings;
            Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
            DeepProfiler.enabled = previousProfiling;
            LanguageDatabase.activeLanguage = previousLanguage;
            LanguageDatabase.defaultLanguage = previousDefaultLanguage;
        }

        [Fact]
        public void OverlappingApproachesRemainInterceptableButOccupiedSitesStayBlocked()
        {
            ArrivalProbe approach = Approach();
            Add(new WorldObject_RHAH_Approach { ID = 12, def = approach.def }, 3);
            Assert.True(approach.CanIntercept(caravan));
            Assert.True((bool)Call(approach, "CanGenerateInterceptionMap"));
            Command_Action command = Assert.Single(approach.GetCaravanGizmos(caravan).OfType<Command_Action>());
            Assert.False(command.Disabled);
            Add(new WorldObject { ID = 13, def = new WorldObjectDef { allowCaravanIncidentsWhichGenerateMap = false } }, 3);
            Assert.False((bool)Call(approach, "CanGenerateInterceptionMap"));
            Assert.True(Assert.Single(approach.GetCaravanGizmos(caravan).OfType<Command_Action>()).Disabled);
        }

        [Fact]
        public void CaravanCommandsRespectTileAndDisabledContent()
        {
            ArrivalProbe approach = Approach();
            Field(typeof(WorldObject), "tile").SetValue(caravan, new PlanetTile(2));
            Assert.Empty(approach.GetCaravanGizmos(caravan));
            Field(typeof(WorldObject), "tile").SetValue(caravan, new PlanetTile(3));
            RHAH_Mod.Settings.enableNewContent = false;
            Assert.Empty(approach.GetCaravanGizmos(caravan));
        }

        [Fact]
        public void AlreadyOnDestinationStartsArrivalBeforeSpendingStaleTravelAndRetriesWithoutMoving()
        {
            ArrivalProbe approach = Approach();
            Settlement home = new Settlement { ID = 14, def = new WorldObjectDef { canBePlayerHome = true } };
            Field(typeof(WorldObject), "tile").SetValue(home, new PlanetTile(3));
            Field(typeof(WorldObject), "factionInt").SetValue(home, player);
            Map map = Empty<Map>();
            map.uniqueID = 41;
            map.info = new MapInfo { parent = home };
            Find.Maps.Add(map);
            Field(typeof(WorldObject_RHAH_Approach), "costLeft").SetValue(approach, 20000);
            Field(typeof(WorldObject_RHAH_Approach), "nextTileId").SetValue(approach, 2);
            Call(approach, "TickInterval", 64);
            Assert.True((bool)Field(typeof(WorldObject_RHAH_Approach), "arrivalPending").GetValue(approach));
            Assert.Equal(-1, Field(typeof(WorldObject_RHAH_Approach), "nextTileId").GetValue(approach));
            Assert.Equal(2500, Field(typeof(WorldObject_RHAH_Approach), "costLeft").GetValue(approach));
            Assert.Equal(new PlanetTile(3), approach.Tile);
            Assert.False(approach.RemovalRequested);
            Call(approach, "TickInterval", 2499);
            Assert.Equal(1, Field(typeof(WorldObject_RHAH_Approach), "costLeft").GetValue(approach));
            Current.Game.components.Add(new GameComponent_RHAH_Game(Current.Game));
            approach.Configure("unknown", 300f, 41, false);
            Call(approach, "TickInterval", 1);
            Assert.True(approach.RemovalRequested);
            Assert.Equal(new PlanetTile(3), approach.Tile);
        }

        ArrivalProbe Approach()
        {
            ArrivalProbe approach = new ArrivalProbe { ID = 10, def = new WorldObjectDef { defName = "RHAH_Approach" } };
            approach.Configure("I-001", 300f, 41, false);
            Add(approach, 3);
            return approach;
        }

        static void Add(WorldObject obj, int tile)
        {
            Field(typeof(WorldObject), "tile").SetValue(obj, new PlanetTile(tile));
            Find.WorldObjects.AllWorldObjects.Add(obj);
            ((HashSet<WorldObject>)Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(Find.WorldObjects)).Add(obj);
        }

        sealed class ArrivalProbe : WorldObject_RHAH_Approach
        {
            internal bool RemovalRequested;
            public override void Destroy() => RemovalRequested = true;
        }

        static object Call(object obj, string method, params object[] args) => typeof(WorldObject_RHAH_Approach)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
        static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
