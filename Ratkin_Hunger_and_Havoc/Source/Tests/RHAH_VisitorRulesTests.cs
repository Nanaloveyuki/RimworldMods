using System.IO;
using System.Runtime.CompilerServices;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_VisitorRulesTests
    {
        [Fact]
        public void CapKeepsTheEventMinimumAndAFixedMotherGroup()
        {
            Assert.Equal(50, RHAH_VisitorRules.LimitCount(50, 10, 8, false));
            Assert.Equal(4, RHAH_VisitorRules.LimitCount(4, 2, 1, true));
            Assert.Equal(30, RHAH_IncidentScale.Count("I-001", 10000f, 30, false));
            Assert.True(RHAH_IncidentScale.Count("I-004", 10000f, 1, false) >= 3);
        }

        [Fact]
        public void OrdinaryAgeUsesTheRangeAndYoungRolesStayYoung()
        {
            Assert.Equal(12f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.Beggar, null, 10f, 20f, 0.2f, false));
            Assert.Equal(1.5f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.RatkinYoung, 1.5f, 20f, 40f, 1f, false));
            Assert.Equal(14f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.Mother, null, 0f, 10f, 0f, true));
            Assert.Equal(14f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.BeggarMother, null, 0f, 50f, 0f, true));
            Assert.Equal((float?)28f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.Mother, 28f, 0f, 10f, 0f, true));
            Assert.Equal(24f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.RatkinYoung, null, 20f, 40f, 0.2f, true));
            Assert.Equal((float?)3.78f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.RatkinYoung, null, 20f, 40f, 0.2f, false));
        }

        [Fact]
        public void MotherAgeUsesItsFloorWithoutChangingYoungRoles()
        {
            Assert.Equal(1f, RHAH_VisitorRules.ClampMotherAge(1f));
            Assert.Equal(14f, RHAH_VisitorRules.ClampMotherAge(40f));
            Assert.Equal(14f, RHAH_VisitorRules.ClampMotherAge(float.NaN));
            Assert.Equal(1f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.Mother, null, 0f, 50f, 0f, false, 1f));
            Assert.Equal(50f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.BeggarMother, null, 0f, 50f, 1f, false, 1f));
            Assert.Equal(14f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.Mother, null, 0f, 10f, 1f, false, 14f));
            Assert.Equal(20f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.BeggarMother, null, 20f, 40f, 0f, false, 1f));
            Assert.Equal(12f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.Beggar, null, 10f, 20f, 0.2f, false, 14f));
            Assert.Equal((float?)3.78f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.RatkinYoung, null, 0f, 50f, 0.2f, false, 14f));
            Assert.Equal(28f, RHAH_VisitorRules.GenerationAge(RHAH_PawnRole.Mother, 28f, 0f, 10f, 0f, false, 14f));
        }

        [Fact]
        public void AirdropAgeIsMostlyUnderThreeAndPlayerIdeoFollowsTheShare()
        {
            Assert.True(RHAH_VisitorRules.AirdropAge(0f) < 3f);
            Assert.True(RHAH_VisitorRules.AirdropAge(0.79f) < 3f);
            Assert.True(RHAH_VisitorRules.AirdropAge(0.8f) >= 3f);
            Assert.True(RHAH_VisitorRules.AirdropAge(1f) <= 6.9f);
            Assert.True(RHAH_VisitorRules.UsesPlayerIdeo(100, 0.99f));
            Assert.False(RHAH_VisitorRules.UsesPlayerIdeo(0, 0f));
            Assert.True(RHAH_VisitorRules.UsesPlayerIdeo(40, 0.39f));
            Assert.False(RHAH_VisitorRules.UsesPlayerIdeo(40, 0.4f));
        }

        [Fact]
        public void GoodwillStaysHostileOrNeutralAndYoungNeedThreeWithoutToddlers()
        {
            Assert.Equal(-100, RHAH_VisitorRules.LockedGoodwill(RHAH_Attitude.Hostile));
            Assert.Equal(0, RHAH_VisitorRules.LockedGoodwill(RHAH_Attitude.Friendly));
            Assert.Equal(0, RHAH_VisitorRules.LockedGoodwill(RHAH_Attitude.Neutral));
            Assert.Equal(0, RHAH_VisitorRules.LockedGoodwill(RHAH_Attitude.LeaningHostile));
            Assert.Equal(0, RHAH_VisitorRules.LockedGoodwill(RHAH_Attitude.LeaningFriendly));
            RHAH_VisitorRules.OutsideRelation(true, false, false, false, out RimWorld.FactionRelationKind hostileKind, out int hostileGoodwill);
            RHAH_VisitorRules.OutsideRelation(false, true, false, true, out RimWorld.FactionRelationKind friendlyKind, out int friendlyGoodwill);
            RHAH_VisitorRules.OutsideRelation(false, false, false, true, out RimWorld.FactionRelationKind mirrorKind, out int mirrorGoodwill);
            RHAH_VisitorRules.OutsideRelation(false, false, false, false, out RimWorld.FactionRelationKind neutralKind, out int neutralGoodwill);
            RHAH_VisitorRules.OutsideRelation(true, false, true, true, out RimWorld.FactionRelationKind siblingKind, out int siblingGoodwill);
            Assert.Equal(RimWorld.FactionRelationKind.Hostile, hostileKind);
            Assert.Equal(-100, hostileGoodwill);
            Assert.Equal(RimWorld.FactionRelationKind.Neutral, friendlyKind);
            Assert.Equal(0, friendlyGoodwill);
            Assert.Equal(RimWorld.FactionRelationKind.Hostile, mirrorKind);
            Assert.Equal(-100, mirrorGoodwill);
            Assert.Equal(RimWorld.FactionRelationKind.Neutral, neutralKind);
            Assert.Equal(0, neutralGoodwill);
            Assert.Equal(RimWorld.FactionRelationKind.Neutral, siblingKind);
            Assert.Equal(0, siblingGoodwill);
            Assert.Equal(4f, RHAH_VisitorRules.WalkingAgeFloor(0.1f, false, false));
            Assert.Equal(8f, RHAH_VisitorRules.WalkingAgeFloor(8f, false, false));
            Assert.Equal(0.1f, RHAH_VisitorRules.WalkingAgeFloor(0.1f, true, false));
            Assert.Equal(0.1f, RHAH_VisitorRules.WalkingAgeFloor(0.1f, false, true));
            Assert.True(RHAH_VisitorRules.ClearsApparel(3, false));
            Assert.False(RHAH_VisitorRules.ClearsApparel(3, true));
            Assert.False(RHAH_VisitorRules.AddsColdClothes(3, true, false, -20f, 10f));
            Assert.True(RHAH_VisitorRules.AddsColdClothes(0, true, false, -20f, 10f));
            Assert.False(RHAH_VisitorRules.AddsColdClothes(1, false, false, -20f, 10f));
            Assert.True(RHAH_VisitorRules.AddsColdClothes(2, false, false, -20f, 10f));
            Assert.Equal("RHAH_Cold_StrawQuilt", RHAH_VisitorRules.SelectTemperatureApparel(-1, 16f, name => true, RHAH_VisitorRules.DefaultInsulation));
            Assert.Equal("RHAH_Cold_HideWrap", RHAH_VisitorRules.SelectTemperatureApparel(-1, 80f, name => true, RHAH_VisitorRules.DefaultInsulation));
            Assert.Equal("RHAH_Heat_BarkWrap", RHAH_VisitorRules.SelectTemperatureApparel(1, 24f, name => name != "RHAH_Heat_MudCoat", RHAH_VisitorRules.DefaultInsulation));
            Assert.Null(RHAH_VisitorRules.SelectTemperatureApparel(0, 20f, name => true, RHAH_VisitorRules.DefaultInsulation));
            Assert.Equal(0, RHAH_VisitorRules.ClampOwnedTraits(-1));
            Assert.Equal(3, RHAH_VisitorRules.ClampOwnedTraits(9));
        }

        [Fact]
        public void ReliefFoodCoversTheMapAndToddlersCanEatWhileDowned()
        {
            Assert.True(RHAH_VisitorRules.WithinFoodRange(400f, float.MaxValue));
            Assert.False(RHAH_VisitorRules.WithinFoodRange(41f, 40f));
            Assert.False(RHAH_VisitorRules.BlocksReliefFeed(true, true, 1));
            Assert.False(RHAH_VisitorRules.BlocksReliefFeed(true, true, 2));
            Assert.True(RHAH_VisitorRules.BlocksReliefFeed(true, true, 0));
            Assert.True(RHAH_VisitorRules.BlocksReliefFeed(true, false, 2));
            Assert.False(RHAH_VisitorRules.BlocksReliefFeed(false, false, 0));
        }

        [Fact]
        public void GenderModeOverridesARandomRollButNotARequestedGender()
        {
            Assert.Equal(2, RHAH_VisitorRules.ResolveGender(null, 2, 0, 0f));
            Assert.Equal(1, RHAH_VisitorRules.ResolveGender(null, 1, 0, 0f));
            Assert.Equal(1, RHAH_VisitorRules.ResolveGender(1, 3, 0, 1f));
        }

        [Fact]
        public void FedWanderUsesHoursAndLeaveNowUsesZero()
        {
            Assert.Equal(0, RHAH_VisitorRules.FedWanderTicks(false, 12));
            Assert.Equal(2500, RHAH_VisitorRules.FedWanderTicks(true, 0));
            Assert.Equal(30000, RHAH_VisitorRules.FedWanderTicks(true, 12));
            Assert.Equal(48, RHAH_VisitorRules.ClampFedWanderHours(80));
            RHAH_PawnState state = new RHAH_PawnState();
            state.TrySetLifecycle(RHAH_Lifecycle.SeekingFood);
            state.TrySetLifecycle(RHAH_Lifecycle.Fed);
            Assert.True(state.hasBeenFed);
            state.ClearFedTimer();
            Assert.True(state.hasBeenFed);
            Assert.Equal(-1, state.leaveAfterGameTick);
        }

        [Fact]
        public void MissingFoodWaitsThenResetsAfterFoodIsFound()
        {
            Assert.Equal(30000, RHAH_VisitorRules.WaitTicks(true, 0.5f));
            Assert.Equal(0, RHAH_VisitorRules.WaitTicks(false, 0.5f));
            Assert.Equal(40000, RHAH_VisitorRules.NextWaitTick(10000, 20000, true, true, 0.5f));
            Assert.Equal(20000, RHAH_VisitorRules.NextWaitTick(10000, 20000, false, true, 0.5f));
            Assert.True(RHAH_VisitorRules.NoFoodWaitExpired(true, false, 20000, 20000));
            Assert.False(RHAH_VisitorRules.NoFoodWaitExpired(true, false, 19999, 20000));
            Assert.False(RHAH_VisitorRules.NoFoodWaitExpired(true, true, 20000, 20000));
            Assert.False(RHAH_VisitorRules.NoFoodWaitExpired(false, false, 20000, 20000));
            Assert.False(RHAH_VisitorRules.NoFoodWaitExpired(true, false, 20000, -1));
        }



        [Fact]
        public void BegTargetSkipsSleepMedicalBedAndYoungChildren()
        {
            Assert.True(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, false, true, true, 4f, false));
            Assert.False(RHAH_VisitorRules.CanReceiveBeg(false, false, false, true, false, true, true, 20f, false));
            Assert.False(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, true, true, true, 20f, false));
            Assert.False(RHAH_VisitorRules.CanReceiveBeg(false, true, false, false, false, true, true, 20f, false));
            Assert.False(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, false, false, true, 20f, false));
            Assert.False(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, false, true, false, 20f, false));
            Assert.False(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, false, true, true, 3.9f, false));
            Assert.True(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, false, true, true, 4f, false));
            Assert.False(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, false, true, true, 1f + 46f / 60f, true));
            Assert.True(RHAH_VisitorRules.CanReceiveBeg(false, false, false, false, false, true, true, 1f + 47f / 60f, true));
            Assert.Equal(3, RHAH_VisitorRules.ClampBegFailCooldownHours(1));
            Assert.Equal(12, RHAH_VisitorRules.ClampBegFailCooldownHours(20));
            Assert.Equal(7500, RHAH_VisitorRules.BegFailCooldownTicks(3));
            Assert.True(RHAH_VisitorRules.BegCooldownReady(7500, 7500));
            Assert.False(RHAH_VisitorRules.BegCooldownReady(7499, 7500));
            Assert.Equal(50, RHAH_VisitorRules.ClampBegSlapChance(50));
            Assert.False(RHAH_VisitorRules.RollsSlap(50, 0.5f));
            Assert.True(RHAH_VisitorRules.RollsSlap(50, 0.49f));
            Assert.False(RHAH_VisitorRules.RollsSlap(0, 0f));
            Assert.True(RHAH_VisitorRules.RollsSlap(100, 0.99f));
            Assert.Equal(0, RHAH_VisitorRules.SlapWoundKind(false, 0f));
            Assert.Equal(1, RHAH_VisitorRules.SlapWoundKind(true, 4f));
            Assert.Equal(2, RHAH_VisitorRules.SlapWoundKind(true, 16f));
            Assert.Equal(8f, RHAH_VisitorRules.NextBruiseSeverity(4f));
            Assert.Equal(16f, RHAH_VisitorRules.NextBruiseSeverity(16f));
            Assert.Equal(-5, RHAH_VisitorRules.BegFailMood);
            Assert.Equal(3, RHAH_VisitorRules.BegSuccessMood);
            Assert.Equal(0.35f, RHAH_VisitorRules.BegSuccessChance(35, 0, 3));
            Assert.Equal(1f, RHAH_VisitorRules.BegSuccessChance(35, 30, 3));
            Assert.Equal(0.41f, RHAH_VisitorRules.BegSuccessChance(35, 2, 3));
            Assert.True(RHAH_VisitorRules.BegFoodEligible(true, true, 7, 5));
            Assert.False(RHAH_VisitorRules.BegFoodEligible(true, true, 4, 5));
            Assert.False(RHAH_VisitorRules.BegFoodEligible(true, false, 7, 5));
            Assert.Equal(-10, RHAH_VisitorRules.BegSlapMood);
            Assert.Equal(-1, RHAH_VisitorRules.UnlimitedThoughtStack);
        }

        [Fact]
        public void HireExpiresAndADownedWorkerKeepsTheRemainingTime()
        {
            int deadline = RHAH_VisitorRules.BeginStay(0, RHAH_StayKind.Hire, 5, 240);
            Assert.Equal(240 * RHAH_VisitorRules.TicksPerDay, deadline);
            Assert.False(RHAH_VisitorRules.StayExpired(deadline, deadline, true));
            int resumed = RHAH_VisitorRules.ResumeStay(deadline + 1000, deadline, 5000, false);
            Assert.Equal(deadline + 1000 + 5000, resumed);
            Assert.True(RHAH_VisitorRules.StayExpired(resumed, resumed, false));
            Assert.Equal(5 * RHAH_VisitorRules.TicksPerDay, RHAH_VisitorRules.BeginStay(0, RHAH_StayKind.Recruit, 5, 240));
            Assert.Equal(240, RHAH_VisitorRules.DefaultHireDays);
            Assert.Equal(240, RHAH_VisitorRules.MaxShelterDays);
            Assert.Equal(2400, RHAH_VisitorRules.MaxHireDays);
            Assert.Equal(1, RHAH_VisitorRules.StayYears(60));
            Assert.Equal(5, RHAH_VisitorRules.StayRestDays(65));
            Assert.Equal(0, RHAH_VisitorRules.StayYears(5));
            Assert.Equal(5, RHAH_VisitorRules.StayRestDays(5));
            Assert.True(RHAH_VisitorRules.ClearsTrade(RHAH_ReleaseReason.Recruited, false));
            Assert.True(RHAH_VisitorRules.AcceptsStone(true, false));
            Assert.False(RHAH_VisitorRules.AcceptsStone(true, true));
            Assert.True(RHAH_VisitorRules.LiftsAgeImmobility(false, true, true, false));
            Assert.False(RHAH_VisitorRules.LiftsAgeImmobility(false, true, true, true));
            Assert.True(RHAH_VisitorRules.RejectsSelfExit(true, true, false));
            Assert.False(RHAH_VisitorRules.RejectsSelfExit(true, true, true));
            Assert.False(RHAH_VisitorRules.RejectsSelfExit(true, false, false));
            Assert.False(RHAH_VisitorRules.RejectsSelfExit(false, true, false));
        }

        [Theory]
        [InlineData(false, true, false, false, true, false)]
        [InlineData(true, true, false, false, true, false)]
        [InlineData(true, false, false, false, true, true)]
        [InlineData(true, true, true, false, true, true)]
        [InlineData(true, true, false, true, true, true)]
        [InlineData(true, true, false, false, false, true)]
        public void OnlyMedicalDowningPausesChildStay(bool downed, bool ageDowned, bool forcedDowned, bool painShock, bool awake, bool expected)
        {
            Assert.Equal(expected, RHAH_VisitorRules.PausesStay(downed, ageDowned, forcedDowned, painShock, awake));
        }

        [Theory]
        [InlineData(true, RHAH_Lifecycle.Leaving, 0, false, false, true)]
        [InlineData(true, RHAH_Lifecycle.Released, 0, false, false, false)]
        [InlineData(true, RHAH_Lifecycle.Dead, 0, false, false, false)]
        [InlineData(true, RHAH_Lifecycle.SeekingFood, 0, false, false, false)]
        [InlineData(true, RHAH_Lifecycle.Leaving, 1, false, false, false)]
        [InlineData(true, RHAH_Lifecycle.Leaving, 0, true, false, false)]
        [InlineData(true, RHAH_Lifecycle.Leaving, 0, false, true, false)]
        [InlineData(false, RHAH_Lifecycle.Leaving, 0, false, false, false)]
        public void LegacyDepartureRecoveryNeverClaimsPermanentOrCurrentColonyMembers(bool player, RHAH_Lifecycle lifecycle, int stayKind, bool prisoner, bool slave, bool expected)
        {
            Assert.Equal(expected, RHAH_VisitorRules.RestoresStuckDeparture(player, lifecycle, stayKind, prisoner, slave));
        }

        [Fact]
        public void ColonyStayBlocksModBehaviorAndExpiredWorkKeepsDefense()
        {
            Assert.True(RHAH_VisitorRules.IsColonyStay((int)RHAH_StayKind.Shelter));
            Assert.True(RHAH_VisitorRules.IsColonyStay((int)RHAH_StayKind.Hire));
            Assert.True(RHAH_VisitorRules.IsColonyStay((int)RHAH_StayKind.Recruit));
            Assert.False(RHAH_VisitorRules.IsColonyStay((int)RHAH_StayKind.None));
            Assert.False(RHAH_VisitorRules.AllowsModBehavior((int)RHAH_StayKind.Recruit));
            Assert.True(RHAH_VisitorRules.AllowsModBehavior((int)RHAH_StayKind.None));
            Assert.False(RHAH_VisitorRules.BlocksAssignedWork((int)RHAH_StayKind.Hire, 10, 20, false));
            Assert.True(RHAH_VisitorRules.BlocksAssignedWork((int)RHAH_StayKind.Hire, 20, 20, false));
            Assert.False(RHAH_VisitorRules.BlocksAssignedWork((int)RHAH_StayKind.Hire, 20, 20, true));
            Assert.True(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Shelter, 10, 20, false, false, false, false, false));
            Assert.True(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Hire, 10, 20, false, false, false, false, false));
            Assert.True(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Recruit, 10, 20, true, false, false, false, false));
            Assert.False(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Shelter, 10, 20, false, false, false, false, true));
            Assert.False(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Shelter, 20, 20, false, false, false, false, false));
            Assert.False(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Hire, 10, 20, false, false, true, false, false));
            Assert.False(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Recruit, 10, 20, false, false, false, true, false));
            Assert.False(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.None, 10, 20, false, false, false, false, false));
            Assert.False(RHAH_VisitorRules.RestoresPlayerFaction((int)RHAH_StayKind.Shelter, 10, 20, false, true, false, false, false));
            Assert.False(RHAH_VisitorRules.AllowsGnaw(true, true, true, false, 0.01f, 0.05f, false, false));
            Assert.False(RHAH_VisitorRules.AllowsGnaw(true, true, false, true, 0.01f, 0.05f, false, false));
            Assert.True(RHAH_VisitorRules.AllowsGnaw(true, true, true, true, 0.049f, 0.05f, false, false));
            Assert.False(RHAH_VisitorRules.AllowsGnaw(true, true, true, true, 0.05f, 0.05f, false, false));
        }

        [Fact]
        public void RemainingStayUsesDeadlineUntilDownedAndFallsBackForOldSaves()
        {
            Assert.Equal(5000, RHAH_VisitorRules.RemainingStay(1000, 6000, false, 9000));
            Assert.Equal(4000, RHAH_VisitorRules.RemainingStay(2000, 6000, false, 9000));
            Assert.Equal(9000, RHAH_VisitorRules.RemainingStay(8000, 6000, true, 9000));
            Assert.Equal(5000, RHAH_VisitorRules.RemainingStay(1000, 6000, true, 0));
            Assert.Equal(0, RHAH_VisitorRules.RemainingStay(6000, 6000, false, 9000));
            Assert.Equal(0, RHAH_VisitorRules.RemainingStay(1000, -1, false, 9000));
            Assert.Null(RHAH_VisitorRules.StayCountdown((int)RHAH_StayKind.None, 1000, 6000, false, 9000));
            Assert.Null(RHAH_VisitorRules.StayCountdown((int)RHAH_StayKind.Hire, 6000, 6000, false, 9000));
        }

        [Fact]
        public void AttitudeFactionsUseRealFieldsAndDoNotDropRaidLoot()
        {
            string xml = File.ReadAllText(FactionPath());
            Assert.DoesNotContain("naturalColonyGoodwill", xml);
            Assert.Contains("<naturalEnemy>true</naturalEnemy>", xml);
            Assert.Contains("<raidLootValueFromPointsCurve>", xml);
            Assert.Equal(1, Count(xml, "<raidLootValueFromPointsCurve>"));
        }

        [Fact]
        public void SpawnDoesNotFallBackToThePlayerFaction()
        {
            string facts = File.ReadAllText(SourcePath("Incidents/RHAH_IncidentFacts.cs"));
            string trade = File.ReadAllText(SourcePath("Trade/TradeEventRouter.cs"));
            string envoy = File.ReadAllText(SourcePath("Storyteller/Suiyin/RHAH_Envoy.cs"));
            string factory = File.ReadAllText(SourcePath("Generation/RHAH_PawnFactory.cs"));
            Assert.DoesNotContain("?? Faction.OfPlayer", facts);
            Assert.DoesNotContain("?? Faction.OfPlayer", trade);
            Assert.DoesNotContain("?? Faction.OfPlayer", envoy);
            Assert.Contains("request.Faction.IsPlayer", factory);
            Assert.Contains("RHAH_AttitudeFactions.Require", facts);
        }

        static int Count(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        static string FactionPath([CallerFilePath] string testFile = null)
        {
            return Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(testFile), "..", "..", "1.6", "Defs", "FactionDefs", "RHAH_Factions.xml"));
        }

        static string SourcePath(string relative, [CallerFilePath] string testFile = null)
        {
            return Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(testFile), "..", "..", "Source", relative));
        }
    }
}
