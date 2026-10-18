using System;
using System.Collections.Generic;

namespace HungerAndHavoc.Data
{
    internal readonly struct RHAH_ContentQuery
    {
        internal RHAH_ContentQuery(
            float ageYears,
            bool adult,
            bool female,
            float? temperature,
            string incidentDisplayId,
            bool historiesEnabled,
            bool traitsEnabled,
            Func<string, bool> historyEnabled,
            Func<string, bool> traitEnabled,
            Func<string, float> traitWeight,
            bool male = false,
            bool filterTraitAge = true,
            Func<string, bool> traitAlreadyOwned = null)
        {
            AgeYears = ageYears;
            Adult = adult;
            Female = female;
            Temperature = temperature;
            IncidentDisplayId = incidentDisplayId;
            HistoriesEnabled = historiesEnabled;
            TraitsEnabled = traitsEnabled;
            HistoryEnabled = historyEnabled ?? (_ => true);
            TraitEnabled = traitEnabled ?? (_ => true);
            TraitWeight = traitWeight ?? (id => RHAH_ContentCatalog.DefaultTraitWeight(id));
            Male = male;
            FilterTraitAge = filterTraitAge;
            TraitAlreadyOwned = traitAlreadyOwned ?? (_ => false);
        }

        internal float AgeYears { get; }
        internal bool Adult { get; }
        internal bool Female { get; }
        internal float? Temperature { get; }
        internal string IncidentDisplayId { get; }
        internal bool HistoriesEnabled { get; }
        internal bool TraitsEnabled { get; }
        internal Func<string, bool> HistoryEnabled { get; }
        internal Func<string, bool> TraitEnabled { get; }
        internal Func<string, float> TraitWeight { get; }
        internal bool Male { get; }
        internal bool FilterTraitAge { get; }
        internal Func<string, bool> TraitAlreadyOwned { get; }
    }

    internal static class RHAH_ContentSelector
    {
        internal static RHAH_HistoryRecord SelectHistory(RHAH_ContentQuery query, Func<float> random)
        {
            if (!query.HistoriesEnabled || random == null)
            {
                return null;
            }

            List<RHAH_HistoryRecord> candidates = new List<RHAH_HistoryRecord>();
            IReadOnlyList<RHAH_HistoryRecord> all = RHAH_ContentCatalog.Histories;
            for (int i = 0; i < all.Count; i++)
            {
                RHAH_HistoryRecord record = all[i];
                if (query.HistoryEnabled(record.DisplayId) && MatchesHistory(record, query, true))
                {
                    candidates.Add(record);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            if (query.Adult)
            {
                return Weighted(candidates, record => record.Weight, random);
            }

            List<RHAH_HistoryRecord> adults = new List<RHAH_HistoryRecord>();
            for (int i = 0; i < all.Count; i++)
            {
                RHAH_HistoryRecord record = all[i];
                if (record.Slot == RHAH_HistorySlot.Adulthood &&
                    query.HistoryEnabled(record.DisplayId) &&
                    MatchesHistory(record, query, false))
                {
                    adults.Add(record);
                }
            }

            if (adults.Count == 0)
            {
                return Weighted(candidates, record => record.Weight, random);
            }

            List<RHAH_HistoryRecord> linked = new List<RHAH_HistoryRecord>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (LinksAny(candidates[i], adults))
                {
                    linked.Add(candidates[i]);
                }
            }

            if (linked.Count == 0)
            {
                return Weighted(candidates, record => record.Weight, random);
            }

            return Weighted(linked, record =>
            {
                float adultWeight = 0f;
                for (int i = 0; i < adults.Count; i++)
                {
                    if (Links(record, adults[i].DisplayId))
                    {
                        adultWeight += adults[i].Weight;
                    }
                }

                float prior = adultWeight < 0.001f ? 0.001f : adultWeight;
                return record.Weight * prior;
            }, random);
        }

        internal static RHAH_TraitRecord SelectTrait(
            RHAH_ContentQuery query,
            string historyDisplayId,
            Func<float> random,
            Func<RHAH_TraitRecord, bool> gain)
        {
            if (!query.TraitsEnabled || random == null || gain == null)
            {
                return null;
            }

            List<RHAH_TraitRecord> slot = CollectSlot(query);
            if (slot.Count == 0)
            {
                return null;
            }

            List<RHAH_TraitRecord> history = MatchingHistory(slot, historyDisplayId);
            if (history.Count > 0)
            {
                return DrawTrait(history, query, random, gain);
            }

            List<RHAH_TraitRecord> incidents = MatchingIncident(slot, query.IncidentDisplayId);
            if (incidents.Count > 0)
            {
                return DrawTrait(incidents, query, random, gain);
            }

            return DrawTrait(slot, query, random, gain);
        }

        internal static bool CategoryMatches(RHAH_ContentCategory category, string incidentDisplayId)
        {
            if (category == RHAH_ContentCategory.None || string.IsNullOrEmpty(incidentDisplayId))
            {
                return false;
            }

            int number;
            if (incidentDisplayId.Length != 5 || incidentDisplayId[0] != 'I' || incidentDisplayId[1] != '-' ||
                !int.TryParse(incidentDisplayId.Substring(2), out number))
            {
                return false;
            }

            bool theft = incidentDisplayId == "I-006" || incidentDisplayId == "I-007" || incidentDisplayId == "I-043";
            bool siege = incidentDisplayId == "I-014" || incidentDisplayId == "I-030" || incidentDisplayId == "I-045";
            bool plague = number >= 36 && number <= 50;
            if (category == RHAH_ContentCategory.Plague)
            {
                return plague;
            }

            if (category == RHAH_ContentCategory.Theft)
            {
                return theft;
            }

            if (category == RHAH_ContentCategory.Conflict)
            {
                return theft || siege;
            }

            return category == RHAH_ContentCategory.Siege && siege;
        }

        internal static bool MatchesQuery(string title, string displayId, string extra, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string[] terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string haystack = (title ?? string.Empty) + " " + (displayId ?? string.Empty) + " " + (extra ?? string.Empty);
            for (int i = 0; i < terms.Length; i++)
            {
                if (haystack.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        internal static string ListKey(int mode, string displayId, RHAH_ContentCategory category, string sourceName, bool owned)
        {
            int safe = mode < 0 || mode > 2 ? 0 : mode;
            if (safe == 1)
            {
                return string.IsNullOrEmpty(displayId) ? "#" : displayId.Substring(0, 1).ToUpperInvariant();
            }

            if (safe == 2)
            {
                if (!owned)
                {
                    return "vanilla";
                }

                return category == RHAH_ContentCategory.None ? "none" : category.ToString().ToLowerInvariant();
            }

            return string.IsNullOrEmpty(sourceName) ? string.Empty : sourceName;
        }

        static List<RHAH_TraitRecord> CollectSlot(RHAH_ContentQuery query)
        {
            List<RHAH_TraitRecord> slot = new List<RHAH_TraitRecord>();
            IReadOnlyList<RHAH_TraitRecord> all = RHAH_ContentCatalog.Traits;
            for (int i = 0; i < all.Count; i++)
            {
                RHAH_TraitRecord record = all[i];
                if ((query.FilterTraitAge && !MatchesTraitSlot(record, query.Adult)) ||
                    !query.TraitEnabled(record.DisplayId) ||
                    query.TraitAlreadyOwned(record.DisplayId))
                {
                    continue;
                }

                if (query.TraitWeight(record.DisplayId) <= 0f)
                {
                    continue;
                }

                slot.Add(record);
            }

            return slot;
        }

        static bool MatchesHistory(RHAH_HistoryRecord record, RHAH_ContentQuery query, bool requireSlot)
        {
            if (requireSlot && (record.Slot == RHAH_HistorySlot.Adulthood) != query.Adult)
            {
                return false;
            }

            if (record.MinimumAge.HasValue)
            {
                bool below = record.MinimumInclusive
                    ? query.AgeYears < record.MinimumAge.Value
                    : query.AgeYears <= record.MinimumAge.Value;
                if (below)
                {
                    return false;
                }
            }

            if (record.MaximumAge.HasValue)
            {
                bool above = record.MaximumInclusive
                    ? query.AgeYears > record.MaximumAge.Value
                    : query.AgeYears >= record.MaximumAge.Value;
                if (above)
                {
                    return false;
                }
            }

            if (record.Gender == RHAH_ContentGender.Female && !query.Female)
            {
                return false;
            }

            if (record.Gender == RHAH_ContentGender.Male && !query.Male)
            {
                return false;
            }

            if (query.Temperature.HasValue &&
                (query.Temperature.Value < record.MinimumTemperature - 0.001f ||
                 query.Temperature.Value > record.MaximumTemperature + 0.001f))
            {
                return false;
            }

            return MatchesIncident(record.IncidentDisplayIds, record.Category, query.IncidentDisplayId);
        }

        static bool MatchesIncident(
            IReadOnlyList<string> incidentDisplayIds,
            RHAH_ContentCategory category,
            string incidentDisplayId)
        {
            bool constrained = incidentDisplayIds.Count > 0 || category != RHAH_ContentCategory.None;
            if (!constrained)
            {
                return true;
            }

            if (string.IsNullOrEmpty(incidentDisplayId))
            {
                return false;
            }

            for (int i = 0; i < incidentDisplayIds.Count; i++)
            {
                if (string.Equals(incidentDisplayIds[i], incidentDisplayId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return CategoryMatches(category, incidentDisplayId);
        }

        static bool MatchesTraitSlot(RHAH_TraitRecord record, bool adult)
        {
            if (record.Slot == RHAH_TraitSlot.Any)
            {
                return true;
            }

            return adult
                ? record.Slot == RHAH_TraitSlot.Adulthood
                : record.Slot == RHAH_TraitSlot.Childhood;
        }

        static List<RHAH_TraitRecord> MatchingHistory(List<RHAH_TraitRecord> slot, string historyDisplayId)
        {
            List<RHAH_TraitRecord> matched = new List<RHAH_TraitRecord>();
            if (string.IsNullOrEmpty(historyDisplayId))
            {
                return matched;
            }

            for (int i = 0; i < slot.Count; i++)
            {
                IReadOnlyList<string> histories = slot[i].HistoryDisplayIds;
                for (int j = 0; j < histories.Count; j++)
                {
                    if (string.Equals(histories[j], historyDisplayId, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(slot[i]);
                        break;
                    }
                }
            }

            return matched;
        }

        static List<RHAH_TraitRecord> MatchingIncident(List<RHAH_TraitRecord> slot, string incidentDisplayId)
        {
            List<RHAH_TraitRecord> matched = new List<RHAH_TraitRecord>();
            for (int i = 0; i < slot.Count; i++)
            {
                if ((slot[i].IncidentDisplayIds.Count > 0 || slot[i].Category != RHAH_ContentCategory.None) &&
                    MatchesIncident(slot[i].IncidentDisplayIds, slot[i].Category, incidentDisplayId))
                {
                    matched.Add(slot[i]);
                }
            }

            return matched;
        }

        static RHAH_TraitRecord DrawTrait(
            List<RHAH_TraitRecord> candidates,
            RHAH_ContentQuery query,
            Func<float> random,
            Func<RHAH_TraitRecord, bool> gain)
        {
            List<RHAH_TraitRecord> remaining = new List<RHAH_TraitRecord>(candidates);
            int attempts = remaining.Count < 8 ? remaining.Count : 8;
            for (int attempt = 0; attempt < attempts && remaining.Count > 0; attempt++)
            {
                RHAH_TraitRecord selected = Weighted(remaining, record =>
                {
                    float weight = query.TraitWeight(record.DisplayId);
                    return weight < 0.001f ? 0.001f : weight;
                }, random);
                if (selected == null)
                {
                    return null;
                }

                float chance = query.TraitWeight(selected.DisplayId) / 100f;
                if (chance < 0f)
                {
                    chance = 0f;
                }
                else if (chance > 1f)
                {
                    chance = 1f;
                }

                if (random() >= chance)
                {
                    return null;
                }

                if (gain(selected))
                {
                    return selected;
                }

                remaining.Remove(selected);
            }

            return null;
        }

        static bool LinksAny(RHAH_HistoryRecord child, List<RHAH_HistoryRecord> adults)
        {
            for (int i = 0; i < adults.Count; i++)
            {
                if (Links(child, adults[i].DisplayId))
                {
                    return true;
                }
            }

            return false;
        }

        static bool Links(RHAH_HistoryRecord child, string adultDisplayId)
        {
            for (int i = 0; i < child.LinkedAdultDisplayIds.Count; i++)
            {
                if (string.Equals(child.LinkedAdultDisplayIds[i], adultDisplayId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        static T Weighted<T>(List<T> items, Func<T, float> weight, Func<float> random) where T : class
        {
            float total = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                float value = weight(items[i]);
                if (value > 0f)
                {
                    total += value;
                }
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = random();
            if (roll < 0f)
            {
                roll = 0f;
            }
            else if (roll >= 1f)
            {
                roll = 0.999999f;
            }

            float cursor = roll * total;
            for (int i = 0; i < items.Count; i++)
            {
                float value = weight(items[i]);
                if (value <= 0f)
                {
                    continue;
                }

                cursor -= value;
                if (cursor <= 0f)
                {
                    return items[i];
                }
            }

            return items[items.Count - 1];
        }
    }
}
