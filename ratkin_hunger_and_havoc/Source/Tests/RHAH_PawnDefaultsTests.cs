using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_PawnDefaultsTests
    {
        [Theory]
        [InlineData(RHAH_Lifecycle.Released)]
        [InlineData(RHAH_Lifecycle.Dead)]
        public void InactiveVisitor_OnlyColonyControlGates(RHAH_Lifecycle lifecycle)
        {
            Assert.True(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.JoinColony));
            Assert.True(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.Hire));
            Assert.True(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.Transfer));
            Assert.True(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.Imprison));
            Assert.True(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.Leash));
            Assert.True(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.Carry));
            Assert.False(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.Beg));
            Assert.False(Allow(RHAH_PawnRole.Thief, lifecycle, RHAH_Attitude.Hostile, RHAH_BehaviorGate.Steal));
            Assert.False(Allow(RHAH_PawnRole.Siege, lifecycle, RHAH_Attitude.Hostile, RHAH_BehaviorGate.Fight));
            Assert.False(Allow(RHAH_PawnRole.Beggar, lifecycle, RHAH_Attitude.Neutral, RHAH_BehaviorGate.ExitMap));
        }

        [Theory]
        [InlineData(RHAH_PawnRole.Beggar)]
        [InlineData(RHAH_PawnRole.BeggarMother)]
        [InlineData(RHAH_PawnRole.BeggarChild)]
        [InlineData(RHAH_PawnRole.Refugee)]
        [InlineData(RHAH_PawnRole.Labor)]
        public void BeggarRoles_CanBeg(RHAH_PawnRole role)
        {
            Assert.True(AllowVisitor(role, RHAH_BehaviorGate.Beg));
        }

        [Theory]
        [InlineData(RHAH_PawnRole.Thief)]
        [InlineData(RHAH_PawnRole.Wild)]
        [InlineData(RHAH_PawnRole.Trader)]
        [InlineData(RHAH_PawnRole.Unspecified)]
        [InlineData(RHAH_PawnRole.Mother)]
        public void NonBeggarRoles_CannotBeg(RHAH_PawnRole role)
        {
            Assert.False(AllowVisitor(role, RHAH_BehaviorGate.Beg));
        }

        [Theory]
        [InlineData(RHAH_PawnRole.Thief)]
        [InlineData(RHAH_PawnRole.ThiefChild)]
        public void ThiefRoles_CanSteal(RHAH_PawnRole role)
        {
            Assert.True(AllowVisitor(role, RHAH_BehaviorGate.Steal));
        }

        [Fact]
        public void NonThief_CannotSteal()
        {
            Assert.False(AllowVisitor(RHAH_PawnRole.Beggar, RHAH_BehaviorGate.Steal));
            Assert.False(AllowVisitor(RHAH_PawnRole.Wild, RHAH_BehaviorGate.Steal));
        }

        [Fact]
        public void Fight_SiegeOrHostile()
        {
            Assert.True(AllowVisitor(RHAH_PawnRole.Siege, RHAH_BehaviorGate.Fight));
            Assert.True(Allow(
                RHAH_PawnRole.Wild,
                RHAH_Lifecycle.SeekingFood,
                RHAH_Attitude.Hostile,
                RHAH_BehaviorGate.Fight));
            Assert.False(AllowVisitor(RHAH_PawnRole.Beggar, RHAH_BehaviorGate.Fight));
            Assert.False(Allow(
                RHAH_PawnRole.Wild,
                RHAH_Lifecycle.SeekingFood,
                RHAH_Attitude.LeaningHostile,
                RHAH_BehaviorGate.Fight));
        }

        [Fact]
        public void EatOutsideRelief_BlockedWhenFriendly()
        {
            Assert.False(Allow(
                RHAH_PawnRole.Beggar,
                RHAH_Lifecycle.Arriving,
                RHAH_Attitude.Friendly,
                RHAH_BehaviorGate.EatOutsideRelief));
            Assert.True(AllowVisitor(RHAH_PawnRole.Beggar, RHAH_BehaviorGate.EatOutsideRelief));
            Assert.True(Allow(
                RHAH_PawnRole.Beggar,
                RHAH_Lifecycle.Arriving,
                RHAH_Attitude.LeaningFriendly,
                RHAH_BehaviorGate.EatOutsideRelief));
        }

        [Theory]
        [InlineData(RHAH_PawnRole.RatkinYoung)]
        [InlineData(RHAH_PawnRole.BeggarChild)]
        [InlineData(RHAH_PawnRole.ThiefChild)]
        [InlineData(RHAH_PawnRole.WildChild)]
        public void ChildRoles_CanLeashButCannotCarry(RHAH_PawnRole role)
        {
            Assert.True(AllowVisitor(role, RHAH_BehaviorGate.Leash));
            Assert.False(AllowVisitor(role, RHAH_BehaviorGate.Carry));
        }

        [Theory]
        [InlineData(RHAH_PawnRole.Beggar)]
        [InlineData(RHAH_PawnRole.Mother)]
        [InlineData(RHAH_PawnRole.BeggarMother)]
        [InlineData(RHAH_PawnRole.Trader)]
        [InlineData(RHAH_PawnRole.Escort)]
        public void AdultCaregivers_CanCarryButCannotLeash(RHAH_PawnRole role)
        {
            Assert.False(AllowVisitor(role, RHAH_BehaviorGate.Leash));
            Assert.True(AllowVisitor(role, RHAH_BehaviorGate.Carry));
        }

        [Theory]
        [InlineData(RHAH_PawnRole.Mother)]
        [InlineData(RHAH_PawnRole.BeggarMother)]
        public void MotherRoles_CanDropOffChild(RHAH_PawnRole role)
        {
            Assert.True(AllowVisitor(role, RHAH_BehaviorGate.DropOffChild));
        }

        [Fact]
        public void NonMother_CannotDropOffChild()
        {
            Assert.False(AllowVisitor(RHAH_PawnRole.Beggar, RHAH_BehaviorGate.DropOffChild));
            Assert.False(AllowVisitor(RHAH_PawnRole.RatkinYoung, RHAH_BehaviorGate.DropOffChild));
        }

        [Fact]
        public void ActiveVisitor_SharedGates()
        {
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.LeaveAfterFed));
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.FeedFromRelief));
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.Gnaw));
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.JoinColony));
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.Hire));
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.Transfer));
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.Imprison));
            Assert.False(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.ExitMap));
            Assert.True(AllowVisitor(RHAH_PawnRole.Unspecified, RHAH_BehaviorGate.TailBite));
        }

        [Fact]
        public void LeavingVisitor_MayExitMap()
        {
            Assert.True(Allow(RHAH_PawnRole.Beggar, RHAH_Lifecycle.Leaving, RHAH_Attitude.Neutral, RHAH_BehaviorGate.ExitMap));
            Assert.False(Allow(RHAH_PawnRole.Beggar, RHAH_Lifecycle.Arriving, RHAH_Attitude.Neutral, RHAH_BehaviorGate.ExitMap));
            Assert.False(Allow(RHAH_PawnRole.Beggar, RHAH_Lifecycle.SeekingFood, RHAH_Attitude.Neutral, RHAH_BehaviorGate.ExitMap));
        }
        [Fact]
        public void NullSnapshot_TreatedAsInactive()
        {
            Assert.True(RHAH_PawnDefaults.Allows((IRHAH_Pawn)null, RHAH_BehaviorGate.JoinColony));
            Assert.True(RHAH_PawnDefaults.Allows((IRHAH_Pawn)null, RHAH_BehaviorGate.Hire));
            Assert.True(RHAH_PawnDefaults.Allows((IRHAH_Pawn)null, RHAH_BehaviorGate.Transfer));
            Assert.False(RHAH_PawnDefaults.Allows((IRHAH_Pawn)null, RHAH_BehaviorGate.Beg));
        }

        [Fact]
        public void SnapshotOverload_UsesRoleLifecycleAttitude()
        {
            RHAH_PawnSnapshot visitor = new RHAH_PawnSnapshot(
                "I-005",
                0,
                0,
                RHAH_PawnRole.Thief,
                RHAH_Lifecycle.SeekingFood,
                false,
                -1,
                false,
                RHAH_Attitude.Neutral,
                RHAH_Attitude.Neutral,
                0,
                null);
            RHAH_PawnSnapshot released = new RHAH_PawnSnapshot(
                "I-005",
                0,
                0,
                RHAH_PawnRole.Thief,
                RHAH_Lifecycle.Released,
                true,
                -1,
                false,
                RHAH_Attitude.Hostile,
                RHAH_Attitude.Hostile,
                0,
                null);

            Assert.True(RHAH_PawnDefaults.Allows(visitor, RHAH_BehaviorGate.Steal));
            Assert.False(RHAH_PawnDefaults.Allows(visitor, RHAH_BehaviorGate.Fight));
            Assert.False(RHAH_PawnDefaults.Allows(released, RHAH_BehaviorGate.Steal));
            Assert.True(RHAH_PawnDefaults.Allows(released, RHAH_BehaviorGate.Imprison));
        }

        static bool AllowVisitor(RHAH_PawnRole role, RHAH_BehaviorGate gate)
        {
            return Allow(role, RHAH_Lifecycle.SeekingFood, RHAH_Attitude.Neutral, gate);
        }

        static bool Allow(
            RHAH_PawnRole role,
            RHAH_Lifecycle lifecycle,
            RHAH_Attitude attitude,
            RHAH_BehaviorGate gate)
        {
            return RHAH_PawnDefaults.Allows(role, lifecycle, attitude, gate);
        }
    }
}
