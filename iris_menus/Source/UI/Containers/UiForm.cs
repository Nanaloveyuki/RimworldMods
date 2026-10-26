using System;
using System.Globalization;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiField
    {
        private readonly UiContext context;
        private readonly UiTooltip tooltip;
        private readonly UiId id;
        private string oldHint;
        public UiField(UiContext context) { this.context = context ?? throw new ArgumentNullException(nameof(context)); tooltip = new UiTooltip(context); id = context.CreateId(); }
        public UiId Id => id;
        public float Measure(float width, string label, string hint, float controlHeight, float scale = 1f, float controlWidth = 280f)
        {
            float unit = Mathf.Max(0f, scale);
            float labelWidth = width < (controlWidth + 196f) * unit ? width : Mathf.Max(0f, width - Mathf.Min(controlWidth * unit, width * 0.46f) - 16f * unit);
            float labelHeight = UiTextMetrics.Height(context, label, Mathf.Max(1f, labelWidth - (string.IsNullOrEmpty(hint) ? 0f : 20f * unit)));
            float hintHeight = string.IsNullOrEmpty(hint) ? 0f : UiTextMetrics.Height(context, hint, Mathf.Max(1f, width - 24f * unit));
            float control = Mathf.Max(controlHeight * unit, 36f * unit);
            return width < (controlWidth + 196f) * unit ? labelHeight + hintHeight + control + 26f * unit : Mathf.Max(control, labelHeight + hintHeight + 4f * unit) + 16f * unit;
        }
        public void Draw(Rect rect, string label, string hint, Action<Rect> drawControl, bool enabled = true, float scale = 1f, float controlWidth = 280f)
        {
            if (drawControl == null) throw new ArgumentNullException(nameof(drawControl));
            float unit = Mathf.Max(0f, scale), gap = 16f * unit;
            bool stacked = rect.width < (controlWidth + 196f) * unit;
            float labelWidth = stacked ? rect.width : Mathf.Max(0f, rect.width - Mathf.Min(controlWidth * unit, rect.width * 0.46f) - gap);
            float height = Mathf.Min(40f * unit, rect.height);
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, stacked ? Mathf.Max(0f, rect.height - height - 10f * unit) : rect.height);
            Rect control = new Rect(stacked ? rect.x : rect.xMax - Mathf.Min(controlWidth * unit, rect.width), stacked ? rect.yMax - height : rect.center.y - height * 0.5f, stacked ? rect.width : Mathf.Min(controlWidth * unit, rect.width), height);
            context.Label(new Rect(labelRect.x, labelRect.y, Mathf.Max(0f, labelRect.width - (string.IsNullOrEmpty(hint) ? 0f : 18f * unit)), Mathf.Min(labelRect.height, 26f * unit)), label ?? string.Empty, context.Palette.Ink, wrap: true);
            if (!string.IsNullOrEmpty(hint))
            {
                UiGlyph.Draw(context, new Rect(labelRect.xMax - 15f * unit, labelRect.y + 6f * unit, 13f * unit, 13f * unit), UiGlyphKind.Help, context.Palette.Muted);
                if (oldHint != hint) { tooltip.Dismiss(); oldHint = hint; }
                tooltip.Observe(labelRect, hint, id, enabled);
            }
            else tooltip.Observe(labelRect, string.Empty, enabled: false);
            using (context.Input(enabled)) drawControl(control);
        }
        public void DrawTooltipOverlay(Rect bounds, Vector2 size, float scale = 1f) => tooltip.DrawOverlay(bounds, size, scale);
        public void Dispose() => tooltip.Dismiss();
    }

    public sealed class UiSectionTitle
    {
        private readonly UiContext context;
        public UiSectionTitle(UiContext context) { this.context = context ?? throw new ArgumentNullException(nameof(context)); }
        public float Measure(float scale = 1f) => 44f * Mathf.Max(0f, scale);
        public void Draw(Rect rect, string title, float scale = 1f)
        {
            float unit = Mathf.Max(0f, scale), oldSize = context.FontSize;
            FontStyle oldStyle = context.FontStyle;
            context.FontSize = 12f * unit; context.FontStyle = FontStyle.Bold;
            context.Fill(new Rect(rect.x, rect.y + 20f * unit, 3f * unit, 10f * unit), context.Palette.Marker);
            context.Label(new Rect(rect.x + 12f * unit, rect.y + 8f * unit, Mathf.Max(0f, rect.width - 12f * unit), 32f * unit), title ?? string.Empty, context.Palette.Muted);
            context.FontSize = oldSize; context.FontStyle = oldStyle;
        }
    }

    public sealed class UiPanel
    {
        private readonly UiContext context;
        public UiPanel(UiContext context) { this.context = context ?? throw new ArgumentNullException(nameof(context)); }
        public Rect ContentRect(Rect rect, float inset = 16f) { float amount = Mathf.Max(0f, inset); return new Rect(rect.x + amount, rect.y + amount, Mathf.Max(0f, rect.width - amount * 2f), Mathf.Max(0f, rect.height - amount * 2f)); }
        public void Draw(Rect rect, Action<Rect> drawContent, float inset = 16f, Color? fill = null)
        { if (drawContent == null) throw new ArgumentNullException(nameof(drawContent)); context.Fill(rect, fill ?? context.Palette.Sheet); drawContent(ContentRect(rect, inset)); }
    }

    public sealed class UiNumberField : IDisposable
    {
        private readonly UiContext context;
        private readonly UiTextField editor;
        private readonly CultureInfo culture;
        private readonly float minimum, maximum, step;
        private string draft;
        private float committed;
        private bool editing, invalid;
        public UiNumberField(UiContext context, float minimum, float maximum, float step = 0f, CultureInfo culture = null)
        {
            if (float.IsNaN(minimum) || float.IsInfinity(minimum)) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (float.IsNaN(step) || float.IsInfinity(step) || step < 0f) throw new ArgumentOutOfRangeException(nameof(step));
            this.context = context ?? throw new ArgumentNullException(nameof(context)); this.minimum = minimum; this.maximum = maximum; this.step = step;
            this.culture = culture ?? CultureInfo.CurrentCulture; editor = new UiTextField(context);
        }
        public UiId Id => editor.Id;
        public bool Invalid => invalid;
        public float Draw(Rect rect, float value, bool enabled = true, string placeholder = null, float scale = 1f)
        {
            if (!editing || (!context.IsFocused(editor.Id) && !editor.Submitted)) { committed = value; draft = value.ToString("G", culture); editing = true; invalid = false; }
            string next = editor.Draw(rect, draft, placeholder, enabled, invalid, scale: scale);
            if (next != draft) { draft = next; invalid = false; }
            if (editor.Submitted)
            {
                if (float.TryParse(draft, NumberStyles.Float, culture, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed))
                {
                    parsed = Mathf.Clamp(parsed, minimum, maximum);
                    if (step > 0f && parsed < maximum) parsed = Mathf.Clamp((float)(minimum + Math.Round(((double)parsed - minimum) / step, MidpointRounding.AwayFromZero) * step), minimum, maximum);
                    committed = parsed; draft = committed.ToString("G", culture); invalid = false;
                }
                else invalid = true;
            }
            return committed;
        }
        public void Dispose() => editor.Dispose();
    }

    public sealed class UiRadio
    {
        private readonly UiContext context;
        private readonly UiControlVisual visual;
        private readonly UiTween selection;
        public UiRadio(UiContext context) { this.context = context ?? throw new ArgumentNullException(nameof(context)); selection = new UiTween(0f); Id = context.CreateId(); }
        public UiId Id { get; }
        public bool Draw(Rect rect, string label, bool selected, bool enabled = true, float scale = 1f)
        {
            UiInteraction hit = context.Interact(Id, rect, enabled); visual.Step(context, hit, enabled);
            float amount = selection.Step(selected ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion); UiPalette p = context.Palette;
            Color hover = p.Ink; hover.a *= 0.05f * Mathf.Max(visual.Hover, visual.Press); context.Fill(rect, hover);
            float unit = Mathf.Max(0f, scale), radius = 6f * unit; Rect ring = new Rect(rect.x + 14f * unit, rect.center.y - radius, radius * 2f, radius * 2f);
            context.Border(ring, Color.Lerp(p.Line, p.Signal, amount), unit);
            if (amount > 0f) context.Fill(new Rect(ring.center.x - 3f * unit, ring.center.y - 3f * unit, 6f * unit, 6f * unit), p.Signal, 3f * unit);
            context.Label(new Rect(ring.xMax + 10f * unit, rect.y, Mathf.Max(0f, rect.width - 36f * unit), rect.height), label ?? string.Empty, p.Ink);
            visual.DrawFocus(context, rect); return hit.Clicked;
        }
    }

    public sealed class UiRadioGroup
    {
        private readonly UiRadio[] radios;
        public UiRadioGroup(UiContext context, string[] options)
        {
            if (context == null) throw new ArgumentNullException(nameof(context)); if (options == null) throw new ArgumentNullException(nameof(options));
            Options = options; radios = new UiRadio[options.Length]; for (int i = 0; i < radios.Length; i++) radios[i] = new UiRadio(context);
        }
        public string[] Options { get; }
        public float Measure(float width, float rowHeight = 36f, float gap = 4f) => Options.Length * rowHeight + Mathf.Max(0, Options.Length - 1) * gap;
        public int Draw(Rect rect, int selected, bool enabled = true, float rowHeight = 36f, float gap = 4f, float scale = 1f)
        {
            if (radios.Length == 0) return -1; int active = Mathf.Clamp(selected, 0, radios.Length - 1); float y = rect.y;
            for (int i = 0; i < radios.Length; i++) { Rect row = new Rect(rect.x, y, rect.width, Mathf.Min(rowHeight * scale, Mathf.Max(0f, rect.yMax - y))); if (radios[i].Draw(row, Options[i], i == active, enabled, scale)) active = i; y += (rowHeight + gap) * scale; }
            return active;
        }
    }
}
