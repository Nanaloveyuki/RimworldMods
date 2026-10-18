using System.IO;
using System.Runtime.CompilerServices;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PawnGateOrderTests
    {
        static RHAH_PawnGateOrderTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        [Fact]
        public void NoMark_AllowsFalse()
        {
            RHAH_Api.Bind(new RHAH_ApiHost());
            try
            {
                Assert.False(RHAH_Api.Allows(null, RHAH_BehaviorGate.Beg));
                Assert.False(new RHAH_ApiHost().Allows(null, RHAH_BehaviorGate.JoinColony));
            }
            finally
            {
                RHAH_Api.Bind(null);
            }
        }

        [Fact]
        public void HostWiresOverrideThenBehaviorThenDefault()
        {
            string source = File.ReadAllText(HostPath());
            int allows = source.IndexOf("public bool Allows(");
            Assert.True(allows >= 0, "RHAH_ApiHost.Allows missing");
            string body = source.Substring(allows);
            int overrideIndex = body.IndexOf("GetGateOverride");
            int behaviorIndex = body.IndexOf("RHAH_PawnBehaviors.Query");
            int defaultIndex = body.IndexOf("RHAH_PawnDefaults.Allows");
            Assert.True(overrideIndex >= 0 && behaviorIndex > overrideIndex && defaultIndex > behaviorIndex);
        }

        [Fact]
        public void OverrideBeatsLaterBehaviorThenDefault()
        {
            RHAH_PawnBehaviors.ResetForTests();
            try
            {
                RHAH_PawnState state = new RHAH_PawnState();
                state.ApplySeed(new RHAH_PawnSeed(
                    sourceIncidentDisplayId: "I-005",
                    role: RHAH_PawnRole.Beggar,
                    lifecycle: RHAH_Lifecycle.SeekingFood,
                    attitudeAtArrival: RHAH_Attitude.Neutral));
                bool baseline = state.Allows(RHAH_BehaviorGate.Beg);
                Assert.Equal(
                    RHAH_PawnDefaults.Allows(state.ToSnapshot(), RHAH_BehaviorGate.Beg),
                    baseline);

                StubBehavior first = new StubBehavior { AllowsResult = !baseline };
                StubBehavior later = new StubBehavior { AllowsResult = baseline };
                StubBehavior skip = new StubBehavior { AllowsResult = null };
                RHAH_PawnBehaviors.Register(first);
                Assert.Equal(!baseline, state.Allows(RHAH_BehaviorGate.Beg));

                RHAH_PawnBehaviors.Register(later);
                Assert.Equal(baseline, state.Allows(RHAH_BehaviorGate.Beg));

                RHAH_PawnBehaviors.Register(skip);
                Assert.Equal(baseline, state.Allows(RHAH_BehaviorGate.Beg));

                state.SetGate(RHAH_BehaviorGate.Beg, !baseline);
                Assert.Equal(!baseline, state.Allows(RHAH_BehaviorGate.Beg));

                RHAH_PawnBehaviors.Unregister(later);
                RHAH_PawnBehaviors.Unregister(first);
                RHAH_PawnBehaviors.Unregister(skip);
                Assert.Equal(!baseline, state.Allows(RHAH_BehaviorGate.Beg));

                state.SetGate(RHAH_BehaviorGate.Beg, null);
                Assert.Equal(baseline, state.Allows(RHAH_BehaviorGate.Beg));
            }
            finally
            {
                RHAH_PawnBehaviors.ResetForTests();
            }
        }

        static string HostPath([CallerFilePath] string testFile = null)
        {
            string testsDir = Path.GetDirectoryName(testFile);
            return Path.GetFullPath(Path.Combine(testsDir, "..", "Identity", "RHAH_ApiHost.cs"));
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
