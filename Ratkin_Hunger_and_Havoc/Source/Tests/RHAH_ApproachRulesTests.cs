using System;
using System.Reflection;
using System.Runtime.Serialization;
using Verse;
using System.Collections.Generic;
using HungerAndHavoc.Incidents;
using RimWorld.Planet;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_ApproachRulesTests
    {
        [Fact]
        public void OnlyMapIncidentsWalkIn()
        {
            Assert.True(RHAH_ApproachRules.UsesApproach(RHAH_IncidentTarget.Map));
            Assert.False(RHAH_ApproachRules.UsesApproach(RHAH_IncidentTarget.Caravan));
        }

        [Fact]
        public void FlatTileTakesAThirdOfADay()
        {
            Assert.Equal(20000, RHAH_ApproachRules.MoveTicks(1f, 1f));
            Assert.Equal(10000, RHAH_ApproachRules.MoveTicks(1f, 0.5f));
            Assert.Equal(20000, RHAH_ApproachRules.MoveTicks(0f, 1f));
            Assert.Equal(30000, RHAH_ApproachRules.MoveTicks(2f, 1f));
        }

        [Fact]
        public void SpendingStopsAtZero()
        {
            Assert.Equal(64, RHAH_ApproachRules.Spend(128, 64));
            Assert.Equal(0, RHAH_ApproachRules.Spend(10, 64));
            Assert.Equal(10, RHAH_ApproachRules.Spend(10, 0));
        }

        [Fact]
        public void DistanceStaysBetweenFourAndSix()
        {
            Assert.Equal(4, RHAH_ApproachRules.PickDistance(0));
            Assert.Equal(5, RHAH_ApproachRules.PickDistance(1));
            Assert.Equal(6, RHAH_ApproachRules.PickDistance(2));
            Assert.Equal(4, RHAH_ApproachRules.PickDistance(3));
            Assert.Equal(6, RHAH_ApproachRules.PickDistance(-1));
        }

        [Fact]
        public void IslandCrossingStartsOnTheNearShore()
        {
            Assert.False(RHAH_ApproachRules.CanWalk(true, false));
            Assert.True(RHAH_ApproachRules.CanWalk(true, true));
            Assert.Equal(-1, RHAH_ApproachRules.PickCrossing(-1, 8, int.MaxValue, 3));
            Assert.Equal(8, RHAH_ApproachRules.PickCrossing(-1, 8, int.MaxValue, 4));
            Assert.Equal(8, RHAH_ApproachRules.PickCrossing(8, 9, 4, 6));
            Assert.False(RHAH_ApproachRules.ReleasePath(true, false));
            Assert.False(RHAH_ApproachRules.ReleasePath(false, true));
            Assert.True(RHAH_ApproachRules.ReleasePath(false, false));
        }

        [Fact]
        public void SpaceHomesStayOutUnlessAllowed()
        {
            Assert.True(RHAH_ApproachRules.IsSpaceHome(true, "TemperateForest"));
            Assert.True(RHAH_ApproachRules.IsSpaceHome(false, "OuterSpaceBiome"));
            Assert.False(RHAH_ApproachRules.IsSpaceHome(false, "TemperateForest"));
            Assert.False(RHAH_ApproachRules.IsSpaceHome(false, null));
            Assert.False(RHAH_ApproachRules.AllowsHome(true, false));
            Assert.True(RHAH_ApproachRules.AllowsHome(true, true));
            Assert.True(RHAH_ApproachRules.AllowsHome(false, false));
            Assert.False(RHAH_ApproachRules.BlocksLaunch(true));
            Assert.True(RHAH_ApproachRules.BlocksLaunch(false));
        }

        [Fact]
        public void SharedTileAddsInterceptWhenThePlayerCaravanHidesIt()
        {
            Assert.True(RHAH_ApproachRules.IncludeApproachMenu(true, false, true, true));
            Assert.False(RHAH_ApproachRules.IncludeApproachMenu(true, true, true, true));
            Assert.False(RHAH_ApproachRules.IncludeApproachMenu(false, false, true, true));
            Assert.False(RHAH_ApproachRules.IncludeApproachMenu(true, false, false, true));
            Assert.False(RHAH_ApproachRules.IncludeApproachMenu(true, false, true, false));
        }


        [Fact]
        public void AdjacentPathYieldsTheColonyOnce()
        {
            using (WorldPath path = Path(8, 3))
            {
                Assert.True(RHAH_ApproachRules.TryConsumeNext(path, out PlanetTile next));
                Assert.Equal(8, next.tileId);
                Assert.Equal(1, path.NodesLeftCount);
            }

            using (WorldPath longer = Path(8, 5, 3))
            {
                Assert.True(RHAH_ApproachRules.TryConsumeNext(longer, out PlanetTile next));
                Assert.Equal(5, next.tileId);
                Assert.Equal(2, longer.NodesLeftCount);
            }

            using (WorldPath arrived = Path(8))
            {
                Assert.False(RHAH_ApproachRules.TryConsumeNext(arrived, out _));
            }

            Assert.False(RHAH_ApproachRules.TryConsumeNext(null, out _));
            Assert.False(RHAH_ApproachRules.TryConsumeNext(WorldPath.NotFound, out _));

            using (WorldPath corrupt = Path(8, 3))
            {
                corrupt.NodesReversed.Clear();
                Assert.False(RHAH_ApproachRules.TryConsumeNext(corrupt, out _));
            }
        }

        [Fact]
        public void ArrivalWithoutRuntimeKeepsItsEventForLater()
        {
            Game previous = Current.Game;
            try
            {
                Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                Current.Game.components = new List<GameComponent>();
                typeof(Game).GetField("maps", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(Current.Game, new List<Map>());
                ArrivalProbe approach = (ArrivalProbe)FormatterServices.GetUninitializedObject(typeof(ArrivalProbe));
                approach.Configure("I-001", 300f, 41, false);
                MethodInfo arrive = typeof(WorldObject_RHAH_Approach).GetMethod("Arrive", BindingFlags.Instance | BindingFlags.NonPublic);
                arrive.Invoke(approach, null);
                Assert.False(approach.RemovalRequested);
                Assert.True((bool)typeof(WorldObject_RHAH_Approach).GetField("arrivalPending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(approach));
                Assert.Equal(2500, (int)typeof(WorldObject_RHAH_Approach).GetField("costLeft", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(approach));
            }
            finally
            {
                Current.Game = previous;
            }
        }

        sealed class ArrivalProbe : WorldObject_RHAH_Approach
        {
            internal bool RemovalRequested;

            public override void Destroy()
            {
                RemovalRequested = true;
            }
        }

        static WorldPath Path(params int[] destinationFirst)
        {
            WorldPath path = new WorldPath();
            for (int i = 0; i < destinationFirst.Length; i++)
            {
                path.AddNodeAtStart(new PlanetTile(destinationFirst[i]));
            }

            path.SetupFound(1f, null);
            return path;
        }
    }
}
