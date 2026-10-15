using System.Collections.Generic;
using HungerAndHavoc.Generation;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_ApparelPolicyTests
    {
        const int Neolithic = 2;
        const int Medieval = 3;
        const int Industrial = 4;
        const string RatkinPack = "Solaris.RatkinRaceMod";

        [Fact]
        public void DefaultPoolKeepsOnlyVanillaTribalAndListedRatkinClothes()
        {
            Assert.True(Allows("Apparel_TribalA", "Ludeon.RimWorld", true, Neolithic));
            Assert.True(Allows("RK_ApronSkirt", RatkinPack, false, Medieval));
            Assert.True(Allows("RK_Cardigan", RatkinPack, false, Medieval));
            Assert.False(Allows("Apparel_BasicShirt", "Ludeon.RimWorld", true, Medieval));
            Assert.False(Allows("Apparel_ShieldBelt", "Ludeon.RimWorld", true, Medieval));
            Assert.False(Allows("RK_NewDress", RatkinPack, false, Medieval));
            Assert.False(Allows("RK_ApronSkirt", "Some.OtherMod", false, Medieval));
            Assert.False(Allows("RK_ApronSkirt", RatkinPack, false, Industrial));
            Assert.False(Allows("OA_RK_Windbreaker_A", "Some.OtherMod", false, Medieval));
            Assert.False(Allows("Foreign_Sack", "Some.OtherMod", false, Medieval));
            Assert.False(Allows(null, "Ludeon.RimWorld", true, Neolithic));
        }

        [Fact]
        public void YoungClothesNeedAPredefinedNameAndOfficialSource()
        {
            Assert.True(Allows("Apparel_BabyOnesie", "ludeon.rimworld", true, Medieval));
            Assert.True(Allows("Apparel_KidTribal", "Ludeon.RimWorld.Biotech", true, Neolithic));
            Assert.True(Allows("Apparel_WarmerHat", "Ludeon.RimWorld.Biotech", true, Medieval));
            Assert.True(Allows("Apparel_SunHat", "Ludeon.RimWorld.Biotech", true, Medieval));
            Assert.False(Allows("Apparel_BabyOnesie", "Some.OtherMod", false, Medieval));
            Assert.False(Allows("Apparel_KidTribal", "Some.OtherMod", true, Medieval));
            Assert.False(Allows("Apparel_TribalA", "Ludeon.RimWorld", false, Neolithic));
        }

        [Fact]
        public void DisabledClothingAndNonclothingStayOutOfThePool()
        {
            Assert.False(Allows("Apparel_TribalA", "Ludeon.RimWorld", true, Neolithic, true, false));
            Assert.False(Allows("Apparel_TribalA", "Ludeon.RimWorld", true, Neolithic, false));
        }

        [Fact]
        public void YoungClothesStayInTheirAgePool()
        {
            Assert.True(RHAH_ApparelPolicy.FitsStage("Apparel_BabyOnesie", 0));
            Assert.False(RHAH_ApparelPolicy.FitsStage("Apparel_KidTribal", 0));
            Assert.True(RHAH_ApparelPolicy.FitsStage("Apparel_KidTribal", 1));
            Assert.False(RHAH_ApparelPolicy.FitsStage("Apparel_BabyOnesie", 1));
            Assert.False(RHAH_ApparelPolicy.FitsStage("Apparel_KidTribal", 2));
            Assert.True(RHAH_ApparelPolicy.FitsStage("Apparel_TribalA", 2));
        }

        [Fact]
        public void QualityStaysAtNormalOrWorseAndClothIsWornOut()
        {
            Assert.Equal(0, RHAH_ApparelPolicy.Quality(0f));
            Assert.Equal(0, RHAH_ApparelPolicy.Quality(0.349f));
            Assert.Equal(1, RHAH_ApparelPolicy.Quality(0.35f));
            Assert.Equal(1, RHAH_ApparelPolicy.Quality(0.849f));
            Assert.Equal(2, RHAH_ApparelPolicy.Quality(0.85f));
            Assert.Equal(2, RHAH_ApparelPolicy.Quality(1f));
            Assert.Equal(10, RHAH_ApparelPolicy.HitPoints(100, 0.10f));
            Assert.Equal(10, RHAH_ApparelPolicy.HitPoints(100, 0f));
            Assert.Equal(1, RHAH_ApparelPolicy.PieceCount(2, 0f));
            Assert.Equal(4, RHAH_ApparelPolicy.PieceCount(2, 1f));
        }

        [Fact]
        public void StuffPrefersClothThenHumanLeather()
        {
            List<string> both = new List<string> { "Cloth", "Humanleather", "Steel" };
            Assert.Equal("Cloth", RHAH_ApparelPolicy.PreferredStuff(both, 0f));
            Assert.Equal("Humanleather", RHAH_ApparelPolicy.PreferredStuff(both, 0.65f));
            Assert.Equal("Cloth", RHAH_ApparelPolicy.PreferredStuff(new List<string> { "Cloth", "Steel" }, 1f));
            Assert.Null(RHAH_ApparelPolicy.PreferredStuff(new List<string> { "Steel" }, 0f));
        }

        static bool Allows(string defName, string packageId, bool official, int techLevel, bool clothing = true, bool enabled = true)
        {
            return RHAH_ApparelPolicy.AllowsApparel(defName, true, true, clothing, packageId, official, techLevel, enabled);
        }
    }
}
