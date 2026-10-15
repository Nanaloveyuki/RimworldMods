using System;
using System.Collections.Generic;
using Verse;

namespace Odf.PrisonerNames
{
    static class PrisonerRaceNames
    {
        static LoadedLanguage language;
        static int definitionCount = -1;
        static readonly List<ThingDef> races = new List<ThingDef>();
        static readonly Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static IReadOnlyList<ThingDef> All
        {
            get { Ensure(); return races; }
        }

        internal static string Label(string defName)
        {
            Ensure();
            return defName != null && labels.TryGetValue(defName, out string label) ? label : defName ?? string.Empty;
        }

        internal static bool AvailableOrMissing(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            Ensure();
            return labels.ContainsKey(defName) || DefDatabase<ThingDef>.GetNamedSilentFail(defName) == null;
        }

        static void Ensure()
        {
            List<ThingDef> definitions = DefDatabase<ThingDef>.AllDefsListForReading;
            LoadedLanguage active = LanguageDatabase.activeLanguage;
            if (ReferenceEquals(language, active) && definitionCount == definitions.Count) return;
            language = active;
            definitionCount = definitions.Count;
            races.Clear();
            labels.Clear();
            active?.LoadData();
            LanguageDatabase.defaultLanguage?.LoadData();
            for (int i = 0; i < definitions.Count; i++)
            {
                ThingDef race = definitions[i];
                if (race.race == null || !race.race.Humanlike || race.thingClass == null || !typeof(Pawn).IsAssignableFrom(race.thingClass)) continue;
                races.Add(race);
                labels[race.defName] = LocalizedLabel(race, active, LanguageDatabase.defaultLanguage);
            }
            races.Sort((left, right) =>
            {
                int order = string.Compare(labels[left.defName], labels[right.defName], StringComparison.CurrentCulture);
                return order != 0 ? order : string.CompareOrdinal(left.defName, right.defName);
            });
        }

        static string LocalizedLabel(ThingDef race, LoadedLanguage active, LoadedLanguage english)
        {
            DefInjectionPackage.DefInjection translated = FindLabel(active, race.defName);
            if (translated != null && !translated.isPlaceholder && !string.IsNullOrWhiteSpace(translated.injection))
                return translated.injection;
            DefInjectionPackage.DefInjection fallback = FindLabel(english, race.defName);
            if (fallback != null && !fallback.isPlaceholder && !string.IsNullOrWhiteSpace(fallback.injection))
                return fallback.injection;
            string original = translated != null && translated.injected ? translated.replacedString : race.label;
            return string.IsNullOrWhiteSpace(original) ? race.defName : original;
        }

        static DefInjectionPackage.DefInjection FindLabel(LoadedLanguage loaded, string defName)
        {
            if (loaded == null) return null;
            string path = defName + ".label";
            for (int i = 0; i < loaded.defInjections.Count; i++)
            {
                DefInjectionPackage package = loaded.defInjections[i];
                if (!typeof(ThingDef).IsAssignableFrom(package.defType)) continue;
                if (package.injections.TryGetValue(path, out DefInjectionPackage.DefInjection found)) return found;
                foreach (DefInjectionPackage.DefInjection injection in package.injections.Values)
                    if (string.Equals(injection.normalizedPath, path, StringComparison.Ordinal)) return injection;
            }
            return null;
        }
    }
}
