namespace LeadYourPet
{
    public static class YoungRatkinOwnershipStateMachine
    {
        public static YoungRatkinOwnershipSnapshot ResolveBoughtTravelYoungRatkinState(YoungRatkinOwnershipSnapshot current)
        {
            return new YoungRatkinOwnershipSnapshot(
                isPet: false,
                isTravelStock: false,
                sellAsPrisoner: false,
                hasCurrentMaster: false);
        }

        public static bool ShouldDetachNonPlayerTravelLeashAfterAcquisition(bool masterIsPlayer, bool petIsPlayerOwned, bool petIsColonyPrisoner, bool isTravelStock)
        {
            return LeadYourPetRules.ShouldBreakNonPlayerLeashAfterAcquisition(
                hasLink: true,
                masterIsPlayer: masterIsPlayer,
                petIsPlayerOwned: petIsPlayerOwned,
                petIsColonyPrisoner: petIsColonyPrisoner,
                isTravelStock: isTravelStock);
        }

        public static bool ShouldClearPawnFactionForTravelRegistration(bool preserveOwnership, bool pawnSameFactionAsCarrier)
        {
            return false;
        }

        public static bool ShouldForceGuestSlaveForTravelRegistration(bool preserveOwnership, bool hasGuestTracker)
        {
            return false;
        }

        public static bool ShouldHandleBoughtTravelYoungRatkin(bool actionIsPlayerBuys, bool wasTravelStock, bool wasSellable)
        {
            return actionIsPlayerBuys && wasTravelStock && wasSellable;
        }
    }
}
