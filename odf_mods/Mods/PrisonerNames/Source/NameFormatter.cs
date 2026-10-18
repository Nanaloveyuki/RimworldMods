using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Odf.PrisonerNames
{
    public sealed class NameTokens
    {
        public int Number;
        public string NumberText = "000";
        public string Grade = NamePolicy.DefaultGrade;
        public double Score;
        public string Race = string.Empty;
        public string RaceLabel = string.Empty;
        public string RacePrefix = string.Empty;
        public string OriginalName = string.Empty;
        public string OriginalFullName = string.Empty;
        public string OriginalShort = string.Empty;
        public string FirstName = string.Empty;
        public string LastName = string.Empty;
        public string InteractionLabel = string.Empty;
    }

    public static class NameFormatter
    {
        public static string Format(string pattern, NameTokens tokens)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            string source = pattern ?? string.Empty;
            var text = new StringBuilder(source.Length + 16);
            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                if (current == '{')
                {
                    if (i + 1 < source.Length && source[i + 1] == '{')
                    {
                        text.Append('{');
                        i++;
                        continue;
                    }
                    int end = source.IndexOf('}', i + 1);
                    if (end < 0) throw new ArgumentException("A placeholder is missing its closing brace.");
                    string token = source.Substring(i + 1, end - i - 1);
                    text.Append(Resolve(token, tokens));
                    i = end;
                    continue;
                }
                if (current == '}')
                {
                    if (i + 1 < source.Length && source[i + 1] == '}')
                    {
                        text.Append('}');
                        i++;
                        continue;
                    }
                    throw new ArgumentException("A closing brace is not escaped.");
                }
                text.Append(current);
            }
            return text.ToString();
        }

        public static string ApplyStyle(NameStyle style, string formatted, NameTokens tokens)
        {
            switch (style)
            {
                case NameStyle.Short: return tokens.OriginalShort ?? string.Empty;
                case NameStyle.Full: return tokens.OriginalFullName ?? string.Empty;
                case NameStyle.First: return tokens.FirstName ?? string.Empty;
                case NameStyle.Last: return tokens.LastName ?? string.Empty;
                default: return formatted;
            }
        }

        static string Resolve(string token, NameTokens tokens)
        {
            switch (token)
            {
                case "number": return tokens.Number.ToString(CultureInfo.InvariantCulture);
                case "number_text": return tokens.NumberText ?? string.Empty;
                case "grade": return tokens.Grade ?? string.Empty;
                case "score": return NamePolicy.FormatNumber(tokens.Score);
                case "race": return tokens.Race ?? string.Empty;
                case "race_label": return tokens.RaceLabel ?? string.Empty;
                case "race_prefix": return tokens.RacePrefix ?? string.Empty;
                case "original_name": return tokens.OriginalName ?? string.Empty;
                case "original_full_name": return tokens.OriginalFullName ?? string.Empty;
                case "original_short": return tokens.OriginalShort ?? string.Empty;
                case "first_name": return tokens.FirstName ?? string.Empty;
                case "last_name": return tokens.LastName ?? string.Empty;
                case "interaction_label": return tokens.InteractionLabel ?? string.Empty;
                default: throw new ArgumentException("Unknown name token '" + token + "'.");
            }
        }
    }

    public static class NamePresets
    {
        public sealed class Preset
        {
            public string Id;
            public string Pattern;
            public string ChinesePattern;
            public NameStyle Style;
        }

        public static IReadOnlyList<Preset> All { get; } = new Preset[]
        {
            new Preset { Id = "code", Pattern = "{race_prefix}-{number_text}", ChinesePattern = "{race_prefix}{number_text}", Style = NameStyle.Code },
            new Preset { Id = "grade", Pattern = "{grade} {number_text}", ChinesePattern = "{grade}{number_text}", Style = NameStyle.Code },
            new Preset { Id = "short", Pattern = "{original_short}", ChinesePattern = "{original_short}", Style = NameStyle.Short },
            new Preset { Id = "full", Pattern = "{original_full_name}", ChinesePattern = "{original_full_name}", Style = NameStyle.Full }
        };

        public static Preset Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++)
                if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase)) return All[i];
            return null;
        }
    }
}
