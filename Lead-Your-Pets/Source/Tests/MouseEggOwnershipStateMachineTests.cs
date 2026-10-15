using Xunit;

namespace LeadYourPet.Tests
{
    public class MouseEggOwnershipStateMachineTests
    {
        [Fact]
        public void ClearsTravelStateAfterPurchase()
        {
            MouseEggOwnershipSnapshot current = new MouseEggOwnershipSnapshot(
                isPet: true,
                isTravelStock: true,
                sellAsPrisoner: true,
                hasCurrentMaster: true);

            MouseEggOwnershipSnapshot next = MouseEggOwnershipStateMachine.ResolveBoughtTravelMouseEggState(current);

            Assert.False(next.IsPet);
            Assert.False(next.IsTravelStock);
            Assert.False(next.SellAsPrisoner);
            Assert.False(next.HasCurrentMaster);
        }

        [Fact]
        public void DetachesNonPlayerLeashAfterPlayerBuysTravelMouseEgg()
        {
            Assert.True(MouseEggOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: false,
                petIsPlayerOwned: true,
                petIsColonyPrisoner: false,
                isTravelStock: true));
        }

        [Fact]
        public void KeepsLeashWhenPawnWasNotTravelStock()
        {
            Assert.False(MouseEggOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: false,
                petIsPlayerOwned: true,
                petIsColonyPrisoner: false,
                isTravelStock: false));
        }

        [Fact]
        public void PreservesPawnIdentityWhenRegisteringTravelMouseEggBeforePurchase()
        {
            Assert.False(MouseEggOwnershipStateMachine.ShouldClearPawnFactionForTravelRegistration(
                preserveOwnership: false,
                pawnSameFactionAsCarrier: true));

            Assert.False(MouseEggOwnershipStateMachine.ShouldForceGuestSlaveForTravelRegistration(
                preserveOwnership: false,
                hasGuestTracker: true));
        }

        [Fact]
        public void HandlesBoughtTravelMouseEggOnlyForPlayerBuysOfTravelStock()
        {
            Assert.True(MouseEggOwnershipStateMachine.ShouldHandleBoughtTravelMouseEgg(
                actionIsPlayerBuys: true,
                wasTravelStock: true,
                wasSellable: true));

            Assert.False(MouseEggOwnershipStateMachine.ShouldHandleBoughtTravelMouseEgg(
                actionIsPlayerBuys: true,
                wasTravelStock: false,
                wasSellable: true));

            Assert.False(MouseEggOwnershipStateMachine.ShouldHandleBoughtTravelMouseEgg(
                actionIsPlayerBuys: false,
                wasTravelStock: true,
                wasSellable: true));
        }

        [Fact]
        public void TraderAcceptsYoungRatkinEvenWhenTheKindDoesNotTradeHumans()
        {
            Assert.True(LeadYourPetRules.TraderWillTradeYoungRatkin(kindWillTrade: false, isTradeableYoungRatkin: true));
            Assert.False(LeadYourPetRules.TraderWillTradeYoungRatkin(kindWillTrade: false, isTradeableYoungRatkin: false));
            Assert.True(LeadYourPetRules.TraderWillTradeYoungRatkin(kindWillTrade: true, isTradeableYoungRatkin: false));
        }

        [Fact]
        public void BoughtYoungRatkinSlaveFallsBackWithoutIdeology()
        {
            Assert.Equal(BoughtYoungRatkinStatus.Prisoner, LeadYourPetRules.ResolveBoughtYoungRatkinStatus(BoughtYoungRatkinStatus.Slave, ideologyActive: false));
            Assert.Equal(BoughtYoungRatkinStatus.Slave, LeadYourPetRules.ResolveBoughtYoungRatkinStatus(BoughtYoungRatkinStatus.Slave, ideologyActive: true));
            Assert.Equal(BoughtYoungRatkinStatus.Colonist, LeadYourPetRules.ResolveBoughtYoungRatkinStatus(BoughtYoungRatkinStatus.Colonist, ideologyActive: false));
        }

        [Fact]
        public void FixedAnchorStaysStillInsideTheLeash()
        {
            Assert.True(LeadYourPetRules.ShouldKeepFixedAnchorStill(anchorIsFixed: true, distance: 8f, leashLength: 10f, dragging: false));
            Assert.False(LeadYourPetRules.ShouldKeepFixedAnchorStill(anchorIsFixed: true, distance: 11f, leashLength: 10f, dragging: false));
            Assert.False(LeadYourPetRules.ShouldKeepFixedAnchorStill(anchorIsFixed: false, distance: 4f, leashLength: 10f, dragging: false));
            Assert.False(LeadYourPetRules.ShouldKeepFixedAnchorStill(anchorIsFixed: true, distance: 4f, leashLength: 10f, dragging: true));
        }
    }
}
