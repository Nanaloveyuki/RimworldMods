using System;
using System.Collections.Generic;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiToolbarItem
    {
        public UiToolbarItem(string key, string label, UiGlyphKind icon)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("An item key is required.", nameof(key));
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("An accessible label is required.", nameof(label));
            if (icon == UiGlyphKind.None || !Enum.IsDefined(typeof(UiGlyphKind), icon))
                throw new ArgumentOutOfRangeException(nameof(icon));
            Key = key;
            Label = label;
            Icon = icon;
        }

        public string Key { get; }
        public string Label { get; }
        public UiGlyphKind Icon { get; }
    }

    // Retain one instance per surface. Item order and keys are immutable; rebuild for new labels.
    public sealed class UiToolbar
    {
        private readonly UiContext context;
        private readonly UiToolbarItem[] items;
        private readonly UiControlVisual[] visuals;
        private readonly UiTooltip[] tooltips;
        private readonly Vector2[] tooltipSizes;
        private float measuredWidth = -1f;
        private float measuredScale = -1f;
        private float measuredFontSize = -1f;
        private FontStyle measuredFontStyle;
        private int active;
        private int pressed = -1;

        public UiToolbar(UiContext context, IReadOnlyList<UiToolbarItem> items)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (items == null) throw new ArgumentNullException(nameof(items));
            this.items = new UiToolbarItem[items.Count];
            visuals = new UiControlVisual[items.Count];
            tooltips = new UiTooltip[items.Count];
            tooltipSizes = new Vector2[items.Count];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                UiToolbarItem item = items[i];
                if (item == null) throw new ArgumentException("Toolbar items cannot be null.", nameof(items));
                if (!keys.Add(item.Key)) throw new ArgumentException("Toolbar item keys must be unique.", nameof(items));
                this.items[i] = item;
                tooltips[i] = new UiTooltip(context);
            }
            Id = context.CreateId();
        }

        // One stable focus/capture proxy provides one Tab stop, even with many items.
        // The retained active index supplies roving focus without changing UiContext's tab order.
        public UiId Id { get; }
        public int Count => items.Length;

        public float MeasureWidth(float scale = 1f)
            => items.Length == 0 ? 0f : (4f + items.Length * 36f + (items.Length - 1) * 2f) * Mathf.Max(0f, scale);

        public int Draw(Rect rect, float scale = 1f, bool enabled = true)
        {
            float unit = Mathf.Max(0f, scale);
            float width = Mathf.Max(0f, Mathf.Min(rect.width, MeasureWidth(unit)));
            float height = Mathf.Max(0f, Mathf.Min(rect.height, 40f * unit));
            Rect bar = new Rect(rect.x, rect.y + (rect.height - height) * 0.5f, width, height);
            int visible = VisibleCount(width, height, unit);
            bool interactive = enabled && GUI.enabled && context.IsInputAllowed && visible > 0 && context.IsRectVisible(bar);
            if (visible > 0) active = Mathf.Clamp(active, 0, visible - 1);
            if (context.Captured != Id) pressed = -1;
            int clicked = -1;
            UiPalette palette = context.Palette;
            context.Fill(bar, palette.Sunken);

            using (context.Clip(bar))
            {
                int hovered = -1;
                for (int i = 0; i < visible; i++)
                    if (context.Hit(HitRect(i, visible, width, height, unit))) { hovered = i; break; }

                if (interactive && context.IsFocused(Id))
                {
                    int next = active;
                    if (context.ConsumeKey(KeyCode.LeftArrow)) next = (active + visible - 1) % visible;
                    else if (context.ConsumeKey(KeyCode.RightArrow)) next = (active + 1) % visible;
                    else if (context.ConsumeKey(KeyCode.Home)) next = 0;
                    else if (context.ConsumeKey(KeyCode.End)) next = visible - 1;
                    if (next != active) { active = next; context.Focus(Id); }
                }

                Event evt = context.Event;
                if (interactive && hovered >= 0 && context.Captured == default && evt != null
                    && evt.type == EventType.MouseDown && evt.button == 0)
                {
                    active = hovered;
                    pressed = hovered;
                }
                bool keyboardEvent = evt != null && (evt.type == EventType.KeyDown || evt.type == EventType.KeyUp
                    || evt.rawType == EventType.KeyDown || evt.rawType == EventType.KeyUp);
                int target = keyboardEvent ? active : pressed >= 0 ? pressed : hovered >= 0 ? hovered : active;
                Rect hit = visible > 0 ? HitRect(target, visible, width, height, unit) : default;
                UiInteraction interaction = context.Interact(Id, hit, interactive);
                if (interaction.Clicked)
                {
                    clicked = target;
                    GUI.changed = true;
                }
                for (int i = 0; i < items.Length; i++)
                {
                    bool shown = i < visible;
                    bool itemEnabled = interactive && shown;
                    bool itemPressed = itemEnabled && interaction.Pressed && i == (pressed >= 0 ? pressed : active);
                    UiInteraction state = new UiInteraction(itemEnabled && hovered == i, itemPressed,
                        itemEnabled && interaction.Focused && active == i, clicked == i);
                    visuals[i].Step(context, state, enabled && GUI.enabled);
                    if (!shown)
                    {
                        tooltips[i].Dismiss();
                        continue;
                    }
                    Rect button = ButtonRect(i, height, unit);
                    using (context.Opacity(visuals[i].Opacity))
                    {
                        // Normal action buttons use 5% hover / 10% active ink, not toggle inversion.
                        Color background = palette.Ink;
                        background.a *= Mathf.Lerp(0.05f * visuals[i].Hover, 0.1f, visuals[i].Press);
                        context.Fill(button, background);
                        Color ink = Color.Lerp(palette.Muted, palette.Ink, Mathf.Max(visuals[i].Hover, visuals[i].Press));
                        float size = 20f * unit;
                        UiGlyph.Draw(context, new Rect(button.center.x - size * 0.5f,
                            button.center.y - size * 0.5f, size, size), items[i].Icon, ink);
                        visuals[i].DrawFocus(context, button);
                    }
                    tooltips[i].Observe(HitRect(i, visible, width, height, unit), items[i].Label,
                        active == i ? Id : default, itemEnabled);
                }
            }
            if (context.Captured != Id) pressed = -1;
            return clicked;
        }

        public void DrawOverlay(Rect viewport, float scale = 1f)
        {
            float unit = Mathf.Max(0f, scale);
            float width = Mathf.Max(0f, Mathf.Min(viewport.width, 320f * unit));
            if (width != measuredWidth || unit != measuredScale || context.FontSize != measuredFontSize
                || context.FontStyle != measuredFontStyle)
            {
                measuredWidth = width;
                measuredScale = unit;
                measuredFontSize = context.FontSize;
                measuredFontStyle = context.FontStyle;
                for (int i = 0; i < items.Length; i++)
                {
                    float panelWidth = Mathf.Min(width, UiTextMetrics.Width(context, items[i].Label) + 20f * unit);
                    float textWidth = Mathf.Max(0f, panelWidth - 20f * unit);
                    tooltipSizes[i] = new Vector2(panelWidth, UiTextMetrics.Height(context, items[i].Label, textWidth) + 12f * unit);
                }
            }
            for (int i = 0; i < tooltips.Length; i++) tooltips[i].DrawOverlay(viewport, tooltipSizes[i], unit);
        }

        private int VisibleCount(float width, float height, float unit)
        {
            if (unit <= 0f || width <= 2f * unit || height <= 4f * unit) return 0;
            return Mathf.Min(items.Length, Mathf.CeilToInt((width - 2f * unit) / (38f * unit)));
        }

        private static Rect ButtonRect(int index, float height, float unit)
            => new Rect((2f + index * 38f) * unit, 2f * unit, 36f * unit, Mathf.Max(0f, height - 4f * unit));

        private static Rect HitRect(int index, int visible, float width, float height, float unit)
        {
            float left = index == 0 ? 0f : (1f + index * 38f) * unit;
            float right = index == visible - 1 ? width : (39f + index * 38f) * unit;
            return new Rect(left, 0f, Mathf.Max(0f, right - left), height);
        }
    }
}
