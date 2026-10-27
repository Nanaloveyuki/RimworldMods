using System;
using System.Globalization;
using UnityEngine;
using Verse;

namespace IrisMenus.UI
{
    /// <summary>Retained opaque RGB editor. Prepare before background input, then DrawOverlay at the surface root.</summary>
    public sealed class UiColorPicker : IDisposable
    {
        private readonly UiContext context;
        private readonly UiDialog dialog;
        private readonly UiSlider[] sliders;
        private readonly UiTextField[] fields;
        private readonly UiId[] swatches;
        private readonly string[] buffers = new string[3];
        private readonly bool[] invalid = new bool[3];
        private readonly UiButton apply, cancel;
        private readonly Action<Rect> drawContent;
        private Action<Color> onApply;
        private Color original, draft;
        private float scale = 1f;
        private bool open;
        private static readonly string[] channels = { "R", "G", "B" };
        private static readonly Color[] presets =
        {
            new Color(0.60f, 0.62f, 0.65f), new Color(0.29f, 0.72f, 0.43f),
            new Color(0.25f, 0.56f, 0.90f), new Color(0.66f, 0.40f, 0.86f),
            new Color(0.91f, 0.69f, 0.24f), new Color(0.90f, 0.35f, 0.31f),
            new Color(0.24f, 0.74f, 0.76f), new Color(0.91f, 0.48f, 0.70f),
            new Color(0.92f, 0.92f, 0.92f), new Color(0.12f, 0.13f, 0.15f)
        };

        public UiColorPicker(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            dialog = new UiDialog(context);
            sliders = new UiSlider[3];
            fields = new UiTextField[3];
            for (int i = 0; i < 3; i++)
            {
                sliders[i] = new UiSlider(context, 0f, 255f, 1f);
                fields[i] = new UiTextField(context, 3);
            }
            swatches = new UiId[presets.Length];
            for (int i = 0; i < swatches.Length; i++) swatches[i] = context.CreateId();
            apply = new UiButton(context);
            cancel = new UiButton(context);
            drawContent = DrawContent;
        }

        public string Title { get; set; } = "IM_ValueChooseColor".Translate();
        public string ApplyLabel { get; set; } = "IM_ValueApply".Translate();
        public string CancelLabel { get; set; } = "CancelButton".Translate();
        public string OriginalLabel { get; set; } = "IM_ValueOriginal".Translate();
        public string PreviewLabel { get; set; } = "IM_ValuePreview".Translate();
        public bool IsOpen => open;
        public bool IsVisible => dialog.IsVisible;
        public Color Preview => draft;

        public void Open(Color color, Action<Color> applyColor)
        {
            onApply = applyColor ?? throw new ArgumentNullException(nameof(applyColor));
            original = draft = new Color(Channel(color.r), Channel(color.g), Channel(color.b), 1f);
            RefreshBuffers();
            open = true;
            dialog.Prepare(true);
        }

        public void Cancel()
        {
            open = false;
            onApply = null;
        }

        public void Prepare() => dialog.Prepare(open);

        public void DrawOverlay(Rect viewport, float scale = 1f)
        {
            this.scale = Mathf.Max(0.1f, Mathf.Min(scale, Mathf.Min(Mathf.Max(1f, viewport.width - 72f) / 428f, Mathf.Max(1f, viewport.height - 136f) / 334f)));
            if (dialog.Draw(viewport, new Vector2(428f * this.scale + 72f, 334f * this.scale + 136f), Title, drawContent, closeOnScrim: true)) Cancel();
        }

        private void RefreshBuffers()
        {
            for (int i = 0; i < 3; i++)
            {
                buffers[i] = Mathf.RoundToInt(GetChannel(i) * 255f).ToString(CultureInfo.InvariantCulture);
                invalid[i] = false;
            }
        }

        private float GetChannel(int index) => index == 0 ? draft.r : index == 1 ? draft.g : draft.b;
        private void SetChannel(int index, float value)
        {
            if (index == 0) draft.r = value;
            else if (index == 1) draft.g = value;
            else draft.b = value;
            draft.a = 1f;
        }

        private void DrawContent(Rect rect)
        {
            float oldSize = context.FontSize;
            context.FontSize = 13f * scale;
            try
            {
                UiPalette palette = context.Palette;
                float gap = 12f * scale;
                float previewWidth = (rect.width - gap) * 0.5f;
                context.Label(new Rect(0f, 0f, previewWidth, 24f * scale), OriginalLabel, palette.Muted);
                context.Label(new Rect(previewWidth + gap, 0f, previewWidth, 24f * scale), PreviewLabel, palette.Muted);
                Rect first = new Rect(0f, 28f * scale, previewWidth, 40f * scale);
                Rect second = new Rect(previewWidth + gap, first.y, previewWidth, first.height);
                context.Fill(first, original);
                context.Fill(second, draft);
                context.Border(first, palette.StrongLine);
                context.Border(second, palette.StrongLine);
                float swatchGap = 5f * scale;
                float swatchWidth = (rect.width - (presets.Length - 1) * swatchGap) / presets.Length;
                for (int i = 0; i < presets.Length; i++)
                {
                    Rect swatch = new Rect(i * (swatchWidth + swatchGap), 82f * scale, swatchWidth, 28f * scale);
                    UiInteraction interaction = context.Interact(swatches[i], swatch, open);
                    context.Fill(swatch, presets[i]);
                    context.Border(swatch, interaction.Focused && context.FocusVisible ? palette.Ink : palette.Line,
                        interaction.Focused && context.FocusVisible ? 2f : 1f);
                    if (interaction.Hovered) context.Border(new Rect(swatch.x + 2f, swatch.y + 2f, swatch.width - 4f, swatch.height - 4f), palette.Ink);
                    if (interaction.Clicked) { draft = presets[i]; RefreshBuffers(); }
                }
                for (int i = 0; i < 3; i++) DrawChannel(rect.width, 124f * scale + i * 48f * scale, i);
                float buttonWidth = (rect.width - gap) * 0.5f;
                float buttonY = Mathf.Max(274f * scale, rect.height - 40f * scale);
                if (cancel.Draw(new Rect(0f, buttonY, buttonWidth, 40f * scale), CancelLabel, scale: scale)) Cancel();
                if (apply.Draw(new Rect(buttonWidth + gap, buttonY, buttonWidth, 40f * scale), ApplyLabel,
                    enabled: !invalid[0] && !invalid[1] && !invalid[2], primary: true, scale: scale))
                {
                    Action<Color> callback = onApply;
                    Color result = draft;
                    Cancel();
                    callback?.Invoke(result);
                }
            }
            finally { context.FontSize = oldSize; }
        }

        private void DrawChannel(float width, float y, int index)
        {
            float fieldWidth = 64f * scale;
            context.Label(new Rect(0f, y, 22f * scale, 36f * scale), channels[index], context.Palette.Ink);
            float before = Mathf.Round(GetChannel(index) * 255f);
            float after = sliders[index].Draw(new Rect(28f * scale, y, Mathf.Max(0f, width - fieldWidth - 44f * scale), 36f * scale), before, scale: scale);
            if (before != after)
            {
                SetChannel(index, after / 255f);
                buffers[index] = Mathf.RoundToInt(after).ToString(CultureInfo.InvariantCulture);
                invalid[index] = false;
            }
            string edited = fields[index].Draw(new Rect(width - fieldWidth, y, fieldWidth, 36f * scale), buffers[index], invalid: invalid[index], scale: scale);
            if (edited == buffers[index]) return;
            buffers[index] = edited;
            invalid[index] = !int.TryParse(edited, NumberStyles.Integer, CultureInfo.InvariantCulture, out int channel) || channel < 0 || channel > 255;
            if (!invalid[index]) SetChannel(index, channel / 255f);
        }
        private static float Channel(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);

        public void Dispose()
        {
            Cancel();
            for (int i = 0; i < 3; i++) { fields[i].Dispose(); context.Release(sliders[i].Id); }
            for (int i = 0; i < swatches.Length; i++) context.Release(swatches[i]);
            context.Release(apply.Id);
            context.Release(cancel.Id);
            context.Release(dialog.Id);
        }
    }
}
