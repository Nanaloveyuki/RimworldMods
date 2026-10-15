using System;
using System.Collections.Generic;
using System.Globalization;

using Oxygendsl.Core;

namespace Odf.PrisonerNames
{
    public static class NamingPreview
    {
        public static Dictionary<string, object> Project(string factsJson, NamePolicy policy, bool chinese)
        {
            Dictionary<string, object> root = JsonFacts.ReadObject(factsJson);
            var facts = new Dictionary<string, object>(StringComparer.Ordinal);
            Copy(root, facts);
            int number = ReadInt(root, "number", 1);
            if (number < 1) number = 1;
            string numberText = ReadString(root, "number_text");
            if (string.IsNullOrEmpty(numberText)) numberText = number.ToString("D3", CultureInfo.InvariantCulture);
            string race = ReadString(root, "race");
            double score = ReadDouble(root, "score", policy == null ? NamePolicy.DefaultNeutralScore : policy.NeutralScore);
            bool manualScore = ReadBool(root, "manual_score");
            string manualGrade = ReadString(root, "manual_grade");
            string grade = !string.IsNullOrEmpty(manualGrade) ? manualGrade : policy == null ? NamePolicy.DefaultGrade : policy.GradeFor(score);
            facts["number"] = number;
            facts["number_text"] = numberText;
            facts["score"] = score;
            facts["grade"] = grade;
            facts["manual_score"] = manualScore;
            facts["manual_grade"] = manualGrade ?? string.Empty;
            facts["race"] = race;
            facts["age"] = ReadDouble(root, "age", 18d);
            facts["race_label"] = ReadString(root, "race_label");
            facts["race_prefix"] = policy == null ? string.Empty : policy.PrefixFor(race, chinese);
            facts["original_name"] = First(root, "original_name", "name");
            facts["original_full_name"] = ReadString(root, "original_full_name");
            facts["original_short"] = First(root, "original_short", "original_name", "name");
            facts["first_name"] = ReadString(root, "first_name");
            facts["last_name"] = ReadString(root, "last_name");
            facts["interaction"] = ReadString(root, "interaction");
            facts["interaction_label"] = ReadString(root, "interaction_label");
            if (!facts.ContainsKey("bleeding")) facts["bleeding"] = false;
            if (!facts.ContainsKey("recruitable")) facts["recruitable"] = true;
            if (!facts.ContainsKey("resistance")) facts["resistance"] = 0d;
            return facts;
        }

        public static string Evaluate(string source, string factsJson, NamePolicy policy, bool chinese, IOxygenLocalization localization = null)
        {
            Dictionary<string, object> facts = Project(factsJson, policy, chinese);
            var host = new PreviewFacts(facts, policy, chinese);
            NamingRules rules = NamingRules.Compile(source, localization: localization);
            return rules.Evaluate(new PawnContext
            {
                Name = Convert.ToString(facts["original_name"], CultureInfo.InvariantCulture),
                Facts = host
            }, new GameContext());
        }
        sealed class PreviewFacts : IOxygenHostValue
        {
            readonly Dictionary<string, object> facts;
            readonly PreviewFormat format;
            readonly PreviewValue values;
            public PreviewFacts(Dictionary<string, object> facts, NamePolicy policy, bool chinese)
            {
                this.facts = facts;
                format = new PreviewFormat(facts, policy, chinese);
                values = new PreviewValue(facts);
            }

            public bool TryGetMember(string name, out object value)
            {
                if (name == "formatted") { value = format; return true; }
                if (name == "value") { value = values; return true; }
                return facts.TryGetValue(name, out value);
            }
        }

        sealed class PreviewFormat : IOxygenHostCallable
        {
            readonly Dictionary<string, object> facts;
            readonly NamePolicy policy;
            readonly bool chinese;
            public PreviewFormat(Dictionary<string, object> facts, NamePolicy policy, bool chinese) { this.facts = facts; this.policy = policy; this.chinese = chinese; }
            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                string pattern = positional != null && positional.Count > 0 ? Convert.ToString(positional[0], CultureInfo.InvariantCulture) : null;
                if (named != null && named.ContainsKey("pattern")) pattern = Convert.ToString(named["pattern"], CultureInfo.InvariantCulture);
                if (pattern == null && policy != null) pattern = policy.PatternFor(Convert.ToString(facts["race"], CultureInfo.InvariantCulture), Convert.ToDouble(facts["age"], CultureInfo.InvariantCulture), chinese);
                var tokens = new NameTokens
                {
                    Number = Convert.ToInt32(facts["number"], CultureInfo.InvariantCulture),
                    NumberText = Convert.ToString(facts["number_text"], CultureInfo.InvariantCulture),
                    Grade = Convert.ToString(facts["grade"], CultureInfo.InvariantCulture),
                    Score = Convert.ToDouble(facts["score"], CultureInfo.InvariantCulture),
                    Race = Convert.ToString(facts["race"], CultureInfo.InvariantCulture),
                    RaceLabel = Convert.ToString(facts["race_label"], CultureInfo.InvariantCulture),
                    RacePrefix = Convert.ToString(facts["race_prefix"], CultureInfo.InvariantCulture),
                    OriginalName = Convert.ToString(facts["original_name"], CultureInfo.InvariantCulture),
                    OriginalShort = Convert.ToString(facts["original_short"], CultureInfo.InvariantCulture),
                    OriginalFullName = Convert.ToString(facts["original_full_name"], CultureInfo.InvariantCulture),
                    FirstName = Convert.ToString(facts["first_name"], CultureInfo.InvariantCulture),
                    LastName = Convert.ToString(facts["last_name"], CultureInfo.InvariantCulture),
                    InteractionLabel = Convert.ToString(facts["interaction_label"], CultureInfo.InvariantCulture)
                };
                string formatted = NameFormatter.Format(pattern, tokens);
                return NameFormatter.ApplyStyle(policy == null ? NameStyle.Code : policy.Style, formatted, tokens);
            }
        }

        sealed class PreviewValue : IOxygenHostCallable
        {
            readonly Dictionary<string, object> facts;
            public PreviewValue(Dictionary<string, object> facts) { this.facts = facts; }
            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                string key = positional != null && positional.Count > 0 ? Convert.ToString(positional[0], CultureInfo.InvariantCulture) : null;
                if (named != null && named.ContainsKey("key")) key = Convert.ToString(named["key"], CultureInfo.InvariantCulture);
                object fallback = null;
                bool hasFallback = false;
                if (named != null && named.ContainsKey("default")) { fallback = named["default"]; hasFallback = true; }
                else if (positional != null && positional.Count > 1) { fallback = positional[1]; hasFallback = true; }
                object value;
                if (!string.IsNullOrEmpty(key) && facts.TryGetValue("value." + key, out value)) return value;
                if (!hasFallback) throw new ArgumentException("The value '" + key + "' is not stored.");
                return fallback;
            }
        }


        static void Copy(Dictionary<string, object> source, Dictionary<string, object> target)
        {
            foreach (KeyValuePair<string, object> pair in source)
            {
                if (pair.Key == null || pair.Value is Dictionary<string, object> || pair.Value is object[]) continue;
                target[pair.Key] = pair.Value;
            }
        }

        static string First(Dictionary<string, object> root, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string value = ReadString(root, keys[i]);
                if (!string.IsNullOrEmpty(value)) return value;
            }
            return string.Empty;
        }

        static string ReadString(Dictionary<string, object> root, string key)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null) return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        static int ReadInt(Dictionary<string, object> root, string key, int fallback)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null) return fallback;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch (Exception) { return fallback; }
        }

        static double ReadDouble(Dictionary<string, object> root, string key, double fallback)
        {
            object value;
            if (!root.TryGetValue(key, out value) || value == null) return fallback;
            try
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return double.IsNaN(number) || double.IsInfinity(number) ? fallback : number;
            }
            catch (Exception) { return fallback; }
        }

        static bool ReadBool(Dictionary<string, object> root, string key)
        {
            object value;
            return root.TryGetValue(key, out value) && value is bool flag && flag;
        }
    }
}
