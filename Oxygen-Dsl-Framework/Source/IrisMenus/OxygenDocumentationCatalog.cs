using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IrisMenus;
using Verse;

namespace Oxygendsl.IrisMenus
{
    internal sealed class OxygenDocumentationCatalog
    {
        private const string ResourceRoot = "docs.Oxygen.zh_cn.";
        private static readonly string[] Paths =
        {
            "00-overview.md",
            "01-keywords-and-syntax.md",
            "02-values-and-functions.md",
            "03-gameplay-and-content.md",
            "04-modules-and-settings.md",
            "05-advanced-and-cautions.md"
        };
        private static readonly string[] TitleKeys =
        {
            "Oxygendsl_DocumentationOverview",
            "Oxygendsl_DocumentationSyntax",
            "Oxygendsl_DocumentationValues",
            "Oxygendsl_DocumentationGameplay",
            "Oxygendsl_DocumentationModules",
            "Oxygendsl_DocumentationAdvanced"
        };
        internal static readonly Lazy<OxygenDocumentationCatalog> Embedded =
            new Lazy<OxygenDocumentationCatalog>(() => new OxygenDocumentationCatalog());

        private readonly Dictionary<string, DocumentationTarget> targets =
            new Dictionary<string, DocumentationTarget>(StringComparer.Ordinal);
        private readonly MenuSearchEntry[] searchEntries;
        internal readonly DocumentationArticle[] Articles;

        private OxygenDocumentationCatalog()
        {
            Articles = new DocumentationArticle[Paths.Length];
            var entries = new List<MenuSearchEntry>();
            for (int index = 0; index < Paths.Length; index++)
            {
                string error;
                string markdown = ReadResource(Paths[index], out error);
                DocumentationArticle article = ParseArticle(Paths[index], TitleKeys[index], markdown, error);
                Articles[index] = article;
                targets.Add(article.Id, new DocumentationTarget(index, 0));
                if (!article.Available) continue;
                entries.Add(new MenuSearchEntry(article.Id, () => article.Title,
                    () => article.Markdown, () => "Oxygendsl_DocumentationTitle".Translate().ToString()));
                for (int blockIndex = 0; blockIndex < article.Blocks.Length; blockIndex++)
                {
                    DocumentationBlock block = article.Blocks[blockIndex];
                    if (block.Id == null) continue;
                    targets.Add(block.Id, new DocumentationTarget(index, blockIndex));
                    entries.Add(new MenuSearchEntry(block.Id, () => block.Title,
                        () => block.Markdown, () => article.Title));
                }
            }
            searchEntries = entries.ToArray();
        }

        internal IEnumerable<MenuSearchEntry> SearchEntries() { return searchEntries; }

        internal bool TryFind(string id, out DocumentationTarget target)
        {
            target = default(DocumentationTarget);
            return id != null && targets.TryGetValue(id, out target);
        }

        private static string ReadResource(string path, out string error)
        {
            error = null;
            string resource = ResourceRoot + path;
            try
            {
                using (Stream stream = typeof(OxygenDocumentationCatalog).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) error = "Embedded resource is missing: " + resource;
                    else
                    {
                        using (var reader = new StreamReader(stream, new UTF8Encoding(false, true)))
                        {
                            string markdown = reader.ReadToEnd();
                            if (!string.IsNullOrWhiteSpace(markdown)) return markdown;
                            error = "Embedded documentation is empty: " + resource;
                        }
                    }
                }
            }
            catch (IOException exception) { error = resource + ": " + exception.Message; }
            catch (DecoderFallbackException exception) { error = resource + ": " + exception.Message; }
            catch (InvalidOperationException exception) { error = resource + ": " + exception.Message; }
            Log.Error("[Oxygen DSL] " + error);
            return string.Empty;
        }

        private static DocumentationArticle ParseArticle(string path, string titleKey, string markdown, string error)
        {
            var headings = new List<Heading>();
            string title = null;
            char fenceMarker = '\0';
            int fenceLength = 0;
            int lineStart = 0;
            while (lineStart < markdown.Length)
            {
                int newline = markdown.IndexOf('\n', lineStart);
                int lineEnd = newline < 0 ? markdown.Length : newline;
                string line = markdown.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');
                int start = 0;
                while (start < line.Length && start < 3 && line[start] == ' ') start++;
                if (fenceMarker != '\0')
                {
                    if (IsClosingFence(line, start, fenceMarker, fenceLength))
                    {
                        fenceMarker = '\0';
                        fenceLength = 0;
                    }
                }
                else if (TryFence(line, start, out char marker, out int length))
                {
                    fenceMarker = marker;
                    fenceLength = length;
                }
                else if (TryHeading(line, start, out int level, out string heading))
                {
                    if (level == 1 && title == null) title = heading;
                    if (level == 2) headings.Add(new Heading(lineStart, heading));
                }
                if (newline < 0) break;
                lineStart = newline + 1;
            }

            var blocks = new List<DocumentationBlock>();
            if (headings.Count == 0)
            {
                if (markdown.Length > 0) blocks.Add(new DocumentationBlock(null, null, markdown));
            }
            else
            {
                if (headings[0].Offset > 0)
                    blocks.Add(new DocumentationBlock(null, null, markdown.Substring(0, headings[0].Offset)));
                var usedSlugs = new HashSet<string>(StringComparer.Ordinal) { "top" };
                for (int index = 0; index < headings.Count; index++)
                {
                    Heading heading = headings[index];
                    int end = index + 1 < headings.Count ? headings[index + 1].Offset : markdown.Length;
                    string baseSlug = Slug(heading.Title);
                    string slug = baseSlug;
                    int suffix = 2;
                    while (!usedSlugs.Add(slug)) slug = baseSlug + "-" + suffix++;
                    blocks.Add(new DocumentationBlock(path + "#" + slug, heading.Title,
                        markdown.Substring(heading.Offset, end - heading.Offset)));
                }
            }
            return new DocumentationArticle(path, titleKey, title, markdown, error, blocks.ToArray());
        }

        internal static bool TryFence(string line, int start, out char marker, out int length)
        {
            marker = '\0';
            length = 0;
            if (start >= line.Length || (line[start] != '`' && line[start] != '~')) return false;
            marker = line[start];
            int end = start;
            while (end < line.Length && line[end] == marker) end++;
            length = end - start;
            return length >= 3 && (marker != '`' || line.IndexOf('`', end) < 0);
        }

        internal static bool IsClosingFence(string line, int start, char marker, int length)
        {
            int end = start;
            while (end < line.Length && line[end] == marker) end++;
            return end - start >= length && string.IsNullOrWhiteSpace(line.Substring(end));
        }

        private static bool TryHeading(string line, int start, out int level, out string title)
        {
            int end = start;
            while (end < line.Length && line[end] == '#') end++;
            level = end - start;
            title = null;
            if (level < 1 || level > 2 || end >= line.Length || !char.IsWhiteSpace(line[end])) return false;
            title = line.Substring(end).Trim();
            int closing = title.Length;
            while (closing > 0 && title[closing - 1] == '#') closing--;
            if (closing > 0 && closing < title.Length && char.IsWhiteSpace(title[closing - 1]))
                title = title.Substring(0, closing).TrimEnd();
            return title.Length > 0;
        }

        private static string Slug(string heading)
        {
            var slug = new StringBuilder(heading.Length);
            bool separator = false;
            foreach (char character in heading)
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    if (separator && slug.Length > 0) slug.Append('-');
                    slug.Append(char.ToLowerInvariant(character));
                    separator = false;
                }
                else if (char.IsWhiteSpace(character) || character == '-') separator = true;
            }
            return slug.Length == 0 ? "section" : slug.ToString();
        }

        private struct Heading
        {
            internal readonly int Offset;
            internal readonly string Title;
            internal Heading(int offset, string title) { Offset = offset; Title = title; }
        }
    }

    internal sealed class DocumentationArticle
    {
        internal readonly string Path;
        internal readonly string Id;
        private readonly string titleKey;
        private readonly string title;
        internal readonly string Markdown;
        internal readonly string Error;
        internal readonly DocumentationBlock[] Blocks;
        internal readonly bool Available;
        internal string Title { get { return title ?? titleKey.Translate().ToString(); } }

        internal DocumentationArticle(string path, string titleKey, string title, string markdown,
            string error, DocumentationBlock[] blocks)
        {
            Path = path;
            Id = path + "#top";
            this.titleKey = titleKey;
            this.title = title;
            Markdown = markdown;
            Error = error;
            Blocks = blocks;
            Available = error == null && !string.IsNullOrWhiteSpace(markdown);
        }
    }

    internal sealed class DocumentationBlock
    {
        internal readonly string Id;
        internal readonly string Title;
        internal readonly string Markdown;
        internal DocumentationBlock(string id, string title, string markdown)
        {
            Id = id;
            Title = title;
            Markdown = markdown;
        }
    }

    internal struct DocumentationTarget
    {
        internal readonly int Article;
        internal readonly int Block;
        internal DocumentationTarget(int article, int block) { Article = article; Block = block; }
    }
}
