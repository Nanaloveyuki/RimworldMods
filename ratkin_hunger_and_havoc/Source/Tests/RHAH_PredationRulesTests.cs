using RimWorld;
using HungerAndHavoc.Incidents;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PredationRulesTests
    {
        [Fact]
        public void ChanceZeroNeverSpawnsAndFullAlwaysDoes()
        {
            Assert.False(RHAH_PredationRules.Rolls(0f, true, false));
            Assert.False(RHAH_PredationRules.Selected(0f, 0f));
            Assert.True(RHAH_PredationRules.Rolls(100f, true, false));
            Assert.True(RHAH_PredationRules.Selected(100f, 0.999f));
            Assert.False(RHAH_PredationRules.Rolls(10f, false, false));
            Assert.False(RHAH_PredationRules.Rolls(10f, true, true));
        }

        [Fact]
        public void CampWeaponsStayOnTheVanillaList()
        {
            Assert.True(RHAH_PredationRules.VanillaWeapon("MeleeWeapon_Club", true, true, 2, false, true));
            Assert.True(RHAH_PredationRules.VanillaWeapon("MeleeWeapon_Spear", true, true, 2, false, true));
            Assert.True(RHAH_PredationRules.VanillaWeapon("Bow_Short", true, false, 2, true, true));
            Assert.False(RHAH_PredationRules.VanillaWeapon("MeleeWeapon_Knife", true, true, 2, false, false));
            Assert.False(RHAH_PredationRules.VanillaWeapon("MeleeWeapon_Mace", true, true, 3, false, true));
            Assert.False(RHAH_PredationRules.VanillaWeapon("Bow_Recurve", true, false, 2, false, true));
            Assert.False(RHAH_PredationRules.VanillaWeapon("ModClub", false, true, 2, false, true));
            Assert.False(RHAH_PredationRules.VanillaWeapon("ModClub", true, true, 2, false, true));
        }

        [Fact]
        public void JoinedRatkinAndHomeCorpsesAreNotPrey()
        {
            Assert.True(RHAH_PredationRules.CampPrey(true, true, false, false, false));
            Assert.False(RHAH_PredationRules.CampPrey(true, true, true, false, false));
            Assert.False(RHAH_PredationRules.CampPrey(true, false, false, false, false));
            Assert.False(RHAH_PredationRules.CampPrey(true, true, false, true, true));
            Assert.True(RHAH_PredationRules.CampPrey(true, true, false, true, false));
        }

        [Fact]
        public void IdleMapsDoNotScanAndActiveHuntersWaitTwoHours()
        {
            int interval = RHAH_PredationRules.SearchIntervalTicks;
            Assert.Equal(GenDate.TicksPerHour * 2, interval);
            Assert.False(RHAH_PredationRules.ShouldScan(0, 1000, 0));
            Assert.False(RHAH_PredationRules.ShouldScan(1, interval - 1, interval));
            Assert.True(RHAH_PredationRules.ShouldScan(1, interval, interval));

            RHAH_PredationDecision waiting = RHAH_PredationRules.Decide(true, false, true, false, 100, interval, true, false, 0, -1);
            RHAH_PredationDecision hunting = RHAH_PredationRules.Decide(true, false, true, false, interval, interval, true, false, 2, -1);
            RHAH_PredationDecision leaving = RHAH_PredationRules.Decide(true, false, true, false, interval, interval, false, false, -1, -1);
            RHAH_PredationDecision vanilla = RHAH_PredationRules.Decide(true, true, true, false, interval, interval, false, false, -1, -1);

            Assert.Equal(RHAH_PredationAction.Wait, waiting.Action);
            Assert.Equal(interval, waiting.NextSearchTick);
            Assert.Equal(RHAH_PredationAction.Hunt, hunting.Action);
            Assert.Equal(interval * 2, hunting.NextSearchTick);
            Assert.Equal(RHAH_PredationAction.Leave, leaving.Action);
            Assert.Equal(RHAH_PredationAction.Vanilla, vanilla.Action);
            Assert.False(RHAH_PredationRules.FightsBack(true, false));
            Assert.True(RHAH_PredationRules.FightsBack(true, true));
        }
}
}
