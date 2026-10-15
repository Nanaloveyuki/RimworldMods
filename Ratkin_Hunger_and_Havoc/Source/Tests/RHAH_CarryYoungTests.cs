using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Pawn;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_CarryYoungTests
    {
        [Fact]
        public void AwakeImmobileVisitorStaysCarriableWithoutBeingDowned()
        {
            Verse.Pawn child = Pawn(false);
            Lord lord = Lord();
            child.lord = lord;

            Assert.Equal(
                JobCondition.Ongoing,
                JobDriver_RHAH_CarryYoung.ReleaseStop(child, child.GetLord(), child.GetLord(), child.CarriedBy, Pawn(false), true));
            Assert.False(child.Downed);
            Assert.Same(lord, child.GetLord());
        }

        [Fact]
        public void DownedVisitorStaysCarriable()
        {
            Verse.Pawn child = Pawn(true);
            Lord lord = Lord();
            child.lord = lord;

            Assert.True(child.Downed);
            Assert.Equal(
                JobCondition.Ongoing,
                JobDriver_RHAH_CarryYoung.ReleaseStop(child, child.GetLord(), child.GetLord(), null, Pawn(false), true));
        }

        [Fact]
        public void IncapacitationKeepsVisitorCaregiversAndChildrenInTheirLord()
        {
            LordJob_RHAH_Visitor visitor = new LordJob_RHAH_Visitor();
            Assert.False(visitor.ShouldRemovePawn(Pawn(true), PawnLostCondition.Incapped));
            Assert.True(visitor.ShouldRemovePawn(Pawn(false), PawnLostCondition.Killed));
            Assert.True(visitor.ShouldRemovePawn(Pawn(false), PawnLostCondition.MadePrisoner));
        }

        [Fact]
        public void AttitudeFactionChangePreservesCareOnlyDuringTheBatchTransition()
        {
            LordJob_RHAH_Visitor visitor = new LordJob_RHAH_Visitor();
            Verse.Pawn pawn = Pawn(false);
            Assert.True(visitor.ShouldRemovePawn(pawn, PawnLostCondition.ChangedFaction));
            visitor.PreserveForAttitudeChange = true;
            Assert.False(visitor.ShouldRemovePawn(pawn, PawnLostCondition.ChangedFaction));
            Assert.True(visitor.ShouldRemovePawn(pawn, PawnLostCondition.MadePrisoner));
            visitor.PreserveForAttitudeChange = false;
            Assert.True(visitor.ShouldRemovePawn(pawn, PawnLostCondition.ChangedFaction));
        }

        [Fact]
        public void CarriedPawnKeepsTheLordUsedByTheStop()
        {
            Verse.Pawn child = Pawn(false);
            Verse.Pawn carrier = Pawn(false);
            Lord lord = Lord();
            child.lord = lord;

            Assert.Same(lord, child.GetLord());
            Assert.Equal(
                JobCondition.Ongoing,
                JobDriver_RHAH_CarryYoung.ReleaseStop(child, lord, child.GetLord(), carrier, carrier, true));
        }

        [Fact]
        public void CarryStopsWhenAnotherPawnAlreadyHoldsTheChild()
        {
            Verse.Pawn child = Pawn(false);
            Verse.Pawn carrier = Pawn(false);
            Lord lord = Lord();
            child.lord = lord;

            Assert.Equal(
                JobCondition.Incompletable,
                JobDriver_RHAH_CarryYoung.ReleaseStop(child, lord, child.GetLord(), Pawn(false), carrier, true));
        }

        [Fact]
        public void CarryStopsWhenTheChildLeavesTheVisitorLord()
        {
            Verse.Pawn child = Pawn(false);
            Lord started = Lord();
            child.lord = Lord();

            Assert.Equal(
                JobCondition.Incompletable,
                JobDriver_RHAH_CarryYoung.ReleaseStop(child, started, child.GetLord(), null, Pawn(false), true));
        }

        [Fact]
        public void CarryStopsWhenTheChildIsNoLongerAVisitor()
        {
            Verse.Pawn child = Pawn(false);
            Lord lord = Lord();
            child.lord = lord;

            Assert.Equal(
                JobCondition.Incompletable,
                JobDriver_RHAH_CarryYoung.ReleaseStop(child, lord, child.GetLord(), null, Pawn(false), false));
        }

        [Fact]
        public void MissingLordIsCapturedOnceAndDoesNotReset()
        {
            Assert.Equal(
                JobCondition.Ongoing,
                JobDriver_RHAH_CarryYoung.ReleaseStop(Pawn(false), null, null, null, Pawn(false), true));
            Assert.Equal(
                JobCondition.Incompletable,
                JobDriver_RHAH_CarryYoung.ReleaseStop(Pawn(false), null, Lord(), null, Pawn(false), true));
        }

        [Fact]
        public void MissingTargetCannotBeReserved()
        {
            Job job = (Job)FormatterServices.GetUninitializedObject(typeof(Job));
            job.count = 1;
            JobDriver_RHAH_CarryYoung driver = new JobDriver_RHAH_CarryYoung();
            driver.pawn = Pawn(false);
            driver.job = job;

            Assert.False(driver.TryMakePreToilReservations(false));
        }

        static Verse.Pawn Pawn(bool downed)
        {
            Verse.Pawn pawn = (Verse.Pawn)FormatterServices.GetUninitializedObject(typeof(Verse.Pawn));
            Pawn_HealthTracker health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
            typeof(Pawn_HealthTracker)
                .GetField("healthState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(health, downed ? PawnHealthState.Down : PawnHealthState.Mobile);
            pawn.health = health;
            return pawn;
        }

        static Lord Lord()
        {
            return (Lord)FormatterServices.GetUninitializedObject(typeof(Lord));
        }
    }
}
