using Xunit;

namespace LeadYourPet.Tests
{
    public class TravelAssignmentRulesTests
    {
        [Fact]
        public void BlocksCarryForProtectedYoungRatkinEvenWhenColonistCarryWouldOtherwiseBeAllowed()
        {
            Assert.True(LeadYourPetRules.ShouldBlockProtectedCarry(
                isProtectedLeashedYoungRatkin: true,
                allowColonistCarry: true));
        }

        [Theory]
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, false, false)]
        [InlineData(false, true, true, false)]
        public void AllowsOnlyMatchingPrisonerTransferToCarryProtectedYoungRatkin(
            bool isProtectedLeashedYoungRatkin,
            bool jobMakesTargetPrisoner,
            bool jobTargetMatchesPawn,
            bool expected)
        {
            Assert.Equal(expected, LeadYourPetRules.ShouldAllowProtectedPawnPrisonerTransfer(
                isProtectedLeashedYoungRatkin,
                jobMakesTargetPrisoner,
                jobTargetMatchesPawn));
        }

        [Fact]
        public void TreatsLeashedYoungRatkinChildAsProtected()
        {
            Assert.True(LeadYourPetRules.ShouldTreatAsProtectedLeashedYoungRatkin(
                isYoungRatkin: true,
                isBaby: false,
                isChild: true,
                hasProtectedLink: true,
                hasProtectedState: false));
        }

        [Fact]
        public void TreatsAnyHumanlikePawnUnderFourteenAsYoungRatkinCompatible()
        {
            Assert.True(LeadYourPetRules.ShouldTreatAsYoungRatkin(
                isRatkinHumanlike: false,
                isBaby: false,
                isChild: false,
                hasMeaningfulState: false,
                isTravelYoungRatkin: false,
                isHumanlikeJuvenile: true));
        }

        [Fact]
        public void TreatsHumanlikeJuvenileAsProtectedWhenLeashed()
        {
            Assert.True(LeadYourPetRules.ShouldTreatAsProtectedLeashedYoungRatkin(
                isYoungRatkin: true,
                isBaby: false,
                isChild: false,
                hasProtectedLink: true,
                hasProtectedState: false,
                isHumanlikeJuvenile: true));
        }

        [Fact]
        public void DropsCarriedTravelYoungRatkinAfterRegistrationWhenCarrierIsSpawned()
        {
            Assert.True(LeadYourPetRules.ShouldDropCarriedTravelYoungRatkinAfterRegistration(
                registrationSucceeded: true,
                carrierSpawned: true,
                eggAlreadySpawned: false));
        }

        [Fact]
        public void RegistersCarriedTravelYoungRatkinOnlyAfterDropToGroundSucceeds()
        {
            Assert.True(LeadYourPetRules.ShouldRegisterDroppedTravelYoungRatkin(
                hadCarriedYoungRatkin: true,
                dropSucceeded: true,
                droppedPawnValid: true));

            Assert.False(LeadYourPetRules.ShouldRegisterDroppedTravelYoungRatkin(
                hadCarriedYoungRatkin: true,
                dropSucceeded: false,
                droppedPawnValid: true));

            Assert.False(LeadYourPetRules.ShouldRegisterDroppedTravelYoungRatkin(
                hadCarriedYoungRatkin: true,
                dropSucceeded: true,
                droppedPawnValid: false));
        }

        [Fact]
        public void SkipsRandomTravelYoungRatkinFallbackWhenExistingCarriedEggWasAlreadyHandled()
        {
            Assert.True(LeadYourPetRules.ShouldSkipRandomTravelYoungRatkinFallback(
                hadCarriedYoungRatkin: true,
                carriedEggRegistrationSucceeded: false));

            Assert.False(LeadYourPetRules.ShouldSkipRandomTravelYoungRatkinFallback(
                hadCarriedYoungRatkin: false,
                carriedEggRegistrationSucceeded: false));
        }

        [Fact]
        public void DropsPawnImmediatelyAfterItBecomesProtectedYoungRatkinPetWhileCarried()
        {
            Assert.True(LeadYourPetRules.ShouldDropCarriedPawnAfterBecomingYoungRatkinPet(
                isProtectedYoungRatkinPet: true,
                pawnHasCarrier: true,
                carrierHasCarryTracker: true,
                carrierIsCarryingPawn: true));
        }

        [Fact]
        public void DoesNotDropCarriedPawnWhenItIsNotProtectedYoungRatkinPet()
        {
            Assert.False(LeadYourPetRules.ShouldDropCarriedPawnAfterBecomingYoungRatkinPet(
                isProtectedYoungRatkinPet: false,
                pawnHasCarrier: true,
                carrierHasCarryTracker: true,
                carrierIsCarryingPawn: true));
        }

        [Fact]
        public void AllowsNearbyRatkinDisasterChildFallbackWhenChildIsUnownedAndNearTrader()
        {
            Assert.True(LeadYourPetRules.IsEligibleNearbyRatkinDisasterTravelChild(
                isTravelChild: true,
                isSpawned: true,
                isDead: false,
                sameFactionAsTraderLord: true,
                hasNoLord: true,
                isNearTrader: true));
        }

        [Fact]
        public void RejectsNearbyRatkinDisasterChildFallbackWhenChildAlreadyHasLord()
        {
            Assert.False(LeadYourPetRules.IsEligibleNearbyRatkinDisasterTravelChild(
                isTravelChild: true,
                isSpawned: true,
                isDead: false,
                sameFactionAsTraderLord: true,
                hasNoLord: false,
                isNearTrader: true));
        }

        [Fact]
        public void BlocksRimTalkToddlerPickupForProtectedLeashedYoungRatkin()
        {
            Assert.True(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedYoungRatkin: true,
                jobDefName: "RimTalk_PickUpToddler"));
        }

        [Fact]
        public void BlocksRimTalkBeingCarriedStateForProtectedLeashedYoungRatkin()
        {
            Assert.True(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedYoungRatkin: true,
                jobDefName: "RimTalk_BeingCarried_Idle"));
        }

        [Theory]
        [InlineData("PlayCrib")]
        [InlineData("BePlayedWith")]
        [InlineData("BringBabyToSafety")]
        [InlineData("PutInCrib")]
        [InlineData("CYB_BatheToddler")]
        public void BlocksToddlersExpandedHoldJobsForProtectedLeashedYoungRatkin(string jobDefName)
        {
            Assert.True(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedYoungRatkin: true,
                jobDefName: jobDefName));
        }

        [Theory]
        [InlineData("Toddlers.JobDriver_BePlayedWith")]
        [InlineData("Toddlers.JobDriver_PlayCrib")]
        [InlineData("RimWorld.JobDriver_BabyPlay")]
        public void EndsToddlersExpandedHoldDriversForProtectedLeashedYoungRatkin(string driverTypeName)
        {
            Assert.True(LeadYourPetRules.ShouldEndExternalToddlerHoldJob(
                isProtectedLeashedYoungRatkin: true,
                driverTypeName: driverTypeName));
        }
        [Theory]
        [InlineData("RimTalk_PickUpToddler")]
        [InlineData("RimTalk_BeingCarried_Struggle")]
        [InlineData("PlayCrib")]
        [InlineData("CYB_WashBaby")]
        public void RecognizesExternalToddlerHoldJobsEvenBeforeLeashProtection(string jobDefName)
        {
            Assert.True(LeadYourPetRules.IsExternalToddlerHoldJob(jobDefName));
            Assert.False(LeadYourPetRules.ShouldBlockExternalToddlerPickup(false, jobDefName));
        }

        [Theory]
        [InlineData("RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_BeingCarried")]
        [InlineData("RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_PickUpToddler")]
        [InlineData("Toddlers.JobDriver_BePlayedWith")]
        public void RecognizesExternalToddlerHoldDrivers(string driverTypeName)
        {
            Assert.True(LeadYourPetRules.IsExternalToddlerHoldDriver(driverTypeName));
        }

        [Fact]
        public void PreservesMapExitFromJobDutyOrLordToil()
        {
            Assert.True(LeadYourPetRules.ShouldPreserveMapExitJob(true, false, false));
            Assert.True(LeadYourPetRules.ShouldPreserveMapExitJob(false, true, false));
            Assert.True(LeadYourPetRules.ShouldPreserveMapExitJob(false, false, true));
            Assert.False(LeadYourPetRules.ShouldPreserveMapExitJob(false, false, false));
        }

        [Fact]
        public void SuppressesAutomaticInteractionsForSharedTravelLord()
        {
            Assert.True(LeadYourPetRules.ShouldSuppressTravelAutoInteraction(false, true, true));
            Assert.False(LeadYourPetRules.ShouldSuppressTravelAutoInteraction(false, true, false));
            Assert.False(LeadYourPetRules.ShouldSuppressTravelAutoInteraction(true, true, true));
        }


        [Fact]
        public void AllowsRimTalkDialogueForProtectedLeashedYoungRatkin()
        {
            Assert.False(LeadYourPetRules.ShouldBlockExternalDialogue(
                isProtectedLeashedYoungRatkin: true,
                integrationName: "RimTalk"));
        }

        [Fact]
        public void AllowsRimTalkConversationWhenOnlyYoungRatkinTargetFilterRejectedIt()
        {
            Assert.True(LeadYourPetRules.ShouldAllowRimTalkYoungRatkinConversation(
                originalAllowed: false,
                targetIsYoungRatkinPawn: true,
                initiatorValid: true,
                targetValid: true,
                initiatorCanTalk: true,
                initiatorIsRimTalkPlayer: false,
                initiatorCanReachTarget: true));
        }

        [Fact]
        public void DoesNotAllowRimTalkConversationForInvalidYoungRatkinTarget()
        {
            Assert.False(LeadYourPetRules.ShouldAllowRimTalkYoungRatkinConversation(
                originalAllowed: false,
                targetIsYoungRatkinPawn: true,
                initiatorValid: true,
                targetValid: false,
                initiatorCanTalk: true,
                initiatorIsRimTalkPlayer: false,
                initiatorCanReachTarget: true));
        }

        [Fact]
        public void DoesNotOverrideExistingRimTalkConversationDecision()
        {
            Assert.False(LeadYourPetRules.ShouldAllowRimTalkYoungRatkinConversation(
                originalAllowed: true,
                targetIsYoungRatkinPawn: true,
                initiatorValid: true,
                targetValid: true,
                initiatorCanTalk: true,
                initiatorIsRimTalkPlayer: false,
                initiatorCanReachTarget: true));
        }

        [Fact]
        public void TreatsValidYoungRatkinPawnAsRimTalkEligible()
        {
            Assert.True(LeadYourPetRules.ShouldTreatYoungRatkinAsRimTalkEligible(
                originalEligible: false,
                isYoungRatkinPawn: true,
                pawnValid: true));
        }

        [Fact]
        public void DoesNotTreatInvalidYoungRatkinPawnAsRimTalkEligible()
        {
            Assert.False(LeadYourPetRules.ShouldTreatYoungRatkinAsRimTalkEligible(
                originalEligible: false,
                isYoungRatkinPawn: true,
                pawnValid: false));
        }

        [Fact]
        public void ClearsFactionFromGeneratedTravelYoungRatkin()
        {
            Assert.True(LeadYourPetRules.ShouldClearGeneratedTravelYoungRatkinFaction(
                hasGeneratedPawn: true,
                hasFaction: true));
            Assert.False(LeadYourPetRules.ShouldClearGeneratedTravelYoungRatkinFaction(
                hasGeneratedPawn: true,
                hasFaction: false));
            Assert.False(LeadYourPetRules.ShouldClearGeneratedTravelYoungRatkinFaction(
                hasGeneratedPawn: false,
                hasFaction: true));
        }

        [Fact]
        public void BoughtTravelYoungRatkinPrisonerStartsWithNoResistanceAndLowWill()
        {
            Assert.Equal(0f, LeadYourPetRules.ResolveTravelYoungRatkinPrisonerResistance());
            Assert.InRange(LeadYourPetRules.ResolveTravelYoungRatkinPrisonerWill(), 0f, 0.199999f);
        }

        [Fact]
        public void OrdinaryTravelCaravanAlwaysTargetsTwoYoungRatkinPets()
        {
            Assert.Equal(2, LeadYourPetRules.ResolveOrdinaryTravelYoungRatkinTargetCount(
                traderLord: true,
                hasEligibleAdults: true));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelYoungRatkinTargetCount(
                traderLord: false,
                hasEligibleAdults: true));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelYoungRatkinTargetCount(
                traderLord: true,
                hasEligibleAdults: false));
        }
        [Fact]
        public void AcceptsAgeInsideConfiguredRangeAndRejectsBothEdges()
        {
            Assert.True(LeadYourPetRules.IsWithinPetAgeRange(0f, 0, 14));
            Assert.True(LeadYourPetRules.IsWithinPetAgeRange(13.99f, 0, 14));
            Assert.False(LeadYourPetRules.IsWithinPetAgeRange(14f, 0, 14));
            Assert.False(LeadYourPetRules.IsWithinPetAgeRange(2.9f, 3, 14));
            Assert.True(LeadYourPetRules.IsWithinPetAgeRange(3f, 3, 14));
        }

        [Fact]
        public void OrdinaryVisitorsLeadConfiguredYoungRatkinCountWhenEnabled()
        {
            Assert.Equal(2, LeadYourPetRules.ResolveOrdinaryTravelYoungRatkinTargetCount(
                traderLord: false,
                visitorLord: true,
                hasEligibleAdults: true,
                configuredCount: 2,
                visitorsLeadRatkinYoung: true));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelYoungRatkinTargetCount(
                traderLord: true,
                visitorLord: false,
                hasEligibleAdults: true,
                configuredCount: 4,
                visitorsLeadRatkinYoung: false));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelYoungRatkinTargetCount(
                traderLord: true,
                visitorLord: false,
                hasEligibleAdults: true,
                configuredCount: 0,
                visitorsLeadRatkinYoung: true));
        }

        [Fact]
        public void SettingsClampKeepsAgeOrderAndTravelCountBounds()
        {
            int min = 40;
            int max = 2;
            int count = 99;
            int leash = 1;
            int distance = 0;

            LeadYourPetRules.ClampPetControlSettings(ref min, ref max, ref count, ref leash, ref distance);

            Assert.Equal(40, min);
            Assert.Equal(40, max);
            Assert.Equal(LeadYourPetSettings.MaxOrdinaryTravelRatkinYoungCount, count);
            Assert.Equal(3, leash);
            Assert.Equal(1, distance);
        }

        [Fact]
        public void OverridesBabyCryMoodForYoungRatkinPawnOnly()
        {
            Assert.True(LeadYourPetRules.ShouldOverrideBabyCryMoodForYoungRatkin(
                sourceIsYoungRatkinPawn: true,
                sourceIsYoungRatkinPet: true,
                hearerIsYoungRatkinPawn: false,
                hearerCanReceiveMood: true));
            Assert.False(LeadYourPetRules.ShouldOverrideBabyCryMoodForYoungRatkin(
                sourceIsYoungRatkinPawn: false,
                sourceIsYoungRatkinPet: true,
                hearerIsYoungRatkinPawn: false,
                hearerCanReceiveMood: true));
            Assert.False(LeadYourPetRules.ShouldOverrideBabyCryMoodForYoungRatkin(
                sourceIsYoungRatkinPawn: true,
                sourceIsYoungRatkinPet: true,
                hearerIsYoungRatkinPawn: true,
                hearerCanReceiveMood: true));
            Assert.False(LeadYourPetRules.ShouldOverrideBabyCryMoodForYoungRatkin(
                sourceIsYoungRatkinPawn: true,
                sourceIsYoungRatkinPet: false,
                hearerIsYoungRatkinPawn: false,
                hearerCanReceiveMood: true));
        }

        [Fact]
        public void DoesNotBlockUnrelatedExternalJob()
        {
            Assert.False(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedYoungRatkin: true,
                jobDefName: "Goto"));
        }

        [Fact]
        public void DoesNotOfferManualYoungRatkinOneWayMenu()
        {
            Assert.False(LeadYourPetRules.ShouldOfferManualYoungRatkinInteractionMenu(
                ownedByActor: true,
                isBidirectionalMenu: false,
                bidirectionalBlocked: false));
        }

        [Fact]
        public void KeepsManualYoungRatkinTwoWayMenuWhenNotBlocked()
        {
            Assert.True(LeadYourPetRules.ShouldOfferManualYoungRatkinInteractionMenu(
                ownedByActor: true,
                isBidirectionalMenu: true,
                bidirectionalBlocked: false));
        }

        [Fact]
        public void PausesLeashProcessingWhenCarriedPetCouldNotBeDroppedYet()
        {
            Assert.True(LeadYourPetRules.ShouldPauseLeashForCarriedPet(
                petWasCarried: true,
                dropSucceeded: false,
                petStillUnavailable: true));
        }

        [Fact]
        public void TreatsPlayerLeashingFriendlyVisitorYoungRatkinAsAggressive()
        {
            Assert.True(LeadYourPetRules.ShouldTreatVisitorYoungRatkinPetLeashAsAggressive(
                masterIsPlayer: true,
                petIsRatkinHumanlike: true,
                petIsBabyOrChild: true,
                petFactionExists: true,
                petFactionIsPlayer: false,
                petFactionHostileToPlayer: false,
                petInFriendlyVisitorLord: true));

            Assert.False(LeadYourPetRules.ShouldTreatVisitorYoungRatkinPetLeashAsAggressive(
                masterIsPlayer: true,
                petIsRatkinHumanlike: true,
                petIsBabyOrChild: true,
                petFactionExists: true,
                petFactionIsPlayer: true,
                petFactionHostileToPlayer: false,
                petInFriendlyVisitorLord: true));
        }
    }
}
