extern alias iris;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using iris::IrisMenus;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal sealed partial class RHAH_IrisMenusPages
    {
        static readonly string[] backgroundEvents =
        {
            "Rebellion", "Ruins", "Unrest", "Mutiny", "TwoTaxes", "SameCallsign", "HarvestFestival",
            "Memorial", "AidManifest", "CancelledBanquet"
        };

        static readonly string[] backgroundSettings =
        {
            "Enabled", "AverageDays", "CivilWar", "CivilWarChance", "CivilWarDelay", "Takeover", "TakeoverChance",
            "TakeoverDelay", "UnrestBonus", "UnrestDays", "RuinsDistance", "Hardtack", "Silver", "Components",
            "TradeDiscount", "WoundedJoinChance", "StoryDistance", "EvacuationGoodwill", "MemorialSteel", "MemorialWood",
            "RestorationSteel", "RestorationWood", "MaterialsGoodwill", "NamesUndergroundGoodwill", "NamesWarlordGoodwill",
            "RestorationGoodwill"
        };

        void DrawBackgroundStories(Listing_Standard list)
        {
            Section(list, "RHAH_Background_SettingsTitle");
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            if (options == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }

            Note(list, "RHAH_Background_SettingsDescription");
            BackgroundToggle(list, "Enabled", ref options.enabled);
            BackgroundNumber(list, "AverageDays", ref options.averageDays, 0f, 360f);
            Note(list, "RHAH_Background_SettingsFrequency");
            BackgroundEvent(list, "Rebellion", ref options.rebellionEnabled, ref options.rebellionWeight);
            BackgroundEvent(list, "Ruins", ref options.ruinsEnabled, ref options.ruinsWeight);
            BackgroundEvent(list, "Unrest", ref options.unrestEnabled, ref options.unrestWeight);
            BackgroundEvent(list, "Mutiny", ref options.mutinyEnabled, ref options.mutinyWeight);
            BackgroundEvent(list, "TwoTaxes", ref options.twoTaxesEnabled, ref options.twoTaxesWeight);
            BackgroundEvent(list, "SameCallsign", ref options.sameCallsignEnabled, ref options.sameCallsignWeight);
            BackgroundEvent(list, "HarvestFestival", ref options.harvestFestivalEnabled, ref options.harvestFestivalWeight);
            BackgroundEvent(list, "Memorial", ref options.memorialEnabled, ref options.memorialWeight);
            BackgroundEvent(list, "AidManifest", ref options.aidManifestEnabled, ref options.aidManifestWeight);
            BackgroundEvent(list, "CancelledBanquet", ref options.cancelledBanquetEnabled, ref options.cancelledBanquetWeight);
            list.Gap(12f);
            BackgroundToggle(list, "CivilWar", ref options.civilWarEnabled);
            BackgroundNumber(list, "CivilWarChance", ref options.civilWarChancePercent, 0f, 100f);
            BackgroundNumber(list, "CivilWarDelay", ref options.civilWarDelayDays, 0f, 360f);
            BackgroundToggle(list, "Takeover", ref options.takeoverEnabled);
            Note(list, "RHAH_Background_SettingsTakeoverRule");
            BackgroundNumber(list, "TakeoverChance", ref options.takeoverChancePercent, 0f, 100f);
            BackgroundNumber(list, "TakeoverDelay", ref options.takeoverDelayDays, 0f, 360f);
            list.Gap(12f);
            BackgroundNumber(list, "UnrestBonus", ref options.unrestBonusPercent, 0f, 500f);
            BackgroundNumber(list, "UnrestDays", ref options.unrestDays, 0f, 60f);
            list.Gap(12f);
            BackgroundRange(list, "RuinsDistance", ref options.ruinsDistance, 1, 100);
            BackgroundRange(list, "Hardtack", ref options.hardtack, 0, 1000);
            BackgroundRange(list, "Silver", ref options.silver, 0, 10000);
            BackgroundRange(list, "Components", ref options.components, 0, 100);
            list.Gap(12f);
            BackgroundNumber(list, "TradeDiscount", ref options.tradeDiscountPercent, 0f, 100f);
            BackgroundNumber(list, "WoundedJoinChance", ref options.woundedJoinChancePercent, 0f, 100f);
            BackgroundRange(list, "StoryDistance", ref options.storyDistance, 1, 100);
            BackgroundInteger(list, "EvacuationGoodwill", ref options.evacuationGoodwill, 100);
            BackgroundInteger(list, "MemorialSteel", ref options.memorialSteel, 10000);
            BackgroundInteger(list, "MemorialWood", ref options.memorialWood, 10000);
            BackgroundInteger(list, "RestorationSteel", ref options.restorationSteel, 10000);
            BackgroundInteger(list, "RestorationWood", ref options.restorationWood, 10000);
            BackgroundInteger(list, "MaterialsGoodwill", ref options.materialsGoodwill, 100);
            BackgroundInteger(list, "NamesUndergroundGoodwill", ref options.namesUndergroundGoodwill, 100);
            BackgroundInteger(list, "NamesWarlordGoodwill", ref options.namesWarlordGoodwill, 100);
            BackgroundInteger(list, "RestorationGoodwill", ref options.restorationGoodwill, 100);
        }

        static void BackgroundToggle(Listing_Standard list, string name, ref bool value)
        {
            RHAH_IrisMenusWidgets.Checkbox(list, "background-" + name,
                ("RHAH_Background_Settings" + name).Translate(), ref value);
        }

        void BackgroundEvent(Listing_Standard list, string name, ref bool enabled, ref float weight)
        {
            RHAH_IrisMenusWidgets.Checkbox(list, "background-event-" + name,
                ("RHAH_Background_" + name + "_Label").Translate(), ref enabled);
            BackgroundNumber(list, "Weight", ref weight, 0f, 10f, "background-weight-" + name);
        }

        void BackgroundNumber(Listing_Standard list, string name, ref float value, float min, float max, string id = null)
        {
            id = id ?? "background-" + name;
            MenuControls.Anchor(list, id);
            value = Mathf.Round(RHAH_IrisMenusWidgets.TuneFloat(list, weightBuffers, id,
                ("RHAH_Background_Settings" + name).Translate(value.ToString("0.0")), value, min, max, "0.0", null) * 10f) / 10f;
        }

        void BackgroundInteger(Listing_Standard list, string name, ref int value, int max)
        {
            string id = "background-" + name;
            MenuControls.Anchor(list, id);
            value = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, id,
                ("RHAH_Background_Settings" + name).Translate(value), value, 0, max, null);
        }

        void BackgroundRange(Listing_Standard list, string name, ref IntRange range, int min, int max)
        {
            string id = "background-" + name;
            MenuControls.Anchor(list, id);
            RHAH_IrisMenusWidgets.Label(list, ("RHAH_Background_Settings" + name).Translate(range.min, range.max));
            range.min = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, id + "-min",
                "RHAH_Background_SettingsMinimum".Translate(range.min), range.min, min, max, null);
            if (range.max < range.min)
            {
                range.max = range.min;
                weightBuffers[id + "-max"] = range.max.ToString();
            }
            range.max = RHAH_IrisMenusWidgets.TuneInt(list, weightBuffers, id + "-max",
                "RHAH_Background_SettingsMaximum".Translate(range.max), range.max, range.min, max, null);
        }

        IEnumerable<MenuSearchEntry> SearchBackgroundStories()
        {
            foreach (string name in backgroundSettings)
            {
                string key = "RHAH_Background_Settings" + name;
                yield return new MenuSearchEntry("background-" + name, () => key.Translate("…", "…"));
            }
            foreach (string name in backgroundEvents)
            {
                string key = "RHAH_Background_" + name + "_Label";
                yield return Entry("background-event-" + name, key);
                yield return new MenuSearchEntry("background-weight-" + name,
                    () => key.Translate() + " / " + "RHAH_Background_SettingsWeight".Translate("…"));
            }
        }
    }
}
