using HungerAndHavoc.Identity;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PlagueTests
    {
        [Fact]
        public void SpreadDay_RunsEveryThirdDayOnce()
        {
            Assert.False(RHAH_Plague.IsSpreadDay(1, -1));
            Assert.False(RHAH_Plague.IsSpreadDay(2, -1));
            Assert.True(RHAH_Plague.IsSpreadDay(3, -1));
            Assert.False(RHAH_Plague.IsSpreadDay(3, 3));
            Assert.True(RHAH_Plague.IsSpreadDay(6, 3));
        }

        [Fact]
        public void SpreadChance_CapsAtThirtyPercent()
        {
            Assert.Equal(0f, RHAH_Plague.SpreadChance(0));
            Assert.Equal(0.015f, RHAH_Plague.SpreadChance(3));
            Assert.Equal(0.30f, RHAH_Plague.SpreadChance(80));
        }

        [Fact]
        public void BloodPumping_AtLeast120Skips()
        {
            Assert.False(RHAH_Plague.SkipsBloodPumping(119.9f));
            Assert.True(RHAH_Plague.SkipsBloodPumping(120f));
        }


    }
}
