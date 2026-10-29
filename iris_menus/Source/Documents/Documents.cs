using System;
using System.Globalization;
using UnityEngine;

namespace IrisMenus
{
    public enum DocumentTargetKind { SubItem, DocumentItem }

    /// <summary>A package-scoped settings or document target. Line numbers are one-based; zero means the page start.</summary>
    public readonly struct DocumentUri : IEquatable<DocumentUri>
    {
        public DocumentUri(string packageId, DocumentTargetKind kind, string pageId, string childId = null, int line = 0)
        {
            if (!ValidPackage(packageId)) throw new ArgumentException("A valid Mod package ID is required.", nameof(packageId));
            if (kind != DocumentTargetKind.SubItem && kind != DocumentTargetKind.DocumentItem) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!ValidId(pageId) || (childId != null && !ValidId(childId))) throw new ArgumentException("A page ID must be a single nonempty path segment.");
            if (line < 0 || (kind == DocumentTargetKind.SubItem && (childId != null || line != 0))) throw new ArgumentException("Only documents support child pages and line numbers.");
            PackageId = packageId.ToLowerInvariant();
            Kind = kind;
            PageId = pageId;
            ChildId = childId;
            Line = line;
        }

        public string PackageId { get; }
        public DocumentTargetKind Kind { get; }
        public string PageId { get; }
        public string ChildId { get; }
        public int Line { get; }
        public bool IsValid => PackageId != null && PageId != null;
        internal string LocalId => ChildId == null ? PageId : PageId + "/" + ChildId;

        public static bool TryParse(string value, out DocumentUri uri)
        {
            uri = default(DocumentUri);
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(new[] { '?', '#', '\\' }) >= 0) return false;
            int scheme = value.IndexOf("://", StringComparison.Ordinal);
            if (scheme <= 0) return false;
            string package = value.Substring(0, scheme);
            if (!ValidPackage(package)) return false;
            string[] parts = value.Substring(scheme + 3).Split('/');
            if (parts.Length < 2 || parts.Length > 3) return false;
            DocumentTargetKind kind;
            if (parts[0] == "SubItem") kind = DocumentTargetKind.SubItem;
            else if (parts[0] == "DocumentItem") kind = DocumentTargetKind.DocumentItem;
            else return false;
            int line = 0;
            int colon = parts[parts.Length - 1].LastIndexOf(':');
            if (colon >= 0)
            {
                string last = parts[parts.Length - 1];
                if (!int.TryParse(last.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out line) || line <= 0) return false;
                parts[parts.Length - 1] = last.Substring(0, colon);
            }
            if (!Decode(parts[1], out string page) || (parts.Length == 3 && !Decode(parts[2], out _))) return false;
            string child = null;
            if (parts.Length == 3) Decode(parts[2], out child);
            if (kind == DocumentTargetKind.SubItem && (child != null || line != 0)) return false;
            uri = new DocumentUri(package, kind, page, child, line);
            return true;
        }

        public override string ToString() => !IsValid ? string.Empty : PackageId + "://" + Kind + "/" + Uri.EscapeDataString(PageId) +
            (ChildId == null ? string.Empty : "/" + Uri.EscapeDataString(ChildId)) + (Line == 0 ? string.Empty : ":" + Line.ToString(CultureInfo.InvariantCulture));
        public bool Equals(DocumentUri other) => PackageId == other.PackageId && Kind == other.Kind && PageId == other.PageId && ChildId == other.ChildId && Line == other.Line;
        public override bool Equals(object obj) => obj is DocumentUri other && Equals(other);
        public override int GetHashCode() => ((PackageId ?? string.Empty).GetHashCode() * 397 ^ (PageId ?? string.Empty).GetHashCode()) * 397 ^
            (ChildId ?? string.Empty).GetHashCode() ^ (int)Kind ^ Line;
        public static bool operator ==(DocumentUri left, DocumentUri right) => left.Equals(right);
        public static bool operator !=(DocumentUri left, DocumentUri right) => !left.Equals(right);

        private static bool Decode(string encoded, out string decoded)
        {
            decoded = null;
            for (int i = 0; i < encoded.Length; i++)
            {
                if (encoded[i] != '%') continue;
                if (i + 2 >= encoded.Length || !IsHex(encoded[i + 1]) || !IsHex(encoded[i + 2])) return false;
                i += 2;
            }
            try { decoded = Uri.UnescapeDataString(encoded); }
            catch (UriFormatException) { return false; }
            return ValidId(decoded);
        }

        private static bool IsHex(char c) => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F';
        internal static bool ValidId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == "..") return false;
            for (int i = 0; i < value.Length; i++)
                if (char.IsControl(value[i]) || "/\\:?#%".IndexOf(value[i]) >= 0) return false;
            return true;
        }
        private static bool ValidPackage(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsLetter(value[0])) return false;
            for (int i = 1; i < value.Length; i++)
                if (!IsLetter(value[i]) && !(value[i] >= '0' && value[i] <= '9') && value[i] != '.' && value[i] != '-' && value[i] != '+') return false;
            return true;
        }
        private static bool IsLetter(char c) => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';
    }

    /// <summary>Measured document content; registration, navigation and scrolling belong to the host.</summary>
    public sealed class DocumentItem
    {
        private readonly Func<float, float> measure;
        private readonly Action<Rect> draw;
        private readonly Func<float, int, float?> linePosition;

        public DocumentItem(string id, Func<string> title, IMarkdown content)
        {
            if (!DocumentUri.ValidId(id)) throw new ArgumentException("A single-segment document ID is required.", nameof(id));
            Id = id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            LayoutKey = () => content.MarkdownText;
        }

        public DocumentItem(string id, Func<string> title, Func<string> markdown)
            : this(id, title, Markdown.Document(markdown)) { }

        public DocumentItem(string id, Func<string> title, Func<float, float> measure, Action<Rect> draw,
            Func<string> layoutKey = null, Func<float, int, float?> linePosition = null)
        {
            if (!DocumentUri.ValidId(id)) throw new ArgumentException("A single-segment document ID is required.", nameof(id));
            Id = id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            this.measure = measure ?? throw new ArgumentNullException(nameof(measure));
            this.draw = draw ?? throw new ArgumentNullException(nameof(draw));
            LayoutKey = layoutKey;
            this.linePosition = linePosition;
        }

        public string Id { get; }
        public Func<string> Title { get; }
        public IMarkdown Content { get; }
        public Func<string> LayoutKey { get; }
        public float Measure(float width) => Content != null ? Content.Measure(width) : measure(width);
        public void Draw(Rect rect, Action<DocumentUri> openLink)
        {
            if (Content is IInteractiveMarkdown interactive) interactive.Draw(rect, openLink);
            else if (Content != null) Content.Draw(rect);
            else draw(rect);
        }
        public bool TryGetLinePosition(float width, int line, out float y)
        {
            y = 0f;
            if (line <= 0) return line == 0;
            if (Content is MarkdownDocument document) return document.TryGetLinePosition(width, line, out y);
            float? position = linePosition?.Invoke(width, line);
            if (!position.HasValue || float.IsNaN(position.Value) || float.IsInfinity(position.Value) || position.Value < 0f) return false;
            y = position.Value;
            return true;
        }
    }
}
