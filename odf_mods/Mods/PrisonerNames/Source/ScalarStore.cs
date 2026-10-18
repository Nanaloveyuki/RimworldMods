using System;
using System.Collections.Generic;
using System.Globalization;

namespace Odf.PrisonerNames
{
    public sealed class ScalarValue
    {
        public bool IsNull;
        public bool? Bool;
        public string Text;
        public double? Number;

        public static ScalarValue From(object value)
        {
            if (value == null) return new ScalarValue { IsNull = true };
            if (value is bool flag) return new ScalarValue { Bool = flag };
            if (value is string text) return new ScalarValue { Text = text };
            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new ArgumentException("Only finite numbers can be stored.");
                return new ScalarValue { Number = number };
            }
            throw new ArgumentException("Only bool, string, finite number, or null can be stored.");
        }

        public object Unwrap()
        {
            if (IsNull) return null;
            if (Bool.HasValue) return Bool.Value;
            if (Number.HasValue) return Number.Value;
            return Text;
        }

        public ScalarValue Copy()
        {
            return new ScalarValue { IsNull = IsNull, Bool = Bool, Text = Text, Number = Number };
        }
    }

    public sealed class ScalarBag
    {
        readonly Dictionary<string, ScalarValue> values = new Dictionary<string, ScalarValue>(StringComparer.Ordinal);

        public int Count { get { return values.Count; } }

        public bool TryGet(string key, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;
            ScalarValue stored;
            if (!values.TryGetValue(key, out stored)) return false;
            value = stored.Unwrap();
            return true;
        }

        public object Get(string key, object fallback)
        {
            object value;
            return TryGet(key, out value) ? value : fallback;
        }

        public void Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A value key is required.", nameof(key));
            if (key.Length > 64) throw new ArgumentException("A value key may contain at most 64 characters.");
            values[key] = ScalarValue.From(value);
        }

        public ScalarBag Copy()
        {
            var copy = new ScalarBag();
            foreach (KeyValuePair<string, ScalarValue> pair in values)
                copy.values[pair.Key] = pair.Value.Copy();
            return copy;
        }

        public void Replace(ScalarBag source)
        {
            values.Clear();
            if (source == null) return;
            foreach (KeyValuePair<string, ScalarValue> pair in source.values)
                values[pair.Key] = pair.Value.Copy();
        }

        public List<KeyValuePair<string, ScalarValue>> Entries()
        {
            var list = new List<KeyValuePair<string, ScalarValue>>(values.Count);
            foreach (KeyValuePair<string, ScalarValue> pair in values) list.Add(pair);
            list.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            return list;
        }
    }
}
