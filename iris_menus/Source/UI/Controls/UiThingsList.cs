using System;
using System.Collections.Generic;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiThingsGroup
    {
        private readonly IReadOnlyList<UiThingEntry> items;
        public UiThingsGroup(string key, string title, IEnumerable<UiThingEntry> items)
        {
            Key = key ?? string.Empty;
            Title = title ?? string.Empty;
            if (items == null) throw new ArgumentNullException(nameof(items));
            this.items = new List<UiThingEntry>(items).AsReadOnly();
        }
        public string Key { get; }
        public string Title { get; }
        public IReadOnlyList<UiThingEntry> Items => items;
    }

    /// <summary>Stable keyed item controls; immutable snapshots can be replaced without losing focus identity.</summary>
    public sealed class UiThingsList
    {
        private readonly UiContext context;
        private readonly Dictionary<string, UiItemSlot> slots = new Dictionary<string, UiItemSlot>(StringComparer.Ordinal);
        private readonly UiScrollView scroll;
        private readonly Action<Rect> drawContent;
        private IReadOnlyList<UiThingEntry> items = Array.Empty<UiThingEntry>();
        private Func<UiThingEntry, bool> selected;
        private Action<UiThingEntry, bool> assign;
        private bool enabled;
        private float scale;
        private UiItemSlot activeTooltip;

        public UiThingsList(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            scroll = new UiScrollView(context);
            drawContent = DrawContent;
        }

        public const float RowHeight = 60f;
        public UiScrollView Scroll => scroll;
        public void SetItems(IReadOnlyList<UiThingEntry> snapshot)
        {
            items = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
        public UiId ItemId(UiThingEntry entry) => Slot(entry).Id;

        private UiItemSlot Slot(UiThingEntry entry)
        {
            if (!slots.TryGetValue(entry.Key, out UiItemSlot slot))
            {
                slot = new UiItemSlot(context);
                slots.Add(entry.Key, slot);
            }
            return slot;
        }

        public bool DrawRow(Rect rect, UiThingEntry entry, bool value, bool enabled = true, float scale = 1f, bool readOnly = false)
        {
            UiItemSlot slot = Slot(entry);
            bool next = slot.Draw(rect, entry, value, enabled, true, scale, readOnly);
            if (next != value) GUI.changed = true;
            if (slot.HasTooltip) activeTooltip = slot;
            return next;
        }

        public void Draw(Rect rect, Func<UiThingEntry, bool> isSelected, Action<UiThingEntry, bool> setSelected, bool enabled = true, float scale = 1f)
        {
            selected = isSelected ?? throw new ArgumentNullException(nameof(isSelected));
            assign = setSelected ?? throw new ArgumentNullException(nameof(setSelected));
            this.enabled = enabled;
            this.scale = scale;
            activeTooltip = null;
            scroll.Draw(rect, items.Count * RowHeight * scale, drawContent, enabled);
            DrawTooltip(rect, scale);
        }

        private void DrawContent(Rect rect)
        {
            float height = RowHeight * scale;
            Rect visible = scroll.VisibleRect;
            int first = Mathf.Clamp(Mathf.FloorToInt(visible.y / height), 0, items.Count);
            int last = Mathf.Clamp(Mathf.CeilToInt(visible.yMax / height), first, items.Count);
            for (int i = first; i < last; i++)
            {
                UiThingEntry entry = items[i];
                bool value = selected(entry);
                bool next = DrawRow(new Rect(rect.x, rect.y + i * height, rect.width, height - 2f * scale), entry, value, enabled, scale);
                if (next != value) assign(entry, next);
            }
        }

        public void DrawTooltip(Rect bounds, float scale = 1f)
        {
            activeTooltip?.DrawTooltip(bounds, scale);
            activeTooltip = null;
        }
    }
}
