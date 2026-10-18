using System;
using System.Collections.Generic;
using Oxygendsl.Core;
using Verse;

namespace Oxygendsl.Mod
{
    public sealed class OxygenLocalization : IOxygenLocalization
    {
        public static readonly OxygenLocalization Instance = new OxygenLocalization();

        private OxygenLocalization()
        {
        }

        public string Translate(string key, IDictionary<string, object> arguments)
        {
            if (LanguageDatabase.activeLanguage == null)
            {
                throw new InvalidOperationException("No active language is loaded. lang cannot translate '" + (key ?? string.Empty) + "'.");
            }

            key = key ?? string.Empty;
            if (!key.TryTranslate(out TaggedString translated))
            {
                if (LanguageDatabase.defaultLanguage == null || !LanguageDatabase.defaultLanguage.HaveTextForKey(key))
                    return key;
                translated = key.Translate();
            }
            if (arguments != null && arguments.Count != 0)
            {
                NamedArgument[] substitutions = new NamedArgument[arguments.Count];
                int index = 0;
                foreach (KeyValuePair<string, object> argument in arguments)
                    substitutions[index++] = new NamedArgument(argument.Value, argument.Key);
                translated = translated.Formatted(substitutions);
            }

            return translated.ToString();
        }
    }
}
