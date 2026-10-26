using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiTabs
    {
        private readonly UiContext context;
        private readonly UiNavigation navigation;
        private readonly UiButton previous, next;
        private readonly UiTooltip tooltip;
        private string[] labels;
        private string[] measuredLabels, displayLabels;
        private float[] widths;
        private Rect[] itemRects;
        private readonly List<int> pageStarts = new List<int>();
        private readonly List<int> pageEnds = new List<int>();
        private float measuredWidth = -1f, measuredFont = -1f, measuredScale = -1f;
        private FontStyle measuredStyle;
        private bool invalid = true;
        private int page, lastSelected = -1;
        private float stripWidth;
        private bool separatePager;
        private bool selectionLayoutPending;
        private string pageLabel = string.Empty;
        private int labelPage = -1, labelCount = -1;
        private string tooltipText = string.Empty, measuredTooltip = string.Empty;
        private float tooltipWidth, tooltipHeight, tooltipScale;

        public UiTabs(UiContext context, string[] labels)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.labels = labels ?? throw new ArgumentNullException(nameof(labels));
            navigation = new UiNavigation(context, labels);
            previous = new UiButton(context); next = new UiButton(context);
            tooltip = new UiTooltip(context);
        }

        public int Count => navigation.Count;
        public float Visibility => navigation.Visibility;
        public UiId ItemId(int index) => navigation.ItemId(index);

        // Catalog refreshes reuse destination identities and their in-flight animation curves.
        internal void SetItems(string[] nextLabels, string[] keys)
        {
            navigation.SetItems(nextLabels, keys);
            labels = nextLabels;
            invalid = true;
        }

        internal bool PreparePagination(float width, float scale)
        {
            selectionLayoutPending |= EnsureLayout(width, scale, true);
            return pageStarts.Count > 1;
        }

        public int Draw(Rect rect, int selected, bool enabled = true, bool[] itemEnabled = null, bool visible = true, float scale = 1f,
            Rect? paginationRect = null)
        {
            if (itemEnabled != null && itemEnabled.Length != Count)
                throw new ArgumentException("Enabled flags must match navigation labels.", nameof(itemEnabled));
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) throw new ArgumentOutOfRangeException(nameof(scale));
            bool layoutChanged = EnsureLayout(rect.width, scale, paginationRect.HasValue) || selectionLayoutPending;
            selectionLayoutPending = false;
            int active;
            using (context.Input(enabled && visible)) active = navigation.HandleKeys(selected, false, enabled && visible, itemEnabled);
            if (layoutChanged || active != lastSelected) ShowSelection(active);
            lastSelected = active;
            if (Count == 0 || rect.width <= 0f || rect.height <= 0f) return active;
            int start = pageStarts[page], end = pageEnds[page];
            float x = 0f;
            for (int i = start; i < end; i++)
            {
                itemRects[i] = new Rect(x, 0f, Mathf.Min(widths[i], stripWidth), rect.height);
                x += widths[i] + 4f * scale;
            }
            int result = navigation.Draw(new Rect(rect.x, rect.y, stripWidth, rect.height), active, false,
                44f * scale, 4f * scale, enabled, itemEnabled, visible,
                itemRects: itemRects, displayLabels: displayLabels, first: start, end: end, scale: scale, handleKeys: false);
            if (result != active) lastSelected = result;
            if (pageStarts.Count > 1)
            {
                using (context.Input(enabled && visible))
                using (context.Opacity(navigation.Visibility))
                {
                    float gap = 8f * scale;
                    Rect pager = paginationRect ?? new Rect(rect.x + stripWidth + gap, rect.y, Mathf.Max(0f, rect.width - stripWidth - gap), rect.height);
                    context.Fill(pager, context.Palette.Sunken);
                    using (context.Clip(pager))
                        for (float line = -pager.height; line < pager.width; line += 8f * scale)
                        {
                            Color hatch = context.Palette.Line; hatch.a *= 0.55f;
                            context.Line(new Vector2(line, pager.height), new Vector2(line + pager.height, 0f), hatch, scale);
                        }
                    float size = Mathf.Min(32f * scale, pager.height - 8f * scale);
                    Rect back = new Rect(pager.x + 4f * scale, pager.center.y - size * 0.5f, size, size);
                    Rect forward = new Rect(pager.xMax - size - 4f * scale, back.y, size, size);
                    // Upstream Pagination uses raised square chevrons and padded 01 / 12 ink.
                    context.Fill(back, context.Palette.Raised); context.Fill(forward, context.Palette.Raised);
                    if (previous.Draw(back, string.Empty, page > 0, variant: UiButtonVariant.Ghost,
                        icon: UiGlyphKind.ChevronRight, iconRotation: 180f, scale: scale)) ChangePage(page - 1);
                    if (next.Draw(forward, string.Empty, page < pageStarts.Count - 1, variant: UiButtonVariant.Ghost,
                        icon: UiGlyphKind.ChevronRight, scale: scale)) ChangePage(page + 1);
                    if (labelPage != page || labelCount != pageStarts.Count)
                    {
                        int digits = Math.Max(2, pageStarts.Count.ToString(CultureInfo.InvariantCulture).Length);
                        string format = "D" + digits;
                        pageLabel = (page + 1).ToString(format, CultureInfo.InvariantCulture) + " / " + pageStarts.Count.ToString(format, CultureInfo.InvariantCulture);
                        labelPage = page; labelCount = pageStarts.Count;
                    }
                    float oldSize = context.FontSize;
                    context.FontSize = 11f * scale;
                    context.Label(new Rect(back.xMax + 2f * scale, pager.y, Mathf.Max(0f, forward.x - back.xMax - 4f * scale), pager.height),
                        pageLabel, context.Palette.Ink, TextAnchor.MiddleCenter);
                    context.FontSize = oldSize;
                }
            }
            bool observed = false;
            if (visible)
                for (int i = start; i < end; i++)
                {
                    if (string.Equals(labels[i], displayLabels[i], StringComparison.Ordinal)) continue;
                    Rect item = itemRects[i]; item.x += rect.x; item.y += rect.y;
                    if (!context.Hit(item) && !(context.FocusVisible && context.IsFocused(ItemId(i)))) continue;
                    tooltipText = labels[i];
                    tooltip.Observe(item, tooltipText, ItemId(i), enabled && (itemEnabled == null || itemEnabled[i]));
                    observed = true; break;
                }
            if (!observed) tooltip.Observe(rect, string.Empty, enabled: false);
            return result;
        }

        internal void DrawTooltip(Rect bounds, float scale)
        {
            float width = Mathf.Min(420f * scale, bounds.width);
            if (tooltipWidth != width || tooltipScale != scale || !string.Equals(measuredTooltip, tooltipText, StringComparison.Ordinal))
            {
                tooltipWidth = width; tooltipScale = scale; measuredTooltip = tooltipText;
                tooltipHeight = Mathf.Max(30f * scale, UiTextMetrics.Height(context, tooltipText, Mathf.Max(1f, width - 20f * scale)) + 12f * scale);
            }
            tooltip.DrawOverlay(bounds, new Vector2(width, tooltipHeight), scale);
        }

        private bool EnsureLayout(float width, float scale, bool externalPager)
        {
            bool changed = invalid || measuredWidth != width || measuredFont != context.FontSize || measuredStyle != context.FontStyle || measuredScale != scale || separatePager != externalPager;
            if (!changed)
                for (int i = 0; i < Count; i++)
                    if (!string.Equals(measuredLabels[i], labels[i], StringComparison.Ordinal)) { changed = true; break; }
            if (!changed) return false;
            measuredWidth = width; measuredFont = context.FontSize; measuredStyle = context.FontStyle; measuredScale = scale;
            separatePager = externalPager;
            invalid = false;
            if (widths == null || widths.Length != Count)
            {
                widths = new float[Count]; itemRects = new Rect[Count];
                measuredLabels = new string[Count]; displayLabels = new string[Count];
            }
            float total = 0f;
            for (int i = 0; i < Count; i++)
            {
                string title = labels[i] ?? string.Empty;
                measuredLabels[i] = title;
                string bounded = BoundLongTitle(title);
                widths[i] = Mathf.Clamp(UiTextMetrics.Width(context, bounded) + 58f * scale, 120f * scale, 280f * scale);
                total += widths[i] + (i == 0 ? 0f : 4f * scale);
                displayLabels[i] = bounded;
            }
            bool paginate = total > width && Count > 1;
            stripWidth = Mathf.Max(0f, width - (paginate && !externalPager ? 168f * scale : 0f));
            // A narrow host still gets one clipped-to-viewport tab, never a dropdown.
            if (paginate && stripWidth < 120f * scale) stripWidth = Mathf.Max(1f, width * 0.5f);
            pageStarts.Clear(); pageEnds.Clear();
            int first = 0; float used = 0f;
            for (int i = 0; i < Count; i++)
            {
                float itemWidth = Mathf.Min(widths[i], stripWidth);
                float required = itemWidth + (i > first ? 4f * scale : 0f);
                if (i > first && used + required > stripWidth)
                {
                    pageStarts.Add(first); pageEnds.Add(i); first = i; used = 0f;
                    required = itemWidth;
                }
                used += required;
                if (UiTextMetrics.Width(context, displayLabels[i]) > Mathf.Max(0f, itemWidth - 58f * scale))
                    displayLabels[i] = UiTextMetrics.Ellipsize(context, labels[i], Mathf.Max(0f, itemWidth - 58f * scale), middle: true);
            }
            if (Count > 0) { pageStarts.Add(first); pageEnds.Add(Count); }
            page = Mathf.Clamp(page, 0, Math.Max(0, pageStarts.Count - 1));
            return true;
        }

        private void ShowSelection(int selected)
        {
            if (selected < 0 || selected >= Count) return;
            for (int i = 0; i < pageStarts.Count; i++)
                if (selected >= pageStarts[i] && selected < pageEnds[i]) { page = i; return; }
        }

        private void ChangePage(int nextPage)
        {
            page = Mathf.Clamp(nextPage, 0, pageStarts.Count - 1);
            tooltip.Dismiss();
        }

        private static string BoundLongTitle(string text)
        {
            int cjk = 0, latin = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char value = text[i];
                if (value >= '\u2e80' && value <= '\u9fff' || value >= '\uac00' && value <= '\ud7af' || value >= '\uf900' && value <= '\ufaff') cjk++;
                else if (value >= 'a' && value <= 'z' || value >= 'A' && value <= 'Z' || value >= '0' && value <= '9') latin++;
            }
            if (cjk < 16 && latin < 24) return text;
            int keep = Math.Min(text.Length - 1, cjk >= 16 ? 12 : 20);
            int head = (keep + 1) / 2, tailStart = text.Length - keep / 2;
            if (head > 0 && char.IsHighSurrogate(text[head - 1])) head--;
            if (tailStart < text.Length && char.IsLowSurrogate(text[tailStart])) tailStart++;
            return text.Substring(0, head) + "..." + text.Substring(tailStart);
        }
    }
}
