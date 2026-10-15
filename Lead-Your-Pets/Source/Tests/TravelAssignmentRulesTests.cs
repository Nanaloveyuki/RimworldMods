using Xunit;

namespace LeadYourPet.Tests
{
    public class TravelAssignmentRulesTests
    {
        [Fact]
        public void BlocksCarryForProtectedMouseEggEvenWhenColonistCarryWouldOtherwiseBeAllowed()
        {
            Assert.True(LeadYourPetRules.ShouldBlockProtectedCarry(
                isProtectedLeashedMouseEgg: true,
                allowColonistCarry: true));
        }

        [Theory]
        [InlineData(true, true, true, true)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, false, false)]
        [InlineData(false, true, true, false)]
        public void AllowsOnlyMatchingPrisonerTransferToCarryProtectedMouseEgg(
            bool isProtectedLeashedMouseEgg,
            bool jobMakesTargetPrisoner,
            bool jobTargetMatchesPawn,
            bool expected)
        {
            Assert.Equal(expected, LeadYourPetRules.ShouldAllowProtectedPawnPrisonerTransfer(
                isProtectedLeashedMouseEgg,
                jobMakesTargetPrisoner,
                jobTargetMatchesPawn));
        }

        [Fact]
        public void TreatsLeashedMouseEggChildAsProtected()
        {
            Assert.True(LeadYourPetRules.ShouldTreatAsProtectedLeashedMouseEgg(
                isMouseEgg: true,
                isBaby: false,
                isChild: true,
                hasProtectedLink: true,
                hasProtectedState: false));
        }

        [Fact]
        public void TreatsAnyHumanlikePawnUnderFourteenAsMouseEggCompatible()
        {
            Assert.True(LeadYourPetRules.ShouldTreatAsMouseEgg(
                isRatkinHumanlike: false,
                isBaby: false,
                isChild: false,
                hasMeaningfulState: false,
                isTravelMouseEgg: false,
                isHumanlikeJuvenile: true));
        }

        [Fact]
        public void TreatsHumanlikeJuvenileAsProtectedWhenLeashed()
        {
            Assert.True(LeadYourPetRules.ShouldTreatAsProtectedLeashedMouseEgg(
                isMouseEgg: true,
                isBaby: false,
                isChild: false,
                hasProtectedLink: true,
                hasProtectedState: false,
                isHumanlikeJuvenile: true));
        }

        [Fact]
        public void DropsCarriedTravelMouseEggAfterRegistrationWhenCarrierIsSpawned()
        {
            Assert.True(LeadYourPetRules.ShouldDropCarriedTravelMouseEggAfterRegistration(
                registrationSucceeded: true,
                carrierSpawned: true,
                eggAlreadySpawned: false));
        }

        [Fact]
        public void RegistersCarriedTravelMouseEggOnlyAfterDropToGroundSucceeds()
        {
            Assert.True(LeadYourPetRules.ShouldRegisterDroppedTravelMouseEgg(
                hadCarriedMouseEgg: true,
                dropSucceeded: true,
                droppedPawnValid: true));

            Assert.False(LeadYourPetRules.ShouldRegisterDroppedTravelMouseEgg(
                hadCarriedMouseEgg: true,
                dropSucceeded: false,
                droppedPawnValid: true));

            Assert.False(LeadYourPetRules.ShouldRegisterDroppedTravelMouseEgg(
                hadCarriedMouseEgg: true,
                dropSucceeded: true,
                droppedPawnValid: false));
        }

        [Fact]
        public void SkipsRandomTravelMouseEggFallbackWhenExistingCarriedEggWasAlreadyHandled()
        {
            Assert.True(LeadYourPetRules.ShouldSkipRandomTravelMouseEggFallback(
                hadCarriedMouseEgg: true,
                carriedEggRegistrationSucceeded: false));

            Assert.False(LeadYourPetRules.ShouldSkipRandomTravelMouseEggFallback(
                hadCarriedMouseEgg: false,
                carriedEggRegistrationSucceeded: false));
        }

        [Fact]
        public void DropsPawnImmediatelyAfterItBecomesProtectedMouseEggPetWhileCarried()
        {
            Assert.True(LeadYourPetRules.ShouldDropCarriedPawnAfterBecomingMouseEggPet(
                isProtectedMouseEggPet: true,
                pawnHasCarrier: true,
                carrierHasCarryTracker: true,
                carrierIsCarryingPawn: true));
        }

        [Fact]
        public void DoesNotDropCarriedPawnWhenItIsNotProtectedMouseEggPet()
        {
            Assert.False(LeadYourPetRules.ShouldDropCarriedPawnAfterBecomingMouseEggPet(
                isProtectedMouseEggPet: false,
                pawnHasCarrier: true,
                carrierHasCarryTracker: true,
                carrierIsCarryingPawn: true));
        }

        [Fact]
        public void AllowsNearbyMouseDisasterChildFallbackWhenChildIsUnownedAndNearTrader()
        {
            Assert.True(LeadYourPetRules.IsEligibleNearbyMouseDisasterTravelChild(
                isTravelChild: true,
                isSpawned: true,
                isDead: false,
                sameFactionAsTraderLord: true,
                hasNoLord: true,
                isNearTrader: true));
        }

        [Fact]
        public void RejectsNearbyMouseDisasterChildFallbackWhenChildAlreadyHasLord()
        {
            Assert.False(LeadYourPetRules.IsEligibleNearbyMouseDisasterTravelChild(
                isTravelChild: true,
                isSpawned: true,
                isDead: false,
                sameFactionAsTraderLord: true,
                hasNoLord: false,
                isNearTrader: true));
        }

        [Fact]
        public void BlocksRimTalkToddlerPickupForProtectedLeashedMouseEgg()
        {
            Assert.True(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedMouseEgg: true,
                jobDefName: "RimTalk_PickUpToddler"));
        }

        [Fact]
        public void BlocksRimTalkBeingCarriedStateForProtectedLeashedMouseEgg()
        {
            Assert.True(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedMouseEgg: true,
                jobDefName: "RimTalk_BeingCarried_Idle"));
        }

        [Theory]
        [InlineData("PlayCrib")]
        [InlineData("BePlayedWith")]
        [InlineData("BringBabyToSafety")]
        [InlineData("PutInCrib")]
        [InlineData("CYB_BatheToddler")]
        public void BlocksToddlersExpandedHoldJobsForProtectedLeashedMouseEgg(string jobDefName)
        {
            Assert.True(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedMouseEgg: true,
                jobDefName: jobDefName));
        }

        [Theory]
        [InlineData("Toddlers.JobDriver_BePlayedWith")]
        [InlineData("Toddlers.JobDriver_PlayCrib")]
        [InlineData("RimWorld.JobDriver_BabyPlay")]
        public void EndsToddlersExpandedHoldDriversForProtectedLeashedMouseEgg(string driverTypeName)
        {
            Assert.True(LeadYourPetRules.ShouldEndExternalToddlerHoldJob(
                isProtectedLeashedMouseEgg: true,
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
        public void AllowsRimTalkDialogueForProtectedLeashedMouseEgg()
        {
            Assert.False(LeadYourPetRules.ShouldBlockExternalDialogue(
                isProtectedLeashedMouseEgg: true,
                integrationName: "RimTalk"));
        }

        [Fact]
        public void AllowsRimTalkConversationWhenOnlyMouseEggTargetFilterRejectedIt()
        {
            Assert.True(LeadYourPetRules.ShouldAllowRimTalkMouseEggConversation(
                originalAllowed: false,
                targetIsMouseEggPawn: true,
                initiatorValid: true,
                targetValid: true,
                initiatorCanTalk: true,
                initiatorIsRimTalkPlayer: false,
                initiatorCanReachTarget: true));
        }

        [Fact]
        public void DoesNotAllowRimTalkConversationForInvalidMouseEggTarget()
        {
            Assert.False(LeadYourPetRules.ShouldAllowRimTalkMouseEggConversation(
                originalAllowed: false,
                targetIsMouseEggPawn: true,
                initiatorValid: true,
                targetValid: false,
                initiatorCanTalk: true,
                initiatorIsRimTalkPlayer: false,
                initiatorCanReachTarget: true));
        }

        [Fact]
        public void DoesNotOverrideExistingRimTalkConversationDecision()
        {
            Assert.False(LeadYourPetRules.ShouldAllowRimTalkMouseEggConversation(
                originalAllowed: true,
                targetIsMouseEggPawn: true,
                initiatorValid: true,
                targetValid: true,
                initiatorCanTalk: true,
                initiatorIsRimTalkPlayer: false,
                initiatorCanReachTarget: true));
        }

        [Fact]
        public void TreatsValidMouseEggPawnAsRimTalkEligible()
        {
            Assert.True(LeadYourPetRules.ShouldTreatMouseEggAsRimTalkEligible(
                originalEligible: false,
                isMouseEggPawn: true,
                pawnValid: true));
        }

        [Fact]
        public void DoesNotTreatInvalidMouseEggPawnAsRimTalkEligible()
        {
            Assert.False(LeadYourPetRules.ShouldTreatMouseEggAsRimTalkEligible(
                originalEligible: false,
                isMouseEggPawn: true,
                pawnValid: false));
        }

        [Fact]
        public void ClearsFactionFromGeneratedTravelMouseEgg()
        {
            Assert.True(LeadYourPetRules.ShouldClearGeneratedTravelMouseEggFaction(
                hasGeneratedPawn: true,
                hasFaction: true));
            Assert.False(LeadYourPetRules.ShouldClearGeneratedTravelMouseEggFaction(
                hasGeneratedPawn: true,
                hasFaction: false));
            Assert.False(LeadYourPetRules.ShouldClearGeneratedTravelMouseEggFaction(
                hasGeneratedPawn: false,
                hasFaction: true));
        }

        [Fact]
        public void BoughtTravelMouseEggPrisonerStartsWithNoResistanceAndLowWill()
        {
            Assert.Equal(0f, LeadYourPetRules.ResolveTravelMouseEggPrisonerResistance());
            Assert.InRange(LeadYourPetRules.ResolveTravelMouseEggPrisonerWill(), 0f, 0.199999f);
        }

        [Fact]
        public void OrdinaryTravelCaravanAlwaysTargetsTwoMouseEggPets()
        {
            Assert.Equal(2, LeadYourPetRules.ResolveOrdinaryTravelMouseEggTargetCount(
                traderLord: true,
                hasEligibleAdults: true));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelMouseEggTargetCount(
                traderLord: false,
                hasEligibleAdults: true));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelMouseEggTargetCount(
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
        public void OrdinaryVisitorsLeadConfiguredMouseEggCountWhenEnabled()
        {
            Assert.Equal(2, LeadYourPetRules.ResolveOrdinaryTravelMouseEggTargetCount(
                traderLord: false,
                visitorLord: true,
                hasEligibleAdults: true,
                configuredCount: 2,
                visitorsLeadRatkinYoung: true));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelMouseEggTargetCount(
                traderLord: true,
                visitorLord: false,
                hasEligibleAdults: true,
                configuredCount: 4,
                visitorsLeadRatkinYoung: false));
            Assert.Equal(0, LeadYourPetRules.ResolveOrdinaryTravelMouseEggTargetCount(
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
        public void OverridesBabyCryMoodForMouseEggPawnOnly()
        {
            Assert.True(LeadYourPetRules.ShouldOverrideBabyCryMoodForMouseEgg(
                sourceIsMouseEggPawn: true,
                sourceIsMouseEggPet: true,
                hearerIsMouseEggPawn: false,
                hearerCanReceiveMood: true));
            Assert.False(LeadYourPetRules.ShouldOverrideBabyCryMoodForMouseEgg(
                sourceIsMouseEggPawn: false,
                sourceIsMouseEggPet: true,
                hearerIsMouseEggPawn: false,
                hearerCanReceiveMood: true));
            Assert.False(LeadYourPetRules.ShouldOverrideBabyCryMoodForMouseEgg(
                sourceIsMouseEggPawn: true,
                sourceIsMouseEggPet: true,
                hearerIsMouseEggPawn: true,
                hearerCanReceiveMood: true));
            Assert.False(LeadYourPetRules.ShouldOverrideBabyCryMoodForMouseEgg(
                sourceIsMouseEggPawn: true,
                sourceIsMouseEggPet: false,
                hearerIsMouseEggPawn: false,
                hearerCanReceiveMood: true));
        }

        [Fact]
        public void DoesNotBlockUnrelatedExternalJob()
        {
            Assert.False(LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                isProtectedLeashedMouseEgg: true,
                jobDefName: "Goto"));
        }

        [Fact]
        public void DoesNotOfferManualMouseEggOneWayMenu()
        {
            Assert.False(LeadYourPetRules.ShouldOfferManualMouseEggInteractionMenu(
                ownedByActor: true,
                isBidirectionalMenu: false,
                bidirectionalBlocked: false));
        }

        [Fact]
        public void KeepsManualMouseEggTwoWayMenuWhenNotBlocked()
        {
            Assert.True(LeadYourPetRules.ShouldOfferManualMouseEggInteractionMenu(
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
        public void TreatsPlayerLeashingFriendlyVisitorMouseEggAsAggressive()
        {
            Assert.True(LeadYourPetRules.ShouldTreatVisitorMouseEggPetLeashAsAggressive(
                masterIsPlayer: true,
                petIsRatkinHumanlike: true,
                petIsBabyOrChild: true,
                petFactionExists: true,
                petFactionIsPlayer: false,
                petFactionHostileToPlayer: false,
                petInFriendlyVisitorLord: true));

            Assert.False(LeadYourPetRules.ShouldTreatVisitorMouseEggPetLeashAsAggressive(
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
