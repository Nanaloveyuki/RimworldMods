using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Oxygendsl.Mod.Editor
{
    internal static class OxygenEditorJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new OxygenEditorJsonException("JSON text is required.");
            var parser = new Parser(text);
            object value = parser.ReadValue();
            parser.SkipSpace();
            if (!parser.End) throw new OxygenEditorJsonException("Unexpected data after JSON value.");
            return value;
        }

        public static Dictionary<string, object> Object(string text)
        {
            object value = Parse(text);
            Dictionary<string, object> map = value as Dictionary<string, object>;
            if (map == null) throw new OxygenEditorJsonException("JSON object is required.");
            return map;
        }

        public static string String(IDictionary<string, object> map, string name)
        {
            object value;
            if (map == null || !map.TryGetValue(name, out value) || value == null) return null;
            string text = value as string;
            if (text == null) throw new OxygenEditorJsonException(name + " must be a string.");
            return text;
        }

        public static bool Bool(IDictionary<string, object> map, string name)
        {
            object value;
            if (map == null || !map.TryGetValue(name, out value) || value == null) return false;
            if (value is bool flag) return flag;
            throw new OxygenEditorJsonException(name + " must be a boolean.");
        }

        public static string Serialize(object value)
        {
            var builder = new StringBuilder();
            Write(builder, value, 0);
            return builder.ToString();
        }

        static void Write(StringBuilder builder, object value, int depth)
        {
            Write(builder, value, depth, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        static void Write(StringBuilder builder, object value, int depth, HashSet<object> seen)
        {
            if (depth > 64) throw new OxygenEditorJsonException("The JSON value is nested too deeply.");
            if (value == null) { builder.Append("null"); return; }
            if (value is string text) { WriteString(builder, text); return; }
            if (value is bool flag) { builder.Append(flag ? "true" : "false"); return; }
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long)
            {
                builder.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is ulong number)
            {
                builder.Append(number.ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (value is float || value is double || value is decimal)
            {
                double real = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(real) || double.IsInfinity(real)) throw new OxygenEditorJsonException("JSON numbers must be finite.");
                builder.Append(real.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                builder.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!(entry.Key is string key)) throw new OxygenEditorJsonException("JSON object keys must be strings.");
                    if (!first) builder.Append(',');
                    first = false;
                    WriteString(builder, key);
                    builder.Append(':');
                    Write(builder, entry.Value, depth + 1, seen);
                }
                builder.Append('}');
                return;
            }
            if (value is IEnumerable list)
            {
                builder.Append('[');
                bool first = true;
                foreach (object item in list)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    Write(builder, item, depth + 1, seen);
                }
                builder.Append(']');
                return;
            }
            Type type = value.GetType();
            if (type.IsEnum)
            {
                WriteString(builder, value.ToString());
                return;
            }
            if (!seen.Add(value)) throw new OxygenEditorJsonException("The JSON value contains a cycle.");
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                map.Add(property.Name, property.GetValue(value, null));
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                map.Add(field.Name, field.GetValue(value));
            if (map.Count == 0) throw new OxygenEditorJsonException("The value type cannot be represented as JSON: " + type.Name);
            Write(builder, map, depth, seen);
            seen.Remove(value);
        }

        static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (char value in text)
            {
                switch (value)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (value < ' ') builder.Append("\\u").Append(((int)value).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(value);
                        break;
                }
            }
            builder.Append('"');
        }

        sealed class Parser
        {
            readonly string _text;
            int _index;
            int _depth;

            public Parser(string text) { _text = text; }
            public bool End { get { return _index >= _text.Length; } }

            public object ReadValue()
            {
                if (++_depth > 64) throw new OxygenEditorJsonException("The JSON value is nested too deeply.");
                SkipSpace();
                if (End) throw new OxygenEditorJsonException("Unexpected end of JSON.");
                char value = _text[_index];
                object result;
                if (value == '{') result = ReadObject();
                else if (value == '[') result = ReadArray();
                else if (value == '"') result = ReadString();
                else if (value == 't') result = ReadLiteral("true", true);
                else if (value == 'f') result = ReadLiteral("false", false);
                else if (value == 'n') result = ReadLiteral("null", null);
                else if (value == '-' || (value >= '0' && value <= '9')) result = ReadNumber();
                else throw new OxygenEditorJsonException("Unexpected JSON character.");
                _depth--;
                return result;
            }

            public void SkipSpace()
            {
                while (!End)
                {
                    char value = _text[_index];
                    if (value != ' ' && value != '\t' && value != '\r' && value != '\n') return;
                    _index++;
                }
            }

            Dictionary<string, object> ReadObject()
            {
                _index++;
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipSpace();
                if (Take('}')) return map;
                while (true)
                {
                    SkipSpace();
                    if (End || _text[_index] != '"') throw new OxygenEditorJsonException("A JSON object key must be a string.");
                    string key = ReadString();
                    SkipSpace();
                    if (!Take(':')) throw new OxygenEditorJsonException("A JSON object key must be followed by a colon.");
                    if (map.ContainsKey(key)) throw new OxygenEditorJsonException("A JSON object contains a duplicate key.");
                    map.Add(key, ReadValue());
                    SkipSpace();
                    if (Take('}')) return map;
                    if (!Take(',')) throw new OxygenEditorJsonException("A JSON object is missing a comma or closing brace.");
                }
            }

            List<object> ReadArray()
            {
                _index++;
                var list = new List<object>();
                SkipSpace();
                if (Take(']')) return list;
                while (true)
                {
                    list.Add(ReadValue());
                    SkipSpace();
                    if (Take(']')) return list;
                    if (!Take(',')) throw new OxygenEditorJsonException("A JSON array is missing a comma or closing bracket.");
                }
            }

            string ReadString()
            {
                _index++;
                var builder = new StringBuilder();
                while (!End)
                {
                    char value = _text[_index++];
                    if (value == '"') return builder.ToString();
                    if (value == '\\')
                    {
                        if (End) break;
                        char escaped = _text[_index++];
                        switch (escaped)
                        {
                            case '"':
                            case '\\':
                            case '/': builder.Append(escaped); break;
                            case 'b': builder.Append('\b'); break;
                            case 'f': builder.Append('\f'); break;
                            case 'n': builder.Append('\n'); break;
                            case 'r': builder.Append('\r'); break;
                            case 't': builder.Append('\t'); break;
                            case 'u': builder.Append(ReadHex()); break;
                            default: throw new OxygenEditorJsonException("Unsupported JSON string escape.");
                        }
                    }
                    else if (value < ' ') throw new OxygenEditorJsonException("A JSON string contains a raw control character.");
                    else builder.Append(value);
                }
                throw new OxygenEditorJsonException("A JSON string is missing its closing quote.");
            }

            char ReadHex()
            {
                if (_index + 4 > _text.Length) throw new OxygenEditorJsonException("A JSON Unicode escape is incomplete.");
                int code = 0;
                for (int count = 0; count < 4; count++)
                {
                    char value = _text[_index++];
                    code <<= 4;
                    if (value >= '0' && value <= '9') code += value - '0';
                    else if (value >= 'a' && value <= 'f') code += value - 'a' + 10;
                    else if (value >= 'A' && value <= 'F') code += value - 'A' + 10;
                    else throw new OxygenEditorJsonException("A JSON Unicode escape is invalid.");
                }
                return (char)code;
            }

            object ReadNumber()
            {
                int start = _index;
                if (Take('-') && End) throw new OxygenEditorJsonException("A JSON number is incomplete.");
                if (Take('0'))
                {
                    if (!End && _text[_index] >= '0' && _text[_index] <= '9') throw new OxygenEditorJsonException("A JSON number has a leading zero.");
                }
                else
                {
                    if (End || _text[_index] < '1' || _text[_index] > '9') throw new OxygenEditorJsonException("A JSON number is invalid.");
                    while (!End && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                }
                bool real = false;
                if (!End && _text[_index] == '.')
                {
                    real = true;
                    _index++;
                    int digits = _index;
                    while (!End && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                    if (_index == digits) throw new OxygenEditorJsonException("A JSON fraction is missing digits.");
                }
                if (!End && (_text[_index] == 'e' || _text[_index] == 'E'))
                {
                    real = true;
                    _index++;
                    if (!End && (_text[_index] == '+' || _text[_index] == '-')) _index++;
                    int digits = _index;
                    while (!End && _text[_index] >= '0' && _text[_index] <= '9') _index++;
                    if (_index == digits) throw new OxygenEditorJsonException("A JSON exponent is missing digits.");
                }
                string text = _text.Substring(start, _index - start);
                if (!real && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer)) return integer;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number))
                    throw new OxygenEditorJsonException("A JSON number is outside the supported range.");
                return number;
            }

            object ReadLiteral(string literal, object value)
            {
                if (_index + literal.Length > _text.Length || string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                    throw new OxygenEditorJsonException("A JSON literal is invalid.");
                _index += literal.Length;
                return value;
            }

            bool Take(char value)
            {
                if (End || _text[_index] != value) return false;
                _index++;
                return true;
            }
        }

        sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object left, object right) { return ReferenceEquals(left, right); }
            public int GetHashCode(object value) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); }
        }
    }

    internal sealed class OxygenEditorJsonException : IOException
    {
        public OxygenEditorJsonException(string message) : base(message) { }
    }
}
