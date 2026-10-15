using System;
using System.Collections.Generic;
using System.Globalization;

namespace Odf.PrisonerNames
{
    public enum NameStyle
    {
        Code,
        Short,
        Full,
        First,
        Last
    }

    public sealed class RacePrefix
    {
        public string Race = string.Empty;
        public string Prefix = string.Empty;
    }

    public sealed class GradeBand
    {
        public double Minimum;
        public string Name = string.Empty;
    }

    public sealed class AgeNameRule
    {
        public double MinimumAge;
        public string Pattern = string.Empty;
        public string ChinesePattern = string.Empty;
        public AgeNameRule Clone() => new AgeNameRule { MinimumAge = MinimumAge, Pattern = Pattern, ChinesePattern = ChinesePattern };
    }

    public sealed class RaceNameRule
    {
        public string Race = string.Empty;
        public bool Enabled;
        public List<AgeNameRule> Ages = new List<AgeNameRule>();
    }

    public sealed class NamePolicy
    {
        public const double DefaultNeutralScore = 0d;
        public const string DefaultGrade = "Neutral";
        public const string DefaultChineseGrade = "普通";

        public string Pattern = "{race_prefix}{number_text}";
        public string ChinesePattern = "{race_prefix}{number_text}";
        public NameStyle Style = NameStyle.Code;
        public string DefaultPrefix = "P";
        public string ChineseDefaultPrefix = "囚";
        public double NeutralScore = DefaultNeutralScore;
        public double ResistancePerPoint;
        public double CustodyDayScore;
        public double PrisonBreakScore;
        public List<RacePrefix> Races = new List<RacePrefix>();
        public List<GradeBand> Grades = new List<GradeBand>();
        public bool UseAgePatterns = true;
        public List<AgeNameRule> Ages;

        public NamePolicy() : this(true) { }

        NamePolicy(bool defaults)
        {
            Ages = defaults ? new List<AgeNameRule>
            {
                new AgeNameRule { MinimumAge = 0d, Pattern = "Baby {number_text}", ChinesePattern = "幼{number_text}" },
                new AgeNameRule { MinimumAge = 1d, Pattern = "Child {number_text}", ChinesePattern = "童{number_text}" },
                new AgeNameRule { MinimumAge = 7d, Pattern = "Youth {number_text}", ChinesePattern = "少{number_text}" },
                new AgeNameRule { MinimumAge = 14d, Pattern = "Teen {number_text}", ChinesePattern = "青{number_text}" },
                new AgeNameRule { MinimumAge = 18d, Pattern = "{race_prefix}{number_text}", ChinesePattern = "{race_prefix}{number_text}" }
            } : new List<AgeNameRule>();
        }
        public List<RaceNameRule> RaceNames = new List<RaceNameRule>();

        public static NamePolicy CreateDefault()
        {
            var policy = new NamePolicy();
            policy.Races.Add(new RacePrefix { Race = "Human", Prefix = "P" });
            policy.Races.Add(new RacePrefix { Race = "Ratkin", Prefix = "RK" });
            policy.Grades.Add(new GradeBand { Minimum = double.NegativeInfinity, Name = DefaultGrade });
            policy.Grades.Add(new GradeBand { Minimum = 20d, Name = "Noted" });
            policy.Grades.Add(new GradeBand { Minimum = 50d, Name = "Valued" });
            return policy;
        }

        public NamePolicy Clone()
        {
            var copy = new NamePolicy(false)
            {
                Pattern = Pattern,
                ChinesePattern = ChinesePattern,
                Style = Style,
                DefaultPrefix = DefaultPrefix,
                ChineseDefaultPrefix = ChineseDefaultPrefix,
                NeutralScore = NeutralScore,
                ResistancePerPoint = ResistancePerPoint,
                CustodyDayScore = CustodyDayScore,
                PrisonBreakScore = PrisonBreakScore,
                UseAgePatterns = UseAgePatterns
            };
            for (int i = 0; i < Races.Count; i++)
                copy.Races.Add(new RacePrefix { Race = Races[i].Race, Prefix = Races[i].Prefix });
            for (int i = 0; i < Grades.Count; i++)
                copy.Grades.Add(new GradeBand { Minimum = Grades[i].Minimum, Name = Grades[i].Name });
            for (int i = 0; i < Ages.Count; i++) copy.Ages.Add(Ages[i].Clone());
            for (int i = 0; i < RaceNames.Count; i++)
            {
                RaceNameRule source = RaceNames[i];
                var race = new RaceNameRule { Race = source.Race, Enabled = source.Enabled };
                for (int j = 0; j < source.Ages.Count; j++) race.Ages.Add(source.Ages[j].Clone());
                copy.RaceNames.Add(race);
            }
            return copy;
        }

        public string PrefixFor(string race, bool chinese)
        {
            if (!string.IsNullOrEmpty(race))
            {
                for (int i = 0; i < Races.Count; i++)
                    if (string.Equals(Races[i].Race, race, StringComparison.Ordinal))
                        return Races[i].Prefix ?? string.Empty;
            }
            return chinese ? ChineseDefaultPrefix ?? string.Empty : DefaultPrefix ?? string.Empty;
        }

        public string PatternFor(string race, double age, bool chinese)
        {
            if (!UseAgePatterns) return chinese ? ChinesePattern : Pattern;
            AgeNameRule band = null;
            for (int i = 0; i < Ages.Count; i++)
                if (Ages[i].MinimumAge <= age && (band == null || Ages[i].MinimumAge > band.MinimumAge)) band = Ages[i];
            if (band == null) return chinese ? ChinesePattern : Pattern;
            for (int i = 0; i < RaceNames.Count; i++)
            {
                RaceNameRule custom = RaceNames[i];
                if (!custom.Enabled || !string.Equals(custom.Race, race, StringComparison.Ordinal)) continue;
                for (int j = 0; j < custom.Ages.Count; j++)
                    if (custom.Ages[j].MinimumAge == band.MinimumAge)
                        return chinese ? custom.Ages[j].ChinesePattern : custom.Ages[j].Pattern;
                break;
            }
            return chinese ? band.ChinesePattern : band.Pattern;
        }

        public void ValidateNaming()
        {
            var tokens = new NameTokens { Number = 1, NumberText = "001", RacePrefix = "P", Grade = "Neutral", OriginalName = "Ada" };
            ValidatePattern(Pattern, tokens);
            ValidatePattern(ChinesePattern, tokens);
            if (Ages == null || Ages.Count == 0) throw new ArgumentException("At least one default age band is required.");
            var minima = new HashSet<double>();
            for (int i = 0; i < Ages.Count; i++)
            {
                AgeNameRule band = Ages[i];
                ValidateBand(band, tokens);
                if (!minima.Add(band.MinimumAge)) throw new ArgumentException("Duplicate default age boundary.");
            }
            if (!minima.Contains(0d)) throw new ArgumentException("Default age bands must start at zero.");
            var races = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < RaceNames.Count; i++)
            {
                RaceNameRule race = RaceNames[i];
                if (race == null || string.IsNullOrWhiteSpace(race.Race) || !races.Add(race.Race)) throw new ArgumentException("A unique race defName is required.");
                var overrides = new HashSet<double>();
                for (int j = 0; j < race.Ages.Count; j++)
                {
                    AgeNameRule band = race.Ages[j];
                    ValidateBand(band, tokens);
                    if (!minima.Contains(band.MinimumAge) || !overrides.Add(band.MinimumAge)) throw new ArgumentException("Race overrides must use unique default age boundaries.");
                }
            }
        }

        static void ValidateBand(AgeNameRule band, NameTokens tokens)
        {
            if (band == null || double.IsNaN(band.MinimumAge) || double.IsInfinity(band.MinimumAge) || band.MinimumAge < 0d)
                throw new ArgumentException("Age boundaries must be finite and nonnegative.");
            ValidatePattern(band.Pattern, tokens);
            ValidatePattern(band.ChinesePattern, tokens);
        }

        static void ValidatePattern(string pattern, NameTokens tokens) => NamingRules.ValidateAlias(NameFormatter.Format(pattern, tokens));

        public string GradeFor(double score)
        {
            if (double.IsNaN(score) || double.IsInfinity(score)) score = NeutralScore;
            GradeBand best = null;
            for (int i = 0; i < Grades.Count; i++)
            {
                GradeBand band = Grades[i];
                if (double.IsNaN(band.Minimum)) continue;
                if (score < band.Minimum) continue;
                if (best == null || band.Minimum > best.Minimum
                    || (band.Minimum == best.Minimum && string.CompareOrdinal(band.Name, best.Name) < 0))
                    best = band;
            }
            return best == null || string.IsNullOrEmpty(best.Name) ? DefaultGrade : best.Name;
        }

        public double? NextThreshold(double score)
        {
            if (double.IsNaN(score) || double.IsInfinity(score)) score = NeutralScore;
            double? next = null;
            for (int i = 0; i < Grades.Count; i++)
            {
                double minimum = Grades[i].Minimum;
                if (double.IsNaN(minimum) || double.IsInfinity(minimum)) continue;
                if (minimum <= score) continue;
                if (next == null || minimum < next.Value) next = minimum;
            }
            return next;
        }

        public static bool TryParseStyle(string text, out NameStyle style)
        {
            style = NameStyle.Code;
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (string.Equals(text, "code", StringComparison.OrdinalIgnoreCase)) { style = NameStyle.Code; return true; }
            if (string.Equals(text, "short", StringComparison.OrdinalIgnoreCase)) { style = NameStyle.Short; return true; }
            if (string.Equals(text, "full", StringComparison.OrdinalIgnoreCase)) { style = NameStyle.Full; return true; }
            if (string.Equals(text, "first", StringComparison.OrdinalIgnoreCase)) { style = NameStyle.First; return true; }
            if (string.Equals(text, "last", StringComparison.OrdinalIgnoreCase)) { style = NameStyle.Last; return true; }
            return false;
        }

        public static string StyleName(NameStyle style)
        {
            switch (style)
            {
                case NameStyle.Short: return "short";
                case NameStyle.Full: return "full";
                case NameStyle.First: return "first";
                case NameStyle.Last: return "last";
                default: return "code";
            }
        }

        public static string FormatNumber(double value)
        {
            if (double.IsNegativeInfinity(value)) return "…";
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            if (value == Math.Truncate(value) && value >= int.MinValue && value <= int.MaxValue)
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
