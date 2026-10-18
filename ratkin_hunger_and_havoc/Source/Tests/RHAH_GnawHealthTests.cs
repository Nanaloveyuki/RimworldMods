using System.Collections.Generic;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_GnawHealthTests
    {
        [Theory]
        [InlineData(100, 10, 90)]
        [InlineData(100000, 10, 99990)]
        [InlineData(100000, 12, 99988)]
        [InlineData(100000, 0, 100000)]
        [InlineData(5, 12, 0)]
        public void BiteRemovesFixedHitPointsRegardlessOfDurability(int hitPoints, int bite, int expected)
        {
            Assert.Equal(expected, RHAH_GnawHealth.FixedHitPointsAfterBite(hitPoints, bite));
        }

        [Theory]
        [InlineData(4, 8, true)]
        [InlineData(8, 4, false)]
        [InlineData(4, 4, true)]
        public void GnawChoosesNearestTarget(int treeDistance, int wallDistance, bool chooseTree)
        {
            Thing tree = new Thing { Position = new IntVec3(treeDistance, 0, 0) };
            Thing wall = new Thing { Position = new IntVec3(wallDistance, 0, 0) };
            Assert.Same(chooseTree ? tree : wall, RHAH_GnawHealth.NearestTarget(IntVec3.Zero, tree, wall));
            Assert.Same(tree, RHAH_GnawHealth.NearestTarget(IntVec3.Zero, tree, null));
            Assert.Same(wall, RHAH_GnawHealth.NearestTarget(IntVec3.Zero, null, wall));
        }

        [Fact]
        public void TwoSlapsSwitchToSelfFeedingAndStopCounting()
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.NoteBegSlap();
            Assert.False(state.PrefersGnaw);
            state.NoteBegSlap();
            Assert.True(state.PrefersGnaw);
            state.NoteBegSlap();
            Assert.Equal(2, state.begSlapCount);
        }

        [Fact]
        public void ShortStayOnlyUnlocksApparelItLocked()
        {
            Verse.Pawn pawn = new Verse.Pawn();
            pawn.apparel = new Pawn_ApparelTracker(pawn);
            Apparel foreignLocked = new Apparel();
            Apparel newlyLocked = new Apparel();
            pawn.apparel.WornApparel.Add(foreignLocked);
            pawn.apparel.WornApparel.Add(newlyLocked);
            pawn.apparel.Lock(foreignLocked);
            CompRHAH_Pawn comp = new CompRHAH_Pawn();

            RHAH_VisitorStay.LockShortStayApparel(pawn, comp);
            Assert.True(pawn.apparel.IsLocked(newlyLocked));
            RHAH_VisitorStay.LockShortStayApparel(pawn, comp);
            RHAH_VisitorStay.UnlockShortStayApparel(pawn, comp);

            Assert.True(pawn.apparel.IsLocked(foreignLocked));
            Assert.False(pawn.apparel.IsLocked(newlyLocked));
            Assert.Null(comp.shortStayLockedApparel);
        }

        [Fact]
        public void WallCount_OvergnawsOnFifthAndForgetsOnLeave()
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();

            Assert.Equal(0, RHAH_GnawHealth.NextWallCount(counts, 0));
            Assert.Equal(1, RHAH_GnawHealth.NextWallCount(counts, 4));
            Assert.Equal(2, RHAH_GnawHealth.NextWallCount(counts, 4));
            Assert.Equal(3, RHAH_GnawHealth.NextWallCount(counts, 4));
            Assert.Equal(4, RHAH_GnawHealth.NextWallCount(counts, 4));
            Assert.False(RHAH_GnawHealth.IsOvergnaw(4));
            Assert.Equal(5, RHAH_GnawHealth.NextWallCount(counts, 4));
            Assert.True(RHAH_GnawHealth.IsOvergnaw(5));

            RHAH_GnawHealth.Forget(counts, 4);
            Assert.False(counts.ContainsKey(4));
            Assert.Equal(1, RHAH_GnawHealth.NextWallCount(counts, 4));
        }

        [Fact]
        public void ClampFood_StopsAtCurrentMaximum()
        {
            Assert.Equal(0.4f, RHAH_GnawHealth.ClampFood(1.2f, 0.4f));
            Assert.Equal(0.3f, RHAH_GnawHealth.ClampFood(0.3f, 0.4f));
            Assert.True(float.IsNaN(RHAH_GnawHealth.ClampFood(float.NaN, 0.4f)));
        }
    }
}
