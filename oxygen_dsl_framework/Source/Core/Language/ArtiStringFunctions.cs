// Derived from AdvancedRimTalk Source/Arti (MIT License).
// Copyright (c) 2026 Nanaloveyuki
// Adapted for Oxygendsl.Language; original prompt/RimTalk/Verse host interfaces are not retained.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Oxygendsl.Language
{
    internal static class ArtiStringFunctions
    {
        private static readonly string[] PublicNames =
        {
            "len",
            "remove_space",
            "remove_spaces",
            "append",
            "prepend",
            "upper",
            "uppercase",
            "lower",
            "lowercase",
            "capitalize",
            "title",
            "trim",
            "trim_start",
            "trim_end",
            "replace",
            "contains",
            "starts_with",
            "ends_with",
            "substring",
            "split",
            "repeat",
            "is_empty"
        };

        internal static IEnumerable<string> Names
        {
            get { return PublicNames; }
        }

        internal static bool TryGetCallable(string name, out IArtiCallable callable)
        {
            string canonicalName = CanonicalName(name);
            if (canonicalName == null)
            {
                callable = null;
                return false;
            }

            callable = new ArtiStringCallable(canonicalName);
            return true;
        }

        private static object Invoke(
            string name,
            bool hasReceiver,
            string receiver,
            IList<object> positional,
            IDictionary<string, object> named)
        {
            switch (name)
            {
                case "len":
                    return LengthOf(GetValue(name, hasReceiver, receiver, positional, named, 0, "value"));
                case "remove_space":
                    return RemoveSpace(GetText(name, hasReceiver, receiver, positional, named, 0, "value"));
                case "append":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value")
                        + GetText(name, hasReceiver, receiver, positional, named, 1, "suffix");
                case "prepend":
                    return GetText(name, hasReceiver, receiver, positional, named, 1, "prefix")
                        + GetText(name, hasReceiver, receiver, positional, named, 0, "value");
                case "upper":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").ToUpperInvariant();
                case "lower":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").ToLowerInvariant();
                case "capitalize":
                    return Capitalize(GetText(name, hasReceiver, receiver, positional, named, 0, "value"));
                case "title":
                    return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                        GetText(name, hasReceiver, receiver, positional, named, 0, "value").ToLowerInvariant());
                case "trim":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").Trim();
                case "trim_start":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").TrimStart();
                case "trim_end":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").TrimEnd();
                case "replace":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").Replace(
                        GetText(name, hasReceiver, receiver, positional, named, 1, "old"),
                        GetText(name, hasReceiver, receiver, positional, named, 2, "replacement"));
                case "contains":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").IndexOf(
                        GetText(name, hasReceiver, receiver, positional, named, 1, "part"),
                        StringComparison.Ordinal) >= 0;
                case "starts_with":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").StartsWith(
                        GetText(name, hasReceiver, receiver, positional, named, 1, "part"),
                        StringComparison.Ordinal);
                case "ends_with":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").EndsWith(
                        GetText(name, hasReceiver, receiver, positional, named, 1, "part"),
                        StringComparison.Ordinal);
                case "substring":
                    return Substring(name, hasReceiver, receiver, positional, named);
                case "split":
                    return Split(name, hasReceiver, receiver, positional, named);
                case "repeat":
                    return Repeat(name, hasReceiver, receiver, positional, named);
                case "is_empty":
                    return GetText(name, hasReceiver, receiver, positional, named, 0, "value").Length == 0;
                default:
                    throw new InvalidOperationException("Unknown string function '" + name + "'.");
            }
        }

        private static string GetText(
            string functionName,
            bool hasReceiver,
            string receiver,
            IList<object> positional,
            IDictionary<string, object> named,
            int position,
            string argumentName)
        {
            return ToText(GetArgument(
                functionName,
                hasReceiver,
                receiver,
                positional,
                named,
                position,
                argumentName,
                true));
        }

        private static object GetValue(
            string functionName,
            bool hasReceiver,
            string receiver,
            IList<object> positional,
            IDictionary<string, object> named,
            int position,
            string argumentName)
        {
            return GetArgument(functionName, hasReceiver, receiver, positional, named, position, argumentName, true);
        }

        private static int LengthOf(object value)
        {
            if (value == null) return 0;
            string text = value as string;
            if (text != null) return text.Length;
            OxygenCollection collection = value as OxygenCollection;
            if (collection != null) return collection.Count;
            RuntimeObject runtimeObject = value as RuntimeObject;
            if (runtimeObject != null) return runtimeObject.Count;
            ICollection generic = value as ICollection;
            return generic == null ? 0 : generic.Count;
        }

        private static object GetArgument(
            string functionName,
            bool hasReceiver,
            string receiver,
            IList<object> positional,
            IDictionary<string, object> named,
            int position,
            string argumentName,
            bool required)
        {
            if (hasReceiver && position == 0)
            {
                return receiver;
            }

            object value;
            if (named != null && named.TryGetValue(argumentName, out value))
            {
                return value;
            }

            int positionalIndex = hasReceiver ? position - 1 : position;
            if (positional != null && positionalIndex >= 0 && positionalIndex < positional.Count)
            {
                return positional[positionalIndex];
            }

            if (!required)
            {
                return null;
            }

            throw new InvalidOperationException(
                "String function '" + functionName + "' requires argument '" + argumentName + "'.");
        }

        private static string RemoveSpace(string value)
        {
            StringBuilder result = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character))
                {
                    result.Append(character);
                }
            }

            return result.ToString();
        }

        private static string Capitalize(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static string Substring(
            string functionName,
            bool hasReceiver,
            string receiver,
            IList<object> positional,
            IDictionary<string, object> named)
        {
            string value = GetText(functionName, hasReceiver, receiver, positional, named, 0, "value");
            int start = ToInt(GetArgument(
                functionName,
                hasReceiver,
                receiver,
                positional,
                named,
                1,
                "start",
                true));
            if (start < 0 || start > value.Length)
            {
                throw new InvalidOperationException("String substring start is outside the value.");
            }

            object lengthArgument = GetArgument(
                functionName,
                hasReceiver,
                receiver,
                positional,
                named,
                2,
                "length",
                false);
            if (lengthArgument == null)
            {
                return value.Substring(start);
            }

            int length = ToInt(lengthArgument);
            if (length < 0 || length > value.Length - start)
            {
                throw new InvalidOperationException("String substring length is outside the value.");
            }

            return value.Substring(start, length);
        }

        private static OxygenCollection Split(
            string functionName,
            bool hasReceiver,
            string receiver,
            IList<object> positional,
            IDictionary<string, object> named)
        {
            string value = GetText(functionName, hasReceiver, receiver, positional, named, 0, "value");
            string separator = GetText(functionName, hasReceiver, receiver, positional, named, 1, "separator");
            OxygenCollection result = new OxygenCollection(false, OxygenTypes.String, value.Length);
            if (separator.Length == 0)
            {
                foreach (char character in value) result.Add(character.ToString());
                return result;
            }

            foreach (string part in value.Split(new[] { separator }, StringSplitOptions.None)) result.Add(part);
            return result;
        }

        private static string Repeat(
            string functionName,
            bool hasReceiver,
            string receiver,
            IList<object> positional,
            IDictionary<string, object> named)
        {
            string value = GetText(functionName, hasReceiver, receiver, positional, named, 0, "value");
            int count = ToInt(GetArgument(
                functionName,
                hasReceiver,
                receiver,
                positional,
                named,
                1,
                "count",
                true));
            if (count < 0)
            {
                throw new InvalidOperationException("String repeat count cannot be negative.");
            }

            StringBuilder result = new StringBuilder();
            for (int index = 0; index < count; index++)
            {
                result.Append(value);
            }

            return result.ToString();
        }

        private static int ToInt(object value)
        {
            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            int result;
            if (int.TryParse(ToText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            throw new InvalidOperationException("Expected an integer string argument.");
        }

        private static string ToText(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string text = value as string;
            if (text != null)
            {
                return text;
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            IFormattable formattable = value as IFormattable;
            return formattable == null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string CanonicalName(string name)
        {
            switch (name)
            {
                case "len":
                    return "len";
                case "remove_space":
                case "remove_spaces":
                    return "remove_space";
                case "append":
                    return "append";
                case "prepend":
                    return "prepend";
                case "upper":
                case "uppercase":
                    return "upper";
                case "lower":
                case "lowercase":
                    return "lower";
                case "capitalize":
                    return "capitalize";
                case "title":
                    return "title";
                case "trim":
                    return "trim";
                case "trim_start":
                    return "trim_start";
                case "trim_end":
                    return "trim_end";
                case "replace":
                    return "replace";
                case "contains":
                    return "contains";
                case "starts_with":
                    return "starts_with";
                case "ends_with":
                    return "ends_with";
                case "substring":
                    return "substring";
                case "split":
                    return "split";
                case "repeat":
                    return "repeat";
                case "is_empty":
                    return "is_empty";
                default:
                    return null;
            }
        }

        private sealed class ArtiStringCallable : IArtiMethodCallable
        {
            private readonly string _name;

            public ArtiStringCallable(string name)
            {
                _name = name;
            }

            public object Invoke(IList<object> positionalArguments, IDictionary<string, object> namedArguments)
            {
                return ArtiStringFunctions.Invoke(
                    _name,
                    false,
                    null,
                    positionalArguments,
                    namedArguments);
            }

            public bool TryInvokeWithReceiver(
                object receiver,
                IList<object> positionalArguments,
                IDictionary<string, object> namedArguments,
                out object value)
            {
                string text = receiver as string;
                if (text == null)
                {
                    value = null;
                    return false;
                }

                value = ArtiStringFunctions.Invoke(
                    _name,
                    true,
                    text,
                    positionalArguments,
                    namedArguments);
                return true;
            }
        }
    }
}
