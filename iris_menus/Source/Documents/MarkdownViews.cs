using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    /// <summary>Read-only rendered Markdown content that can be measured and drawn by a settings page.</summary>
    public interface IMarkdown
    {
        string MarkdownText { get; }
        string RenderedText { get; }
        float Measure(float width);
        void Draw(Rect rect);
    }

    /// <summary>Markdown that can expose and activate registered IrisMenus document links.</summary>
    public interface IInteractiveMarkdown : IMarkdown
    {
        IReadOnlyList<MarkdownLink> Links { get; }
        void Draw(Rect rect, Action<DocumentUri> openLink);
    }


    /// <summary>A parsed Markdown link whose target passed the internal document URI validation.</summary>
    public sealed class MarkdownLink
    {
        internal MarkdownLink(string label, DocumentUri uri, int line, int column, int renderedStart, int renderedEnd)
        {
            Label = label;
            Uri = uri;
            Line = line;
            Column = column;
            RenderedStart = renderedStart;
            RenderedEnd = renderedEnd;
        }

        public string Label { get; }
        public DocumentUri Uri { get; }
        public int Line { get; }
        public int Column { get; }
        internal int RenderedStart { get; }
        internal int RenderedEnd { get; }
    }


    public sealed class DynamicMarkdown : IInteractiveMarkdown
    {
        private readonly Func<string> getMarkdown;
        private readonly IMarkdownParser parser;
        private bool hasCache;
        private string markdownText = string.Empty;
        private string renderedText = string.Empty;
        private readonly List<MarkdownLink> links = new List<MarkdownLink>();
        private readonly MarkdownLinkGeometry geometry = new MarkdownLinkGeometry();

        public DynamicMarkdown(Func<string> getMarkdown) : this(getMarkdown, null) { }

        public DynamicMarkdown(Func<string> getMarkdown, IMarkdownParser parser)
        {
            this.getMarkdown = getMarkdown ?? throw new ArgumentNullException(nameof(getMarkdown));
            this.parser = parser ?? new MarkdownParser();
        }

        public string MarkdownText { get { Refresh(); return markdownText; } }
        public string RenderedText { get { Refresh(); return renderedText; } }
        public IReadOnlyList<MarkdownLink> Links { get { Refresh(); return links; } }
        public float Measure(float width) { Refresh(); return MarkdownRendering.Measure(renderedText, width); }
        public void Draw(Rect rect) { Refresh(); MarkdownRendering.Draw(rect, renderedText); }
        public void Draw(Rect rect, Action<DocumentUri> openLink)
        {
            if (openLink == null) throw new ArgumentNullException(nameof(openLink));
            Refresh();
            MarkdownRendering.Draw(rect, renderedText);
            MarkdownRendering.ActivateLinks(rect, renderedText, links, geometry, openLink);
        }

        private void Refresh()
        {
            string current = getMarkdown() ?? string.Empty;
            if (hasCache && string.Equals(current, markdownText, StringComparison.Ordinal)) return;
            markdownText = current;
            links.Clear();
            string builtIn = MarkdownParser.ParseDocument(current, links);
            renderedText = parser is MarkdownParser ? builtIn : parser.Parse(current) ?? string.Empty;
            if (!string.Equals(renderedText, builtIn, StringComparison.Ordinal)) links.Clear();
            hasCache = true;
        }
    }

    /// <summary>Markdown loaded once from an external UTF-8 file; the file is never written or watched.</summary>
    public sealed class StaticMarkdown : IInteractiveMarkdown
    {
        private readonly IMarkdownParser parser;
        private readonly MarkdownLinkGeometry geometry = new MarkdownLinkGeometry();

        public StaticMarkdown(string filePath) : this(filePath, null) { }

        public StaticMarkdown(string filePath, IMarkdownParser parser)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A Markdown file path is required.", nameof(filePath));
            FilePath = Path.GetFullPath(filePath);
            MarkdownText = File.ReadAllText(FilePath);
            this.parser = parser ?? new MarkdownParser();
            var links = new List<MarkdownLink>();
            string builtIn = MarkdownParser.ParseDocument(MarkdownText, links);
            RenderedText = this.parser is MarkdownParser ? builtIn : this.parser.Parse(MarkdownText) ?? string.Empty;
            if (!string.Equals(RenderedText, builtIn, StringComparison.Ordinal)) links.Clear();
            Links = links;
        }

        public string FilePath { get; }
        public string MarkdownText { get; }
        public string RenderedText { get; }
        public IReadOnlyList<MarkdownLink> Links { get; }
        public float Measure(float width) => MarkdownRendering.Measure(RenderedText, width);
        public void Draw(Rect rect) => MarkdownRendering.Draw(rect, RenderedText);
        public void Draw(Rect rect, Action<DocumentUri> openLink)
        {
            if (openLink == null) throw new ArgumentNullException(nameof(openLink));
            MarkdownRendering.Draw(rect, RenderedText);
            MarkdownRendering.ActivateLinks(rect, RenderedText, Links, geometry, openLink);
        }
    }

    /// <summary>Factories and the pure conversion entry point for the built-in Markdown implementation.</summary>
    public static class Markdown
    {
        public static string Parse(string markdown) => new MarkdownParser().Parse(markdown);

        public static MarkdownDocument Document(string markdown) => new MarkdownDocument(markdown);
        public static MarkdownDocument Document(Func<string> markdown) => new MarkdownDocument(markdown);

        public static IMarkdown Dynamic(Func<string> getMarkdown, IMarkdownParser parser = null)
        {
            return new DynamicMarkdown(getMarkdown, parser);
        }

        public static IInteractiveMarkdown Interactive(Func<string> getMarkdown, IMarkdownParser parser = null)
        {
            return new DynamicMarkdown(getMarkdown, parser);
        }

        public static IMarkdown StaticFile(string filePath, IMarkdownParser parser = null)
        {
            return new StaticMarkdown(filePath, parser);
        }

        public static IInteractiveMarkdown InteractiveFile(string filePath, IMarkdownParser parser = null)
        {
            return new StaticMarkdown(filePath, parser);
        }
    }

    internal sealed class MarkdownLinkGeometry
    {
#if RIMWORLD_1_7
        private readonly List<Rect> selection = new List<Rect>();
        public bool CanHit => UnityStubs.Gui.GuiBackend.Current != null &&
            !UnityStubs.Gui.GuiBackend.Current.MouseOutsideHitClip &&
            !UnityStubs.Gui.GuiBackend.Current.InlineControlsObscured;

        public bool Contains(Rect rect, string rendered, int start, int end, Vector2 mouse)
        {
            GUIStyle style = Text.CurFontStyle;
            var backend = UnityStubs.Gui.GuiBackend.Current;
            float width = Mathf.Max(1f, rect.width - style.padding.horizontal);
            backend.TextSelectionRects(style.FontIndex, style.fontSize, style.fontStyle, rendered,
                width, start, end, selection, style.richText);
            Vector2 local = new Vector2(mouse.x - rect.x - style.padding.left - style.contentOffset.x,
                mouse.y - rect.y - style.padding.top - style.contentOffset.y);
            for (int i = 0; i < selection.Count; i++)
                if (selection[i].Contains(local)) return true;
            return false;
        }
#else
        private readonly GUIContent content = new GUIContent();
        private readonly GUIContent character = new GUIContent();
        public bool CanHit => true;

        public bool Contains(Rect rect, string rendered, int start, int end, Vector2 mouse)
        {
            GUIStyle style = Text.CurFontStyle;
            TextAnchor anchor = style.alignment;
            bool wrap = style.wordWrap;
            try
            {
                style.alignment = TextAnchor.UpperLeft;
                style.wordWrap = true;
                content.text = rendered;
                for (int i = start; i < end; i++)
                {
                    if (rendered[i] == '<')
                    {
                        int close = rendered.IndexOf('>', i + 1);
                        if (close >= 0 && close < end) { i = close; continue; }
                    }
                    Vector2 from = style.GetCursorPixelPosition(rect, content, i);
                    Vector2 to = style.GetCursorPixelPosition(rect, content, i + 1);
                    character.text = rendered.Substring(i, 1);
                    float width = to.y == from.y ? to.x - from.x : style.CalcSize(character).x;
                    if (new Rect(from.x, from.y, width, style.lineHeight).Contains(mouse)) return true;
                }
                return false;
            }
            finally { style.alignment = anchor; style.wordWrap = wrap; }
        }
#endif
    }

    internal static class MarkdownRendering
    {
        public static float Measure(string renderedText, float width)
        {
            return Text.CalcHeight(renderedText ?? string.Empty, Mathf.Max(1f, width));
        }

        public static void Draw(Rect rect, string renderedText)
        {
            TextAnchor anchor = Text.Anchor;
            bool wrap = Text.WordWrap;
            try
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Widgets.Label(rect, renderedText ?? string.Empty);
            }
            finally { Text.Anchor = anchor; Text.WordWrap = wrap; }
        }
        public static void ActivateLinks(Rect rect, string rendered, IReadOnlyList<MarkdownLink> links,
            MarkdownLinkGeometry geometry, Action<DocumentUri> openLink)
        {
            Event input = Event.current;
            if (input == null || input.type != EventType.MouseUp || input.button != 0 || !GUI.enabled ||
                !rect.Contains(input.mousePosition) || !geometry.CanHit) return;
            for (int i = 0; i < links.Count; i++)
            {
                MarkdownLink link = links[i];
                if (!geometry.Contains(rect, rendered, link.RenderedStart, link.RenderedEnd, input.mousePosition)) continue;
                input.Use();
                openLink(link.Uri);
                return;
            }
        }
    }
}
