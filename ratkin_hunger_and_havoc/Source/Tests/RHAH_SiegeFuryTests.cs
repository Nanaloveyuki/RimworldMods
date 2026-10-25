using HungerAndHavoc.Generation;
using HungerAndHavoc.Pawn;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_SiegeFuryTests
    {
        [Fact]
        public void SiegeFuryOnlyAppliesToVisitorSnapshotNonPlayerSiegeMembers()
        {
            Assert.True(RHAH_SiegeFuryGrant.CanApply(true, true, false, "I-014"));
            Assert.True(RHAH_SiegeFuryGrant.CanApply(true, true, false, "I-030"));
            Assert.True(RHAH_SiegeFuryGrant.CanApply(true, true, false, "I-045"));
            Assert.False(RHAH_SiegeFuryGrant.CanApply(true, true, false, "I-005"));
            Assert.False(RHAH_SiegeFuryGrant.CanApply(true, true, false, null));
            Assert.False(RHAH_SiegeFuryGrant.CanApply(true, true, true, "I-014"));
            Assert.False(RHAH_SiegeFuryGrant.CanApply(true, false, false, "I-014"));
            Assert.False(RHAH_SiegeFuryGrant.CanApply(false, true, false, "I-014"));
        }

        [Fact]
        public void SiegeFuryStageFollowsMalnutritionSeverity()
        {
            Assert.Equal(0.10f, HediffComp_RHAH_SiegeFuryStage.SeverityForStage(0f));
            Assert.Equal(0.10f, HediffComp_RHAH_SiegeFuryStage.SeverityForStage(0.249f));
            Assert.Equal(0.50f, HediffComp_RHAH_SiegeFuryStage.SeverityForStage(0.25f));
            Assert.Equal(0.50f, HediffComp_RHAH_SiegeFuryStage.SeverityForStage(0.499f));
            Assert.Equal(0.90f, HediffComp_RHAH_SiegeFuryStage.SeverityForStage(0.50f));
            Assert.Equal(0.90f, HediffComp_RHAH_SiegeFuryStage.SeverityForStage(1f));
            Assert.Equal(0, HediffComp_RHAH_SiegeFuryStage.StageOf(0.24f));
            Assert.Equal(1, HediffComp_RHAH_SiegeFuryStage.StageOf(0.25f));
            Assert.Equal(2, HediffComp_RHAH_SiegeFuryStage.StageOf(0.50f));
        }
    }
}
