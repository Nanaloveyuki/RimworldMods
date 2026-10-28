using HarmonyLib;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisUI.Framework
{
    public sealed class FrameworkMod : Mod
    {
        public static FrameworkSettings Settings { get; private set; }
        private readonly Listing_Standard listing = new Listing_Standard();
        public FrameworkMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FrameworkSettings>();
            new Harmony("Nanaloveyuki.IrisUI.Framework").PatchAll(typeof(FrameworkMod).Assembly);
        }
        public override string SettingsCategory() => "IrisUI";
        public override void DoSettingsWindowContents(Rect rect)
        {
            listing.Begin(rect);
            bool dark = Settings.Theme == UiTheme.Dark;
            listing.CheckboxLabeled("IUI_DarkTheme".Translate(), ref dark);
            Settings.Theme = dark ? UiTheme.Dark : UiTheme.Light;
            listing.CheckboxLabeled("IUI_ReducedMotion".Translate(), ref Settings.ReducedMotion);
            listing.Label("IUI_Scale".Translate(Settings.Scale.ToStringPercent()));
            Settings.Scale = listing.Slider(Settings.Scale, 0.8f, 1.3f);
            listing.End();
        }
    }

    public sealed class FrameworkSettings : ModSettings
    {
        public UiTheme Theme = UiTheme.Light;
        public bool ReducedMotion;
        public float Scale = 1f;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref Theme, "theme", UiTheme.Light);
            Scribe_Values.Look(ref ReducedMotion, "reducedMotion", false);
            Scribe_Values.Look(ref Scale, "scale", 1f);
        }
    }
}
