using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Odf.PrisonerNames
{
    public static class JsonFacts
    {
        const int MaxLength = 1024 * 1024;

        public static Dictionary<string, object> ReadObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>(StringComparer.Ordinal);
            if (json.Length > MaxLength) throw new ArgumentException("Facts JSON is larger than 1 MiB.");
            int index = 0;
            Skip(json, ref index);
            Dictionary<string, object> root = ReadMap(json, ref index);
            Skip(json, ref index);
            if (index != json.Length) throw new ArgumentException("Facts JSON has trailing text.");
            return root;
        }

        static Dictionary<string, object> ReadMap(string json, ref int index)
        {
            Expect(json, ref index, '{');
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            Skip(json, ref index);
            if (Peek(json, index) == '}') { index++; return map; }
            while (true)
            {
                Skip(json, ref index);
                string key = ReadString(json, ref index);
                if (map.ContainsKey(key)) throw new ArgumentException("Facts JSON repeats '" + key + "'.");
                Skip(json, ref index);
                Expect(json, ref index, ':');
                map[key] = ReadScalar(json, ref index);
                Skip(json, ref index);
                if (Peek(json, index) == ',') { index++; continue; }
                Expect(json, ref index, '}');
                return map;
            }
        }

        static object ReadScalar(string json, ref int index)
        {
            Skip(json, ref index);
            char current = Peek(json, index);
            if (current == '{' || current == '[') throw new ArgumentException("Facts JSON only accepts scalar values.");
            if (current == '"') return ReadString(json, ref index);
            if (current == 't' || current == 'f') return ReadBool(json, ref index);
            if (current == 'n') return ReadNull(json, ref index);
            return ReadNumber(json, ref index);
        }

        static string ReadString(string json, ref int index)
        {
            Expect(json, ref index, '"');
            var text = new StringBuilder();
            while (index < json.Length)
            {
                char current = json[index++];
                if (current == '"') return text.ToString();
                if (current != '\\') { text.Append(current); continue; }
                if (index >= json.Length) break;
                char escaped = json[index++];
                switch (escaped)
                {
                    case '"': case '\\': case '/': text.Append(escaped); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw new ArgumentException("Facts JSON has a short escape.");
                        text.Append((char)int.Parse(json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default: throw new ArgumentException("Facts JSON has an unknown escape.");
                }
            }
            throw new ArgumentException("Facts JSON has an unclosed string.");
        }

        static bool ReadBool(string json, ref int index)
        {
            if (Match(json, index, "true")) { index += 4; return true; }
            if (Match(json, index, "false")) { index += 5; return false; }
            throw new ArgumentException("Facts JSON has an invalid boolean.");
        }

        static object ReadNull(string json, ref int index)
        {
            if (!Match(json, index, "null")) throw new ArgumentException("Facts JSON has an invalid null.");
            index += 4;
            return null;
        }

        static double ReadNumber(string json, ref int index)
        {
            int start = index;
            if (Peek(json, index) == '-') index++;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+' || json[index] == '-'))
                index++;
            double number;
            if (!double.TryParse(json.Substring(start, index - start), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                || double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException("Facts JSON only accepts finite numbers.");
            return number;
        }

        static void Skip(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        static char Peek(string json, int index)
        {
            return index < json.Length ? json[index] : '\0';
        }

        static void Expect(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected) throw new ArgumentException("Facts JSON expected '" + expected + "'.");
            index++;
        }

        static bool Match(string json, int index, string word)
        {
            return index + word.Length <= json.Length && string.CompareOrdinal(json, index, word, 0, word.Length) == 0;
        }
    }
}
