using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_FamilyRelationTests
    {
        [Theory]
        [InlineData("I-003", RHAH_PawnRole.Mother, RHAH_PawnRole.RatkinYoung)]
        [InlineData("I-004", RHAH_PawnRole.BeggarMother, RHAH_PawnRole.BeggarChild)]
        public void ArrivingFamilyHasRealParentRelations(string displayId, RHAH_PawnRole motherRole, RHAH_PawnRole childRole)
        {
            RimWorldAssemblies.EnsureResolved();
            Game previousGame = Current.Game;
            bool previousBinding = DefOfHelperBinding;
            DefOfHelperBinding = true;
            PawnRelationDef previousParent = PawnRelationDefOf.Parent;
            try
            {
                PawnRelationDefOf.Parent = new PawnRelationDef { defName = "Parent" };
                Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                Current.Game.tickManager = new TickManager();
                Current.Game.World = (RimWorld.Planet.World)FormatterServices.GetUninitializedObject(typeof(RimWorld.Planet.World));
                Current.Game.World.worldPawns = new WorldPawns();
                Verse.Pawn mother = Member(1, displayId, motherRole, Gender.Female);
                Verse.Pawn first = Member(2, displayId, childRole, Gender.Male);
                Verse.Pawn second = Member(3, displayId, childRole, Gender.Female);
                List<Verse.Pawn> family = new List<Verse.Pawn> { mother, first, second };
                MethodInfo link = typeof(RHAH_IncidentFacts).GetMethod("LinkFamily", BindingFlags.Static | BindingFlags.NonPublic);
                link.Invoke(null, new object[] { displayId, family });
                link.Invoke(null, new object[] { displayId, family });
                Assert.True(first.relations.DirectRelationExists(PawnRelationDefOf.Parent, mother));
                Assert.True(second.relations.DirectRelationExists(PawnRelationDefOf.Parent, mother));
                Assert.Single(first.relations.DirectRelations);
                Assert.Single(second.relations.DirectRelations);
                Assert.Equal(new[] { 2, 3 }, CompRHAH_Pawn.TryGet(mother).State.childPawnLoadIds);
            }
            finally
            {
                Current.Game = previousGame;
                PawnRelationDefOf.Parent = previousParent;
                DefOfHelperBinding = previousBinding;
            }
        }

        [Fact]
        public void HistoricalFamilyRestoresOnlyExplicitSavedParentAndKeepsExistingMother()
        {
            RimWorldAssemblies.EnsureResolved();
            Game previousGame = Current.Game;
            bool previousBinding = DefOfHelperBinding;
            DefOfHelperBinding = true;
            PawnRelationDef previousParent = PawnRelationDefOf.Parent;
            try
            {
                PawnRelationDefOf.Parent = new PawnRelationDef { defName = "Parent" };
                Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
                Current.Game.tickManager = new TickManager();
                Current.Game.World = (World)FormatterServices.GetUninitializedObject(typeof(World));
                Current.Game.World.worldPawns = new WorldPawns();
                Verse.Pawn mother = Member(1, "I-003", RHAH_PawnRole.Mother, Gender.Female);
                Verse.Pawn child = Member(2, "I-003", RHAH_PawnRole.RatkinYoung, Gender.Male);
                Verse.Pawn unrelated = Member(3, "I-003", RHAH_PawnRole.RatkinYoung, Gender.Female);
                Verse.Pawn otherChild = Member(4, "I-003", RHAH_PawnRole.RatkinYoung, Gender.Male);
                Verse.Pawn existingMother = Member(5, "I-004", RHAH_PawnRole.BeggarMother, Gender.Female);
                CompRHAH_Pawn.TryGet(child).State.parentPawnLoadId = mother.thingIDNumber;
                CompRHAH_Pawn.TryGet(otherChild).State.parentPawnLoadId = mother.thingIDNumber;
                CompRHAH_Pawn.TryGet(mother).State.childPawnLoadIds.AddRange(new[] { 2, 4 });
                otherChild.relations.AddDirectRelation(PawnRelationDefOf.Parent, existingMother);
                List<Verse.Pawn> pawns = new List<Verse.Pawn> { mother, child, unrelated, otherChild, existingMother };
                RHAH_IncidentFacts.RestoreFamilyRelations(pawns);
                RHAH_IncidentFacts.RestoreFamilyRelations(pawns);
                Assert.True(child.relations.DirectRelationExists(PawnRelationDefOf.Parent, mother));
                Assert.Single(child.relations.DirectRelations);
                Assert.Empty(unrelated.relations.DirectRelations);
                Assert.True(otherChild.relations.DirectRelationExists(PawnRelationDefOf.Parent, existingMother));
                Assert.False(otherChild.relations.DirectRelationExists(PawnRelationDefOf.Parent, mother));
            }
            finally
            {
                Current.Game = previousGame;
                PawnRelationDefOf.Parent = previousParent;
                DefOfHelperBinding = previousBinding;
            }
        }

        static bool DefOfHelperBinding
        {
            get => (bool)typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            set => typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }

        static Verse.Pawn Member(int id, string displayId, RHAH_PawnRole role, Gender gender)
        {
            Verse.Pawn pawn = new Verse.Pawn
            {
                thingIDNumber = id, gender = gender,
                def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef))
            };
            pawn.relations = new Pawn_RelationsTracker(pawn);
            pawn.health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
            pawn.health.hediffSet = new HediffSet(pawn);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn };
            CompRHAH_Pawn comp = new CompRHAH_Pawn { parent = mark };
            mark.comps = new List<HediffComp> { comp };
            pawn.health.hediffSet.hediffs.Add(mark);
            comp.ApplySeed(new RHAH_PawnSeed(displayId, 7, 7, role));
            return pawn;
        }
    }
}
