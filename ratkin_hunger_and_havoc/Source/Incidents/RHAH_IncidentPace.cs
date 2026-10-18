using System;
using System.Collections.Generic;
using System.Globalization;

namespace HungerAndHavoc.Incidents
{
    internal readonly struct RHAH_IncidentPaceContext
    {
        internal RHAH_IncidentPaceContext(float day, float averageDays, int trust, int season)
        {
            Day = day;
            AverageDays = averageDays;
            Trust = trust;
            Season = season;
        }

        internal float Day { get; }

        internal float AverageDays { get; }

        internal int Trust { get; }

        internal int Season { get; }
    }

    internal static class RHAH_IncidentPace
    {
        internal const string DefaultFormula = "averageDays";
        internal const int MaxLength = 160;

        internal static float Resolve(string formula, float averageDays, RHAH_IncidentPaceContext context)
        {
            if (!TryEvaluate(formula, context, out float days))
            {
                days = context.AverageDays;
            }

            return RHAH_IncidentSchedule.ClampPaceResult(days);
        }

        internal static string Normalize(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                return DefaultFormula;
            }

            string trimmed = formula.Trim();
            if (trimmed.Length > MaxLength || !TryEvaluate(trimmed, Sample(), out _))
            {
                return DefaultFormula;
            }

            return trimmed;
        }

        internal static bool TryEvaluate(string formula, RHAH_IncidentPaceContext context, out float days)
        {
            days = 0f;
            if (string.IsNullOrWhiteSpace(formula) || formula.Length > MaxLength)
            {
                return false;
            }

            Parser parser = new Parser(formula, context);
            if (!parser.TryParse(out days) || float.IsNaN(days) || float.IsInfinity(days))
            {
                days = 0f;
                return false;
            }

            return true;
        }

        static RHAH_IncidentPaceContext Sample()
        {
            return new RHAH_IncidentPaceContext(1f, RHAH_IncidentSchedule.DefaultDays, 0, 0);
        }

        sealed class Parser
        {
            readonly string text;
            readonly RHAH_IncidentPaceContext context;
            int index;

            internal Parser(string text, RHAH_IncidentPaceContext context)
            {
                this.text = text;
                this.context = context;
            }

            internal bool TryParse(out float value)
            {
                value = 0f;
                if (!TryExpression(out value) || Skip() < text.Length)
                {
                    value = 0f;
                    return false;
                }

                return !float.IsNaN(value) && !float.IsInfinity(value);
            }

            bool TryExpression(out float value)
            {
                if (!TryTerm(out value))
                {
                    return false;
                }

                while (true)
                {
                    int mark = Skip();
                    if (mark >= text.Length || (text[mark] != '+' && text[mark] != '-'))
                    {
                        return true;
                    }

                    index = mark + 1;
                    if (!TryTerm(out float right))
                    {
                        return false;
                    }

                    value = text[mark] == '+' ? value + right : value - right;
                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        return false;
                    }
                }
            }

            bool TryTerm(out float value)
            {
                if (!TryUnary(out value))
                {
                    return false;
                }

                while (true)
                {
                    int mark = Skip();
                    if (mark >= text.Length || (text[mark] != '*' && text[mark] != '/'))
                    {
                        return true;
                    }

                    index = mark + 1;
                    if (!TryUnary(out float right))
                    {
                        return false;
                    }

                    if (text[mark] == '*')
                    {
                        value *= right;
                    }
                    else
                    {
                        if (right == 0f)
                        {
                            return false;
                        }

                        value /= right;
                    }

                    if (float.IsNaN(value) || float.IsInfinity(value))
                    {
                        return false;
                    }
                }
            }

            bool TryUnary(out float value)
            {
                int mark = Skip();
                if (mark < text.Length && (text[mark] == '+' || text[mark] == '-'))
                {
                    index = mark + 1;
                    if (!TryUnary(out value))
                    {
                        return false;
                    }

                    if (text[mark] == '-')
                    {
                        value = -value;
                    }

                    return !float.IsNaN(value) && !float.IsInfinity(value);
                }

                return TryPrimary(out value);
            }

            bool TryPrimary(out float value)
            {
                value = 0f;
                int mark = Skip();
                if (mark >= text.Length)
                {
                    return false;
                }

                if (text[mark] == '(')
                {
                    index = mark + 1;
                    if (!TryExpression(out value))
                    {
                        return false;
                    }

                    int close = Skip();
                    if (close >= text.Length || text[close] != ')')
                    {
                        return false;
                    }

                    index = close + 1;
                    return true;
                }

                if (IsNameStart(text[mark]))
                {
                    return TryName(out value);
                }

                return TryNumber(out value);
            }

            bool TryName(out float value)
            {
                value = 0f;
                int start = index;
                while (index < text.Length && IsNamePart(text[index]))
                {
                    index++;
                }

                string name = text.Substring(start, index - start);
                int mark = Skip();
                if (mark < text.Length && text[mark] == '(')
                {
                    return TryCall(name, out value);
                }

                return TryVariable(name, out value);
            }

            bool TryCall(string name, out float value)
            {
                value = 0f;
                index++;
                List<float> args = new List<float>(3);
                int mark = Skip();
                if (mark < text.Length && text[mark] == ')')
                {
                    return false;
                }

                while (true)
                {
                    if (!TryExpression(out float arg))
                    {
                        return false;
                    }

                    args.Add(arg);
                    mark = Skip();
                    if (mark >= text.Length)
                    {
                        return false;
                    }

                    if (text[mark] == ',')
                    {
                        index = mark + 1;
                        continue;
                    }

                    if (text[mark] == ')')
                    {
                        index = mark + 1;
                        return Apply(name, args, out value);
                    }

                    return false;
                }
            }

            bool TryVariable(string name, out float value)
            {
                switch (name)
                {
                    case "day":
                        value = context.Day;
                        return true;
                    case "averageDays":
                        value = context.AverageDays;
                        return true;
                    case "trust":
                        value = context.Trust;
                        return true;
                    case "season":
                        value = context.Season;
                        return true;
                    default:
                        value = 0f;
                        return false;
                }
            }

            static bool Apply(string name, List<float> args, out float value)
            {
                value = 0f;
                switch (name)
                {
                    case "min":
                        if (args.Count != 2)
                        {
                            return false;
                        }

                        value = Math.Min(args[0], args[1]);
                        return true;
                    case "max":
                        if (args.Count != 2)
                        {
                            return false;
                        }

                        value = Math.Max(args[0], args[1]);
                        return true;
                    case "abs":
                        if (args.Count != 1)
                        {
                            return false;
                        }

                        value = Math.Abs(args[0]);
                        return true;
                    case "clamp":
                        if (args.Count != 3)
                        {
                            return false;
                        }

                        float low = args[1];
                        float high = args[2];
                        if (low > high)
                        {
                            return false;
                        }

                        value = args[0] < low ? low : args[0] > high ? high : args[0];
                        return true;
                    default:
                        return false;
                }
            }

            bool TryNumber(out float value)
            {
                value = 0f;
                int start = index;
                if (start >= text.Length || !IsDigit(text[start]))
                {
                    return false;
                }

                index++;
                while (index < text.Length && IsDigit(text[index]))
                {
                    index++;
                }

                if (index < text.Length && text[index] == '.')
                {
                    int dot = index;
                    index++;
                    if (index >= text.Length || !IsDigit(text[index]))
                    {
                        index = dot;
                    }
                    else
                    {
                        while (index < text.Length && IsDigit(text[index]))
                        {
                            index++;
                        }
                    }
                }

                return float.TryParse(
                    text.Substring(start, index - start),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value) && !float.IsNaN(value) && !float.IsInfinity(value);
            }

            int Skip()
            {
                while (index < text.Length && text[index] == ' ')
                {
                    index++;
                }

                return index;
            }

            static bool IsNameStart(char value)
            {
                return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
            }

            static bool IsNamePart(char value)
            {
                return IsNameStart(value) || IsDigit(value);
            }

            static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }
        }
    }
}
