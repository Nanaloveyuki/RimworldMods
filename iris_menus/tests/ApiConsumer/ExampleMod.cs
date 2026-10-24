using System;
using System.Collections.Generic;
using IrisMenus;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisMenusApiConsumer
{
    // Compile-only API consumer, not included in the shipped mod.
    public sealed class ExampleMod : Mod
    {
        private readonly ExampleSettings settings;
        private string numberBuffer;
        private string floatBuffer;
        private readonly IMarkdown dynamicMarkdown = Markdown.Dynamic(() => "**Live** help text");

        private static IMarkdown StaticMarkdown(string path)
        {
            return Markdown.StaticFile(path);
        }

        public ExampleMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<ExampleSettings>();
            MenuRegistry.RegisterModNameAlias(this, "English", "Example Mod");
            MenuRegistry.RegisterModNameAlias(content, "ChineseSimplified", "示例 Mod");
            MenuRegistry.RegisterModNameAlias("example.othermod", "English", "Other Mod");
            MenuRegistry.RegisterSubItemListing(this, "general", () => "General", Draw);
            MenuRegistry.RegisterPresentation(this, "general",
                new MenuPresentation(context => new ExampleNativePage(context, settings, WriteSettings, dynamicMarkdown)));
            MenuRegistry.RegisterSearchProvider(this, "general", () => new[]
            {
                new MenuSearchEntry("enabled", () => "Enabled", () => "activate toggle", () => "Enable the feature"),
                new MenuSearchEntry("strength", () => "Strength"),
                new MenuSearchEntry("samples", () => "Samples"),
                new MenuSearchEntry("threshold", () => "Threshold"),
                new MenuSearchEntry("mode", () => "Mode"),
                new MenuSearchEntry("help", () => "Help", context: () => dynamicMarkdown.MarkdownText)
            });
            var sections = new MenuSectionView(new[]
            {
                new MenuSection("advanced", width => 60f, rect => Widgets.Label(rect, "Advanced options")),
                new MenuSection("markdown", dynamicMarkdown.Measure, dynamicMarkdown.Draw,
                    () => dynamicMarkdown.MarkdownText)
            });
            MenuRegistry.RegisterSubItem(this, "advanced", () => "Advanced", sections.Draw);
            MenuRegistry.RegisterSearchProvider(this, "advanced", () => new[]
            {
                new MenuSearchEntry("advanced", () => "Advanced options")
                }, sections.Focus);
            MenuRegistry.RegisterSubItem(this, "markdown", () => "Markdown", dynamicMarkdown.Draw);
            MenuRegistry.ReplaceSubItemListings(this, "dynamic", new[]
            {
                new MenuSubItemListing("generated", () => "Generated", Draw, searchProvider: () => new[]
                {
                    new MenuSearchEntry("generated", () => "Generated")
                })
            });
            MenuOwnerActions.RegisterOwnerAction(this, "reload", () => "Reload", () => { },
                visible: () => true, enabled: () => settings.Enabled, confirmation: () => "Reload now?");
            MenuOwnerActions.RegisterOwnerAction(content, "export", () => "Export", () => { });
        }

        private void Draw(Listing_Standard listing)
        {
            MenuControls.Section(listing, "General");
            MenuControls.Anchor(listing, "enabled");
            MenuControls.Checkbox(listing, "Enabled", ref settings.Enabled);
            MenuControls.Anchor(listing, "strength");
            settings.Strength = MenuControls.Slider(listing, "Strength", settings.Strength, 0f, 1f);
            MenuControls.Section(listing, "Tuning");
            MenuControls.Anchor(listing, "samples");
            MenuControls.Number(listing, "Samples", ref settings.SampleCount, ref numberBuffer, 1, 16);
            MenuControls.Anchor(listing, "threshold");
            MenuControls.Number(listing, "Threshold", ref settings.Threshold, ref floatBuffer, 0f, 1f);
            MenuControls.Anchor(listing, "mode");
            MenuControls.Select(listing, "Mode", settings.Mode, new[] { "Default", "Custom" }, value => value, value => settings.Mode = value);
            MenuControls.Section(listing, "Help");
            MenuControls.Anchor(listing, "help");
            MenuControls.Markdown(listing, dynamicMarkdown);
        }
    }

    // One retained instance per host page: the page scrolls, and its popup draws at the root.
    public sealed class ExampleNativePage : MenuNativePage
    {
        private readonly UiContext context;
        private readonly ExampleSettings settings;
        private readonly Action save;
        private readonly IMarkdown help;
        private readonly UiScrollView scroll;
        private readonly UiSwitch enabled;
        private readonly UiSlider strength, samples, threshold;
        private readonly UiSelect mode;
        private readonly Action<Rect> drawContent;
        private readonly MenuChapter[] chapters =
        {
            new MenuChapter("general", () => "General"),
            new MenuChapter("tuning", () => "Tuning"),
            new MenuChapter("help", () => "Help")
        };
        private readonly MenuNote[] notes =
        {
            new MenuNote("tuning", () => "Samples and threshold",
                () => "Samples ranges from 1 to 16. Threshold and strength are independent values between zero and one.")
        };

        public ExampleNativePage(UiContext context, ExampleSettings settings, Action save, IMarkdown help)
        {
            this.context = context;
            this.settings = settings;
            this.save = save;
            this.help = help;
            scroll = new UiScrollView(context, 8);
            enabled = new UiSwitch(context);
            strength = new UiSlider(context, 0f, 1f, 0.01f);
            samples = new UiSlider(context, 1f, 16f, 1f);
            threshold = new UiSlider(context, 0f, 1f, 0.01f);
            mode = new UiSelect(context, new[] { "Default", "Custom" });
            drawContent = DrawContent;
        }

        public override IReadOnlyList<MenuChapter> Chapters => chapters;
        public override IReadOnlyList<MenuNote> Notes => notes;
        public override bool CanReset => true;
        public override void Prepare() => mode.Prepare();

        public override void Draw(Rect rect)
        {
            scroll.Draw(rect, 452f + help.Measure(Mathf.Max(1f, rect.width - 44f)), drawContent);
        }

        private void DrawContent(Rect rect)
        {
            float x = 12f, width = Mathf.Max(0f, rect.width - 24f);
            UiPalette palette = context.Palette;
            context.Fill(rect, palette.Surface);
            scroll.Anchor(0, new Rect(x, 0f, width, 156f), focusTarget: enabled.Id);
            context.Label(new Rect(x, 4f, width, 28f), "General", palette.Muted);
            scroll.Anchor(3, new Rect(x, 40f, width, 40f), focusTarget: enabled.Id);
            bool nextEnabled = enabled.Draw(new Rect(x, 40f, width, 40f), settings.Enabled, "Enabled");
            context.Label(new Rect(x, 88f, width, 24f), "Strength · " + settings.Strength.ToString("0.00"), palette.Ink);
            scroll.Anchor(4, new Rect(x, 112f, width, 40f), focusTarget: strength.Id);
            float nextStrength = strength.Draw(new Rect(x, 112f, width, 40f), settings.Strength);
            scroll.Anchor(1, new Rect(x, 172f, width, 252f), focusTarget: samples.Id);
            context.Label(new Rect(x, 172f, width, 28f), "Tuning", palette.Muted);
            context.Label(new Rect(x, 208f, width, 24f), "Samples · " + settings.SampleCount, palette.Ink);
            scroll.Anchor(5, new Rect(x, 232f, width, 40f), focusTarget: samples.Id);
            int nextSamples = Mathf.RoundToInt(samples.Draw(new Rect(x, 232f, width, 40f), settings.SampleCount));
            context.Label(new Rect(x, 280f, width, 24f), "Threshold · " + settings.Threshold.ToString("0.00"), palette.Ink);
            scroll.Anchor(6, new Rect(x, 304f, width, 40f), focusTarget: threshold.Id);
            float nextThreshold = threshold.Draw(new Rect(x, 304f, width, 40f), settings.Threshold);
            context.Label(new Rect(x, 352f, width, 24f), "Mode", palette.Ink);
            scroll.Anchor(7, new Rect(x, 380f, width, 40f), focusTarget: mode.Id);
            int nextMode = mode.Draw(new Rect(x, 380f, width, 40f), settings.Mode == "Custom" ? 1 : 0);
            float helpHeight = help.Measure(width);
            scroll.Anchor(2, new Rect(x, 440f, width, helpHeight));
            help.Draw(new Rect(x, 440f, width, helpHeight));
            if (nextEnabled == settings.Enabled && Mathf.Approximately(nextStrength, settings.Strength) && nextSamples == settings.SampleCount
                && Mathf.Approximately(nextThreshold, settings.Threshold) && (nextMode == 1) == (settings.Mode == "Custom")) return;
            settings.Enabled = nextEnabled;
            settings.Strength = nextStrength;
            settings.SampleCount = nextSamples;
            settings.Threshold = nextThreshold;
            settings.Mode = nextMode == 1 ? "Custom" : "Default";
            save();
        }

        public override void DrawOverlay(Rect viewport)
        {
            int before = settings.Mode == "Custom" ? 1 : 0;
            int after = mode.DrawPopup(viewport, before);
            if (before == after) return;
            settings.Mode = after == 1 ? "Custom" : "Default";
            save();
        }

        public override void Focus(string anchor)
        {
            switch (anchor)
            {
                case "general": scroll.FocusAnchor(0); break;
                case "tuning": scroll.FocusAnchor(1); break;
                case "help": scroll.FocusAnchor(2); break;
                case "enabled": scroll.FocusAnchor(3); break;
                case "strength": scroll.FocusAnchor(4); break;
                case "samples": scroll.FocusAnchor(5); break;
                case "threshold": scroll.FocusAnchor(6); break;
                case "mode": scroll.FocusAnchor(7); break;
            }
        }

        public override void Reset()
        {
            settings.Enabled = true;
            settings.Strength = 0.5f;
            settings.SampleCount = 4;
            settings.Threshold = 0.5f;
            settings.Mode = "Default";
            save();
        }

        public override void Dispose()
        {
            mode.Close();
            context.Release(enabled.Id);
            context.Release(strength.Id);
            context.Release(samples.Id);
            context.Release(threshold.Id);
            context.Release(mode.Id);
            context.Release(mode.GetOptionId(0));
            context.Release(mode.GetOptionId(1));
            context.Release(scroll.Id);
            for (int i = 0; i < scroll.AnchorCount; i++) context.Release(scroll.AnchorId(i));
        }
    }

    public sealed class ExampleSettings : ModSettings
    {
        public bool Enabled = true;
        public float Strength = 0.5f;
        public int SampleCount = 4;
        public float Threshold = 0.5f;
        public string Mode = "Default";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref Strength, "strength", 0.5f);
            Scribe_Values.Look(ref SampleCount, "sampleCount", 4);
            Scribe_Values.Look(ref Threshold, "threshold", 0.5f);
            Scribe_Values.Look(ref Mode, "mode", "Default");
        }
    }
}
