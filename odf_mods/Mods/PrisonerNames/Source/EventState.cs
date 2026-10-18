using System;
using System.Collections.Generic;
using Oxygendsl.Core;

namespace Odf.PrisonerNames
{
    public sealed class EventState : IOxygenHostValue
    {
        readonly ScalarBag values;
        readonly ValueCallable read;
        readonly ValueWriter write;
        public double Score;
        public bool ScoreChanged;
        readonly bool manualScore;

        public EventState(ScalarBag values, double score, bool manualScore)
        {
            this.values = values;
            Score = score;
            this.manualScore = manualScore;
            read = new ValueCallable(values);
            write = new ValueWriter(this);
        }

        public bool TryGetMember(string name, out object value)
        {
            switch (name)
            {
                case "get": value = read; return true;
                case "set": value = write; return true;
                case "score": value = Score; return true;
                default: value = null; return false;
            }
        }

        sealed class ValueWriter : IOxygenHostCallable
        {
            readonly EventState owner;
            public ValueWriter(EventState owner) { this.owner = owner; }

            public object Invoke(IList<object> positional, IDictionary<string, object> named)
            {
                string key = Required(positional, named, "key", 0);
                object raw = named != null && named.ContainsKey("value") ? named["value"] : positional != null && positional.Count > 1 ? positional[1] : null;
                if (string.Equals(key, "score", StringComparison.Ordinal))
                {
                    if (owner.manualScore) return null;
                    double number = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture);
                    if (double.IsNaN(number) || double.IsInfinity(number)) throw new ArgumentException("A score must be finite.");
                    owner.Score = number;
                    owner.ScoreChanged = true;
                    return null;
                }
                owner.values.Set(key, raw);
                return null;
            }
        }

        static string Required(IList<object> positional, IDictionary<string, object> named, string name, int index)
        {
            object value = null;
            if (named != null && named.TryGetValue(name, out value)) return Convert.ToString(value);
            if (positional != null && positional.Count > index) return Convert.ToString(positional[index]);
            throw new ArgumentException("A value key is required.");
        }
    }

    public sealed class ValueCallable : IOxygenHostCallable
    {
        readonly ScalarBag values;
        public ValueCallable(ScalarBag values) { this.values = values; }

        public object Invoke(IList<object> positional, IDictionary<string, object> named)
        {
            string key = positional != null && positional.Count > 0 ? Convert.ToString(positional[0]) : null;
            if (named != null && named.ContainsKey("key")) key = Convert.ToString(named["key"]);
            object fallback = null;
            bool hasFallback = false;
            if (named != null && named.ContainsKey("default")) { fallback = named["default"]; hasFallback = true; }
            else if (positional != null && positional.Count > 1) { fallback = positional[1]; hasFallback = true; }
            object stored;
            if (values != null && values.TryGet(key, out stored)) return stored;
            if (!hasFallback) throw new ArgumentException("The value '" + key + "' is not stored.");
            return fallback;
        }
    }
}
