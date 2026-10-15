using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public partial class MouseDisasterMod : Mod
    {
        public static MouseDisasterSettings Settings;
        private Vector2 settingsScrollPosition;
        private float settingsContentHeight = 1920f;

        public MouseDisasterMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MouseDisasterSettings>();
            RegisterIrisMenus();
        }

        public override string SettingsCategory()
        {
            return "MouseDisaster_ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(settingsContentHeight, inRect.height));
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(new Rect(0f, 0f, viewRect.width - 16f, viewRect.height));

            DrawSettings(listing);
            settingsContentHeight = listing.CurHeight + 16f;

            listing.End();
            Widgets.EndScrollView();
            SaveSettings();
        }

        private static void SaveSettings()
        {
            Settings.ClampValues();
            Settings.Write();
        }

        private static void DrawSettings(Listing_Standard listing, SettingsPage? page = null)
        {
            if (page == null || page == SettingsPage.Safety) DrawSafetySettings(listing);
            if (page == null || page == SettingsPage.General) DrawGeneralSettings(listing);
            if (page == null || page == SettingsPage.Environment) DrawEnvironmentSettings(listing);
            if (page == null || page == SettingsPage.PawnBehavior) DrawPawnSettings(listing);
            if (page == null || page == SettingsPage.FoodWeights) DrawEventFoodWeightSettings(listing);
            if (page == null || page == SettingsPage.PawnHistory) DrawPawnHistorySettings(listing);
            if (page == null || page == SettingsPage.PawnTrait) DrawPawnTraitSettings(listing);
            if (page == null || page == SettingsPage.Predation) DrawPredationSettings(listing);
            if (page == null) DrawIncidentSection(listing);
            else if (page == SettingsPage.OriginalEvents || page == SettingsPage.ContinuedEvents)
                DrawIncidentSection(listing, page == SettingsPage.OriginalEvents);
            if (page == null || page == SettingsPage.ContinuedEvents) DrawNarrativeControls(listing, page != null);
            if (page == null || page == SettingsPage.Endings) DrawEndingSettings(listing, page != null);
            if (page == SettingsPage.Developer || (page == null && Prefs.DevMode)) DrawDeveloperSettings(listing);
            if (page == null || page == SettingsPage.Safety) DrawSettingsFooter(listing);
            Settings.ClampValues();
        }

        private static void DrawPawnSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_IrisMenus_PawnBehavior");
            listing.CheckboxLabeled("MouseDisaster_Settings_LeaveAfterFed".Translate(), ref Settings.leaveAfterFed);
            listing.Label("MouseDisaster_Settings_MaxEventPawns".Translate(Settings.maxEventPawns), tooltip: "MouseDisaster_Settings_MaxEventPawns_Tooltip".Translate());
            Settings.maxEventPawns = Mathf.RoundToInt(listing.Slider(Settings.maxEventPawns, 1, 100));
            listing.Label("MouseDisaster_Settings_GeneratedAgeRange".Translate(Settings.minGeneratedAge.ToString("0.0"), Settings.maxGeneratedAge.ToString("0.0")), tooltip: "MouseDisaster_Settings_GeneratedAgeRange_Tooltip".Translate());
            listing.Label("MouseDisaster_Settings_MinGeneratedAge".Translate());
            Settings.minGeneratedAge = Mathf.Round(listing.Slider(Settings.minGeneratedAge, 0f, 100f) * 10f) / 10f;
            listing.Label("MouseDisaster_Settings_MaxGeneratedAge".Translate());
            Settings.maxGeneratedAge = Mathf.Round(listing.Slider(Settings.maxGeneratedAge, Settings.minGeneratedAge, 100f) * 10f) / 10f;
            listing.Label("MouseDisaster_Settings_ReliefFoodScoreBonus".Translate(Settings.reliefFoodScoreBonus.ToString("P0")), tooltip: "MouseDisaster_Settings_ReliefFoodScoreBonus_Tooltip".Translate());
            Settings.reliefFoodScoreBonus = Mathf.Round(listing.Slider(Settings.reliefFoodScoreBonus, 0f, 1f) * 100f) / 100f;
            DrawCheckbox(listing, "MouseDisaster_Settings_AllowEventPawnsEatOutsideReliefArea",
                ref Settings.allowEventPawnsEatOutsideReliefArea,
                "MouseDisaster_Settings_AllowEventPawnsEatOutsideReliefArea_Tooltip");
            listing.Label("MouseDisaster_Settings_FedWanderDays".Translate(Settings.fedWanderDays.ToString("0.00")), tooltip: "MouseDisaster_Settings_FedWanderDays_Tooltip".Translate());
            Settings.fedWanderDays = Mathf.Round(listing.Slider(Settings.fedWanderDays, 0f, 5f) * 100f) / 100f;
            DrawCheckbox(listing, "MouseDisaster_Settings_WaitWhenNoFood", ref Settings.waitWhenNoFood, "MouseDisaster_Settings_WaitWhenNoFood_Tooltip");
            if (Settings.waitWhenNoFood)
            {
                listing.Label("MouseDisaster_Settings_NoFoodWaitDays".Translate(Settings.noFoodWaitDays.ToString("0.00")));
                Settings.noFoodWaitDays = Mathf.Round(listing.Slider(Settings.noFoodWaitDays, 0f, 5f) * 100f) / 100f;
            }
            DrawCheckbox(listing, "MouseDisaster_Settings_AllowMouseDisasterFactionToLeaveWhenIdle", ref Settings.allowMouseDisasterFactionToLeaveWhenIdle,
                "MouseDisaster_Settings_AllowMouseDisasterFactionToLeaveWhenIdle_Tooltip");
            DrawCheckbox(listing, "MouseDisaster_Settings_PreventUnnecessaryNeutralPawnRelations", ref Settings.preventUnnecessaryNeutralPawnRelations,
                "MouseDisaster_Settings_PreventUnnecessaryNeutralPawnRelations_Tooltip");
            DrawCheckbox(listing, "MouseDisaster_Settings_DisableMultiFrameIncidentGeneration", ref Settings.disableMultiFrameIncidentGeneration,
                "MouseDisaster_Settings_DisableMultiFrameIncidentGeneration_Tooltip");
            DrawCheckbox(listing, "MouseDisaster_Settings_AllowColonistAutoGiveFood", ref Settings.allowColonistAutoGiveFood, "MouseDisaster_Settings_AllowColonistAutoGiveFood_Tooltip");
            DrawCheckbox(listing, "MouseDisaster_Settings_AllowColonistChildcareForMouseDisasterEggs", ref Settings.allowColonistChildcareForMouseDisasterEggs, "MouseDisaster_Settings_AllowColonistChildcareForMouseDisasterEggs_Tooltip");
            DrawCheckbox(listing, "MouseDisaster_Settings_AllowNonColonistChildcareForMouseDisasterEggs", ref Settings.allowNonColonistChildcareForMouseDisasterEggs, "MouseDisaster_Settings_AllowNonColonistChildcareForMouseDisasterEggs_Tooltip");
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_VisitorEmployment");
            DrawEmploymentDurationSlider(listing, "MouseDisaster_Settings_TemporaryRecruitDuration",
                ref Settings.temporaryRecruitDurationDays,
                MouseDisasterSettings.MinTemporaryRecruitDurationDays,
                MouseDisasterSettings.MaxTemporaryRecruitDurationDays,
                "MouseDisaster_Settings_TemporaryRecruitDuration_Tooltip");
            DrawEmploymentDurationSlider(listing, "MouseDisaster_Settings_HiredWorkerDuration",
                ref Settings.hiredWorkerDurationDays,
                MouseDisasterSettings.MinHiredWorkerDurationDays,
                MouseDisasterSettings.MaxHiredWorkerDurationDays,
                "MouseDisaster_Settings_HiredWorkerDuration_Tooltip");
            DrawRatkinYoungTradeSettings(listing);
            DrawCheckbox(listing, "MouseDisaster_Settings_EnableGnawing", ref Settings.enableGnawing, "MouseDisaster_Settings_EnableGnawing_Tooltip");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnableExperimentalTailBite", ref Settings.enableExperimentalTailBite, "MouseDisaster_Settings_EnableExperimentalTailBite_Tooltip");
            DrawBiologySettings(listing);
        }

        private static void DrawEventFoodWeightSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_IrisMenus_FoodWeights");
            listing.Label("MouseDisaster_Settings_EventFoodWeights_Hint".Translate());
            listing.Gap(2f);
            if (listing.ButtonText("MouseDisaster_Settings_EventFood_EnableAll".Translate()))
                Settings.SetAllEventFoodsEnabled(true);
            if (listing.ButtonText("MouseDisaster_Settings_EventFood_DisableAll".Translate()))
                Settings.SetAllEventFoodsEnabled(false);
            if (listing.ButtonText("MouseDisaster_Settings_EventFood_ResetWeights".Translate()))
                Settings.ResetEventFoodWeights();
            listing.Gap(2f);

            IReadOnlyList<ThingDef> foods = MouseDisasterEventFoodPolicy.AllFoodDefs();
            if (foods.Count == 0)
            {
                listing.Label("MouseDisaster_Settings_EventFood_NotLoaded".Translate());
                return;
            }

            for (int i = 0; i < foods.Count; i++)
            {
                ThingDef food = foods[i];
                bool enabled = Settings.IsEventFoodEnabled(food.defName);
                bool changed = enabled;
                string label = food.LabelCap.ToString();
                if (string.IsNullOrWhiteSpace(label)) label = food.defName;
                listing.CheckboxLabeled(label, ref changed, "MouseDisaster_Settings_EventFood_Item_Tooltip".Translate(food.defName));
                if (changed != enabled)
                    Settings.SetEventFoodEnabled(food.defName, changed);

                if (changed)
                {
                    float weight = Settings.GetEventFoodWeight(food.defName);
                    listing.Label("MouseDisaster_Settings_EventFood_Weight".Translate(weight.ToString("P0")),
                        -1f, new TipSignal("MouseDisaster_Settings_EventFood_Weight_Tooltip".Translate()));
                    float updatedWeight = Mathf.Round(listing.Slider(weight,
                        MouseDisasterSettings.MinEventFoodWeight,
                        MouseDisasterSettings.MaxEventFoodWeight) * 100f) / 100f;
                    if (Mathf.Abs(updatedWeight - weight) > 0.001f)
                        Settings.SetEventFoodWeight(food.defName, updatedWeight);
                }

                listing.Gap(1f);
            }
        }

        private static void DrawPawnHistorySettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_PawnHistory");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnablePawnHistories",
                ref Settings.enablePawnHistories, "MouseDisaster_Settings_EnablePawnHistories_Tooltip");

            if (listing.ButtonText("MouseDisaster_Settings_PawnHistory_EnableAll".Translate()))
                Settings.SetAllPawnHistoriesEnabled(true);
            if (listing.ButtonText("MouseDisaster_Settings_PawnHistory_DisableAll".Translate()))
                Settings.SetAllPawnHistoriesEnabled(false);

            listing.Label("MouseDisaster_Settings_PawnHistory_SelectionHint".Translate());
            listing.Gap(2f);
            foreach (MouseDisasterPawnHistoryDefinition history in MouseDisasterPawnHistoryCatalog.All)
            {
                bool selected = Settings.IsPawnHistorySelected(history.Id);
                bool changed = selected;
                listing.CheckboxLabeled(history.DisplayLabel, ref changed, history.Tooltip);
                if (changed != selected)
                    Settings.SetPawnHistoryEnabled(history.Id, changed);
                listing.Gap(1f);
            }
        }

        private static void DrawPawnTraitSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_PawnTrait");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnablePawnTraits",
                ref Settings.enablePawnTraits, "MouseDisaster_Settings_EnablePawnTraits_Tooltip");

            if (listing.ButtonText("MouseDisaster_Settings_PawnTrait_EnableAll".Translate()))
                Settings.SetAllPawnTraitsEnabled(true);
            if (listing.ButtonText("MouseDisaster_Settings_PawnTrait_DisableAll".Translate()))
                Settings.SetAllPawnTraitsEnabled(false);

            listing.Label("MouseDisaster_Settings_PawnTrait_SelectionHint".Translate());
            listing.Gap(2f);
            foreach (MouseDisasterTraitDefinition trait in MouseDisasterTraitCatalog.All)
            {
                bool selected = Settings.IsPawnTraitSelected(trait.Id);
                bool changed = selected;
                string label = trait.DisplayLabel;
                Color color = trait.LabelColor;
                if (color.a > 0.01f && color != Color.white)
                    label = label.Colorize(color);
                listing.CheckboxLabeled(label, ref changed, trait.Tooltip);
                if (changed != selected)
                    Settings.SetPawnTraitEnabled(trait.Id, changed);

                if (changed)
                {
                    float weight = Settings.GetPawnTraitWeight(trait.Id);
                    listing.Label("MouseDisaster_Settings_PawnTrait_Weight".Translate(weight.ToString("0")),
                        -1f, new TipSignal("MouseDisaster_Settings_PawnTrait_Weight_Tooltip".Translate()));
                    float updatedWeight = Mathf.Round(listing.Slider(weight,
                        MouseDisasterSettings.MinPawnTraitWeight,
                        MouseDisasterSettings.MaxPawnTraitWeight));
                    if (Mathf.Abs(updatedWeight - weight) > 0.001f)
                        Settings.SetPawnTraitWeight(trait.Id, updatedWeight);
                }

                listing.Gap(1f);
            }
        }

        private static void DrawRatkinYoungTradeSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_Trade");
            MouseDisasterTradePawnJoinMode current = Settings.GetRatkinYoungTradeJoinMode();
            string currentLabel = ("MouseDisaster_Settings_RatkinYoungTradeJoinMode_" + current).Translate().ToString();
            if (listing.ButtonText("MouseDisaster_Settings_RatkinYoungTradeJoinMode".Translate(currentLabel).ToString()))
            {
                var options = new List<FloatMenuOption>();
                foreach (MouseDisasterTradePawnJoinMode mode in System.Enum.GetValues(typeof(MouseDisasterTradePawnJoinMode)))
                {
                    MouseDisasterTradePawnJoinMode selected = mode;
                    options.Add(new FloatMenuOption(
                        ("MouseDisaster_Settings_RatkinYoungTradeJoinMode_" + mode).Translate(),
                        () => Settings.ratkinYoungTradeJoinMode = selected));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Gap(2f);
        }

        private static void DrawPredationSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Predation_Settings");
            DrawPercentSlider(listing, "MouseDisaster_Predation_Chance", ref Settings.refugeePredationChancePercent, 0f, 100f);
            DrawCheckbox(listing, "MouseDisaster_Predation_FightBack", ref Settings.refugeePredationFightBack, "MouseDisaster_Predation_FightBack_Tip");
            DrawCheckbox(listing, "MouseDisaster_Predation_FollowDifficulty", ref Settings.outsidePredatorsFollowDifficulty, "MouseDisaster_Predation_FollowDifficulty_Tip");
            DrawSecondsSlider(listing, "MouseDisaster_Predation_SearchInterval", ref Settings.wildPredatorSearchIntervalTicks,
                MouseDisasterSettings.MinWildPredatorSearchIntervalTicks, MouseDisasterSettings.MaxWildPredatorSearchIntervalTicks,
                "MouseDisaster_Predation_SearchInterval_Tip");
            DrawCheckbox(listing, "MouseDisaster_Predation_AvoidWhenFed", ref Settings.wildPredatorsAvoidRatkinWhenFed, "MouseDisaster_Predation_AvoidWhenFed_Tip");
            DrawCheckbox(listing, "MouseDisaster_Predation_LeaveAfterFed", ref Settings.wildPredatorsLeaveAfterFed, "MouseDisaster_Predation_LeaveAfterFed_Tip");
            DrawCheckbox(listing, "MouseDisaster_Predation_HuntHomeArea", ref Settings.wildPredatorsHuntHomeAreaRatkin, "MouseDisaster_Predation_HuntHomeArea_Tip");
        }

        private static void DrawDeveloperSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_IrisMenus_Developer");
            if (!Prefs.DevMode) { listing.Label("MouseDisaster_Developer_Disabled".Translate()); return; }
            DrawCheckbox(listing, "MouseDisaster_Developer_Logging", ref Settings.enablePrisonerScavengeDebugLog, "MouseDisaster_Developer_LoggingTip");
            DrawCheckbox(listing, "MouseDisaster_Developer_DetailedTraceLogging", ref Settings.enableDetailedTraceLog, "MouseDisaster_Developer_DetailedTraceLoggingTip");
            DrawCheckbox(listing, "MouseDisaster_Developer_ForceDismissMobileNonInfants", ref Settings.forceDismissMobileNonInfantEventPawns, "MouseDisaster_Developer_ForceDismissMobileNonInfantsTip");
            if (Current.ProgramState != ProgramState.Playing || Current.Game == null)
            { listing.Label("MouseDisaster_Developer_NoGame".Translate()); return; }
            foreach (bool original in new[] { true, false })
            {
                DrawSectionTitle(listing, original ? "MouseDisaster_Story_DebugOriginal" : "MouseDisaster_Story_DebugContinued");
                foreach (var entry in MouseDisasterIncidentCatalog.AllEntries)
                    if (entry.IsOriginal == original && listing.ButtonText(entry.DisplayLabel))
                        GameComponent_MouseDisasterNarrative.RunNarrativeDebug(entry.DefName);
            }
            DrawSectionTitle(listing, "MouseDisaster_Story_DebugNarratives");
            foreach (string id in GameComponent_MouseDisasterNarrative.NarrativeDebugIds)
                if (listing.ButtonText(("MouseDisaster_Story_Debug" + id).Translate()))
                    GameComponent_MouseDisasterNarrative.RunNarrativeDebug(id);
        }

        private static void DrawSafetySettings(Listing_Standard listing)
        {
            DrawHeader(listing);
            var removal = Current.Game?.GetComponent<GameComponent_MouseDisasterRemoval>();
            if (Current.ProgramState == ProgramState.Playing && removal != null)
            {
                listing.Label("MouseDisaster_RemovalSection".Translate());
                if (removal.NewContentDisabled) listing.Label("MouseDisaster_RemovalDisabled".Translate());
                else if (listing.ButtonText("MouseDisaster_RemovalDisable".Translate())) removal.DisableNewContent();
                if (listing.ButtonText("MouseDisaster_RemovalExport".Translate()))
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("MouseDisaster_RemovalConfirm".Translate(),
                        () => LongEventHandler.QueueLongEvent(removal.ExportCleanSave, "SavingLongEvent", false, null)));
                listing.GapLine();
            }

            DrawCheckbox(listing, "MouseDisaster_Settings_EnableNewContent", ref Settings.enableNewContent, "MouseDisaster_Settings_EnableNewContent_Tooltip");
            if (!Settings.enableNewContent)
            {
                listing.Label("MouseDisaster_Settings_NewContentDisabledHint".Translate());
            }
            listing.GapLine();
        }

        private static void DrawGeneralSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_General");
            DrawDecimalSlider(listing, "MouseDisaster_Settings_PositiveDays", ref Settings.positiveIncidentDays, 0, 60, "0.0");
            DrawDecimalSlider(listing, "MouseDisaster_Settings_NegativeDays", ref Settings.negativeIncidentDays, 0, 60, "0.0");
            DrawDaysSlider(listing, "MouseDisaster_Settings_BroadcastHopeCooldown", ref Settings.broadcastHopeCooldownDays, 0, 10);
            DrawFamineSettings(listing);
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_DisplayDebug");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnableFloatingText", ref Settings.enableFloatingText, "MouseDisaster_Settings_EnableFloatingText_Tooltip");
        }

        private static void DrawEnvironmentSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_Environment");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnableTemperatureProtectionApparel",
                ref Settings.enableTemperatureProtectionApparel, "MouseDisaster_Settings_EnableTemperatureProtectionApparel_Tooltip");
            DrawDecimalSlider(listing, "MouseDisaster_Settings_TemperatureMinimum",
                ref Settings.mouseDisasterMinimumEnvironmentTemperature,
                MouseDisasterSettings.MinMouseDisasterEnvironmentTemperature,
                MouseDisasterSettings.MaxMouseDisasterEnvironmentTemperature, "0.0");
            DrawDecimalSlider(listing, "MouseDisaster_Settings_TemperatureMaximum",
                ref Settings.mouseDisasterMaximumEnvironmentTemperature,
                MouseDisasterSettings.MinMouseDisasterEnvironmentTemperature,
                MouseDisasterSettings.MaxMouseDisasterEnvironmentTemperature, "0.0");
            listing.Label("MouseDisaster_Settings_TemperatureRangeHint".Translate());

            if (!Settings.enableTemperatureProtectionApparel)
            {
                listing.Label("MouseDisaster_Settings_TemperatureProtectionDisabledHint".Translate());
                return;
            }

            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_TemperatureApparel");
            foreach (MouseDisasterUtility.TemperatureApparelOption option in MouseDisasterUtility.AllTemperatureApparelOptions)
            {
                string label = ResolveTemperatureApparelLabel(option);
                bool enabled = Settings.IsTemperatureApparelEnabled(option.DefName);
                listing.CheckboxLabeled(
                    "MouseDisaster_Settings_TemperatureApparelEnabled".Translate(label).ToString(),
                    ref enabled,
                    "MouseDisaster_Settings_TemperatureApparelEnabled_Tooltip".Translate(label).ToString());
                if (enabled != Settings.IsTemperatureApparelEnabled(option.DefName))
                {
                    Settings.SetTemperatureApparelEnabled(option.DefName, enabled);
                }

                float insulation = Settings.GetTemperatureApparelInsulation(option.DefName, option.Insulation);
                string direction = (option.IsCold
                    ? "MouseDisaster_Settings_TemperatureCold"
                    : "MouseDisaster_Settings_TemperatureHeat").Translate().ToString();
                listing.Label("MouseDisaster_Settings_TemperatureApparelInsulation".Translate(
                    label, insulation.ToString("0.0"), direction).ToString());
                insulation = listing.Slider(insulation, MouseDisasterSettings.MinTemperatureApparelInsulation,
                    MouseDisasterSettings.MaxTemperatureApparelInsulation);
                Settings.SetTemperatureApparelInsulation(option.DefName, insulation);
                listing.Gap(2f);
            }
        }

        private static string ResolveTemperatureApparelLabel(MouseDisasterUtility.TemperatureApparelOption option)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(option.DefName);
            return def?.LabelCap ?? option.DefName;
        }

        private static void DrawEndingSettings(Listing_Standard listing, bool hosted)
        {
            listing.CheckboxLabeled("MouseDisaster_Settings_CountWithoutSuin".Translate(), ref Settings.countWithoutSuin);
            listing.CheckboxLabeled("MouseDisaster_Settings_EndingsWithoutSuin".Translate(), ref Settings.endingsWithoutSuin);
            if (hosted) DrawDaysSlider(listing, "MouseDisaster_Story_EndingDelay", ref Settings.narrativeEndingDelayDays, 0, 120);
            DrawSectionTitle(listing, "MouseDisaster_Story_Settings");
            listing.Label("MouseDisaster_Story_AidGoal".Translate(Settings.narrativeAidGoal));
            Settings.narrativeAidGoal = Mathf.RoundToInt(listing.Slider(Settings.narrativeAidGoal, 1, 999));
            listing.Label("MouseDisaster_Story_BroadcastGoal".Translate(Settings.narrativeBroadcastGoal));
            Settings.narrativeBroadcastGoal = Mathf.RoundToInt(listing.Slider(Settings.narrativeBroadcastGoal, 1, 99));
            listing.Label("MouseDisaster_Story_DriveLimit".Translate(Settings.narrativeDriveLimit));
            Settings.narrativeDriveLimit = Mathf.RoundToInt(listing.Slider(Settings.narrativeDriveLimit, 0, 99));
            listing.Label("MouseDisaster_Story_AdultGoal".Translate(Settings.narrativeAdultGoal));
            Settings.narrativeAdultGoal = Mathf.RoundToInt(listing.Slider(Settings.narrativeAdultGoal, 1, 500));
        }

        private static void DrawFamineSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_FamineYear");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnableFamineYearSystem", ref Settings.enableFamineYearSystem, "MouseDisaster_Settings_EnableFamineYearSystem_Tooltip");
            if (Settings.enableFamineYearSystem)
            {
                DrawPercentSlider(listing, "MouseDisaster_Settings_FamineYearChance", ref Settings.famineYearChancePercent, MouseDisasterSettings.MinFamineYearChancePercent, MouseDisasterSettings.MaxFamineYearChancePercent);
                DrawPercentSlider(listing, "MouseDisaster_Settings_FamineYearDisasterBonus", ref Settings.famineYearDisasterBonusPercent, MouseDisasterSettings.MinFamineYearDisasterBonusPercent, MouseDisasterSettings.MaxFamineYearDisasterBonusPercent);
            }
            else
            {
                listing.Label("MouseDisaster_Settings_FamineYearDisabledHint".Translate());
            }

        }

        private static void DrawBiologySettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_Age");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnableAgeCap", ref Settings.enableAgeCapAdjustment, "MouseDisaster_Settings_EnableAgeCap_Tooltip");
            if (Settings.enableAgeCapAdjustment)
            {
                listing.Label("MouseDisaster_Settings_MaxAge".Translate(Settings.maxRatkinAge));
                Settings.maxRatkinAge = Mathf.RoundToInt(listing.Slider(Settings.maxRatkinAge, MouseDisasterSettings.MinRatkinAge, MouseDisasterSettings.MaxRatkinAge));

                DrawDecimalSlider(listing, "MouseDisaster_Settings_DiseaseMult", ref Settings.ageDiseaseMultiplier, MouseDisasterSettings.MinAgeDiseaseMultiplier, MouseDisasterSettings.MaxAgeDiseaseMultiplier, "0.0");
            }
            else
            {
                listing.Label("MouseDisaster_Settings_AgeDisabledHint".Translate());
            }

            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_Genes");
            DrawCheckbox(listing, "MouseDisaster_Settings_EnableExperimentalIdentityInheritance", ref Settings.enableExperimentalIdentityInheritance, "MouseDisaster_Settings_EnableExperimentalIdentityInheritance_Tooltip");
            DrawCheckbox(listing, "MouseDisaster_Settings_ChaosPregnancyEnabled", ref Settings.enableChaosRoomPregnancy, "MouseDisaster_Settings_ChaosPregnancyEnabled_Tooltip");
            if (Settings.enableChaosRoomPregnancy)
            {
                DrawDecimalSlider(listing, "MouseDisaster_Settings_ChaosChance", ref Settings.chaosPregnancyChancePercent, MouseDisasterSettings.MinChaosPregnancyChancePercent, MouseDisasterSettings.MaxChaosPregnancyChancePercent, "0.0");
                DrawSecondsSlider(listing, "MouseDisaster_Settings_ChaosInterval", ref Settings.chaosPregnancyCheckIntervalTicks, MouseDisasterSettings.MinChaosPregnancyIntervalTicks, MouseDisasterSettings.MaxChaosPregnancyIntervalTicks);
            }
            else
            {
                listing.Label("MouseDisaster_Settings_ChaosDisabledHint".Translate());
            }

            DrawAdaptiveRatkinXenotypeSettings(listing);

            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_Compat");
            DrawCheckbox(listing, "MouseDisaster_Settings_RatEggTraitBridge", ref Settings.enableRatEggTraitsBridge, "MouseDisaster_Settings_RatEggTraitBridge_Tooltip");
            if (Settings.enableRatEggTraitsBridge)
            {
                DrawFactorAsPercentSlider(listing, "MouseDisaster_Settings_RatEggTraitChance", ref Settings.ratEggTraitGenerationChance, MouseDisasterSettings.MinRatEggTraitGenerationChance, MouseDisasterSettings.MaxRatEggTraitGenerationChance);
            }
            else
            {
                listing.Label("MouseDisaster_Settings_CompatDisabledHint".Translate());
            }

        }

        private static void DrawAdaptiveRatkinXenotypeSettings(Listing_Standard listing)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_AdaptiveRatkinXenotypes");
            listing.Label("MouseDisaster_Settings_AdaptiveRatkinXenotypes_Hint".Translate());

            if (!ModsConfig.BiotechActive)
            {
                listing.Label("MouseDisaster_Settings_AdaptiveRatkinXenotypes_Disabled".Translate());
                return;
            }

            List<RimWorld.XenotypeDef> candidates = MouseDisasterAdaptiveXenotypeUtility.GetCandidates();
            if (candidates.Count == 0)
            {
                listing.Label("MouseDisaster_Settings_AdaptiveRatkinXenotypes_None".Translate());
                return;
            }

            if (listing.ButtonText("MouseDisaster_Settings_AdaptiveRatkinXenotypes_Reset".Translate()))
            {
                Settings.ResetRatkinXenotypeSpawnWeights();
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                RimWorld.XenotypeDef xenotype = candidates[i];
                float weight = MouseDisasterAdaptiveXenotypeUtility.GetSpawnWeight(xenotype);
                string displayName = xenotype.LabelCap.ToString();
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = xenotype.defName;
                }
                string label = "MouseDisaster_Settings_AdaptiveRatkinXenotypeWeight".Translate(
                    displayName,
                    weight.ToString("0"));
                string tooltip = "MouseDisaster_Settings_AdaptiveRatkinXenotypeWeight_Tooltip".Translate(
                    xenotype.defName,
                    MouseDisasterAdaptiveXenotypeUtility.GetDefaultSpawnWeight(xenotype).ToString("0"));
                listing.Label(label, -1f, new TipSignal(tooltip));
                float updatedWeight = listing.Slider(weight,
                    MouseDisasterSettings.MinRatkinXenotypeSpawnWeight,
                    MouseDisasterSettings.MaxRatkinXenotypeSpawnWeight);
                if (Mathf.Abs(updatedWeight - weight) > 0.001f)
                {
                    Settings.SetRatkinXenotypeSpawnWeight(xenotype.defName, updatedWeight);
                }
            }
        }

        private static void DrawSettingsFooter(Listing_Standard listing)
        {
            listing.Gap(10f);
            if (listing.ButtonText("MouseDisaster_Settings_ResetDefaults".Translate()))
            {
                Settings.ResetToDefaults();
            }

        }

        private static void DrawHeader(Listing_Standard listing)
        {
            int enabledIncidentCount = MouseDisasterRuntime.AllowsNewContent
                ? MouseDisasterIncidentCatalog.CountEnabledIncidents(Settings.disabledIncidentDefNames)
                : 0;
            int totalIncidentCount = MouseDisasterIncidentCatalog.AllEntries.Count;
            string newContentStatus = MouseDisasterRuntime.AllowsNewContent ? "MouseDisaster_Settings_Status_On".TranslateSimple() : "MouseDisaster_Settings_Status_Off".TranslateSimple();
            string gnawingStatus = Settings.enableGnawing ? "MouseDisaster_Settings_Status_On".TranslateSimple() : "MouseDisaster_Settings_Status_Off".TranslateSimple();
            string tailBiteStatus = Settings.enableExperimentalTailBite ? "MouseDisaster_Settings_Status_On".TranslateSimple() : "MouseDisaster_Settings_Status_Off".TranslateSimple();
            listing.Label("MouseDisaster_UI_SettingsSummary".Translate(newContentStatus, enabledIncidentCount, totalIncidentCount, gnawingStatus, tailBiteStatus).Resolve());
            listing.GapLine();
        }

        private static void DrawIncidentSection(Listing_Standard listing, bool? original = null)
        {
            DrawSectionTitle(listing, "MouseDisaster_Settings_Section_Incidents");
            DrawIncidentCategorySection(listing, MouseDisasterIncidentCategory.MouseDisaster, "MouseDisaster_Settings_IncidentCategory_MouseDisaster", original);
            DrawIncidentCategorySection(listing, MouseDisasterIncidentCategory.Plague, "MouseDisaster_Settings_IncidentCategory_Plague", original);
        }

        private static void DrawIncidentCategorySection(Listing_Standard listing, MouseDisasterIncidentCategory category, string titleKey, bool? original)
        {
            IReadOnlyList<MouseDisasterIncidentEntry> entries = MouseDisasterIncidentCatalog.GetEntries(category);
            if (original.HasValue)
            {
                bool hasEntries = false;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].IsOriginal == original.Value) { hasEntries = true; break; }
                if (!hasEntries) return;
            }
            listing.Label(titleKey.Translate());
            listing.Gap(2f);
            for (int i = 0; i < entries.Count; i++)
            {
                MouseDisasterIncidentEntry entry = entries[i];
                if (original.HasValue && entry.IsOriginal != original.Value) continue;
                bool enabled = Settings.IsIncidentEnabled(entry.DefName);
                bool newEnabled = enabled;
                listing.CheckboxLabeled(entry.DisplayLabel, ref newEnabled);
                if (newEnabled != enabled)
                {
                    Settings.SetIncidentEnabled(entry.DefName, newEnabled);
                }
                DrawAttitudeControl(listing, Settings.GetEventAttitude(entry.DefName),
                    selected => Settings.eventAttitudes[entry.DefName] = selected);
                var def = DefDatabase<RimWorld.IncidentDef>.GetNamedSilentFail(entry.DefName);
                if (def != null)
                {
                    bool positive = Settings.IsPositiveIncident(def);
                    if (listing.ButtonText((positive ? "MouseDisaster_Settings_Positive" : "MouseDisaster_Settings_Negative").Translate()))
                        Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption> {
                            new FloatMenuOption("MouseDisaster_Settings_Positive".Translate(), () => Settings.positiveIncidents[entry.DefName] = true),
                            new FloatMenuOption("MouseDisaster_Settings_Negative".Translate(), () => Settings.positiveIncidents[entry.DefName] = false)
                        }));
                    bool replace = Settings.ReplacesRaid(entry.DefName);
                    listing.CheckboxLabeled("MouseDisaster_Settings_ReplaceRaid".Translate(), ref replace);
                    Settings.raidReplacementIncidents[entry.DefName] = replace;
                }
                listing.Gap(2f);
            }

            listing.GapLine();
        }

        private static void DrawPercentSlider(Listing_Standard listing, string labelKey, ref float value, float min, float max)
        {
            listing.Label(labelKey.Translate(value.ToString("0")));
            value = listing.Slider(value, min, max);
        }

        private static void DrawAttitudeControl(Listing_Standard listing, MouseDisasterEventAttitude? current,
            System.Action<MouseDisasterEventAttitude> set, string extraTip = null)
        {
            string label = current.HasValue ? ("MouseDisaster_Attitude_" + current.Value).Translate().ToString()
                : "MouseDisaster_Story_AttitudeMixed".Translate().ToString();
            string tip = current.HasValue ? ("MouseDisaster_AttitudeTip_" + current.Value).Translate().ToString() : label;
            Rect rect = listing.GetRect(0f);
            rect.height = 30f;
            TooltipHandler.TipRegion(rect, extraTip == null ? tip : tip + "\n\n" + extraTip);
            if (!listing.ButtonText(label)) return;
            var options = new List<FloatMenuOption>();
            foreach (MouseDisasterEventAttitude attitude in System.Enum.GetValues(typeof(MouseDisasterEventAttitude)))
            {
                MouseDisasterEventAttitude selected = attitude;
                options.Add(new FloatMenuOption(("MouseDisaster_Attitude_" + attitude).Translate(), () => set(selected)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void DrawNarrativeControls(Listing_Standard listing, bool hosted = false)
        {
            DrawSectionTitle(listing, "MouseDisaster_Story_ControlSection");
            DrawCheckbox(listing, "MouseDisaster_Story_EnableNarrative", ref Settings.enableNarrative, "MouseDisaster_Story_EnableNarrativeTip");
            foreach (string id in MouseDisasterNarrativePolicy.SettingIds)
            {
                bool enabled = MouseDisasterNarrativePolicy.Enabled(id, Settings.disabledNarrativeIds);
                bool changed = enabled;
                listing.CheckboxLabeled(("MouseDisaster_Story_Toggle" + id).Translate(), ref changed,
                    "MouseDisaster_Story_ToggleTip".Translate());
                if (changed != enabled) Settings.SetNarrativeEnabled(id, changed);
                var sources = MouseDisasterNarrativePolicy.GetAttitudeSources(id);
                if (sources.Count > 0)
                {
                    var names = new List<string>();
                    foreach (string source in sources)
                        foreach (var entry in MouseDisasterIncidentCatalog.AllEntries)
                            if (entry.DefName == source) names.Add(entry.DisplayLabel);
                    string tip = names.Count == 0 ? "MouseDisaster_Story_AttitudeIndependentTip".Translate().ToString()
                        : "MouseDisaster_Story_AttitudeSharedTip".Translate(string.Join("\n", names)).ToString();
                    DrawAttitudeControl(listing, Settings.GetNarrativeAttitude(id),
                        selected => Settings.SetNarrativeAttitude(id, selected), tip);
                }
                listing.Gap(2f);
            }
            DrawSectionTitle(listing, "MouseDisaster_Story_TriggerSection");
            DrawNarrativeInteger(listing, "MouseDisaster_Story_ProgressGoal", ref Settings.narrativeProgressGoal, 1, 50);
            DrawNarrativeInteger(listing, "MouseDisaster_Story_RewardGoal", ref Settings.narrativeRewardGoal, 1, 50);
            DrawNarrativeInteger(listing, "MouseDisaster_Story_TheftGoal", ref Settings.narrativeTheftGoal, 1, 20);
            DrawNarrativeInteger(listing, "MouseDisaster_Story_EnvoyGoal", ref Settings.narrativeEnvoyGoal, 1, 50);
            DrawNarrativeInteger(listing, "MouseDisaster_Story_RelicGoal", ref Settings.narrativeRelicGoal, 1, 50);
            if (!hosted) DrawDaysSlider(listing, "MouseDisaster_Story_EndingDelay", ref Settings.narrativeEndingDelayDays, 0, 120);
            DrawSectionTitle(listing, "MouseDisaster_Story_FrequencySection");
            DrawPercentSlider(listing, "MouseDisaster_Story_N004ReturnChance", ref Settings.narrativeN004ReturnChancePercent, 0, 100);
            DrawPercentSlider(listing, "MouseDisaster_Story_ReturnChance", ref Settings.narrativeReturnChancePercent, 0, 100);
            DrawDaysSlider(listing, "MouseDisaster_Story_ReturnDelay", ref Settings.narrativeReturnDelayDays, 1, 120);
            DrawPercentSlider(listing, "MouseDisaster_Story_EchoChance", ref Settings.narrativeEchoChancePercent, 0, 100);
            DrawDaysSlider(listing, "MouseDisaster_Story_EchoCooldown", ref Settings.narrativeEchoCooldownDays, 1, 60);
            if (Current.Game != null && listing.ButtonText("MouseDisaster_Story_AlertLabel".Translate()))
                Current.Game.GetComponent<GameComponent_MouseDisasterNarrative>()?.OpenNarrativeJournal();
        }

        private static void DrawNarrativeInteger(Listing_Standard listing, string key, ref int value, int min, int max)
        {
            listing.Label(key.Translate(value));
            value = Mathf.RoundToInt(listing.Slider(value, min, max));
        }

        private static void DrawDecimalSlider(Listing_Standard listing, string labelKey, ref float value, float min, float max, string format)
        {
            listing.Label(labelKey.Translate(value.ToString(format)));
            value = listing.Slider(value, min, max);
        }

        private static void DrawSecondsSlider(Listing_Standard listing, string labelKey, ref int ticksValue, int min, int max)
        {
            DrawSecondsSlider(listing, labelKey, ref ticksValue, min, max, null);
        }

        private static void DrawSecondsSlider(Listing_Standard listing, string labelKey, ref int ticksValue, int min, int max, string tooltipKey)
        {
            float seconds = ticksValue / 60f;
            TipSignal? tip = tooltipKey == null ? (TipSignal?)null : new TipSignal(tooltipKey.Translate());
            listing.Label(labelKey.Translate(seconds.ToString("0.0")), -1f, tip);
            ticksValue = Mathf.RoundToInt(listing.Slider(ticksValue, min, max));
        }

        private static void DrawDaysSlider(Listing_Standard listing, string labelKey, ref int daysValue, int min, int max)
        {
            listing.Label(labelKey.Translate(daysValue.ToString()));
            daysValue = Mathf.RoundToInt(listing.Slider(daysValue, min, max));
        }

        private static void DrawEmploymentDurationSlider(Listing_Standard listing, string labelKey, ref int daysValue,
            int min, int max, string tooltipKey)
        {
            string duration = MouseDisasterVisitorUtility.FormatDurationLabel(GenDate.DaysToTicks(daysValue));
            listing.Label(labelKey.Translate(duration), -1f, new TipSignal(tooltipKey.Translate()));
            daysValue = Mathf.RoundToInt(listing.Slider(daysValue, min, max));
        }

        private static void DrawFactorAsPercentSlider(Listing_Standard listing, string labelKey, ref float value, float min, float max)
        {
            listing.Label(labelKey.Translate((value * 100f).ToString("0")));
            value = listing.Slider(value, min, max);
        }

        private static void DrawSectionTitle(Listing_Standard listing, string key)
        {
            listing.Gap(4f);
            listing.Label(key.Translate());
            listing.GapLine();
        }

        private static void DrawCheckbox(Listing_Standard listing, string labelKey, ref bool value, string tooltipKey)
        {
            listing.CheckboxLabeled(labelKey.Translate(), ref value, tooltipKey.Translate());
            listing.Gap(2f);
        }
    }
}
