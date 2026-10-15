using System;
using System.Collections.Generic;
using IrisMenus;
using UnityEngine;
using Verse;

namespace Odf.PrisonerNames
{
    static class PrisonerAgeSettings
    {
        static bool raceTab;
        static string selectedRace = "Human";
        static NamePolicy bound;
        static readonly Dictionary<AgeNameRule, string> ageBuffers = new Dictionary<AgeNameRule, string>();
        static readonly bool[] ruleScopes = { false, true };
        static IEnumerable<string> raceOptions;
        static float newMinimum = 21f;
        static string newMinimumBuffer = "21";
        static float sampleAge = 18f;
        static string sampleAgeBuffer = "18";

        internal static double SampleAge => sampleAge;
        internal static string SampleRace => raceTab ? selectedRace : "Human";

        internal static void Draw(Listing_Standard listing, NamePolicy policy, Action changed, Action previewChanged)
        {
            if (!ReferenceEquals(bound, policy))
            {
                bound = policy;
                ageBuffers.Clear();
                raceOptions = RaceOptions(policy);
            }
            bool enabled = policy.UseAgePatterns;
            listing.CheckboxLabeled("OPN_UseAgePatterns".Translate(), ref enabled);
            if (enabled != policy.UseAgePatterns) { policy.UseAgePatterns = enabled; changed(); }
            if (!enabled) return;
            listing.Label("OPN_AgeRulesIntro".Translate());
            listing.Gap(8f);
            MenuControls.Select(listing, "OPN_Race".Translate(), raceTab, ruleScopes,
                specific => (specific ? "OPN_RaceAgeRules" : "OPN_DefaultAgeRules").Translate(),
                specific =>
                {
                    if (raceTab == specific) return;
                    raceTab = specific;
                    previewChanged();
                });
            listing.Gap(12f);
            if (raceTab) DrawRace(listing, policy, changed, previewChanged);
            else DrawDefaults(listing, policy, changed);
            listing.Gap(16f);
            MenuControls.Section(listing, "OPN_PolicyPreview".Translate());
            float previousAge = sampleAge;
            MenuControls.Number(listing, "OPN_PreviewAge".Translate(), ref sampleAge, ref sampleAgeBuffer, 0f, 100000f);
            if (sampleAge != previousAge) previewChanged();
        }

        static void DrawDefaults(Listing_Standard listing, NamePolicy policy, Action changed)
        {
            DrawHeader(listing);
            int remove = -1;
            bool reorder = false;
            for (int i = 0; i < policy.Ages.Count; i++)
            {
                AgeNameRule rule = policy.Ages[i];
                double previous = rule.MinimumAge;
                if (DrawRow(listing, policy, rule, i > 0, true, changed)) remove = i;
                if (rule.MinimumAge != previous)
                {
                    reorder = true;
                    for (int raceIndex = 0; raceIndex < policy.RaceNames.Count; raceIndex++)
                        for (int ageIndex = 0; ageIndex < policy.RaceNames[raceIndex].Ages.Count; ageIndex++)
                            if (policy.RaceNames[raceIndex].Ages[ageIndex].MinimumAge == previous)
                                policy.RaceNames[raceIndex].Ages[ageIndex].MinimumAge = rule.MinimumAge;
                    changed();
                }
            }
            if (remove >= 0)
            {
                double minimum = policy.Ages[remove].MinimumAge;
                ageBuffers.Remove(policy.Ages[remove]);
                policy.Ages.RemoveAt(remove);
                for (int i = 0; i < policy.RaceNames.Count; i++)
                    policy.RaceNames[i].Ages.RemoveAll(rule => rule.MinimumAge == minimum);
                changed();
            }
            if (reorder) policy.Ages.Sort((left, right) => left.MinimumAge.CompareTo(right.MinimumAge));
            listing.Gap(16f);
            listing.GapLine(8f);
            MenuControls.Number(listing, "OPN_AgeMinimum".Translate(), ref newMinimum, ref newMinimumBuffer, 0f, 100000f);
            listing.Gap(8f);
            if (listing.ButtonText("OPN_AddAgeBand".Translate()))
            {
                bool exists = false;
                for (int i = 0; i < policy.Ages.Count; i++) exists |= policy.Ages[i].MinimumAge == newMinimum;
                if (!exists)
                {
                    policy.Ages.Add(new AgeNameRule { MinimumAge = newMinimum, Pattern = policy.PatternFor(null, newMinimum, false), ChinesePattern = policy.PatternFor(null, newMinimum, true) });
                    policy.Ages.Sort((left, right) => left.MinimumAge.CompareTo(right.MinimumAge));
                    changed();
                }
            }
        }

        static void DrawRace(Listing_Standard listing, NamePolicy policy, Action changed, Action previewChanged)
        {
            if (!PrisonerRaceNames.AvailableOrMissing(selectedRace))
            {
                selectedRace = "Human";
                previewChanged();
            }
            MenuControls.Select(listing, "OPN_Race".Translate(), selectedRace, raceOptions,
                raceName => PrisonerRaceNames.Label(raceName) + " (" + raceName + ")",
                raceName =>
                {
                    if (selectedRace == raceName) return;
                    selectedRace = raceName;
                    previewChanged();
                });
            listing.Gap(8f);
            RaceNameRule race = FindRace(policy);
            bool enabled = race != null && race.Enabled;
            bool previous = enabled;
            listing.CheckboxLabeled("OPN_UseRaceAgeRules".Translate(), ref enabled);
            if (enabled != previous)
            {
                if (race == null)
                {
                    race = new RaceNameRule { Race = selectedRace };
                    policy.RaceNames.Add(race);
                }
                race.Enabled = enabled;
                changed();
            }
            if (!enabled) { listing.Label("OPN_AgeInherited".Translate()); return; }
            listing.Gap(12f);
            DrawHeader(listing);
            for (int i = 0; i < policy.Ages.Count; i++)
            {
                AgeNameRule global = policy.Ages[i];
                AgeNameRule custom = null;
                for (int j = 0; j < race.Ages.Count; j++)
                    if (race.Ages[j].MinimumAge == global.MinimumAge) { custom = race.Ages[j]; break; }
                if (custom == null)
                {
                    Rect row = listing.GetRect(30f);
                    Widgets.Label(new Rect(row.x, row.y, 112f, row.height), Range(policy, global.MinimumAge));
                    Widgets.Label(new Rect(row.x + 120f, row.y, Math.Max(20f, row.width - 224f), row.height), "OPN_AgeInherited".Translate());
                    if (Widgets.ButtonText(new Rect(row.xMax - 96f, row.y, 96f, row.height), "OPN_AgeOverride".Translate()))
                    {
                        race.Ages.Add(global.Clone());
                        changed();
                    }
                    listing.Gap(8f);
                }
                else if (DrawRow(listing, policy, custom, false, false, changed))
                {
                    race.Ages.Remove(custom);
                    changed();
                }
            }
        }

        static IEnumerable<string> RaceOptions(NamePolicy policy)
        {
            IReadOnlyList<ThingDef> available = PrisonerRaceNames.All;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < available.Count; i++)
                if (seen.Add(available[i].defName)) yield return available[i].defName;
            for (int i = 0; i < policy.RaceNames.Count; i++)
            {
                string race = policy.RaceNames[i].Race;
                if (PrisonerRaceNames.AvailableOrMissing(race) && seen.Add(race)) yield return race;
            }
        }

        static RaceNameRule FindRace(NamePolicy policy)
        {
            for (int i = 0; i < policy.RaceNames.Count; i++)
                if (policy.RaceNames[i].Race == selectedRace) return policy.RaceNames[i];
            return null;
        }

        static void DrawHeader(Listing_Standard listing)
        {
            Rect row = listing.GetRect(24f);
            Widgets.Label(new Rect(row.x, row.y, 112f, row.height), "OPN_AgeRange".Translate());
            Widgets.Label(new Rect(row.x + 120f, row.y, Math.Max(20f, row.width - 224f), row.height), "OPN_Pattern".Translate());
        }

        static bool DrawRow(Listing_Standard listing, NamePolicy policy, AgeNameRule rule, bool editableMinimum, bool defaultRule, Action changed)
        {
            Rect row = listing.GetRect(30f);
            if (editableMinimum)
            {
                if (!ageBuffers.TryGetValue(rule, out string buffer)) buffer = NamePolicy.FormatNumber(rule.MinimumAge);
                float minimum = (float)rule.MinimumAge;
                float previous = minimum;
                Widgets.TextFieldNumeric(new Rect(row.x, row.y, 56f, row.height), ref minimum, ref buffer, 0f, 100000f);
                ageBuffers[rule] = buffer;
                if (minimum != previous) rule.MinimumAge = minimum;
                Widgets.Label(new Rect(row.x + 60f, row.y, 52f, row.height), Upper(policy, rule.MinimumAge));
            }
            else Widgets.Label(new Rect(row.x, row.y, 112f, row.height), Range(policy, rule.MinimumAge));
            string pattern = NamingLanguage.Chinese ? rule.ChinesePattern : rule.Pattern;
            string edited = Widgets.TextField(new Rect(row.x + 120f, row.y, Math.Max(20f, row.width - 224f), row.height), pattern ?? string.Empty);
            if (edited != (pattern ?? string.Empty))
            {
                if (NamingLanguage.Chinese) rule.ChinesePattern = edited;
                else rule.Pattern = edited;
                changed();
            }
            bool remove = false;
            if (!defaultRule || editableMinimum)
                remove = Widgets.ButtonText(new Rect(row.xMax - 96f, row.y, 96f, row.height), (defaultRule ? "OPN_Remove" : "OPN_AgeInherit").Translate());
            listing.Gap(8f);
            return remove;
        }

        static string Range(NamePolicy policy, double minimum) => NamePolicy.FormatNumber(minimum) + Upper(policy, minimum);

        static string Upper(NamePolicy policy, double minimum)
        {
            double next = double.PositiveInfinity;
            for (int i = 0; i < policy.Ages.Count; i++)
                if (policy.Ages[i].MinimumAge > minimum && policy.Ages[i].MinimumAge < next) next = policy.Ages[i].MinimumAge;
            return double.IsPositiveInfinity(next) ? "+" : "–" + NamePolicy.FormatNumber(next);
        }
    }
}
