using System;
using System.Collections.Generic;
using UnityEngine;

namespace IrisMenus.UI
{
    // Identity belongs to a destination, not its translated label or current pagination slot.
    internal sealed class UiNavigation
    {
        private sealed class Item
        {
            internal readonly UiId Id;
            internal UiInteraction Interaction;
            internal UiTween Hover, Press, Focus, Activation;
            internal Item(UiContext context) { Id = context.CreateId(); }
        }

        private readonly UiContext context;
        private string[] labels;
        private Item[] items;
        private string[] keys;
        private UiTween indicatorX, indicatorY, indicatorWidth, indicatorHeight, indicatorAlpha;
        private UiTween presence = new UiTween(0f);
        private bool positioned;
        private string[] railLabels, measuredRailLabels;
        private float railTextWidth = -1f, railFont = -1f;

        internal UiNavigation(UiContext context, string[] labels, string[] keys = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            SetItems(labels, keys);
        }

        internal void SetItems(string[] nextLabels, string[] nextKeys = null)
        {
            if (nextLabels == null) throw new ArgumentNullException(nameof(nextLabels));
            if (nextKeys != null && nextKeys.Length != nextLabels.Length)
                throw new ArgumentException("Keys must match navigation labels.", nameof(nextKeys));
            var previous = new Dictionary<string, Item>(StringComparer.Ordinal);
            if (keys != null) for (int i = 0; i < keys.Length; i++) previous.Add(keys[i], items[i]);
            var nextItems = new Item[nextLabels.Length];
            var identities = new string[nextLabels.Length];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < nextLabels.Length; i++)
            {
                if (nextLabels[i] == null) throw new ArgumentException("Navigation labels cannot be null.", nameof(nextLabels));
                string key = nextKeys == null ? i.ToString(System.Globalization.CultureInfo.InvariantCulture) : nextKeys[i];
                if (key == null || !unique.Add(key)) throw new ArgumentException("Navigation keys must be non-null and unique.", nameof(nextKeys));
                identities[i] = key;
                nextItems[i] = previous.TryGetValue(key, out Item item) ? item : new Item(context);
            }
            labels = nextLabels; keys = identities; items = nextItems;
        }

        internal int Count => items.Length;
        internal UiId ItemId(int index) => items[index].Id;
        internal float Visibility => presence.Value;

        internal int HandleKeys(int selected, bool vertical, bool enabled, bool[] itemEnabled,
            Rect rect = default(Rect), float rowHeight = 44f, float gap = 4f, Rect[] itemRects = null)
        {
            if (!enabled || !context.IsInputAllowed) return selected;
            int focused = -1;
            for (int i = 0; i < items.Length; i++)
                if (context.IsFocused(items[i].Id) && (itemEnabled == null || itemEnabled[i])) { focused = i; break; }
            if (focused < 0) return selected;
            int next = focused;
            if (context.ConsumeKey(vertical ? KeyCode.UpArrow : KeyCode.LeftArrow))
                next = NextAvailable(focused, -1, itemEnabled, rect, vertical, rowHeight, gap, itemRects);
            else if (context.ConsumeKey(vertical ? KeyCode.DownArrow : KeyCode.RightArrow))
                next = NextAvailable(focused, 1, itemEnabled, rect, vertical, rowHeight, gap, itemRects);
            else if (context.ConsumeKey(KeyCode.Home)) next = EdgeAvailable(false, itemEnabled, rect, vertical, rowHeight, gap, itemRects);
            else if (context.ConsumeKey(KeyCode.End)) next = EdgeAvailable(true, itemEnabled, rect, vertical, rowHeight, gap, itemRects);
            if (next >= 0 && next != focused) { context.Focus(items[next].Id); return next; }
            return selected;
        }

        internal int Draw(Rect rect, int selected, bool vertical, float rowHeight, float gap,
            bool enabled, bool[] itemEnabled, bool visible, Texture2D[] icons = null,
            Rect[] itemRects = null, string[] displayLabels = null, int first = 0, int end = -1,
            float scale = 1f, bool handleKeys = true)
        {
            if (itemEnabled != null && itemEnabled.Length != items.Length)
                throw new ArgumentException("Enabled flags must match navigation labels.", nameof(itemEnabled));
            if (icons != null && icons.Length != items.Length)
                throw new ArgumentException("Icons must match navigation labels.", nameof(icons));
            if (float.IsNaN(rowHeight) || float.IsInfinity(rowHeight) || rowHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rowHeight));
            if (float.IsNaN(gap) || float.IsInfinity(gap) || gap < 0f)
                throw new ArgumentOutOfRangeException(nameof(gap));
            float alpha = presence.Step(visible ? 1f : 0f, context.Time,
                visible ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
            if (items.Length == 0 || rect.width <= 0f || rect.height <= 0f || (!visible && alpha <= 0f)) return selected;
            if (end < 0) end = items.Length;
            int active = selected >= 0 && selected < items.Length ? selected : -1;
            using (context.Input(enabled && visible))
            using (context.Clip(rect))
            {
                Rect local = new Rect(0f, 0f, rect.width, rect.height);
                if (vertical) MeasureRailLabels(Mathf.Max(0f, rect.width - 28f * scale - (icons != null ? 30f : 0f)));
                if (handleKeys) active = HandleKeys(active, vertical, enabled && visible, itemEnabled, local, rowHeight, gap, itemRects);
                for (int i = first; i < end; i++)
                {
                    Rect itemRect = itemRects == null ? ItemRect(local, i, vertical, rowHeight, gap) : itemRects[i];
                    UiInteraction interaction = context.Interact(items[i].Id, itemRect, itemEnabled == null || itemEnabled[i]);
                    items[i].Interaction = interaction;
                    if (interaction.Clicked) active = i;
                }
                using (context.Opacity(alpha))
                {
                    UiPalette palette = context.Palette;
                    if (!vertical)
                        context.Fill(new Rect(0f, Mathf.Max(0f, local.height - scale), local.width, scale), palette.StrongLine);
                    Rect indicator = default(Rect);
                    float indicatorOpacity = 0f;
                    if (vertical)
                    {
                        Rect target = active < 0 ? new Rect() : itemRects == null ? ItemRect(local, active, true, rowHeight, gap) : itemRects[active];
                        if (active >= 0 && !positioned)
                        {
                            indicatorX.Snap(target.x); indicatorY.Snap(target.y);
                            indicatorWidth.Snap(target.width); indicatorHeight.Snap(target.height);
                            indicatorAlpha.Snap(1f); positioned = true;
                        }
                        float ix = indicatorX.Step(active >= 0 ? target.x : indicatorX.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        float iy = indicatorY.Step(active >= 0 ? target.y : indicatorY.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        float iw = indicatorWidth.Step(active >= 0 ? target.width : indicatorWidth.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        float ih = indicatorHeight.Step(active >= 0 ? target.height : indicatorHeight.Target, context.Time, UiMotion.Standard, context.ReducedMotion);
                        indicatorOpacity = indicatorAlpha.Step(active >= 0 ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        indicator = new Rect(ix, iy, iw, ih);
                        using (context.Opacity(indicatorOpacity))
                        {
                            context.Fill(indicator, palette.Selected);
                            context.Fill(new Rect(ix, iy + (ih - 24f) * 0.5f, 3f, Mathf.Min(24f, ih)), palette.Signal);
                        }
                    }
                    // Step every destination, including off-page tabs, so reversals cannot resurrect stale ink.
                    for (int i = 0; i < items.Length; i++)
                    {
                        Item state = items[i];
                        bool drawn = i >= first && i < end;
                        float h = state.Hover.Step(drawn && state.Interaction.Hovered ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        float p = state.Press.Step(drawn && state.Interaction.Pressed ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        float f = state.Focus.Step(drawn && context.IsFocused(state.Id) && context.FocusVisible ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                        float selectedAmount = state.Activation.Step(i == active ? 1f : 0f, context.Time, UiMotion.Enter, context.ReducedMotion);
                        if (!drawn) continue;
                        Rect item = itemRects == null ? ItemRect(local, i, vertical, rowHeight, gap) : itemRects[i];
                        bool available = enabled && (itemEnabled == null || itemEnabled[i]);
                        using (context.Opacity(available ? 1f : 0.42f))
                        {
                            float coverage = vertical ? Mathf.Clamp01((Mathf.Min(item.yMax, indicator.yMax) - Mathf.Max(item.y, indicator.y)) / item.height) * indicatorOpacity : selectedAmount;
                            Color fill = palette.Hover;
                            fill.a *= Mathf.Max(h, p) * (1f - coverage);
                            context.Fill(item, fill);
                            if (!vertical)
                            {
                                Color selectedFill = palette.Selected; selectedFill.a *= selectedAmount;
                                context.Fill(item, selectedFill);
                            }
                            Color outline = palette.Ink; outline.a *= f;
                            context.Border(new Rect(item.x + 2f, item.y + 2f, Mathf.Max(0f, item.width - 4f), Mathf.Max(0f, item.height - 4f)), outline);
                            Color restingInk = vertical ? Color.Lerp(palette.Muted, palette.Ink, Mathf.Max(h, p)) : palette.Ink;
                            float iconWidth = icons != null ? 30f : 0f;
                            if (icons != null && icons[i] != null && context.IsRepaint)
                            {
                                Color oldColor = GUI.color;
                                GUI.color = new Color(1f, 1f, 1f, context.Alpha);
                                try { GUI.DrawTexture(new Rect(item.x + 14f, item.center.y - 10f, 20f, 20f), icons[i], ScaleMode.ScaleToFit); }
                                finally { GUI.color = oldColor; }
                            }
                            Rect text = new Rect(item.x + 14f * scale + iconWidth, item.y,
                                Mathf.Max(0f, item.width - 28f * scale - iconWidth - (vertical ? 0f : 30f * scale)), item.height);
                            if (!vertical) text.x += 15f * scale - selectedAmount * 12f * scale;
                            string label = vertical ? railLabels[i] : displayLabels == null ? labels[i] : displayLabels[i];
                            TextAnchor anchor = vertical ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                            FontStyle oldStyle = context.FontStyle;
                            context.FontStyle = FontStyle.Normal;
                            context.Label(text, label, vertical ? restingInk : Color.Lerp(restingInk, palette.OnSelected, selectedAmount), anchor);
                            if (vertical && indicatorOpacity > 0f)
                            {
                                Rect overlap = Intersection(item, indicator);
                                if (overlap.width > 0f && overlap.height > 0f)
                                    using (context.Clip(overlap))
                                        context.Label(new Rect(text.x - overlap.x, text.y - overlap.y, text.width, text.height), label,
                                            Color.Lerp(restingInk, palette.OnSelected, indicatorOpacity), anchor);
                            }
                            context.FontStyle = oldStyle;
                            if (!vertical && selectedAmount > 0f)
                            {
                                using (context.Opacity(selectedAmount))
                                {
                                    Rect arrow = new Rect(item.xMax - 32f * scale, item.center.y - 12f * scale, 24f * scale, 24f * scale);
                                    context.Fill(arrow, palette.Raised);
                                    Vector2 center = arrow.center;
                                    context.Triangle(new Vector2(center.x - 2.5f * scale, center.y - 4f * scale),
                                        new Vector2(center.x + 3.5f * scale, center.y), new Vector2(center.x - 2.5f * scale, center.y + 4f * scale), palette.Ink);
                                }
                            }
                        }
                    }
                }
            }
            return active;
        }

        private void MeasureRailLabels(float width)
        {
            bool changed = railLabels == null || railLabels.Length != labels.Length || railTextWidth != width || railFont != context.FontSize;
            if (!changed)
                for (int i = 0; i < labels.Length; i++)
                    if (!string.Equals(measuredRailLabels[i], labels[i], StringComparison.Ordinal)) { changed = true; break; }
            if (!changed) return;
            if (railLabels == null || railLabels.Length != labels.Length)
            { railLabels = new string[labels.Length]; measuredRailLabels = new string[labels.Length]; }
            railTextWidth = width; railFont = context.FontSize;
            for (int i = 0; i < labels.Length; i++)
            {
                measuredRailLabels[i] = labels[i];
                railLabels[i] = UiTextMetrics.Ellipsize(context, labels[i], width);
            }
        }

        private Rect ItemRect(Rect rect, int index, bool vertical, float rowHeight, float gap)
        {
            if (vertical) return new Rect(0f, index * (rowHeight + gap), rect.width, rowHeight);
            float width = Mathf.Max(0f, (rect.width - gap * (items.Length - 1)) / items.Length);
            return new Rect(index * (width + gap), 0f, width, rect.height);
        }

        private int NextAvailable(int start, int direction, bool[] enabled, Rect rect, bool vertical, float rowHeight, float gap, Rect[] itemRects)
        {
            for (int step = 1; step <= items.Length; step++)
            {
                int index = (start + direction * step + items.Length) % items.Length;
                if ((enabled == null || enabled[index]) && (!vertical || context.IsRectVisible(itemRects == null ? ItemRect(rect, index, true, rowHeight, gap) : itemRects[index]))) return index;
            }
            return -1;
        }

        private int EdgeAvailable(bool last, bool[] enabled, Rect rect, bool vertical, float rowHeight, float gap, Rect[] itemRects)
        {
            for (int step = 0; step < items.Length; step++)
            {
                int index = last ? items.Length - 1 - step : step;
                if ((enabled == null || enabled[index]) && (!vertical || context.IsRectVisible(itemRects == null ? ItemRect(rect, index, true, rowHeight, gap) : itemRects[index]))) return index;
            }
            return -1;
        }

        private static Rect Intersection(Rect a, Rect b)
        {
            float x = Mathf.Max(a.x, b.x), y = Mathf.Max(a.y, b.y);
            return new Rect(x, y, Mathf.Max(0f, Mathf.Min(a.xMax, b.xMax) - x), Mathf.Max(0f, Mathf.Min(a.yMax, b.yMax) - y));
        }
    }
}
