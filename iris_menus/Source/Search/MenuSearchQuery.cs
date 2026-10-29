using System;

namespace IrisMenus
{
    internal sealed class MenuSearchQuery
    {
        internal readonly bool HasOwnerSelector;
        internal readonly bool IsModLookup;
        internal readonly string OwnerPattern;
        internal readonly string[] Terms;
        private readonly string[] ownerParts;
        private readonly bool ownerHasWildcard;

        private MenuSearchQuery(bool hasOwnerSelector, bool isModLookup, string ownerPattern, string[] terms)
        {
            HasOwnerSelector = hasOwnerSelector;
            IsModLookup = isModLookup;
            OwnerPattern = ownerPattern;
            Terms = terms;
            ownerHasWildcard = ownerPattern != null && ownerPattern.IndexOf('*') >= 0;
            ownerParts = ownerHasWildcard
                ? ownerPattern.Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();
        }

        internal static MenuSearchQuery Parse(string text)
        {
            text = (text ?? "").Trim();
            bool lookup = text.StartsWith("+@", StringComparison.Ordinal);
            bool scoped = lookup || text.StartsWith("@", StringComparison.Ordinal);
            if (!scoped)
                return new MenuSearchQuery(false, false, null,
                    text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

            int start = lookup ? 2 : 1;
            int end = start;
            string pattern;
            if (start < text.Length && text[start] == '"')
            {
                // Quote the entire selector when its identity contains spaces: @"*FSF Tweaks*" .*
                start++;
                end = text.IndexOf('"', start);
                if (end < 0) end = text.Length;
                pattern = text.Substring(start, end - start);
                if (end < text.Length) end++;
            }
            else
            {
                while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
                pattern = text.Substring(start, end - start);
            }

            string[] terms = text.Substring(end).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            foreach (string term in terms)
                if (term != ".*") terms[count++] = term;
            if (count != terms.Length) Array.Resize(ref terms, count);
            return new MenuSearchQuery(true, lookup, pattern, terms);
        }

        internal bool MatchesOwner(MenuPage page)
        {
            if (!HasOwnerSelector) return true;
            if (MatchesIdentity(page.DisplayModName) || MatchesIdentity(page.Content.Name)
                || MatchesIdentity(page.Content.PackageIdPlayerFacing) || MatchesIdentity(page.OwnerId)) return true;
            if (page.Owner != null)
            {
                Type type = page.Owner.GetType();
                if (MatchesIdentity(type.FullName) || MatchesIdentity(type.Name)) return true;
            }
            foreach (string alias in MenuRegistry.GetModNameAliases(page.Content))
                if (MatchesIdentity(alias)) return true;
            return false;
        }

        internal bool MatchesMod(MenuPage page)
        {
            if (!MatchesOwner(page)) return false;
            foreach (string term in Terms)
            {
                if (Contains(page.DisplayModName, term) || Contains(page.Content.Name, term)
                    || Contains(page.Content.PackageIdPlayerFacing, term) || Contains(page.OwnerId, term)) continue;
                if (page.Owner != null)
                {
                    Type type = page.Owner.GetType();
                    if (Contains(type.FullName, term) || Contains(type.Name, term)) continue;
                }
                bool matches = false;
                foreach (string alias in MenuRegistry.GetModNameAliases(page.Content))
                    if (Contains(alias, term)) { matches = true; break; }
                if (!matches) return false;
            }
            return true;
        }

        private static bool Contains(string identity, string term) =>
            identity != null && identity.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        private bool MatchesIdentity(string identity)
        {
            if (string.IsNullOrEmpty(identity) || OwnerPattern.Length == 0) return false;
            if (!ownerHasWildcard) return string.Equals(identity, OwnerPattern, StringComparison.OrdinalIgnoreCase);
            if (ownerParts.Length == 0) return true;

            int position = 0;
            int part = 0;
            if (OwnerPattern[0] != '*')
            {
                if (!identity.StartsWith(ownerParts[0], StringComparison.OrdinalIgnoreCase)) return false;
                position = ownerParts[0].Length;
                part++;
            }
            for (; part < ownerParts.Length; part++)
            {
                string segment = ownerParts[part];
                if (part == ownerParts.Length - 1 && OwnerPattern[OwnerPattern.Length - 1] != '*')
                {
                    int finalStart = identity.Length - segment.Length;
                    return finalStart >= position && string.Compare(identity, finalStart, segment, 0, segment.Length,
                        StringComparison.OrdinalIgnoreCase) == 0;
                }
                int found = identity.IndexOf(segment, position, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return false;
                position = found + segment.Length;
            }
            return true;
        }
    }
}
