using System;
using System.Collections.Generic;
using System.Globalization;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal sealed class ItemValuePalettePage : MenuNativePage
    {
        private readonly UiContext context;
        private readonly MenuSettings settings;
        private readonly List<BandRow> rows = new List<BandRow>();
        private readonly UiScrollView scroll;
        private readonly UiColorPicker picker;
        private readonly UiButton add;
        private readonly Action<Rect> drawContent;
        private readonly MenuChapter[] chapters;
        private readonly MenuNote[] notes;
        private float scale, rowHeight;
        private List<ItemValueBand> rowSource;
        private string language, title, hint, addLabel, thresholdLabel, removeLabel, invalidLabel;

        internal ItemValuePalettePage(UiContext context, IrisMenusMod mod)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            settings = mod.Settings;
            scroll = new UiScrollView(context, 1);
            picker = new UiColorPicker(context);
            add = new UiButton(context);
            drawContent = DrawContent;
            chapters = new[] { new MenuChapter("bands", () => T("IM_ValueColors")) };
            notes = new[] { new MenuNote("bands", () => T("IM_ValueColors"), () => T("IM_ValueColorsHint")) };
            RefreshLanguage();
            SyncRows();
        }

        public override IReadOnlyList<MenuChapter> Chapters => chapters;
        public override IReadOnlyList<MenuNote> Notes => notes;
        public override string ActiveChapterId => "bands";
        public override bool CanReset => true;
        private static string T(string key) => key.Translate().ToString();

        private void RefreshLanguage()
        {
            language = LanguageDatabase.activeLanguage?.folderName;
            title = T("IM_ValueColors");
            hint = T("IM_ValueColorsHint");
            addLabel = T("IM_ValueAddBand");
            thresholdLabel = T("IM_ValueThreshold");
            removeLabel = T("IM_ValueRemoveBand");
            invalidLabel = T("IM_ValueThresholdInvalid");
            SetPickerLabels(picker);
        }

        private static void SetPickerLabels(UiColorPicker picker)
        {
            picker.Title = T("IM_ValueChooseColor");
            picker.ApplyLabel = T("IM_ValueApply");
            picker.CancelLabel = "CancelButton".Translate().ToString();
            picker.OriginalLabel = T("IM_ValueOriginal");
            picker.PreviewLabel = T("IM_ValuePreview");
        }

        private void SyncRows()
        {
            if (rowSource == settings.ItemValueBands && rows.Count == settings.ItemValueBands.Count) return;
            rowSource = settings.ItemValueBands;
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (settings.ItemValueBands.Contains(rows[i].Band)) continue;
                rows[i].Dispose();
                rows.RemoveAt(i);
            }
            for (int i = 0; i < settings.ItemValueBands.Count; i++)
            {
                ItemValueBand band = settings.ItemValueBands[i];
                if (i < rows.Count && rows[i].Band == band) continue;
                rows.Insert(i, new BandRow(context, band));
            }
        }

        public override void Prepare()
        {
            if (language != LanguageDatabase.activeLanguage?.folderName) RefreshLanguage();
            SyncRows();
            picker.Prepare();
        }

        public override void Draw(Rect rect)
        {
            scale = settings.Scale;
            rowHeight = 166f * scale;
            scroll.Draw(rect, (208f + rows.Count * 166f) * scale, drawContent);
        }

        private void DrawContent(Rect rect)
        {
            UiPalette palette = context.Palette;
            float oldSize = context.FontSize;
            try
            {
                context.FontSize = 15f * scale;
                scroll.Anchor(0, new Rect(0f, 0f, rect.width, 112f * scale), focusTarget: add.Id);
                context.Label(new Rect(0f, 0f, rect.width, 32f * scale), title, palette.Ink);
                context.FontSize = 12f * scale;
                context.Label(new Rect(0f, 40f * scale, rect.width, 76f * scale), hint, palette.Muted, wrap: true);
                for (int i = 0; i < rows.Count; i++)
                {
                    Rect row = new Rect(0f, 136f * scale + i * rowHeight, rect.width, rowHeight);
                    if (row.yMax < scroll.VisibleRect.y || row.y > scroll.VisibleRect.yMax) continue;
                    if (DrawBand(row, rows[i], i)) return;
                }
                if (add.Draw(new Rect(0f, 144f * scale + rows.Count * rowHeight, Mathf.Min(rect.width, 240f * scale), 40f * scale), addLabel, scale: scale))
                    ItemValueColors.Add(settings.ItemValueBands);
            }
            finally { context.FontSize = oldSize; }
        }

        private bool DrawBand(Rect rect, BandRow row, int index)
        {
            UiPalette palette = context.Palette;
            float controlsY = rect.y + 42f * scale;
            float swatchWidth = Mathf.Min(220f * scale, rect.width);
            Rect swatch = new Rect(rect.x, rect.y, swatchWidth, 36f * scale);
            UiInteraction interaction = context.Interact(row.Swatch, swatch);
            context.Fill(new Rect(swatch.x, swatch.y + 3f * scale, 30f * scale, 30f * scale), row.Band.Color);
            context.Border(new Rect(swatch.x, swatch.y + 3f * scale, 30f * scale, 30f * scale), palette.StrongLine);
            if (interaction.Focused && context.FocusVisible) context.Border(swatch, palette.Ink, 2f);
            context.Label(new Rect(swatch.x + 40f * scale, swatch.y, Mathf.Max(0f, swatch.width - 40f * scale), swatch.height), row.Range(index, settings.ItemValueBands), palette.Ink);
            if (interaction.Hovered) context.Fill(new Rect(swatch.x, swatch.yMax - scale, swatch.width, scale), palette.Ink);
            if (interaction.Clicked)
            {
                picker.Open(row.Band.Color, row.ApplyColor);
            }
            float start = rect.x;
            float width = Mathf.Max(0f, rect.xMax - start);
            float removeWidth = 84f * scale;
            float fieldWidth = Mathf.Min(142f * scale, Mathf.Max(80f * scale, width - removeWidth - 12f * scale));
            context.Label(new Rect(start, controlsY, Mathf.Max(0f, width - fieldWidth - removeWidth - 20f * scale), 36f * scale), thresholdLabel, palette.Muted);
            Rect field = new Rect(rect.xMax - removeWidth - fieldWidth - 12f * scale, controlsY, fieldWidth, 36f * scale);
            row.Buffer = row.Field.Draw(field, row.Buffer, enabled: index > 0, invalid: row.Invalid, scale: scale);
            if (index > 0)
            {
                row.Invalid = !float.TryParse(row.Buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                    || !ItemValueColors.SetMinimum(settings.ItemValueBands, index, value);
                if (!row.Invalid && row.Buffer != row.LastBuffer) row.UpdateSliderMaximum();
                row.LastBuffer = row.Buffer;
            }
            if (row.Remove.Draw(new Rect(rect.xMax - removeWidth, controlsY, removeWidth, 36f * scale), removeLabel, enabled: index > 0, variant: UiButtonVariant.Ghost, scale: scale))
            {
                settings.ItemValueBands.Remove(row.Band);
                return true;
            }
            if (index > 0)
            {
                double lower = settings.ItemValueBands[index - 1].Minimum;
                double upper = index + 1 < settings.ItemValueBands.Count ? settings.ItemValueBands[index + 1].Minimum
                    : row.SliderMaximum;
                if (index + 1 == settings.ItemValueBands.Count && row.Band.Minimum >= upper && !row.Slider.IsDragging)
                {
                    row.UpdateSliderMaximum();
                    upper = row.SliderMaximum;
                }
                float normalized = upper > lower ? (float)((row.Band.Minimum - lower) / (upper - lower)) : 0f;
                float after = row.Slider.Draw(new Rect(start, controlsY + 44f * scale, width, 32f * scale), normalized, upper > lower, scale);
                if (after != normalized && ItemValueColors.SetMinimum(settings.ItemValueBands, index, (float)(lower + (upper - lower) * after))) row.RefreshBuffer();
            }
            if (row.Invalid) context.Label(new Rect(start, controlsY + 78f * scale, width, 24f * scale), invalidLabel, palette.Error, wrap: true);
            context.Fill(new Rect(rect.x, rect.yMax - 12f * scale, rect.width, scale), palette.SoftLine);
            return false;
        }

        public override void DrawOverlay(Rect viewport) => picker.DrawOverlay(viewport, settings.Scale);
        public override void Focus(string anchor) { if (anchor == "bands") scroll.FocusAnchor(0); }
        public override void Reset()
        {
            picker.Cancel();
            settings.ItemValueBands = ItemValueColors.CreateDefaults();
        }
        public override void Dispose()
        {
            picker.Dispose();
            for (int i = 0; i < rows.Count; i++) rows[i].Dispose();
            context.Release(add.Id);
            context.Release(scroll.Id);
            context.Release(scroll.AnchorId(0));
        }

        private sealed class BandRow : IDisposable
        {
            private readonly UiContext context;
            internal readonly ItemValueBand Band;
            internal readonly UiId Swatch;
            internal readonly UiTextField Field;
            internal readonly UiSlider Slider;
            internal readonly UiButton Remove;
            internal readonly Action<Color> ApplyColor;
            internal string Buffer, LastBuffer;
            internal bool Invalid;
            internal double SliderMaximum;
            private float cachedMin = float.NaN, cachedMax = float.NaN;
            private bool cachedLast;
            private string range;

            internal BandRow(UiContext context, ItemValueBand band)
            {
                this.context = context;
                Band = band;
                Swatch = context.CreateId();
                Field = new UiTextField(context, 32);
                Slider = new UiSlider(context);
                Remove = new UiButton(context);
                ApplyColor = color => Band.Color = ItemValueColors.Opaque(color);
                RefreshBuffer();
                UpdateSliderMaximum();
            }
            internal void UpdateSliderMaximum() => SliderMaximum = Math.Min(float.MaxValue, Math.Max((double)Band.Minimum * 2d, (double)Band.Minimum + 100d));
            internal void RefreshBuffer()
            {
                Buffer = LastBuffer = Band.Minimum.ToString("G9", CultureInfo.InvariantCulture);
                Invalid = false;
            }
            internal string Range(int index, List<ItemValueBand> bands)
            {
                bool last = index + 1 >= bands.Count;
                float maximum = last ? float.PositiveInfinity : bands[index + 1].Minimum;
                if (cachedMin != Band.Minimum || cachedMax != maximum || cachedLast != last)
                {
                    cachedMin = Band.Minimum; cachedMax = maximum; cachedLast = last;
                    range = "[" + Band.Minimum.ToString("G6", CultureInfo.InvariantCulture) + ", "
                        + (last ? "∞" : maximum.ToString("G6", CultureInfo.InvariantCulture)) + ")";
                }
                return range;
            }
            public void Dispose() { Field.Dispose(); context.Release(Swatch); context.Release(Slider.Id); context.Release(Remove.Id); }
        }
    }
}
