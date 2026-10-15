using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace MouseDisaster
{
    public enum PrisonerScavengePoisonMode
    {
        Safe = 0,
        Normal = 1,
        Abuse = 2
    }

    public enum MouseDisasterTradePawnJoinMode
    {
        Slave = 0,
        Prisoner = 1,
        Colonist = 2
    }

    public class MouseDisasterSettings : ModSettings
    {
        public const int MinRatkinAge = 30;
        public const int MaxRatkinAge = 80;
        public const float MinAgeDiseaseMultiplier = 0.5f;
        public const float MaxAgeDiseaseMultiplier = 3f;
        public const float MinChaosPregnancyChancePercent = 0f;
        public const float MaxChaosPregnancyChancePercent = 20f;
        public const int MinChaosPregnancyIntervalTicks = 60;
        public const int MaxChaosPregnancyIntervalTicks = 15000;
        public const float MinRatEggTraitGenerationChance = 0f;
        public const float MaxRatEggTraitGenerationChance = 1f;
        public const float MinFamineYearChancePercent = 0f;
        public const float MaxFamineYearChancePercent = 100f;
        public const float MinFamineYearDisasterBonusPercent = 0f;
        public const float MaxFamineYearDisasterBonusPercent = 100f;
        public const float ScavengeToxicBuildupSafe = 0f;
        public const float ScavengeToxicBuildupNormal = 0.02f;
        public const float ScavengeToxicBuildupAbuse = 0.05f;
        public const int MinWildPredatorSearchIntervalTicks = 60;
        public const int MaxWildPredatorSearchIntervalTicks = 1200;
        public const int DefaultWildPredatorSearchIntervalTicks = 250;
        public const int MinTemporaryRecruitDurationDays = 5;
        public const int DefaultTemporaryRecruitDurationDays = 5;
        public const int MaxTemporaryRecruitDurationDays = GenDate.DaysPerYear;
        public const int MinHiredWorkerDurationDays = 5;
        public const int DefaultHiredWorkerDurationDays = GenDate.DaysPerYear;
        public const int MaxHiredWorkerDurationDays = GenDate.DaysPerYear * 10;
        public const float DefaultFedWanderDays = 0f;
        public const float MinRatkinXenotypeSpawnWeight = 0f;
        public const float MaxRatkinXenotypeSpawnWeight = 100f;
        public const float DefaultRatkinXenotypeSpawnWeight = 85f;
        public const float MinPawnTraitWeight = 0f;
        public const float MaxPawnTraitWeight = 100f;
        public const float DefaultEventFoodWeight = MouseDisasterEventFoodPolicy.DefaultWeight;
        public const float MinEventFoodWeight = MouseDisasterEventFoodPolicy.MinWeight;
        public const float MaxEventFoodWeight = MouseDisasterEventFoodPolicy.MaxWeight;
        public const float DefaultExternalRatkinXenotypeSpawnWeight = 0f;
        public const float DefaultMouseDisasterMinimumEnvironmentTemperature = -35f;
        public const float DefaultMouseDisasterMaximumEnvironmentTemperature = 70f;
        public const float MinMouseDisasterEnvironmentTemperature = -35f;
        public const float MaxMouseDisasterEnvironmentTemperature = 70f;
        public const float MinTemperatureApparelInsulation = 0f;
        public const float MaxTemperatureApparelInsulation = 100f;

        public bool enableNewContent = true;
        public bool successorNoticeDismissed;
        public float refugeePredationChancePercent = 10f;
        public bool refugeePredationFightBack = true;
        public bool outsidePredatorsFollowDifficulty = false;
        public bool wildPredatorsAvoidRatkinWhenFed = true;
        public bool wildPredatorsLeaveAfterFed = false;
        public bool wildPredatorsHuntHomeAreaRatkin = false;
        public int wildPredatorSearchIntervalTicks = DefaultWildPredatorSearchIntervalTicks;
        public bool leaveAfterFed = true;
        public int maxEventPawns = 30;
        public float minGeneratedAge = 0f;
        public float maxGeneratedAge = 50f;
        public float reliefFoodScoreBonus = 0.1f;
        public bool allowEventPawnsEatOutsideReliefArea = false;
        public List<string> disabledEventFoodDefNames = new List<string>();
        public Dictionary<string, float> eventFoodWeights = new Dictionary<string, float>();
        public int EventFoodSettingsStamp { get; private set; }
        public float fedWanderDays = DefaultFedWanderDays;
        public bool waitWhenNoFood = true;
        public float noFoodWaitDays = 0.5f;
        public bool allowMouseDisasterFactionToLeaveWhenIdle = false;
        public bool preventUnnecessaryNeutralPawnRelations = true;
        public bool disableMultiFrameIncidentGeneration = false;
        public int temporaryRecruitDurationDays = DefaultTemporaryRecruitDurationDays;
        public int hiredWorkerDurationDays = DefaultHiredWorkerDurationDays;
        public Dictionary<string, float> ratkinXenotypeSpawnWeights = new Dictionary<string, float>();
        public bool countWithoutSuin = true;
        public bool endingsWithoutSuin = true;
        public float positiveIncidentDays = 3f;
        public float negativeIncidentDays = 3f;
        public bool allowColonistChildcareForMouseDisasterEggs = false;
        public bool allowNonColonistChildcareForMouseDisasterEggs = false;
        public Dictionary<string, bool> positiveIncidents = new Dictionary<string, bool>();
        public Dictionary<string, bool> raidReplacementIncidents = new Dictionary<string, bool>();

        public bool IsPositiveIncident(RimWorld.IncidentDef def) =>
            positiveIncidents.TryGetValue(def.defName, out bool positive) ? positive :
            def.letterDef == RimWorld.LetterDefOf.PositiveEvent;

        public bool ReplacesRaid(string name) => raidReplacementIncidents.TryGetValue(name, out bool replace) && replace;
        public bool allowColonistAutoGiveFood = false;
        public MouseDisasterTradePawnJoinMode ratkinYoungTradeJoinMode = MouseDisasterTradePawnJoinMode.Slave;
        public Dictionary<string, MouseDisasterEventAttitude> eventAttitudes = new Dictionary<string, MouseDisasterEventAttitude>();

        public MouseDisasterEventAttitude GetEventAttitude(string defName)
        {
            return defName != null && eventAttitudes != null && eventAttitudes.TryGetValue(defName, out var attitude)
                ? MouseDisasterEventPolicy.Normalize(attitude) : MouseDisasterEventAttitude.Neutral;
        }

        public MouseDisasterEventAttitude? GetNarrativeAttitude(string id)
        {
            var sources = MouseDisasterNarrativePolicy.GetAttitudeSources(id);
            if (sources.Count == 0) return null;
            var first = GetEventAttitude(sources[0]);
            return sources.All(source => GetEventAttitude(source) == first) ? first : (MouseDisasterEventAttitude?)null;
        }

        public void SetNarrativeAttitude(string id, MouseDisasterEventAttitude attitude)
        {
            eventAttitudes ??= new Dictionary<string, MouseDisasterEventAttitude>();
            foreach (string source in MouseDisasterNarrativePolicy.GetAttitudeSources(id))
                eventAttitudes[source] = MouseDisasterEventPolicy.Normalize(attitude);
        }
        public bool enableAgeCapAdjustment = true;
        public bool enableWildRatkinIncidents = true;
        public bool enableThiefIncidents = true;
        public bool enableBeggarIncidents = true;
        public bool incidentToggleMigrationApplied = false;
        public bool enableGnawing = true;
        public bool enablePrisonerScavenge = true;
        public bool prisonerScavengeMigrationApplied = false;
        public bool enableFloatingText = true;
        public bool enableTemperatureProtectionApparel = true;
        public float mouseDisasterMinimumEnvironmentTemperature = DefaultMouseDisasterMinimumEnvironmentTemperature;
        public float mouseDisasterMaximumEnvironmentTemperature = DefaultMouseDisasterMaximumEnvironmentTemperature;
        public Dictionary<string, float> temperatureApparelInsulation = new Dictionary<string, float>();
        public List<string> disabledTemperatureApparelDefNames = new List<string>();
        public bool enablePrisonerScavengeDebugLog = false;
        public bool enableDetailedTraceLog = false;
        public bool forceDismissMobileNonInfantEventPawns = false;
        public bool enableExperimentalTailBite = false;
        public PrisonerScavengePoisonMode prisonerScavengePoisonMode = PrisonerScavengePoisonMode.Normal;
        public bool enableChaosRoomPregnancy = true;
        public bool enableExperimentalIdentityInheritance = false;
        public float chaosPregnancyChancePercent = 2f;
        public int chaosPregnancyCheckIntervalTicks = 3000;
        public int broadcastHopeCooldownDays = 3;
        public int narrativeAidGoal = 99;
        public int narrativeBroadcastGoal = 3;
        public int narrativeDriveLimit = 3;
        public int narrativeAdultGoal = 100;
        public bool enableNarrative = true;
        public List<string> disabledNarrativeIds = new List<string>();
        public int narrativeProgressGoal = 3;
        public int narrativeRewardGoal = 8;
        public int narrativeTheftGoal = 2;
        public int narrativeEnvoyGoal = 5;
        public int narrativeRelicGoal = 8;
        public int narrativeEndingDelayDays = 30;
        public float narrativeReturnChancePercent = 100f;
        public int narrativeReturnDelayDays = 15;
        public float narrativeN004ReturnChancePercent = 100f;
        public float narrativeEchoChancePercent = 100f;
        public int narrativeEchoCooldownDays = 3;
        public bool enableFamineYearSystem = false;
        public float famineYearChancePercent = 40f;
        public float famineYearDisasterBonusPercent = 20f;
        public bool enableRatEggTraitsBridge = true;
        public float ratEggTraitGenerationChance = 0.5f;
        public bool enablePawnHistories = true;
        public List<string> enabledPawnHistoryIds = CreateDefaultPawnHistoryIds();
        public bool enablePawnTraits = true;
        public List<string> enabledPawnTraitIds = CreateDefaultPawnTraitIds();
        public Dictionary<string, float> pawnTraitWeights = new Dictionary<string, float>();
        public List<string> disabledIncidentDefNames = new List<string>();
        public int maxRatkinAge = 50;
        public float ageDiseaseMultiplier = 1f;

        public void ResetToDefaults()
        {
            refugeePredationChancePercent = 10f;
            refugeePredationFightBack = true;
            outsidePredatorsFollowDifficulty = false;
            wildPredatorsAvoidRatkinWhenFed = true;
            wildPredatorsLeaveAfterFed = false;
            wildPredatorsHuntHomeAreaRatkin = false;
            wildPredatorSearchIntervalTicks = DefaultWildPredatorSearchIntervalTicks;
            leaveAfterFed = countWithoutSuin = endingsWithoutSuin = true;
            maxEventPawns = 30;
            minGeneratedAge = 0f;
            maxGeneratedAge = 50f;
            reliefFoodScoreBonus = 0.1f;
            allowEventPawnsEatOutsideReliefArea = false;
            disabledEventFoodDefNames = new List<string>();
            eventFoodWeights = new Dictionary<string, float>();
            TouchEventFoodSettings();
            fedWanderDays = DefaultFedWanderDays;
            noFoodWaitDays = 0.5f;
            waitWhenNoFood = true;
            allowMouseDisasterFactionToLeaveWhenIdle = false;
            preventUnnecessaryNeutralPawnRelations = true;
            disableMultiFrameIncidentGeneration = false;
            temporaryRecruitDurationDays = DefaultTemporaryRecruitDurationDays;
            hiredWorkerDurationDays = DefaultHiredWorkerDurationDays;
            ratkinXenotypeSpawnWeights = new Dictionary<string, float>();
            positiveIncidentDays = negativeIncidentDays = 3f;
            positiveIncidents.Clear();
            raidReplacementIncidents.Clear();
            eventAttitudes.Clear();
            enableNewContent = true;
            allowColonistAutoGiveFood = false;
            ratkinYoungTradeJoinMode = MouseDisasterTradePawnJoinMode.Slave;
            allowColonistChildcareForMouseDisasterEggs = false;
            allowNonColonistChildcareForMouseDisasterEggs = false;
            enableAgeCapAdjustment = true;
            enableWildRatkinIncidents = true;
            enableThiefIncidents = true;
            enableBeggarIncidents = true;
            incidentToggleMigrationApplied = true;
            enableGnawing = true;
            enablePrisonerScavenge = true;
            prisonerScavengeMigrationApplied = true;
            enableFloatingText = true;
            enableTemperatureProtectionApparel = true;
            mouseDisasterMinimumEnvironmentTemperature = DefaultMouseDisasterMinimumEnvironmentTemperature;
            mouseDisasterMaximumEnvironmentTemperature = DefaultMouseDisasterMaximumEnvironmentTemperature;
            temperatureApparelInsulation = new Dictionary<string, float>();
            disabledTemperatureApparelDefNames = new List<string>();
            enablePrisonerScavengeDebugLog = false;
            enableDetailedTraceLog = false;
            forceDismissMobileNonInfantEventPawns = false;
            enableExperimentalTailBite = false;
            prisonerScavengePoisonMode = PrisonerScavengePoisonMode.Normal;
            enableChaosRoomPregnancy = true;
            enableExperimentalIdentityInheritance = false;
            chaosPregnancyChancePercent = 2f;
            chaosPregnancyCheckIntervalTicks = 3000;
            broadcastHopeCooldownDays = 3;
            narrativeAidGoal = 99;
            narrativeBroadcastGoal = 3;
            narrativeDriveLimit = 3;
            narrativeAdultGoal = 100;
            enableNarrative = true;
            disabledNarrativeIds = new List<string>();
            narrativeProgressGoal = 3;
            narrativeRewardGoal = 8;
            narrativeTheftGoal = 2;
            narrativeEnvoyGoal = 5;
            narrativeRelicGoal = 8;
            narrativeEndingDelayDays = 30;
            narrativeReturnChancePercent = 100f;
            narrativeReturnDelayDays = 15;
            narrativeN004ReturnChancePercent = 100f;
            narrativeEchoChancePercent = 100f;
            narrativeEchoCooldownDays = 3;
            enableFamineYearSystem = false;
            famineYearChancePercent = 40f;
            famineYearDisasterBonusPercent = 20f;
            enableRatEggTraitsBridge = true;
            ratEggTraitGenerationChance = 0.5f;
            enablePawnHistories = true;
            enabledPawnHistoryIds = CreateDefaultPawnHistoryIds();
            enablePawnTraits = true;
            enabledPawnTraitIds = CreateDefaultPawnTraitIds();
            pawnTraitWeights = new Dictionary<string, float>();
            disabledIncidentDefNames = new List<string>();
            maxRatkinAge = 50;
            ageDiseaseMultiplier = 1f;
            ClampValues();
        }

        public bool IsIncidentEnabled(string defName)
        {
            return MouseDisasterIncidentCatalog.IsIncidentEnabled(defName, disabledIncidentDefNames);
        }

        public void SetIncidentEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return;
            }

            disabledIncidentDefNames ??= new List<string>();
            disabledIncidentDefNames.RemoveAll(entry => entry.Equals(defName, System.StringComparison.OrdinalIgnoreCase));
            if (!enabled)
            {
                disabledIncidentDefNames.Add(defName);
            }

            NormalizeIncidentToggleState();
        }

        public void ClampValues()
        {
            refugeePredationChancePercent = float.IsNaN(refugeePredationChancePercent) ? 10f : Mathf.Clamp(refugeePredationChancePercent, 0f, 100f);
            positiveIncidentDays = Mathf.Clamp(positiveIncidentDays, 0f, 60f);
            negativeIncidentDays = Mathf.Clamp(negativeIncidentDays, 0f, 60f);
            narrativeProgressGoal = Mathf.Clamp(narrativeProgressGoal, 1, 50);
            narrativeRewardGoal = Mathf.Clamp(narrativeRewardGoal, 1, 50);
            narrativeTheftGoal = Mathf.Clamp(narrativeTheftGoal, 1, 20);
            narrativeEnvoyGoal = Mathf.Clamp(narrativeEnvoyGoal, 1, 50);
            narrativeRelicGoal = Mathf.Clamp(narrativeRelicGoal, 1, 50);
            narrativeEndingDelayDays = Mathf.Clamp(narrativeEndingDelayDays, 0, 120);
            narrativeReturnChancePercent = Mathf.Clamp(narrativeReturnChancePercent, 0f, 100f);
            narrativeReturnDelayDays = Mathf.Clamp(narrativeReturnDelayDays, 1, 120);
            narrativeN004ReturnChancePercent = Mathf.Clamp(narrativeN004ReturnChancePercent, 0f, 100f);
            narrativeEchoChancePercent = Mathf.Clamp(narrativeEchoChancePercent, 0f, 100f);
            narrativeEchoCooldownDays = Mathf.Clamp(narrativeEchoCooldownDays, 1, 60);
            disabledNarrativeIds = (disabledNarrativeIds ?? new List<string>()).Where(id => MouseDisasterNarrativePolicy.SettingIds.Contains(id)).Distinct().ToList();
            narrativeAidGoal = Mathf.Clamp(narrativeAidGoal, 1, 999);
            narrativeBroadcastGoal = Mathf.Clamp(narrativeBroadcastGoal, 1, 99);
            narrativeDriveLimit = Mathf.Clamp(narrativeDriveLimit, 0, 99);
            narrativeAdultGoal = Mathf.Clamp(narrativeAdultGoal, 1, 500);
            temporaryRecruitDurationDays = Mathf.Clamp(temporaryRecruitDurationDays,
                MinTemporaryRecruitDurationDays, MaxTemporaryRecruitDurationDays);
            hiredWorkerDurationDays = Mathf.Clamp(hiredWorkerDurationDays,
                MinHiredWorkerDurationDays, MaxHiredWorkerDurationDays);
            NormalizeRatkinXenotypeSpawnWeights();
            maxEventPawns = Mathf.Clamp(maxEventPawns, 1, 100);
            minGeneratedAge = float.IsNaN(minGeneratedAge) ? 0f : Mathf.Clamp(minGeneratedAge, 0f, 100f);
            maxGeneratedAge = float.IsNaN(maxGeneratedAge) ? 50f : Mathf.Clamp(maxGeneratedAge, minGeneratedAge, 100f);
            reliefFoodScoreBonus = float.IsNaN(reliefFoodScoreBonus) ? 0.1f : Mathf.Clamp01(reliefFoodScoreBonus);
            NormalizeEventFoodSettings();
            fedWanderDays = float.IsNaN(fedWanderDays) ? DefaultFedWanderDays : Mathf.Clamp(fedWanderDays, 0f, 5f);
            noFoodWaitDays = float.IsNaN(noFoodWaitDays) ? 0.5f : Mathf.Clamp(noFoodWaitDays, 0f, 5f);
            maxRatkinAge = Mathf.Clamp(maxRatkinAge, MinRatkinAge, MaxRatkinAge);
            ageDiseaseMultiplier = Mathf.Clamp(ageDiseaseMultiplier, MinAgeDiseaseMultiplier, MaxAgeDiseaseMultiplier);
            chaosPregnancyChancePercent = Mathf.Clamp(chaosPregnancyChancePercent, MinChaosPregnancyChancePercent, MaxChaosPregnancyChancePercent);
            chaosPregnancyCheckIntervalTicks = Mathf.Clamp(chaosPregnancyCheckIntervalTicks, MinChaosPregnancyIntervalTicks, MaxChaosPregnancyIntervalTicks);
            wildPredatorSearchIntervalTicks = Mathf.Clamp(wildPredatorSearchIntervalTicks,
                MinWildPredatorSearchIntervalTicks, MaxWildPredatorSearchIntervalTicks);
            broadcastHopeCooldownDays = MouseDisasterBroadcastHopePolicy.NormalizeCooldownDays(broadcastHopeCooldownDays);
            famineYearChancePercent = Mathf.Clamp(famineYearChancePercent, MinFamineYearChancePercent, MaxFamineYearChancePercent);
            famineYearDisasterBonusPercent = Mathf.Clamp(famineYearDisasterBonusPercent, MinFamineYearDisasterBonusPercent, MaxFamineYearDisasterBonusPercent);
            ratEggTraitGenerationChance = Mathf.Clamp(ratEggTraitGenerationChance, MinRatEggTraitGenerationChance, MaxRatEggTraitGenerationChance);
            mouseDisasterMinimumEnvironmentTemperature = NormalizeTemperature(
                mouseDisasterMinimumEnvironmentTemperature, DefaultMouseDisasterMinimumEnvironmentTemperature);
            mouseDisasterMaximumEnvironmentTemperature = NormalizeTemperature(
                mouseDisasterMaximumEnvironmentTemperature, DefaultMouseDisasterMaximumEnvironmentTemperature);
            if (mouseDisasterMinimumEnvironmentTemperature > mouseDisasterMaximumEnvironmentTemperature)
            {
                float temperature = mouseDisasterMinimumEnvironmentTemperature;
                mouseDisasterMinimumEnvironmentTemperature = mouseDisasterMaximumEnvironmentTemperature;
                mouseDisasterMaximumEnvironmentTemperature = temperature;
            }
            NormalizeTemperatureApparelSettings();
            NormalizePawnHistorySettings();
            NormalizePawnTraitSettings();
            if (!System.Enum.IsDefined(typeof(PrisonerScavengePoisonMode), prisonerScavengePoisonMode))
            {
                prisonerScavengePoisonMode = PrisonerScavengePoisonMode.Normal;
            }
            if (!System.Enum.IsDefined(typeof(MouseDisasterTradePawnJoinMode), ratkinYoungTradeJoinMode))
            {
                ratkinYoungTradeJoinMode = MouseDisasterTradePawnJoinMode.Slave;
            }
        }

        public int GetTemporaryRecruitDurationDays()
        {
            return Mathf.Clamp(temporaryRecruitDurationDays,
                MinTemporaryRecruitDurationDays, MaxTemporaryRecruitDurationDays);
        }

        public int GetHiredWorkerDurationDays()
        {
            return Mathf.Clamp(hiredWorkerDurationDays,
                MinHiredWorkerDurationDays, MaxHiredWorkerDurationDays);
        }

        public float GetRatkinXenotypeSpawnWeight(string defName, float fallback)
        {
            float normalizedFallback = NormalizeRatkinXenotypeSpawnWeight(fallback);
            if (string.IsNullOrWhiteSpace(defName) || ratkinXenotypeSpawnWeights == null ||
                !ratkinXenotypeSpawnWeights.TryGetValue(defName, out float value))
            {
                return normalizedFallback;
            }

            return NormalizeRatkinXenotypeSpawnWeight(value);
        }

        public void SetRatkinXenotypeSpawnWeight(string defName, float value)
        {
            if (string.IsNullOrWhiteSpace(defName))
            {
                return;
            }

            ratkinXenotypeSpawnWeights ??= new Dictionary<string, float>();
            ratkinXenotypeSpawnWeights[defName.Trim()] = NormalizeRatkinXenotypeSpawnWeight(value);
        }

        public void ResetRatkinXenotypeSpawnWeights()
        {
            ratkinXenotypeSpawnWeights = new Dictionary<string, float>();
        }

        public MouseDisasterTradePawnJoinMode GetRatkinYoungTradeJoinMode()
        {
            return System.Enum.IsDefined(typeof(MouseDisasterTradePawnJoinMode), ratkinYoungTradeJoinMode)
                ? ratkinYoungTradeJoinMode
                : MouseDisasterTradePawnJoinMode.Slave;
        }

        public float GetPrisonerScavengeToxicBuildupPerEat()
        {
            switch (prisonerScavengePoisonMode)
            {
                case PrisonerScavengePoisonMode.Safe:
                    return ScavengeToxicBuildupSafe;
                case PrisonerScavengePoisonMode.Abuse:
                    return ScavengeToxicBuildupAbuse;
                default:
                    return ScavengeToxicBuildupNormal;
            }
        }

        public bool IsMouseDisasterEnvironmentTemperatureAllowed(float temperature)
        {
            return !float.IsNaN(temperature) && !float.IsInfinity(temperature) &&
                   temperature >= mouseDisasterMinimumEnvironmentTemperature - 0.001f &&
                   temperature <= mouseDisasterMaximumEnvironmentTemperature + 0.001f;
        }

        public bool IsTemperatureApparelEnabled(string defName)
        {
            return !string.IsNullOrWhiteSpace(defName) &&
                   !(disabledTemperatureApparelDefNames ?? new List<string>())
                       .Any(entry => string.Equals(entry, defName, System.StringComparison.OrdinalIgnoreCase));
        }

        public void SetTemperatureApparelEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(defName)) return;
            disabledTemperatureApparelDefNames ??= new List<string>();
            disabledTemperatureApparelDefNames.RemoveAll(entry =>
                string.Equals(entry, defName, System.StringComparison.OrdinalIgnoreCase));
            if (!enabled) disabledTemperatureApparelDefNames.Add(defName);
            NormalizeTemperatureApparelSettings();
        }

        public bool IsPawnHistorySelected(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   (enabledPawnHistoryIds ?? new List<string>())
                       .Any(entry => string.Equals(entry, id, System.StringComparison.OrdinalIgnoreCase));
        }

        public bool IsPawnHistoryEnabled(string id)
        {
            return enablePawnHistories && IsPawnHistorySelected(id);
        }

        public void SetPawnHistoryEnabled(string id, bool enabled)
        {
            if (!MouseDisasterPawnHistoryCatalog.IsKnown(id)) return;
            enabledPawnHistoryIds ??= new List<string>();
            enabledPawnHistoryIds.RemoveAll(entry =>
                string.Equals(entry, id, System.StringComparison.OrdinalIgnoreCase));
            if (enabled) enabledPawnHistoryIds.Add(id);
            NormalizePawnHistorySettings();
        }

        public void SetAllPawnHistoriesEnabled(bool enabled)
        {
            enabledPawnHistoryIds = enabled
                ? MouseDisasterPawnHistoryCatalog.All.Select(history => history.Id).ToList()
                : new List<string>();
            NormalizePawnHistorySettings();
        }

        public bool IsPawnTraitSelected(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   (enabledPawnTraitIds ?? new List<string>())
                       .Any(entry => string.Equals(entry, id, System.StringComparison.OrdinalIgnoreCase));
        }

        public bool IsPawnTraitEnabled(string id)
        {
            return enablePawnTraits && IsPawnTraitSelected(id);
        }

        public void SetPawnTraitEnabled(string id, bool enabled)
        {
            if (!MouseDisasterTraitCatalog.IsKnown(id)) return;
            enabledPawnTraitIds ??= new List<string>();
            enabledPawnTraitIds.RemoveAll(entry =>
                string.Equals(entry, id, System.StringComparison.OrdinalIgnoreCase));
            if (enabled) enabledPawnTraitIds.Add(id);
            NormalizePawnTraitSettings();
        }

        public void SetAllPawnTraitsEnabled(bool enabled)
        {
            enabledPawnTraitIds = enabled
                ? MouseDisasterTraitCatalog.All.Select(trait => trait.Id).ToList()
                : new List<string>();
            NormalizePawnTraitSettings();
        }

        public float GetPawnTraitWeight(string id)
        {
            float fallback = MouseDisasterTraitCatalog.GetDefaultWeight(id);
            if (string.IsNullOrWhiteSpace(id) || pawnTraitWeights == null ||
                !pawnTraitWeights.TryGetValue(id, out float value))
            {
                return fallback;
            }

            return NormalizePawnTraitWeight(value, fallback);
        }

        public void SetPawnTraitWeight(string id, float value)
        {
            if (!MouseDisasterTraitCatalog.IsKnown(id)) return;
            pawnTraitWeights ??= new Dictionary<string, float>();
            pawnTraitWeights[id] = NormalizePawnTraitWeight(value, MouseDisasterTraitCatalog.GetDefaultWeight(id));
            NormalizePawnTraitSettings();
        }

        public bool AllowsEventPawnsEatOutsideReliefArea => allowEventPawnsEatOutsideReliefArea;

        public bool IsEventFoodEnabled(string defName)
        {
            return MouseDisasterEventFoodPolicy.IsFoodAllowed(defName, disabledEventFoodDefNames);
        }

        public void SetEventFoodEnabled(string defName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(defName)) return;
            disabledEventFoodDefNames ??= new List<string>();
            disabledEventFoodDefNames.RemoveAll(entry =>
                string.Equals(entry, defName, System.StringComparison.OrdinalIgnoreCase));
            if (!enabled) disabledEventFoodDefNames.Add(defName.Trim());
            NormalizeEventFoodSettings();
            TouchEventFoodSettings();
        }

        public void SetAllEventFoodsEnabled(bool enabled)
        {
            if (enabled)
            {
                disabledEventFoodDefNames = new List<string>();
            }
            else
            {
                disabledEventFoodDefNames = MouseDisasterEventFoodPolicy.AllFoodDefs()
                    .Select(def => def.defName)
                    .ToList();
            }
            NormalizeEventFoodSettings();
            TouchEventFoodSettings();
        }

        public float GetEventFoodWeight(string defName)
        {
            return MouseDisasterEventFoodPolicy.ResolveWeight(defName, eventFoodWeights);
        }

        public void SetEventFoodWeight(string defName, float value)
        {
            if (string.IsNullOrWhiteSpace(defName)) return;
            eventFoodWeights ??= new Dictionary<string, float>();
            eventFoodWeights[defName.Trim()] = MouseDisasterEventFoodPolicy.NormalizeWeight(value);
            NormalizeEventFoodSettings();
            TouchEventFoodSettings();
        }

        public void ResetEventFoodWeights()
        {
            eventFoodWeights = new Dictionary<string, float>();
            TouchEventFoodSettings();
        }

        internal void TouchEventFoodSettings()
        {
            EventFoodSettingsStamp++;
        }

        public float GetTemperatureApparelInsulation(string defName, float fallback)
        {
            if (temperatureApparelInsulation != null && temperatureApparelInsulation.TryGetValue(defName, out float value) &&
                !float.IsNaN(value) && !float.IsInfinity(value))
            {
                return Mathf.Clamp(value, MinTemperatureApparelInsulation, MaxTemperatureApparelInsulation);
            }

            return Mathf.Clamp(fallback, MinTemperatureApparelInsulation, MaxTemperatureApparelInsulation);
        }

        public void SetTemperatureApparelInsulation(string defName, float value)
        {
            if (string.IsNullOrWhiteSpace(defName)) return;
            temperatureApparelInsulation ??= new Dictionary<string, float>();
            temperatureApparelInsulation[defName] = Mathf.Clamp(value,
                MinTemperatureApparelInsulation, MaxTemperatureApparelInsulation);
        }

        private static float NormalizeTemperature(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : Mathf.Clamp(value, MinMouseDisasterEnvironmentTemperature, MaxMouseDisasterEnvironmentTemperature);
        }

        private void NormalizeTemperatureApparelSettings()
        {
            var normalizedInsulation = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var option in MouseDisasterUtility.AllTemperatureApparelOptions)
            {
                float value = option.Insulation;
                if (temperatureApparelInsulation != null && temperatureApparelInsulation.TryGetValue(option.DefName, out float configured))
                    value = configured;
                if (float.IsNaN(value) || float.IsInfinity(value)) value = option.Insulation;
                normalizedInsulation[option.DefName] = Mathf.Clamp(value,
                    MinTemperatureApparelInsulation, MaxTemperatureApparelInsulation);
            }
            temperatureApparelInsulation = normalizedInsulation;
            disabledTemperatureApparelDefNames = (disabledTemperatureApparelDefNames ?? new List<string>())
                .Where(defName => !string.IsNullOrWhiteSpace(defName))
                .Select(defName => defName.Trim())
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .Where(defName => MouseDisasterUtility.AllTemperatureApparelOptions.Any(option =>
                    string.Equals(option.DefName, defName, System.StringComparison.OrdinalIgnoreCase)))
                .OrderBy(defName => defName)
                .ToList();
        }

        private void NormalizeRatkinXenotypeSpawnWeights()
        {
            var normalized = new Dictionary<string, float>();
            if (ratkinXenotypeSpawnWeights != null)
            {
                foreach (KeyValuePair<string, float> entry in ratkinXenotypeSpawnWeights)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    normalized[entry.Key.Trim()] = NormalizeRatkinXenotypeSpawnWeight(entry.Value);
                }
            }

            ratkinXenotypeSpawnWeights = normalized;
        }

        private static float NormalizeRatkinXenotypeSpawnWeight(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? DefaultRatkinXenotypeSpawnWeight
                : Mathf.Clamp(value, MinRatkinXenotypeSpawnWeight, MaxRatkinXenotypeSpawnWeight);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref refugeePredationChancePercent, "refugeePredationChancePercent", 10f);
            Scribe_Values.Look(ref refugeePredationFightBack, "refugeePredationFightBack", true);
            Scribe_Values.Look(ref outsidePredatorsFollowDifficulty, "outsidePredatorsFollowDifficulty", false);
            Scribe_Values.Look(ref wildPredatorsAvoidRatkinWhenFed, "wildPredatorsAvoidRatkinWhenFed", true);
            Scribe_Values.Look(ref wildPredatorsLeaveAfterFed, "wildPredatorsLeaveAfterFed", false);
            Scribe_Values.Look(ref wildPredatorsHuntHomeAreaRatkin, "wildPredatorsHuntHomeAreaRatkin", false);
            Scribe_Values.Look(ref wildPredatorSearchIntervalTicks, "wildPredatorSearchIntervalTicks", DefaultWildPredatorSearchIntervalTicks);
            Scribe_Values.Look(ref leaveAfterFed, "leaveAfterFed", true);
            Scribe_Values.Look(ref maxEventPawns, "maxEventPawns", 30);
            Scribe_Values.Look(ref minGeneratedAge, "minGeneratedAge", 0f);
            Scribe_Values.Look(ref maxGeneratedAge, "maxGeneratedAge", 50f);
            Scribe_Values.Look(ref reliefFoodScoreBonus, "reliefFoodScoreBonus", 0.1f);
            Scribe_Values.Look(ref allowEventPawnsEatOutsideReliefArea, "allowEventPawnsEatOutsideReliefArea", false);
            Scribe_Collections.Look(ref disabledEventFoodDefNames, "disabledEventFoodDefNames", LookMode.Value);
            Scribe_Collections.Look(ref eventFoodWeights, "eventFoodWeights", LookMode.Value, LookMode.Value);
            disabledEventFoodDefNames ??= new List<string>();
            eventFoodWeights ??= new Dictionary<string, float>();
            Scribe_Values.Look(ref fedWanderDays, "fedWanderDays", DefaultFedWanderDays);
            Scribe_Values.Look(ref waitWhenNoFood, "waitWhenNoFood", true);
            Scribe_Values.Look(ref noFoodWaitDays, "noFoodWaitDays", 0.5f);
            Scribe_Values.Look(ref allowMouseDisasterFactionToLeaveWhenIdle, "allowMouseDisasterFactionToLeaveWhenIdle", false);
            Scribe_Values.Look(ref preventUnnecessaryNeutralPawnRelations, "preventUnnecessaryNeutralPawnRelations", true);
            Scribe_Values.Look(ref disableMultiFrameIncidentGeneration, "disableMultiFrameIncidentGeneration", false);
            Scribe_Values.Look(ref temporaryRecruitDurationDays, "temporaryRecruitDurationDays", DefaultTemporaryRecruitDurationDays);
            Scribe_Values.Look(ref hiredWorkerDurationDays, "hiredWorkerDurationDays", DefaultHiredWorkerDurationDays);
            Scribe_Collections.Look(ref ratkinXenotypeSpawnWeights, "ratkinXenotypeSpawnWeights", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref countWithoutSuin, "countWithoutSuin", true);
            Scribe_Values.Look(ref endingsWithoutSuin, "endingsWithoutSuin", true);
            Scribe_Values.Look(ref positiveIncidentDays, "positiveIncidentDays", 3f);
            Scribe_Values.Look(ref negativeIncidentDays, "negativeIncidentDays", 3f);
            Scribe_Collections.Look(ref positiveIncidents, "positiveIncidents", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref raidReplacementIncidents, "raidReplacementIncidents", LookMode.Value, LookMode.Value);
            positiveIncidents ??= new Dictionary<string, bool>();
            raidReplacementIncidents ??= new Dictionary<string, bool>();
            Scribe_Collections.Look(ref eventAttitudes, "eventAttitudes", LookMode.Value, LookMode.Value);
            eventAttitudes ??= new Dictionary<string, MouseDisasterEventAttitude>();
            Scribe_Values.Look(ref enableNarrative, "enableNarrative", true);
            Scribe_Collections.Look(ref disabledNarrativeIds, "disabledNarrativeIds", LookMode.Value);
            Scribe_Values.Look(ref narrativeProgressGoal, "narrativeProgressGoal", 3);
            Scribe_Values.Look(ref narrativeRewardGoal, "narrativeRewardGoal", 8);
            Scribe_Values.Look(ref narrativeTheftGoal, "narrativeTheftGoal", 2);
            Scribe_Values.Look(ref narrativeEnvoyGoal, "narrativeEnvoyGoal", 5);
            Scribe_Values.Look(ref narrativeRelicGoal, "narrativeRelicGoal", 8);
            Scribe_Values.Look(ref narrativeEndingDelayDays, "narrativeEndingDelayDays", 30);
            Scribe_Values.Look(ref narrativeReturnChancePercent, "narrativeReturnChancePercent", 100f);
            Scribe_Values.Look(ref narrativeReturnDelayDays, "narrativeReturnDelayDays", 15);
            Scribe_Values.Look(ref narrativeN004ReturnChancePercent, "narrativeN004ReturnChancePercent", 100f);
            Scribe_Values.Look(ref narrativeEchoChancePercent, "narrativeEchoChancePercent", 100f);
            Scribe_Values.Look(ref narrativeEchoCooldownDays, "narrativeEchoCooldownDays", 3);
            Scribe_Values.Look(ref enableNewContent, "enableNewContent", true);
            Scribe_Values.Look(ref successorNoticeDismissed, "successorNoticeDismissed", false);
            Scribe_Values.Look(ref allowColonistAutoGiveFood, "allowColonistAutoGiveFood", false);
            int ratkinYoungTradeJoinModeRaw = (int)ratkinYoungTradeJoinMode;
            Scribe_Values.Look(ref ratkinYoungTradeJoinModeRaw, "ratkinYoungTradeJoinMode", (int)MouseDisasterTradePawnJoinMode.Slave);
            ratkinYoungTradeJoinMode = System.Enum.IsDefined(typeof(MouseDisasterTradePawnJoinMode), ratkinYoungTradeJoinModeRaw)
                ? (MouseDisasterTradePawnJoinMode)ratkinYoungTradeJoinModeRaw
                : MouseDisasterTradePawnJoinMode.Slave;
            Scribe_Values.Look(ref allowColonistChildcareForMouseDisasterEggs, "allowColonistChildcareForMouseDisasterEggs", false);
            Scribe_Values.Look(ref allowNonColonistChildcareForMouseDisasterEggs, "allowNonColonistChildcareForMouseDisasterEggs", false);
            Scribe_Values.Look(ref enableAgeCapAdjustment, "enableAgeCapAdjustment", true);
            Scribe_Values.Look(ref enableWildRatkinIncidents, "enableWildRatkinIncidents", true);
            Scribe_Values.Look(ref enableThiefIncidents, "enableThiefIncidents", true);
            Scribe_Values.Look(ref enableBeggarIncidents, "enableBeggarIncidents", true);
            Scribe_Values.Look(ref incidentToggleMigrationApplied, "incidentToggleMigrationApplied", false);
            Scribe_Values.Look(ref enableGnawing, "enableGnawing", true);
            Scribe_Values.Look(ref enablePrisonerScavenge, "enablePrisonerScavenge", true);
            Scribe_Values.Look(ref prisonerScavengeMigrationApplied, "prisonerScavengeMigrationApplied", false);
            Scribe_Values.Look(ref enableFloatingText, "enableFloatingText", true);
            Scribe_Values.Look(ref enableTemperatureProtectionApparel, "enableTemperatureProtectionApparel", true);
            Scribe_Values.Look(ref mouseDisasterMinimumEnvironmentTemperature, "mouseDisasterMinimumEnvironmentTemperature", DefaultMouseDisasterMinimumEnvironmentTemperature);
            Scribe_Values.Look(ref mouseDisasterMaximumEnvironmentTemperature, "mouseDisasterMaximumEnvironmentTemperature", DefaultMouseDisasterMaximumEnvironmentTemperature);
            Scribe_Collections.Look(ref temperatureApparelInsulation, "temperatureApparelInsulation", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref disabledTemperatureApparelDefNames, "disabledTemperatureApparelDefNames", LookMode.Value);
            Scribe_Values.Look(ref enablePrisonerScavengeDebugLog, "enablePrisonerScavengeDebugLog", false);
            Scribe_Values.Look(ref enableDetailedTraceLog, "enableDetailedTraceLog", false);
            Scribe_Values.Look(ref forceDismissMobileNonInfantEventPawns, "forceDismissMobileNonInfantEventPawns", false);
            Scribe_Values.Look(ref enableExperimentalTailBite, "enableExperimentalTailBite", false);
            int poisonModeRaw = (int)prisonerScavengePoisonMode;
            Scribe_Values.Look(ref poisonModeRaw, "prisonerScavengePoisonMode", (int)PrisonerScavengePoisonMode.Normal);
            prisonerScavengePoisonMode = System.Enum.IsDefined(typeof(PrisonerScavengePoisonMode), poisonModeRaw)
                ? (PrisonerScavengePoisonMode)poisonModeRaw
                : PrisonerScavengePoisonMode.Normal;
            Scribe_Values.Look(ref enableChaosRoomPregnancy, "enableChaosRoomPregnancy", true);
            Scribe_Values.Look(ref enableExperimentalIdentityInheritance, "enableExperimentalIdentityInheritance", false);
            Scribe_Values.Look(ref chaosPregnancyChancePercent, "chaosPregnancyChancePercent", 2f);
            Scribe_Values.Look(ref chaosPregnancyCheckIntervalTicks, "chaosPregnancyCheckIntervalTicks", 3000);
            Scribe_Values.Look(ref broadcastHopeCooldownDays, "broadcastHopeCooldownDays", 3);
            Scribe_Values.Look(ref narrativeAidGoal, "narrativeAidGoal", 99);
            Scribe_Values.Look(ref narrativeBroadcastGoal, "narrativeBroadcastGoal", 3);
            Scribe_Values.Look(ref narrativeDriveLimit, "narrativeDriveLimit", 3);
            Scribe_Values.Look(ref narrativeAdultGoal, "narrativeAdultGoal", 100);
            Scribe_Values.Look(ref enableFamineYearSystem, "enableFamineYearSystem", false);
            Scribe_Values.Look(ref famineYearChancePercent, "famineYearChancePercent", 40f);
            Scribe_Values.Look(ref famineYearDisasterBonusPercent, "famineYearDisasterBonusPercent", 20f);
            Scribe_Values.Look(ref enableRatEggTraitsBridge, "enableRatEggTraitsBridge", true);
            Scribe_Values.Look(ref ratEggTraitGenerationChance, "ratEggTraitGenerationChance", 0.5f);
            Scribe_Values.Look(ref enablePawnHistories, "enablePawnHistories", true);
            Scribe_Collections.Look(ref enabledPawnHistoryIds, "enabledPawnHistoryIds", LookMode.Value);
            Scribe_Values.Look(ref enablePawnTraits, "enablePawnTraits", true);
            Scribe_Collections.Look(ref enabledPawnTraitIds, "enabledPawnTraitIds", LookMode.Value);
            Scribe_Collections.Look(ref pawnTraitWeights, "pawnTraitWeights", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref disabledIncidentDefNames, "disabledIncidentDefNames", LookMode.Value);
            Scribe_Values.Look(ref maxRatkinAge, "maxRatkinAge", 50);
            Scribe_Values.Look(ref ageDiseaseMultiplier, "ageDiseaseMultiplier", 1f);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && !prisonerScavengeMigrationApplied)
            {
                enablePrisonerScavenge = true;
                prisonerScavengeMigrationApplied = true;
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && !incidentToggleMigrationApplied)
            {
                disabledIncidentDefNames = MouseDisasterIncidentCatalog.BuildMigratedDisabledIncidentDefNames(
                    enableWildRatkinIncidents,
                    enableThiefIncidents,
                    enableBeggarIncidents,
                    disabledIncidentDefNames);
                enableWildRatkinIncidents = true;
                enableThiefIncidents = true;
                enableBeggarIncidents = true;
                incidentToggleMigrationApplied = true;
            }

            NormalizeIncidentToggleState();
            NormalizePawnHistorySettings();
            NormalizePawnTraitSettings();
            ClampValues();
        }

        private void NormalizeIncidentToggleState()
        {
            disabledIncidentDefNames ??= new List<string>();
            disabledIncidentDefNames = disabledIncidentDefNames
                .Where(defName => !string.IsNullOrWhiteSpace(defName))
                .Select(defName => defName.Trim())
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .Where(MouseDisasterIncidentCatalog.IsKnownIncident)
                .OrderBy(defName => defName)
                .ToList();
        }

        private void NormalizeEventFoodSettings()
        {
            disabledEventFoodDefNames = (disabledEventFoodDefNames ?? new List<string>())
                .Where(defName => !string.IsNullOrWhiteSpace(defName))
                .Select(defName => defName.Trim())
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(defName => defName)
                .ToList();
            var normalizedWeights = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
            if (eventFoodWeights != null)
            {
                foreach (KeyValuePair<string, float> entry in eventFoodWeights)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                    float weight = MouseDisasterEventFoodPolicy.NormalizeWeight(entry.Value);
                    if (System.Math.Abs(weight - DefaultEventFoodWeight) < 0.0001f) continue;
                    normalizedWeights[entry.Key.Trim()] = weight;
                }
            }

            eventFoodWeights = normalizedWeights;
        }

        private void NormalizePawnHistorySettings()
        {
            // A missing list means this save predates pawn histories. Keep the
            // feature's default-on behavior, while preserving an explicitly
            // saved empty list from "Disable all histories".
            if (enabledPawnHistoryIds == null)
            {
                enabledPawnHistoryIds = CreateDefaultPawnHistoryIds();
                return;
            }

            enabledPawnHistoryIds = enabledPawnHistoryIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .Where(MouseDisasterPawnHistoryCatalog.IsKnown)
                .OrderBy(id => id)
                .ToList();
        }

        private void NormalizePawnTraitSettings()
        {
            // A missing list means this save predates pawn traits. Keep the
            // feature's default-on behavior, while preserving an explicitly
            // saved empty list from "Disable all traits".
            if (enabledPawnTraitIds == null)
            {
                enabledPawnTraitIds = CreateDefaultPawnTraitIds();
            }
            else
            {
                enabledPawnTraitIds = enabledPawnTraitIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(System.StringComparer.OrdinalIgnoreCase)
                    .Where(MouseDisasterTraitCatalog.IsKnown)
                    .OrderBy(id => id)
                    .ToList();
            }

            var normalized = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
            if (pawnTraitWeights != null)
            {
                foreach (KeyValuePair<string, float> entry in pawnTraitWeights)
                {
                    if (!MouseDisasterTraitCatalog.IsKnown(entry.Key)) continue;
                    normalized[entry.Key] = NormalizePawnTraitWeight(entry.Value,
                        MouseDisasterTraitCatalog.GetDefaultWeight(entry.Key));
                }
            }

            pawnTraitWeights = normalized;
        }

        private static List<string> CreateDefaultPawnTraitIds()
        {
            return MouseDisasterTraitCatalog.All
                .Select(trait => trait.Id)
                .ToList();
        }

        private static float NormalizePawnTraitWeight(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Clamp(Mathf.Round(value), MinPawnTraitWeight, MaxPawnTraitWeight);
        }

        private static List<string> CreateDefaultPawnHistoryIds()
        {
            return MouseDisasterPawnHistoryCatalog.All
                .Select(history => history.Id)
                .ToList();
        }

        public bool IsNarrativeEnabled(string id)
        {
            return enableNarrative && MouseDisasterNarrativePolicy.Enabled(id, disabledNarrativeIds);
        }

        public void SetNarrativeEnabled(string id, bool enabled)
        {
            if (!MouseDisasterNarrativePolicy.SettingIds.Contains(id)) return;
            disabledNarrativeIds ??= new List<string>();
            disabledNarrativeIds.RemoveAll(item => item == id);
            if (!enabled) disabledNarrativeIds.Add(id);
        }
    }
}
