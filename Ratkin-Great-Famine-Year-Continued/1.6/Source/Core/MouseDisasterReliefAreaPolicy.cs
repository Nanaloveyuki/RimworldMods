namespace MouseDisaster
{
    public static class MouseDisasterReliefAreaPolicy
    {
        public static bool CanUseReliefFood(bool isMouseDisasterFoodConsumer, bool isPlayerColonist, bool isScriptedDelivery = false)
        {
            return isMouseDisasterFoodConsumer && !isPlayerColonist && !isScriptedDelivery;
        }

        public static bool ShouldAllowFoodTypeInReliefAreaSearch(
            bool isNutritionGivingIngestible,
            bool isIngestibleNow,
            bool isHarvestablePlant,
            bool isCorpse,
            bool allowCorpse)
        {
            if (isHarvestablePlant)
            {
                return true;
            }

            if (isNutritionGivingIngestible && isIngestibleNow)
            {
                return true;
            }

            if (isCorpse)
            {
                return true;
            }

            return false;
        }

        public static bool ShouldAllowMealSourceBuildingInReliefAreaSearch(bool isMealSourceBuilding)
        {
            return isMealSourceBuilding;
        }

        public static bool ShouldAllowRawFoodAsMinimumPreferability(bool desperate, bool eaterNonHumanlikeOrWildMan, bool eaterDoesNotMindRawFood)
        {
            return true;
        }

        public static bool ShouldUseMealSourceInteractionFlow(bool isMealSourceBuilding)
        {
            return isMealSourceBuilding;
        }

        public static bool ShouldUseReliefAreaFoodFirst(bool shouldPrioritizeReliefArea, bool hasReliefFood, bool fallbackFoodExists)
        {
            return shouldPrioritizeReliefArea && hasReliefFood;
        }

        public static bool ShouldFallbackToDefaultFoodSearch(bool shouldPrioritizeReliefArea, bool hasReliefFood)
        {
            return !shouldPrioritizeReliefArea || !hasReliefFood;
        }
    }
}
