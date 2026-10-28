using System;
using System.Collections.Generic;
using IrisMenus;
using UnityEngine;

namespace IrisMenus.UI
{
    /// <summary>Native-surface adapter for MarkdownDocument with cached wrapped styled spans.</summary>
    public sealed class UiDocument
    {
        private readonly UiContext context;
        private readonly IMarkdown content;
        private readonly List<Row> rows = new List<Row>();
        private readonly List<Rect> codeSurfaces = new List<Rect>();
        private string cachedText;
        private float cachedWidth = -1f;
        private float cachedFont = -1f;
        private FontStyle cachedStyle;
        private float height;

        public UiDocument(UiContext context, IMarkdown content)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public float Measure(float width)
        {
            EnsureLayout(width);
            return height;
        }

        public void Draw(Rect rect, Action<DocumentUri> openLink = null)
        {
            EnsureLayout(rect.width);
            for (int i = 0; i < codeSurfaces.Count; i++)
            {
                Rect surface = codeSurfaces[i];
                surface.x += rect.x;
                surface.y += rect.y;
                context.Fill(surface, Color.Lerp(context.Palette.Sunken, context.Palette.Ink, 0.045f));
                context.Border(surface, context.Palette.StrongLine);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                Rect rowRect = new Rect(rect.x, rect.y + row.Y, rect.width, row.Height);
                if (rowRect.yMax < rect.y || rowRect.y > rect.yMax) continue;
                float x = rowRect.x + row.Indent;
                for (int j = 0; j < row.Spans.Count; j++)
                {
                    Span span = row.Spans[j];
                    float oldSize = context.FontSize;
                    FontStyle oldStyle = context.FontStyle;
                    context.FontSize = oldSize * span.Scale;
                    context.FontStyle = span.Bold ? FontStyle.Bold : (span.Italic ? FontStyle.Italic : FontStyle.Normal);
                    float width = UiTextMetrics.Width(context, span.Text);
                    Rect spanRect = new Rect(x, rowRect.y, width, rowRect.height);
                    Color color = span.LinkIndex >= 0 ? context.Palette.Signal : context.Palette.Ink;
                    if (span.Code) color = context.Palette.Ink;
                    context.Label(spanRect, span.Text, color, TextAnchor.UpperLeft, false);
                    context.FontSize = oldSize;
                    context.FontStyle = oldStyle;
                    if (span.LinkIndex >= 0 && openLink != null && context.Event != null &&
                        context.Event.type == EventType.MouseUp && context.Event.button == 0 && GUI.enabled && context.Hit(spanRect))
                    {
                        context.Consume();
                        openLink(span.Uri);
                    }
                    x += width;
                }
            }
        }

        public float LineOffset(int line, float width)
        {
            EnsureLayout(width);
            for (int i = 0; i < rows.Count; i++) if (rows[i].SourceLine >= line) return rows[i].Y;
            return height;
        }

        public bool TryGetLinePosition(float width, int line, out float y)
        {
            y = LineOffset(line, width);
            return line > 0 && rows.Count > 0 && line <= rows[rows.Count - 1].SourceLine;
        }

        private void EnsureLayout(float width)
        {
            width = Mathf.Max(1f, width);
            string text = content.RenderedText ?? string.Empty;
            if (Mathf.Approximately(cachedWidth, width) && cachedFont == context.FontSize &&
                cachedStyle == context.FontStyle &&
                string.Equals(cachedText, text, StringComparison.Ordinal)) return;
            cachedWidth = width;
            cachedFont = context.FontSize;
            cachedStyle = context.FontStyle;
            cachedText = text;
            rows.Clear();
            codeSurfaces.Clear();
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            float y = 0f, codeTop = 0f;
            char fence = '\0';
            int fenceLength = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int start = 0;
                while (start < line.Length && start < 3 && line[start] == ' ') start++;
                char marker = start < line.Length ? line[start] : '\0';
                int end = start;
                if (marker == '`' || marker == '~')
                    while (end < line.Length && line[end] == marker) end++;
                int length = end - start;
                bool boundary = length >= 3 && (fence == '\0' || marker == fence && length >= fenceLength);
                if (boundary)
                {
                    rows.Add(new Row(i + 1, y, 0f, 0f));
                    if (fence == '\0') { fence = marker; fenceLength = length; codeTop = y; y += 12f; }
                    else
                    {
                        y += 12f;
                        codeSurfaces.Add(new Rect(0f, codeTop, width, y - codeTop));
                        fence = '\0';
                    }
                    continue;
                }
                bool literal = fence != '\0';
                List<Span> spans = literal
                    ? new List<Span> { new Span(line, false, false, true, -1, default(DocumentUri)) }
                    : ParseSpans(line, i + 1);
                AddWrapped(spans, i + 1, width, ref y, literal ? 12f : 0f);
            }
            if (fence != '\0')
            {
                y += 12f;
                codeSurfaces.Add(new Rect(0f, codeTop, width, y - codeTop));
            }
            height = y;
        }

        private void AddWrapped(List<Span> spans, int sourceLine, float width, ref float y, float indent = 0f)
        {
            float lineHeight = Mathf.Max(16f, context.FontSize * 1.35f);
            Row row = new Row(sourceLine, y, lineHeight, indent);
            width = Mathf.Max(1f, width - indent * 2f);
            float used = 0f;
            for (int i = 0; i < spans.Count; i++)
            {
                Span span = spans[i];
                int start = 0;
                while (start < span.Text.Length)
                {
                    float available = width - used;
                    int take = Fit(span.Text, start, available, span.Scale);
                    if (take <= 0 && used > 0f)
                    {
                        rows.Add(row);
                        y += lineHeight;
                        row = new Row(sourceLine, y, lineHeight, indent);
                        used = 0f;
                        continue;
                    }
                    if (take <= 0) take = 1;
                    string piece = span.Text.Substring(start, take);
                    row.Spans.Add(new Span(piece, span.Bold, span.Italic, span.Code, span.LinkIndex, span.Uri) { Scale = span.Scale });
                    used += TextWidth(piece, span.Scale);
                    start += take;
                    if (start < span.Text.Length && used >= width)
                    {
                        rows.Add(row);
                        y += lineHeight;
                        row = new Row(sourceLine, y, lineHeight, indent);
                        used = 0f;
                    }
                }
            }
            if (row.Spans.Count == 0) row.Spans.Add(new Span(string.Empty, false, false, false, -1, default(DocumentUri)));
            rows.Add(row);
            y += lineHeight;
        }

        private int Fit(string text, int start, float available, float scale)
        {
            if (available <= 0f) return 0;
            int count = 0;
            while (start + count < text.Length)
            {
                float next = TextWidth(text.Substring(start + count, 1), scale);
                if (count > 0 && next > available) break;
                count++;
                available -= next;
            }
            return count;
        }

        private float TextWidth(string text, float scale)
        {
            float old = context.FontSize;
            context.FontSize = old * scale;
            float result = UiTextMetrics.Width(context, text);
            context.FontSize = old;
            return result;
        }

        private List<Span> ParseSpans(string line, int sourceLine)
        {
            var spans = new List<Span>();
            bool bold = false, italic = false;
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '<')
                {
                    int end = line.IndexOf('>', i + 1);
                    if (end >= 0)
                    {
                        string tag = line.Substring(i, end - i + 1);
                        if (tag == "<b>") bold = true;
                        else if (tag == "</b>") bold = false;
                        else if (tag == "<i>") italic = true;
                        else if (tag == "</i>") italic = false;
                        else if (tag.StartsWith("<color=#66b3ffff>", StringComparison.OrdinalIgnoreCase)) { }
                        else if (tag == "</color>") { }
                        i = end + 1;
                        continue;
                    }
                }
                int next = i + 1;
                while (next < line.Length && line[next] != '<') next++;
                string value = line.Substring(i, next - i);
                int link = LinkIndex(value, sourceLine);
                DocumentUri uri = link >= 0 ? LinkUri(value, sourceLine) : default(DocumentUri);
                spans.Add(new Span(value, bold, italic, false, link, uri));
                i = next;
            }
            if (spans.Count == 0) spans.Add(new Span(line, false, false, false, -1, default(DocumentUri)));
            return spans;
        }

        private static int LinkLine(string source, int offset)
        {
            int line = 1;
            int end = Math.Min(Math.Max(0, offset), source == null ? 0 : source.Length);
            for (int i = 0; i < end; i++) if (source[i] == '\n') line++;
            return line;
        }

        private int LinkIndex(string label, int line)
        {
            if (!(content is IInteractiveMarkdown interactive)) return -1;
            IReadOnlyList<MarkdownLink> links = interactive.Links;
            string source = interactive.MarkdownText;
            for (int i = 0; i < links.Count; i++)
                if (links[i].Label == label && LinkLine(source, links[i].Line) == line) return i;
            return -1;
        }

        private DocumentUri LinkUri(string label, int line)
        {
            if (!(content is IInteractiveMarkdown interactive)) return default(DocumentUri);
            IReadOnlyList<MarkdownLink> links = interactive.Links;
            string source = interactive.MarkdownText;
            for (int i = 0; i < links.Count; i++)
                if (links[i].Label == label && LinkLine(source, links[i].Line) == line) return links[i].Uri;
            return default(DocumentUri);
        }

        private sealed class Row
        {
            public Row(int sourceLine, float y, float height, float indent) { SourceLine = sourceLine; Y = y; Height = height; Indent = indent; }
            public int SourceLine; public float Y; public float Height; public float Indent; public readonly List<Span> Spans = new List<Span>();
        }
        private sealed class Span
        {
            public Span(string text, bool bold, bool italic, bool code, int linkIndex, DocumentUri uri) { Text = text; Bold = bold; Italic = italic; Code = code; LinkIndex = linkIndex; Uri = uri; }
            public string Text; public bool Bold; public bool Italic; public bool Code; public int LinkIndex; public DocumentUri Uri; public float Scale = 1f;
        }
    }
}
namespace IrisMenus
{
    internal sealed class DocumentNativePage : MenuNativePage
    {
        private readonly UI.UiContext context;
        private readonly DocumentItem item;
        private readonly UI.UiDocument document;
        private readonly UI.UiScrollView scroll;
        private int pendingLine;

        internal DocumentNativePage(UI.UiContext context, DocumentItem item)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            document = item.Content == null ? null : new UI.UiDocument(context, item.Content);
            scroll = new UI.UiScrollView(context);
        }

        public override void Focus(string anchor)
        {
            if (!int.TryParse(anchor, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int line) || line < 1) return;
            pendingLine = line;
        }

        public override void Draw(Rect rect)
        {
            float width = Mathf.Max(1f, rect.width - 20f);
            float height = document != null ? document.Measure(width) : item.Measure(width);
            if (pendingLine > 0)
            {
                float offset;
                bool found = document != null ? document.TryGetLinePosition(width, pendingLine, out offset)
                    : item.TryGetLinePosition(width, pendingLine, out offset);
                if (found) scroll.SetPosition(offset);
                pendingLine = 0;
            }
            scroll.Draw(rect, height, contentRect =>
            {
                Rect drawRect = new Rect(contentRect.x, contentRect.y, width, height);
                if (document != null) document.Draw(drawRect, uri => MenuRegistry.OpenUri(uri));
                else item.Draw(drawRect, uri => MenuRegistry.OpenUri(uri));
            });
        }
    }
}
