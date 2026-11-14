using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_MapResolverTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly Harmony harmony = new Harmony("rhah.map.resolver.tests");
        static readonly HashSet<Map> homes = new HashSet<Map>();
        static readonly HashSet<Map> excluded = new HashSet<Map>();

        public RHAH_MapResolverTests()
        {
            Current.Game = Empty<Game>();
            Current.Game.components = new List<GameComponent>();
            Current.Game.tickManager = new TickManager();
            Current.Game.components.Add(new GameComponent_RHAH_Game(Current.Game));
            RHAH_Mod.Settings = new RHAH_Settings();
            typeof(Game).GetField("maps", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(Current.Game, new List<Map>());
            Current.Game.currentMapIndex = -1;
            Current.ProgramState = ProgramState.Entry;
            homes.Clear();
            excluded.Clear();
            harmony.Patch(AccessTools.PropertyGetter(typeof(Map), nameof(Map.IsPlayerHome)),
                prefix: new HarmonyMethod(typeof(RHAH_MapResolverTests), nameof(IsHome)));
            harmony.Patch(AccessTools.Method(typeof(RHAH_Approach), nameof(RHAH_Approach.AllowsHome)),
                prefix: new HarmonyMethod(typeof(RHAH_MapResolverTests), nameof(AllowsHome)));
            harmony.Patch(AccessTools.Method(typeof(GameComponent_RHAH_Game), nameof(GameComponent_RHAH_Game.TrySpawnPending)),
                prefix: new HarmonyMethod(typeof(RHAH_MapResolverTests), nameof(KeepQueued)));
            Rand.PushState(47);
        }

        public void Dispose()
        {
            Rand.PopState();
            harmony.UnpatchAll(harmony.Id);
            homes.Clear();
            excluded.Clear();
            Current.Game = previousGame;
            Current.ProgramState = previousState;
            RHAH_Mod.Settings = previousSettings;
        }

        [Fact]
        public void DeveloperTriggerPrefersViewedHomeAmongMultipleHomes()
        {
            AddMap(14);
            Map viewed = AddMap(21);
            Current.Game.currentMapIndex = 1;

            Assert.Same(viewed, RHAH_MapResolver.ResolveDebugMap());
        }

        [Fact]
        public void PocketMapsCannotBeSelectedEvenIfTreatedAsPlayerHomes()
        {
            Map home = AddMap(14);
            Map pocket = AddMap(15);
            pocket.info.isPocketMap = true;
            Current.Game.currentMapIndex = 1;

            Assert.Same(home, RHAH_MapResolver.ResolveDebugMap());
            Assert.Same(home, RHAH_MapResolver.Resolve());
            Assert.Null(RHAH_MapResolver.Resolve(pocket));
            Assert.False(RHAH_IncidentArrival.TryFindSpawnCell(pocket, out _, out _));
            Assert.False(RHAH_IncidentArrival.TryFindSpawnCell(pocket, out _, out _, true));
        }

        [Fact]
        public void InvalidWorldTileAndNonHomeMapsAreExcludedFromAutomaticSelection()
        {
            Map invalid = AddMap(15);
            SetTile(invalid, PlanetTile.Invalid);
            Map site = AddMap(16);
            homes.Remove(site);
            Map home = AddMap(14);
            Current.Game.currentMapIndex = 1;

            Assert.Same(home, RHAH_MapResolver.ResolveDebugMap());
            Assert.Same(home, RHAH_MapResolver.Resolve());
        }

        [Fact]
        public void MapSettingsCanExcludeViewedHomeAndNoEligibleHomeReturnsNull()
        {
            Map viewed = AddMap(14);
            Map fallback = AddMap(21);
            Current.Game.currentMapIndex = 0;
            excluded.Add(viewed);

            Assert.Same(fallback, RHAH_MapResolver.ResolveDebugMap());
            excluded.Add(fallback);
            Assert.Null(RHAH_MapResolver.ResolveDebugMap());
        }

        [Fact]
        public void QueuedTargetKeepsItsHomeAfterTheViewChanges()
        {
            Map original = AddMap(14);
            AddMap(21);
            Current.Game.currentMapIndex = 0;
            Assert.True(RHAH_Scheduler.QueueDebugIncident("I-009"));
            GameComponent_RHAH_Game game = Current.Game.GetComponent<GameComponent_RHAH_Game>();
            int targetId = Assert.Single(game.PendingIncidentTargetIds);
            Current.Game.currentMapIndex = 1;

            Assert.Same(original, RHAH_QueuedTarget.Resolve(RHAH_IncidentTarget.Map, targetId).Target);
        }

        [Fact]
        public void SavedPocketTargetTerminatesWithoutRedirectingToAnotherHome()
        {
            AddMap(14);
            Map pocket = AddMap(15);
            pocket.info.isPocketMap = true;

            RHAH_QueuedTarget result = RHAH_QueuedTarget.Resolve(RHAH_IncidentTarget.Map, 15);

            Assert.True(result.Terminal);
            Assert.Null(result.Target);
            Assert.Null(RHAH_MapResolver.Resolve(selectedMapId: 15));
        }

        static Map AddMap(int id)
        {
            Map map = Empty<Map>();
            map.uniqueID = id;
            map.mapPawns = Empty<MapPawns>();
            map.info = new MapInfo { parent = new Settlement() };
            SetTile(map, new PlanetTile(0));
            Find.Maps.Add(map);
            homes.Add(map);
            return map;
        }

        static void SetTile(Map map, PlanetTile tile)
        {
            AccessTools.Field(typeof(WorldObject), "tile").SetValue(map.Parent, tile);
        }

        static bool IsHome(Map __instance, ref bool __result)
        {
            __result = homes.Contains(__instance);
            return false;
        }

        static bool AllowsHome(Map map, ref bool __result)
        {
            __result = !excluded.Contains(map);
            return false;
        }

        static bool KeepQueued(ref bool __result)
        {
            __result = false;
            return false;
        }

        static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
