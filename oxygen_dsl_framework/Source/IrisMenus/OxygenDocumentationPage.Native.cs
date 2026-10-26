using System;
using System.Collections.Generic;
using IrisMenus;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace Oxygendsl.IrisMenus
{
    internal sealed partial class OxygenDocumentationPage
    {
        // The registered legacy page remains the state owner, including search and mode changes.
        private sealed class NativePage : MenuNativePage
        {
            private readonly UiContext context;
            private readonly OxygenDocumentationPage owner;
            private readonly NativeArticle[] articles;
            private readonly NativeNavigationRow[][] navigation;
            private readonly UiScrollView indexScroll;
            private readonly UiButton indexButton, copyButton;
            private readonly UiSwitch sourceSwitch;
            private readonly Action<Rect> drawIndex, drawArticle, drawDocument;
            private readonly UiPageTransition articleTransition;
            private GUIStyle literalStyle, richStyle, navigationStyle, titleStyle;
            private readonly GUIContent measuring = new GUIContent();
            private NativeNavigationRow[] rows;
            private NativeArticle article;
            private int revision = -1, activeArticle = -1;
            private float scale = 1f, measuredScale = -1f, indexWidth = -1f;
            private float indexHeight, contentWidth = -1f, contentHeight;
            private float documentTop, documentTitleHeight;
            private bool measuredSource, indexOpen, focusIndex = true;
            private int pending = -1;
            private object labelsLanguage;
            private string tooltipText;
            private readonly UiTooltip tooltip;
            private bool darkLinks;

            internal NativePage(UiContext context, OxygenDocumentationPage owner)
            {
                this.context = context ?? throw new ArgumentNullException(nameof(context));
                this.owner = owner;
                articles = new NativeArticle[owner.catalog.Articles.Length];
                navigation = new NativeNavigationRow[articles.Length][];
                int anchorCount = articles.Length;
                for (int i = 0; i < articles.Length; i++)
                    for (int block = 0; block < owner.catalog.Articles[i].Blocks.Length; block++)
                        if (owner.catalog.Articles[i].Blocks[block].Id != null) anchorCount++;
                indexScroll = new UiScrollView(context, anchorCount);
                indexButton = new UiButton(context);
                copyButton = new UiButton(context);
                sourceSwitch = new UiSwitch(context);
                tooltip = new UiTooltip(context);
                articleTransition = new UiPageTransition(context);
                int anchor = 0;
                for (int i = 0; i < articles.Length; i++)
                {
                    DocumentationArticle source = owner.catalog.Articles[i];
                    var items = new List<NativeNavigationRow>();
                    items.Add(new NativeNavigationRow(context, i, -1, anchor++));
                    for (int block = 0; block < source.Blocks.Length; block++)
                        if (source.Blocks[block].Id != null)
                            items.Add(new NativeNavigationRow(context, i, block, anchor++));
                    navigation[i] = items.ToArray();
                }
                drawIndex = DrawIndexRows;
                drawArticle = DrawArticleContent;
                drawDocument = DrawDocumentNative;
            }

            public override void Focus(string anchor)
            {
                owner.Focus(anchor);
                SynchronizeSelection();
                indexOpen = false;
            }

            public override void Prepare()
            {
                owner.RefreshLabels();
                SynchronizeSelection();
            }

            private void SynchronizeSelection()
            {
                if (revision == owner.selectionRevision && activeArticle == owner.selectedArticle) return;
                bool changed = activeArticle != owner.selectedArticle;
                activeArticle = owner.selectedArticle;
                revision = owner.selectionRevision;
                if (articles[activeArticle] == null)
                    articles[activeArticle] = new NativeArticle(context, owner.catalog.Articles[activeArticle]);
                article = articles[activeArticle];
                pending = owner.selectedBlock < 0 ? 0 : owner.selectedBlock;
                if (changed)
                {
                    article.Scroll.Reset();
                    contentWidth = indexWidth = -1f;
                    var visible = new List<NativeNavigationRow>();
                    for (int i = 0; i < navigation.Length; i++)
                    {
                        visible.Add(navigation[i][0]);
                        if (i == activeArticle)
                            for (int j = 1; j < navigation[i].Length; j++) visible.Add(navigation[i][j]);
                    }
                    rows = visible.ToArray();
                }
                focusIndex = true;
                GUI.FocusControl(null);
            }

            public override void Draw(Rect rect)
            {
                if (rect.width <= 0f || rect.height <= 0f) return;
                var saved = new DocumentationGuiState(true);
                float font = context.FontSize;
                FontStyle fontStyle = context.FontStyle;
                try
                {
                    owner.RefreshLabels();
                    SynchronizeSelection();
                    scale = context.Scale;
                    PrepareStyles();
                    if (!ReferenceEquals(labelsLanguage, LanguageDatabase.activeLanguage))
                    {
                        labelsLanguage = LanguageDatabase.activeLanguage;
                        indexWidth = -1f;
                    }
                    context.FontSize = 14f * scale;
                    context.FontStyle = FontStyle.Normal;
                    float gap = 20f * scale;
                    if (rect.width >= 640f * scale)
                    {
                        float width = Mathf.Min(280f * scale, rect.width * 0.32f);
                        DrawIndex(new Rect(rect.x, rect.y, width, rect.height), true);
                        articleTransition.Draw(new Rect(rect.x + width + gap, rect.y,
                            Mathf.Max(1f, rect.width - width - gap), rect.height), article.Id, drawDocument);
                    }
                    else
                    {
                        Rect header = new Rect(rect.x, rect.y, rect.width, Mathf.Min(40f * scale, rect.height));
                        if (indexButton.Draw(header, owner.indexLabel, variant: UiButtonVariant.Light,
                            icon: UiGlyphKind.Layers, scale: scale)) indexOpen = !indexOpen;
                        tooltip.Observe(header, owner.narrowNavigationLabel, indexButton.Id);
                        tooltipText = owner.narrowNavigationLabel;
                        Rect body = new Rect(rect.x, header.yMax + 12f * scale, rect.width,
                            Mathf.Max(0f, rect.yMax - header.yMax - 12f * scale));
                        if (indexOpen) DrawIndex(body, false);
                        else articleTransition.Draw(body, article.Id, drawDocument);
                    }
                }
                finally
                {
                    context.FontSize = font;
                    context.FontStyle = fontStyle;
                    saved.Restore();
                }
            }

            private void PrepareStyles()
            {
                if (literalStyle == null)
                {
                    literalStyle = new GUIStyle(Text.CurFontStyle) { richText = false, wordWrap = true, alignment = TextAnchor.UpperLeft };
                    richStyle = new GUIStyle(literalStyle) { richText = true };
                    navigationStyle = new GUIStyle(literalStyle);
                    titleStyle = new GUIStyle(literalStyle) { fontStyle = FontStyle.Bold };
                }
                if (measuredScale != scale)
                {
                    measuredScale = scale;
                    literalStyle.fontSize = richStyle.fontSize = Mathf.RoundToInt(16f * scale);
                    navigationStyle.fontSize = Mathf.RoundToInt(14f * scale);
                    titleStyle.fontSize = Mathf.RoundToInt(18f * scale);
                    contentWidth = indexWidth = -1f;
                }
                literalStyle.font = richStyle.font = navigationStyle.font = titleStyle.font = Text.CurFontStyle.font;
                navigationStyle.normal.textColor = context.Palette.Ink;
                bool nextDark = context.ThemeAmount >= 0.5f;
                if (nextDark != darkLinks) { darkLinks = nextDark; contentWidth = -1f; }
            }

            private float TextHeight(string text, float width, GUIStyle style)
            {
                measuring.text = text;
                return style.CalcHeight(measuring, Mathf.Max(1f, width));
            }

            private void DrawIndex(Rect rect, bool heading)
            {
                if (rect.width <= 0f || rect.height <= 0f) return;
                float top = 0f;
                if (heading)
                {
                    float height = Mathf.Max(32f * scale, TextHeight(owner.indexLabel, rect.width - 16f * scale, navigationStyle));
                    context.Label(new Rect(rect.x + 8f * scale, rect.y, rect.width - 16f * scale, height),
                        owner.indexLabel, context.Palette.Muted, TextAnchor.MiddleLeft, true);
                    top = height + 12f * scale;
                }
                Rect viewport = new Rect(rect.x, rect.y + top, rect.width, Mathf.Max(0f, rect.height - top));
                float width = Mathf.Max(1f, viewport.width - 20f);
                if (indexWidth != width)
                {
                    float y = 0f;
                    for (int i = 0; i < rows.Length; i++)
                    {
                        NativeNavigationRow row = rows[i];
                        row.Label = row.Block < 0 ? owner.articleTitles[row.Article] : owner.catalog.Articles[row.Article].Blocks[row.Block].Title;
                        float indent = (row.Block < 0 ? 12f : 28f) * scale;
                        float textWidth = Mathf.Max(1f, width - indent - (row.Block < 0 ? 28f : 10f) * scale);
                        float height = Mathf.Max(38f * scale, TextHeight(row.Label, textWidth, navigationStyle) + 14f * scale);
                        row.Rect = new Rect(0f, y, width, height);
                        row.TextRect = new Rect(indent, y + 7f * scale, textWidth, height - 14f * scale);
                        y += height + (row.Block < 0 ? 6f : 2f) * scale;
                    }
                    indexHeight = y;
                    indexWidth = width;
                }
                if (focusIndex && viewport.height > 0f)
                {
                    for (int i = 0; i < rows.Length; i++)
                        if (rows[i].Article == activeArticle && rows[i].Block == owner.selectedBlock)
                        { indexScroll.FocusAnchor(rows[i].Anchor, 8f * scale); break; }
                    focusIndex = false;
                }
                context.Fill(new Rect(rect.xMax - scale, rect.y, scale, rect.height), context.Palette.Line);
                indexScroll.Draw(viewport, indexHeight, drawIndex);
            }

            private void DrawIndexRows(Rect rect)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    NativeNavigationRow row = rows[i];
                    indexScroll.Anchor(row.Anchor, row.Rect);
                    UiInteraction hit = context.Interact(row.Id, row.Rect);
                    bool selected = row.Article == activeArticle && row.Block == owner.selectedBlock;
                    float active = row.Selection.Step(selected ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                    float hover = row.Hover.Step(hit.Hovered || hit.Pressed ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                    Color background = context.Palette.Selected; background.a *= active;
                    context.Fill(row.Rect, background);
                    Color hovered = context.Palette.Ink; hovered.a *= hover * (1f - active) * 0.07f;
                    context.Fill(row.Rect, hovered);
                    if (active > 0f)
                    {
                        Color marker = context.Palette.Signal; marker.a *= active;
                        context.Fill(new Rect(row.Rect.x, row.Rect.y + 8f * scale, 3f * scale, row.Rect.height - 16f * scale), marker);
                    }
                    if (hit.Focused && context.FocusVisible) context.Border(row.Rect, context.Palette.Ink);
                    Color ink = Color.Lerp(context.Palette.Ink, context.Palette.OnSelected, active);
                    context.Label(row.TextRect, row.Label, ink, TextAnchor.UpperLeft, true);
                    if (row.Block < 0)
                        UiGlyph.Draw(context, new Rect(row.Rect.xMax - 22f * scale, row.Rect.center.y - 7f * scale, 14f * scale, 14f * scale),
                            UiGlyphKind.ChevronRight, ink, row.Article == activeArticle ? 90f : 0f);
                    if (hit.Clicked)
                    {
                        owner.Select(row.Article, row.Block);
                        SynchronizeSelection();
                        indexOpen = false;
                        return;
                    }
                }
            }

            private void DrawDocumentNative(Rect rect)
            {
                if (rect.height <= 0f) return;
                float width = Mathf.Max(1f, rect.width - 20f);
                documentTitleHeight = Mathf.Max(32f * scale, TextHeight(owner.titleContent.text, width, titleStyle));
                documentTop = documentTitleHeight + (width < 360f * scale ? 108f : 64f) * scale;
                if (contentWidth != width || measuredSource != owner.sourceMode)
                {
                    int block = 0;
                    float fraction = 0f;
                    bool preserve = contentWidth >= 0f && pending < 0 && article.Blocks.Length > 0;
                    if (preserve)
                        for (int i = 0; i < article.Blocks.Length; i++)
                        {
                            NativeBlock old = article.Blocks[i];
                            if (old.Y + documentTop > article.Scroll.Position.y) break;
                            block = i;
                            fraction = Mathf.Clamp01((article.Scroll.Position.y - documentTop - old.Y) / Mathf.Max(1f, old.Height));
                        }
                    contentHeight = documentTop + article.Measure(width, owner.sourceMode, literalStyle, richStyle, darkLinks, scale, owner.errorContent.text);
                    if (preserve) article.Scroll.SetPosition(documentTop + article.Blocks[block].Y + fraction * article.Blocks[block].Height);
                    contentWidth = width;
                    measuredSource = owner.sourceMode;
                }
                if (pending >= 0 && article.Blocks.Length > 0)
                {
                    // Top-level articles retain the title and toolbar; chapters focus their body.
                    if (owner.selectedBlock < 0) article.Scroll.SetPosition(0f);
                    else article.Scroll.FocusAnchor(Mathf.Min(pending, article.Blocks.Length - 1), 4f * scale);
                    pending = -1;
                }
                article.Scroll.Draw(rect, contentHeight, drawArticle);
            }

            private bool DrawDocumentToolbar(float width)
            {
                DocumentationArticle source = owner.catalog.Articles[activeArticle];
                context.FontSize = 18f * scale;
                context.FontStyle = FontStyle.Bold;
                context.Label(new Rect(0f, 0f, width, documentTitleHeight), owner.titleContent.text,
                    context.Palette.Ink, TextAnchor.UpperLeft, true);
                context.FontSize = 13f * scale;
                context.FontStyle = FontStyle.Normal;
                float y = documentTitleHeight + 12f * scale;
                float toolbar = 36f * scale;
                float copyWidth = Mathf.Min(144f * scale, width * 0.42f);
                bool stacked = width < 360f * scale;
                Rect toggle = new Rect(0f, y, stacked ? width : Mathf.Max(1f, width - copyWidth - 12f * scale), toolbar);
                bool previous = owner.sourceMode;
                owner.sourceMode = sourceSwitch.Draw(toggle, previous, owner.sourceLabel, scale: scale);
                context.FontSize = 14f * scale;
                Rect copy = new Rect(stacked ? 0f : width - copyWidth, stacked ? y + toolbar + 8f * scale : y,
                    stacked ? width : copyWidth, toolbar);
                if (copyButton.Draw(copy, owner.copyLabel, source.Available, variant: UiButtonVariant.Light, scale: scale))
                    GUIUtility.systemCopyBuffer = source.Markdown;
                context.Fill(new Rect(0f, documentTop - 8f * scale, width, scale), context.Palette.Line);
                if (owner.sourceMode == previous) return true;
                contentWidth = owner.documentWidth = -1f;
                owner.documentScroll = Vector2.zero;
                article.Scroll.Reset();
                pending = owner.selectedBlock < 0 ? 0 : owner.selectedBlock;
                GUI.FocusControl(null);
                return false;
            }

            private void DrawArticleContent(Rect rect)
            {
                if (!DrawDocumentToolbar(rect.width)) return;
                if (!owner.catalog.Articles[activeArticle].Available)
                {
                    context.Label(new Rect(0f, documentTop, rect.width, Mathf.Max(0f, contentHeight - documentTop)), owner.errorContent.text,
                        context.Palette.Error, TextAnchor.UpperLeft, true);
                    return;
                }
                Rect visible = article.Scroll.VisibleRect;
                for (int i = 0; i < article.Blocks.Length; i++)
                {
                    NativeBlock block = article.Blocks[i];
                    float top = documentTop + block.Y;
                    Rect section = new Rect(0f, top, rect.width, block.Height);
                    if (!owner.sourceMode)
                        for (int j = 0; j < block.Segments.Length; j++)
                        {
                            NativeSegment segment = block.Segments[j];
                            if (!segment.Literal) continue;
                            Rect code = new Rect(8f * scale, top + segment.Y - 8f * scale,
                                Mathf.Max(1f, rect.width - 16f * scale), segment.Height + 16f * scale);
                            if (code.yMax < visible.y || code.y > visible.yMax) continue;
                            context.Fill(code, Color.Lerp(context.Palette.Sunken, context.Palette.Ink, 0.045f));
                            context.Border(code, context.Palette.StrongLine, scale);
                        }
                    // Section tint is one layer over every surface, including fenced code.
                    article.Scroll.Anchor(i, section);
                    if (top + block.Height < visible.y || top > visible.yMax) continue;
                    if (owner.sourceMode)
                        DrawText(section, block.Source, literalStyle);
                    else
                        for (int j = 0; j < block.Segments.Length; j++)
                        {
                            NativeSegment segment = block.Segments[j];
                            Rect bounds = new Rect(segment.Literal ? 20f * scale : 0f, top + segment.Y,
                                Mathf.Max(1f, rect.width - (segment.Literal ? 40f * scale : 0f)), segment.Height);
                            if (bounds.yMax < visible.y || bounds.y > visible.yMax) continue;
                            DrawText(bounds, segment.Content, segment.Literal ? literalStyle : richStyle);
                        }
                }
            }

            private void DrawText(Rect rect, GUIContent content, GUIStyle style)
            {
                if (!context.IsRepaint) return;
                Color ink = context.Palette.Ink;
                ink.a *= context.Alpha;
                UnityStubs.Gui.GuiBackend.Current?.DrawText(rect, content.text, style.FontIndex, style.fontSize,
                    style.fontStyle, style.alignment, style.wordWrap, ink, default, style.richText);
            }

            public override void DrawOverlay(Rect viewport)
            {
                if (string.IsNullOrEmpty(tooltipText)) return;
                float old = context.FontSize;
                context.FontSize = 13f * scale;
                try
                {
                    float width = Mathf.Min(380f * scale, viewport.width);
                    float height = Mathf.Min(viewport.height, TextHeight(tooltipText, Mathf.Max(1f, width - 20f * scale), navigationStyle) + 20f * scale);
                    tooltip.DrawOverlay(viewport, new Vector2(width, height), scale);
                }
                finally { context.FontSize = old; }
            }

            public override void Dispose()
            {
                context.Release(indexButton.Id);
                context.Release(copyButton.Id);
                context.Release(sourceSwitch.Id);
                context.Release(indexScroll.Id);
                for (int i = 0; i < navigation.Length; i++)
                    for (int j = 0; j < navigation[i].Length; j++) context.Release(navigation[i][j].Id);
                for (int i = 0; i < articles.Length; i++)
                    if (articles[i] != null)
                    {
                        context.Release(articles[i].Id);
                        context.Release(articles[i].Scroll.Id);
                    }
                tooltip.Dismiss();
            }

            private sealed class NativeNavigationRow
            {
                internal readonly UiId Id;
                internal readonly int Article, Block, Anchor;
                internal string Label;
                internal Rect Rect, TextRect;
                internal UiTween Hover, Selection;
                internal NativeNavigationRow(UiContext context, int article, int block, int anchor)
                { Id = context.CreateId(); Article = article; Block = block; Anchor = anchor; }
            }

            private sealed class NativeArticle
            {
                internal readonly UiId Id;
                internal readonly UiScrollView Scroll;
                internal readonly NativeBlock[] Blocks;
                internal NativeArticle(UiContext context, DocumentationArticle source)
                {
                    Id = context.CreateId();
                    Scroll = new UiScrollView(context, source.Blocks.Length);
                    Blocks = new NativeBlock[source.Blocks.Length];
                    for (int i = 0; i < Blocks.Length; i++) Blocks[i] = new NativeBlock(source.Blocks[i].Markdown);
                }
                internal float Measure(float width, bool source, GUIStyle literal, GUIStyle rich, bool dark, float scale, string error)
                {
                    if (Blocks.Length == 0) return literal.CalcHeight(new GUIContent(error), width);
                    float y = 0f;
                    for (int i = 0; i < Blocks.Length; i++)
                    {
                        NativeBlock block = Blocks[i];
                        block.Y = y;
                        block.Height = 0f;
                        if (source) block.Height = literal.CalcHeight(block.Source, width);
                        else
                            for (int j = 0; j < block.Segments.Length; j++)
                            {
                                NativeSegment segment = block.Segments[j];
                                segment.Content.text = segment.Literal ? segment.Text : segment.Text.Replace("<color=#66b3ffff>", dark ? "<color=#9dd2ffff>" : "<color=#25587bff>");
                                if (segment.Literal) block.Height += 10f * scale;
                                segment.Y = block.Height;
                                segment.Height = (segment.Literal ? literal : rich).CalcHeight(segment.Content,
                                    Mathf.Max(1f, width - (segment.Literal ? 40f * scale : 0f)));
                                block.Height += segment.Height + (segment.Literal ? 14f : 4f) * scale;
                            }
                        y += block.Height + 12f * scale;
                    }
                    return y;
                }
            }

            private sealed class NativeBlock
            {
                internal readonly GUIContent Source;
                internal readonly NativeSegment[] Segments;
                internal float Y, Height;
                internal NativeBlock(string source)
                {
                    Source = new GUIContent(source);
                    Segment[] parsed = SplitSegments(source);
                    Segments = new NativeSegment[parsed.Length];
                    for (int i = 0; i < parsed.Length; i++)
                        Segments[i] = new NativeSegment(parsed[i].Markdown == null ? parsed[i].Plain.text : parsed[i].Markdown.RenderedText, parsed[i].Markdown == null);
                }
            }

            private sealed class NativeSegment
            {
                internal readonly string Text;
                internal readonly bool Literal;
                internal readonly GUIContent Content;
                internal float Y, Height;
                internal NativeSegment(string text, bool literal)
                { Text = text; Literal = literal; Content = new GUIContent(text); }
            }
        }
    }
}
