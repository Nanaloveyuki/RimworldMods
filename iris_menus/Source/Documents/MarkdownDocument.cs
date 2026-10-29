using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    /// <summary>Cached Markdown content with source-line positioning for document navigation.</summary>
    public sealed class MarkdownDocument : IInteractiveMarkdown
    {
        private readonly Func<string> source;
        private readonly IMarkdownParser parser;
        private readonly List<MarkdownLink> links = new List<MarkdownLink>();
        private readonly List<string> renderedLines = new List<string>();
        private string markdownText = string.Empty;
        private string renderedText = string.Empty;
        private float measuredWidth = -1f;
        private float[] lineOffsets = Array.Empty<float>();
        private float measuredHeight;
        private bool parsed;

        public MarkdownDocument(string markdown) : this(() => markdown) { }

        public MarkdownDocument(Func<string> source) : this(source, null) { }

        public MarkdownDocument(Func<string> source, IMarkdownParser parser)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.parser = parser ?? new MarkdownParser();
        }

        public string MarkdownText { get { Refresh(); return markdownText; } }
        public string RenderedText { get { Refresh(); return renderedText; } }
        public IReadOnlyList<MarkdownLink> Links { get { Refresh(); return links; } }
        public int LineCount { get { Refresh(); return renderedLines.Count; } }

        public float Measure(float width)
        {
            Refresh();
            EnsureGeometry(width);
            return measuredHeight;
        }

        public void Draw(Rect rect)
        {
            Refresh();
            MarkdownRendering.Draw(rect, renderedText);
        }

        public void Draw(Rect rect, Action<DocumentUri> openLink)
        {
            if (openLink == null) throw new ArgumentNullException(nameof(openLink));
            Refresh();
            MarkdownRendering.Draw(rect, renderedText);
            MarkdownRendering.ActivateLinks(rect, renderedText, links, new MarkdownLinkGeometry(), openLink);
        }

        /// <summary>Returns the top offset of a one-based source line at the requested width.</summary>
        public float LineOffset(int line, float width)
        {
            Refresh();
            EnsureGeometry(width);
            if (line <= 1 || lineOffsets.Length == 0) return 0f;
            if (line > lineOffsets.Length) return measuredHeight;
            return lineOffsets[line - 1];
        }

        public bool TryGetLinePosition(float width, int line, out float y)
        {
            y = LineOffset(line, width);
            return line >= 1 && line <= LineCount;
        }

        private void Refresh()
        {
            string current = source() ?? string.Empty;
            if (parsed && string.Equals(current, markdownText, StringComparison.Ordinal)) return;
            markdownText = current;
            links.Clear();
            renderedText = parser is MarkdownParser
                ? MarkdownParser.ParseDocument(current, links)
                : parser.Parse(current) ?? string.Empty;
            if (!(parser is MarkdownParser)) links.Clear();
            renderedLines.Clear();
            SplitLines(renderedText, renderedLines);
            measuredWidth = -1f;
            lineOffsets = Array.Empty<float>();
            measuredHeight = 0f;
            parsed = true;
        }

        private void EnsureGeometry(float width)
        {
            width = Mathf.Max(1f, width);
            if (measuredWidth >= 0f && Math.Abs(width - measuredWidth) <= 0.01f) return;
            measuredWidth = width;
            lineOffsets = new float[renderedLines.Count];
            float y = 0f;
            for (int i = 0; i < renderedLines.Count; i++)
            {
                lineOffsets[i] = y;
                string line = renderedLines[i];
                float height = string.IsNullOrEmpty(line) ? Text.CalcHeight(" ", width) : Text.CalcHeight(line, width);
                y += Mathf.Max(1f, height);
            }
            measuredHeight = y;
        }

        private static void SplitLines(string text, List<string> output)
        {
            if (string.IsNullOrEmpty(text)) return;
            int start = 0;
            while (start <= text.Length)
            {
                int end = text.IndexOf('\n', start);
                if (end < 0) end = text.Length;
                output.Add(text.Substring(start, end - start));
                if (end == text.Length) break;
                start = end + 1;
            }
        }
    }
}
