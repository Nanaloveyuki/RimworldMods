using HungerAndHavoc.Incidents;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_IncidentPaceTests
    {
        [Fact]
        public void DefaultFormulaKeepsTheAverageDays()
        {
            var context = new RHAH_IncidentPaceContext(12f, 15f, -40, 3);

            Assert.Equal(15f, RHAH_IncidentPace.Resolve(null, 15f, context));
            Assert.Equal(15f, RHAH_IncidentPace.Resolve("   ", 15f, context));
            Assert.Equal(15f, RHAH_IncidentSchedule.PoolDays("averageDays", 15f, 12f, -40, 3));
        }

        [Fact]
        public void DayTrustAndSeasonChangeOnlyThatDaysInterval()
        {
            Assert.Equal(20f, RHAH_IncidentSchedule.PoolDays("day + 8", 15f, 12f, 0, 0));
            Assert.Equal(11.25f, RHAH_IncidentSchedule.PoolDays("clamp(averageDays * (1 + trust / 400.0), 0, 60)", 15f, 4f, -100, 2), 3);
            Assert.Equal(18f, RHAH_IncidentSchedule.PoolDays("averageDays + season", 15f, 4f, 0, 3));
            Assert.Equal(0f, RHAH_IncidentSchedule.PoolDays("min(day - 30, 0)", 15f, 10f, 0, 0));
            Assert.Equal(60f, RHAH_IncidentSchedule.PoolDays("abs(day) * 9", 15f, 10f, 0, 0));
        }

        [Fact]
        public void BrokenFormulaFallsBackWithoutLeavingTheMtbRange()
        {
            var context = new RHAH_IncidentPaceContext(3f, 15f, 0, 0);

            Assert.False(RHAH_IncidentPace.TryEvaluate("day / 0", context, out _));
            Assert.False(RHAH_IncidentPace.TryEvaluate("pawn", context, out _));
            Assert.False(RHAH_IncidentPace.TryEvaluate("clamp(day, 10, 1)", context, out _));
            Assert.Equal(RHAH_IncidentPace.DefaultFormula, RHAH_IncidentPace.Normalize("day / (trust - trust)"));
            Assert.Equal(15f, RHAH_IncidentSchedule.PoolDays("1 / 0", 15f, 3f, 0, 0));
            Assert.Equal(15f, RHAH_IncidentSchedule.PoolDays(new string('+', 161), 15f, 3f, 0, 0));
            Assert.Equal("max(day, averageDays)", RHAH_IncidentPace.Normalize(" max(day, averageDays) "));
        }
    }
}
