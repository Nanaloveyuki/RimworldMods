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
            MenuRegistry.RegisterNativeSubItem(mod, "appearance", () => T("IM_NativeAppearance"),
                new MenuPresentation(context => new SettingsPage(context, mod, true)), mod.WriteSettings);
            MenuRegistry.RegisterSearchProvider(mod, "appearance", AppearanceSearch);
            MenuRegistry.RegisterNativeSubItem(mod, "behavior", () => T("IM_NativeBehavior"),
                new MenuPresentation(context => new SettingsPage(context, mod, false)), mod.WriteSettings);
            MenuRegistry.RegisterSearchProvider(mod, "behavior", BehaviorSearch);
            MenuRegistry.RegisterNativeSubItem(mod, "value-colors", () => T("IM_ValueColors"),
                new MenuPresentation(context => new ItemValuePalettePage(context, mod)), mod.WriteSettings);
            MenuRegistry.RegisterSearchProvider(mod, "value-colors", () => new[] { Search("bands", "IM_ValueColors", "IM_ValueColors", "IM_ValueColorsHint") });
            ThingRegistryPage.Register(mod);
            registered = true;
        }

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
            Search("opacity", "IM_NativeOpacity", "IM_NativeBackground", "IM_NativeOpacityHint"),
            Search("marquee", "IM_MarqueeText", "IM_NativeBackground")
        };
        private static readonly MenuSearchEntry[] behaviorSearch =
        {
            Search("remember", "IM_NativeRemember", "IM_NativeNavigation", "IM_NativeRememberHint"),
            Search("main-button", "IM_NativeMainButton", "IM_NativeNavigation", "IM_NativeMainButtonHint"),
            Search("search-scope", "IM_NativeSearchScope", "IM_NativeNavigation"),
            Search("tips", "IM_NativeTips", "IM_NativeInteraction"),
            Search("tip-delay", "IM_NativeTipDelay", "IM_NativeInteraction", "IM_NativeTipDelayHint"),
            Search("toast-duration", "IM_ToastDuration", "IM_NativeInteraction"),
            Search("toasts", "IM_ShowToasts", "IM_NativeInteraction")
        };
        private static IEnumerable<MenuSearchEntry> AppearanceSearch() => appearanceSearch;
        private static IEnumerable<MenuSearchEntry> BehaviorSearch() => behaviorSearch;

        private enum RowKind { Switch, Slider, Segments, Select, Text }

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
            private readonly UiHoverCard tooltip;
            private readonly string suffix;
            private readonly float displayMultiplier;
            private string label, hint, output;
            private readonly UiTextField textField;
            private readonly Func<string> getText;
            private readonly Action<string> setText;
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
                tooltip = new UiHoverCard(context);
                switch (kind)
                {
                    case RowKind.Switch: toggle = new UiSwitch(context); break;
                    case RowKind.Slider: slider = new UiSlider(context, min, max, step); break;
                    case RowKind.Segments: segments = new UiSegments(context, TranslateOptions()); break;
                    case RowKind.Select: select = new UiSelect(context, TranslateOptions()); break;
                }
            }

            internal SettingRow(UiContext context, string id, string labelKey, Func<string> get, Action<string> set)
            {
                this.context = context;
                Id = id;
                this.labelKey = labelKey;
                kind = RowKind.Text;
                getText = get;
                setText = set;
                textField = new UiTextField(context);
                tooltip = new UiHoverCard(context);
            }

            internal string Id { get; }
            internal UiId FocusTarget => textField != null ? textField.Id : toggle != null ? toggle.Id : slider != null ? slider.Id :
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
                bool rowStacked = stacked && kind != RowKind.Switch;
                float labelHeight = rowStacked ? 34f * scale : row.height;
                float controlWidth = kind == RowKind.Switch ? 52f * scale : rowStacked ? row.width : Mathf.Min(290f * scale, row.width * 0.45f);
                Rect labelRect = new Rect(row.x, row.y, rowStacked ? row.width : Mathf.Max(0f, row.width - controlWidth - 16f * scale), labelHeight);
                Rect control = new Rect(row.xMax - controlWidth, rowStacked ? row.y + labelHeight : row.center.y - 20f * scale,
                    controlWidth, 40f * scale);
                using (context.Opacity(enabled ? 1f : 0.4f))
                {
                    float hintWidth = hint == null ? 0f : 22f * scale;
                    context.Label(new Rect(labelRect.x, labelRect.y, Mathf.Max(0f, labelRect.width - hintWidth), labelRect.height), label, palette.Ink, wrap: true);
                    if (hint != null)
                        UiGlyph.Draw(context, new Rect(labelRect.xMax - 15f * scale, labelRect.center.y - 6.5f * scale, 13f * scale, 13f * scale), UiGlyphKind.Help, palette.Muted);
                }
                if (kind == RowKind.Text)
                {
                    string previous = getText();
                    string next = textField.Draw(control, previous, enabled: enabled, scale: scale);
                    if (previous == next) return false;
                    setText(next);
                    return true;
                }
                float before = get();
                float after = before;
                switch (kind)
                {
                    case RowKind.Switch:
                        after = toggle.Draw(new Rect(control.xMax - 52f * scale, control.y, 52f * scale, control.height), before > 0f, enabled: enabled, scale: scale) ? 1f : 0f;
                        break;
                    case RowKind.Slider:
                        float outputWidth = 64f * scale;
                        after = slider.Draw(new Rect(control.x, control.y, Mathf.Max(0f, control.width - outputWidth - 12f * scale), control.height), before, enabled, scale: scale);
                        if (after != outputValue)
                        {
                            outputValue = after;
                            output = Mathf.RoundToInt(after * displayMultiplier).ToString() + suffix;
                        }
                        using (context.Opacity(enabled ? 1f : 0.4f))
                            context.Label(new Rect(control.xMax - outputWidth, control.y, outputWidth, control.height), output, palette.Ink, TextAnchor.MiddleRight);
                        break;
                    case RowKind.Segments: after = segments.Draw(control, Mathf.RoundToInt(before), enabled, scale: scale); break;
                    case RowKind.Select: after = select.Draw(control, Mathf.RoundToInt(before), enabled, scale: scale); break;
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
                tooltip.DrawOverlay(viewport, scale);
            }

            internal void Dispose()
            {
                select?.Close();
                textField?.Dispose();
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
            private string language, firstTitle, secondTitle;
            private float scale, rowHeight, secondSectionY, naturalHeight, viewportHeight;
            private bool stacked;
            private int previewScaleValue = -1, previewOpacityValue = -1;
            private string previewScale, previewOpacity;
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
                            () => settings.Scale, value => settings.Scale = value, min: 0.8f, max: 1.3f),
                        new SettingRow(context, "motion", "IM_NativeMotion", RowKind.Switch,
                            () => settings.ReducedMotion ? 0f : 1f, value => settings.ReducedMotion = value == 0f, "IM_NativeMotionHint"),
                        new SettingRow(context, "opacity", "IM_NativeOpacity", RowKind.Slider,
                            () => settings.PanelOpacity, value => settings.PanelOpacity = value, "IM_NativeOpacityHint"),
                        new SettingRow(context, "marquee", "IM_MarqueeText", () => settings.MarqueeText, value => settings.MarqueeText = value)
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
                            min: 0.1f, max: 0.8f, step: 0.05f, displayMultiplier: 1000f, suffix: " ms"),
                        new SettingRow(context, "toast-duration", "IM_ToastDuration", RowKind.Slider,
                            () => settings.ToastDuration, value => settings.ToastDuration = value,
                            min: 1f, max: 10f, step: 1f, displayMultiplier: 1f, suffix: " s"),
                        new SettingRow(context, "toasts", "IM_ShowToasts", RowKind.Switch,
                            () => settings.ShowToasts ? 1f : 0f, value => settings.ShowToasts = value > 0f)
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
            public override string ActiveChapterId => chapters[scroll.Position.y + 24f * scale >= secondSectionY ||
                (naturalHeight > viewportHeight && scroll.Position.y >= naturalHeight - viewportHeight - 1f) ? 1 : 0].Id;
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
                scale = settings.Scale;
                float contentWidth = Mathf.Max(0f, rect.width - 20f);
                stacked = contentWidth < 450f * scale;
                rowHeight = (stacked ? settings.Density == 0 ? 88f : settings.Density == 2 ? 112f : 100f :
                    settings.Density == 0 ? 54f : settings.Density == 2 ? 78f : 66f) * scale;
                secondSectionY = 64f * scale + firstSectionCount * rowHeight;
                naturalHeight = rows.Length * rowHeight + 128f * scale;
                viewportHeight = rect.height;
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
                float secondHeight = 64f * scale + (rows.Length - firstSectionCount) * rowHeight;
                DrawSection(new Rect(0f, secondSectionY, rect.width, secondHeight), 1, secondTitle, firstSectionCount, rows.Length - firstSectionCount);
            }

            private void DrawSection(Rect panel, int anchor, string title, int start, int count)
            {
                UiPalette palette = context.Palette;
                scroll.Anchor(anchor, panel, focusTarget: rows[start].FocusTarget);
                float oldSize = context.FontSize;
                FontStyle oldStyle = context.FontStyle;
                context.FontSize = 12f * scale;
                context.FontStyle = FontStyle.Bold;
                context.Fill(new Rect(panel.x, panel.y + 30f * scale, 3f * scale, 10f * scale), palette.Marker);
                context.Label(new Rect(panel.x + 12f * scale, panel.y + 18f * scale, Mathf.Max(0f, panel.width - 12f * scale), 32f * scale), title, palette.Muted);
                context.FontStyle = FontStyle.Normal;
                context.FontSize = 13f * scale;
                for (int i = start; i < start + count; i++)
                {
                    Rect row = new Rect(panel.x, panel.y + 52f * scale + (i - start) * rowHeight, panel.width, rowHeight);
                    scroll.Anchor(i + 2, row, focusTarget: rows[i].FocusTarget);
                    bool enabled = appearance || i != 4 || settings.ShowTips;
                    rows[i].Draw(row, scale, stacked, enabled, settings);
                    if (i < start + count - 1)
                        context.Fill(new Rect(row.x, row.yMax - 1f, row.width, 1f), palette.SoftLine);
                }
                context.FontSize = oldSize;
                context.FontStyle = oldStyle;
            }

            public override void DrawOverlay(Rect viewport)
            {
                for (int i = 0; i < rows.Length; i++) rows[i].DrawOverlay(viewport, true);
                for (int i = 0; i < rows.Length; i++) rows[i].DrawTooltip(viewport, settings.Scale, settings);
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
                    settings.Scale = 1f;
                    settings.ReducedMotion = false;
                    settings.PanelOpacity = 0.94f;
                    settings.MarqueeText = "IRIS MENUS";
                }
                else
                {
                    settings.RememberState = true;
                    settings.ShowMainButton = true;
                    settings.SearchCurrentMod = false;
                    settings.ShowTips = true;
                    settings.TipDelay = 0.3f;
                    settings.ToastDuration = 3f;
                    settings.ShowToasts = true;
                }
            }

            public override void DrawPreview(Rect rect)
            {
                if (!appearance || rect.width <= 0f || rect.height <= 0f) return;
                UiPalette palette = context.Palette;
                float s = settings.Scale;
                float oldSize = context.FontSize;
                context.FontSize = 11f * s;
                float labelHeight = 22f * s;
                UiGlyph.Draw(context, new Rect(rect.x, rect.y + 4f * s, 14f * s, 14f * s), UiGlyphKind.Grid, palette.Muted);
                context.Label(new Rect(rect.x + 22f * s, rect.y, Mathf.Max(0f, rect.width - 22f * s), labelHeight), currentAppearance, palette.Muted);
                float sceneHeight = Mathf.Min(rect.width * 0.75f, Mathf.Max(0f, rect.height - 170f * s));
                if (sceneHeight < 80f) { context.FontSize = oldSize; return; }
                Rect sample = new Rect(rect.x, rect.y + labelHeight + 15f * s, rect.width, sceneHeight);
                DrawPreviewScene(sample, palette, s);
                float miniWidth = Mathf.Min(sample.width - 38f * s, 244f * s);
                float miniHeight = Mathf.Min(sample.height - 36f * s, 142f * s);
                Rect window = new Rect(sample.center.x - miniWidth * 0.5f, sample.center.y - miniHeight * 0.5f, miniWidth, miniHeight);
                Color fill = palette.Mini;
                fill.a *= settings.PanelOpacity;
                context.Fill(new Rect(window.x + 3f * s, window.y + 8f * s, window.width, window.height), palette.Shadow);
                context.Fill(window, fill);
                context.Border(window, palette.PreviewEdge);
                float unit = Mathf.Min(s, window.width / 244f);
                float top = window.y + 28f * unit;
                context.Fill(new Rect(window.x, top, window.width, 1f), palette.Line);
                context.FontSize = 9f * s;
                context.Label(new Rect(window.x + 11f * unit, window.y, window.width - 22f * unit, top - window.y), "IrisMenus", palette.Ink);
                UiGlyph.Draw(context, new Rect(window.xMax - 18f * unit, window.y + 9f * unit, 10f * unit, 10f * unit), UiGlyphKind.Close, palette.Muted);
                float navWidth = window.width * 0.27f;
                context.Fill(new Rect(window.x + navWidth, top, 1f, window.yMax - top), palette.Line);
                float gap = (settings.Density == 0 ? 9f : settings.Density == 2 ? 17f : 13f) * unit;
                for (int i = 0; i < 3; i++)
                    context.Fill(new Rect(window.x + 7f * unit, top + 12f * unit + i * (gap + 5f * unit),
                        Mathf.Max(0f, navWidth - 14f * unit), (i == 0 ? 12f : 4f) * unit), i == 0 ? palette.Selected : palette.MiniMark);
                Rect content = new Rect(window.x + navWidth + 13f * unit, top + 12f * unit,
                    Mathf.Max(0f, window.width - navWidth - 26f * unit), Mathf.Max(0f, window.yMax - top - 24f * unit));
                DrawMiniRow(content, 0f, settings.ShowTips, palette, unit);
                DrawMiniRow(content, gap + 12f * unit, !settings.ReducedMotion, palette, unit);
                float sliderY = Mathf.Min(content.yMax - 4f * unit, content.y + 2f * (gap + 12f * unit));
                context.Fill(new Rect(content.x, sliderY, content.width, 2f * unit), palette.MiniTrack);
                float amount = Mathf.InverseLerp(0.8f, 1.3f, settings.Scale);
                context.Fill(new Rect(content.x, sliderY, content.width * amount, 2f * unit), palette.Selected);
                context.Fill(new Rect(content.x + content.width * amount - 2f * unit, sliderY - 3f * unit, 4f * unit, 8f * unit), palette.Selected);
                UpdatePreviewValues();
                context.FontSize = 11f * s;
                float yFacts = sample.yMax + 24f * s;
                context.Fill(new Rect(rect.x, yFacts, rect.width, 1f), palette.StrongLine);
                yFacts += 16f * s;
                DrawFact(rect.x, yFacts, rect.width, scaleLabel, previewScale);
                DrawFact(rect.x, yFacts + 26f * s, rect.width, opacityLabel, previewOpacity);
                DrawFact(rect.x, yFacts + 52f * s, rect.width, densityLabel, settings.Density == 0 ? compact : settings.Density == 2 ? relaxed : standard);
                DrawFact(rect.x, yFacts + 78f * s, rect.width, motionLabel, settings.ReducedMotion ? offLabel : onLabel);
                context.FontSize = oldSize;
            }

            private void DrawMiniRow(Rect content, float offset, bool on, UiPalette palette, float unit)
            {
                float y = content.y + offset;
                context.Fill(new Rect(content.x, y + 4f * unit, Mathf.Min(28f * unit, content.width * 0.35f), 3f * unit), palette.MiniMark);
                Rect track = new Rect(content.xMax - 20f * unit, y, 20f * unit, 10f * unit);
                context.Fill(track, on ? palette.SwitchOn : palette.MiniTrack, 5f * unit);
                context.Fill(new Rect(on ? track.xMax - 8f * unit : track.x + 2f * unit, y + 2f * unit, 6f * unit, 6f * unit),
                    on ? palette.Signal : palette.Surface, 3f * unit);
            }

            private void DrawPreviewScene(Rect sample, UiPalette palette, float scale)
            {
                using (context.Clip(sample))
                {
                    Rect local = new Rect(0f, 0f, sample.width, sample.height);
                    context.Fill(local, palette.Preview);
                    float extent = Mathf.Max(local.width, local.height) * 1.6f;
                    Vector2 center = local.center;
                    float angle = -20f * Mathf.Deg2Rad;
                    float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                    float spacing = 32f * scale;
                    for (float offset = -extent; offset <= extent; offset += spacing)
                    {
                        context.Line(center + Rotate(new Vector2(offset, -extent), cosine, sine), center + Rotate(new Vector2(offset, extent), cosine, sine), palette.SoftLine);
                        context.Line(center + Rotate(new Vector2(-extent, offset), cosine, sine), center + Rotate(new Vector2(extent, offset), cosine, sine), palette.SoftLine);
                    }
                    angle = 30f * Mathf.Deg2Rad;
                    cosine = Mathf.Cos(angle); sine = Mathf.Sin(angle);
                    float halfBand = local.height * 0.175f;
                    Vector2 a = center + Rotate(new Vector2(-extent, -halfBand), cosine, sine);
                    Vector2 b = center + Rotate(new Vector2(extent, -halfBand), cosine, sine);
                    Vector2 c = center + Rotate(new Vector2(extent, halfBand), cosine, sine);
                    Vector2 d = center + Rotate(new Vector2(-extent, halfBand), cosine, sine);
                    context.Triangle(a, b, c, palette.PreviewShade);
                    context.Triangle(a, c, d, palette.PreviewShade);
                    float inset = 8f * scale, length = 8f * scale;
                    context.Line(new Vector2(inset, inset + length), new Vector2(inset, inset), palette.PreviewEdge);
                    context.Line(new Vector2(inset, inset), new Vector2(inset + length, inset), palette.PreviewEdge);
                    context.Line(new Vector2(local.width - inset - length, local.height - inset), new Vector2(local.width - inset, local.height - inset), palette.PreviewEdge);
                    context.Line(new Vector2(local.width - inset, local.height - inset), new Vector2(local.width - inset, local.height - inset - length), palette.PreviewEdge);
                }
            }

            private static Vector2 Rotate(Vector2 point, float cosine, float sine) =>
                new Vector2(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine);

            private void UpdatePreviewValues()
            {
                int value = Mathf.RoundToInt(settings.Scale * 100f);
                if (value != previewScaleValue) { previewScaleValue = value; previewScale = value.ToString() + "%"; }
                value = Mathf.RoundToInt(settings.PanelOpacity * 100f);
                if (value != previewOpacityValue) { previewOpacityValue = value; previewOpacity = value.ToString() + "%"; }
            }

            private void DrawFact(float x, float y, float width, string label, string value)
            {
                context.Label(new Rect(x, y, width * 0.64f, 28f * settings.Scale), label, context.Palette.Muted);
                context.Label(new Rect(x + width * 0.64f, y, width * 0.36f, 28f * settings.Scale), value, context.Palette.Ink, TextAnchor.MiddleRight);
            }

            public override void Dispose()
            {
                for (int i = 0; i < rows.Length; i++) rows[i].Dispose();
                context.Release(scroll.Id);
                for (int i = 0; i < scroll.AnchorCount; i++) context.Release(scroll.AnchorId(i));
            }
        }
    }
}
