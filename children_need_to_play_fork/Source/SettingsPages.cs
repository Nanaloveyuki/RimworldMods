using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    public partial class TPE_Mod
    {
        private static void DrawToddlerMoods(Listing_Standard list)
        {
            list.CheckboxLabeled("TPE_Settings_RandomMoods_Label".Translate(), ref Settings.enableRandomToddlerMoods,
                "TPE_Settings_RandomMoods_Tooltip".Translate());
            if (Settings.enableRandomToddlerMoods)
            {
                list.Label("TPE_Settings_RandomMoodMax".Translate(Settings.randomMoodMaxActive));
                Settings.randomMoodMaxActive = Mathf.RoundToInt(list.Slider(Settings.randomMoodMaxActive, 1f, 5f));
                list.Label("TPE_Settings_RandomMoodMax_Help".Translate());
                list.Gap(8f);
                list.Label("TPE_Settings_RandomMoodFrequency".Translate(Settings.randomMoodFrequency.ToString("P0")));
                Settings.randomMoodFrequency = list.Slider(Settings.randomMoodFrequency, 0.05f, 0.50f);
                list.Label("TPE_Settings_RandomMoodFrequency_Help".Translate());
            }
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_EventMoods_Label".Translate(), ref Settings.enableEventTriggeredMoods,
                "TPE_Settings_EventMoods_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_KeepPlayMoodForPsychopaths_Label".Translate(),
                ref Settings.keepPlayMoodForPsychopaths, "TPE_Settings_KeepPlayMoodForPsychopaths_Tooltip".Translate());
        }

        private static void DrawToddlerActivities(Listing_Standard list)
        {
            list.CheckboxLabeled("TPE_Settings_SelfSoothing_Label".Translate(), ref Settings.enableSelfSoothing,
                "TPE_Settings_SelfSoothing_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_HeaterSnuggling_Label".Translate(), ref Settings.enableHeaterSnuggling,
                "TPE_Settings_HeaterSnuggling_Tooltip".Translate());
            if (Settings.enableHeaterSnuggling)
            {
                list.Gap(4f);
                list.CheckboxLabeled("TPE_Settings_GeothermalSnuggling_Label".Translate(), ref Settings.allowGeothermalSnuggling,
                    "TPE_Settings_GeothermalSnuggling_Tooltip".Translate());
            }
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_ConfinementPriority_Label".Translate(), ref Settings.enableConfinementPriorityScaling,
                "TPE_Settings_ConfinementPriority_Tooltip".Translate());
        }

        private static void DrawChildMoods(Listing_Standard list)
        {
            list.CheckboxLabeled("TPE_Settings_ChildCrying_Label".Translate(), ref Settings.enableChildCrying,
                "TPE_Settings_ChildCrying_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_OlderChildCrying_Label".Translate(), ref Settings.enableOlderChildCrying,
                "TPE_Settings_OlderChildCrying_Tooltip".Translate());
            list.Gap(12f);
            list.Label("TPE_Settings_ParentIgnoreCryChance".Translate(Settings.parentIgnoreCryChance.ToString("P0")));
            Settings.parentIgnoreCryChance = list.Slider(Settings.parentIgnoreCryChance, 0f, 1f);
            list.Label("TPE_Settings_ParentIgnoreCryChance_Help".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_PrisonerCryAffectsColonists_Label".Translate(), ref Settings.prisonerCryAffectsColonists,
                "TPE_Settings_PrisonerCryAffectsColonists_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_PrisonerCryAffectsOthers_Label".Translate(), ref Settings.prisonerCryAffectsOthers,
                "TPE_Settings_PrisonerCryAffectsOthers_Tooltip".Translate());
        }

        private static void DrawChildPlay(Listing_Standard list)
        {
            list.CheckboxLabeled("TPE_Settings_ChildrenPlayNeed_Label".Translate(), ref Settings.enableChildrenPlayNeed,
                "TPE_Settings_ChildrenPlayNeed_Tooltip".Translate());
            if (!Settings.enableChildrenPlayNeed) return;
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_AgeGatedDesires_Label".Translate(), ref Settings.ageGatedPlayDesires,
                "TPE_Settings_AgeGatedDesires_Tooltip".Translate());
            list.Gap(8f);
            list.Label("TPE_Settings_PlayDecayDays".Translate(Settings.playNeedDecayDays.ToString("F1")));
            Settings.playNeedDecayDays = list.Slider(Settings.playNeedDecayDays, 3f, 14f);
            list.Label("TPE_Settings_PlayDecayDays_Help".Translate());
            list.Gap(8f);
            list.Label("TPE_Settings_PlayGrowthWeight".Translate(Settings.playWeightInGrowth.ToString("F2")));
            Settings.playWeightInGrowth = list.Slider(Settings.playWeightInGrowth, 0f, 1f);
            list.Label("TPE_Settings_PlayGrowthWeight_Help".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_PlayWhenIdle_Label".Translate(), ref Settings.playWhenIdle,
                "TPE_Settings_PlayWhenIdle_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_GuaranteeActivePlay_Label".Translate(), ref Settings.guaranteeActivePlayDesire,
                "TPE_Settings_GuaranteeActivePlay_Tooltip".Translate());
        }

        private static void DrawPlushSleep(Listing_Standard list)
        {
            list.CheckboxLabeled("TPE_Settings_ChildPlushSleep_Label".Translate(), ref Settings.enableChildPlushSleep,
                "TPE_Settings_ChildPlushSleep_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_OlderChildPlushSleep_Label".Translate(), ref Settings.enableOlderChildPlushSleep,
                "TPE_Settings_OlderChildPlushSleep_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_EnableAdultPlushSleep_Label".Translate(), ref Settings.enableAdultPlushSleep,
                "TPE_Settings_EnableAdultPlushSleep_Tooltip".Translate());
            if (Settings.enableAdultPlushSleep)
            {
                list.Gap(12f);
                if (!IrisMenusIntegration.DrawAdultMoodChoice(list))
                {
                    list.Label("TPE_Settings_AdultMoodMode_Label".Translate());
                    DrawAdultMoodMenu(list);
                }
                list.Gap(6f);
                list.Label(("TPE_Settings_AdultMoodMode_" + Settings.adultPlushieMoodMode + "_Desc").Translate());
            }
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_AlwaysSeekPlushRegardlessOfRest_Label".Translate(),
                ref Settings.alwaysSeekPlushRegardlessOfRest, "TPE_Settings_AlwaysSeekPlushRegardlessOfRest_Tooltip".Translate());
        }

        private static void DrawAdultMoodMenu(Listing_Standard list)
        {
            if (Widgets.ButtonText(list.GetRect(32f), AdultMoodModeLabel(Settings.adultPlushieMoodMode)))
            {
                var options = new List<FloatMenuOption>(4);
                for (int i = 0; i < 4; i++)
                {
                    AdultPlushieMoodMode mode = (AdultPlushieMoodMode)i;
                    options.Add(new FloatMenuOption(AdultMoodModeLabel(mode), () => Settings.adultPlushieMoodMode = mode));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static string AdultMoodModeLabel(AdultPlushieMoodMode mode) =>
            ("TPE_Settings_AdultMoodMode_" + mode).Translate();

        private static void DrawPresentation(Listing_Standard list)
        {
            list.CheckboxLabeled("TPE_Settings_MuteBabyCrying_Label".Translate(), ref Settings.muteBabyCryingSound,
                "TPE_Settings_MuteBabyCrying_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_MuteChildCrying_Label".Translate(), ref Settings.muteChildCryingSound,
                "TPE_Settings_MuteChildCrying_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_DisableToddlerPlayAnimations_Label".Translate(),
                ref Settings.disableToddlerPlayAnimations, "TPE_Settings_DisableToddlerPlayAnimations_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_DisableChildPlayAnimations_Label".Translate(),
                ref Settings.disableChildPlayAnimations, "TPE_Settings_DisableChildPlayAnimations_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_RenderPlushWhileSleeping_Label".Translate(),
                ref Settings.renderPlushWhileSleeping, "TPE_Settings_RenderPlushWhileSleeping_Tooltip".Translate());
            list.Gap(12f);
            list.CheckboxLabeled("TPE_Settings_ShowDirtCastles_Label".Translate(), ref Settings.showDirtCastles,
                "TPE_Settings_ShowDirtCastles_Tooltip".Translate());
            list.Gap(12f);
            list.Label("TPE_Settings_BubbleLimit".Translate(Settings.maxBubblesPerMap), -1f,
                (TipSignal)"TPE_Settings_BubbleLimit_Tooltip".Translate());
            Settings.maxBubblesPerMap = Mathf.RoundToInt(list.Slider(Settings.maxBubblesPerMap, 1f, 100f));
            list.Gap(8f);
            list.Label("TPE_Settings_BubbleOpacity".Translate(Settings.bubbleOpacity.ToString("P0")), -1f,
                (TipSignal)"TPE_Settings_BubbleOpacity_Tooltip".Translate());
            Settings.bubbleOpacity = list.Slider(Settings.bubbleOpacity, 0f, 1f);
        }

        private static void DrawDebug(Listing_Standard list)
        {
            list.CheckboxLabeled("TPE_Settings_DebugLogging_Label".Translate(), ref Settings.debugLogging,
                "TPE_Settings_DebugLogging_Tooltip".Translate());
        }
    }
}
