using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using RimWorld;
using Verse;
using Verse.Grammar;
using HungerAndHavoc.Generation;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_RatkinNameTests
    {
        [Fact]
        public void RaceNameMakerIsUsedAndMissingMakerLeavesTheName()
        {
            RulePackDef maker = new RulePackDef { defName = "NamerPerson_RatkinKingdom" };
            Assert.Same(maker, RHAH_RatkinName.Maker(Pawn(maker, Gender.Female)));
            Assert.Null(RHAH_RatkinName.Maker(Pawn(null, Gender.Male)));
            Assert.Null(RHAH_RatkinName.Maker(null));
        }

        [Fact]
        public void GeneratedRaceNameReplacesTheCurrentName()
        {
            RulePackDef maker = new RulePackDef { defName = "NamerPerson_RatkinKingdom" };
            Name current = NameTriple.FromString("John Smith", false);
            Name replaced = RHAH_RatkinName.Choose(current, maker, "Lily Nut");
            Assert.Equal("Lily", ((NameTriple)replaced).First);
            Assert.Equal("Nut", ((NameTriple)replaced).Last);
            Assert.Same(current, RHAH_RatkinName.Choose(current, null, "Lily Nut"));
            Assert.Same(current, RHAH_RatkinName.Choose(current, maker, null));
        }

        [Fact]
        public void VisitorRequestSkipsSolidBiosEvenWithAnExplicitBackstory()
        {
            PawnKindDef kind = (PawnKindDef)FormatterServices.GetUninitializedObject(typeof(PawnKindDef));
            ThingDef race = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            race.race = new RaceProperties { lifeStageAges = new List<LifeStageAge>() };
            kind.race = race;
            PawnGenerationRequest request = RHAH_GenerationOptimizer.BuildRequest(
                new RHAH_PawnRequest { PawnKind = kind, BiologicalAge = 20f },
                new RHAH_PawnProfile { UseExplicitBackstory = true, Childhood = new BackstoryDef() }, null);
            Assert.True(request.ForceNoBackstory);
        }

        static Verse.Pawn Pawn(RulePackDef maker, Gender gender)
        {
            Verse.Pawn pawn = (Verse.Pawn)FormatterServices.GetUninitializedObject(typeof(Verse.Pawn));
            pawn.gender = gender;
            ThingDef def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            RaceProperties race = (RaceProperties)FormatterServices.GetUninitializedObject(typeof(RaceProperties));
            typeof(RaceProperties).GetField("nameGenerator", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(race, maker);
            def.race = race;
            pawn.def = def;
            return pawn;
        }
    }
}
