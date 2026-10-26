using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal sealed class ThingRegistryPage
    {
        private static bool registered;
        private readonly List<UI.UiThingEntry> matched = new List<UI.UiThingEntry>();
        private readonly MenuThingsList items = new MenuThingsList("registry-thing-", "registry-category-");
        private readonly List<CategoryOption> options = new List<CategoryOption>();
        private readonly UI.UiContext loading = new UI.UiContext();
        private readonly UI.UiLoader loader;
        private string query = string.Empty;
        private string cachedQuery;
        private CategoryOption selected;
        private int revision = -1;
        private int categoryCount = -1;
        private object language;
        private bool diagnostics;

        private ThingRegistryPage() { loader = new UI.UiLoader(loading); }

        internal static void Register(IrisMenusMod owner)
        {
            if (registered) return;
            var page = new ThingRegistryPage();
            MenuRegistry.RegisterSubItemListing(owner, "thing-registry", () => T("IM_ThingRegistry"), page.Draw);
            MenuRegistry.RegisterPresentation(owner, "thing-registry", new MenuPresentation(context => new NativePage(context)));
            registered = true;
        }

        private static string T(string key) => key.Translate().ToString();

        private void Draw(Listing_Standard list)
        {
            MenuControls.Section(list, T("IM_ThingRegistry"));
            string status = "IM_RegistryProgress".Translate(ThingRegistry.ProcessedCount, ThingRegistry.TotalCount);
            Rect progress = list.GetRect(44f);
            if (Event.current.type == EventType.Repaint)
            {
                loading.BeginFrame(new Rect(0f, 0f, list.ColumnWidth, Mathf.Max(1f, list.CurHeight)),
                    IrisMenusMod.Instance.Settings.Theme, IrisMenusMod.Instance.Settings.ReducedMotion);
                try { loader.Draw(progress, status, ThingRegistry.Progress); }
                finally { loading.EndFrame(); }
            }
            list.Gap(8f);
            RefreshOptions();
            MenuControls.Select(list, T("IM_RegistryCategories"), selected, options, option => option.Label, option => { selected = option; revision = -1; });
            query = Widgets.TextField(list.GetRect(30f), query);
            TooltipHandler.TipRegion(new Rect(0f, list.CurHeight - 30f, list.ColumnWidth, 30f), T("IM_RegistrySearch"));
            list.Gap(4f);
            if (!ThingRegistry.IsReady) return;
            RefreshMatches();
            list.Label(matched.Count + " / " + ThingRegistry.Things.Count);
            for (int i = 0; i < matched.Count; i++) items.DrawItem(list, matched[i], false, enabled: false);
            list.Gap(8f);
            if (list.ButtonText(T("IM_RegistryErrors") + " (" + ThingRegistry.Errors.Count + ")")) diagnostics = !diagnostics;
            if (diagnostics)
            {
                if (ThingRegistry.Errors.Count == 0) list.Label(T("IM_RegistryNoErrors"));
                else foreach (string error in ThingRegistry.Errors) list.Label(error);
            }
            if (list.ButtonText(T("IM_RegistryDump"))) Dump();
        }

        private void RefreshMatches()
        {
            if (cachedQuery == query && revision == ThingRegistry.Revision) return;
            matched.Clear();
            IReadOnlyList<ThingDef> source = selected.Category != null ? ThingRegistry.InCategory(selected.Category) :
                selected.Uncategorized ? ThingRegistry.Uncategorized : ThingRegistry.Things;
            foreach (ThingDef thing in source)
            {
                if (thing == null) continue;
                if (query.Length == 0 || Contains(thing.label, query) || Contains(thing.defName, query) ||
                    Contains(thing.modContentPack?.Name, query) || Contains(thing.modContentPack?.PackageIdPlayerFacing, query))
                    matched.Add(new UI.UiThingEntry(thing, description: thing.defName + " · " + (thing.modContentPack?.Name ?? "—"),
                        tooltip: thing.modContentPack?.PackageIdPlayerFacing));
            }
            cachedQuery = query;
            revision = ThingRegistry.Revision;
        }

        private void RefreshOptions()
        {
            if (options.Count > 0 && categoryCount == ThingRegistry.Categories.Count && ReferenceEquals(language, LanguageDatabase.activeLanguage)) return;
            ThingCategoryDef previous = selected?.Category;
            bool wasUncategorized = selected?.Uncategorized ?? false;
            options.Clear();
            options.Add(new CategoryOption(null, false, T("IM_RegistryAll")));
            options.Add(new CategoryOption(null, true, T("IM_RegistryUncategorized")));
            foreach (ThingCategoryDef category in ThingRegistry.Categories)
            {
                if (category == null) continue;
                var ancestors = new HashSet<ThingCategoryDef>();
                string path = category.LabelCap;
                ThingCategoryDef parent = category.parent;
                while (parent != null && ancestors.Add(parent)) { path = parent.LabelCap + " / " + path; parent = parent.parent; }
                options.Add(new CategoryOption(category, false, path));
            }
            selected = options.Find(option => ReferenceEquals(option.Category, previous) && option.Uncategorized == wasUncategorized) ?? options[0];
            language = LanguageDatabase.activeLanguage;
            categoryCount = ThingRegistry.Categories.Count;
            revision = -1;
        }

        private static bool Contains(string text, string value) => text != null && text.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0;
        private static void Dump()
        {
            var text = new StringBuilder("[IrisMenus] Thing registry ");
            text.Append(ThingRegistry.ProcessedCount).Append('/').Append(ThingRegistry.TotalCount)
                .Append(" scanned, ").Append(ThingRegistry.Things.Count).AppendLine(" registered");
            foreach (ThingCategoryDef category in ThingRegistry.Categories)
            {
                if (category == null) continue;
                text.Append(category.defName).Append(" [parent=").Append(category.parent?.defName ?? "—").Append("]: ");
                foreach (ThingDef thing in ThingRegistry.InCategory(category)) text.Append(thing.defName).Append(' ');
                text.AppendLine();
            }
            text.Append("Uncategorized: ");
            foreach (ThingDef thing in ThingRegistry.Uncategorized) text.Append(thing.defName).Append(' ');
            foreach (string error in ThingRegistry.Errors) text.AppendLine().Append(error);
            Log.Message(text.ToString());
        }

        private sealed class NativePage : MenuNativePage
        {
            private readonly UI.UiContext context;
            private readonly ThingRegistryPage model = new ThingRegistryPage();
            private readonly UI.UiLoader progress;
            private readonly UI.UiSelect categories;
            private readonly UI.UiTextField search;
            private readonly UI.UiButton diagnostic, dump;
            private readonly UI.UiThingsList rows;
            private readonly UI.UiScrollView scroll;
            private readonly Action<Rect> drawRows;
            private int categoryIndex;
            private int optionCount = -1;
            private object optionLanguage;
            private float scale;
            private string title, categoryLabel, searchHint, countLabel, progressLabel, diagnosticLabel, dumpLabel;
            private int count = -1, processed = -1;

            internal NativePage(UI.UiContext context)
            {
                this.context = context;
                progress = new UI.UiLoader(context);
                categories = new UI.UiSelect(context, Array.Empty<string>());
                search = new UI.UiTextField(context);
                diagnostic = new UI.UiButton(context);
                dump = new UI.UiButton(context);
                rows = new UI.UiThingsList(context);
                scroll = new UI.UiScrollView(context);
                drawRows = DrawRows;
            }

            public override void Prepare()
            {
                model.RefreshOptions();
                if (optionCount != model.options.Count || !ReferenceEquals(optionLanguage, LanguageDatabase.activeLanguage))
                {
                    optionCount = model.options.Count;
                    optionLanguage = LanguageDatabase.activeLanguage;
                    var labels = new string[optionCount];
                    for (int i = 0; i < labels.Length; i++) labels[i] = model.options[i].Label;
                    categories.SetOptions(labels);
                    title = T("IM_ThingRegistry");
                    categoryLabel = T("IM_RegistryCategories");
                    searchHint = T("IM_RegistrySearch");
                    diagnosticLabel = T("IM_RegistryErrors") + " (" + ThingRegistry.Errors.Count + ")";
                    dumpLabel = T("IM_RegistryDump");
                    count = processed = -1;
                }
                categories.Prepare();
            }

            public override void Draw(Rect rect)
            {
                scale = IrisMenusMod.Instance.Settings.NativeScale;
                float oldFont = context.FontSize;
                try
                {
                    context.FontSize = 15f * scale;
                    context.Label(new Rect(0f, 0f, rect.width, 32f * scale), title, context.Palette.Ink);
                    if (processed != ThingRegistry.ProcessedCount)
                    {
                        processed = ThingRegistry.ProcessedCount;
                        progressLabel = "IM_RegistryProgress".Translate(processed, ThingRegistry.TotalCount);
                    }
                    progress.Draw(new Rect(0f, 40f * scale, rect.width, 44f * scale), progressLabel, ThingRegistry.Progress, scale);
                    context.Label(new Rect(0f, 96f * scale, rect.width * 0.3f, 36f * scale), categoryLabel, context.Palette.Muted);
                    ChangeCategory(categories.Draw(new Rect(rect.width * 0.3f, 96f * scale, rect.width * 0.7f, 36f * scale), categoryIndex, scale: scale));
                    model.query = search.Draw(new Rect(0f, 144f * scale, rect.width, 36f * scale), model.query, searchHint, scale: scale);
                    if (!ThingRegistry.IsReady) return;
                    model.RefreshMatches();
                    if (count != model.matched.Count) { count = model.matched.Count; countLabel = count + " / " + ThingRegistry.Things.Count; }
                    context.Label(new Rect(0f, 188f * scale, rect.width, 28f * scale), countLabel, context.Palette.Muted);
                    float footer = 48f * scale;
                    Rect viewport = new Rect(0f, 224f * scale, rect.width, Mathf.Max(1f, rect.height - 224f * scale - footer));
                    float details = model.diagnostics ? Mathf.Max(1, ThingRegistry.Errors.Count) * 32f * scale : 0f;
                    scroll.Draw(viewport, details + model.matched.Count * UI.UiThingsList.RowHeight * scale, drawRows);
                    float width = Mathf.Max(0f, (rect.width - 12f * scale) * 0.5f);
                    if (diagnostic.Draw(new Rect(0f, rect.height - 36f * scale, width, 36f * scale), diagnosticLabel, scale: scale)) model.diagnostics = !model.diagnostics;
                    if (dump.Draw(new Rect(width + 12f * scale, rect.height - 36f * scale, width, 36f * scale), dumpLabel, scale: scale)) Dump();
                }
                finally { context.FontSize = oldFont; }
            }

            private void ChangeCategory(int index)
            {
                if (index == categoryIndex) return;
                categoryIndex = index;
                model.selected = model.options[index];
                model.revision = -1;
                scroll.Reset();
            }

            private void DrawRows(Rect rect)
            {
                float height = UI.UiThingsList.RowHeight * scale;
                float details = model.diagnostics ? Mathf.Max(1, ThingRegistry.Errors.Count) * 32f * scale : 0f;
                if (model.diagnostics)
                {
                    if (ThingRegistry.Errors.Count == 0) context.Label(new Rect(0f, 0f, rect.width, 32f * scale), T("IM_RegistryNoErrors"), context.Palette.Muted);
                    for (int i = 0; i < ThingRegistry.Errors.Count; i++)
                        context.Label(new Rect(0f, i * 32f * scale, rect.width, 32f * scale), ThingRegistry.Errors[i], context.Palette.Error);
                }
                int first = Mathf.Clamp(Mathf.FloorToInt((scroll.VisibleRect.y - details) / height), 0, model.matched.Count);
                int last = Mathf.Clamp(Mathf.CeilToInt((scroll.VisibleRect.yMax - details) / height), first, model.matched.Count);
                for (int i = first; i < last; i++) rows.DrawRow(new Rect(0f, details + i * height, rect.width, height - 2f * scale), model.matched[i], false, scale: scale, readOnly: true);
            }

            public override void DrawOverlay(Rect viewport)
            {
                ChangeCategory(categories.DrawPopup(viewport, categoryIndex));
                rows.DrawTooltip(viewport, scale);
            }

            public override void Dispose()
            {
                search.Dispose();
                categories.Close();
                context.Release(categories.Id);
                for (int i = 0; i < optionCount; i++) context.Release(categories.GetOptionId(i));
                context.Release(diagnostic.Id);
                context.Release(dump.Id);
                context.Release(scroll.Id);
                model.loading.Dispose();
            }
        }

        private sealed class CategoryOption
        {
            internal readonly ThingCategoryDef Category;
            internal readonly bool Uncategorized;
            internal readonly string Label;
            internal CategoryOption(ThingCategoryDef category, bool uncategorized, string label)
            { Category = category; Uncategorized = uncategorized; Label = label; }
        }
    }
}
