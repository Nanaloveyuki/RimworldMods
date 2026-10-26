using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    /// <summary>Opt-in bridge for hosted Listing_Standard pages. Enter with the host's prepared context.</summary>
    public static class MenuItemControls
    {
        [ThreadStatic] private static UiContext current;
        [ThreadStatic] private static List<MenuThingsList> overlays;
        private static readonly ConditionalWeakTable<UiContext, ChoiceCache> choices = new ConditionalWeakTable<UiContext, ChoiceCache>();
        public static bool IsNative => current != null;
        internal static UiContext Context => current;

        public static Scope Enter(UiContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            UiContext previous = current;
            List<MenuThingsList> previousOverlays = overlays;
            current = context;
            overlays = overlayCaches.GetValue(context, CreateOverlayCache);
            overlays.Clear();
            return new Scope(previous, previousOverlays, context);
        }

        private static readonly ConditionalWeakTable<UiContext, List<MenuThingsList>> overlayCaches = new ConditionalWeakTable<UiContext, List<MenuThingsList>>();
        private static List<MenuThingsList> CreateOverlayCache(UiContext _) => new List<MenuThingsList>();
        private static ChoiceCache CreateChoiceCache(UiContext context) => new ChoiceCache(context);
        internal static void Overlay(MenuThingsList list) { if (overlays != null && !overlays.Contains(list)) overlays.Add(list); }
        internal static bool Visible(Rect rect) => MenuScrollView.Active == null || rect.Overlaps(MenuScrollView.Active.VisibleRect);

        private static int DrawNativeChoice(Listing_Standard list, string anchor, string label, int selected, string[] options, bool enabled)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (options == null || options.Length == 0) throw new ArgumentException("Choices are required.", nameof(options));
            MenuControls.Anchor(list, anchor, 36f);
            ChoiceCache cache = choices.GetValue(current, CreateChoiceCache);
            UiChoiceSort control = cache.Get(anchor, options);
            Rect rect = list.GetRect(36f);
            list.Gap(4f);
            return Visible(rect) ? control.Draw(rect, label, selected, enabled) : selected;
        }

        public static void ChoiceSort(Listing_Standard list, string anchor, string label, int selected, string[] options, Action<int> assign, bool enabled = true)
        {
            if (assign == null) throw new ArgumentNullException(nameof(assign));
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (options == null || options.Length == 0) throw new ArgumentException("Choices are required.", nameof(options));
            if (current == null)
            {
                MenuControls.Anchor(list, anchor);
                Rect rect = list.GetRect(30f);
                list.Gap(4f);
                Widgets.Label(new Rect(rect.x, rect.y, rect.width * 0.4f, rect.height), label);
                if (enabled && Visible(rect) && Widgets.ButtonText(new Rect(rect.x + rect.width * 0.4f, rect.y, rect.width * 0.6f, rect.height), options[Mathf.Clamp(selected, 0, options.Length - 1)]))
                {
                    var menu = new List<FloatMenuOption>(options.Length);
                    for (int i = 0; i < options.Length; i++)
                    {
                        int choice = i;
                        menu.Add(new FloatMenuOption(options[i], () => { if (choice != selected) { assign(choice); GUI.changed = true; } }));
                    }
                    Find.WindowStack.Add(new FloatMenu(menu));
                }
                return;
            }
            int next = DrawNativeChoice(list, anchor, label, selected, options, enabled);
            if (next != selected) { assign(next); GUI.changed = true; }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly UiContext previous, context;
            private readonly List<MenuThingsList> previousOverlays;
            internal Scope(UiContext previous, List<MenuThingsList> previousOverlays, UiContext context)
            { this.previous = previous; this.previousOverlays = previousOverlays; this.context = context; }
            public void Dispose()
            {
                try
                {
                    if (overlays != null)
                        for (int i = 0; i < overlays.Count; i++) overlays[i].DrawTooltip(context.Viewport);
                }
                finally { current = previous; overlays = previousOverlays; }
            }
        }

        private sealed class ChoiceCache
        {
            private readonly UiContext context;
            private readonly Dictionary<string, UiChoiceSort> controls = new Dictionary<string, UiChoiceSort>(StringComparer.Ordinal);
            internal ChoiceCache(UiContext context) { this.context = context; }
            internal UiChoiceSort Get(string key, string[] options)
            {
                if (!controls.TryGetValue(key, out UiChoiceSort control))
                { control = new UiChoiceSort(context, options); controls.Add(key, control); }
                else control.SetOptions(options);
                return control;
            }
        }
    }

    /// <summary>Grouped, searchable item list for hosted pages; owns only presentation and stable control identity.</summary>
    public sealed class MenuThingsList
    {
        private readonly string itemPrefix, groupPrefix;
        private readonly ConditionalWeakTable<UiContext, NativeState> native = new ConditionalWeakTable<UiContext, NativeState>();
        private IReadOnlyList<UiThingsGroup> groups = Array.Empty<UiThingsGroup>();
        private readonly List<FilteredGroup> filtered = new List<FilteredGroup>();
        private readonly Dictionary<string, string> itemAnchors = new Dictionary<string, string>(StringComparer.Ordinal);
        private string lastQuery;
        private bool dirty = true;
        private NativeState active;

        public MenuThingsList(string itemAnchorPrefix, string groupAnchorPrefix)
        {
            itemPrefix = itemAnchorPrefix ?? throw new ArgumentNullException(nameof(itemAnchorPrefix));
            groupPrefix = groupAnchorPrefix ?? throw new ArgumentNullException(nameof(groupAnchorPrefix));
        }

        public void SetGroups(IReadOnlyList<UiThingsGroup> snapshot)
        {
            groups = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            dirty = true;
        }

        /// <summary>Uses the registered game categories, retaining direct membership and the native hierarchy.</summary>
        public void SetRegistry(Predicate<ThingDef> include = null)
        {
            var snapshot = new List<UiThingsGroup>();
            IReadOnlyList<ThingCategoryDef> categories = ThingRegistry.Categories;
            for (int i = 0; i < categories.Count; i++)
            {
                ThingCategoryDef category = categories[i];
                var entries = new List<UiThingEntry>();
                IReadOnlyList<ThingDef> defs = ThingRegistry.InCategory(category);
                for (int j = 0; j < defs.Count; j++)
                    if (include == null || include(defs[j])) entries.Add(new UiThingEntry(defs[j], category.defName + "/" + defs[j].defName));
                if (entries.Count > 0) snapshot.Add(new UiThingsGroup(category.defName, CategoryPath(category), entries));
            }
            var uncategorized = new List<UiThingEntry>();
            IReadOnlyList<ThingDef> other = ThingRegistry.Uncategorized;
            for (int i = 0; i < other.Count; i++)
                if (include == null || include(other[i])) uncategorized.Add(new UiThingEntry(other[i]));
            if (uncategorized.Count > 0) snapshot.Add(new UiThingsGroup(string.Empty, "IrisMenus_Items_Uncategorized".Translate(), uncategorized));
            SetGroups(snapshot.AsReadOnly());
        }

        private static string CategoryPath(ThingCategoryDef category)
        {
            string title = category.LabelCap;
            var visited = new HashSet<ThingCategoryDef> { category };
            for (ThingCategoryDef parent = category.parent; parent != null && parent.parent != null && visited.Add(parent); parent = parent.parent)
                title = parent.LabelCap + " / " + title;
            return title;
        }

        public bool Draw(Listing_Standard list, string query, HashSet<string> collapsedGroups, ref string pendingGroup,
            Func<UiThingEntry, bool> selected, Action<UiThingEntry, bool> assign, string emptyLabel = null, bool enabled = true)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (collapsedGroups == null) throw new ArgumentNullException(nameof(collapsedGroups));
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            if (assign == null) throw new ArgumentNullException(nameof(assign));
            Filter(query ?? string.Empty);
            UiContext context = MenuItemControls.Context;
            active = context == null ? null : native.GetValue(context, CreateNative);
            bool searching = !string.IsNullOrEmpty(query);
            bool any = false;
            for (int i = 0; i < filtered.Count; i++)
            {
                FilteredGroup group = filtered[i];
                if (group.Items.Count == 0) continue;
                any = true;
                bool reveal = pendingGroup != null && string.Equals(pendingGroup, group.Source.Key, StringComparison.Ordinal);
                if (reveal) { collapsedGroups.Remove(group.Source.Key); pendingGroup = null; }
                bool open = searching || !collapsedGroups.Contains(group.Source.Key);
                MenuControls.Anchor(list, group.Anchor, 30f);
                Rect header = list.GetRect(30f);
                bool toggle = false;
                if (MenuItemControls.Visible(header))
                {
                    if (active != null)
                    {
                        NativeGroup state = active.Group(group.Source.Key, open);
                        toggle = state.DrawHeader(header, group.Title, open, enabled, searching);
                    }
                    else
                    {
                        Widgets.DrawHighlightIfMouseover(header);
                        Widgets.Label(header, open ? group.OpenTitle : group.ClosedTitle);
                        toggle = enabled && Widgets.ButtonInvisible(header);
                    }
                }
                if (toggle && !searching)
                {
                    if (open) collapsedGroups.Add(group.Source.Key); else collapsedGroups.Remove(group.Source.Key);
                    open = !open;
                }
                if (active != null)
                {
                    NativeGroup state = active.Group(group.Source.Key, open);
                    // Navigation resolves item anchors in this same pass; do not hide its target behind an opening clip.
                    if (reveal) state.Reveal();
                    float stride = UiThingsList.RowHeight + 2f;
                    float height = state.Measure(open, group.Items.Count * stride);
                    Rect content = list.GetRect(height);
                    // Keep anchors in listing coordinates even though drawing below uses a local clip.
                    if (open)
                        for (int j = 0; j < group.Items.Count && j * stride < height; j++)
                            MenuScrollView.Active.Anchor(ItemAnchor(group.Items[j]), new Rect(content.x, content.y + j * stride, content.width,
                                Mathf.Min(UiThingsList.RowHeight, height - j * stride)));
                    if (height <= 0f || !MenuItemControls.Visible(content)) continue;
                    Rect visible = MenuScrollView.Active.VisibleRect;
                    int first = Mathf.Clamp(Mathf.FloorToInt((visible.y - content.y) / stride), 0, group.Items.Count);
                    int last = Mathf.Clamp(Mathf.CeilToInt((Mathf.Min(visible.yMax, content.yMax) - content.y) / stride), first, group.Items.Count);
                    using (context.Input(enabled && open))
                    using (context.Clip(content))
                        for (int j = first; j < last; j++)
                        {
                            UiThingEntry entry = group.Items[j];
                            bool value = selected(entry);
                            bool next = active.List.DrawRow(new Rect(0f, j * stride, content.width, UiThingsList.RowHeight), entry, value, enabled);
                            if (next != value) { assign(entry, next); GUI.changed = true; }
                        }
                }
                else if (open)
                    for (int j = 0; j < group.Items.Count; j++)
                        DrawItem(list, group.Items[j], selected, assign, enabled);
            }
            if (!any && !string.IsNullOrEmpty(emptyLabel)) list.Label(emptyLabel);
            if (active != null) MenuItemControls.Overlay(this);
            return any;
        }

        public bool DrawItem(Listing_Standard list, UiThingEntry entry, bool value, bool enabled = true)
        {
            UiContext context = MenuItemControls.Context;
            active = context == null ? null : native.GetValue(context, CreateNative);
            float height = active == null ? 28f : UiThingsList.RowHeight;
            MenuControls.Anchor(list, ItemAnchor(entry), height);
            Rect rect = list.GetRect(height);
            list.Gap(2f);
            bool previousValue = value;
            if (!MenuItemControls.Visible(rect)) return value;
            if (active != null)
            {
                value = active.List.DrawRow(rect, entry, value, enabled);
                MenuItemControls.Overlay(this);
            }
            else
            {
                bool previous = GUI.enabled;
                GUI.enabled &= enabled;
                try
                {
                    Widgets.CheckboxLabeled(rect, string.IsNullOrEmpty(entry.Description) ? entry.Label : entry.Description, ref value);
                    TooltipHandler.TipRegion(rect, entry.LegacyTooltip);
                }
                finally { GUI.enabled = previous; }
            }
            if (value != previousValue) GUI.changed = true;
            return value;
        }

        private string ItemAnchor(UiThingEntry entry)
        {
            if (!itemAnchors.TryGetValue(entry.Key, out string anchor))
            {
                anchor = itemPrefix + entry.Key;
                itemAnchors.Add(entry.Key, anchor);
            }
            return anchor;
        }

        private void DrawItem(Listing_Standard list, UiThingEntry entry, Func<UiThingEntry, bool> selected, Action<UiThingEntry, bool> assign, bool enabled)
        {
            bool value = selected(entry);
            bool next = DrawItem(list, entry, value, enabled);
            if (next != value) assign(entry, next);
        }

        internal void DrawTooltip(Rect bounds) => active?.List.DrawTooltip(bounds);
        private static NativeState CreateNative(UiContext context) => new NativeState(context);
        private void Filter(string query)
        {
            if (!dirty && lastQuery == query) return;
            dirty = false;
            lastQuery = query;
            filtered.Clear();
            string[] terms = query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < groups.Count; i++)
            {
                UiThingsGroup group = groups[i];
                var match = new FilteredGroup(group);
                for (int j = 0; j < group.Items.Count; j++)
                {
                    UiThingEntry entry = group.Items[j];
                    bool all = true;
                    for (int k = 0; k < terms.Length; k++)
                    {
                        string term = terms[k];
                        if (!Contains(entry.Label, term) && !Contains(entry.Def.defName, term) && !Contains(entry.Description, term)
                            && !Contains(entry.Def.modContentPack?.Name, term)) { all = false; break; }
                    }
                    if (all) match.Items.Add(entry);
                }
                string title = group.Title + " (" + match.Items.Count + ")";
                match.Title = title;
                match.OpenTitle = "−  " + title;
                match.ClosedTitle = "+  " + title;
                match.Anchor = groupPrefix + group.Key;
                filtered.Add(match);
            }
        }
        private static bool Contains(string text, string term) => !string.IsNullOrEmpty(text) && text.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) >= 0;
        private sealed class FilteredGroup
        {
            internal readonly UiThingsGroup Source;
            internal readonly List<UiThingEntry> Items = new List<UiThingEntry>();
            internal string Title, OpenTitle, ClosedTitle, Anchor;
            internal FilteredGroup(UiThingsGroup source) { Source = source; }
        }
        private sealed class NativeState
        {
            private readonly UiContext context;
            private readonly Dictionary<string, NativeGroup> headers = new Dictionary<string, NativeGroup>(StringComparer.Ordinal);
            internal readonly UiThingsList List;
            internal NativeState(UiContext context) { this.context = context; List = new UiThingsList(context); }
            internal NativeGroup Group(string key, bool open)
            {
                if (!headers.TryGetValue(key, out NativeGroup group)) { group = new NativeGroup(context, open); headers.Add(key, group); }
                return group;
            }
        }

        private sealed class NativeGroup
        {
            private readonly UiContext context;
            private readonly UiButton button;
            private UiTween expansion, marker;

            internal NativeGroup(UiContext context, bool open)
            {
                this.context = context;
                button = new UiButton(context);
                expansion = new UiTween(open ? 1f : 0f);
                marker = new UiTween(open ? 1f : 0f);
            }

            internal void Reveal() { expansion.Snap(1f); marker.Snap(1f); }

            internal float Measure(bool open, float naturalHeight) => naturalHeight *
                expansion.Step(open ? 1f : 0f, context.Time, UiMotion.Enter, context.ReducedMotion);

            internal bool DrawHeader(Rect rect, string title, bool open, bool enabled, bool forcedOpen)
            {
                UiInteraction interaction = context.Interact(button.Id, rect, enabled && !forcedOpen);
                bool toggle = interaction.Clicked;
                if (interaction.Focused && enabled && !forcedOpen)
                {
                    if (context.ConsumeKey(KeyCode.LeftArrow)) toggle = open;
                    else if (context.ConsumeKey(KeyCode.RightArrow)) toggle = !open;
                }
                bool next = toggle ? !open : open;
                Color foreground = button.DrawVisual(rect, title, interaction, enabled, variant: UiButtonVariant.Ghost, drawContent: false);
                float mark = marker.Step(next ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                using (context.Opacity(enabled ? 1f : 0.42f))
                {
                    Vector2 center = new Vector2(rect.x + 12f, rect.center.y);
                    context.Line(center + new Vector2(-5f, 0f), center + new Vector2(5f, 0f), foreground, 1.5f);
                    float angle = (1f - mark) * Mathf.PI * 0.5f;
                    Vector2 stroke = new Vector2(Mathf.Cos(angle) * 5f, Mathf.Sin(angle) * 5f);
                    Color ink = foreground;
                    ink.a *= 1f - mark;
                    context.Line(center - stroke, center + stroke, ink, 1.5f);
                    context.Label(new Rect(rect.x + 28f, rect.y, Mathf.Max(0f, rect.width - 38f), rect.height), title, foreground);
                }
                return toggle;
            }
        }
    }
}
