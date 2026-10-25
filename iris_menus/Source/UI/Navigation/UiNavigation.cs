using System;
using UnityEngine;

namespace IrisMenus.UI
{
    // Labels are caller-owned and may be translated in place; identity never comes from text.
    internal sealed class UiNavigation
    {
        private readonly UiContext context;
        private readonly string[] labels;
        private readonly UiId[] ids;
        private readonly UiInteraction[] interactions;
        private readonly UiTween[] hover;
        private readonly UiTween[] press;
        private readonly UiTween[] focus;
        private readonly UiTween[] activation;
        private UiTween indicatorX, indicatorY, indicatorWidth, indicatorHeight, indicatorAlpha;
        private UiTween presence = new UiTween(0f);
        private bool positioned;

        internal UiNavigation(UiContext context, string[] labels)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.labels = labels ?? throw new ArgumentNullException(nameof(labels));
            ids = new UiId[labels.Length];
            interactions = new UiInteraction[labels.Length];
            hover = new UiTween[labels.Length];
            press = new UiTween[labels.Length];
            focus = new UiTween[labels.Length];
            activation = new UiTween[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) throw new ArgumentException("Navigation labels cannot be null.", nameof(labels));
                ids[i] = context.CreateId();
            }
        }

        internal int Count => ids.Length;
        internal UiId ItemId(int index) => ids[index];
        internal float Visibility => presence.Value;

        internal int Draw(Rect rect, int selected, bool vertical, float rowHeight, float gap,
            bool enabled, bool[] itemEnabled, bool visible, UiGlyphKind[] icons = null)
        {
            if (itemEnabled != null && itemEnabled.Length != ids.Length)
                throw new ArgumentException("Enabled flags must match navigation labels.", nameof(itemEnabled));
            if (icons != null && icons.Length != ids.Length)
                throw new ArgumentException("Icons must match navigation labels.", nameof(icons));
            if (float.IsNaN(rowHeight) || float.IsInfinity(rowHeight) || rowHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rowHeight));
            if (float.IsNaN(gap) || float.IsInfinity(gap) || gap < 0f)
                throw new ArgumentOutOfRangeException(nameof(gap));
            float alpha = presence.Step(visible ? 1f : 0f, context.Time,
                visible ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
            if (ids.Length == 0 || rect.width <= 0f || rect.height <= 0f || (!visible && alpha <= 0f)) return selected;
            int active = selected >= 0 && selected < ids.Length ? selected : -1;
            int focused = -1;
            using (context.Input(enabled && visible))
            using (context.Clip(rect))
            {
                Rect local = new Rect(0f, 0f, rect.width, rect.height);
                for (int i = 0; i < ids.Length; i++)
                {
                    bool available = itemEnabled == null || itemEnabled[i];
                    UiInteraction interaction = context.Interact(ids[i], ItemRect(local, i, vertical, rowHeight, gap), available);
                    interactions[i] = interaction;
                    if (interaction.Focused && available) focused = i;
                    if (interaction.Clicked) active = i;
                }
                if (focused >= 0)
                {
                    int next = focused;
                    if (context.ConsumeKey(vertical ? KeyCode.UpArrow : KeyCode.LeftArrow))
                        next = NextAvailable(focused, -1, itemEnabled, local, vertical, rowHeight, gap);
                    else if (context.ConsumeKey(vertical ? KeyCode.DownArrow : KeyCode.RightArrow))
                        next = NextAvailable(focused, 1, itemEnabled, local, vertical, rowHeight, gap);
                    else if (context.ConsumeKey(KeyCode.Home)) next = EdgeAvailable(false, itemEnabled, local, vertical, rowHeight, gap);
                    else if (context.ConsumeKey(KeyCode.End)) next = EdgeAvailable(true, itemEnabled, local, vertical, rowHeight, gap);
                    if (next >= 0 && next != focused)
                    {
                        active = next;
                        context.Focus(ids[next]);
                    }
                }
                using (context.Opacity(alpha))
                {
                    UiPalette palette = context.Palette;
                    if (!vertical)
                        context.Fill(new Rect(0f, Mathf.Max(0f, local.height - 1f), local.width, 1f), palette.StrongLine);
                    if (vertical)
                    {
                        Rect target = active < 0 ? new Rect() : ItemRect(local, active, true, rowHeight, gap);
                        if (active >= 0 && !positioned)
                        {
                            indicatorX.Snap(target.x);
                            indicatorY.Snap(target.y);
                            indicatorWidth.Snap(target.width);
                            indicatorHeight.Snap(target.height);
                            indicatorAlpha.Snap(1f);
                            positioned = true;
                        }
                        float ix = indicatorX.Step(active >= 0 ? target.x : indicatorX.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        float iy = indicatorY.Step(active >= 0 ? target.y : indicatorY.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        float iw = indicatorWidth.Step(active >= 0 ? target.width : indicatorWidth.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        float ih = indicatorHeight.Step(active >= 0 ? target.height : indicatorHeight.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        float ia = indicatorAlpha.Step(active >= 0 ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        using (context.Opacity(ia))
                        {
                            context.Fill(new Rect(ix, iy, iw, ih), palette.Selected);
                            context.Fill(new Rect(ix, iy + (ih - 24f) * 0.5f, 3f, Mathf.Min(24f, ih)), palette.Signal);
                        }
                    }
                    for (int i = 0; i < ids.Length; i++)
                    {
                        Rect item = ItemRect(local, i, vertical, rowHeight, gap);
                        UiInteraction interaction = interactions[i];
                        float h = hover[i].Step(interaction.Hovered ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        float p = press[i].Step(interaction.Pressed ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        float f = focus[i].Step(context.IsFocused(ids[i]) && context.FocusVisible ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        bool available = enabled && (itemEnabled == null || itemEnabled[i]);
                        using (context.Opacity(available ? 1f : 0.42f))
                        {
                            Color fill = palette.Hover;
                            fill.a *= Mathf.Max(h, p);
                            context.Fill(item, fill);
                            float selectedAmount = vertical ? (i == active ? 1f : 0f) : activation[i].Step(i == active ? 1f : 0f,
                                context.Time, UiMotion.Fast, context.ReducedMotion);
                            if (!vertical)
                            {
                                Color selectedFill = palette.Selected;
                                selectedFill.a *= selectedAmount;
                                context.Fill(item, selectedFill);
                            }
                            Color outline = palette.Ink;
                            outline.a *= f;
                            context.Border(new Rect(item.x + 2f, item.y + 2f, Mathf.Max(0f, item.width - 4f), Mathf.Max(0f, item.height - 4f)), outline);
                            Color ink = Color.Lerp(palette.Muted, palette.OnSelected, selectedAmount);
                            float iconWidth = icons != null && icons[i] != UiGlyphKind.None ? 30f : 0f;
                            if (iconWidth > 0f)
                                UiGlyph.Draw(context, new Rect(item.x + 14f, item.center.y - 9f, 18f, 18f), icons[i], ink);
                            Rect text = new Rect(item.x + 14f + iconWidth, item.y,
                                Mathf.Max(0f, item.width - 28f - iconWidth - (vertical ? 0f : selectedAmount * 22f)), item.height);
                            FontStyle oldStyle = context.FontStyle;
                            context.FontStyle = i == active ? FontStyle.Bold : FontStyle.Normal;
                            context.Label(text, labels[i], ink, vertical ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                            context.FontStyle = oldStyle;
                            if (!vertical && selectedAmount > 0f)
                            {
                                using (context.Opacity(selectedAmount))
                                {
                                    Rect arrow = new Rect(item.xMax - 26f, item.center.y - 10f, 20f, 20f);
                                    context.Fill(arrow, palette.OnSelected);
                                    Vector2 center = arrow.center;
                                    context.Triangle(new Vector2(center.x - 2f, center.y - 3f),
                                        new Vector2(center.x + 2f, center.y), new Vector2(center.x - 2f, center.y + 3f), palette.Selected);
                                }
                            }
                        }
                    }
                }
            }
            return active;
        }

        private Rect ItemRect(Rect rect, int index, bool vertical, float rowHeight, float gap)
        {
            if (vertical) return new Rect(0f, index * (rowHeight + gap), rect.width, rowHeight);
            float width = Mathf.Max(0f, (rect.width - gap * (ids.Length - 1)) / ids.Length);
            return new Rect(index * (width + gap), 0f, width, rect.height);
        }

        private int NextAvailable(int start, int direction, bool[] enabled, Rect rect, bool vertical, float rowHeight, float gap)
        {
            for (int step = 1; step <= ids.Length; step++)
            {
                int index = (start + direction * step + ids.Length) % ids.Length;
                if ((enabled == null || enabled[index]) && context.IsRectVisible(ItemRect(rect, index, vertical, rowHeight, gap))) return index;
            }
            return -1;
        }

        private int EdgeAvailable(bool last, bool[] enabled, Rect rect, bool vertical, float rowHeight, float gap)
        {
            for (int step = 0; step < ids.Length; step++)
            {
                int index = last ? ids.Length - 1 - step : step;
                if ((enabled == null || enabled[index]) && context.IsRectVisible(ItemRect(rect, index, vertical, rowHeight, gap))) return index;
            }
            return -1;
        }
    }
}
