using System;
using System.Collections.Generic;
using System.Globalization;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal static class NativeSettingsPages
    {
        private static bool registered;

        internal static void EnsureRegistered()
        {
            IrisMenusMod mod = IrisMenusMod.Instance;
            if (registered || mod == null) return;
            MenuRegistry.RegisterSubItemListing(mod, "appearance", () => T("IM_NativeAppearance"), DrawLegacyAppearance, mod.WriteSettings);
            MenuRegistry.RegisterPresentation(mod, "appearance", new MenuPresentation(context => new SettingsPage(context, mod, true)));
            MenuRegistry.RegisterSearchProvider(mod, "appearance", AppearanceSearch);
            MenuRegistry.RegisterSubItemListing(mod, "behavior", () => T("IM_NativeBehavior"), DrawLegacyBehavior, mod.WriteSettings);
            MenuRegistry.RegisterPresentation(mod, "behavior", new MenuPresentation(context => new SettingsPage(context, mod, false)));
            MenuRegistry.RegisterSearchProvider(mod, "behavior", BehaviorSearch);
            registered = true;
        }

        private static void DrawLegacyAppearance(Listing_Standard list)
        {
            MenuSettings settings = IrisMenusMod.Instance.Settings;
            MenuControls.Section(list, T("IM_NativeDisplay"));
            MenuControls.Anchor(list, "theme");
            MenuControls.Select(list, T("IM_NativeTheme"), settings.Theme, legacyThemes,
                value => T(value == UiTheme.Dark ? "IM_NativeDark" : "IM_NativeLight"), value => settings.Theme = value);
            MenuControls.Anchor(list, "density");
            MenuControls.Select(list, T("IM_NativeDensity"), settings.Density, legacyDensities,
                value => T(value == 0 ? "IM_NativeCompact" : value == 2 ? "IM_NativeRelaxed" : "IM_NativeStandard"), value => settings.Density = value);
            MenuControls.Anchor(list, "scale");
            settings.NativeScale = MenuControls.Slider(list, T("IM_NativeScale"), settings.NativeScale, 0.8f, 1.3f, "P0");
            MenuControls.Anchor(list, "motion");
            bool motion = !settings.ReducedMotion;
            MenuControls.Checkbox(list, T("IM_NativeMotion"), ref motion, T("IM_NativeMotionHint"));
            settings.ReducedMotion = !motion;
            MenuControls.Section(list, T("IM_NativeBackground"));
            MenuControls.Anchor(list, "opacity");
            settings.NativePanelOpacity = MenuControls.Slider(list, T("IM_NativeOpacity"), settings.NativePanelOpacity, 0f, 1f, "P0", T("IM_NativeOpacityHint"));
        }

        private static void DrawLegacyBehavior(Listing_Standard list)
        {
            MenuSettings settings = IrisMenusMod.Instance.Settings;
            MenuControls.Section(list, T("IM_NativeNavigation"));
            MenuControls.Anchor(list, "remember");
            bool remember = settings.RememberState;
            MenuControls.Checkbox(list, T("IM_NativeRemember"), ref remember, T("IM_NativeRememberHint"));
            if (remember != settings.RememberState)
            {
                settings.RememberState = remember;
                if (!remember) settings.ClearRememberedView();
            }
            MenuControls.Anchor(list, "main-button");
            MenuControls.Checkbox(list, T("IM_NativeMainButton"), ref settings.ShowMainButton, T("IM_NativeMainButtonHint"));
            MenuControls.Anchor(list, "search-scope");
            MenuControls.Checkbox(list, T("IM_NativeSearchScope") + ": " + T("IM_NativeCurrentMod"), ref settings.SearchCurrentMod);
            MenuControls.Section(list, T("IM_NativeInteraction"));
            MenuControls.Anchor(list, "tips");
            MenuControls.Checkbox(list, T("IM_NativeTips"), ref settings.ShowTips);
            MenuControls.Anchor(list, "tip-delay");
            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled &= settings.ShowTips;
                settings.TipDelay = MenuControls.Slider(list, T("IM_NativeTipDelay"), settings.TipDelay * 1000f, 100f, 800f, "F0", T("IM_NativeTipDelayHint")) / 1000f;
            }
            finally { GUI.enabled = enabled; }
        }

        private static readonly UiTheme[] legacyThemes = { UiTheme.Light, UiTheme.Dark };
        private static readonly int[] legacyDensities = { 0, 1, 2 };

        private static string T(string key) => key.Translate().ToString();
        private static Func<string> Text(string key) => () => T(key);
        private static MenuSearchEntry Search(string id, string label, string section, string hint = null) =>
            new MenuSearchEntry(id, Text(label), Text(section), hint == null ? Text(section) : Text(hint));

        private static readonly MenuSearchEntry[] appearanceSearch =
        {
            Search("theme", "IM_NativeTheme", "IM_NativeDisplay"),
            Search("density", "IM_NativeDensity", "IM_NativeDisplay"),
            Search("scale", "IM_NativeScale", "IM_NativeDisplay"),
            Search("motion", "IM_NativeMotion", "IM_NativeDisplay", "IM_NativeMotionHint"),
            Search("opacity", "IM_NativeOpacity", "IM_NativeBackground", "IM_NativeOpacityHint")
        };
        private static readonly MenuSearchEntry[] behaviorSearch =
        {
            Search("remember", "IM_NativeRemember", "IM_NativeNavigation", "IM_NativeRememberHint"),
            Search("main-button", "IM_NativeMainButton", "IM_NativeNavigation", "IM_NativeMainButtonHint"),
            Search("search-scope", "IM_NativeSearchScope", "IM_NativeNavigation"),
            Search("tips", "IM_NativeTips", "IM_NativeInteraction"),
            Search("tip-delay", "IM_NativeTipDelay", "IM_NativeInteraction", "IM_NativeTipDelayHint")
        };
        private static IEnumerable<MenuSearchEntry> AppearanceSearch() => appearanceSearch;
        private static IEnumerable<MenuSearchEntry> BehaviorSearch() => behaviorSearch;

        private enum RowKind { Switch, Slider, Segments, Select }

        private sealed class SettingRow
        {
            private readonly UiContext context;
            private readonly RowKind kind;
            private readonly string labelKey, hintKey;
            private readonly string[] optionKeys;
            private readonly Func<float> get;
            private readonly Action<float> set;
            private readonly UiSwitch toggle;
            private readonly UiSlider slider;
            private readonly UiSegments segments;
            private readonly UiSelect select;
            private readonly UiTooltip tooltip;
            private readonly string suffix;
            private readonly float displayMultiplier;
            private string label, hint, output;
            private float outputValue = float.NaN;

            internal SettingRow(UiContext context, string id, string labelKey, RowKind kind,
                Func<float> get, Action<float> set, string hintKey = null,
                float min = 0f, float max = 1f, float step = 0.01f,
                string[] optionKeys = null, float displayMultiplier = 100f, string suffix = "%")
            {
                this.context = context;
                Id = id;
                this.labelKey = labelKey;
                this.hintKey = hintKey;
                this.kind = kind;
                this.get = get;
                this.set = set;
                this.optionKeys = optionKeys;
                this.displayMultiplier = displayMultiplier;
                this.suffix = suffix;
                tooltip = new UiTooltip(context);
                switch (kind)
                {
                    case RowKind.Switch: toggle = new UiSwitch(context); break;
                    case RowKind.Slider: slider = new UiSlider(context, min, max, step); break;
                    case RowKind.Segments: segments = new UiSegments(context, TranslateOptions()); break;
                    case RowKind.Select: select = new UiSelect(context, TranslateOptions()); break;
                }
            }

            internal string Id { get; }
            internal UiId FocusTarget => toggle != null ? toggle.Id : slider != null ? slider.Id :
                select != null ? select.Id : segments.GetItemId(Mathf.Clamp(Mathf.RoundToInt(get()), 0, segments.Count - 1));

            private string[] TranslateOptions()
            {
                var result = new string[optionKeys.Length];
                for (int i = 0; i < result.Length; i++) result[i] = T(optionKeys[i]);
                return result;
            }

            internal void Translate()
            {
                label = T(labelKey);
                hint = hintKey == null ? null : T(hintKey);
                if (segments != null) segments.SetOptions(TranslateOptions());
                if (select != null) select.SetOptions(TranslateOptions());
            }

            internal void Prepare() => select?.Prepare();

            internal bool Draw(Rect row, float scale, bool stacked, bool enabled, MenuSettings settings)
            {
                UiPalette palette = context.Palette;
                float labelHeight = stacked ? 34f * scale : row.height;
                float controlWidth = stacked ? row.width : Mathf.Min(290f * scale, row.width * 0.47f);
                Rect labelRect = new Rect(row.x, row.y, stacked ? row.width : Mathf.Max(0f, row.width - controlWidth - 16f * scale), labelHeight);
                Rect control = new Rect(row.xMax - controlWidth, stacked ? row.y + labelHeight : row.center.y - 20f * scale,
                    controlWidth, 40f * scale);
                using (context.Opacity(enabled ? 1f : 0.4f))
                    context.Label(labelRect, label, palette.Ink, wrap: true);
                float before = get();
                float after = before;
                switch (kind)
                {
                    case RowKind.Switch:
                        after = toggle.Draw(new Rect(control.xMax - 56f * scale, control.y, 56f * scale, control.height), before > 0f, enabled: enabled) ? 1f : 0f;
                        break;
                    case RowKind.Slider:
                        float outputWidth = 64f * scale;
                        after = slider.Draw(new Rect(control.x, control.y, Mathf.Max(0f, control.width - outputWidth - 12f * scale), control.height), before, enabled);
                        if (after != outputValue)
                        {
                            outputValue = after;
                            output = Mathf.RoundToInt(after * displayMultiplier).ToString() + suffix;
                        }
                        using (context.Opacity(enabled ? 1f : 0.4f))
                            context.Label(new Rect(control.xMax - outputWidth, control.y, outputWidth, control.height), output, palette.Ink, TextAnchor.MiddleRight);
                        break;
                    case RowKind.Segments: after = segments.Draw(control, Mathf.RoundToInt(before), enabled); break;
                    case RowKind.Select: after = select.Draw(control, Mathf.RoundToInt(before), enabled); break;
                }
                tooltip.Delay = settings.TipDelay;
                if (hint != null && context.IsRectVisible(row)) tooltip.Observe(row, hint, FocusTarget, settings.ShowTips && enabled);
                if (!enabled || Mathf.Approximately(before, after)) return false;
                set(after);
                return true;
            }

            internal bool DrawOverlay(Rect viewport, bool enabled)
            {
                if (select == null) return false;
                float before = get();
                int after = select.DrawPopup(viewport, Mathf.RoundToInt(before), enabled);
                if (before == after) return false;
                set(after);
                return true;
            }

            internal void DrawTooltip(Rect viewport, float scale, MenuSettings settings)
            {
                if (!settings.ShowTips) tooltip.Dismiss();
                tooltip.DrawOverlay(viewport, new Vector2(320f * scale, 92f * scale));
            }

            internal void Dispose()
            {
                select?.Close();
                if (toggle != null) context.Release(toggle.Id);
                if (slider != null) context.Release(slider.Id);
                if (select != null)
                {
                    context.Release(select.Id);
                    for (int i = 0; i < optionKeys.Length; i++) context.Release(select.GetOptionId(i));
                }
                if (segments != null)
                    for (int i = 0; i < segments.Count; i++) context.Release(segments.GetItemId(i));
                tooltip.Dismiss();
            }
        }

        private sealed class SettingsPage : MenuNativePage
        {
            private readonly UiContext context;
            private readonly MenuSettings settings;
            private readonly bool appearance;
            private readonly SettingRow[] rows;
            private readonly UiScrollView scroll;
            private readonly Action<Rect> drawContent;
            private readonly MenuChapter[] chapters;
            private readonly MenuNote[] notes;
            private readonly int firstSectionCount;
            private string language, firstTitle, secondTitle, savedLabel;
            private float scale, rowHeight;
            private bool stacked;
            private int previewScaleValue = -1, previewOpacityValue = -1;
            private string previewScale, previewOpacity;
            private readonly NativeBackdrop backdrop = new NativeBackdrop();
            private readonly string scrollKey;
            private float savedScroll;
            private string currentAppearance, densityLabel, scaleLabel, opacityLabel, motionLabel;
            private string compact, standard, relaxed, onLabel, offLabel;

            internal SettingsPage(UiContext context, IrisMenusMod mod, bool appearance)
            {
                this.context = context ?? throw new ArgumentNullException(nameof(context));
                settings = mod.Settings;
                this.appearance = appearance;
                if (appearance)
                {
                    rows = new[]
                    {
                        new SettingRow(context, "theme", "IM_NativeTheme", RowKind.Segments,
                            () => settings.Theme == UiTheme.Dark ? 1f : 0f, value => settings.Theme = value > 0f ? UiTheme.Dark : UiTheme.Light,
                            optionKeys: new[] { "IM_NativeLight", "IM_NativeDark" }),
                        new SettingRow(context, "density", "IM_NativeDensity", RowKind.Segments,
                            () => settings.Density, value => settings.Density = Mathf.RoundToInt(value),
                            optionKeys: new[] { "IM_NativeCompact", "IM_NativeStandard", "IM_NativeRelaxed" }),
                        new SettingRow(context, "scale", "IM_NativeScale", RowKind.Slider,
                            () => settings.NativeScale, value => settings.NativeScale = value, min: 0.8f, max: 1.3f),
                        new SettingRow(context, "motion", "IM_NativeMotion", RowKind.Switch,
                            () => settings.ReducedMotion ? 0f : 1f, value => settings.ReducedMotion = value == 0f, "IM_NativeMotionHint"),
                        new SettingRow(context, "opacity", "IM_NativeOpacity", RowKind.Slider,
                            () => settings.NativePanelOpacity, value => settings.NativePanelOpacity = value, "IM_NativeOpacityHint")
                    };
                    firstSectionCount = 4;
                    chapters = new[] { new MenuChapter("display", Text("IM_NativeDisplay")), new MenuChapter("background", Text("IM_NativeBackground")) };
                    notes = new[]
                    {
                        new MenuNote("display", Text("IM_NativeDisplayNoteTitle"), Text("IM_NativeDisplayNote")),
                        new MenuNote("background", Text("IM_NativeBackgroundNoteTitle"), Text("IM_NativeBackgroundNote"))
                    };
                }
                else
                {
                    rows = new[]
                    {
                        new SettingRow(context, "remember", "IM_NativeRemember", RowKind.Switch,
                            () => settings.RememberState ? 1f : 0f, SetRemember, "IM_NativeRememberHint"),
                        new SettingRow(context, "main-button", "IM_NativeMainButton", RowKind.Switch,
                            () => settings.ShowMainButton ? 1f : 0f, value => settings.ShowMainButton = value > 0f, "IM_NativeMainButtonHint"),
                        new SettingRow(context, "search-scope", "IM_NativeSearchScope", RowKind.Select,
                            () => settings.SearchCurrentMod ? 1f : 0f, value => settings.SearchCurrentMod = value > 0f,
                            optionKeys: new[] { "IM_NativeAllMods", "IM_NativeCurrentMod" }),
                        new SettingRow(context, "tips", "IM_NativeTips", RowKind.Switch,
                            () => settings.ShowTips ? 1f : 0f, value => settings.ShowTips = value > 0f),
                        new SettingRow(context, "tip-delay", "IM_NativeTipDelay", RowKind.Slider,
                            () => settings.TipDelay, value => settings.TipDelay = value, "IM_NativeTipDelayHint",
                            min: 0.1f, max: 0.8f, step: 0.05f, displayMultiplier: 1000f, suffix: " ms")
                    };
                    firstSectionCount = 3;
                    chapters = new[] { new MenuChapter("navigation", Text("IM_NativeNavigation")), new MenuChapter("interaction", Text("IM_NativeInteraction")) };
                    notes = new[]
                    {
                        new MenuNote("navigation", Text("IM_NativeNavigationNoteTitle"), Text("IM_NativeNavigationNote")),
                        new MenuNote("interaction", Text("IM_NativeInteractionNoteTitle"), Text("IM_NativeInteractionNote"))
                    };
                }
                scroll = new UiScrollView(context, rows.Length + 2);
                scrollKey = appearance ? "native-settings:appearance:scroll" : "native-settings:behavior:scroll";
                if (settings.RememberState && settings.HostView.TryGetValue(scrollKey, out string remembered)
                    && float.TryParse(remembered, NumberStyles.Float, CultureInfo.InvariantCulture, out float position)
                    && !float.IsNaN(position) && !float.IsInfinity(position))
                {
                    scroll.SetPosition(position);
                    savedScroll = scroll.Position.y;
                }
                drawContent = DrawContent;
                RefreshLanguage();
            }

            public override IReadOnlyList<MenuChapter> Chapters => chapters;
            public override IReadOnlyList<MenuNote> Notes => notes;
            public override bool HasPreview => appearance;
            public override bool CanReset => true;

            private void SetRemember(float value)
            {
                settings.RememberState = value > 0f;
                if (!settings.RememberState) settings.ClearRememberedView();
            }

            private void RefreshLanguage()
            {
                language = LanguageDatabase.activeLanguage?.folderName;
                firstTitle = chapters[0].Title();
                secondTitle = chapters[1].Title();
                savedLabel = T("IM_NativeSavedAutomatically");
                for (int i = 0; i < rows.Length; i++) rows[i].Translate();
                currentAppearance = T("IM_NativeCurrentAppearance");
                densityLabel = T("IM_NativeDensity");
                scaleLabel = T("IM_NativeScale");
                opacityLabel = T("IM_NativeOpacity");
                motionLabel = T("IM_NativeMotion");
                compact = T("IM_NativeCompact");
                standard = T("IM_NativeStandard");
                relaxed = T("IM_NativeRelaxed");
                onLabel = T("IM_NativeOn");
                offLabel = T("IM_NativeOff");
            }

            public override void Prepare()
            {
                if (language != LanguageDatabase.activeLanguage?.folderName) RefreshLanguage();
                for (int i = 0; i < rows.Length; i++) rows[i].Prepare();
            }

            public override void Draw(Rect rect)
            {
                scale = settings.NativeScale;
                float contentWidth = Mathf.Max(0f, rect.width - 20f);
                float inset = Mathf.Min(28f * scale, contentWidth * 0.06f);
                stacked = contentWidth - inset * 2f < 450f * scale;
                rowHeight = (stacked ? settings.Density == 0 ? 88f : settings.Density == 2 ? 112f : 100f :
                    settings.Density == 0 ? 54f : settings.Density == 2 ? 78f : 66f) * scale;
                float naturalHeight = rows.Length * rowHeight + 2f * 64f * scale + 24f * scale + 58f * scale;
                scroll.Draw(rect, naturalHeight, drawContent);
                if (settings.RememberState && savedScroll != scroll.Position.y)
                {
                    savedScroll = scroll.Position.y;
                    settings.HostView[scrollKey] = savedScroll.ToString(CultureInfo.InvariantCulture);
                }
            }

            private void DrawContent(Rect rect)
            {
                float firstHeight = 64f * scale + firstSectionCount * rowHeight;
                DrawSection(new Rect(0f, 0f, rect.width, firstHeight), 0, firstTitle, 0, firstSectionCount);
                float secondY = firstHeight + 24f * scale;
                float secondHeight = 64f * scale + (rows.Length - firstSectionCount) * rowHeight;
                DrawSection(new Rect(0f, secondY, rect.width, secondHeight), 1, secondTitle, firstSectionCount, rows.Length - firstSectionCount);
                context.Label(new Rect(4f * scale, secondY + secondHeight + 12f * scale, Mathf.Max(0f, rect.width - 8f * scale), 36f * scale),
                    savedLabel, context.Palette.Muted, wrap: true);
            }

            private void DrawSection(Rect panel, int anchor, string title, int start, int count)
            {
                UiPalette palette = context.Palette;
                Color sheet = palette.Surface;
                sheet.a = 0.86f;
                context.Fill(panel, sheet);
                scroll.Anchor(anchor, panel, focusTarget: rows[start].FocusTarget);
                float inset = Mathf.Min(28f * scale, panel.width * 0.06f);
                float width = Mathf.Max(0f, panel.width - inset * 2f);
                context.Fill(new Rect(panel.x + inset, panel.y + 30f * scale, 3f * scale, 10f * scale), palette.Muted);
                context.Label(new Rect(panel.x + inset + 12f * scale, panel.y + 18f * scale, Mathf.Max(0f, width - 12f * scale), 32f * scale), title, palette.Muted);
                for (int i = start; i < start + count; i++)
                {
                    Rect row = new Rect(panel.x + inset, panel.y + 52f * scale + (i - start) * rowHeight, width, rowHeight);
                    scroll.Anchor(i + 2, row, focusTarget: rows[i].FocusTarget);
                    bool enabled = appearance || i != 4 || settings.ShowTips;
                    rows[i].Draw(row, scale, stacked, enabled, settings);
                    if (i < start + count - 1)
                    {
                        Color line = palette.Line;
                        line.a *= 0.55f;
                        context.Fill(new Rect(row.x, row.yMax - 1f, row.width, 1f), line);
                    }
                }
            }

            public override void DrawOverlay(Rect viewport)
            {
                for (int i = 0; i < rows.Length; i++) rows[i].DrawOverlay(viewport, true);
                for (int i = 0; i < rows.Length; i++) rows[i].DrawTooltip(viewport, settings.NativeScale, settings);
            }

            public override void Focus(string anchor)
            {
                for (int i = 0; i < chapters.Length; i++)
                    if (chapters[i].Id == anchor) { scroll.FocusAnchor(i); return; }
                for (int i = 0; i < rows.Length; i++)
                    if (rows[i].Id == anchor) { scroll.FocusAnchor(i + 2); return; }
            }

            public override void Reset()
            {
                if (appearance)
                {
                    settings.Theme = UiTheme.Light;
                    settings.Density = 1;
                    settings.NativeScale = 1f;
                    settings.ReducedMotion = false;
                    settings.NativePanelOpacity = 0.94f;
                }
                else
                {
                    settings.RememberState = true;
                    settings.ShowMainButton = true;
                    settings.SearchCurrentMod = false;
                    settings.ShowTips = true;
                    settings.TipDelay = 0.5f;
                }
            }

            public override void DrawPreview(Rect rect)
            {
                if (!appearance || rect.width <= 0f || rect.height <= 0f) return;
                UiPalette palette = context.Palette;
                float s = settings.NativeScale;
                float labelHeight = 30f * s;
                context.Label(new Rect(rect.x, rect.y, rect.width, labelHeight), currentAppearance, palette.Muted);
                float sceneHeight = Mathf.Min(rect.width * 0.75f, Mathf.Max(0f, rect.height - 170f * s));
                if (sceneHeight < 80f) return;
                Rect sample = new Rect(rect.x, rect.y + labelHeight + 12f * s, rect.width, sceneHeight);
                backdrop.Draw(context, sample);
                float miniWidth = Mathf.Min(sample.width - 24f, 210f * s);
                float miniHeight = Mathf.Min(sample.height - 24f, 138f * s);
                Rect window = new Rect(sample.center.x - miniWidth * 0.5f, sample.center.y - miniHeight * 0.5f, miniWidth, miniHeight);
                Color fill = palette.Surface;
                fill.a = settings.NativePanelOpacity;
                context.Fill(window, fill);
                context.Border(window, palette.StrongLine);
                float top = window.y + 24f;
                context.Fill(new Rect(window.x, top, window.width, 1f), palette.Line);
                context.Label(new Rect(window.x + 10f, window.y, window.width - 20f, 24f), "IrisMenus", palette.Ink);
                float navWidth = window.width * 0.26f;
                context.Fill(new Rect(window.x + navWidth, top, 1f, window.yMax - top), palette.Line);
                float gap = settings.Density == 0 ? 9f : settings.Density == 2 ? 17f : 13f;
                for (int i = 0; i < 3; i++)
                {
                    context.Fill(new Rect(window.x + 7f, top + 12f + i * (gap + 5f), Mathf.Max(0f, navWidth - 14f), i == 0 ? 12f : 4f), i == 0 ? palette.Selected : palette.Muted);
                }
                Rect content = new Rect(window.x + navWidth + 12f, top + 12f, Mathf.Max(0f, window.width - navWidth - 24f), Mathf.Max(0f, window.yMax - top - 24f));
                DrawMiniRow(content, 0f, settings.ShowTips, palette);
                DrawMiniRow(content, gap + 12f, !settings.ReducedMotion, palette);
                float sliderY = Mathf.Min(content.yMax - 4f, content.y + 2f * (gap + 12f));
                context.Fill(new Rect(content.x, sliderY, content.width, 2f), palette.StrongLine);
                float amount = Mathf.InverseLerp(0.8f, 1.3f, settings.NativeScale);
                context.Fill(new Rect(content.x, sliderY, content.width * amount, 2f), palette.Selected);
                context.Fill(new Rect(content.x + content.width * amount - 2f, sliderY - 3f, 4f, 8f), palette.Selected);
                UpdatePreviewValues();
                float yFacts = sample.yMax + 12f * s;
                DrawFact(rect.x, yFacts, rect.width, scaleLabel, previewScale);
                DrawFact(rect.x, yFacts + 30f * s, rect.width, opacityLabel, previewOpacity);
                DrawFact(rect.x, yFacts + 60f * s, rect.width, densityLabel, settings.Density == 0 ? compact : settings.Density == 2 ? relaxed : standard);
                DrawFact(rect.x, yFacts + 90f * s, rect.width, motionLabel, settings.ReducedMotion ? offLabel : onLabel);
            }

            private void DrawMiniRow(Rect content, float offset, bool on, UiPalette palette)
            {
                float y = content.y + offset;
                context.Fill(new Rect(content.x, y + 4f, Mathf.Min(28f, content.width * 0.35f), 3f), palette.Muted);
                Rect track = new Rect(content.xMax - 20f, y, 20f, 10f);
                context.Fill(track, on ? palette.SwitchOn : palette.StrongLine, 5f);
                context.Fill(new Rect(on ? track.xMax - 8f : track.x + 2f, y + 2f, 6f, 6f), on ? palette.Signal : palette.Surface, 3f);
            }

            private void UpdatePreviewValues()
            {
                int value = Mathf.RoundToInt(settings.NativeScale * 100f);
                if (value != previewScaleValue) { previewScaleValue = value; previewScale = value.ToString() + "%"; }
                value = Mathf.RoundToInt(settings.NativePanelOpacity * 100f);
                if (value != previewOpacityValue) { previewOpacityValue = value; previewOpacity = value.ToString() + "%"; }
            }

            private void DrawFact(float x, float y, float width, string label, string value)
            {
                context.Label(new Rect(x, y, width * 0.64f, 28f * settings.NativeScale), label, context.Palette.Muted);
                context.Label(new Rect(x + width * 0.64f, y, width * 0.36f, 28f * settings.NativeScale), value, context.Palette.Ink, TextAnchor.MiddleRight);
            }

            public override void Dispose()
            {
                backdrop.Dispose();
                for (int i = 0; i < rows.Length; i++) rows[i].Dispose();
                context.Release(scroll.Id);
                for (int i = 0; i < scroll.AnchorCount; i++) context.Release(scroll.AnchorId(i));
            }
        }
    }
}
