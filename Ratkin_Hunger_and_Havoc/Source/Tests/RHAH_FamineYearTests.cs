using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_FamineYearTests
    {
        [Fact]
        public void JudgmentRunsOnceAtTheYearBoundaryAndCatchesUpOnlyInitially()
        {
            Assert.False(GameComponent_RHAH_FamineYear.JudgmentDue(-1, 5500, 0, 5));
            Assert.True(GameComponent_RHAH_FamineYear.JudgmentDue(-1, 5500, 0, 6));
            Assert.True(GameComponent_RHAH_FamineYear.JudgmentDue(-1, 5500, 20, 3));
            Assert.False(GameComponent_RHAH_FamineYear.JudgmentDue(5500, 5500, 20, 6));
            Assert.False(GameComponent_RHAH_FamineYear.JudgmentDue(5500, 5501, 0, 5));
            Assert.True(GameComponent_RHAH_FamineYear.JudgmentDue(5500, 5501, 0, 6));
            Assert.False(GameComponent_RHAH_FamineYear.JudgmentDue(5500, 5501, 1, 6));
        }

        [Fact]
        public void RareEventLimitsUseExactCooldownBoundariesAndTheAnnualQuota()
        {
            int day = GenDate.TicksPerDay;
            Assert.False(GameComponent_RHAH_FamineYear.SwarmAvailable(false, 0, -1, 0));
            Assert.True(GameComponent_RHAH_FamineYear.SwarmAvailable(true, 0, -1, 0));
            Assert.False(GameComponent_RHAH_FamineYear.SwarmAvailable(true, 1, 100, 100 + 15 * day - 1));
            Assert.True(GameComponent_RHAH_FamineYear.SwarmAvailable(true, 1, 100, 100 + 15 * day));
            Assert.False(GameComponent_RHAH_FamineYear.SwarmAvailable(true, 2, 100, 100 + 60 * day));
            Assert.False(GameComponent_RHAH_FamineYear.PleadingAvailable(100, 100 + 180 * day - 1));
            Assert.True(GameComponent_RHAH_FamineYear.PleadingAvailable(100, 100 + 180 * day));
            Assert.True(GameComponent_RHAH_FamineYear.PleadingAvailable(-1, 0));
        }

        [Fact]
        public void FamineScalesGroupsButPreservesIndividualsFamiliesAndSwarmLimits()
        {
            Assert.Equal(9, RHAH_IncidentScale.MultiplyCount("I-012", 3, true, 3f));
            Assert.Equal(12, RHAH_IncidentScale.MultiplyCount("I-012", 3, true, 9f));
            Assert.Equal(3, RHAH_IncidentScale.MultiplyCount("I-012", 3, true, 0f));
            Assert.Equal(5, RHAH_IncidentScale.MultiplyCount("I-012", 3, true, 1.5f));
            Assert.Equal(3, RHAH_IncidentScale.MultiplyCount("I-012", 3, false, 3f));
            Assert.Equal(1, RHAH_IncidentScale.MultiplyCount("I-011", 1, true, 3f));
            Assert.Equal(3, RHAH_IncidentScale.MultiplyCount("I-004", 3, true, 3f));
            Assert.Equal(2, RHAH_IncidentScale.MultiplyCount("I-029", 2, true, 3f));
            Assert.Equal(50, RHAH_IncidentScale.MultiplyCount("I-052", 50, true, 3f));
        }
    }
}
