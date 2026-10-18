using HungerAndHavoc.Api;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PawnBehaviorsTests
    {
        static RHAH_PawnBehaviorsTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        [Fact]
        public void QueryUsesLaterRegistrationAndSkipsNull()
        {
            RHAH_PawnBehaviors.ResetForTests();
            try
            {
                RHAH_PawnSnapshot snapshot = new RHAH_PawnSnapshot(
                    "I-001",
                    1,
                    1,
                    RHAH_PawnRole.Beggar,
                    RHAH_Lifecycle.SeekingFood,
                    false,
                    -1,
                    false,
                    RHAH_Attitude.Neutral,
                    RHAH_Attitude.Neutral,
                    0,
                    null);
                StubBehavior first = new StubBehavior { AllowsResult = true };
                StubBehavior second = new StubBehavior { AllowsResult = false };
                StubBehavior third = new StubBehavior { AllowsResult = null };

                RHAH_PawnBehaviors.Register(null);
                RHAH_PawnBehaviors.Register(first);
                RHAH_PawnBehaviors.Register(first);
                RHAH_PawnBehaviors.Register(second);
                RHAH_PawnBehaviors.Register(third);

                Assert.False(RHAH_PawnBehaviors.Query(behavior =>
                    behavior.Allows(null, snapshot, RHAH_BehaviorGate.Beg)));

                RHAH_PawnBehaviors.Unregister(second);
                Assert.True(RHAH_PawnBehaviors.Query(behavior =>
                    behavior.Allows(null, snapshot, RHAH_BehaviorGate.Beg)));

                RHAH_PawnBehaviors.Unregister(first);
                RHAH_PawnBehaviors.Unregister(third);
                RHAH_PawnBehaviors.Unregister(null);
                Assert.Null(RHAH_PawnBehaviors.Query(behavior =>
                    behavior.Allows(null, snapshot, RHAH_BehaviorGate.Beg)));
                Assert.Null(RHAH_PawnBehaviors.Query(null));
            }
            finally
            {
                RHAH_PawnBehaviors.ResetForTests();
            }
        }

        [Fact]
        public void ResetForTestsClearsHandlers()
        {
            RHAH_PawnBehaviors.ResetForTests();
            try
            {
                RHAH_PawnBehaviors.Register(new StubBehavior { AllowsResult = true });
                RHAH_PawnBehaviors.ResetForTests();
                Assert.Null(RHAH_PawnBehaviors.Query(behavior =>
                    behavior.Allows(null, DummySnapshot(), RHAH_BehaviorGate.Steal)));
            }
            finally
            {
                RHAH_PawnBehaviors.ResetForTests();
            }
        }

        static RHAH_PawnSnapshot DummySnapshot()
        {
            return new RHAH_PawnSnapshot(
                "I-001",
                0,
                0,
                RHAH_PawnRole.Unspecified,
                RHAH_Lifecycle.Arriving,
                false,
                -1,
                false,
                RHAH_Attitude.Neutral,
                RHAH_Attitude.Neutral,
                0,
                null);
        }

        sealed class StubBehavior : IRHAH_PawnBehavior
        {
            public bool? AllowsResult;

            public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
            {
                return AllowsResult;
            }

            public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                return null;
            }
        }
    }
}
