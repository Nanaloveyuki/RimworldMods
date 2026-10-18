using HungerAndHavoc.Pawn;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_ClaySatietyTests
    {
        const int Day = 60000;

        [Fact]
        public void Register_StopsAtThreeInsideFifteenDays()
        {
            ClayBite first = RHAH_ClaySatiety.Register(0, -1, 0, 0f, Day);
            ClayBite second = RHAH_ClaySatiety.Register(Day, 0, first.Count, first.Severity, Day);
            ClayBite third = RHAH_ClaySatiety.Register(Day * 2, 0, second.Count, second.Severity, Day);
            ClayBite fourth = RHAH_ClaySatiety.Register(Day * 3, 0, third.Count, third.Severity, Day);

            Assert.True(first.Accepted);
            Assert.Equal(1, first.Count);
            Assert.Equal(0.33f, first.Severity);
            Assert.True(third.Accepted);
            Assert.Equal(3, third.Count);
            Assert.Equal(0.99f, third.Severity, 3);
            Assert.False(fourth.Accepted);
            Assert.Equal(3, fourth.Count);
            Assert.Equal(third.Severity, fourth.Severity);
        }

        [Fact]
        public void Register_OpensANewWindowAfterFifteenDays()
        {
            ClayBite again = RHAH_ClaySatiety.Register(Day * 15, 0, 3, 0.99f, Day);

            Assert.True(again.Accepted);
            Assert.Equal(1, again.Count);
            Assert.Equal(1f, again.Severity);
        }

        [Fact]
        public void Decay_BottomsAtTheStoredMinimum()
        {
            Assert.Equal(0.9f, RHAH_ClaySatiety.Decay(1f, Day, Day));
            Assert.Equal(0.001f, RHAH_ClaySatiety.Decay(0.05f, Day, Day));
            Assert.True(RHAH_ClaySatiety.ShouldRemove(0.001f, 0, -1));
            Assert.False(RHAH_ClaySatiety.ShouldRemove(0.001f, 1, 0));
        }
    }
}
