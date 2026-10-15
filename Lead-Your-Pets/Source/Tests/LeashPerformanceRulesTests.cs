using Xunit;

namespace LeadYourPet.Tests
{
    public class LeashPerformanceRulesTests
    {
        [Fact]
        public void ProcessesLinkOnlyOnItsAssignedBucketTick()
        {
            Assert.True(LeadYourPetRules.ShouldProcessLinkOnTick(
                currentTick: 120,
                bucketCount: 4,
                linkBucket: 0));

            Assert.False(LeadYourPetRules.ShouldProcessLinkOnTick(
                currentTick: 121,
                bucketCount: 4,
                linkBucket: 0));
        }

        [Fact]
        public void AlwaysProcessesWhenImmediateRefreshIsRequested()
        {
            Assert.True(LeadYourPetRules.ShouldProcessLinkOnTick(
                currentTick: 121,
                bucketCount: 4,
                linkBucket: 0,
                forceImmediate: true));
        }

        [Fact]
        public void ReachesEveryBucketWhenCalledOnSixTickCadence()
        {
            Assert.True(LeadYourPetRules.ShouldProcessLinkOnTick(
                currentTick: 126,
                bucketCount: 4,
                linkBucket: 1));

            Assert.True(LeadYourPetRules.ShouldProcessLinkOnTick(
                currentTick: 138,
                bucketCount: 4,
                linkBucket: 3));
        }

        [Fact]
        public void RunsExpensiveUpdateWhenCooldownElapsed()
        {
            Assert.True(LeadYourPetRules.ShouldRunExpensiveLinkUpdate(
                currentTick: 600,
                lastExpensiveUpdateTick: 540,
                minIntervalTicks: 60,
                forceImmediate: false));
        }

        [Fact]
        public void SkipsExpensiveUpdateBeforeCooldownElapsed()
        {
            Assert.False(LeadYourPetRules.ShouldRunExpensiveLinkUpdate(
                currentTick: 580,
                lastExpensiveUpdateTick: 540,
                minIntervalTicks: 60,
                forceImmediate: false));
        }

        [Fact]
        public void RunsExpensiveUpdateImmediatelyWhenForced()
        {
            Assert.True(LeadYourPetRules.ShouldRunExpensiveLinkUpdate(
                currentTick: 541,
                lastExpensiveUpdateTick: 540,
                minIntervalTicks: 60,
                forceImmediate: true));
        }

        [Fact]
        public void SkipsFollowPathRefreshBeforeCooldownElapsed()
        {
            Assert.False(LeadYourPetRules.ShouldIssueFollowPath(
                currentTick: 1020,
                lastFollowIssueTick: 1000,
                minIntervalTicks: 30,
                forceImmediate: false));
        }

        [Fact]
        public void IssuesLeashFollowJobWhenFollowingMasterAndNoCurrentFollowJobExists()
        {
            Assert.True(LeadYourPetRules.ShouldIssueLeashFollowJob(
                masterIsPlayerControlled: true,
                followTargetIsMaster: true,
                alreadyFollowingSameMaster: false,
                canReachTarget: true,
                shouldIssueFollowPath: true));
        }

        [Fact]
        public void DoesNotReissueLeashFollowJobWhenAlreadyFollowingSameMaster()
        {
            Assert.False(LeadYourPetRules.ShouldIssueLeashFollowJob(
                masterIsPlayerControlled: true,
                followTargetIsMaster: true,
                alreadyFollowingSameMaster: true,
                canReachTarget: true,
                shouldIssueFollowPath: true));
        }

        [Fact]
        public void DoesNotIssueLeashFollowJobForAnchoredTarget()
        {
            Assert.False(LeadYourPetRules.ShouldIssueLeashFollowJob(
                masterIsPlayerControlled: true,
                followTargetIsMaster: false,
                alreadyFollowingSameMaster: false,
                canReachTarget: true,
                shouldIssueFollowPath: true));
        }

        [Fact]
        public void DoesNotIssueLeashFollowJobForNonPlayerMaster()
        {
            Assert.False(LeadYourPetRules.ShouldIssueLeashFollowJob(
                masterIsPlayerControlled: false,
                followTargetIsMaster: true,
                alreadyFollowingSameMaster: false,
                canReachTarget: true,
                shouldIssueFollowPath: true));
        }

        [Fact]
        public void SleepingMasterDoesNotUseBidirectionalMouseEggAutoInteractions()
        {
            Assert.False(LeadYourPetRules.ShouldUseBidirectionalMouseEggAutoInteractions(
                playerControlled: true,
                canUseSingleDirection: false,
                masterSleeping: true));
        }

        [Fact]
        public void AwakeMasterMayUseBidirectionalMouseEggAutoInteractions()
        {
            Assert.True(LeadYourPetRules.ShouldUseBidirectionalMouseEggAutoInteractions(
                playerControlled: true,
                canUseSingleDirection: false,
                masterSleeping: false));
        }

        [Fact]
        public void RefreshesAutoInteractionPoolWhenCachedStateChanges()
        {
            Assert.True(LeadYourPetRules.ShouldRefreshAutoInteractionPool(
                currentTick: 1200,
                lastPoolTick: 1000,
                cacheIntervalTicks: 300,
                cacheValid: true,
                cachedPlayerControlled: false,
                playerControlled: true,
                cachedCanUseSingleDirection: true,
                canUseSingleDirection: true,
                cachedMasterSleeping: false,
                masterSleeping: false));
        }

        [Fact]
        public void KeepsAutoInteractionPoolCacheBeforeExpiryWhenStateMatches()
        {
            Assert.False(LeadYourPetRules.ShouldRefreshAutoInteractionPool(
                currentTick: 1200,
                lastPoolTick: 1000,
                cacheIntervalTicks: 300,
                cacheValid: true,
                cachedPlayerControlled: true,
                playerControlled: true,
                cachedCanUseSingleDirection: false,
                canUseSingleDirection: false,
                cachedMasterSleeping: false,
                masterSleeping: false));
        }

        [Fact]
        public void RefreshesAutoInteractionPoolWhenMasterSleepStateChanges()
        {
            Assert.True(LeadYourPetRules.ShouldRefreshAutoInteractionPool(
                currentTick: 1200,
                lastPoolTick: 1000,
                cacheIntervalTicks: 300,
                cacheValid: true,
                cachedPlayerControlled: true,
                playerControlled: true,
                cachedCanUseSingleDirection: false,
                canUseSingleDirection: false,
                cachedMasterSleeping: false,
                masterSleeping: true));
        }

        [Fact]
        public void RopeDrawOverrideOnlyRunsWhenAnyLeashExists()
        {
            Assert.True(LeadYourPetRules.ShouldEvaluateCustomRopeTarget(
                hasAnyLeash: true,
                trackerAlreadyRoped: false,
                pawnAvailable: true));

            Assert.False(LeadYourPetRules.ShouldEvaluateCustomRopeTarget(
                hasAnyLeash: false,
                trackerAlreadyRoped: false,
                pawnAvailable: true));

            Assert.False(LeadYourPetRules.ShouldEvaluateCustomRopeTarget(
                hasAnyLeash: true,
                trackerAlreadyRoped: true,
                pawnAvailable: true));
        }

        [Fact]
        public void RopeDrawOverrideSkipsPawnsWithoutPossibleLeashMembership()
        {
            Assert.True(LeadYourPetRules.ShouldEvaluateCustomRopeTarget(
                hasAnyLeash: true,
                trackerAlreadyRoped: false,
                pawnAvailable: true,
                pawnCanParticipateInLeash: true));

            Assert.False(LeadYourPetRules.ShouldEvaluateCustomRopeTarget(
                hasAnyLeash: true,
                trackerAlreadyRoped: false,
                pawnAvailable: true,
                pawnCanParticipateInLeash: false));
        }

        [Fact]
        public void ResolvesMouseEggDutyStateByMasterActivity()
        {
            Assert.Equal(LeashedMouseEggDutyState.FollowMaster, LeadYourPetRules.ResolveLeashedMouseEggDutyState(
                masterSleeping: false,
                masterEating: false));

            Assert.Equal(LeashedMouseEggDutyState.SleepAtLeash, LeadYourPetRules.ResolveLeashedMouseEggDutyState(
                masterSleeping: true,
                masterEating: false));

            Assert.Equal(LeashedMouseEggDutyState.WaitForFeeding, LeadYourPetRules.ResolveLeashedMouseEggDutyState(
                masterSleeping: false,
                masterEating: true));
        }

        [Fact]
        public void SharesHalfOfMastersMealNutritionWithLeashedMouseEgg()
        {
            Assert.Equal(0.5f, LeadYourPetRules.ResolveSharedNutritionFromMasterMeal(1f), 3);
            Assert.Equal(0f, LeadYourPetRules.ResolveSharedNutritionFromMasterMeal(0f), 3);
        }

        [Fact]
        public void CapsFedNutritionAtPetsFoodNeedMaximum()
        {
            Assert.Equal(0.9f, LeadYourPetRules.ResolveFedNutritionLevel(
                currentNutrition: 0.4f,
                maxNutrition: 0.9f,
                gainedNutrition: 0.7f), 3);

            Assert.Equal(0.6f, LeadYourPetRules.ResolveFedNutritionLevel(
                currentNutrition: 0.4f,
                maxNutrition: 0.9f,
                gainedNutrition: 0.2f), 3);
        }

        [Fact]
        public void AppendsCustomMouseEggDutyInspectLineOnlyWhenNotAlreadyVisible()
        {
            Assert.True(LeadYourPetRules.ShouldAppendCustomMouseEggDutyInspectLine(
                hasCustomReport: true,
                inspectStringAlreadyContainsReport: false));

            Assert.False(LeadYourPetRules.ShouldAppendCustomMouseEggDutyInspectLine(
                hasCustomReport: true,
                inspectStringAlreadyContainsReport: true));

            Assert.False(LeadYourPetRules.ShouldAppendCustomMouseEggDutyInspectLine(
                hasCustomReport: false,
                inspectStringAlreadyContainsReport: false));
        }

        [Fact]
        public void SkipsYoungRatkinTradeCheckWhenTheKindAlreadyTradesOrTheRowIsNotAPawn()
        {
            Assert.False(LeadYourPetRules.ShouldEvaluateTradeableYoungRatkin(kindWillTrade: true, isPawn: true));
            Assert.False(LeadYourPetRules.ShouldEvaluateTradeableYoungRatkin(kindWillTrade: false, isPawn: false));
            Assert.True(LeadYourPetRules.ShouldEvaluateTradeableYoungRatkin(kindWillTrade: false, isPawn: true));
        }

        [Fact]
        public void PreparesADepartingLordOncePerCooldownUnlessForced()
        {
            Assert.True(LeadYourPetRules.ShouldPrepareTravelDeparture(
                currentTick: 600, lastPreparedTick: 500, minIntervalTicks: 60, forceImmediate: false, departing: true));
            Assert.False(LeadYourPetRules.ShouldPrepareTravelDeparture(
                currentTick: 540, lastPreparedTick: 500, minIntervalTicks: 60, forceImmediate: false, departing: true));
            Assert.True(LeadYourPetRules.ShouldPrepareTravelDeparture(
                currentTick: 501, lastPreparedTick: 500, minIntervalTicks: 60, forceImmediate: true, departing: true));
            Assert.False(LeadYourPetRules.ShouldPrepareTravelDeparture(
                currentTick: 600, lastPreparedTick: -99999, minIntervalTicks: 60, forceImmediate: true, departing: false));
        }
    }
}
