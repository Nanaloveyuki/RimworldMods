using System;
using System.Collections.Generic;
using HungerAndHavoc.Generation;
using Verse;

namespace HungerAndHavoc.Core
{
    public class RHAH_Settings : ModSettings
    {
        public bool enableNewContent = true;
        public bool optimizeGeneration = true;
        public float positiveIncidentDays = 15f;
        public float negativeIncidentDays = 15f;
        public string positiveIncidentPace = HungerAndHavoc.Incidents.RHAH_IncidentPace.DefaultFormula;
        public string negativeIncidentPace = HungerAndHavoc.Incidents.RHAH_IncidentPace.DefaultFormula;
        public float frequencyWindowDays = 15f;
        Dictionary<string, float> xenotypeWeights = new Dictionary<string, float>();
        List<string> enabledXenotypeDefNames = new List<string>();
        List<string> enabledGeneDefNames = new List<string>();
        public int litterMin = Generation.RHAH_FertilityRules.DefaultLitterMin;
        public int litterPeak = Generation.RHAH_FertilityRules.DefaultLitterPeak;
        public int litterMax = Generation.RHAH_FertilityRules.DefaultLitterMax;
        public float fertileMinAge = Generation.RHAH_FertilityRules.DefaultFertileAge;
        public float fertilityPercent = Generation.RHAH_FertilityRules.DefaultFertilityPercent;
        public float gestationDays = Generation.RHAH_FertilityRules.DefaultGestationDays;
        public float lovinPregnancyPercent = Generation.RHAH_FertilityRules.DefaultLovinPregnancyPercent;
        public float maternalMilkPercent = Pawn.RHAH_ParentalGenes.DefaultMaternalMilkPercent;
        public float maternalHungerPercent = Pawn.RHAH_ParentalGenes.DefaultMaternalHungerPercent;
        public float maternalNutritionPercent = Pawn.RHAH_ParentalGenes.DefaultMaternalNutritionPercent;
        public float paternalWorkPercent = Pawn.RHAH_ParentalGenes.DefaultPaternalWorkPercent;
        public float paternalHungerPercent = Pawn.RHAH_ParentalGenes.DefaultPaternalHungerPercent;
        public float paternalLearningPercent = Pawn.RHAH_ParentalGenes.DefaultPaternalLearningPercent;
        public float paternalDamagePercent = Pawn.RHAH_ParentalGenes.DefaultPaternalDamagePercent;
        public bool reliefEnabled = true;
        public bool allowEatOutsideRelief;
        public bool ignoreReliefAfterFed;
        public bool leaveAfterFed = true;
        List<string> disabledReliefFoodDefNames = new List<string>();
        List<string> disabledGiveFoodDefNames = new List<string>();
        List<string> disabledBegFoodDefNames = new List<string>();
        public int giveFoodListMode;
        public int begFoodListMode;
        public bool aidRequestsEnabled = true;
        public bool intelTradesEnabled = true;
        public bool visitorChoicesEnabled = true;
        public bool foodGiveHintDismissed;
        public bool traderIgnoresHarshEnvironment = true;
        public bool traderIgnoresEnclosedSpace = true;
        public bool famineYearEnabled;
        public float famineYearChancePercent = 40f;
        public float famineYearPawnMultiplier = 3f;
        public float famineYearDisasterChancePercent = 20f;
        public bool famineArrivalEnabled = true;
        public float famineArrivalMalnutrition = 0.25f;
        public bool famineMoodEnabled = true;
        public bool clayMoodEnabled = true;
        public bool famineTradeEnabled = true;
        public float famineFoodSaleFactor = 1.5f;
        public float famineGoodsBuyFactor = 0.75f;
        public bool armedDemandEnabled = true;
        public bool armedWeaponsEnabled = true;
        public bool spaceApproachEnabled;
        public bool childExchangeFoodSubstitution = true;
        public bool familyDropEnabled = true;
        public bool motherFeedEnabled = true;
        public bool prisonerScavengeEnabled = true;
        public bool tailBiteEnabled;
        public bool famineVisitorsOpenDoors;
        public bool plagueSafeMode;
        public bool greenIncidentLetters = true;
        public bool broadcastEnabled = true;
        public int broadcastCooldownDays = 3;
        public bool staggerGeneration = true;
        public bool refugeeCampEnabled = true;
        public float refugeePredationChancePercent = 10f;
        public bool refugeePredationFightBack = true;
        public bool outsidePredatorsFollowDifficulty;
        public bool pawnHistoriesEnabled = true;
        public bool pawnTraitsEnabled = true;
        List<string> disabledIncidentGroupIds = new List<string>();
        Dictionary<string, float> incidentGroupPoints = new Dictionary<string, float>();
        Dictionary<string, float> incidentGroupWeights = new Dictionary<string, float>();
        Dictionary<string, int> incidentGroupAttitudes = new Dictionary<string, int>();
        List<string> incidentGroupLongChains = DefaultIncidentLongChains();
        internal bool ObsoleteIncidentSettingsLoaded;
        List<string> disabledHistoryDisplayIds = new List<string>();
        List<string> disabledTraitDisplayIds = new List<string>();
        Dictionary<string, float> traitWeights = new Dictionary<string, float>();
        public int maxEventPawns = 30;
        public float minGeneratedAge;
        public float maxGeneratedAge = 50f;
        public float minMotherAge = Pawn.RHAH_VisitorRules.DefaultMotherMinAge;
        public bool youngAgeFollowsRange;
        public bool allowImmobileBabies;
        public int genderMode;
        public int femaleSharePercent = 50;
        public int playerIdeoPercent = 100;
        public int apparelMode;
        public int apparelListMode;
        List<string> disabledRefugeeApparelDefNames = new List<string>();
        public int maxOwnedTraits = 1;
        public bool allowVanillaTraits = true;
        public bool traitAgeFilter = true;
        public int contentListMode;
        public float reliefFoodScoreBonus = 0.1f;
        public bool fedWanderEnabled = true;
        public int fedWanderHours = 12;
        public bool waitWhenNoFood = true;
        public float noFoodWaitDays = 0.5f;
        public int shelterDays = 5;
        public int hireDays = 60 * 4;
        public bool shortStayLockApparel = true;
        public bool coldClothesEnabled = true;
        Dictionary<string, float> temperatureApparelInsulation = new Dictionary<string, float>();
        List<string> disabledTemperatureApparelDefNames = new List<string>();
        public float minimumEventTemperature = -35f;
        public float maximumEventTemperature = 70f;
        // 退休普通结局设置仅保留已发布键和值 不再暴露操作或参与行为
        public bool countEndingsWithoutNarrator = true;
        public bool endingsWithoutNarrator = true;
        public int endingAidGoal = 99;
        public int endingBroadcastGoal = 3;
        public int endingExpulsionLimit = 3;
        public int endingAdultGoal = 100;
        public int endingWaitDays = 30;
        public bool endingE01 = true;
        public bool endingE02 = true;
        public bool endingE03 = true;
        public bool endingE04 = true;
        public bool endingE05 = true;
        public bool endingIdentity = true;
        public bool plagueEnabled = true;
        public float plagueSpreadChancePerCarrier = 0.005f;
        public float plagueSpreadChanceCap = 0.30f;
        public int plagueSpreadDayInterval = 3;
        public int plagueSpreadHour = 6;
        public float plagueBloodPumpingSkipPercent = 120f;
        public bool beggingEnabled = true;
        public bool begAutoGiveEnabled = false;
        public int begFailCooldownHours = 3;
        public int begSlapChancePercent = 50;
        public int begSuccessChancePercent = 35;
        public int begSocialBonusPercent = 3;
        public bool stealingEnabled = true;
        public bool fightingEnabled = true;
        public bool gnawingEnabled = true;
        public bool batchTurnsHostile = true;
        public bool batchLeavesTogether = true;
        public bool suiYinThreatTempo = true;
        public float weightWild = 1.4f;
        public float weightBeggar = 1f;
        public float weightThief = 0.7f;
        public float weightTrade = 0.5f;
        public float weightSiege = 0.35f;
        public float weightAid = 0.25f;
        public float weightSpecial = 0.2f;
        public float weightIntel = 0.12f;
        public float weightSeason = 1.1f;
        public float weightPlague = 0.5f;
        public int requestMinSimple = 6;
        public int requestMaxSimple = 28;
        public int requestMinFine = 4;
        public int requestMaxFine = 18;
        public int requestMinMedicine = 2;
        public int requestMaxMedicine = 10;
        public int requestMinSilver = 80;
        public int requestMaxSilver = 1200;
        public int requestMinHerbal = 3;
        public int requestMaxHerbal = 15;
        public int requestDays = 1;
        public float requestPointScale = 300f;
        public int foodPerChild = 10;
        public int foodPerVisitor = 1;
        public int maxFoodRequest = 12;
        public int envoyMealCost = 6;
        public int campMinAdults = 2;
        public int campMaxAdults = 4;
        public int campMinChildren = 8;
        public int campMaxChildren = 16;
        public int campGoodwill = 12;
        public int campDays = 15;
        public int campHuts = 4;
        public int holeWoodCost = 20;
        public int holeCleanPortions = 5;
        public int holeLossRange = 12;
        public int holeMaxLosses = 3;
        public int apparelAwfulPercent = 35;
        public int apparelPoorPercent = 50;
        public int apparelMinDurabilityPercent = 10;
        public int apparelMaxDurabilityPercent = 60;
        public int apparelCorpsePercent = 25;
        public int apparelClothPercent = 65;
        public int apparelMaxPieces = 4;
        public int begFailMood = -5;
        public int begSuccessMood = 3;
        public int begSlapMood = -10;
        public int begSlapKnockoutHours = 0;
        public float begBruiseSeverity = 4f;
        public float begBruiseStep = 4f;
        public float begBruiseMax = 16f;
        public float barkNutrition = 0.25f;
        public float barkDamage = 12f;
        public float wallNutrition = 0.2f;
        public float wallDamage = 10f;
        public int clayMaxBites = 3;
        public int clayWindowDays = 15;
        public float claySeverityPerBite = 0.33f;
        public float childHungryPercent = 30f;
        public float prisonerHungryPercent = 20f;
        public float tailBiteAge = 3f;
        public float scavengeNutrition = 0.15f;
        public float tailNutrition = 0.35f;
        public float tailFailDamage = 4f;
        public float satisfiedFoodPercent = 82f;
        public float refeedMalnutrition = 0.4f;
        public float plagueSeverityMax = 0.1f;
        public int followPredatorPercent = 10;
        public int followBirthWatchDays = 3;
        public int followPlagueBirthDays = 5;
        public int followMotherReturnDays = 15;
        public int followLongReturnYears = 2;
        public float followAdultAge = 14f;
        public int followMoodScalePercent = 100;
        public int endingTrustFloor = 50;
        public int endingHopeTrust = 75;
        // 以下三项仅存读退休结局的已发布值
        public int endingHaltTrust = -75;
        public int endingLowKinds = 6;
        public float endingThreatDays = 13f;
        public int trustKill = -10;
        public int trustCaptive = -5;
        public int trustEntrustGood = 5;
        public int trustEntrustCaptive = -5;
        public int trustEntrustStory = -2;
        public int trustEntrustRegret = -3;
        public int trustEntrustBanished = -1;
        public int trustExchange = 3;
        public int trustHoleOpen = 1;
        public int trustHoleIgnore = -1;
        public int trustHoleBait = 3;
        public int trustEnvoyFail = -2;
        public int trustRelicFail = -2;
        public int trustHold = -1;
        public int trustDeliver = 1;
        public int trustLeave = 1;
        public int trustExpel = -1;
        public int narrativeRewardSilver = 300;
        public int narrativeRescueCost = 250;
        public int narrativeRescueReward = 2500;
        public int narrativeRelicTake = 200;
        public int narrativeRelicHand = 100;
        public int narrativeTrustBonusPercent = 25;
        public int narrativeMissingDays = 1;
        public int narrativeObserveDays = 30;
        public int narrativeHoleIgnoreDays = 3;
        public int narrativeEnvoyWaitDays = 3;
        public int narrativeEnvoyCheckDays = 1;
        public int narrativeRelicDays = 15;
        public int narrativeRevisitYears = 4;
        public int narrativeAsideCooldownDays = 3;
        public int narrativeAsideCutoff = -75;
        public int narrativeAdultYears = 18;
        public int narrativeTheftKinds = 2;
        public int narrativeProgressKinds = 3;
        public int narrativeRewardKinds = 8;
        public int narrativeEnvoyKinds = 5;
        public int narrativeRelicKinds = 8;

        static List<string> DefaultIncidentLongChains()
        {
            return new List<string> { "I-002", "I-012" };
        }

        internal void ResetToDefaults()
        {
            RHAH_Settings defaults = new RHAH_Settings();
            // 原地恢复全部配置 保留 Verse 持有的设置实例和 Mod 归属
            System.Reflection.FieldInfo[] fields = typeof(RHAH_Settings).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i].SetValue(this, fields[i].GetValue(defaults));
            }

            InvalidateReliefSearch();
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                System.Xml.XmlNode parent = Scribe.loader.curXmlParent;
                ObsoleteIncidentSettingsLoaded = parent?["disabledIncidentDisplayIds"] != null ||
                    parent?["incidentDebugPoints"] != null || parent?["incidentWeights"] != null ||
                    parent?["incidentAttitudes"] != null || parent?["incidentLongChains"] != null;
            }
            Scribe_Values.Look(ref enableNewContent, "enableNewContent", true);
            Scribe_Values.Look(ref optimizeGeneration, "optimizeGeneration", true);
            Scribe_Values.Look(ref positiveIncidentDays, "positiveIncidentDays", 15f);
            Scribe_Values.Look(ref negativeIncidentDays, "negativeIncidentDays", 15f);
            Scribe_Values.Look(ref positiveIncidentPace, "positiveIncidentPace", HungerAndHavoc.Incidents.RHAH_IncidentPace.DefaultFormula);
            Scribe_Values.Look(ref negativeIncidentPace, "negativeIncidentPace", HungerAndHavoc.Incidents.RHAH_IncidentPace.DefaultFormula);
            Scribe_Values.Look(ref frequencyWindowDays, "frequencyWindowDays", 15f);
            Scribe_Collections.Look(ref xenotypeWeights, "xenotypeWeights", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref enabledXenotypeDefNames, "enabledXenotypeDefNames", LookMode.Value);
            Scribe_Collections.Look(ref enabledGeneDefNames, "enabledGeneDefNames", LookMode.Value);
            Scribe_Values.Look(ref litterMin, "litterMin", Generation.RHAH_FertilityRules.DefaultLitterMin);
            Scribe_Values.Look(ref litterPeak, "litterPeak", Generation.RHAH_FertilityRules.DefaultLitterPeak);
            Scribe_Values.Look(ref litterMax, "litterMax", Generation.RHAH_FertilityRules.DefaultLitterMax);
            Scribe_Values.Look(ref fertileMinAge, "fertileMinAge", Generation.RHAH_FertilityRules.DefaultFertileAge);
            Scribe_Values.Look(ref fertilityPercent, "fertilityPercent", Generation.RHAH_FertilityRules.DefaultFertilityPercent);
            Scribe_Values.Look(ref gestationDays, "gestationDays", Generation.RHAH_FertilityRules.DefaultGestationDays);
            Scribe_Values.Look(ref lovinPregnancyPercent, "lovinPregnancyPercent", Generation.RHAH_FertilityRules.DefaultLovinPregnancyPercent);
            Scribe_Values.Look(ref maternalMilkPercent, "maternalMilkPercent", Pawn.RHAH_ParentalGenes.DefaultMaternalMilkPercent);
            Scribe_Values.Look(ref maternalHungerPercent, "maternalHungerPercent", Pawn.RHAH_ParentalGenes.DefaultMaternalHungerPercent);
            Scribe_Values.Look(ref maternalNutritionPercent, "maternalNutritionPercent", Pawn.RHAH_ParentalGenes.DefaultMaternalNutritionPercent);
            Scribe_Values.Look(ref paternalWorkPercent, "paternalWorkPercent", Pawn.RHAH_ParentalGenes.DefaultPaternalWorkPercent);
            Scribe_Values.Look(ref paternalHungerPercent, "paternalHungerPercent", Pawn.RHAH_ParentalGenes.DefaultPaternalHungerPercent);
            Scribe_Values.Look(ref paternalLearningPercent, "paternalLearningPercent", Pawn.RHAH_ParentalGenes.DefaultPaternalLearningPercent);
            Scribe_Values.Look(ref paternalDamagePercent, "paternalDamagePercent", Pawn.RHAH_ParentalGenes.DefaultPaternalDamagePercent);
            Scribe_Values.Look(ref reliefEnabled, "reliefEnabled", true);
            Scribe_Values.Look(ref allowEatOutsideRelief, "allowEatOutsideRelief", false);
            Scribe_Values.Look(ref ignoreReliefAfterFed, "ignoreReliefAfterFed", false);
            Scribe_Values.Look(ref leaveAfterFed, "leaveAfterFed", true);
            Scribe_Collections.Look(ref disabledReliefFoodDefNames, "disabledReliefFoodDefNames", LookMode.Value);
            Scribe_Collections.Look(ref disabledGiveFoodDefNames, "disabledGiveFoodDefNames", LookMode.Value);
            Scribe_Collections.Look(ref disabledBegFoodDefNames, "disabledBegFoodDefNames", LookMode.Value);
            Scribe_Values.Look(ref giveFoodListMode, "giveFoodListMode", 0);
            Scribe_Values.Look(ref begFoodListMode, "begFoodListMode", 0);
            Scribe_Values.Look(ref aidRequestsEnabled, "aidRequestsEnabled", true);
            Scribe_Values.Look(ref intelTradesEnabled, "intelTradesEnabled", true);
            Scribe_Values.Look(ref visitorChoicesEnabled, "visitorChoicesEnabled", true);
            Scribe_Values.Look(ref foodGiveHintDismissed, "foodGiveHintDismissed", false);
            Scribe_Values.Look(ref traderIgnoresHarshEnvironment, "traderIgnoresHarshEnvironment", true);
            Scribe_Values.Look(ref traderIgnoresEnclosedSpace, "traderIgnoresEnclosedSpace", true);
            Scribe_Values.Look(ref famineYearEnabled, "famineYearEnabled", false);
            Scribe_Values.Look(ref famineYearChancePercent, "famineYearChancePercent", 40f);
            Scribe_Values.Look(ref famineYearPawnMultiplier, "famineYearPawnMultiplier", 3f);
            Scribe_Values.Look(ref famineYearDisasterChancePercent, "famineYearDisasterChancePercent", 20f);
            Scribe_Values.Look(ref famineArrivalEnabled, "famineArrivalEnabled", true);
            Scribe_Values.Look(ref famineArrivalMalnutrition, "famineArrivalMalnutrition", 0.25f);
            Scribe_Values.Look(ref famineMoodEnabled, "famineMoodEnabled", true);
            Scribe_Values.Look(ref clayMoodEnabled, "clayMoodEnabled", true);
            Scribe_Values.Look(ref famineTradeEnabled, "famineTradeEnabled", true);
            Scribe_Values.Look(ref famineFoodSaleFactor, "famineFoodSaleFactor", 1.5f);
            Scribe_Values.Look(ref famineGoodsBuyFactor, "famineGoodsBuyFactor", 0.75f);
            Scribe_Values.Look(ref armedDemandEnabled, "armedDemandEnabled", true);
            Scribe_Values.Look(ref armedWeaponsEnabled, "armedWeaponsEnabled", true);
            Scribe_Values.Look(ref spaceApproachEnabled, "spaceApproachEnabled", false);
            Scribe_Values.Look(ref childExchangeFoodSubstitution, "childExchangeFoodSubstitution", true);
            Scribe_Values.Look(ref familyDropEnabled, "familyDropEnabled", true);
            Scribe_Values.Look(ref motherFeedEnabled, "motherFeedEnabled", true);
            Scribe_Values.Look(ref prisonerScavengeEnabled, "prisonerScavengeEnabled", true);
            Scribe_Values.Look(ref tailBiteEnabled, "tailBiteEnabled", false);
            Scribe_Values.Look(ref famineVisitorsOpenDoors, "famineVisitorsOpenDoors", false);
            Scribe_Values.Look(ref plagueSafeMode, "plagueSafeMode", false);
            Scribe_Values.Look(ref greenIncidentLetters, "greenIncidentLetters", true);
            Scribe_Values.Look(ref broadcastEnabled, "broadcastEnabled", true);
            Scribe_Values.Look(ref broadcastCooldownDays, "broadcastCooldownDays", 3);
            Scribe_Values.Look(ref staggerGeneration, "staggerGeneration", true);
            Scribe_Values.Look(ref refugeeCampEnabled, "refugeeCampEnabled", true);
            Scribe_Values.Look(ref refugeePredationChancePercent, "refugeePredationChancePercent", 10f);
            Scribe_Values.Look(ref refugeePredationFightBack, "refugeePredationFightBack", true);
            Scribe_Values.Look(ref outsidePredatorsFollowDifficulty, "outsidePredatorsFollowDifficulty", false);
            Scribe_Values.Look(ref pawnHistoriesEnabled, "pawnHistoriesEnabled", true);
            Scribe_Values.Look(ref pawnTraitsEnabled, "pawnTraitsEnabled", true);
            Scribe_Collections.Look(ref disabledIncidentGroupIds, "disabledIncidentGroupIds", LookMode.Value);
            Scribe_Collections.Look(ref incidentGroupPoints, "incidentGroupPoints", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref incidentGroupWeights, "incidentGroupWeights", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref incidentGroupAttitudes, "incidentGroupAttitudes", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref incidentGroupLongChains, "incidentGroupLongChains", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars &&
                Scribe.loader.curXmlParent?["incidentGroupLongChains"] == null)
            {
                incidentGroupLongChains = DefaultIncidentLongChains();
            }
            Scribe_Collections.Look(ref disabledHistoryDisplayIds, "disabledHistoryDisplayIds", LookMode.Value);
            Scribe_Collections.Look(ref disabledTraitDisplayIds, "disabledTraitDisplayIds", LookMode.Value);
            Scribe_Collections.Look(ref traitWeights, "traitWeights", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref maxEventPawns, "maxEventPawns", 30);
            Scribe_Values.Look(ref minGeneratedAge, "minGeneratedAge", 0f);
            Scribe_Values.Look(ref maxGeneratedAge, "maxGeneratedAge", 50f);
            Scribe_Values.Look(ref minMotherAge, "minMotherAge", Pawn.RHAH_VisitorRules.DefaultMotherMinAge);
            Scribe_Values.Look(ref youngAgeFollowsRange, "youngAgeFollowsRange", false);
            Scribe_Values.Look(ref allowImmobileBabies, "allowImmobileBabies", false);
            Scribe_Values.Look(ref genderMode, "genderMode", 0);
            Scribe_Values.Look(ref femaleSharePercent, "femaleSharePercent", 50);
            Scribe_Values.Look(ref playerIdeoPercent, "playerIdeoPercent", 100);
            Scribe_Values.Look(ref apparelMode, "apparelMode", 0);
            Scribe_Values.Look(ref apparelListMode, "apparelListMode", 0);
            Scribe_Collections.Look(ref disabledRefugeeApparelDefNames, "disabledRefugeeApparelDefNames", LookMode.Value);
            Scribe_Values.Look(ref maxOwnedTraits, "maxOwnedTraits", 1);
            Scribe_Values.Look(ref allowVanillaTraits, "allowVanillaTraits", true);
            Scribe_Values.Look(ref traitAgeFilter, "traitAgeFilter", true);
            Scribe_Values.Look(ref contentListMode, "contentListMode", 0);
            Scribe_Values.Look(ref reliefFoodScoreBonus, "reliefFoodScoreBonus", 0.1f);
            Scribe_Values.Look(ref fedWanderEnabled, "fedWanderEnabled", true);
            Scribe_Values.Look(ref fedWanderHours, "fedWanderHours", 12);
            Scribe_Values.Look(ref waitWhenNoFood, "waitWhenNoFood", true);
            Scribe_Values.Look(ref noFoodWaitDays, "noFoodWaitDays", 0.5f);
            Scribe_Values.Look(ref shelterDays, "shelterDays", 5);
            Scribe_Values.Look(ref hireDays, "hireDays", 60 * 4);
            Scribe_Values.Look(ref shortStayLockApparel, "shortStayLockApparel", true);
            Scribe_Values.Look(ref coldClothesEnabled, "coldClothesEnabled", true);
            Scribe_Collections.Look(ref temperatureApparelInsulation, "temperatureApparelInsulation", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref disabledTemperatureApparelDefNames, "disabledTemperatureApparelDefNames", LookMode.Value);
            Scribe_Values.Look(ref minimumEventTemperature, "minimumEventTemperature", -35f);
            Scribe_Values.Look(ref maximumEventTemperature, "maximumEventTemperature", 70f);
            Scribe_Values.Look(ref countEndingsWithoutNarrator, "countEndingsWithoutNarrator", true);
            Scribe_Values.Look(ref endingsWithoutNarrator, "endingsWithoutNarrator", true);
            Scribe_Values.Look(ref endingAidGoal, "endingAidGoal", 99);
            Scribe_Values.Look(ref endingBroadcastGoal, "endingBroadcastGoal", 3);
            Scribe_Values.Look(ref endingExpulsionLimit, "endingExpulsionLimit", 3);
            Scribe_Values.Look(ref endingAdultGoal, "endingAdultGoal", 100);
            Scribe_Values.Look(ref endingWaitDays, "endingWaitDays", 30);
            Scribe_Values.Look(ref endingE01, "endingE01", true);
            Scribe_Values.Look(ref endingE02, "endingE02", true);
            Scribe_Values.Look(ref endingE03, "endingE03", true);
            Scribe_Values.Look(ref endingE04, "endingE04", true);
            Scribe_Values.Look(ref endingE05, "endingE05", true);
            Scribe_Values.Look(ref plagueEnabled, "plagueEnabled", true);
            Scribe_Values.Look(ref plagueSpreadChancePerCarrier, "plagueSpreadChancePerCarrier", 0.005f);
            Scribe_Values.Look(ref plagueSpreadChanceCap, "plagueSpreadChanceCap", 0.30f);
            Scribe_Values.Look(ref plagueSpreadDayInterval, "plagueSpreadDayInterval", 3);
            Scribe_Values.Look(ref plagueSpreadHour, "plagueSpreadHour", 6);
            Scribe_Values.Look(ref plagueBloodPumpingSkipPercent, "plagueBloodPumpingSkipPercent", 120f);
            Scribe_Values.Look(ref beggingEnabled, "beggingEnabled", true);
            Scribe_Values.Look(ref begAutoGiveEnabled, "begAutoGiveEnabled", false);
            Scribe_Values.Look(ref begFailCooldownHours, "begFailCooldownHours", 3);
            Scribe_Values.Look(ref begSlapChancePercent, "begSlapChancePercent", 50);
            Scribe_Values.Look(ref begSuccessChancePercent, "begSuccessChancePercent", 35);
            Scribe_Values.Look(ref begSocialBonusPercent, "begSocialBonusPercent", 3);
            Scribe_Values.Look(ref stealingEnabled, "stealingEnabled", true);
            Scribe_Values.Look(ref fightingEnabled, "fightingEnabled", true);
            Scribe_Values.Look(ref gnawingEnabled, "gnawingEnabled", true);
            Scribe_Values.Look(ref batchTurnsHostile, "batchTurnsHostile", true);
            Scribe_Values.Look(ref batchLeavesTogether, "batchLeavesTogether", true);
            Scribe_Values.Look(ref suiYinThreatTempo, "suiYinThreatTempo", true);
            Scribe_Values.Look(ref weightWild, "weightWild", 1.4f);
            Scribe_Values.Look(ref weightBeggar, "weightBeggar", 1f);
            Scribe_Values.Look(ref weightThief, "weightThief", 0.7f);
            Scribe_Values.Look(ref weightTrade, "weightTrade", 0.5f);
            Scribe_Values.Look(ref weightSiege, "weightSiege", 0.35f);
            Scribe_Values.Look(ref weightAid, "weightAid", 0.25f);
            Scribe_Values.Look(ref weightSpecial, "weightSpecial", 0.2f);
            Scribe_Values.Look(ref weightIntel, "weightIntel", 0.12f);
            Scribe_Values.Look(ref weightSeason, "weightSeason", 1.1f);
            Scribe_Values.Look(ref weightPlague, "weightPlague", 0.5f);
            Scribe_Values.Look(ref endingIdentity, "endingIdentity", true);
            Scribe_Values.Look(ref requestMinSimple, "requestMinSimple", 6);
            Scribe_Values.Look(ref requestMaxSimple, "requestMaxSimple", 28);
            Scribe_Values.Look(ref requestMinFine, "requestMinFine", 4);
            Scribe_Values.Look(ref requestMaxFine, "requestMaxFine", 18);
            Scribe_Values.Look(ref requestMinMedicine, "requestMinMedicine", 2);
            Scribe_Values.Look(ref requestMaxMedicine, "requestMaxMedicine", 10);
            Scribe_Values.Look(ref requestMinSilver, "requestMinSilver", 80);
            Scribe_Values.Look(ref requestMaxSilver, "requestMaxSilver", 1200);
            Scribe_Values.Look(ref requestMinHerbal, "requestMinHerbal", 3);
            Scribe_Values.Look(ref requestMaxHerbal, "requestMaxHerbal", 15);
            Scribe_Values.Look(ref requestDays, "requestDays", 1);
            Scribe_Values.Look(ref requestPointScale, "requestPointScale", 300f);
            Scribe_Values.Look(ref foodPerChild, "foodPerChild", 10);
            Scribe_Values.Look(ref foodPerVisitor, "foodPerVisitor", 1);
            Scribe_Values.Look(ref maxFoodRequest, "maxFoodRequest", 12);
            Scribe_Values.Look(ref envoyMealCost, "envoyMealCost", 6);
            Scribe_Values.Look(ref campMinAdults, "campMinAdults", 2);
            Scribe_Values.Look(ref campMaxAdults, "campMaxAdults", 4);
            Scribe_Values.Look(ref campMinChildren, "campMinChildren", 8);
            Scribe_Values.Look(ref campMaxChildren, "campMaxChildren", 16);
            Scribe_Values.Look(ref campGoodwill, "campGoodwill", 12);
            Scribe_Values.Look(ref campDays, "campDays", 15);
            Scribe_Values.Look(ref campHuts, "campHuts", 4);
            Scribe_Values.Look(ref holeWoodCost, "holeWoodCost", 20);
            Scribe_Values.Look(ref holeCleanPortions, "holeCleanPortions", 5);
            Scribe_Values.Look(ref holeLossRange, "holeLossRange", 12);
            Scribe_Values.Look(ref holeMaxLosses, "holeMaxLosses", 3);
            Scribe_Values.Look(ref apparelAwfulPercent, "apparelAwfulPercent", 35);
            Scribe_Values.Look(ref apparelPoorPercent, "apparelPoorPercent", 50);
            Scribe_Values.Look(ref apparelMinDurabilityPercent, "apparelMinDurabilityPercent", 10);
            Scribe_Values.Look(ref apparelMaxDurabilityPercent, "apparelMaxDurabilityPercent", 60);
            Scribe_Values.Look(ref apparelCorpsePercent, "apparelCorpsePercent", 25);
            Scribe_Values.Look(ref apparelClothPercent, "apparelClothPercent", 65);
            Scribe_Values.Look(ref apparelMaxPieces, "apparelMaxPieces", 4);
            Scribe_Values.Look(ref begFailMood, "begFailMood", -5);
            Scribe_Values.Look(ref begSuccessMood, "begSuccessMood", 3);
            Scribe_Values.Look(ref begSlapMood, "begSlapMood", -10);
            Scribe_Values.Look(ref begSlapKnockoutHours, "begSlapKnockoutHours", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars && begSlapKnockoutHours == Pawn.RHAH_VisitorRules.LegacyBegSlapKnockoutHours)
            {
                begSlapKnockoutHours = 0;
            }
            Scribe_Values.Look(ref begBruiseSeverity, "begBruiseSeverity", 4f);
            Scribe_Values.Look(ref begBruiseStep, "begBruiseStep", 4f);
            Scribe_Values.Look(ref begBruiseMax, "begBruiseMax", 16f);
            Scribe_Values.Look(ref barkNutrition, "barkNutrition", 0.25f);
            Scribe_Values.Look(ref barkDamage, "barkDamage", 12f);
            Scribe_Values.Look(ref wallNutrition, "wallNutrition", 0.2f);
            Scribe_Values.Look(ref wallDamage, "wallDamage", 10f);
            Scribe_Values.Look(ref clayMaxBites, "clayMaxBites", 3);
            Scribe_Values.Look(ref clayWindowDays, "clayWindowDays", 15);
            Scribe_Values.Look(ref claySeverityPerBite, "claySeverityPerBite", 0.33f);
            Scribe_Values.Look(ref childHungryPercent, "childHungryPercent", 30f);
            Scribe_Values.Look(ref prisonerHungryPercent, "prisonerHungryPercent", 20f);
            Scribe_Values.Look(ref tailBiteAge, "tailBiteAge", 3f);
            Scribe_Values.Look(ref scavengeNutrition, "scavengeNutrition", 0.15f);
            Scribe_Values.Look(ref tailNutrition, "tailNutrition", 0.35f);
            Scribe_Values.Look(ref tailFailDamage, "tailFailDamage", 4f);
            Scribe_Values.Look(ref satisfiedFoodPercent, "satisfiedFoodPercent", 82f);
            Scribe_Values.Look(ref refeedMalnutrition, "refeedMalnutrition", 0.4f);
            Scribe_Values.Look(ref plagueSeverityMax, "plagueSeverityMax", 0.1f);
            Scribe_Values.Look(ref followPredatorPercent, "followPredatorPercent", 10);
            Scribe_Values.Look(ref followBirthWatchDays, "followBirthWatchDays", 3);
            Scribe_Values.Look(ref followPlagueBirthDays, "followPlagueBirthDays", 5);
            Scribe_Values.Look(ref followMotherReturnDays, "followMotherReturnDays", 15);
            Scribe_Values.Look(ref followLongReturnYears, "followLongReturnYears", 2);
            Scribe_Values.Look(ref followAdultAge, "followAdultAge", 14f);
            Scribe_Values.Look(ref followMoodScalePercent, "followMoodScalePercent", 100);
            Scribe_Values.Look(ref endingTrustFloor, "endingTrustFloor", 50);
            Scribe_Values.Look(ref endingHopeTrust, "endingHopeTrust", 75);
            Scribe_Values.Look(ref endingHaltTrust, "endingHaltTrust", -75);
            Scribe_Values.Look(ref endingLowKinds, "endingLowKinds", 6);
            Scribe_Values.Look(ref endingThreatDays, "endingThreatDays", 13f);
            Scribe_Values.Look(ref trustKill, "trustKill", -10);
            Scribe_Values.Look(ref trustCaptive, "trustCaptive", -5);
            Scribe_Values.Look(ref trustEntrustGood, "trustEntrustGood", 5);
            Scribe_Values.Look(ref trustEntrustCaptive, "trustEntrustCaptive", -5);
            Scribe_Values.Look(ref trustEntrustStory, "trustEntrustStory", -2);
            Scribe_Values.Look(ref trustEntrustRegret, "trustEntrustRegret", -3);
            Scribe_Values.Look(ref trustEntrustBanished, "trustEntrustBanished", -1);
            Scribe_Values.Look(ref trustExchange, "trustExchange", 3);
            Scribe_Values.Look(ref trustHoleOpen, "trustHoleOpen", 1);
            Scribe_Values.Look(ref trustHoleIgnore, "trustHoleIgnore", -1);
            Scribe_Values.Look(ref trustHoleBait, "trustHoleBait", 3);
            Scribe_Values.Look(ref trustEnvoyFail, "trustEnvoyFail", -2);
            Scribe_Values.Look(ref trustRelicFail, "trustRelicFail", -2);
            Scribe_Values.Look(ref trustHold, "trustHold", -1);
            Scribe_Values.Look(ref trustDeliver, "trustDeliver", 1);
            Scribe_Values.Look(ref trustLeave, "trustLeave", 1);
            Scribe_Values.Look(ref trustExpel, "trustExpel", -1);
            Scribe_Values.Look(ref narrativeRewardSilver, "narrativeRewardSilver", 300);
            Scribe_Values.Look(ref narrativeRescueCost, "narrativeRescueCost", 250);
            Scribe_Values.Look(ref narrativeRescueReward, "narrativeRescueReward", 2500);
            Scribe_Values.Look(ref narrativeRelicTake, "narrativeRelicTake", 200);
            Scribe_Values.Look(ref narrativeRelicHand, "narrativeRelicHand", 100);
            Scribe_Values.Look(ref narrativeTrustBonusPercent, "narrativeTrustBonusPercent", 25);
            Scribe_Values.Look(ref narrativeMissingDays, "narrativeMissingDays", 1);
            Scribe_Values.Look(ref narrativeObserveDays, "narrativeObserveDays", 30);
            Scribe_Values.Look(ref narrativeHoleIgnoreDays, "narrativeHoleIgnoreDays", 3);
            Scribe_Values.Look(ref narrativeEnvoyWaitDays, "narrativeEnvoyWaitDays", 3);
            Scribe_Values.Look(ref narrativeEnvoyCheckDays, "narrativeEnvoyCheckDays", 1);
            Scribe_Values.Look(ref narrativeRelicDays, "narrativeRelicDays", 15);
            Scribe_Values.Look(ref narrativeRevisitYears, "narrativeRevisitYears", 4);
            Scribe_Values.Look(ref narrativeAsideCooldownDays, "narrativeAsideCooldownDays", 3);
            Scribe_Values.Look(ref narrativeAsideCutoff, "narrativeAsideCutoff", -75);
            Scribe_Values.Look(ref narrativeAdultYears, "narrativeAdultYears", 18);
            Scribe_Values.Look(ref narrativeTheftKinds, "narrativeTheftKinds", 2);
            Scribe_Values.Look(ref narrativeProgressKinds, "narrativeProgressKinds", 3);
            Scribe_Values.Look(ref narrativeRewardKinds, "narrativeRewardKinds", 8);
            Scribe_Values.Look(ref narrativeEnvoyKinds, "narrativeEnvoyKinds", 5);
            Scribe_Values.Look(ref narrativeRelicKinds, "narrativeRelicKinds", 8);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                xenotypeWeights = xenotypeWeights ?? new Dictionary<string, float>();
                enabledXenotypeDefNames = enabledXenotypeDefNames ?? new List<string>();
                enabledGeneDefNames = enabledGeneDefNames ?? new List<string>();
                disabledReliefFoodDefNames = disabledReliefFoodDefNames ?? new List<string>();
                disabledGiveFoodDefNames = disabledGiveFoodDefNames ?? new List<string>();
            disabledBegFoodDefNames = disabledBegFoodDefNames ?? new List<string>();
                disabledRefugeeApparelDefNames = disabledRefugeeApparelDefNames ?? new List<string>();
                temperatureApparelInsulation = temperatureApparelInsulation ?? new Dictionary<string, float>();
                disabledTemperatureApparelDefNames = disabledTemperatureApparelDefNames ?? new List<string>();
                disabledIncidentGroupIds = disabledIncidentGroupIds ?? new List<string>();
                incidentGroupPoints = incidentGroupPoints ?? new Dictionary<string, float>();
                incidentGroupWeights = incidentGroupWeights ?? new Dictionary<string, float>();
                incidentGroupAttitudes = incidentGroupAttitudes ?? new Dictionary<string, int>();
                incidentGroupLongChains = incidentGroupLongChains ?? new List<string>();
                disabledTraitDisplayIds = disabledTraitDisplayIds ?? new List<string>();
                traitWeights = traitWeights ?? new Dictionary<string, float>();
                broadcastCooldownDays = HungerAndHavoc.Incidents.RHAH_BroadcastRules.ClampDays(broadcastCooldownDays);
                Normalize();
                positiveIncidentDays = HungerAndHavoc.Incidents.RHAH_IncidentSchedule.ClampDays(positiveIncidentDays);
                negativeIncidentDays = HungerAndHavoc.Incidents.RHAH_IncidentSchedule.ClampDays(negativeIncidentDays);
                positiveIncidentPace = HungerAndHavoc.Incidents.RHAH_IncidentPace.Normalize(positiveIncidentPace);
                negativeIncidentPace = HungerAndHavoc.Incidents.RHAH_IncidentPace.Normalize(negativeIncidentPace);
                frequencyWindowDays = HungerAndHavoc.Incidents.RHAH_IncidentSchedule.ClampWindowDays(frequencyWindowDays);
                ClampVisitorRules();
                ClampIdentityThresholds();
                ClampTunables();
                refugeePredationChancePercent = HungerAndHavoc.Incidents.RHAH_PredationRules.ClampChance(refugeePredationChancePercent);
                ClampPlagueRules();
                ClampFamilyWeights();
                ClampFertility();
            }
        }

        public float XenotypeWeight(string defName)
        {
            EnsureCollections();
            return RHAH_XenotypeWeightTable.StoredOrDefault(xenotypeWeights, defName, RHAH_GeneCatalog.SuggestedWeight(defName));
        }

        public void SetXenotypeWeight(string defName, float value)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            EnsureCollections();
            xenotypeWeights[defName] = RHAH_XenotypeWeightTable.Clamp(value);
        }

        public void ResetXenotypeWeights()
        {
            EnsureCollections();
            xenotypeWeights.Clear();
        }

        public bool IsXenotypeEnabled(string defName)
        {
            EnsureCollections();
            return RHAH_GeneCatalog.IsBuiltin(defName) || enabledXenotypeDefNames.Contains(defName);
        }

        public void SetXenotypeEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrEmpty(defName) || RHAH_GeneCatalog.IsBuiltin(defName))
            {
                return;
            }

            EnsureCollections();
            if (enabled)
            {
                if (!enabledXenotypeDefNames.Contains(defName))
                {
                    enabledXenotypeDefNames.Add(defName);
                }

                return;
            }

            enabledXenotypeDefNames.Remove(defName);
        }

        public bool IsGeneEnabled(string defName)
        {
            EnsureCollections();
            return enabledGeneDefNames.Contains(defName);
        }

        public void SetGeneEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            EnsureCollections();
            if (enabled)
            {
                if (!enabledGeneDefNames.Contains(defName))
                {
                    enabledGeneDefNames.Add(defName);
                }

                return;
            }

            enabledGeneDefNames.Remove(defName);
        }

        public List<string> EnabledXenotypeNames()
        {
            EnsureCollections();
            return enabledXenotypeDefNames;
        }

        public List<string> EnabledGeneNames()
        {
            EnsureCollections();
            return enabledGeneDefNames;
        }

        public List<string> MissingXenotypeNames()
        {
            EnsureCollections();
            List<string> missing = new List<string>();
            foreach (KeyValuePair<string, float> entry in xenotypeWeights)
            {
                if (!string.IsNullOrEmpty(entry.Key))
                {
                    missing.Add(entry.Key);
                }
            }

            for (int i = 0; i < enabledXenotypeDefNames.Count; i++)
            {
                if (!missing.Contains(enabledXenotypeDefNames[i]))
                {
                    missing.Add(enabledXenotypeDefNames[i]);
                }
            }

            return missing;
        }

        public bool IsReliefFoodEnabled(string defName)
        {
            EnsureCollections();
            return string.IsNullOrEmpty(defName) || !disabledReliefFoodDefNames.Contains(defName);
        }

        public void SetReliefFoodEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            EnsureCollections();
            if (enabled)
            {
                disabledReliefFoodDefNames.Remove(defName);
            }
            else if (!disabledReliefFoodDefNames.Contains(defName))
            {
                disabledReliefFoodDefNames.Add(defName);
            }

            InvalidateReliefSearch();
        }

        public void SetAllReliefFood(bool enabled, List<string> candidates)
        {
            EnsureCollections();
            disabledReliefFoodDefNames.Clear();
            if (!enabled && candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!string.IsNullOrEmpty(candidates[i]) && !disabledReliefFoodDefNames.Contains(candidates[i]))
                    {
                        disabledReliefFoodDefNames.Add(candidates[i]);
                    }
                }
            }

            InvalidateReliefSearch();
        }

        public bool IsGiveFoodEnabled(string defName)
        {
            EnsureCollections();
            return string.IsNullOrEmpty(defName) || !disabledGiveFoodDefNames.Contains(defName);
        }

        public void SetGiveFoodEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            EnsureCollections();
            if (enabled)
            {
                disabledGiveFoodDefNames.Remove(defName);
            }
            else if (!disabledGiveFoodDefNames.Contains(defName))
            {
                disabledGiveFoodDefNames.Add(defName);
            }
        }

        public void SetAllGiveFood(bool enabled, List<string> candidates)
        {
            EnsureCollections();
            disabledGiveFoodDefNames.Clear();
            if (!enabled && candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!string.IsNullOrEmpty(candidates[i]) && !disabledGiveFoodDefNames.Contains(candidates[i]))
                    {
                        disabledGiveFoodDefNames.Add(candidates[i]);
                    }
                }
            }
        }

        public bool IsBegFoodEnabled(string defName)
        {
            EnsureCollections();
            return string.IsNullOrEmpty(defName) || !disabledBegFoodDefNames.Contains(defName);
        }

        public void SetBegFoodEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            EnsureCollections();
            if (enabled)
            {
                disabledBegFoodDefNames.Remove(defName);
            }
            else if (!disabledBegFoodDefNames.Contains(defName))
            {
                disabledBegFoodDefNames.Add(defName);
            }
        }

        public void SetAllBegFood(bool enabled, List<string> candidates)
        {
            EnsureCollections();
            disabledBegFoodDefNames.Clear();
            if (!enabled && candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!string.IsNullOrEmpty(candidates[i]) && !disabledBegFoodDefNames.Contains(candidates[i]))
                    {
                        disabledBegFoodDefNames.Add(candidates[i]);
                    }
                }
            }
        }



        public bool IsRefugeeApparelEnabled(string defName)
        {
            EnsureCollections();
            return string.IsNullOrEmpty(defName) || !disabledRefugeeApparelDefNames.Contains(defName);
        }

        public void SetRefugeeApparelEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            EnsureCollections();
            if (enabled)
            {
                disabledRefugeeApparelDefNames.Remove(defName);
            }
            else if (!disabledRefugeeApparelDefNames.Contains(defName))
            {
                disabledRefugeeApparelDefNames.Add(defName);
            }
        }

        public void SetAllRefugeeApparel(bool enabled, List<string> candidates)
        {
            EnsureCollections();
            disabledRefugeeApparelDefNames.Clear();
            if (!enabled && candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!string.IsNullOrEmpty(candidates[i]) && !disabledRefugeeApparelDefNames.Contains(candidates[i]))
                    {
                        disabledRefugeeApparelDefNames.Add(candidates[i]);
                    }
                }
            }
        }

        public void InvalidateReliefSearch()
        {
            if (Current.Game == null || Current.Game.Maps == null)
            {
                return;
            }

            for (int i = 0; i < Current.Game.Maps.Count; i++)
            {
                Map map = Current.Game.Maps[i];
                MapComponent_RHAH_Map component = map != null
                    ? map.GetComponent<MapComponent_RHAH_Map>()
                    : null;
                if (component != null)
                {
                    component.InvalidateFoodSearch();
                }
            }
        }
        public bool IsTemperatureApparelEnabled(string defName)
        {
            EnsureCollections();
            return !string.IsNullOrEmpty(defName) && !disabledTemperatureApparelDefNames.Contains(defName);
        }

        public void SetTemperatureApparelEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrEmpty(defName) || !Pawn.RHAH_VisitorRules.IsTemperatureApparel(defName))
            {
                return;
            }

            EnsureCollections();
            if (enabled)
            {
                disabledTemperatureApparelDefNames.Remove(defName);
                return;
            }

            if (!disabledTemperatureApparelDefNames.Contains(defName))
            {
                disabledTemperatureApparelDefNames.Add(defName);
            }
        }

        public float TemperatureApparelInsulation(string defName)
        {
            EnsureCollections();
            float fallback = Pawn.RHAH_VisitorRules.DefaultInsulation(defName);
            float stored;
            if (!string.IsNullOrEmpty(defName) && temperatureApparelInsulation.TryGetValue(defName, out stored))
            {
                return Pawn.RHAH_VisitorRules.ClampInsulation(stored, fallback);
            }

            return fallback;
        }

        public void SetTemperatureApparelInsulation(string defName, float value)
        {
            if (string.IsNullOrEmpty(defName) || !Pawn.RHAH_VisitorRules.IsTemperatureApparel(defName))
            {
                return;
            }

            EnsureCollections();
            temperatureApparelInsulation[defName] = Pawn.RHAH_VisitorRules.ClampInsulation(
                value,
                Pawn.RHAH_VisitorRules.DefaultInsulation(defName));
        }

        static float ClampStoredInsulation(float value)
        {
            return Pawn.RHAH_VisitorRules.ClampInsulation(value, Pawn.RHAH_VisitorRules.MinTemperatureInsulation);
        }

        void Normalize()
        {
            famineYearChancePercent = GameComponent_RHAH_FamineYear.ClampPercent(famineYearChancePercent, 40f);
            famineYearPawnMultiplier = GameComponent_RHAH_FamineYear.ClampMultiplier(famineYearPawnMultiplier);
            famineYearDisasterChancePercent = GameComponent_RHAH_FamineYear.ClampPercent(famineYearDisasterChancePercent, 20f);
            xenotypeWeights = ClampWeights(xenotypeWeights, RHAH_XenotypeWeightTable.Clamp);
            incidentGroupPoints = ClampWeights(incidentGroupPoints, ClampDebugPoints);
            incidentGroupWeights = ClampWeights(incidentGroupWeights, ClampIncidentWeight);
            incidentGroupAttitudes = ClampAttitudes(incidentGroupAttitudes);
            incidentGroupLongChains = Clean(incidentGroupLongChains);
            traitWeights = ClampWeights(traitWeights, ClampTraitWeight);
            enabledXenotypeDefNames = Clean(enabledXenotypeDefNames);
            enabledGeneDefNames = Clean(enabledGeneDefNames);
            disabledReliefFoodDefNames = Clean(disabledReliefFoodDefNames);
            disabledGiveFoodDefNames = Clean(disabledGiveFoodDefNames);
            disabledBegFoodDefNames = Clean(disabledBegFoodDefNames);
            disabledRefugeeApparelDefNames = Clean(disabledRefugeeApparelDefNames);
            temperatureApparelInsulation = ClampWeights(temperatureApparelInsulation, ClampStoredInsulation);
            disabledTemperatureApparelDefNames = Clean(disabledTemperatureApparelDefNames);
            disabledIncidentGroupIds = Clean(disabledIncidentGroupIds);
            disabledHistoryDisplayIds = Clean(disabledHistoryDisplayIds);
            disabledTraitDisplayIds = Clean(disabledTraitDisplayIds);
        }

        void ClampVisitorRules()
        {
            maxEventPawns = Pawn.RHAH_VisitorRules.ClampCount(maxEventPawns);
            minGeneratedAge = Pawn.RHAH_VisitorRules.ClampAge(minGeneratedAge);
            maxGeneratedAge = Pawn.RHAH_VisitorRules.ClampAge(maxGeneratedAge);
            if (maxGeneratedAge < minGeneratedAge)
            {
                maxGeneratedAge = minGeneratedAge;
            }
            minMotherAge = Pawn.RHAH_VisitorRules.ClampMotherAge(minMotherAge);
            genderMode = Pawn.RHAH_VisitorRules.ClampBodyMode(genderMode, 4);
            femaleSharePercent = Pawn.RHAH_VisitorRules.ClampFemaleShare(femaleSharePercent);
            playerIdeoPercent = Pawn.RHAH_VisitorRules.ClampPercent(playerIdeoPercent, 0, 100);
            apparelMode = Pawn.RHAH_VisitorRules.ClampBodyMode(apparelMode, 4);
            maxOwnedTraits = Pawn.RHAH_VisitorRules.ClampOwnedTraits(maxOwnedTraits);
            contentListMode = Pawn.RHAH_VisitorRules.ClampBodyMode(contentListMode, 3);
            giveFoodListMode = Pawn.RHAH_VisitorRules.ClampBodyMode(giveFoodListMode, 3);
            begFoodListMode = Pawn.RHAH_VisitorRules.ClampBodyMode(begFoodListMode, 3);
            begSuccessChancePercent = Pawn.RHAH_VisitorRules.ClampPercent(begSuccessChancePercent, Pawn.RHAH_VisitorRules.MinBegSuccessChancePercent, Pawn.RHAH_VisitorRules.MaxBegSuccessChancePercent);
            begSocialBonusPercent = Pawn.RHAH_VisitorRules.ClampPercent(begSocialBonusPercent, Pawn.RHAH_VisitorRules.MinBegSocialBonusPercent, Pawn.RHAH_VisitorRules.MaxBegSocialBonusPercent);
            apparelListMode = Pawn.RHAH_VisitorRules.ClampBodyMode(apparelListMode, 3);
            ClampFertility();

            reliefFoodScoreBonus = Pawn.RHAH_VisitorRules.ClampBonus(reliefFoodScoreBonus);
            fedWanderHours = Pawn.RHAH_VisitorRules.ClampFedWanderHours(fedWanderHours);
            noFoodWaitDays = ClampStayDays(noFoodWaitDays, 0.5f);
            shelterDays = Pawn.RHAH_VisitorRules.ClampShelterDays(shelterDays);
            begFailCooldownHours = Pawn.RHAH_VisitorRules.ClampBegFailCooldownHours(begFailCooldownHours);
            begSlapChancePercent = Pawn.RHAH_VisitorRules.ClampBegSlapChance(begSlapChancePercent);
            hireDays = Pawn.RHAH_VisitorRules.ClampHireDays(hireDays);
            if (float.IsNaN(minimumEventTemperature) || float.IsInfinity(minimumEventTemperature))
            {
                minimumEventTemperature = -35f;
            }

            if (float.IsNaN(maximumEventTemperature) || float.IsInfinity(maximumEventTemperature))
            {
                maximumEventTemperature = 70f;
            }

            if (minimumEventTemperature < -35f)
            {
                minimumEventTemperature = -35f;
            }

            if (maximumEventTemperature > 70f)
            {
                maximumEventTemperature = 70f;
            }

            if (minimumEventTemperature > maximumEventTemperature)
            {
                float swap = minimumEventTemperature;
                minimumEventTemperature = maximumEventTemperature;
                maximumEventTemperature = swap;
            }
        }
        void ClampFertility()
        {
            Generation.RHAH_FertilityRules.ClampLitter(ref litterMin, ref litterPeak, ref litterMax);
            fertileMinAge = Generation.RHAH_FertilityRules.ClampFertileAge(fertileMinAge);
            fertilityPercent = Generation.RHAH_FertilityRules.ClampFertilityPercent(fertilityPercent);
            gestationDays = Generation.RHAH_FertilityRules.ClampGestationDays(gestationDays);
            lovinPregnancyPercent = Generation.RHAH_FertilityRules.ClampLovinPregnancyPercent(lovinPregnancyPercent);
        }



        static float ClampStayDays(float days, float fallback)
        {
            if (float.IsNaN(days) || float.IsInfinity(days))
            {
                return fallback;
            }

            if (days < 0f)
            {
                return 0f;
            }

            return days > 5f ? 5f : days;
        }

        static Dictionary<string, float> ClampWeights(Dictionary<string, float> source, Func<float, float> clamp)
        {
            Dictionary<string, float> normalized = new Dictionary<string, float>();
            if (source == null)
            {
                return normalized;
            }

            foreach (KeyValuePair<string, float> entry in source)
            {
                if (!string.IsNullOrEmpty(entry.Key))
                {
                    normalized[entry.Key] = clamp(entry.Value);
                }
            }

            return normalized;
        }

        static float ClampTraitWeight(float value)
        {
            return ClampUnit(value);
        }

        static float ClampIncidentWeight(float value)
        {
            return ClampUnit(value);
        }

        static float ClampDebugPoints(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < HungerAndHavoc.Incidents.RHAH_IncidentTuning.MinDebugPoints)
            {
                return HungerAndHavoc.Incidents.RHAH_IncidentTuning.MinDebugPoints;
            }

            return value > HungerAndHavoc.Incidents.RHAH_IncidentTuning.MaxDebugPoints
                ? HungerAndHavoc.Incidents.RHAH_IncidentTuning.MaxDebugPoints
                : value;
        }

        static float ClampUnit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return 0f;
            }

            return value > 100f ? 100f : value;
        }

        static List<string> Clean(List<string> names)
        {
            List<string> cleaned = new List<string>();
            if (names == null)
            {
                return cleaned;
            }

            for (int i = 0; i < names.Count; i++)
            {
                if (!string.IsNullOrEmpty(names[i]) && !cleaned.Contains(names[i]))
                {
                    cleaned.Add(names[i]);
                }
            }

            return cleaned;
        }

        void EnsureCollections()
        {
            xenotypeWeights = xenotypeWeights ?? new Dictionary<string, float>();
            enabledXenotypeDefNames = enabledXenotypeDefNames ?? new List<string>();
            enabledGeneDefNames = enabledGeneDefNames ?? new List<string>();
            disabledReliefFoodDefNames = disabledReliefFoodDefNames ?? new List<string>();
            disabledGiveFoodDefNames = disabledGiveFoodDefNames ?? new List<string>();
                disabledBegFoodDefNames = disabledBegFoodDefNames ?? new List<string>();
            disabledRefugeeApparelDefNames = disabledRefugeeApparelDefNames ?? new List<string>();
            temperatureApparelInsulation = temperatureApparelInsulation ?? new Dictionary<string, float>();
            disabledTemperatureApparelDefNames = disabledTemperatureApparelDefNames ?? new List<string>();
            disabledIncidentGroupIds = disabledIncidentGroupIds ?? new List<string>();
            incidentGroupPoints = incidentGroupPoints ?? new Dictionary<string, float>();
            incidentGroupWeights = incidentGroupWeights ?? new Dictionary<string, float>();
            incidentGroupAttitudes = incidentGroupAttitudes ?? new Dictionary<string, int>();
            incidentGroupLongChains = incidentGroupLongChains ?? new List<string>();
            disabledHistoryDisplayIds = disabledHistoryDisplayIds ?? new List<string>();
            disabledTraitDisplayIds = disabledTraitDisplayIds ?? new List<string>();
            traitWeights = traitWeights ?? new Dictionary<string, float>();
        }

        static string IncidentGroupId(string displayId)
        {
            int group = HungerAndHavoc.Incidents.RHAH_IncidentGroups.GroupOf(displayId);
            return group < 0 ? null : HungerAndHavoc.Incidents.RHAH_IncidentGroups.Scenarios(group)[0].DisplayId;
        }

        public bool IsIncidentEnabled(string displayId)
        {
            EnsureCollections();
            string groupId = IncidentGroupId(displayId);
            return !string.IsNullOrEmpty(displayId) && (groupId == null || !disabledIncidentGroupIds.Contains(groupId));
        }

        public void SetIncidentEnabled(string displayId, bool enabled)
        {
            EnsureCollections();
            displayId = IncidentGroupId(displayId);
            if (string.IsNullOrEmpty(displayId))
            {
                return;
            }

            if (enabled)
            {
                disabledIncidentGroupIds.Remove(displayId);
                return;
            }

            if (!disabledIncidentGroupIds.Contains(displayId))
            {
                disabledIncidentGroupIds.Add(displayId);
            }
        }

        public float IncidentDebugPoints(string displayId, float catalogPoints)
        {
            EnsureCollections();
            displayId = IncidentGroupId(displayId);
            float stored;
            if (!string.IsNullOrEmpty(displayId) && incidentGroupPoints.TryGetValue(displayId, out stored))
            {
                return ClampDebugPoints(stored);
            }

            return ClampDebugPoints(catalogPoints);
        }

        public void SetIncidentDebugPoints(string displayId, float points)
        {
            displayId = IncidentGroupId(displayId);
            if (string.IsNullOrEmpty(displayId))
            {
                return;
            }

            EnsureCollections();
            incidentGroupPoints[displayId] = ClampDebugPoints(points);
        }

        public float IncidentWeight(string displayId)
        {
            EnsureCollections();
            displayId = IncidentGroupId(displayId);
            float stored;
            if (!string.IsNullOrEmpty(displayId) && incidentGroupWeights.TryGetValue(displayId, out stored))
            {
                return ClampIncidentWeight(stored);
            }

            return HungerAndHavoc.Incidents.RHAH_IncidentTuning.DefaultWeight;
        }

        public void SetIncidentWeight(string displayId, float weight)
        {
            displayId = IncidentGroupId(displayId);
            if (string.IsNullOrEmpty(displayId))
            {
                return;
            }

            EnsureCollections();
            incidentGroupWeights[displayId] = ClampIncidentWeight(weight);
        }

        public HungerAndHavoc.Api.RHAH_Attitude IncidentAttitude(string displayId, HungerAndHavoc.Api.RHAH_Attitude catalog)
        {
            EnsureCollections();
            displayId = IncidentGroupId(displayId);
            int stored;
            if (!string.IsNullOrEmpty(displayId) && incidentGroupAttitudes.TryGetValue(displayId, out stored) && stored >= 0 && stored <= 4)
            {
                return (HungerAndHavoc.Api.RHAH_Attitude)stored;
            }

            return catalog;
        }

        public void SetIncidentAttitude(string displayId, HungerAndHavoc.Api.RHAH_Attitude attitude)
        {
            displayId = IncidentGroupId(displayId);
            if (string.IsNullOrEmpty(displayId))
            {
                return;
            }

            EnsureCollections();
            int stored = (int)attitude;
            incidentGroupAttitudes[displayId] = stored < 0 ? 0 : (stored > 4 ? 4 : stored);
        }

        public bool IncidentUsesLongChain(string displayId)
        {
            EnsureCollections();
            displayId = IncidentGroupId(displayId);
            return !string.IsNullOrEmpty(displayId) && incidentGroupLongChains.Contains(displayId);
        }

        public void SetIncidentLongChain(string displayId, bool longChain)
        {
            displayId = IncidentGroupId(displayId);
            if (string.IsNullOrEmpty(displayId))
            {
                return;
            }

            EnsureCollections();
            bool present = incidentGroupLongChains.Contains(displayId);
            if (longChain && !present)
            {
                incidentGroupLongChains.Add(displayId);
            }
            else if (!longChain && present)
            {
                incidentGroupLongChains.Remove(displayId);
            }
        }

        static Dictionary<string, int> ClampAttitudes(Dictionary<string, int> values)
        {
            Dictionary<string, int> cleaned = new Dictionary<string, int>();
            if (values == null)
            {
                return cleaned;
            }

            foreach (KeyValuePair<string, int> pair in values)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                int stored = pair.Value;
                cleaned[pair.Key] = stored < 0 ? 0 : (stored > 4 ? 4 : stored);
            }

            return cleaned;
        }
        public bool IsHistoryEnabled(string displayId)
        {
            EnsureCollections();
            return !string.IsNullOrEmpty(displayId) && !disabledHistoryDisplayIds.Contains(displayId);
        }

        public void SetHistoryEnabled(string displayId, bool enabled)
        {
            SetDisabled(disabledHistoryDisplayIds, displayId, enabled);
        }

        public void SetAllHistories(bool enabled, IReadOnlyList<string> displayIds)
        {
            EnsureCollections();
            disabledHistoryDisplayIds.Clear();
            if (enabled || displayIds == null)
            {
                return;
            }

            for (int i = 0; i < displayIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(displayIds[i]) && !disabledHistoryDisplayIds.Contains(displayIds[i]))
                {
                    disabledHistoryDisplayIds.Add(displayIds[i]);
                }
            }
        }

        public bool IsTraitEnabled(string displayId)
        {
            EnsureCollections();
            return !string.IsNullOrEmpty(displayId) && !disabledTraitDisplayIds.Contains(displayId);
        }

        public void SetTraitEnabled(string displayId, bool enabled)
        {
            SetDisabled(disabledTraitDisplayIds, displayId, enabled);
        }

        public void SetAllTraits(bool enabled, IReadOnlyList<string> displayIds)
        {
            EnsureCollections();
            disabledTraitDisplayIds.Clear();
            if (enabled || displayIds == null)
            {
                return;
            }

            for (int i = 0; i < displayIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(displayIds[i]) && !disabledTraitDisplayIds.Contains(displayIds[i]))
                {
                    disabledTraitDisplayIds.Add(displayIds[i]);
                }
            }
        }

        public float TraitWeight(string displayId)
        {
            EnsureCollections();
            float stored;
            if (!string.IsNullOrEmpty(displayId) && traitWeights.TryGetValue(displayId, out stored))
            {
                return ClampTraitWeight(stored);
            }

            return Data.RHAH_ContentCatalog.DefaultTraitWeight(displayId);
        }

        public void SetTraitWeight(string displayId, float weight)
        {
            if (string.IsNullOrEmpty(displayId))
            {
                return;
            }

            EnsureCollections();
            traitWeights[displayId] = ClampTraitWeight(weight);
        }

        void SetDisabled(List<string> disabled, string displayId, bool enabled)
        {
            EnsureCollections();
            if (string.IsNullOrEmpty(displayId) || disabled == null)
            {
                return;
            }

            if (enabled)
            {
                disabled.Remove(displayId);
                return;
            }

            if (!disabled.Contains(displayId))
            {
                disabled.Add(displayId);
            }
        }

        public bool AllowsRequest(HungerAndHavoc.Incidents.RHAH_ChoiceKind choice)
        {
            if (choice == HungerAndHavoc.Incidents.RHAH_ChoiceKind.Aid ||
                choice == HungerAndHavoc.Incidents.RHAH_ChoiceKind.ChildExchange)
            {
                return aidRequestsEnabled;
            }

            if (choice == HungerAndHavoc.Incidents.RHAH_ChoiceKind.Intel)
            {
                return intelTradesEnabled;
            }

            if (choice == HungerAndHavoc.Incidents.RHAH_ChoiceKind.None)
            {
                return false;
            }

            return visitorChoicesEnabled;
        }


        internal void CopyNarrative(HungerAndHavoc.Storyteller.Suiyin.SuiyinConfig config)
        {
            if (config == null)
            {
                return;
            }

            config.ProgressKinds = narrativeProgressKinds;
            config.RewardKinds = narrativeRewardKinds;
            config.EnvoyKinds = narrativeEnvoyKinds;
            config.RelicKinds = narrativeRelicKinds;
            config.TheftKinds = narrativeTheftKinds;
            config.RewardSilver = narrativeRewardSilver;
            config.RescueCost = narrativeRescueCost;
            config.RescueReward = narrativeRescueReward;
            config.RelicTakeSilver = narrativeRelicTake;
            config.RelicHandSilver = narrativeRelicHand;
            config.MissingDays = narrativeMissingDays;
            config.ObserveDays = narrativeObserveDays;
            config.HoleIgnoreDays = narrativeHoleIgnoreDays;
            config.EnvoyWaitDays = narrativeEnvoyWaitDays;
            config.EnvoyCheckDays = narrativeEnvoyCheckDays;
            config.RelicDays = narrativeRelicDays;
            config.RevisitYears = narrativeRevisitYears;
            config.AsideCooldownDays = narrativeAsideCooldownDays;
            config.TrustAsideCutoff = narrativeAsideCutoff;
            config.AdultYears = narrativeAdultYears;
            config.TrustBonusPercent = narrativeTrustBonusPercent;
        }

        internal HungerAndHavoc.Incidents.RHAH_WeightFactors WeightFactors()
        {
            return new HungerAndHavoc.Incidents.RHAH_WeightFactors(
                weightWild,
                weightBeggar,
                weightThief,
                weightTrade,
                weightSiege,
                weightAid,
                weightSpecial,
                weightIntel,
                weightSeason,
                weightPlague);
        }

        void ClampPlagueRules()
        {
            plagueSpreadChancePerCarrier = ClampUnit(plagueSpreadChancePerCarrier, 0.005f);
            plagueSpreadChanceCap = ClampUnit(plagueSpreadChanceCap, 0.30f);
            if (plagueSpreadDayInterval < 1)
            {
                plagueSpreadDayInterval = 1;
            }

            if (plagueSpreadDayInterval > 30)
            {
                plagueSpreadDayInterval = 30;
            }

            if (plagueSpreadHour < 0)
            {
                plagueSpreadHour = 0;
            }

            if (plagueSpreadHour > 23)
            {
                plagueSpreadHour = 23;
            }

            if (float.IsNaN(plagueBloodPumpingSkipPercent) || plagueBloodPumpingSkipPercent < 0f)
            {
                plagueBloodPumpingSkipPercent = 120f;
            }

            if (plagueBloodPumpingSkipPercent > 300f)
            {
                plagueBloodPumpingSkipPercent = 300f;
            }

        }

        void ClampFamilyWeights()
        {
            weightWild = ClampFactor(weightWild, 1.4f);
            weightBeggar = ClampFactor(weightBeggar, 1f);
            weightThief = ClampFactor(weightThief, 0.7f);
            weightTrade = ClampFactor(weightTrade, 0.5f);
            weightSiege = ClampFactor(weightSiege, 0.35f);
            weightAid = ClampFactor(weightAid, 0.25f);
            weightSpecial = ClampFactor(weightSpecial, 0.2f);
            weightIntel = ClampFactor(weightIntel, 0.12f);
            weightSeason = ClampFactor(weightSeason, 1.1f);
            weightPlague = ClampFactor(weightPlague, 0.5f);
        }

        static float ClampUnit(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return fallback;
            }

            return value > 1f ? 1f : value;
        }

        static float ClampFactor(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return fallback;
            }

            return value > 5f ? 5f : value;
        }

        void ClampIdentityThresholds()
        {
            endingTrustFloor = ClampSigned(endingTrustFloor, -100, 100);
            endingHopeTrust = ClampSigned(endingHopeTrust, endingTrustFloor, 100);
        }

        void ClampTunables()
        {
            maternalMilkPercent = ClampFloat(maternalMilkPercent, Pawn.RHAH_ParentalGenes.MinPercent, Pawn.RHAH_ParentalGenes.MaxPercent, Pawn.RHAH_ParentalGenes.DefaultMaternalMilkPercent);
            maternalHungerPercent = ClampFloat(maternalHungerPercent, Pawn.RHAH_ParentalGenes.MinPercent, Pawn.RHAH_ParentalGenes.MaxPercent, Pawn.RHAH_ParentalGenes.DefaultMaternalHungerPercent);
            maternalNutritionPercent = ClampFloat(maternalNutritionPercent, Pawn.RHAH_ParentalGenes.MinPercent, Pawn.RHAH_ParentalGenes.MaxPercent, Pawn.RHAH_ParentalGenes.DefaultMaternalNutritionPercent);
            paternalWorkPercent = ClampFloat(paternalWorkPercent, Pawn.RHAH_ParentalGenes.MinPercent, Pawn.RHAH_ParentalGenes.MaxPercent, Pawn.RHAH_ParentalGenes.DefaultPaternalWorkPercent);
            paternalHungerPercent = ClampFloat(paternalHungerPercent, Pawn.RHAH_ParentalGenes.MinPercent, Pawn.RHAH_ParentalGenes.MaxPercent, Pawn.RHAH_ParentalGenes.DefaultPaternalHungerPercent);
            paternalLearningPercent = ClampFloat(paternalLearningPercent, Pawn.RHAH_ParentalGenes.MinPercent, Pawn.RHAH_ParentalGenes.MaxPercent, Pawn.RHAH_ParentalGenes.DefaultPaternalLearningPercent);
            paternalDamagePercent = ClampFloat(paternalDamagePercent, Pawn.RHAH_ParentalGenes.MinPercent, Pawn.RHAH_ParentalGenes.MaxDamagePercent, Pawn.RHAH_ParentalGenes.DefaultPaternalDamagePercent);
            Order(ref requestMinSimple, ref requestMaxSimple, 0, 200, 6, 28);
            Order(ref requestMinFine, ref requestMaxFine, 0, 200, 4, 18);
            Order(ref requestMinMedicine, ref requestMaxMedicine, 0, 100, 2, 10);
            Order(ref requestMinSilver, ref requestMaxSilver, 0, 10000, 80, 1200);
            Order(ref requestMinHerbal, ref requestMaxHerbal, 0, 100, 3, 15);
            requestDays = ClampSigned(requestDays, 0, 30);
            requestPointScale = ClampFloat(requestPointScale, 1f, 10000f, 300f);
            foodPerChild = ClampSigned(foodPerChild, 0, 100);
            foodPerVisitor = ClampSigned(foodPerVisitor, 0, 20);
            maxFoodRequest = ClampSigned(maxFoodRequest, 0, 100);
            envoyMealCost = ClampSigned(envoyMealCost, 0, 100);
            Order(ref campMinAdults, ref campMaxAdults, 0, 40, 2, 4);
            Order(ref campMinChildren, ref campMaxChildren, 0, 80, 8, 16);
            campGoodwill = ClampSigned(campGoodwill, -100, 100);
            campDays = ClampSigned(campDays, 1, 120);
            campHuts = ClampSigned(campHuts, 0, 12);
            holeWoodCost = ClampSigned(holeWoodCost, 0, 200);
            holeCleanPortions = ClampSigned(holeCleanPortions, 0, 50);
            holeLossRange = ClampSigned(holeLossRange, 1, 60);
            holeMaxLosses = ClampSigned(holeMaxLosses, 0, 20);
            apparelAwfulPercent = ClampSigned(apparelAwfulPercent, 0, 100);
            apparelPoorPercent = ClampSigned(apparelPoorPercent, 0, 100 - apparelAwfulPercent);
            Order(ref apparelMinDurabilityPercent, ref apparelMaxDurabilityPercent, 1, 100, 10, 60);
            apparelCorpsePercent = ClampSigned(apparelCorpsePercent, 0, 100);
            apparelClothPercent = ClampSigned(apparelClothPercent, 0, 100);
            apparelMaxPieces = ClampSigned(apparelMaxPieces, 1, 8);
            begFailMood = ClampSigned(begFailMood, -50, 50);
            begSuccessMood = ClampSigned(begSuccessMood, -50, 50);
            begSlapMood = ClampSigned(begSlapMood, -50, 50);
            begSlapKnockoutHours = ClampSigned(begSlapKnockoutHours, 0, 24);
            begBruiseSeverity = ClampFloat(begBruiseSeverity, 0f, 40f, 4f);
            begBruiseStep = ClampFloat(begBruiseStep, 0f, 40f, 4f);
            begBruiseMax = ClampFloat(begBruiseMax, begBruiseSeverity, 40f, 16f);
            barkNutrition = ClampFloat(barkNutrition, 0f, 2f, 0.25f);
            barkDamage = ClampFloat(barkDamage, 0f, 50f, 12f);
            wallNutrition = ClampFloat(wallNutrition, 0f, 2f, 0.2f);
            wallDamage = ClampFloat(wallDamage, 0f, 50f, 10f);
            clayMaxBites = ClampSigned(clayMaxBites, 1, 3);
            clayWindowDays = ClampSigned(clayWindowDays, 1, 60);
            claySeverityPerBite = ClampFloat(claySeverityPerBite, 0f, 1f, 0.33f);
            famineArrivalMalnutrition = ClampFloat(famineArrivalMalnutrition, 0.01f, 0.5f, 0.25f);
            famineFoodSaleFactor = ClampFloat(famineFoodSaleFactor, 1f, 3f, 1.5f);
            famineGoodsBuyFactor = ClampFloat(famineGoodsBuyFactor, 0.1f, 1f, 0.75f);
            childHungryPercent = ClampFloat(childHungryPercent, 0f, 100f, 30f);
            prisonerHungryPercent = ClampFloat(prisonerHungryPercent, 0f, 100f, 20f);
            tailBiteAge = ClampFloat(tailBiteAge, 0f, 18f, 3f);
            scavengeNutrition = ClampFloat(scavengeNutrition, 0f, 2f, 0.15f);
            tailNutrition = ClampFloat(tailNutrition, 0f, 2f, 0.35f);
            tailFailDamage = ClampFloat(tailFailDamage, 0f, 50f, 4f);
            satisfiedFoodPercent = ClampFloat(satisfiedFoodPercent, 1f, 100f, 82f);
            refeedMalnutrition = ClampFloat(refeedMalnutrition, 0f, 1f, 0.4f);
            plagueSeverityMax = ClampFloat(plagueSeverityMax, 0.01f, 1f, 0.1f);
            followPredatorPercent = ClampSigned(followPredatorPercent, 0, 100);
            followBirthWatchDays = ClampSigned(followBirthWatchDays, 0, 60);
            followPlagueBirthDays = ClampSigned(followPlagueBirthDays, 0, 60);
            followMotherReturnDays = ClampSigned(followMotherReturnDays, 0, 120);
            followLongReturnYears = ClampSigned(followLongReturnYears, 0, 20);
            followAdultAge = ClampFloat(followAdultAge, 1f, 80f, 14f);
            followMoodScalePercent = ClampSigned(followMoodScalePercent, 0, 300);
            trustKill = ClampSigned(trustKill, -100, 100);
            trustCaptive = ClampSigned(trustCaptive, -100, 100);
            trustEntrustGood = ClampSigned(trustEntrustGood, -100, 100);
            trustEntrustCaptive = ClampSigned(trustEntrustCaptive, -100, 100);
            trustEntrustStory = ClampSigned(trustEntrustStory, -100, 100);
            trustEntrustRegret = ClampSigned(trustEntrustRegret, -100, 100);
            trustEntrustBanished = ClampSigned(trustEntrustBanished, -100, 100);
            trustExchange = ClampSigned(trustExchange, -100, 100);
            trustHoleOpen = ClampSigned(trustHoleOpen, -100, 100);
            trustHoleIgnore = ClampSigned(trustHoleIgnore, -100, 100);
            trustHoleBait = ClampSigned(trustHoleBait, -100, 100);
            trustEnvoyFail = ClampSigned(trustEnvoyFail, -100, 100);
            trustRelicFail = ClampSigned(trustRelicFail, -100, 100);
            trustHold = ClampSigned(trustHold, -100, 100);
            trustDeliver = ClampSigned(trustDeliver, -100, 100);
            trustLeave = ClampSigned(trustLeave, -100, 100);
            trustExpel = ClampSigned(trustExpel, -100, 100);
            narrativeRewardSilver = ClampSigned(narrativeRewardSilver, 0, 10000);
            narrativeRescueCost = ClampSigned(narrativeRescueCost, 0, 10000);
            narrativeRescueReward = ClampSigned(narrativeRescueReward, 0, 20000);
            narrativeRelicTake = ClampSigned(narrativeRelicTake, 0, 10000);
            narrativeRelicHand = ClampSigned(narrativeRelicHand, 0, 10000);
            narrativeTrustBonusPercent = ClampSigned(narrativeTrustBonusPercent, 0, 100);
            narrativeMissingDays = ClampSigned(narrativeMissingDays, 0, 60);
            narrativeObserveDays = ClampSigned(narrativeObserveDays, 0, 120);
            narrativeHoleIgnoreDays = ClampSigned(narrativeHoleIgnoreDays, 0, 30);
            narrativeEnvoyWaitDays = ClampSigned(narrativeEnvoyWaitDays, 0, 30);
            narrativeEnvoyCheckDays = ClampSigned(narrativeEnvoyCheckDays, 0, 30);
            narrativeRelicDays = ClampSigned(narrativeRelicDays, 0, 120);
            narrativeRevisitYears = ClampSigned(narrativeRevisitYears, 0, 20);
            narrativeAsideCooldownDays = ClampSigned(narrativeAsideCooldownDays, 0, 30);
            narrativeAsideCutoff = ClampSigned(narrativeAsideCutoff, -100, 0);
            narrativeAdultYears = ClampSigned(narrativeAdultYears, 1, 80);
            narrativeTheftKinds = ClampSigned(narrativeTheftKinds, 1, 14);
            narrativeProgressKinds = ClampSigned(narrativeProgressKinds, 1, 14);
            narrativeRewardKinds = ClampSigned(narrativeRewardKinds, 1, 14);
            narrativeEnvoyKinds = ClampSigned(narrativeEnvoyKinds, 1, 14);
            narrativeRelicKinds = ClampSigned(narrativeRelicKinds, 1, 14);
        }

        static int ClampSigned(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        static float ClampFloat(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        static void Order(ref int low, ref int high, int min, int max, int fallbackLow, int fallbackHigh)
        {
            low = ClampSigned(low, min, max);
            high = ClampSigned(high, min, max);
            if (high < low)
            {
                high = low;
            }
        }
    }
}
