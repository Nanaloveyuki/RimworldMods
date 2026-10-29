using System;
using System.Collections.Generic;
using System.Text;

namespace IrisMenus
{
    /// <summary>Converts Markdown source into RimWorld/Unity rich text.</summary>
    public interface IMarkdownParser
    {
        string Parse(string markdown);
    }

    /// <summary>
    /// Parses the Markdown subset intended for RimWorld settings and help text.
    /// Existing RimWorld color tags are passed through unchanged.
    /// </summary>
    public sealed class MarkdownParser : IMarkdownParser
    {
        public string Parse(string markdown) => ParseDocument(markdown, null);

        internal static string ParseDocument(string markdown, List<MarkdownLink> links)
        {
            if (string.IsNullOrEmpty(markdown)) return string.Empty;

            var result = new StringBuilder(markdown.Length + 16);
            int lineStart = 0;
            bool inFence = false;
            char fenceMarker = '\0';
            int fenceLength = 0;
            while (lineStart <= markdown.Length)
            {
                int lineEnd = markdown.IndexOf('\n', lineStart);
                bool hasNewline = lineEnd >= 0;
                if (!hasNewline) lineEnd = markdown.Length;

                int contentEnd = lineEnd;
                if (contentEnd > lineStart && markdown[contentEnd - 1] == '\r') contentEnd--;
                string line = markdown.Substring(lineStart, contentEnd - lineStart);
                if (TryFence(line, out char marker, out int length))
                {
                    if (!inFence)
                    {
                        inFence = true;
                        fenceMarker = marker;
                        fenceLength = length;
                    }
                    else if (marker == fenceMarker && length >= fenceLength)
                    {
                        inFence = false;
                    }
                    result.Append(line);
                }
                else if (inFence)
                {
                    // Fenced content is literal: emphasis and rich-text tags are not parsed.
                    result.Append(line);
                }
                else
                {
                    AppendBlockLine(result, line, links, lineStart);
                }

                if (!hasNewline) break;
                result.Append('\n');
                lineStart = lineEnd + 1;
            }
            return result.ToString();
        }

        private static bool TryFence(string line, out char marker, out int length)
        {
            marker = '\0';
            length = 0;
            int start = 0;
            while (start < line.Length && start < 3 && line[start] == ' ') start++;
            if (start >= line.Length || (line[start] != '`' && line[start] != '~')) return false;
            marker = line[start];
            int cursor = start;
            while (cursor < line.Length && line[cursor] == marker) cursor++;
            length = cursor - start;
            return length >= 3;
        }

        private static void AppendBlockLine(StringBuilder result, string line, List<MarkdownLink> links, int lineStart)
        {
            int start = 0;
            int leadingSpaces = 0;
            while (start < line.Length)
            {
                if (line[start] == ' ' && leadingSpaces < 3)
                {
                    start++;
                    leadingSpaces++;
                    continue;
                }
                if (TryRichTextTag(line, start, out int tagLength, out _))
                {
                    start += tagLength;
                    continue;
                }
                break;
            }

            int headingEnd = start;
            while (headingEnd < line.Length && line[headingEnd] == '#') headingEnd++;
            if (headingEnd > start && headingEnd - start <= 6 &&
                headingEnd < line.Length && char.IsWhiteSpace(line[headingEnd]))
            {
                int headingStart = headingEnd;
                while (headingStart < line.Length && char.IsWhiteSpace(line[headingStart])) headingStart++;
                string heading = TrimClosingHeadingMarkers(line.Substring(headingStart).TrimEnd());
                result.Append("<b>");
                result.Append(ParseInline(line.Substring(0, start), links, result.Length, lineStart, 0));
                result.Append(ParseInline(heading, links, result.Length, lineStart, headingStart));
                result.Append("</b>");
                return;
            }

            if (start < line.Length && (line[start] == '-' || line[start] == '+' || line[start] == '*') &&
                start + 1 < line.Length && char.IsWhiteSpace(line[start + 1]))
            {
                result.Append(ParseInline(line.Substring(0, start), links, result.Length, lineStart, 0));
                result.Append("- ");
                int contentStart = start + 1;
                while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart])) contentStart++;
                result.Append(ParseInline(line.Substring(contentStart), links, result.Length, lineStart, contentStart));
                return;
            }

            int orderedEnd = start;
            while (orderedEnd < line.Length && char.IsDigit(line[orderedEnd])) orderedEnd++;
            if (orderedEnd > start && orderedEnd < line.Length && line[orderedEnd] == '.' &&
                orderedEnd + 1 < line.Length && char.IsWhiteSpace(line[orderedEnd + 1]))
            {
                result.Append(ParseInline(line.Substring(0, start), links, result.Length, lineStart, 0));
                result.Append(line.Substring(start, orderedEnd - start + 1));
                result.Append(' ');
                int contentStart = orderedEnd + 1;
                while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart])) contentStart++;
                result.Append(ParseInline(line.Substring(contentStart), links, result.Length, lineStart, contentStart));
                return;
            }

            if (start < line.Length && line[start] == '>' &&
                start + 1 < line.Length && char.IsWhiteSpace(line[start + 1]))
            {
                result.Append(ParseInline(line.Substring(0, start), links, result.Length, lineStart, 0));
                result.Append("> ");
                int contentStart = start + 1;
                while (contentStart < line.Length && char.IsWhiteSpace(line[contentStart])) contentStart++;
                result.Append(ParseInline(line.Substring(contentStart), links, result.Length, lineStart, contentStart));
                return;
            }

            result.Append(ParseInline(line, links, result.Length, lineStart, 0));
        }

        private static string TrimClosingHeadingMarkers(string heading)
        {
            int markerStart = heading.Length;
            while (markerStart > 0 && heading[markerStart - 1] == '#') markerStart--;
            if (markerStart == heading.Length || markerStart == 0 || !char.IsWhiteSpace(heading[markerStart - 1]))
                return heading;
            return heading.Substring(0, markerStart).TrimEnd();
        }

        private static string ParseInline(string text, List<MarkdownLink> links, int renderedOffset, int lineStart, int column)
        {
            var result = new StringBuilder(text.Length + 8);
            int index = 0;
            while (index < text.Length)
            {
                if (TryRichTextTag(text, index, out int tagLength, out RichTextTagKind kind))
                {
                    if (kind != RichTextTagKind.Unsupported)
                        result.Append(text, index, tagLength);
                    index += tagLength;
                    continue;
                }

                if (text[index] == '\\' && index + 1 < text.Length && IsMarkdownEscape(text[index + 1]))
                {
                    result.Append(text[index + 1]);
                    index += 2;
                    continue;
                }

                if (text[index] == '[')
                {
                    int labelEnd = text.IndexOf(']', index + 1);
                    if (labelEnd > index + 1 && labelEnd + 1 < text.Length && text[labelEnd + 1] == '('
                        && text.IndexOf(')', labelEnd + 2) is int destinationEnd && destinationEnd > labelEnd + 2)
                    {
                        string label = text.Substring(index + 1, labelEnd - index - 1);
                        result.Append("<color=#66b3ffff>");
                        int renderedStart = renderedOffset + result.Length;
                        result.Append(ParseInline(label, null, 0, lineStart, column + index + 1));
                        if (links != null && DocumentUri.TryParse(text.Substring(labelEnd + 2, destinationEnd - labelEnd - 2), out DocumentUri uri))
                            links.Add(new MarkdownLink(label, uri, lineStart, column + index,
                                renderedStart, renderedOffset + result.Length));
                        result.Append("</color>");
                        index = destinationEnd + 1;
                        continue;
                    }
                }

                if (StartsWith(text, index, "**") && IsStandaloneDelimiter(text, index, 2) &&
                    IsOpeningDelimiter(text, index, 2))
                {
                    int closing = FindClosingDelimiter(text, index + 2, "**");
                    if (closing >= 0)
                    {
                        result.Append("<b>");
                        result.Append(ParseInline(text.Substring(index + 2, closing - index - 2), links,
                            renderedOffset + result.Length, lineStart, column + index + 2));
                        result.Append("</b>");
                        index = closing + 2;
                        continue;
                    }
                }

                if ((text[index] == '*' || text[index] == '_') &&
                    IsStandaloneDelimiter(text, index, 1) && IsOpeningDelimiter(text, index, 1))
                {
                    string delimiter = text[index].ToString();
                    int closing = FindClosingDelimiter(text, index + 1, delimiter);
                    if (closing >= 0)
                    {
                        result.Append("<i>");
                        result.Append(ParseInline(text.Substring(index + 1, closing - index - 1), links,
                            renderedOffset + result.Length, lineStart, column + index + 1));
                        result.Append("</i>");
                        index = closing + 1;
                        continue;
                    }
                }

                result.Append(text[index]);
                index++;
            }
            return result.ToString();
        }

        private static int FindClosingDelimiter(string text, int start, string delimiter)
        {
            for (int index = start; index <= text.Length - delimiter.Length; index++)
            {
                if (TryRichTextTag(text, index, out int tagLength, out _))
                {
                    index += tagLength - 1;
                    continue;
                }
                if (text[index] == '\\' && index + 1 < text.Length && IsMarkdownEscape(text[index + 1]))
                {
                    index++;
                    continue;
                }
                if (StartsWith(text, index, delimiter) && IsStandaloneDelimiter(text, index, delimiter.Length) &&
                    IsClosingDelimiter(text, index, delimiter.Length)) return index;
            }
            return -1;
        }

        private static bool IsOpeningDelimiter(string text, int index, int length)
        {
            char? previous = PreviousVisibleCharacter(text, index);
            char? next = NextVisibleCharacter(text, index + length);
            if (!next.HasValue || char.IsWhiteSpace(next.Value)) return false;
            return !previous.HasValue || char.IsWhiteSpace(previous.Value) || char.IsLetterOrDigit(previous.Value);
        }

        private static bool IsClosingDelimiter(string text, int index, int length)
        {
            char? previous = PreviousVisibleCharacter(text, index);
            char? next = NextVisibleCharacter(text, index + length);
            if (!previous.HasValue || char.IsWhiteSpace(previous.Value)) return false;
            return !next.HasValue || char.IsWhiteSpace(next.Value) || !char.IsLetterOrDigit(next.Value);
        }

        private static bool IsStandaloneDelimiter(string text, int index, int length)
        {
            char delimiter = text[index];
            return (index == 0 || text[index - 1] != delimiter) &&
                (index + length >= text.Length || text[index + length] != delimiter);
        }

        private static char? PreviousVisibleCharacter(string text, int index)
        {
            int cursor = index - 1;
            while (cursor >= 0)
            {
                if (text[cursor] == '<')
                {
                    if (TryRichTextTag(text, cursor, out int tagLength, out _) && cursor + tagLength == index)
                    {
                        cursor--;
                        continue;
                    }
                }
                if (text[cursor] == '>')
                {
                    int tagStart = text.LastIndexOf('<', cursor);
                    if (tagStart >= 0 && TryRichTextTag(text, tagStart, out int tagLength, out _) &&
                        tagStart + tagLength == cursor + 1)
                    {
                        cursor = tagStart - 1;
                        continue;
                    }
                }
                return text[cursor];
            }
            return null;
        }

        private static char? NextVisibleCharacter(string text, int index)
        {
            int cursor = index;
            while (cursor < text.Length)
            {
                if (TryRichTextTag(text, cursor, out int tagLength, out _))
                {
                    cursor += tagLength;
                    continue;
                }
                return text[cursor];
            }
            return null;
        }

        private enum RichTextTagKind
        {
            Unsupported,
            Supported,
            InvalidColor
        }

        private static bool TryRichTextTag(string text, int index, out int length, out RichTextTagKind kind)
        {
            length = 0;
            kind = RichTextTagKind.Unsupported;
            if (index < 0 || index >= text.Length || text[index] != '<') return false;
            int end = text.IndexOf('>', index + 1);
            if (end < 0) return false;
            length = end - index + 1;
            string body = text.Substring(index + 1, end - index - 1);
            if (IsColorTagCandidate(body))
            {
                kind = IsValidColorTag(body) ? RichTextTagKind.Supported : RichTextTagKind.InvalidColor;
                return true;
            }
            string trimmedBody = body.Trim();
            if (trimmedBody.Equals("b", StringComparison.OrdinalIgnoreCase) ||
                trimmedBody.Equals("/b", StringComparison.OrdinalIgnoreCase) ||
                trimmedBody.Equals("i", StringComparison.OrdinalIgnoreCase) ||
                trimmedBody.Equals("/i", StringComparison.OrdinalIgnoreCase))
                kind = RichTextTagKind.Supported;
            return true;
        }

        private static bool IsColorTagCandidate(string body)
        {
            if (body.Equals("color", StringComparison.OrdinalIgnoreCase) ||
                body.Equals("/color", StringComparison.OrdinalIgnoreCase))
                return true;
            if (body.Length > 5 && body.StartsWith("color", StringComparison.OrdinalIgnoreCase) &&
                (body[5] == '=' || char.IsWhiteSpace(body[5])))
                return true;
            return body.Length > 6 && body.StartsWith("/color", StringComparison.OrdinalIgnoreCase) &&
                char.IsWhiteSpace(body[6]);
        }

        private static bool IsValidColorTag(string body)
        {
            if (body.Equals("/color", StringComparison.OrdinalIgnoreCase)) return true;
            if (!body.StartsWith("color=", StringComparison.OrdinalIgnoreCase)) return false;
            string value = body.Substring(6);
            if ((value.Length != 7 && value.Length != 9) || value[0] != '#') return false;
            for (int i = 1; i < value.Length; i++)
            {
                char digit = value[i];
                bool hex = digit >= '0' && digit <= '9' ||
                    digit >= 'a' && digit <= 'f' ||
                    digit >= 'A' && digit <= 'F';
                if (!hex) return false;
            }
            return true;
        }

        private static bool IsMarkdownEscape(char value)
        {
            return value == '\\' || value == '*' || value == '_' || value == '`' ||
                value == '[' || value == ']' || value == '#' || value == '+' || value == '-' || value == '.';
        }

        private static bool StartsWith(string text, int index, string value)
        {
            return index >= 0 && index + value.Length <= text.Length &&
                string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
        }

    }
}
