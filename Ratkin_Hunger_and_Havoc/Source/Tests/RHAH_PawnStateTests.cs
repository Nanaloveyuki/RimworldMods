using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PawnStateTests
    {
        [Theory]
        [InlineData(RHAH_Lifecycle.Arriving, RHAH_Lifecycle.SeekingFood, true)]
        [InlineData(RHAH_Lifecycle.Arriving, RHAH_Lifecycle.Leaving, true)]
        [InlineData(RHAH_Lifecycle.Arriving, RHAH_Lifecycle.Released, true)]
        [InlineData(RHAH_Lifecycle.Arriving, RHAH_Lifecycle.Dead, true)]
        [InlineData(RHAH_Lifecycle.Arriving, RHAH_Lifecycle.Fed, false)]
        [InlineData(RHAH_Lifecycle.SeekingFood, RHAH_Lifecycle.Fed, true)]
        [InlineData(RHAH_Lifecycle.SeekingFood, RHAH_Lifecycle.Leaving, true)]
        [InlineData(RHAH_Lifecycle.SeekingFood, RHAH_Lifecycle.Released, true)]
        [InlineData(RHAH_Lifecycle.SeekingFood, RHAH_Lifecycle.Dead, true)]
        [InlineData(RHAH_Lifecycle.SeekingFood, RHAH_Lifecycle.Arriving, false)]
        [InlineData(RHAH_Lifecycle.Fed, RHAH_Lifecycle.Leaving, true)]
        [InlineData(RHAH_Lifecycle.Fed, RHAH_Lifecycle.Released, true)]
        [InlineData(RHAH_Lifecycle.Fed, RHAH_Lifecycle.Dead, true)]
        [InlineData(RHAH_Lifecycle.Fed, RHAH_Lifecycle.SeekingFood, false)]
        [InlineData(RHAH_Lifecycle.Fed, RHAH_Lifecycle.Arriving, false)]
        [InlineData(RHAH_Lifecycle.Leaving, RHAH_Lifecycle.Released, true)]
        [InlineData(RHAH_Lifecycle.Leaving, RHAH_Lifecycle.Dead, true)]
        [InlineData(RHAH_Lifecycle.Leaving, RHAH_Lifecycle.Fed, false)]
        [InlineData(RHAH_Lifecycle.Leaving, RHAH_Lifecycle.SeekingFood, false)]
        [InlineData(RHAH_Lifecycle.Released, RHAH_Lifecycle.Dead, true)]
        [InlineData(RHAH_Lifecycle.Released, RHAH_Lifecycle.Leaving, false)]
        [InlineData(RHAH_Lifecycle.Released, RHAH_Lifecycle.Arriving, false)]
        [InlineData(RHAH_Lifecycle.Dead, RHAH_Lifecycle.Released, false)]
        [InlineData(RHAH_Lifecycle.Dead, RHAH_Lifecycle.Arriving, false)]
        [InlineData(RHAH_Lifecycle.Dead, RHAH_Lifecycle.SeekingFood, false)]
        [InlineData(RHAH_Lifecycle.Dead, RHAH_Lifecycle.Fed, false)]
        [InlineData(RHAH_Lifecycle.Dead, RHAH_Lifecycle.Leaving, false)]
        public void TrySetLifecycle_RespectsLegalTransitions(
            RHAH_Lifecycle from,
            RHAH_Lifecycle to,
            bool legal)
        {
            RHAH_PawnState state = StateWithLifecycle(from);

            bool changed = state.TrySetLifecycle(to);

            Assert.Equal(legal, changed);
            Assert.Equal(legal ? to : from, state.lifecycle);
        }

        [Fact]
        public void TrySetLifecycle_SameValueDoesNotChange()
        {
            RHAH_PawnState state = StateWithLifecycle(RHAH_Lifecycle.SeekingFood);

            bool changed = state.TrySetLifecycle(RHAH_Lifecycle.SeekingFood);

            Assert.False(changed);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, state.lifecycle);
        }

        [Fact]
        public void TrySetLifecycle_FedSetsHasBeenFed()
        {
            RHAH_PawnState state = StateWithLifecycle(RHAH_Lifecycle.SeekingFood);

            Assert.True(state.TrySetLifecycle(RHAH_Lifecycle.Fed));
            Assert.True(state.hasBeenFed);
            Assert.True(state.TrySetLifecycle(RHAH_Lifecycle.Leaving));
            Assert.True(state.hasBeenFed);
        }

        [Fact]
        public void TrySetLifecycle_IllegalDoesNotSetHasBeenFed()
        {
            RHAH_PawnState state = StateWithLifecycle(RHAH_Lifecycle.Arriving);

            Assert.False(state.TrySetLifecycle(RHAH_Lifecycle.Fed));
            Assert.False(state.hasBeenFed);
            Assert.Equal(RHAH_Lifecycle.Arriving, state.lifecycle);
        }

        [Fact]
        public void BegSlapsCapAtTwoAndGnawSearchStartsWithoutCooldown()
        {
            RHAH_PawnState state = new RHAH_PawnState();

            Assert.Equal(-1, state.gnawSearchUntilTick);
            state.NoteBegSlap();
            state.NoteBegSlap();
            state.NoteBegSlap();

            Assert.Equal(2, state.begSlapCount);
            Assert.True(state.PrefersGnaw);
        }

        [Fact]
        public void ApplySeed_OverwritesIdentityAndCopiesCollections()
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.SetGate(RHAH_BehaviorGate.Beg, false);
            state.SetExtra("keep", "yes");
            List<int> children = new List<int> { 7, 8 };
            Dictionary<RHAH_BehaviorGate, bool> gates = new Dictionary<RHAH_BehaviorGate, bool>
            {
                { RHAH_BehaviorGate.Steal, true }
            };
            RHAH_PawnSeed seed = new RHAH_PawnSeed(
                "I-005",
                3,
                9,
                RHAH_PawnRole.Beggar,
                RHAH_Lifecycle.SeekingFood,
                true,
                RHAH_Attitude.Hostile,
                42,
                11,
                children,
                gates);

            state.ApplySeed(seed);
            children.Add(99);
            gates[RHAH_BehaviorGate.Fight] = true;
            state.childPawnLoadIds.Add(100);
            state.SetGate(RHAH_BehaviorGate.LeaveAfterFed, false);

            Assert.Equal("I-005", state.sourceIncidentDisplayId);
            Assert.Equal(3, state.spawnBatchId);
            Assert.Equal(9, state.relationshipGroupId);
            Assert.Equal(RHAH_PawnRole.Beggar, state.role);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, state.lifecycle);
            Assert.True(state.carriesPlague);
            Assert.Equal(RHAH_Attitude.Hostile, state.attitudeAtArrival);
            Assert.Equal(42, state.leaveAfterGameTick);
            Assert.Equal(11, state.parentPawnLoadId);
            Assert.Equal(new[] { 7, 8, 100 }, state.childPawnLoadIds);
            Assert.Equal(new[] { 7, 8 }, seed.ChildPawnLoadIds);
            Assert.True(state.GetGateOverride(RHAH_BehaviorGate.Steal));
            Assert.False(state.GetGateOverride(RHAH_BehaviorGate.LeaveAfterFed));
            Assert.Null(state.GetGateOverride(RHAH_BehaviorGate.Beg));
            Assert.False(seed.GateOverrides.ContainsKey(RHAH_BehaviorGate.Fight));
            Assert.True(state.TryGetExtra("keep", out string kept));
            Assert.Equal("yes", kept);
        }

        [Fact]
        public void ApplySeed_ReplacesPreviousGateOverrides()
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.ApplySeed(new RHAH_PawnSeed(
                gateOverrides: new Dictionary<RHAH_BehaviorGate, bool>
                {
                    { RHAH_BehaviorGate.Beg, true },
                    { RHAH_BehaviorGate.Steal, false }
                }));
            state.ApplySeed(new RHAH_PawnSeed(
                role: RHAH_PawnRole.Thief,
                gateOverrides: new Dictionary<RHAH_BehaviorGate, bool>
                {
                    { RHAH_BehaviorGate.Steal, true }
                }));

            Assert.Equal(RHAH_PawnRole.Thief, state.role);
            Assert.Null(state.GetGateOverride(RHAH_BehaviorGate.Beg));
            Assert.True(state.GetGateOverride(RHAH_BehaviorGate.Steal));
        }

        [Fact]
        public void ApplySeed_IgnoresNull()
        {
            RHAH_PawnState state = StateWithLifecycle(RHAH_Lifecycle.Arriving);
            state.sourceIncidentDisplayId = "I-001";

            state.ApplySeed(null);

            Assert.Equal("I-001", state.sourceIncidentDisplayId);
            Assert.Equal(RHAH_Lifecycle.Arriving, state.lifecycle);
        }

        [Fact]
        public void SetGate_OverrideAndClear()
        {
            RHAH_PawnState state = new RHAH_PawnState();

            state.SetGate(RHAH_BehaviorGate.Fight, true);
            Assert.True(state.GetGateOverride(RHAH_BehaviorGate.Fight));

            state.SetGate(RHAH_BehaviorGate.Fight, false);
            Assert.False(state.GetGateOverride(RHAH_BehaviorGate.Fight));

            state.SetGate(RHAH_BehaviorGate.Fight, null);
            Assert.Null(state.GetGateOverride(RHAH_BehaviorGate.Fight));
        }

        [Fact]
        public void Allows_OverrideBeatsDefault()
        {
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_PawnState state = new RHAH_PawnState();
            state.ApplySeed(new RHAH_PawnSeed(
                role: RHAH_PawnRole.Beggar,
                lifecycle: RHAH_Lifecycle.SeekingFood));

            Assert.True(state.Allows(RHAH_BehaviorGate.Beg));
            state.SetGate(RHAH_BehaviorGate.Beg, false);
            Assert.False(state.Allows(RHAH_BehaviorGate.Beg));
            state.SetGate(RHAH_BehaviorGate.Beg, null);
            Assert.True(state.Allows(RHAH_BehaviorGate.Beg));
        }

        [Fact]
        public void ExtraData_SetGetAndDelete()
        {
            RHAH_PawnState state = new RHAH_PawnState();

            state.SetExtra("mod.key", "a");
            Assert.True(state.TryGetExtra("mod.key", out string value));
            Assert.Equal("a", value);

            state.SetExtra("mod.key", "b");
            Assert.True(state.TryGetExtra("mod.key", out value));
            Assert.Equal("b", value);

            state.SetExtra("mod.key", null);
            Assert.False(state.TryGetExtra("mod.key", out value));
            Assert.Null(value);

            state.SetExtra(null, "x");
            state.SetExtra("", "x");
            Assert.False(state.TryGetExtra(null, out value));
            Assert.Null(value);
            Assert.False(state.TryGetExtra("", out value));
            Assert.Null(value);
            Assert.False(state.TryGetExtra("missing", out value));
            Assert.Null(value);
        }

        [Fact]
        public void ToSnapshot_IsCopy()
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.ApplySeed(new RHAH_PawnSeed(
                "I-014",
                1,
                2,
                RHAH_PawnRole.Refugee,
                RHAH_Lifecycle.Fed,
                false,
                RHAH_Attitude.Neutral,
                8,
                4,
                new[] { 1, 2 }));
            state.hasBeenFed = true;

            RHAH_PawnSnapshot snapshot = state.ToSnapshot();
            state.TrySetLifecycle(RHAH_Lifecycle.Leaving);
            state.childPawnLoadIds.Add(3);
            state.sourceIncidentDisplayId = "I-015";

            Assert.Equal("I-014", snapshot.SourceIncidentDisplayId);
            Assert.Equal(RHAH_Lifecycle.Fed, snapshot.Lifecycle);
            Assert.Equal(new[] { 1, 2 }, snapshot.ChildPawnLoadIds);
            Assert.True(snapshot.HasBeenFed);
            Assert.False(snapshot.IsReleased);
            Assert.True(snapshot.IsActiveVisitor);
            Assert.Equal(RHAH_Lifecycle.Leaving, state.lifecycle);
        }

        [Fact]
        public void IsReleasedAndIsActiveVisitor_FollowLifecycle()
        {
            RHAH_PawnState state = new RHAH_PawnState();
            Assert.False(state.IsReleased);
            Assert.True(state.IsActiveVisitor);

            state.ApplySeed(new RHAH_PawnSeed(lifecycle: RHAH_Lifecycle.Released));
            Assert.True(state.IsReleased);
            Assert.False(state.IsActiveVisitor);

            state.ApplySeed(new RHAH_PawnSeed(lifecycle: RHAH_Lifecycle.Dead));
            Assert.False(state.IsReleased);
            Assert.False(state.IsActiveVisitor);
        }

        [Fact]
        public void EnsureCollections_TreatsNullAsEmpty()
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.childPawnLoadIds = null;
            state.gateOverrides = null;
            state.extraData = null;

            state.EnsureCollections();

            Assert.Empty(state.childPawnLoadIds);
            Assert.Empty(state.gateOverrides);
            Assert.Empty(state.extraData);
        }

        static RHAH_PawnState StateWithLifecycle(RHAH_Lifecycle lifecycle)
        {
            RHAH_PawnState state = new RHAH_PawnState();
            state.ApplySeed(new RHAH_PawnSeed(lifecycle: lifecycle));
            return state;
        }
    }
}
