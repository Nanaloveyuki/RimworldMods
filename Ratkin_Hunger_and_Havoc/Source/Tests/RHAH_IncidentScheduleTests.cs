using System;
using HungerAndHavoc.Incidents;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_IncidentScheduleTests
    {
        [Fact]
        public void DaysClampToTheFrequencyRange()
        {
            Assert.Equal(15f, RHAH_IncidentSchedule.ClampDays(float.NaN));
            Assert.Equal(15f, RHAH_IncidentSchedule.ClampDays(-1f));
            Assert.Equal(0f, RHAH_IncidentSchedule.ClampDays(0f));
            Assert.Equal(60f, RHAH_IncidentSchedule.ClampDays(90f));
        }

        [Fact]
        public void OccurrenceChanceIsThePoolCheckNotAnIncidentShare()
        {
            Assert.Equal(0f, RHAH_IncidentSchedule.OccurrenceChance(0f));
            Assert.Equal(1000f / (15f * 60000f), RHAH_IncidentSchedule.OccurrenceChance(15f), 6);
            Assert.True(RHAH_IncidentSchedule.OccurrenceChance(15f) > RHAH_IncidentSchedule.OccurrenceChance(60f));
            Assert.Equal(1000f / (60f * 60000f), RHAH_IncidentSchedule.OccurrenceChance(90f), 6);
        }

        [Fact]
        public void DailyChanceCompoundsSixtyChecks()
        {
            Assert.Equal(0f, RHAH_IncidentSchedule.DailyOccurrenceChance(0f));
            float fifteen = 1f - (float)Math.Pow(1d - 1000f / (15f * 60000f), 60);
            Assert.Equal(fifteen, RHAH_IncidentSchedule.DailyOccurrenceChance(15f), 6);
            Assert.True(RHAH_IncidentSchedule.DailyOccurrenceChance(15f) > RHAH_IncidentSchedule.DailyOccurrenceChance(60f));
        }

        [Fact]
        public void WindowDaysStayBetweenFiveAndOneYear()
        {
            Assert.Equal(5f, RHAH_IncidentSchedule.ClampWindowDays(float.NaN));
            Assert.Equal(5f, RHAH_IncidentSchedule.ClampWindowDays(1f));
            Assert.Equal(30f, RHAH_IncidentSchedule.ClampWindowDays(30f));
            Assert.Equal(90f, RHAH_IncidentSchedule.ClampWindowDays(90f));
            Assert.Equal(360f, RHAH_IncidentSchedule.ClampWindowDays(400f));
        }

        [Fact]
        public void HoverMapsTheGraphWidthOntoCalendarDays()
        {
            var graph = new UnityEngine.Rect(10f, 20f, 100f, 80f);
            Assert.Equal(0f, HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.DayAt(graph, 10f, 30f));
            Assert.Equal(15f, HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.DayAt(graph, 60f, 30f));
            Assert.Equal(30f, HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.DayAt(graph, 200f, 30f));
            Assert.Equal(5f, HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.DayAt(graph, 200f, 1f));
            Assert.Equal(0f, HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.DayAt(new UnityEngine.Rect(0f, 0f, 0f, 10f), 4f, 30f));
        }

        [Fact]
        public void MapPoolCannotSelectCaravanIncidents()
        {
            string selected = RHAH_IncidentSchedule.Select(
                RHAH_AttitudePool.Negative,
                trust: 0,
                RHAH_IncidentSeason.Summer,
                mapHome: true,
                playerCaravan: false,
                roll: 0f);

            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(selected);
            Assert.NotNull(entry);
            Assert.Equal(RHAH_IncidentTarget.Map, entry.Target);
            Assert.Equal(RHAH_AttitudePool.Negative, entry.DefaultAttitudePool);
        }

        [Fact]
        public void CaravanPoolSelectsOnlyCaravanIncidents()
        {
            float total = RHAH_IncidentSchedule.TotalWeight(
                RHAH_AttitudePool.Negative,
                0,
                RHAH_IncidentSeason.Summer,
                mapHome: false,
                playerCaravan: true);
            string selected = RHAH_IncidentSchedule.Select(
                RHAH_AttitudePool.Negative,
                0,
                RHAH_IncidentSeason.Summer,
                mapHome: false,
                playerCaravan: true,
                total - 0.001f);

            Assert.Contains(selected, new[] { "I-035", "I-050" });
        }

        [Fact]
        public void PositivePoolStaysOnAidIntelAndPositiveOriginals()
        {
            string selected = RHAH_IncidentSchedule.Select(
                RHAH_AttitudePool.Positive,
                trust: -100,
                RHAH_IncidentSeason.Winter,
                mapHome: true,
                playerCaravan: false,
                roll: 0f);

            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(selected);
            Assert.Equal(RHAH_AttitudePool.Positive, entry.DefaultAttitudePool);
            Assert.NotEqual(RHAH_IncidentCategory.Plague, entry.Category);
        }

        [Fact]
        public void HighestTrustStillLeavesNegativeWeight()
        {
            float hostile = RHAH_IncidentSchedule.TotalWeight(
                RHAH_AttitudePool.Negative,
                trust: 100,
                RHAH_IncidentSeason.Fall,
                mapHome: true,
                playerCaravan: false);
            float friendly = RHAH_IncidentSchedule.TotalWeight(
                RHAH_AttitudePool.Negative,
                trust: -100,
                RHAH_IncidentSeason.Fall,
                mapHome: true,
                playerCaravan: false);

            Assert.True(hostile > 0f);
            Assert.True(friendly > hostile);
        }

        [Fact]
        public void SavedTargetsKeepTheirOriginalMapOrCaravan()
        {
            Assert.Equal(12, RHAH_IncidentSchedule.TargetId(caravan: false, 12));
            Assert.Equal(-7, RHAH_IncidentSchedule.TargetId(caravan: true, 7));
            Assert.Equal(0, RHAH_IncidentSchedule.TargetId(caravan: true, 0));
            Assert.Equal(12, RHAH_IncidentSchedule.DecodeTargetId(12, RHAH_IncidentTarget.Map));
            Assert.Equal(7, RHAH_IncidentSchedule.DecodeTargetId(-7, RHAH_IncidentTarget.Caravan));
            Assert.Equal(0, RHAH_IncidentSchedule.DecodeTargetId(12, RHAH_IncidentTarget.Caravan));
            Assert.Equal(0, RHAH_IncidentSchedule.DecodeTargetId(-7, RHAH_IncidentTarget.Map));
            Assert.Equal(int.MinValue, RHAH_IncidentSchedule.TargetId(caravan: false, 0));
            Assert.NotEqual(RHAH_IncidentSchedule.UnspecifiedTargetId, RHAH_IncidentSchedule.MapZeroTargetId);
            Assert.Equal(0, RHAH_IncidentSchedule.DecodeTargetId(int.MinValue, RHAH_IncidentTarget.Map));
            Assert.Equal(0, RHAH_IncidentSchedule.DecodeTargetId(int.MinValue, RHAH_IncidentTarget.Caravan));
        }

        [Fact]
        public void UnavailableHeadDoesNotBlockAReadyLaterTarget()
        {
            bool[] terminal = { false, true, false };
            bool[] ready = { false, false, true };

            Assert.Equal(1, RHAH_IncidentSchedule.NextExecutable(terminal, ready));
            Assert.Equal(2, RHAH_IncidentSchedule.NextExecutable(new[] { false, false, false }, ready));
            Assert.Equal(-1, RHAH_IncidentSchedule.NextExecutable(new[] { false }, new[] { false }));
        }

        [Fact]
        public void ExtraRequestScenariosDoNotMultiplyTheirClassShare()
        {
            var previous = HungerAndHavoc.Core.RHAH_Mod.Settings;
            try
            {
                var settings = new HungerAndHavoc.Core.RHAH_Settings();
                HungerAndHavoc.Core.RHAH_Mod.Settings = settings;
                foreach (var entry in RHAH_IncidentCatalog.All)
                {
                    settings.SetIncidentEnabled(entry.DisplayId, false);
                }
                settings.SetIncidentEnabled("I-015", true);
                Assert.Equal(settings.weightAid, RHAH_IncidentSchedule.TotalWeight(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false), 6);
                Assert.Equal("I-015", RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false, 0f));
                Assert.Equal("I-018", RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false, settings.weightAid - 0.0001f));
                Assert.Null(RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false, settings.weightAid));
            }
            finally
            {
                HungerAndHavoc.Core.RHAH_Mod.Settings = previous;
            }
        }

        [Fact]
        public void RetiredBabyRequestCannotEnterNewSelectionEvenWhenEnabled()
        {
            var previous = HungerAndHavoc.Core.RHAH_Mod.Settings;
            try
            {
                var settings = new HungerAndHavoc.Core.RHAH_Settings();
                HungerAndHavoc.Core.RHAH_Mod.Settings = settings;
                foreach (var entry in RHAH_IncidentCatalog.All)
                {
                    settings.SetIncidentEnabled(entry.DisplayId, entry.DisplayId == "I-019");
                }
                Assert.Equal(0f, RHAH_IncidentSchedule.TotalWeight(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false));
                Assert.Null(RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false, 0f));
            }
            finally
            {
                HungerAndHavoc.Core.RHAH_Mod.Settings = previous;
            }
        }

        [Fact]
        public void BroadcastGivesEachClassOneShareBeforeChoosingItsScenario()
        {
            var candidates = new[] { "I-001", "I-005", "I-011" };
            Assert.Equal("I-001", RHAH_BroadcastRules.Pick(candidates, 0f));
            Assert.Equal("I-005", RHAH_BroadcastRules.Pick(candidates, 0.5f));
            Assert.Equal("I-011", RHAH_BroadcastRules.Pick(candidates, 1f));
            Assert.Equal("I-011", RHAH_BroadcastRules.Pick(candidates, 1.999f));
            Assert.Null(RHAH_BroadcastRules.Pick(candidates, 2f));
        }

        [Fact]
        public void PlagueRefugeesStayInTheRefugeeClassPool()
        {
            var previous = HungerAndHavoc.Core.RHAH_Mod.Settings;
            try
            {
                var settings = new HungerAndHavoc.Core.RHAH_Settings();
                HungerAndHavoc.Core.RHAH_Mod.Settings = settings;
                foreach (var entry in RHAH_IncidentCatalog.All)
                {
                    settings.SetIncidentEnabled(entry.DisplayId, false);
                }
                settings.SetIncidentEnabled("I-040", true);
                Assert.Equal(0f, RHAH_IncidentSchedule.TotalWeight(RHAH_AttitudePool.Negative, 0,
                    RHAH_IncidentSeason.Summer, true, false));
                Assert.Equal(settings.weightBeggar * (1f + settings.weightPlague) / 2f,
                    RHAH_IncidentSchedule.TotalWeight(RHAH_AttitudePool.Positive, 0,
                        RHAH_IncidentSeason.Summer, true, false), 6);
                Assert.Equal("I-011", RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false, 0f));
                Assert.Equal("I-040", RHAH_IncidentSchedule.Select(RHAH_AttitudePool.Positive, 0,
                    RHAH_IncidentSeason.Summer, true, false, settings.weightBeggar * (1f + settings.weightPlague) / 2f - 0.0001f));
            }
            finally
            {
                HungerAndHavoc.Core.RHAH_Mod.Settings = previous;
            }
        }
    }
}
