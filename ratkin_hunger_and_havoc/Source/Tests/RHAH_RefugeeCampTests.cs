using System.Collections.Generic;
using HungerAndHavoc.Incidents;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_RefugeeCampTests
    {
        [Fact]
        public void CampKeepsTheOldHeadcountAndReward()
        {
            RHAH_CampPlan low = RHAH_RefugeeCampRules.Plan(0, 0);
            RHAH_CampPlan high = RHAH_RefugeeCampRules.Plan(9, 40);

            Assert.Equal(2, low.Adults);
            Assert.Equal(8, low.Children);
            Assert.Equal(4, high.Adults);
            Assert.Equal(16, high.Children);
            Assert.Equal(12, high.Goodwill);
            Assert.Equal(15, high.Days);
        }

        [Fact]
        public void QuestNeedsViolenceSponsorAndTile()
        {
            Assert.True(RHAH_RefugeeCampRules.CanOffer(true, true, true, true));
            Assert.False(RHAH_RefugeeCampRules.CanOffer(false, true, true, true));
            Assert.False(RHAH_RefugeeCampRules.CanOffer(true, false, true, true));
            Assert.False(RHAH_RefugeeCampRules.CanOffer(true, true, false, true));
            Assert.False(RHAH_RefugeeCampRules.CanOffer(true, true, true, false));
        }

        [Fact]
        public void OnlyDeadResidentsClearTheCamp()
        {
            Assert.False(RHAH_RefugeeCampRules.Cleared(new List<bool>()));
            Assert.False(RHAH_RefugeeCampRules.Cleared(new List<bool> { true, false }));
            Assert.True(RHAH_RefugeeCampRules.Cleared(new List<bool> { true, true }));
            Assert.False(RHAH_RefugeeCampRules.CountsAsDead(false, false, true, false, true));
            Assert.False(RHAH_RefugeeCampRules.CountsAsDead(false, false, false, true, true));
            Assert.True(RHAH_RefugeeCampRules.CountsAsDead(false, true, false, false, false));
        }

        [Fact]
        public void ResidentsOnlyCarryVanillaWoodenWeapons()
        {
            Assert.True(RHAH_RefugeeCampRules.AllowedWeapon("MeleeWeapon_Club", true, true, true, 2, false, true));
            Assert.True(RHAH_RefugeeCampRules.AllowedWeapon("Bow_Short", true, true, false, 2, true, true));
            Assert.False(RHAH_RefugeeCampRules.AllowedWeapon("MeleeWeapon_Mace", true, true, true, 3, false, true));
            Assert.False(RHAH_RefugeeCampRules.AllowedWeapon("MeleeWeapon_Club", false, true, true, 2, false, true));
            Assert.False(RHAH_RefugeeCampRules.AllowedWeapon("ModClub", true, true, true, 2, false, true));
            Assert.False(RHAH_RefugeeCampRules.AllowedWeapon("MeleeWeapon_Knife", true, true, true, 2, false, false));
        }
    }
}
