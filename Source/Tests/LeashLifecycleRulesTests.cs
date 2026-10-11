using Xunit;

namespace LeadYourPet.Tests
{
    public class LeashLifecycleRulesTests
    {
        [Fact]
        public void AllowsRatkinMotherLeashWhenCoreRequirementsAreMet()
        {
            Assert.True(LeadYourPetRules.CanFormOrMaintainRatkinMotherLeash(
                motherIsRatkinHumanlike: true,
                babyIsRatkinHumanlike: true,
                motherIsDead: false,
                babyIsDead: false,
                babyIsBabyStage: true,
                motherIsBabyStage: false,
                hasParentRelation: true));
        }

        [Fact]
        public void RejectsRatkinMotherLeashWhenBabyGrowsOutOfBabyStage()
        {
            Assert.False(LeadYourPetRules.CanFormOrMaintainRatkinMotherLeash(
                motherIsRatkinHumanlike: true,
                babyIsRatkinHumanlike: true,
                motherIsDead: false,
                babyIsDead: false,
                babyIsBabyStage: false,
                motherIsBabyStage: false,
                hasParentRelation: true));
        }

        [Fact]
        public void RejectsRatkinMotherLeashWhenParentRelationIsMissing()
        {
            Assert.False(LeadYourPetRules.CanFormOrMaintainRatkinMotherLeash(
                motherIsRatkinHumanlike: true,
                babyIsRatkinHumanlike: true,
                motherIsDead: false,
                babyIsDead: false,
                babyIsBabyStage: true,
                motherIsBabyStage: false,
                hasParentRelation: false));
        }

        [Fact]
        public void BlocksPlayerTakeoverFromNonPlayerLeash()
        {
            Assert.True(LeadYourPetRules.ShouldRejectPlayerTakeoverOfNonPlayerLeash(
                hasExistingLink: true,
                existingMasterIsDifferent: true,
                existingMasterIsPlayer: false,
                newMasterIsPlayer: true));
        }

        [Fact]
        public void AllowsReplacingOwnExistingLeash()
        {
            Assert.False(LeadYourPetRules.ShouldRejectPlayerTakeoverOfNonPlayerLeash(
                hasExistingLink: true,
                existingMasterIsDifferent: false,
                existingMasterIsPlayer: false,
                newMasterIsPlayer: true));
        }

        [Fact]
        public void AllowsReplacingWhenExistingMasterIsPlayer()
        {
            Assert.False(LeadYourPetRules.ShouldRejectPlayerTakeoverOfNonPlayerLeash(
                hasExistingLink: true,
                existingMasterIsDifferent: true,
                existingMasterIsPlayer: true,
                newMasterIsPlayer: true));
        }

        [Fact]
        public void BreaksLeashWhenMasterAndPetAreNotOnSameSpawnedMap()
        {
            Assert.True(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: false,
                masterAndPetOnSameMap: false,
                masterDead: false,
                petDead: false,
                masterDowned: false,
                petDowned: false,
                allowImmobilePetDowned: false,
                masterInMentalState: false,
                masterIsCaravanMember: false,
                exemptTravelMouseEggCaravanLeash: false,
                playerMasterExitsMapOnArrival: false));
        }

        [Fact]
        public void BreaksLeashWhenMasterIsDowned()
        {
            Assert.True(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: true,
                masterAndPetOnSameMap: true,
                masterDead: false,
                petDead: false,
                masterDowned: true,
                petDowned: false,
                allowImmobilePetDowned: false,
                masterInMentalState: false,
                masterIsCaravanMember: false,
                exemptTravelMouseEggCaravanLeash: false,
                playerMasterExitsMapOnArrival: false));
        }

        [Fact]
        public void KeepsLeashForImmobileMouseEggEvenWhenPetIsDowned()
        {
            Assert.False(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: true,
                masterAndPetOnSameMap: true,
                masterDead: false,
                petDead: false,
                masterDowned: false,
                petDowned: true,
                allowImmobilePetDowned: true,
                masterInMentalState: false,
                masterIsCaravanMember: false,
                exemptTravelMouseEggCaravanLeash: false,
                playerMasterExitsMapOnArrival: false));
        }

        [Fact]
        public void BreaksLeashWhenMasterIsInMentalState()
        {
            Assert.True(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: true,
                masterAndPetOnSameMap: true,
                masterDead: false,
                petDead: false,
                masterDowned: false,
                petDowned: false,
                allowImmobilePetDowned: false,
                masterInMentalState: true,
                masterIsCaravanMember: false,
                exemptTravelMouseEggCaravanLeash: false,
                playerMasterExitsMapOnArrival: false));
        }

        [Fact]
        public void BreaksLeashWhenPlayerMasterIsLeavingMap()
        {
            Assert.True(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: true,
                masterAndPetOnSameMap: true,
                masterDead: false,
                petDead: false,
                masterDowned: false,
                petDowned: false,
                allowImmobilePetDowned: false,
                masterInMentalState: false,
                masterIsCaravanMember: false,
                exemptTravelMouseEggCaravanLeash: false,
                playerMasterExitsMapOnArrival: true));
        }

        [Fact]
        public void KeepsPlayerLeashDuringCaravanExitPreparation()
        {
            Assert.False(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: true,
                masterAndPetOnSameMap: true,
                masterDead: false,
                petDead: false,
                masterDowned: false,
                petDowned: false,
                allowImmobilePetDowned: false,
                masterInMentalState: false,
                masterIsCaravanMember: false,
                exemptTravelMouseEggCaravanLeash: false,
                playerMasterExitsMapOnArrival: true,
                preservePlayerCaravanLeash: true));
        }

        [Fact]
        public void KeepsNonPlayerTravelMouseEggLeashForCaravanMaster()
        {
            Assert.False(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: true,
                masterAndPetOnSameMap: true,
                masterDead: false,
                petDead: false,
                masterDowned: false,
                petDowned: false,
                allowImmobilePetDowned: false,
                masterInMentalState: false,
                masterIsCaravanMember: true,
                exemptTravelMouseEggCaravanLeash: true,
                playerMasterExitsMapOnArrival: false));
        }

        [Fact]
        public void StillBreaksNormalLeashForCaravanMaster()
        {
            Assert.True(LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: false,
                petDestroyed: false,
                masterSpawned: true,
                petSpawned: true,
                masterAndPetOnSameMap: true,
                masterDead: false,
                petDead: false,
                masterDowned: false,
                petDowned: false,
                allowImmobilePetDowned: false,
                masterInMentalState: false,
                masterIsCaravanMember: true,
                exemptTravelMouseEggCaravanLeash: false,
                playerMasterExitsMapOnArrival: false));
        }


        [Fact]
        public void UsesExpectedAnimationDurationForWhirl()
        {
            Assert.Equal(90, LeadYourPetRules.GetInteractionAnimationDuration(LeadYourPetInteractionKind.Whirl));
        }

        [Fact]
        public void UsesExpectedAnimationDurationForFed()
        {
            Assert.Equal(60, LeadYourPetRules.GetInteractionAnimationDuration(LeadYourPetInteractionKind.Fed));
        }

        [Fact]
        public void UsesDefaultAnimationDurationForUnknownInteraction()
        {
            Assert.Equal(45, LeadYourPetRules.GetInteractionAnimationDuration((LeadYourPetInteractionKind)999));
        }

        [Fact]
        public void BlocksNonPlayerMotherLeashFromReplacingDifferentNonPlayerLeash()
        {
            Assert.True(LeadYourPetRules.ShouldRejectNonPlayerLeashReplacement(
                hasExistingLink: true,
                existingMasterIsDifferent: true,
                existingMasterIsPlayer: false));
        }

        [Fact]
        public void AllowsNonPlayerMotherLeashToKeepSameExistingMaster()
        {
            Assert.False(LeadYourPetRules.ShouldRejectNonPlayerLeashReplacement(
                hasExistingLink: true,
                existingMasterIsDifferent: false,
                existingMasterIsPlayer: false));
        }

        [Fact]
        public void AllowsReplacingWhenExistingMasterIsPlayerForMotherLeashRule()
        {
            Assert.False(LeadYourPetRules.ShouldRejectNonPlayerLeashReplacement(
                hasExistingLink: true,
                existingMasterIsDifferent: true,
                existingMasterIsPlayer: true));
        }
    }
}
