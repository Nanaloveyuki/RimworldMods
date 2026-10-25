namespace LeadYourPet
{
    public static class LeadYourPetRules
    {
        public static bool ShouldProcessLinkOnTick(int currentTick, int bucketCount, int linkBucket, bool forceImmediate = false)
        {
            if (forceImmediate)
            {
                return true;
            }

            if (bucketCount <= 1)
            {
                return true;
            }

            if (currentTick % 6 != 0)
            {
                return false;
            }

            return (currentTick / 6) % bucketCount == linkBucket;
        }

        public static bool ShouldRunExpensiveLinkUpdate(int currentTick, int lastExpensiveUpdateTick, int minIntervalTicks, bool forceImmediate)
        {
            if (forceImmediate)
            {
                return true;
            }

            return currentTick - lastExpensiveUpdateTick >= minIntervalTicks;
        }

        public static bool ShouldIssueFollowPath(int currentTick, int lastFollowIssueTick, int minIntervalTicks, bool forceImmediate)
        {
            if (forceImmediate)
            {
                return true;
            }

            return currentTick - lastFollowIssueTick >= minIntervalTicks;
        }

        public static bool ShouldEvaluateTradeableYoungRatkin(bool kindWillTrade, bool isPawn)
        {
            return !kindWillTrade && isPawn;
        }

        public static bool ShouldPrepareTravelDeparture(int currentTick, int lastPreparedTick, int minIntervalTicks, bool forceImmediate, bool departing)
        {
            if (!departing)
            {
                return false;
            }

            if (forceImmediate)
            {
                return true;
            }

            return currentTick - lastPreparedTick >= minIntervalTicks;
        }

        public static bool TraderWillTradeYoungRatkin(bool kindWillTrade, bool isTradeableYoungRatkin)
        {
            return kindWillTrade || isTradeableYoungRatkin;
        }

        public static BoughtYoungRatkinStatus ResolveBoughtYoungRatkinStatus(BoughtYoungRatkinStatus configured, bool ideologyActive)
        {
            if (configured == BoughtYoungRatkinStatus.Slave && !ideologyActive)
            {
                return BoughtYoungRatkinStatus.Prisoner;
            }

            return configured == BoughtYoungRatkinStatus.Colonist
                || configured == BoughtYoungRatkinStatus.Slave
                ? configured
                : BoughtYoungRatkinStatus.Prisoner;
        }

        public static bool ShouldUseBidirectionalYoungRatkinAutoInteractions(bool playerControlled, bool canUseSingleDirection, bool masterSleeping)
        {
            return playerControlled && !canUseSingleDirection && !masterSleeping;
        }

        public static bool ShouldRefreshAutoInteractionPool(
            int currentTick,
            int lastPoolTick,
            int cacheIntervalTicks,
            bool cacheValid,
            bool cachedPlayerControlled,
            bool playerControlled,
            bool cachedCanUseSingleDirection,
            bool canUseSingleDirection,
            bool cachedMasterSleeping,
            bool masterSleeping)
        {
            if (!cacheValid)
            {
                return true;
            }

            if (cachedPlayerControlled != playerControlled
                || cachedCanUseSingleDirection != canUseSingleDirection
                || cachedMasterSleeping != masterSleeping)
            {
                return true;
            }

            return currentTick - lastPoolTick >= cacheIntervalTicks;
        }

        public static bool ShouldTreatAsYoungRatkin(bool isRatkinHumanlike, bool isBaby, bool isChild, bool hasMeaningfulState, bool isTravelYoungRatkin, bool isHumanlikeJuvenile = false)
        {
            return (isRatkinHumanlike || isHumanlikeJuvenile)
                && (isBaby || isChild || isHumanlikeJuvenile || hasMeaningfulState || isTravelYoungRatkin);
        }

        public static bool ShouldBlockProtectedCarry(bool isProtectedLeashedYoungRatkin, bool allowColonistCarry)
        {
            return isProtectedLeashedYoungRatkin;
        }

        public static bool ShouldAllowProtectedPawnPrisonerTransfer(
            bool isProtectedLeashedYoungRatkin,
            bool jobMakesTargetPrisoner,
            bool jobTargetMatchesPawn)
        {
            return isProtectedLeashedYoungRatkin
                && jobMakesTargetPrisoner
                && jobTargetMatchesPawn;
        }

        public static bool ShouldTreatAsProtectedLeashedYoungRatkin(bool isYoungRatkin, bool isBaby, bool isChild, bool hasProtectedLink, bool hasProtectedState, bool isHumanlikeJuvenile = false)
        {
            return isYoungRatkin && (isBaby || isChild || isHumanlikeJuvenile) && (hasProtectedLink || hasProtectedState);
        }

        public static bool ShouldDropCarriedTravelYoungRatkinAfterRegistration(bool registrationSucceeded, bool carrierSpawned, bool eggAlreadySpawned)
        {
            return registrationSucceeded && carrierSpawned && !eggAlreadySpawned;
        }

        public static bool ShouldRegisterDroppedTravelYoungRatkin(bool hadCarriedYoungRatkin, bool dropSucceeded, bool droppedPawnValid)
        {
            return hadCarriedYoungRatkin && dropSucceeded && droppedPawnValid;
        }

        public static bool ShouldSkipRandomTravelYoungRatkinFallback(bool hadCarriedYoungRatkin, bool carriedEggRegistrationSucceeded)
        {
            return hadCarriedYoungRatkin || carriedEggRegistrationSucceeded;
        }

        public static bool ShouldDropCarriedPawnAfterBecomingYoungRatkinPet(bool isProtectedYoungRatkinPet, bool pawnHasCarrier, bool carrierHasCarryTracker, bool carrierIsCarryingPawn)
        {
            return isProtectedYoungRatkinPet
                && pawnHasCarrier
                && carrierHasCarryTracker
                && carrierIsCarryingPawn;
        }

        public static bool ShouldClearGeneratedTravelYoungRatkinFaction(bool hasGeneratedPawn, bool hasFaction)
        {
            return hasGeneratedPawn && hasFaction;
        }

        public static float ResolveTravelYoungRatkinPrisonerResistance()
        {
            return 0f;
        }

        public static float ResolveTravelYoungRatkinPrisonerWill()
        {
            return 0.1f;
        }

        public static int ResolveOrdinaryTravelYoungRatkinTargetCount(bool traderLord, bool hasEligibleAdults)
        {
            return ResolveOrdinaryTravelYoungRatkinTargetCount(traderLord, false, hasEligibleAdults, 2, true);
        }

        public static int ResolveOrdinaryTravelYoungRatkinTargetCount(bool traderLord, bool visitorLord, bool hasEligibleAdults, int configuredCount, bool visitorsLeadRatkinYoung)
        {
            if (!visitorsLeadRatkinYoung || !hasEligibleAdults || configuredCount <= 0)
            {
                return 0;
            }

            if (!traderLord && !visitorLord)
            {
                return 0;
            }

            return configuredCount;
        }

        public static bool IsWithinPetAgeRange(float biologicalAgeYears, int minAgeYears, int maxAgeYears)
        {
            if (biologicalAgeYears < 0f)
            {
                return false;
            }

            int min = minAgeYears < 0 ? 0 : minAgeYears;
            int max = maxAgeYears < min ? min : maxAgeYears;
            return biologicalAgeYears >= min && biologicalAgeYears < max;
        }

        public static void ClampPetControlSettings(ref int minPetAgeYears, ref int maxPetAgeYears, ref int ordinaryTravelRatkinYoungCount, ref int maxLeashLength, ref int maxYoungRatkinPetLeashStartDistance)
        {
            maxLeashLength = maxLeashLength < 3 ? 3 : (maxLeashLength > 30 ? 30 : maxLeashLength);
            maxYoungRatkinPetLeashStartDistance = maxYoungRatkinPetLeashStartDistance < 1 ? 1 : (maxYoungRatkinPetLeashStartDistance > 30 ? 30 : maxYoungRatkinPetLeashStartDistance);
            if (minPetAgeYears < 0)
            {
                minPetAgeYears = 0;
            }

            if (minPetAgeYears > LeadYourPetSettings.AbsoluteMaxPetAgeYears)
            {
                minPetAgeYears = LeadYourPetSettings.AbsoluteMaxPetAgeYears;
            }

            if (maxPetAgeYears < minPetAgeYears)
            {
                maxPetAgeYears = minPetAgeYears;
            }

            if (maxPetAgeYears > LeadYourPetSettings.AbsoluteMaxPetAgeYears)
            {
                maxPetAgeYears = LeadYourPetSettings.AbsoluteMaxPetAgeYears;
            }

            if (ordinaryTravelRatkinYoungCount < 0)
            {
                ordinaryTravelRatkinYoungCount = 0;
            }

            if (ordinaryTravelRatkinYoungCount > LeadYourPetSettings.MaxOrdinaryTravelRatkinYoungCount)
            {
                ordinaryTravelRatkinYoungCount = LeadYourPetSettings.MaxOrdinaryTravelRatkinYoungCount;
            }
        }

        public static LeashedYoungRatkinDutyState ResolveLeashedYoungRatkinDutyState(bool masterSleeping, bool masterEating)
        {
            if (masterEating)
            {
                return LeashedYoungRatkinDutyState.WaitForFeeding;
            }

            if (masterSleeping)
            {
                return LeashedYoungRatkinDutyState.SleepAtLeash;
            }

            return LeashedYoungRatkinDutyState.FollowMaster;
        }

        public static float ResolveSharedNutritionFromMasterMeal(float nutritionIngested)
        {
            if (nutritionIngested <= 0f)
            {
                return 0f;
            }

            return nutritionIngested * 0.5f;
        }

        public static float ResolveFedNutritionLevel(float currentNutrition, float maxNutrition, float gainedNutrition)
        {
            if (maxNutrition <= 0f)
            {
                return 0f;
            }

            if (gainedNutrition <= 0f)
            {
                return currentNutrition;
            }

            return currentNutrition + gainedNutrition > maxNutrition
                ? maxNutrition
                : currentNutrition + gainedNutrition;
        }

        public static bool ShouldAppendCustomYoungRatkinDutyInspectLine(bool hasCustomReport, bool inspectStringAlreadyContainsReport)
        {
            return hasCustomReport && !inspectStringAlreadyContainsReport;
        }

        public static bool CanStartYoungRatkinPetLeashAtDistance(bool sameMap, float distance, int maxDistance)
        {
            return sameMap && distance <= maxDistance;
        }

        public static YoungRatkinSpecialSource ResolveTravelYoungRatkinSpecialSource(bool isRatkinDisasterTraderAdult, bool isDefendPointLord, bool sellAsPrisoner)
        {
            return isRatkinDisasterTraderAdult && isDefendPointLord && !sellAsPrisoner
                ? YoungRatkinSpecialSource.RatkinDisasterChildExchange
                : YoungRatkinSpecialSource.None;
        }

        public static bool IsEligibleNearbyRatkinDisasterTravelChild(bool isTravelChild, bool isSpawned, bool isDead, bool sameFactionAsTraderLord, bool hasNoLord, bool isNearTrader)
        {
            return isTravelChild
                && isSpawned
                && !isDead
                && sameFactionAsTraderLord
                && hasNoLord
                && isNearTrader;
        }

        public static bool ShouldBlockExternalToddlerPickup(bool isProtectedLeashedYoungRatkin, string jobDefName)
        {
            if (!isProtectedLeashedYoungRatkin || string.IsNullOrEmpty(jobDefName))
            {
                return false;
            }

            return jobDefName == "RimTalk_PickUpToddler"
                || jobDefName.StartsWith("RimTalk_BeingCarried", System.StringComparison.OrdinalIgnoreCase)
                || jobDefName == "PlayCrib"
                || jobDefName == "BePlayedWith"
                || jobDefName == "BringBabyToSafety"
                || jobDefName == "PutInCrib"
                || jobDefName == "LetOutOfCrib"
                || jobDefName == "CYB_BatheToddler"
                || jobDefName == "CYB_WashBaby";
        }

        public static bool IsExternalToddlerHoldJob(string jobDefName)
        {
            return ShouldBlockExternalToddlerPickup(true, jobDefName);
        }

        public static bool IsExternalToddlerHoldDriver(string driverTypeName)
        {
            if (string.IsNullOrEmpty(driverTypeName))
            {
                return false;
            }

            return driverTypeName == "RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_BeingCarried"
                || driverTypeName == "RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_BeingCarried_Idle"
                || driverTypeName == "RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_BeingCarried_Observe"
                || driverTypeName == "RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_BeingCarried_Sleep"
                || driverTypeName == "RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_BeingCarried_DiaperChange"
                || driverTypeName == "RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_BeingCarried_Struggle"
                || driverTypeName == "RimTalk_ToddlersExpansion.Integration.Toddlers.JobDriver_PickUpToddler"
                || ShouldEndExternalToddlerHoldJob(true, driverTypeName);
        }

        public static bool ShouldPreserveMapExitJob(bool currentJobExitsMap, bool currentDutyExitsMap, bool lordToilExitsMap)
        {
            return currentJobExitsMap || currentDutyExitsMap || lordToilExitsMap;
        }

        public static bool ShouldSuppressTravelAutoInteraction(bool masterIsPlayer, bool petIsTravelStock, bool petSharesMasterLord)
        {
            return !masterIsPlayer && petIsTravelStock && petSharesMasterLord;
        }

        public static bool ShouldEndExternalToddlerHoldJob(bool isProtectedLeashedYoungRatkin, string driverTypeName)
        {
            if (!isProtectedLeashedYoungRatkin || string.IsNullOrEmpty(driverTypeName))
            {
                return false;
            }

            return driverTypeName == "Toddlers.JobDriver_BePlayedWith"
                || driverTypeName == "Toddlers.JobDriver_PlayCrib"
                || driverTypeName == "RimWorld.JobDriver_BabyPlay";
        }

        public static bool ShouldOfferManualYoungRatkinInteractionMenu(bool ownedByActor, bool isBidirectionalMenu, bool bidirectionalBlocked)
        {
            return ownedByActor && isBidirectionalMenu && !bidirectionalBlocked;
        }


        public static bool ShouldBlockExternalDialogue(bool isProtectedLeashedYoungRatkin, string integrationName)
        {
            return false;
        }

        public static bool ShouldTreatYoungRatkinAsRimTalkEligible(bool originalEligible, bool isYoungRatkinPawn, bool pawnValid)
        {
            return !originalEligible && isYoungRatkinPawn && pawnValid;
        }

        public static bool ShouldAllowRimTalkYoungRatkinConversation(bool originalAllowed, bool targetIsYoungRatkinPawn, bool initiatorValid, bool targetValid, bool initiatorCanTalk, bool initiatorIsRimTalkPlayer, bool initiatorCanReachTarget)
        {
            if (originalAllowed || !targetIsYoungRatkinPawn || !initiatorValid || !targetValid || !initiatorCanTalk)
            {
                return false;
            }

            return initiatorIsRimTalkPlayer || initiatorCanReachTarget;
        }

        public static bool ShouldOverrideBabyCryMoodForYoungRatkin(bool sourceIsYoungRatkinPawn, bool sourceIsYoungRatkinPet, bool hearerIsYoungRatkinPawn, bool hearerCanReceiveMood)
        {
            return sourceIsYoungRatkinPawn && sourceIsYoungRatkinPet && !hearerIsYoungRatkinPawn && hearerCanReceiveMood;
        }

        public static bool ShouldPauseLeashForCarriedPet(bool petWasCarried, bool dropSucceeded, bool petStillUnavailable)
        {
            return petWasCarried && (!dropSucceeded || petStillUnavailable);
        }

        public static YoungRatkinSpecialSource ResolveMotherLeashSpecialSource(YoungRatkinSpecialSource currentSource, bool motherIsRatkinDisasterBeggarAdult, bool hasParentRelation)
        {
            if (currentSource != YoungRatkinSpecialSource.None)
            {
                return currentSource;
            }

            return motherIsRatkinDisasterBeggarAdult && hasParentRelation
                ? YoungRatkinSpecialSource.RatkinDisasterBeggarFamily
                : YoungRatkinSpecialSource.None;
        }

        public static bool CanFormOrMaintainRatkinMotherLeash(bool motherIsRatkinHumanlike, bool babyIsRatkinHumanlike, bool motherIsDead, bool babyIsDead, bool babyIsBabyStage, bool motherIsBabyStage, bool hasParentRelation)
        {
            if (!motherIsRatkinHumanlike || !babyIsRatkinHumanlike)
            {
                return false;
            }

            if (motherIsDead || babyIsDead)
            {
                return false;
            }

            if (!babyIsBabyStage)
            {
                return false;
            }

            if (motherIsBabyStage)
            {
                return false;
            }

            return hasParentRelation;
        }

        public static bool ShouldBreakNonPlayerLeashAfterAcquisition(bool hasLink, bool masterIsPlayer, bool petIsPlayerOwned, bool petIsColonyPrisoner, bool isTravelStock)
        {
            return hasLink && !masterIsPlayer && isTravelStock && (petIsPlayerOwned || petIsColonyPrisoner);
        }

        public static bool ShouldRejectPlayerTakeoverOfNonPlayerLeash(bool hasExistingLink, bool existingMasterIsDifferent, bool existingMasterIsPlayer, bool newMasterIsPlayer)
        {
            return hasExistingLink && existingMasterIsDifferent && !existingMasterIsPlayer && newMasterIsPlayer;
        }

        public static bool ShouldRejectNonPlayerLeashReplacement(bool hasExistingLink, bool existingMasterIsDifferent, bool existingMasterIsPlayer)
        {
            return hasExistingLink && existingMasterIsDifferent && !existingMasterIsPlayer;
        }

        public static bool ShouldTreatVisitorYoungRatkinPetLeashAsAggressive(
            bool masterIsPlayer,
            bool petIsRatkinHumanlike,
            bool petIsBabyOrChild,
            bool petFactionExists,
            bool petFactionIsPlayer,
            bool petFactionHostileToPlayer,
            bool petInFriendlyVisitorLord)
        {
            return masterIsPlayer
                && petIsRatkinHumanlike
                && petIsBabyOrChild
                && petFactionExists
                && !petFactionIsPlayer
                && !petFactionHostileToPlayer
                && petInFriendlyVisitorLord;
        }

        public static bool ShouldBreakLeashLifecycle(bool masterDestroyed, bool petDestroyed, bool masterSpawned, bool petSpawned, bool masterAndPetOnSameMap, bool masterDead, bool petDead, bool masterDowned, bool petDowned, bool allowImmobilePetDowned, bool masterInMentalState, bool masterIsCaravanMember, bool exemptTravelYoungRatkinCaravanLeash, bool playerMasterExitsMapOnArrival, bool preservePlayerCaravanLeash = false)
        {
            if (masterDestroyed || petDestroyed)
            {
                return true;
            }

            if (!masterSpawned || !petSpawned || !masterAndPetOnSameMap)
            {
                return true;
            }

            if (masterDead || petDead || masterDowned)
            {
                return true;
            }

            if (petDowned && !allowImmobilePetDowned)
            {
                return true;
            }

            if (masterInMentalState)
            {
                return true;
            }

            if (masterIsCaravanMember && !exemptTravelYoungRatkinCaravanLeash)
            {
                return true;
            }

            if (playerMasterExitsMapOnArrival && !preservePlayerCaravanLeash)
            {
                return true;
            }

            return false;
        }

        public static int GetInteractionAnimationDuration(LeadYourPetInteractionKind kind)
        {
            switch (kind)
            {
                case LeadYourPetInteractionKind.Observe:
                case LeadYourPetInteractionKind.RestAtFeet:
                case LeadYourPetInteractionKind.IdleNearOwner:
                case LeadYourPetInteractionKind.StudyFloor:
                case LeadYourPetInteractionKind.SillySmile:
                case LeadYourPetInteractionKind.OwnerWaited:
                    return 75;
                case LeadYourPetInteractionKind.Fed:
                case LeadYourPetInteractionKind.Snack:
                case LeadYourPetInteractionKind.TailPetting:
                case LeadYourPetInteractionKind.EarPetting:
                    return 60;
                case LeadYourPetInteractionKind.PullTail:
                case LeadYourPetInteractionKind.PullEar:
                    return 120;
                case LeadYourPetInteractionKind.Slap:
                    return 60;
                case LeadYourPetInteractionKind.Kick:
                    return 36;
                case LeadYourPetInteractionKind.KickButt:
                    return 45;
                case LeadYourPetInteractionKind.Whirl:
                    return 90;
                default:
                    return 45;
            }
        }

        public static bool ShouldHaveSpecialMotherMood(bool hasParentRelation, bool childLeashedByMother, YoungRatkinSpecialSource source, YoungRatkinSpecialSource expectedSource)
        {
            return hasParentRelation && childLeashedByMother && source == expectedSource;
        }

        public static bool ShouldHaveSpecialBabyMood(bool hasParentRelation, bool leashedByMother, YoungRatkinSpecialSource source, YoungRatkinSpecialSource expectedSource)
        {
            return hasParentRelation && leashedByMother && source == expectedSource;
        }
    }
}
