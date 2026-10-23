using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Generation;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_BirthCompletionTests
    {
        static readonly BindingFlags Methods = BindingFlags.Static | BindingFlags.NonPublic;

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExtraBirthRunsOnlyAfterItsLaborWasRemoved(bool removed)
        {
            Pawn_HealthTracker health = Health();
            Hediff labor = new Hediff_LaborPushing { pawn = health.hediffSet.pawn };
            health.hediffSet.hediffs.Add(labor);
            object scope = Enter(labor);
            int births = 0;
            RHAH_BirthCompletion.CompleteAfterRemoval(labor.pawn, () => births++);
            Assert.Equal(0, births);
            if (removed) health.hediffSet.hediffs.Remove(labor);
            Exception error = new InvalidOperationException("original removal failure");
            Assert.Same(error, Exit(health, scope, error));
            Assert.Equal(removed ? 1 : 0, births);
            RHAH_BirthCompletion.CompleteAfterRemoval(labor.pawn, () => births++);
            Assert.Equal(removed ? 2 : 1, births);
        }

        [Fact]
        public void NestedCommittedBirthSurvivesOuterRemovalFailureWithoutRepeatingOuterBirth()
        {
            Pawn_HealthTracker outer = Health();
            Pawn_HealthTracker inner = Health();
            Hediff first = new Hediff_LaborPushing { pawn = outer.hediffSet.pawn };
            Hediff second = new Hediff_LaborPushing { pawn = inner.hediffSet.pawn };
            outer.hediffSet.hediffs.Add(first);
            inner.hediffSet.hediffs.Add(second);
            object firstScope = Enter(first);
            List<string> births = new List<string>();
            RHAH_BirthCompletion.CompleteAfterRemoval(first.pawn, () => births.Add("outer"));
            object secondScope = Enter(second);
            RHAH_BirthCompletion.CompleteAfterRemoval(second.pawn, () => births.Add("inner"));
            inner.hediffSet.hediffs.Remove(second);
            Assert.Null(Exit(inner, secondScope, null));
            Assert.Empty(births);
            Exception failure = new InvalidOperationException("outer birth failed before removal");
            Assert.Same(failure, Exit(outer, firstScope, failure));
            Assert.Equal(new[] { "inner" }, births);
        }

        static Pawn_HealthTracker Health()
        {
            Verse.Pawn pawn = (Verse.Pawn)FormatterServices.GetUninitializedObject(typeof(Verse.Pawn));
            Pawn_HealthTracker health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
            health.hediffSet = new HediffSet(pawn);
            pawn.health = health;
            return health;
        }

        static object Enter(Hediff labor)
        {
            object[] args = { labor, null };
            typeof(RHAH_BirthCompletion).GetMethod("Prefix", Methods).Invoke(null, args);
            return args[1];
        }

        static Exception Exit(Pawn_HealthTracker health, object scope, Exception error)
        {
            return (Exception)typeof(RHAH_BirthCompletion).GetMethod("Finalizer", Methods)
                .Invoke(null, new[] { health, scope, error });
        }
    }
}
