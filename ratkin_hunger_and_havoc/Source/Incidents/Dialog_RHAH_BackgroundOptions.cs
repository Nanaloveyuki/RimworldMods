using HungerAndHavoc.Core;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal sealed class Dialog_RHAH_BackgroundOptions : Window
    {
        Vector2 scroll;
        float height = 2200f;

        public override Vector2 InitialSize => new Vector2(760f, 760f);

        internal Dialog_RHAH_BackgroundOptions()
        {
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        internal static void DrawEntry(Listing_Standard list)
        {
            if (list.ButtonText("RHAH_Background_SettingsTitle".Translate()))
                Find.WindowStack.Add(new Dialog_RHAH_BackgroundOptions());
        }

        public override void DoWindowContents(Rect inRect)
        {
            RHAH_BackgroundOptions options = RHAH_Mod.Settings?.background;
            if (options == null) return;
            Rect body = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 50f);
            Rect view = new Rect(0f, 0f, body.width - 20f, height);
            Widgets.BeginScrollView(body, ref scroll, view);
            Listing_Standard list = new Listing_Standard { maxOneColumn = true };
            list.Begin(view);
            list.Label("RHAH_Background_SettingsTitle".Translate());
            list.Label("RHAH_Background_SettingsDescription".Translate());
            Toggle(list, "Enabled", ref options.enabled);
            Slider(list, "AverageDays", ref options.averageDays, 0f, 360f);
            list.Label("RHAH_Background_SettingsFrequency".Translate());
            Event(list, "Rebellion", ref options.rebellionEnabled, ref options.rebellionWeight);
            Event(list, "Ruins", ref options.ruinsEnabled, ref options.ruinsWeight);
            Event(list, "Unrest", ref options.unrestEnabled, ref options.unrestWeight);
            Event(list, "Mutiny", ref options.mutinyEnabled, ref options.mutinyWeight);
            Event(list, "TwoTaxes", ref options.twoTaxesEnabled, ref options.twoTaxesWeight);
            Event(list, "SameCallsign", ref options.sameCallsignEnabled, ref options.sameCallsignWeight);
            Event(list, "HarvestFestival", ref options.harvestFestivalEnabled, ref options.harvestFestivalWeight);
            Event(list, "Memorial", ref options.memorialEnabled, ref options.memorialWeight);
            Event(list, "AidManifest", ref options.aidManifestEnabled, ref options.aidManifestWeight);
            Event(list, "CancelledBanquet", ref options.cancelledBanquetEnabled, ref options.cancelledBanquetWeight);
            list.GapLine();
            Toggle(list, "CivilWar", ref options.civilWarEnabled);
            Slider(list, "CivilWarChance", ref options.civilWarChancePercent, 0f, 100f);
            Slider(list, "CivilWarDelay", ref options.civilWarDelayDays, 0f, 360f);
            Toggle(list, "Takeover", ref options.takeoverEnabled);
            list.Label("RHAH_Background_SettingsTakeoverRule".Translate());
            Slider(list, "TakeoverChance", ref options.takeoverChancePercent, 0f, 100f);
            Slider(list, "TakeoverDelay", ref options.takeoverDelayDays, 0f, 360f);
            list.GapLine();
            Slider(list, "UnrestBonus", ref options.unrestBonusPercent, 0f, 500f);
            Slider(list, "UnrestDays", ref options.unrestDays, 0f, 60f);
            list.GapLine();
            Range(list, "RuinsDistance", ref options.ruinsDistance, 1, 100);
            Range(list, "Hardtack", ref options.hardtack, 0, 1000);
            Range(list, "Silver", ref options.silver, 0, 10000);
            Range(list, "Components", ref options.components, 0, 100);
            list.GapLine();
            Slider(list, "TradeDiscount", ref options.tradeDiscountPercent, 0f, 100f);
            Slider(list, "WoundedJoinChance", ref options.woundedJoinChancePercent, 0f, 100f);
            Range(list, "StoryDistance", ref options.storyDistance, 1, 100);
            Integer(list, "EvacuationGoodwill", ref options.evacuationGoodwill, 100);
            Integer(list, "MemorialSteel", ref options.memorialSteel, 10000);
            Integer(list, "MemorialWood", ref options.memorialWood, 10000);
            Integer(list, "RestorationSteel", ref options.restorationSteel, 10000);
            Integer(list, "RestorationWood", ref options.restorationWood, 10000);
            Integer(list, "MaterialsGoodwill", ref options.materialsGoodwill, 100);
            Integer(list, "NamesUndergroundGoodwill", ref options.namesUndergroundGoodwill, 100);
            Integer(list, "NamesWarlordGoodwill", ref options.namesWarlordGoodwill, 100);
            Integer(list, "RestorationGoodwill", ref options.restorationGoodwill, 100);
            height = list.CurHeight + 12f;
            list.End();
            Widgets.EndScrollView();
        }

        static void Event(Listing_Standard list, string name, ref bool enabled, ref float weight)
        {
            list.CheckboxLabeled(("RHAH_Background_" + name + "_Label").Translate(), ref enabled);
            Slider(list, "Weight", ref weight, 0f, 10f);
        }

        static void Toggle(Listing_Standard list, string name, ref bool value)
        {
            list.CheckboxLabeled(("RHAH_Background_Settings" + name).Translate(), ref value);
        }

        static void Slider(Listing_Standard list, string name, ref float value, float min, float max)
        {
            list.Label(("RHAH_Background_Settings" + name).Translate(value.ToString("0.0")));
            value = Mathf.Round(list.Slider(value, min, max) * 10f) / 10f;
        }

        static void Integer(Listing_Standard list, string name, ref int value, int max)
        {
            list.Label(("RHAH_Background_Settings" + name).Translate(value));
            value = Mathf.RoundToInt(list.Slider(value, 0f, max));
        }

        static void Range(Listing_Standard list, string name, ref IntRange range, int min, int max)
        {
            list.Label(("RHAH_Background_Settings" + name).Translate(range.min, range.max));
            list.IntRange(ref range, min, max);
        }

        public override void PostClose()
        {
            base.PostClose();
            RHAH_Mod.Settings?.Mod.WriteSettings();
        }
    }
}
