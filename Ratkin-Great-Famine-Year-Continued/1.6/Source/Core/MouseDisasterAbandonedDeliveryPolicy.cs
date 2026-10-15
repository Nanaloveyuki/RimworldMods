namespace MouseDisaster
{
    public static class MouseDisasterAbandonedDeliveryPolicy
    {
        public const int AdultArrivalStallTimeoutTicks = 3 * 60 * 60;
        public const float InfantAgeYears = 3f;
        public const float AdultDropoffRadius = 3f;
        public const float ChildDropoffRadius = 3f;
        public const float ChildReliefDropoffRadius = 8f;

        public static bool ShouldWaitAtDropoff()
        {
            return false;
        }

        public static bool ShouldAdultLeaveWhenChildrenAreOnMap(bool adultCarryingChild, int spawnedChildCount)
        {
            return !adultCarryingChild && spawnedChildCount > 0;
        }

        public static bool ShouldUseLeadYourPetDropoff(bool leadYourPetEnabled, int childCount)
        {
            return ShouldWaitAtDropoff() && leadYourPetEnabled && childCount > 0;
        }

        public static bool ShouldSpawnChildrenAtDropoff(bool leadYourPetEnabled)
        {
            return false;
        }

        public static bool ShouldUseVanillaCarryDelivery(bool leadYourPetEnabled, bool allChildrenArrived)
        {
            return ShouldWaitAtDropoff() && !leadYourPetEnabled && !allChildrenArrived;
        }

        public static bool ShouldCarryUndeliveredChild(bool leadYourPetEnabled, bool allChildrenArrived, bool childDowned)
        {
            return ShouldUseVanillaCarryDelivery(leadYourPetEnabled, allChildrenArrived) && childDowned;
        }

        public static bool ShouldGiveChildSelfGoto(bool allChildrenArrived, bool childDowned)
        {
            return ShouldWaitAtDropoff() && !allChildrenArrived && !childDowned;
        }

        public static float DropoffRadius(bool forAdult, bool dropoffInReliefArea, bool pawnInReliefArea)
        {
            if (forAdult)
            {
                return AdultDropoffRadius;
            }

            return dropoffInReliefArea && pawnInReliefArea ? ChildReliefDropoffRadius : ChildDropoffRadius;
        }

        public static bool IsDropoffSatisfied(bool nearFoodCell)
        {
            return nearFoodCell;
        }

        public static bool ShouldRegisterAsVisitorChoiceTargets()
        {
            return false;
        }

        public static bool ShouldTryAnyFoodFallback(bool foundHomeAreaFood, bool foundAnyFoodThing)
        {
            return !foundHomeAreaFood && foundAnyFoodThing;
        }

        public static bool ShouldFallbackToStoredFoodCell(bool foundHomeAreaFood, bool foundAnyFoodThing)
        {
            return !foundHomeAreaFood && !foundAnyFoodThing;
        }

        public static bool ShouldReleaseLeadYourPetDropoffLeashes(bool leadYourPetEnabled, bool adultLeaving)
        {
            return leadYourPetEnabled && adultLeaving;
        }

        public static bool ShouldForceAdultLeaveAfterArrivalStall(bool adultArrivedAtDropoff, int ticksWaitingSinceArrival)
        {
            return adultArrivedAtDropoff && ticksWaitingSinceArrival >= AdultArrivalStallTimeoutTicks;
        }

        public static bool ShouldForceAdultLeaveAfterApproachStall(bool adultArrivedAtDropoff, int ticksSinceStart)
        {
            return !adultArrivedAtDropoff && ticksSinceStart >= AdultArrivalStallTimeoutTicks;
        }

        public static bool ShouldForceDismissMobileNonInfant(bool settingEnabled, bool canMove, float ageYears)
        {
            return settingEnabled && canMove && ageYears >= InfantAgeYears;
        }
    }
}
