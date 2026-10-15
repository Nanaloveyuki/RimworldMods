using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using RimWorld;
using Verse;
using HungerAndHavoc.Generation;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_FertilityRulesTests
    {
        [Fact]
        public void LitterChancePeaksInTheMiddleAndDropsAtBothEnds()
        {
            Assert.Equal(0f, RHAH_FertilityRules.LitterChance(1, 2, 4, 6));
            Assert.Equal(1f / 3f, RHAH_FertilityRules.LitterChance(2, 2, 4, 6), 3);
            Assert.Equal(2f / 3f, RHAH_FertilityRules.LitterChance(3, 2, 4, 6), 3);
            Assert.Equal(1f, RHAH_FertilityRules.LitterChance(4, 2, 4, 6));
            Assert.Equal(2f / 3f, RHAH_FertilityRules.LitterChance(5, 2, 4, 6), 3);
            Assert.Equal(1f / 3f, RHAH_FertilityRules.LitterChance(6, 2, 4, 6), 3);
        }

        [Fact]
        public void LitterRollFollowsTheSuppliedChance()
        {
            Assert.Equal(4, RHAH_FertilityRules.RollLitter(2, 4, 6, () => 0.5f));
            Assert.Equal(2, RHAH_FertilityRules.RollLitter(2, 4, 6, () => 0f));
            Assert.Equal(6, RHAH_FertilityRules.RollLitter(2, 4, 6, () => 1f));
        }

        [Fact]
        public void LitterBoundsStayOrderedInsideTheAllowedRange()
        {
            int minimum = 0;
            int peak = 20;
            int maximum = 3;
            RHAH_FertilityRules.ClampLitter(ref minimum, ref peak, ref maximum);
            Assert.Equal(1, minimum);
            Assert.Equal(3, peak);
            Assert.Equal(3, maximum);
        }

        [Fact]
        public void FertileAgeStaysBetweenOneAndFourteen()
        {
            Assert.Equal(1f, RHAH_FertilityRules.ClampFertileAge(0f));
            Assert.Equal(14f, RHAH_FertilityRules.ClampFertileAge(40f));
            Assert.True(RHAH_FertilityRules.AgeAllows(1f, 1f));
            Assert.False(RHAH_FertilityRules.AgeAllows(0.9f, 1f));
        }

        [Fact]
        public void FertilityPercentConvertsToABoundedFactor()
        {
            Assert.Equal(1f, RHAH_FertilityRules.FertilityFactor(50f));
            Assert.Equal(3f, RHAH_FertilityRules.FertilityFactor(300f));
            Assert.Equal(10f, RHAH_FertilityRules.FertilityFactor(5000f));
        }

        [Fact]
        public void GestationStaysBetweenThreeDaysAndTheVanillaFloor()
        {
            Assert.Equal(3f, RHAH_FertilityRules.ClampGestationDays(1f));
            Assert.Equal(RHAH_FertilityRules.VanillaGestationFloorDays, RHAH_FertilityRules.ClampGestationDays(18f));
            Assert.Equal(4f, RHAH_FertilityRules.GestationDays(4f, 18f));
            Assert.Equal(5f, RHAH_FertilityRules.GestationDays(5.661f, 5f));
        }

        [Fact]
        public void LovinPregnancyChanceDefaultsToHalfAndStaysInsideZeroToOne()
        {
            Assert.Equal(0.5f, RHAH_FertilityRules.LovinPregnancyChance(50f));
            Assert.Equal(0f, RHAH_FertilityRules.LovinPregnancyChance(-10f));
            Assert.Equal(1f, RHAH_FertilityRules.LovinPregnancyChance(140f));
            Assert.Equal(50f, RHAH_FertilityRules.ClampLovinPregnancyPercent(float.NaN));
        }

        [Fact]
        public void RoomPartnerIgnoresMarriageAndRejectsTheOutdoors()
        {
            Assert.True(RHAH_FertilityRules.RoomPartner(true, true, false, true, false, false));
            Assert.False(RHAH_FertilityRules.RoomPartner(true, true, true, true, false, false));
            Assert.False(RHAH_FertilityRules.RoomPartner(true, false, false, true, false, false));
            Assert.False(RHAH_FertilityRules.RoomPartner(false, true, false, true, false, false));
            Assert.False(RHAH_FertilityRules.RoomPartner(true, true, false, false, false, false));
            Assert.False(RHAH_FertilityRules.RoomPartner(true, true, false, true, true, false));
            Assert.False(RHAH_FertilityRules.RoomPartner(true, true, false, true, false, true));
        }

        [Fact]
        public void RoomLovinWaitsUntilBothAreInTheSameBedAndCooledDown()
        {
            Assert.False(RHAH_FertilityRules.RoomLovinReady(100, 101, false, true, false));
            Assert.False(RHAH_FertilityRules.RoomLovinReady(100, 100, true, true, false));
            Assert.False(RHAH_FertilityRules.RoomLovinReady(100, 100, false, false, false));
            Assert.False(RHAH_FertilityRules.RoomLovinReady(100, 100, false, true, true));
            Assert.True(RHAH_FertilityRules.RoomLovinReady(100, 100, false, true, false));
            Assert.False(RHAH_FertilityRules.RoomLovinBed(false, false, true, 100, 100));
            Assert.False(RHAH_FertilityRules.RoomLovinBed(true, true, true, 100, 100));
            Assert.False(RHAH_FertilityRules.RoomLovinBed(true, false, false, 100, 100));
            Assert.False(RHAH_FertilityRules.RoomLovinBed(true, false, true, 101, 100));
            Assert.True(RHAH_FertilityRules.RoomLovinBed(true, false, true, 100, 100));
            Assert.Equal(1250, RHAH_FertilityRules.MinRoomLovinCooldownTicks);
        }

        [Fact]
        public void LitterRequestKeepsHeritableGenesAsEndogenes()
        {
            GeneDef ears = Gene("RK_Gene_LargeEars");
            GeneDef xenogene = Gene("RK_Gene_Implant");
            XenotypeDef ratkin = new XenotypeDef
            {
                defName = "RK_XenoType_Ratkin",
                inheritable = true,
                genes = new List<GeneDef> { ears }
            };
            PawnGenerationRequest request = new PawnGenerationRequest();
            request = RHAH_Fertility.AddEndogenes(request, Parent(ratkin, ears, xenogene));
            Assert.Contains(ears, request.ForcedEndogenes);
            Assert.DoesNotContain(xenogene, request.ForcedEndogenes);
            Assert.Null(request.ForcedXenogenes);
            Assert.True(RHAH_Fertility.LitterRequest(Parent(ratkin, ears, xenogene), null).ForceNoBackstory);
            Assert.Same(ratkin, RHAH_Fertility.InheritedXenotype(Parent(ratkin, ears, xenogene), null));
        }


        static GeneDef Gene(string defName)
        {
            return new GeneDef { defName = defName };
        }

        static Verse.Pawn Parent(XenotypeDef xenotype, GeneDef endogene, GeneDef xenogene)
        {
            Verse.Pawn pawn = (Verse.Pawn)FormatterServices.GetUninitializedObject(typeof(Verse.Pawn));
            Pawn_GeneTracker genes = (Pawn_GeneTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_GeneTracker));
            typeof(Pawn_GeneTracker).GetField("xenotype", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(genes, xenotype);
            typeof(Pawn_GeneTracker).GetField("endogenes", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(genes, new List<Gene> { GeneOf(endogene) });
            typeof(Pawn_GeneTracker).GetField("xenogenes", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(genes, new List<Gene> { GeneOf(xenogene) });
            pawn.genes = genes;
            pawn.kindDef = (PawnKindDef)FormatterServices.GetUninitializedObject(typeof(PawnKindDef));
            ThingDef race = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            race.race = new RaceProperties { lifeStageAges = new List<LifeStageAge>() };
            pawn.kindDef.race = race;
            return pawn;
        }

        static Gene GeneOf(GeneDef def)
        {
            Gene gene = (Gene)FormatterServices.GetUninitializedObject(typeof(Gene));
            gene.def = def;
            return gene;
        }
    }
}
