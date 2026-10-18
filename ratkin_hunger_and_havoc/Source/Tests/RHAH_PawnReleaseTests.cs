using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PawnReleaseTests
    {
        static RHAH_PawnReleaseTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        [Fact]
        public void HostWithoutMark_ReleaseFails()
        {
            RHAH_Api.Bind(new RHAH_ApiHost());
            try
            {
                Assert.False(RHAH_Api.ReleaseToColony(null, RHAH_ReleaseReason.Recruited));
                Assert.False(new RHAH_ApiHost().ReleaseToColony(null, RHAH_ReleaseReason.Recruited));
            }
            finally
            {
                RHAH_Api.Bind(null);
            }
        }

        [Fact]
        public void ReleaseToColonyAlreadyReleasedPolicyRejectAndSuccess()
        {
            RHAH_PawnBehaviors.ResetForTests();
            int releasedEvents = 0;
            int lifecycleEvents = 0;
            RHAH_Api.ReleasedToColony += OnReleased;
            RHAH_Api.LifecycleChanged += OnLifecycle;
            try
            {
                RHAH_PawnState state = new RHAH_PawnState();
                state.ApplySeed(new RHAH_PawnSeed(
                    sourceIncidentDisplayId: "I-001",
                    role: RHAH_PawnRole.Refugee,
                    lifecycle: RHAH_Lifecycle.Arriving));

                RHAH_PawnBehaviors.Register(new StubBehavior { ReleaseResult = false });
                Assert.False(state.ReleaseToColony(RHAH_ReleaseReason.Recruited));
                Assert.Equal(RHAH_Lifecycle.Arriving, state.lifecycle);
                Assert.Equal(0, releasedEvents);
                Assert.Equal(0, lifecycleEvents);

                RHAH_PawnBehaviors.ResetForTests();
                Assert.True(state.ReleaseToColony(RHAH_ReleaseReason.Recruited));
                Assert.Equal(RHAH_Lifecycle.Released, state.lifecycle);
                Assert.True(state.IsReleased);
                Assert.Equal(1, releasedEvents);
                Assert.Equal(1, lifecycleEvents);

                Assert.True(state.ReleaseToColony(RHAH_ReleaseReason.Imprisoned));
                Assert.Equal(RHAH_Lifecycle.Released, state.lifecycle);
                Assert.Equal(1, releasedEvents);
                Assert.Equal(1, lifecycleEvents);
            }
            finally
            {
                RHAH_Api.ReleasedToColony -= OnReleased;
                RHAH_Api.LifecycleChanged -= OnLifecycle;
                RHAH_PawnBehaviors.ResetForTests();
            }

            void OnReleased(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                releasedEvents++;
                Assert.NotNull(snapshot);
                Assert.True(snapshot.IsReleased);
            }

            void OnLifecycle(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_Lifecycle lifecycle)
            {
                lifecycleEvents++;
                Assert.Equal(RHAH_Lifecycle.Released, lifecycle);
            }
        }

        [Fact]
        public void IllegalLifecycleDoesNotRaiseEvent()
        {
            int lifecycleEvents = 0;
            RHAH_Api.LifecycleChanged += OnLifecycle;
            try
            {
                RHAH_PawnState state = new RHAH_PawnState();
                state.ApplySeed(new RHAH_PawnSeed(lifecycle: RHAH_Lifecycle.Arriving));
                Assert.False(state.TrySetLifecycle(RHAH_Lifecycle.Fed));
                Assert.Equal(RHAH_Lifecycle.Arriving, state.lifecycle);
                Assert.Equal(0, lifecycleEvents);

                Assert.True(state.TrySetLifecycle(RHAH_Lifecycle.SeekingFood));
                Assert.Equal(0, lifecycleEvents);

                Assert.False(state.TrySetLifecycle(RHAH_Lifecycle.SeekingFood));
                Assert.Equal(RHAH_Lifecycle.SeekingFood, state.lifecycle);
                Assert.Equal(0, lifecycleEvents);
            }
            finally
            {
                RHAH_Api.LifecycleChanged -= OnLifecycle;
            }

            void OnLifecycle(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_Lifecycle lifecycle)
            {
                lifecycleEvents++;
            }
        }

        sealed class StubBehavior : IRHAH_PawnBehavior
        {
            public bool? ReleaseResult;

            public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
            {
                return null;
            }

            public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                return ReleaseResult;
            }
        }
    }
}
