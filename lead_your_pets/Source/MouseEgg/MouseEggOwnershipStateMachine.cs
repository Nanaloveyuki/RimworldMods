namespace LeadYourPet
{
    public static class MouseEggOwnershipStateMachine
    {
        public static MouseEggOwnershipSnapshot ResolveBoughtTravelMouseEggState(MouseEggOwnershipSnapshot current)
        {
            return new MouseEggOwnershipSnapshot(
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

        public static bool ShouldHandleBoughtTravelMouseEgg(bool actionIsPlayerBuys, bool wasTravelStock, bool wasSellable)
        {
            return actionIsPlayerBuys && wasTravelStock && wasSellable;
        }
    }
}
