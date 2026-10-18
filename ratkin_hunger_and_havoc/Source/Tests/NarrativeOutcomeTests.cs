using HungerAndHavoc.Narrative;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class NarrativeOutcomeTests
    {
        [Fact]
        public void FailureHasPriorityOverOtherFacts()
        {
            Assert.Equal(NarrativeOutcome.Failure, Calculate(new NarrativeSnapshot(10, 10, 5, 0, true)));
        }

        [Fact]
        public void LossWinsWhenLossesOutnumberRescues()
        {
            Assert.Equal(NarrativeOutcome.Massacre, Calculate(new NarrativeSnapshot(10, 10, 1, 2, false)));
        }

        [Fact]
        public void RevelationRequiresBothEvidenceAndTrust()
        {
            Assert.Equal(NarrativeOutcome.Unresolved, Calculate(new NarrativeSnapshot(9, 5, 0, 0, false)));
            Assert.Equal(NarrativeOutcome.Unresolved, Calculate(new NarrativeSnapshot(9, 5, 0, 0, false)));
        }

        [Fact]
        public void RescueIsStableAfterReloadableSnapshot()
        {
            NarrativeSnapshot snapshot = new NarrativeSnapshot(0, 0, 1, 0, false);
            Assert.Equal(NarrativeOutcome.Rescue, Calculate(snapshot));
            Assert.Equal(NarrativeOutcome.Rescue, Calculate(new NarrativeSnapshot(snapshot.RevealedCount, snapshot.Trust, snapshot.Rescued, snapshot.Lost, snapshot.Failed)));
        }

        static NarrativeOutcome Calculate(NarrativeSnapshot snapshot)
        {
            return NarrativeOutcomeCalculator.Calculate(snapshot);
        }
    }
}
