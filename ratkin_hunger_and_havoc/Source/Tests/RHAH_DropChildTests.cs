using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_DropChildTests
    {
        [Fact]
        public void ActualCarriedChildMatchesBothSidesOfOwnership()
        {
            Verse.Pawn mother = Mother();
            Verse.Pawn child = Child(mother);
            mother.carryTracker.innerContainer.InnerListForReading.Add(child);

            Assert.True(RHAH_FamilyRules.StillCarried(mother, child));
        }

        [Fact]
        public void ContainerEntryWithAnotherHolderIsNotAValidCarriedChild()
        {
            Verse.Pawn mother = Mother();
            Verse.Pawn child = Child(Mother());
            mother.carryTracker.innerContainer.InnerListForReading.Add(child);

            Assert.False(RHAH_FamilyRules.StillCarried(mother, child));
        }

        [Fact]
        public void MissingChildAndMissingCarryTrackerAreNotValidCarriedChildren()
        {
            Verse.Pawn mother = Mother();
            Verse.Pawn child = Child(mother);

            Assert.False(RHAH_FamilyRules.StillCarried(mother, null));
            mother.carryTracker = null;
            Assert.False(RHAH_FamilyRules.StillCarried(mother, child));
        }

        [Fact]
        public void StaleChildHolderWithEmptyCarryContainerDoesNotDropOrDetach()
        {
            Verse.Pawn mother = Mother();
            Verse.Pawn child = Child(mother);
            Assert.False(RHAH_FamilyRules.StillCarried(mother, child));

            RunDropToil(mother, child);

            Assert.Null(mother.carryTracker.CarriedThing);
            Assert.Same(mother, child.CarriedBy);
            Assert.Same(child, child.GetLord().ownedPawns[0]);
            Assert.Empty(CompRHAH_Pawn.TryGet(mother).State.droppedChildLoadIds);
        }

        [Fact]
        public void StaleChildHolderDoesNotDropAnotherCarriedPawn()
        {
            Verse.Pawn mother = Mother();
            Verse.Pawn child = Child(mother);
            Verse.Pawn other = Child(mother);
            mother.carryTracker.innerContainer.InnerListForReading.Add(other);
            Assert.False(RHAH_FamilyRules.StillCarried(mother, child));

            RunDropToil(mother, child);

            Assert.Same(other, mother.carryTracker.CarriedThing);
            Assert.Same(mother, other.CarriedBy);
            Assert.Same(child, child.GetLord().ownedPawns[0]);
            Assert.Empty(CompRHAH_Pawn.TryGet(mother).State.droppedChildLoadIds);
        }

        static void RunDropToil(Verse.Pawn mother, Verse.Pawn child)
        {
            Job job = Uninitialized<Job>();
            job.targetA = child;
            JobDriver_RHAH_DropChild driver = new JobDriver_RHAH_DropChild { pawn = mother, job = job };
            IEnumerable<Toil> toils = (IEnumerable<Toil>)typeof(JobDriver_RHAH_DropChild)
                .GetMethod("MakeNewToils", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(driver, null);
            using (IEnumerator<Toil> enumerator = toils.GetEnumerator())
            {
                Assert.True(enumerator.MoveNext());
                Toil toil = enumerator.Current;
                try
                {
                    toil.initAction();
                }
                finally
                {
                    toil.ReturnToPool();
                }
            }
        }

        static Verse.Pawn Mother()
        {
            Verse.Pawn mother = Uninitialized<Verse.Pawn>();
            mother.carryTracker = new Pawn_CarryTracker(mother);
            mother.health = Uninitialized<Pawn_HealthTracker>();
            mother.health.hediffSet = new HediffSet(mother);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = mother };
            mark.comps = new List<HediffComp> { new CompRHAH_Pawn { parent = mark } };
            mother.health.hediffSet.hediffs.Add(mark);
            return mother;
        }

        static Verse.Pawn Child(Verse.Pawn mother)
        {
            Verse.Pawn child = Uninitialized<Verse.Pawn>();
            child.thingIDNumber = 42;
            child.holdingOwner = mother.carryTracker.innerContainer;
            child.lord = Uninitialized<Lord>();
            child.lord.ownedPawns = new List<Verse.Pawn> { child };
            return child;
        }

        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
    }
}
