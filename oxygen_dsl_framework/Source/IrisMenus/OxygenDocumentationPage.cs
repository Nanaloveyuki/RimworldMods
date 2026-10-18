using System;
using System.Collections.Generic;
using System.Text;
using IrisMenus;
using UnityEngine;
using Verse;

namespace Oxygendsl.IrisMenus
{
    internal sealed class OxygenDocumentationPage
    {
        private const float Gap = 8f;
        private const float Padding = 8f;
        private const float RowGap = 2f;
        private readonly OxygenDocumentationCatalog catalog;
        private readonly ArticleView[] views;
        private readonly string[] articleTitles;
        private Vector2 navigationScroll;
        private Vector2 documentScroll;
        private int selectedArticle;
        private int selectedBlock = -1;
        private int pendingBlock = -1;
        private bool focusNavigation;
        private bool clearControlFocus;
        private bool sourceMode;
        private GUIStyle plainStyle;
        private NavigationRow[] navigationRows = new NavigationRow[0];
        private float navigationWidth = -1f;
        private int navigationArticle = -1;
        private float navigationHeight;
        private float documentWidth = -1f;
        private float documentViewportHeight = -1f;
        private float documentHeight;
        private bool layoutSourceMode;
        private object language;
        private string indexLabel;
        private string sourceLabel;
        private string copyLabel;
        private string narrowNavigationLabel;
        private GUIContent titleContent = new GUIContent();
        private GUIContent errorContent = new GUIContent();
        private float titleWidth = -1f;
        private float titleHeight;

        public OxygenDocumentationPage()
        {
            catalog = OxygenDocumentationCatalog.Embedded.Value;
            views = new ArticleView[catalog.Articles.Length];
            articleTitles = new string[catalog.Articles.Length];
        }

        public IEnumerable<MenuSearchEntry> SearchEntries() { return catalog.SearchEntries(); }

        public void Focus(string id)
        {
            if (!catalog.TryFind(id, out DocumentationTarget target)) return;
            Select(target.Article, id == catalog.Articles[target.Article].Id ? -1 : target.Block);
        }

        public void Draw(Rect rect)
        {
            var state = new DocumentationGuiState(true);
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = Color.white;
                RefreshLabels();
                if (plainStyle == null)
                    plainStyle = new GUIStyle(Text.CurTextAreaReadOnlyStyle) { richText = false, wordWrap = true };
                if (clearControlFocus)
                {
                    GUI.FocusControl(null);
                    clearControlFocus = false;
                }
                if (rect.width <= 0f || rect.height <= 0f) return;
                if (rect.width < 520f)
                {
                    Rect selector = new Rect(rect.x, rect.y, rect.width, Mathf.Min(30f, rect.height));
                    if (Widgets.ButtonText(selector, narrowNavigationLabel)) OpenNavigationMenu();
                    DrawDocument(new Rect(rect.x, selector.yMax + Gap, rect.width,
                        Mathf.Max(0f, rect.height - selector.height - Gap)));
                }
                else
                {
                    float width = Mathf.Min(255f, rect.width * 0.3f);
                    DrawNavigation(new Rect(rect.x, rect.y, width, rect.height));
                    DrawDocument(new Rect(rect.x + width + Gap, rect.y,
                        rect.width - width - Gap, rect.height));
                }
            }
            finally { state.Restore(); }
        }

        private void RefreshLabels()
        {
            if (indexLabel != null && ReferenceEquals(language, LanguageDatabase.activeLanguage)) return;
            language = LanguageDatabase.activeLanguage;
            indexLabel = "Oxygendsl_DocumentationIndex".Translate().ToString();
            sourceLabel = "Oxygendsl_DocumentationSource".Translate().ToString();
            copyLabel = "Oxygendsl_DocumentationCopy".Translate().ToString();
            for (int index = 0; index < articleTitles.Length; index++) articleTitles[index] = catalog.Articles[index].Title;
            navigationArticle = -1;
            UpdateSelectedLabels();
        }

        private void UpdateSelectedLabels()
        {
            DocumentationArticle article = catalog.Articles[selectedArticle];
            titleContent.text = articleTitles[selectedArticle];
            errorContent.text = article.Available ? string.Empty :
                "Oxygendsl_DocumentationUnavailable".Translate().ToString() + "\n\n" + article.Error;
            narrowNavigationLabel = indexLabel + ": " + articleTitles[selectedArticle];
            titleWidth = -1f;
        }

        private void Select(int article, int block)
        {
            selectedArticle = article;
            selectedBlock = block;
            pendingBlock = block < 0 ? 0 : block;
            documentScroll = Vector2.zero;
            documentWidth = -1f;
            navigationArticle = -1;
            focusNavigation = true;
            clearControlFocus = true;
            if (indexLabel != null) UpdateSelectedLabels();
        }

        private void LayoutNavigation(float width)
        {
            if (navigationWidth == width && navigationArticle == selectedArticle) return;
            var rows = new List<NavigationRow>();
            float y = 30f;
            for (int article = 0; article < catalog.Articles.Length; article++)
            {
                float height = Mathf.Max(28f, Text.CalcHeight(articleTitles[article], width - 8f) + 6f);
                rows.Add(new NavigationRow(article, -1, articleTitles[article], new Rect(4f, y, width - 8f, height)));
                y += height + RowGap;
                if (article == selectedArticle)
                {
                    DocumentationBlock[] blocks = catalog.Articles[article].Blocks;
                    for (int block = 0; block < blocks.Length; block++)
                    {
                        if (blocks[block].Id == null) continue;
                        height = Mathf.Max(26f, Text.CalcHeight(blocks[block].Title, width - 20f) + 4f);
                        rows.Add(new NavigationRow(article, block, blocks[block].Title,
                            new Rect(16f, y, width - 20f, height)));
                        y += height + RowGap;
                    }
                }
                y += 6f;
            }
            navigationRows = rows.ToArray();
            navigationWidth = width;
            navigationArticle = selectedArticle;
            navigationHeight = y + Padding;
        }

        private void DrawNavigation(Rect rect)
        {
            float width = Mathf.Max(1f, rect.width - 20f);
            LayoutNavigation(width);
            float height = Mathf.Max(rect.height, navigationHeight);
            if (focusNavigation)
            {
                for (int index = 0; index < navigationRows.Length; index++)
                {
                    NavigationRow row = navigationRows[index];
                    if (row.Article != selectedArticle || row.Block != selectedBlock) continue;
                    navigationScroll.y = Mathf.Clamp(row.Rect.y - Padding, 0f, height - rect.height);
                    break;
                }
                focusNavigation = false;
            }
            navigationScroll.x = 0f;
            navigationScroll.y = Mathf.Clamp(navigationScroll.y, 0f, height - rect.height);
            Widgets.BeginScrollView(rect, ref navigationScroll, new Rect(0f, 0f, width, height));
            try
            {
                Widgets.Label(new Rect(4f, 0f, width - 8f, 26f), indexLabel);
                for (int index = 0; index < navigationRows.Length; index++)
                {
                    NavigationRow row = navigationRows[index];
                    if (row.Article == selectedArticle && row.Block == selectedBlock) Widgets.DrawHighlight(row.Rect);
                    if (Widgets.ButtonText(row.Rect, row.Label, false)) Select(row.Article, row.Block);
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private void OpenNavigationMenu()
        {
            var options = new List<FloatMenuOption>();
            for (int article = 0; article < catalog.Articles.Length; article++)
            {
                int targetArticle = article;
                options.Add(new FloatMenuOption(articleTitles[article], () => Select(targetArticle, -1)));
                if (article != selectedArticle) continue;
                DocumentationBlock[] blocks = catalog.Articles[article].Blocks;
                for (int block = 0; block < blocks.Length; block++)
                {
                    if (blocks[block].Id == null) continue;
                    int targetBlock = block;
                    options.Add(new FloatMenuOption("  " + blocks[block].Title, () => Select(targetArticle, targetBlock)));
                }
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawDocument(Rect rect)
        {
            if (rect.height <= 0f) return;
            DocumentationArticle article = catalog.Articles[selectedArticle];
            float width = Mathf.Max(1f, rect.width - Padding * 2f);
            if (titleWidth != width)
            {
                Text.Font = GameFont.Medium;
                titleHeight = Mathf.Max(30f, Text.CalcHeight(titleContent.text, width));
                Text.Font = GameFont.Small;
                titleWidth = width;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + Padding, rect.y, width, titleHeight), titleContent.text);
            Text.Font = GameFont.Small;
            float toolbarY = rect.y + titleHeight + 4f;
            float copyWidth = Mathf.Min(128f, width * 0.45f);
            Rect sourceToggle = new Rect(rect.x + Padding, toolbarY, Mathf.Max(1f, width - copyWidth - Gap), 28f);
            bool previousSource = sourceMode;
            Widgets.CheckboxLabeled(sourceToggle, sourceLabel, ref sourceMode);
            if (sourceMode != previousSource)
            {
                documentWidth = -1f;
                documentScroll = Vector2.zero;
                pendingBlock = selectedBlock < 0 ? 0 : selectedBlock;
                GUI.FocusControl(null);
            }
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && article.Available;
            try
            {
                if (Widgets.ButtonText(new Rect(rect.xMax - Padding - copyWidth, toolbarY, copyWidth, 28f), copyLabel))
                    GUIUtility.systemCopyBuffer = article.Markdown;
            }
            finally { GUI.enabled = enabled; }
            float viewportY = toolbarY + 28f + Gap;
            Rect viewport = new Rect(rect.x + Padding, viewportY, width, Mathf.Max(0f, rect.yMax - viewportY));
            if (viewport.height <= 0f) return;
            if (!article.Available)
            {
                GUI.Label(viewport, errorContent, plainStyle);
                return;
            }
            ArticleView view = views[selectedArticle];
            if (view == null) views[selectedArticle] = view = new ArticleView(article);
            float contentWidth = Mathf.Max(1f, viewport.width - 20f);
            if (documentWidth != contentWidth || layoutSourceMode != sourceMode)
            {
                int scrollBlock = 0;
                float scrollFraction = 0f;
                bool preserveAnchor = documentWidth >= 0f && pendingBlock < 0;
                if (preserveAnchor)
                {
                    for (int index = 0; index < view.Blocks.Length; index++)
                    {
                        BlockView block = view.Blocks[index];
                        if (block.Y > documentScroll.y) break;
                        scrollBlock = index;
                        scrollFraction = Mathf.Clamp01((documentScroll.y - block.Y) / Mathf.Max(1f, block.Height));
                    }
                }
                view.Measure(contentWidth, sourceMode, plainStyle);
                if (preserveAnchor)
                    documentScroll.y = view.Blocks[scrollBlock].Y + scrollFraction * view.Blocks[scrollBlock].Height;
                documentWidth = contentWidth;
                layoutSourceMode = sourceMode;
                documentViewportHeight = -1f;
                documentScroll.x = 0f;
            }
            if (documentViewportHeight != viewport.height)
            {
                documentHeight = Mathf.Max(viewport.height, view.Height + Padding);
                documentViewportHeight = viewport.height;
            }
            if (pendingBlock >= 0)
            {
                documentScroll.y = view.Blocks[Mathf.Min(pendingBlock, view.Blocks.Length - 1)].Y;
                pendingBlock = -1;
            }
            documentScroll.y = Mathf.Clamp(documentScroll.y, 0f, documentHeight - viewport.height);
            Widgets.BeginScrollView(viewport, ref documentScroll, new Rect(0f, 0f, contentWidth, documentHeight));
            try
            {
                float bottom = documentScroll.y + viewport.height;
                for (int blockIndex = 0; blockIndex < view.Blocks.Length; blockIndex++)
                {
                    BlockView block = view.Blocks[blockIndex];
                    if (block.Y + block.Height < documentScroll.y || block.Y > bottom) continue;
                    if (sourceMode)
                        GUI.Label(new Rect(0f, block.Y, contentWidth, block.Height), block.Source, plainStyle);
                    else
                    {
                        for (int index = 0; index < block.Segments.Length; index++)
                        {
                            Segment segment = block.Segments[index];
                            float y = block.Y + segment.Y;
                            if (y + segment.Height < documentScroll.y || y > bottom) continue;
                            Rect content = new Rect(0f, y, contentWidth, segment.Height);
                            if (segment.Markdown != null) segment.Markdown.Draw(content);
                            else GUI.Label(content, segment.Plain, plainStyle);
                        }
                    }
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private sealed class ArticleView
        {
            internal readonly BlockView[] Blocks;
            internal float Height;
            private float measuredWidth = -1f;
            private bool measuredSource;
            internal ArticleView(DocumentationArticle article)
            {
                Blocks = new BlockView[article.Blocks.Length];
                for (int index = 0; index < Blocks.Length; index++) Blocks[index] = new BlockView(article.Blocks[index].Markdown);
            }
            internal void Measure(float width, bool source, GUIStyle plainStyle)
            {
                if (measuredWidth == width && measuredSource == source) return;
                measuredWidth = width;
                measuredSource = source;
                float y = 0f;
                for (int index = 0; index < Blocks.Length; index++)
                {
                    BlockView block = Blocks[index];
                    block.Y = y;
                    block.Measure(width, source, plainStyle);
                    y += block.Height + Gap;
                }
                Height = y;
            }
        }

        private sealed class BlockView
        {
            internal readonly GUIContent Source;
            internal readonly Segment[] Segments;
            internal float Y;
            internal float Height;
            internal BlockView(string source)
            {
                Source = new GUIContent(source);
                Segments = SplitSegments(source);
            }
            internal void Measure(float width, bool source, GUIStyle plainStyle)
            {
                if (source)
                {
                    Height = plainStyle.CalcHeight(Source, width);
                    return;
                }
                float y = 0f;
                for (int index = 0; index < Segments.Length; index++)
                {
                    Segment segment = Segments[index];
                    segment.Y = y;
                    segment.Height = segment.Markdown == null ? plainStyle.CalcHeight(segment.Plain, width) : segment.Markdown.Measure(width);
                    y += segment.Height + 4f;
                }
                Height = y;
            }
        }

        // Native Markdown handles prose. Literal code bypasses its tag stripping and lack of fence/code-span support.
        private static Segment[] SplitSegments(string source)
        {
            var segments = new List<Segment>();
            var prose = new StringBuilder();
            var code = new StringBuilder();
            char fenceMarker = '\0';
            int fenceLength = 0;
            using (var reader = new System.IO.StringReader(source))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int start = 0;
                    while (start < line.Length && start < 3 && line[start] == ' ') start++;
                    if (fenceMarker != '\0')
                    {
                        if (OxygenDocumentationCatalog.IsClosingFence(line, start, fenceMarker, fenceLength))
                        {
                            segments.Add(Segment.Literal(code.ToString()));
                            code.Clear();
                            fenceMarker = '\0';
                        }
                        else code.AppendLine(line);
                    }
                    else if (OxygenDocumentationCatalog.TryFence(line, start, out char marker, out int length))
                    {
                        FlushProse(segments, prose);
                        fenceMarker = marker;
                        fenceLength = length;
                    }
                    else prose.AppendLine(line);
                }
            }
            if (fenceMarker != '\0') segments.Add(Segment.Literal(code.ToString()));
            FlushProse(segments, prose);
            return segments.ToArray();
        }

        private sealed class InlineCodeParser : IMarkdownParser
        {
            private readonly MarkdownParser native = new MarkdownParser();

            public string Parse(string markdown)
            {
                if (string.IsNullOrEmpty(markdown) || markdown.IndexOf('`') < 0) return native.Parse(markdown);
                string prefix = "\uE000OxygenInlineCode";
                while (markdown.IndexOf(prefix, StringComparison.Ordinal) >= 0) prefix += "\uE000";
                var protectedSource = new StringBuilder(markdown.Length);
                var literals = new List<string>();
                var tokens = new List<string>();
                int index = 0;
                while (index < markdown.Length)
                {
                    if (markdown[index] == '\\' && index + 1 < markdown.Length && markdown[index + 1] == '`')
                    {
                        protectedSource.Append(markdown, index, 2);
                        index += 2;
                        continue;
                    }
                    if (markdown[index] != '`') { protectedSource.Append(markdown[index++]); continue; }
                    int end = index;
                    while (end < markdown.Length && markdown[end] == '`') end++;
                    int length = end - index;
                    int closing = FindClosingCode(markdown, end, length);
                    if (closing < 0)
                    {
                        protectedSource.Append(markdown, index, length);
                        index = end;
                        continue;
                    }
                    string token = prefix + literals.Count + "\uE001";
                    tokens.Add(token);
                    // Shield literal punctuation from IM, then prevent Unity from reading code as rich-text tags.
                    literals.Add(markdown.Substring(end, closing - end).Replace("<", "<\u200B"));
                    protectedSource.Append(token);
                    index = closing + length;
                }
                string rendered = native.Parse(protectedSource.ToString());
                for (int literal = 0; literal < literals.Count; literal++) rendered = rendered.Replace(tokens[literal], literals[literal]);
                return rendered;
            }

            private static int FindClosingCode(string markdown, int start, int length)
            {
                int index = start;
                while (index < markdown.Length && markdown[index] != '\n' && markdown[index] != '\r')
                {
                    if (markdown[index] != '`') { index++; continue; }
                    int end = index;
                    while (end < markdown.Length && markdown[end] == '`') end++;
                    if (end - index == length) return index;
                    index = end;
                }
                return -1;
            }
        }

        private static void FlushProse(List<Segment> segments, StringBuilder prose)
        {
            if (prose.Length == 0) return;
            string text = prose.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                segments.Add(new Segment(global::IrisMenus.Markdown.Dynamic(() => text, new InlineCodeParser())));
            prose.Clear();
        }

        private sealed class Segment
        {
            internal readonly IMarkdown Markdown;
            internal readonly GUIContent Plain;
            internal float Y;
            internal float Height;
            internal Segment(IMarkdown markdown) { Markdown = markdown; }
            private Segment(string plain) { Plain = new GUIContent(plain); }
            internal static Segment Literal(string plain) { return new Segment(plain); }
        }

        private struct NavigationRow
        {
            internal readonly int Article;
            internal readonly int Block;
            internal readonly string Label;
            internal readonly Rect Rect;
            internal NavigationRow(int article, int block, string label, Rect rect)
            { Article = article; Block = block; Label = label; Rect = rect; }
        }

        private struct DocumentationGuiState
        {
            private readonly Color color;
            private readonly Color background;
            private readonly Color content;
            private readonly bool enabled;
            private readonly bool changed;
            private readonly Matrix4x4 matrix;
            private readonly GUISkin skin;
            private readonly int depth;
            private readonly GameFont font;
            private readonly TextAnchor anchor;
            private readonly bool wordWrap;
            internal DocumentationGuiState(bool capture)
            {
                color = GUI.color;
                background = GUI.backgroundColor;
                content = GUI.contentColor;
                enabled = GUI.enabled;
                changed = GUI.changed;
                matrix = GUI.matrix;
                skin = GUI.skin;
                depth = GUI.depth;
                font = Text.Font;
                anchor = Text.Anchor;
                wordWrap = Text.WordWrap;
            }
            internal void Restore()
            {
                GUI.color = color;
                GUI.backgroundColor = background;
                GUI.contentColor = content;
                GUI.enabled = enabled;
                GUI.changed = changed;
                GUI.matrix = matrix;
                GUI.skin = skin;
                GUI.depth = depth;
                Text.Font = font;
                Text.Anchor = anchor;
                Text.WordWrap = wordWrap;
            }
        }
    }
}
