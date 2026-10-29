using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_IncidentEntryTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly Harmony harmony = new Harmony("rhah.incident.entry.tests");
        readonly Map map;
        readonly PlanetLayer layer;
        static readonly IntVec3 landing = new IntVec3(4, 0, 4);
        static bool edgeAvailable;
        static bool dropAvailable;
        static bool reachable;

        public RHAH_IncidentEntryTests()
        {
            Current.Game = Empty<Game>();
            Current.Game.components = new List<GameComponent>();
            Current.Game.tickManager = new TickManager();
            Current.ProgramState = ProgramState.Entry;
            RHAH_Mod.Settings = new RHAH_Settings();
            World world = Empty<World>();
            Current.Game.World = world;
            world.grid = new WorldGrid();
            layer = new OrbitLayer();
            Field(typeof(PlanetLayer), "def").SetValue(layer, new PlanetLayerDef { isSpace = false });
            Field(typeof(PlanetLayer), "layerId").SetValue(layer, 1);
            ((Dictionary<int, PlanetLayer>)Field(typeof(WorldGrid), "planetLayers").GetValue(world.grid)).Add(1, layer);
            layer.Tiles.Add(new Tile());
            Settlement home = new Settlement();
            Field(typeof(WorldObject), "tile").SetValue(home, new PlanetTile(0, layer));
            map = Empty<Map>();
            map.uniqueID = 41;
            map.info = new MapInfo { parent = home, Size = new IntVec3(9, 1, 9) };
            map.mapPawns = Empty<MapPawns>();
            map.cellIndices = new CellIndices(map);
            map.terrainGrid = new TerrainGrid(map);
            TerrainDef[] grid = (TerrainDef[])Field(typeof(TerrainGrid), "topGrid").GetValue(map.terrainGrid);
            for (int i = 0; i < grid.Length; i++) grid[i] = new TerrainDef();
            map.reachability = Empty<Reachability>();
            edgeAvailable = false;
            dropAvailable = true;
            reachable = true;
            Rand.PushState(97);
            Patch(typeof(RCellFinder), "TryFindRandomPawnEntryCell", nameof(Entry));
            Patch(typeof(GenGrid), "Standable", nameof(Standable));
            harmony.Patch(AccessTools.Method(typeof(GridsUtility), "Fogged", new[] { typeof(IntVec3), typeof(Map) }),
                prefix: new HarmonyMethod(typeof(RHAH_IncidentEntryTests), nameof(Unfogged)));
            Patch(typeof(DropCellFinder), "CanPhysicallyDropInto", nameof(Drop));
            Patch(typeof(Reachability), "CanReachColony", nameof(Reach));
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            Rand.PopState();
            Current.Game = previousGame;
            Current.ProgramState = previousState;
            RHAH_Mod.Settings = previousSettings;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OrbitGeometryUsesSafeReachableDropEvenWhenLayerIsNotSpace(bool isSpace)
        {
            layer.Def.isSpace = isSpace;
            GroupProbe worker = new GroupProbe();

            Assert.True(worker.Execute(map));
            Assert.Same(map, worker.Context.Map);
            Assert.Equal(landing, worker.Context.SpawnCell);
            Assert.True(worker.Context.DropPod);
        }

        [Fact]
        public void AvailableWalkInKeepsItsExistingArrivalMode()
        {
            edgeAvailable = true;
            GroupProbe worker = new GroupProbe();

            Assert.True(worker.Execute(map));
            Assert.False(worker.Context.DropPod);
        }

        [Fact]
        public void SurfaceMapsWithoutWalkInDoNotGainAnUnrelatedDropFallback()
        {
            PlanetLayer surface = new SurfaceLayer();
            Field(typeof(PlanetLayer), "def").SetValue(surface, new PlanetLayerDef());
            Field(typeof(PlanetLayer), "layerId").SetValue(surface, 1);
            ((Dictionary<int, PlanetLayer>)Field(typeof(WorldGrid), "planetLayers").GetValue(Find.WorldGrid))[1] = surface;

            Assert.False(RHAH_IncidentArrival.TryFindSpawnCell(map, out IntVec3 cell, out _));
            Assert.Equal(IntVec3.Invalid, cell);
        }

        [Theory]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void MissingOrUnreachableOrDangerousLandingDoesNotSubmitAnyIncident(bool canDrop, bool canReach, bool dangerous)
        {
            dropAvailable = canDrop;
            reachable = canReach;
            landing.GetTerrain(map).dangerous = dangerous;
            GroupProbe worker = new GroupProbe();

            Assert.False(worker.Execute(map));
            Assert.Null(worker.Context);
        }

        [Fact]
        public void OrbitalFallbackDoesNotBypassContentOrTemperatureSettings()
        {
            GroupProbe worker = new GroupProbe();
            RHAH_Mod.Settings.enableNewContent = false;
            Assert.False(worker.Execute(map));
            RHAH_Mod.Settings.enableNewContent = true;
            RHAH_Mod.Settings.minimumEventTemperature = 1f;
            RHAH_Mod.Settings.maximumEventTemperature = 2f;
            Assert.False(worker.Execute(map));
            Assert.Null(worker.Context);
        }


        void Patch(Type type, string method, string prefix)
        {
            harmony.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(typeof(RHAH_IncidentEntryTests), prefix));
        }

        static bool Entry(ref IntVec3 result, ref bool __result)
        {
            result = edgeAvailable ? landing : IntVec3.Invalid;
            __result = edgeAvailable;
            return false;
        }
        static bool Standable(IntVec3 c, ref bool __result) { __result = c == landing; return false; }
        static bool Unfogged(ref bool __result) { __result = false; return false; }
        static bool Drop(IntVec3 c, ref bool __result) { __result = dropAvailable && c == landing; return false; }
        static bool Reach(ref bool __result) { __result = reachable; return false; }
        static FieldInfo Field(Type type, string name) => type.GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        sealed class GroupProbe : IncidentWorker_RHAH_Group
        {
            internal RHAH_IncidentContext Context;
            protected override string DisplayId => "I-005";
            protected override RHAH_PawnRole Role => RHAH_PawnRole.Refugee;
            protected override bool SubmitFacts(RHAH_IncidentContext context) { Context = context; return true; }
            internal bool Execute(Map target) => TryExecuteWorker(new IncidentParms { target = target, points = 100f });
        }
    }
}
