using System;
using System.Collections.Generic;
using AdvancedRimTalk.Documentation;
using AdvancedRimTalk.Integration;
using UnityEngine;
using Verse;

namespace AdvancedRimTalk.UI
{
    internal sealed class DocumentationPage
    {
        private const float LeftPanelWidth = 255f;
        private const float PanelGap = 8f;
        private const float RowHeight = 24f;
        private const float NativeRowHeight = 32f;
        private static readonly Color SectionColor =
            new Color(1f, 0.85f, 0.55f);

        private readonly AdvancedRimTalkDocumentationCatalog catalog;
        private readonly IrisMenusMarkdownRenderer markdown;
        private Vector2 navigationScrollPosition = Vector2.zero;
        private Vector2 documentScrollPosition = Vector2.zero;
        private DocumentationEntry selectedEntry;
        private bool sourceMode;
        private GUIStyle sourceStyle;
        private GUIStyle nativeSourceStyle;
        private string pendingPath;
        private bool pendingBack;
        private bool focusNavigation;
        private readonly Stack<DocumentationEntry> back = new Stack<DocumentationEntry>();
        public IEnumerable<DocumentationEntry> Entries => catalog.Entries;
        internal AdvancedRimTalkDocumentationCatalog Catalog => catalog;

        public void Focus(string path) { pendingPath = path; }

        public DocumentationPage(string contentRoot)
            : this(AdvancedRimTalkDocumentationCatalog.Create(contentRoot))
        {
        }

        internal DocumentationPage(AdvancedRimTalkDocumentationCatalog catalog)
        {
            this.catalog = catalog;
            markdown = IrisMenusMarkdownRenderer.TryCreate(
                delegate
                {
                    return selectedEntry == null
                        ? string.Empty
                        : selectedEntry.Markdown;
                });
            selectedEntry = catalog.Overview;
            if (markdown != null) markdown.OpenLink = OpenLink;
        }

        private void OpenLink(string link)
        {
            if (Uri.TryCreate(link, UriKind.Absolute, out Uri uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Application.OpenURL(link);
                return;
            }
            string path = AdvancedRimTalkDocumentationCatalog.ResolvePath(selectedEntry.RelativePath, Uri.UnescapeDataString(link));
            if (path != null && catalog.Find(path) != null) Focus(path);
            else Messages.Message("AdvancedRimTalk.Documentation.LinkUnavailable".Translate(link), RimWorld.MessageTypeDefOf.RejectInput);
        }

        public void Draw(Rect inRect)
        {
            if (pendingBack && back.Count > 0)
            {
                selectedEntry = back.Pop();
                focusNavigation = true;
                documentScrollPosition = Vector2.zero;
                GUI.FocusControl(null);
            }
            pendingBack = false;
            if (pendingPath != null)
            {
                var entry = catalog.Find(pendingPath);
                if (entry != null) Select(entry);
                pendingPath = null;
            }
            if (!catalog.IsAvailable)
            {
                ArtiUi.Label(
                    inRect,
                    "AdvancedRimTalk.Documentation.Unavailable".Translate());
                return;
            }

            if (selectedEntry == null)
            {
                selectedEntry = catalog.Overview;
            }

            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            try
            {
                Rect leftRect = new Rect(
                    inRect.x,
                    inRect.y,
                    Mathf.Min(ArtiUi.IsNative ? 280f : LeftPanelWidth, Mathf.Max(180f, inRect.width * 0.32f)),
                    inRect.height);
                Rect rightRect = new Rect(
                    leftRect.xMax + PanelGap,
                    inRect.y,
                    Mathf.Max(1f, inRect.width - leftRect.width - PanelGap),
                    inRect.height);

                DrawNavigation(leftRect);
                DrawDocument(rightRect);
            }
            finally
            {
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
                GUI.color = previousColor;
            }
        }

        private void DrawNavigation(Rect rect)
        {
            ArtiUi.DrawMenuSection(rect);
            bool native = ArtiUi.IsNative;
            float rowHeight = native ? NativeRowHeight : RowHeight;
            float rowGap = native ? 4f : 2f;
            float sectionGap = native ? 8f : 4f;
            float firstRowY = native ? 40f : 30f;
            float contentWidth = Mathf.Max(1f, rect.width - 18f);
            float selectedY = selectedEntry == catalog.Overview ? firstRowY : -1f;
            float contentHeight = firstRowY + rowHeight + rowGap + (native ? 8f : 6f);
            foreach (DocumentationCategory category in catalog.Categories)
            {
                foreach (DocumentationEntry entry in category.Documents)
                {
                    if (entry == selectedEntry) selectedY = contentHeight;
                    contentHeight += rowHeight + rowGap;
                }
                contentHeight += sectionGap;
            }
            contentHeight += native ? 8f : 2f;

            Rect viewRect = new Rect(
                0f,
                0f,
                contentWidth,
                Mathf.Max(rect.height, contentHeight));
            if (focusNavigation && selectedY >= 0f)
            {
                navigationScrollPosition.y = Mathf.Clamp(
                    selectedY - firstRowY,
                    0f,
                    Mathf.Max(0f, viewRect.height - rect.height));
                focusNavigation = false;
            }
            ArtiUi.BeginScrollView(rect, ref navigationScrollPosition, viewRect);
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = native ? ArtiUi.Muted : SectionColor;
                ArtiUi.Label(
                    new Rect(native ? 12f : 6f, native ? 8f : 4f, viewRect.width - (native ? 24f : 12f), 24f),
                    "AdvancedRimTalk.Documentation.Index".Translate());
                GUI.color = native ? ArtiUi.Ink : Color.white;
                float y = DrawNavigationEntry(
                    viewRect,
                    firstRowY,
                    native ? 8f : 4f,
                    "AdvancedRimTalk.Documentation.Overview".Translate().ToString(),
                    catalog.Overview);
                y += native ? 8f : 6f;

                foreach (DocumentationCategory category in catalog.Categories)
                {
                    DocumentationEntry categoryIndex = category.Documents[0];
                    Rect header = new Rect(
                        native ? 8f : 4f,
                        y,
                        Mathf.Max(1f, viewRect.width - (native ? 16f : 8f)),
                        rowHeight);
                    if (native)
                    {
                        if (ArtiUi.Navigation(header, category.TitleKey.Translate().ToString(), categoryIndex == selectedEntry))
                            Select(categoryIndex);
                    }
                    else
                    {
                        if (categoryIndex == selectedEntry) Widgets.DrawHighlight(header);
                        GUI.color = SectionColor;
                        if (ArtiUi.ButtonText(header, category.TitleKey.Translate().ToString(), false))
                            Select(categoryIndex);
                    }

                    GUI.color = native ? ArtiUi.Ink : Color.white;
                    y += rowHeight + rowGap;
                    for (int index = 1; index < category.Documents.Count; index++)
                    {
                        y = DrawNavigationEntry(
                            viewRect,
                            y,
                            native ? 20f : 16f,
                            category.Documents[index].Title,
                            category.Documents[index]);
                    }

                    y += sectionGap;
                }
            }
            finally
            {
                ArtiUi.EndScrollView();
            }
        }

        private float DrawNavigationEntry(
            Rect viewRect,
            float y,
            float indent,
            string label,
            DocumentationEntry entry)
        {
            bool native = ArtiUi.IsNative;
            Rect row = new Rect(
                indent,
                y,
                Mathf.Max(1f, viewRect.width - indent - (native ? 8f : 4f)),
                native ? NativeRowHeight : RowHeight);
            if (native)
            {
                if (ArtiUi.Navigation(row, label, entry == selectedEntry)) Select(entry);
            }
            else
            {
                if (entry == selectedEntry) Widgets.DrawHighlight(row);
                if (ArtiUi.ButtonText(row, label, false)) Select(entry);
            }

            return y + row.height + (native ? 4f : 2f);
        }

        private void DrawDocument(Rect rect)
        {
            bool native = ArtiUi.IsNative;
            ArtiUi.DrawMenuSection(rect);
            if (selectedEntry == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                ArtiUi.Label(
                    rect,
                    "AdvancedRimTalk.Documentation.SelectDocument".Translate(),
                    native ? ArtiUi.Muted : Color.gray);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = native ? ArtiUi.Ink : Color.white;
                return;
            }

            GUI.color = native ? ArtiUi.Ink : Color.white;
            Text.Font = GameFont.Medium;
            float titleWidth = Mathf.Max(1f, rect.width - 24f);
            float titleHeight = Mathf.Max(
                30f,
                Text.CalcHeight(selectedEntry.Title, titleWidth));
            ArtiUi.Label(
                new Rect(rect.x + 12f, rect.y + 8f, titleWidth, titleHeight),
                selectedEntry.Title);

            Text.Font = native ? GameFont.Small : GameFont.Tiny;
            ArtiUi.Label(
                new Rect(
                    rect.x + 12f,
                    rect.y + 8f + titleHeight,
                    titleWidth,
                    native ? 24f : 18f),
                selectedEntry.RelativePath,
                native ? ArtiUi.Muted : Color.gray);
            Text.Font = GameFont.Small;
            float toolbarY = rect.y + (native ? 40f : 34f) + titleHeight;
            float toolbarHeight = native ? NativeRowHeight : RowHeight;
            Rect backButton = new Rect(rect.x + 12f, toolbarY, native ? 32f : 28f, toolbarHeight);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && back.Count > 0;
            if (ArtiUi.ButtonText(backButton, "◀"))
            {
                pendingBack = true;
            }
            GUI.enabled = enabled;
            TooltipHandler.TipRegion(backButton, "AdvancedRimTalk.Documentation.Back".Translate());
            Rect sourceToggle = new Rect(rect.x + (native ? 52f : 48f), toolbarY,
                Mathf.Max(1f, rect.width - (native ? 160f : 156f)), toolbarHeight);
            bool previousSourceMode = sourceMode;
            ArtiUi.CheckboxLabeled(sourceToggle,
                "AdvancedRimTalk.Documentation.Source".Translate().ToString(), ref sourceMode);
            if (sourceMode != previousSourceMode)
            {
                documentScrollPosition = Vector2.zero;
                GUI.FocusControl(null);
            }
            Rect copyButton = new Rect(rect.xMax - 102f, toolbarY, 90f, toolbarHeight);
            if (ArtiUi.ButtonText(copyButton, "AdvancedRimTalk.Documentation.Copy".Translate().ToString()))
            {
                GUIUtility.systemCopyBuffer = selectedEntry.Markdown ?? string.Empty;
            }
            Text.Font = GameFont.Small;

            float contentY = toolbarY + toolbarHeight + (native ? 8f : 6f);
            Rect viewport = new Rect(
                rect.x + 8f,
                contentY,
                Mathf.Max(1f, rect.width - 16f),
                Mathf.Max(1f, rect.yMax - contentY - 8f));
            float contentWidth = Mathf.Max(1f, viewport.width - 20f);
            if (sourceStyle == null)
            {
                sourceStyle = new GUIStyle(Text.CurTextAreaReadOnlyStyle) { richText = false };
            }
            GUIStyle currentSourceStyle = sourceStyle;
            if (native)
            {
                if (nativeSourceStyle == null) nativeSourceStyle = new GUIStyle(sourceStyle);
                Color ink = ArtiUi.Ink;
                SetSourceStyleState(nativeSourceStyle.normal, ink);
                SetSourceStyleState(nativeSourceStyle.hover, ink);
                SetSourceStyleState(nativeSourceStyle.active, ink);
                SetSourceStyleState(nativeSourceStyle.focused, ink);
                SetSourceStyleState(nativeSourceStyle.onNormal, ink);
                SetSourceStyleState(nativeSourceStyle.onHover, ink);
                SetSourceStyleState(nativeSourceStyle.onActive, ink);
                SetSourceStyleState(nativeSourceStyle.onFocused, ink);
                currentSourceStyle = nativeSourceStyle;
            }

            bool showSource = sourceMode || markdown == null || markdown.Failed;
            float measuredHeight = showSource ? 0f : markdown.Measure(contentWidth);
            showSource = showSource || markdown.Failed;
            if (showSource)
            {
                measuredHeight = currentSourceStyle.CalcHeight(
                    new GUIContent(selectedEntry.Markdown ?? string.Empty), contentWidth);
            }
            float contentHeight = Mathf.Max(
                viewport.height,
                measuredHeight + 12f);
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);

            ArtiUi.BeginScrollView(viewport, ref documentScrollPosition, viewRect);
            try
            {
                Rect contentRect = new Rect(
                    0f,
                    0f,
                    contentWidth,
                    Mathf.Max(contentHeight, measuredHeight));
                if (showSource)
                {
                    // Ignore edits: this is a selectable snapshot, never a document editor.
                    GUI.color = Color.white;
                    GUI.TextArea(contentRect, selectedEntry.Markdown ?? string.Empty, currentSourceStyle);
                    GUI.color = native ? ArtiUi.Ink : Color.white;
                }
                else
                {
                    markdown.Draw(contentRect);
                }
            }
            finally
            {
                ArtiUi.EndScrollView();
            }
        }

        private static void SetSourceStyleState(GUIStyleState state, Color ink)
        {
            state.background = null;
            state.textColor = ink;
        }

        private void Select(DocumentationEntry entry)
        {
            if (entry == selectedEntry)
            {
                return;
            }

            if (selectedEntry != null) back.Push(selectedEntry);
            selectedEntry = entry;
            focusNavigation = true;
            documentScrollPosition = Vector2.zero;
            GUI.FocusControl(null);
        }

    }
}
