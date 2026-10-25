using Xunit;

namespace LeadYourPet.Tests
{
    public class YoungRatkinOwnershipStateMachineTests
    {
        [Fact]
        public void ClearsTravelStateAfterPurchase()
        {
            YoungRatkinOwnershipSnapshot current = new YoungRatkinOwnershipSnapshot(
                isPet: true,
                isTravelStock: true,
                sellAsPrisoner: true,
                hasCurrentMaster: true);

            YoungRatkinOwnershipSnapshot next = YoungRatkinOwnershipStateMachine.ResolveBoughtTravelYoungRatkinState(current);

            Assert.False(next.IsPet);
            Assert.False(next.IsTravelStock);
            Assert.False(next.SellAsPrisoner);
            Assert.False(next.HasCurrentMaster);
        }

        [Fact]
        public void DetachesNonPlayerLeashAfterPlayerBuysTravelYoungRatkin()
        {
            Assert.True(YoungRatkinOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: false,
                petIsPlayerOwned: true,
                petIsColonyPrisoner: false,
                isTravelStock: true));
        }

        [Fact]
        public void KeepsLeashWhenPawnWasNotTravelStock()
        {
            Assert.False(YoungRatkinOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: false,
                petIsPlayerOwned: true,
                petIsColonyPrisoner: false,
                isTravelStock: false));
        }

        [Fact]
        public void PreservesPawnIdentityWhenRegisteringTravelYoungRatkinBeforePurchase()
        {
            Assert.False(YoungRatkinOwnershipStateMachine.ShouldClearPawnFactionForTravelRegistration(
                preserveOwnership: false,
                pawnSameFactionAsCarrier: true));

            Assert.False(YoungRatkinOwnershipStateMachine.ShouldForceGuestSlaveForTravelRegistration(
                preserveOwnership: false,
                hasGuestTracker: true));
        }

        [Fact]
        public void HandlesBoughtTravelYoungRatkinOnlyForPlayerBuysOfTravelStock()
        {
            Assert.True(YoungRatkinOwnershipStateMachine.ShouldHandleBoughtTravelYoungRatkin(
                actionIsPlayerBuys: true,
                wasTravelStock: true,
                wasSellable: true));

            Assert.False(YoungRatkinOwnershipStateMachine.ShouldHandleBoughtTravelYoungRatkin(
                actionIsPlayerBuys: true,
                wasTravelStock: false,
                wasSellable: true));

            Assert.False(YoungRatkinOwnershipStateMachine.ShouldHandleBoughtTravelYoungRatkin(
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

    }
}
