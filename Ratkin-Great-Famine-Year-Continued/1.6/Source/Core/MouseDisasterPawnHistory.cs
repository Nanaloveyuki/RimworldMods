using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public enum MouseDisasterPawnHistorySlot
    {
        Young = 0,
        Adult = 1
    }

    public enum MouseDisasterPawnHistoryEventCategory
    {
        None = 0,
        Plague = 1,
        Theft = 2,
        Conflict = 3,
        Siege = 4
    }

    [Flags]
    public enum MouseDisasterPawnHistoryRestriction
    {
        None = 0,
        NoWeapons = 1
    }

    public sealed class MouseDisasterPawnHistorySkillRange
    {
        public string SkillDefName { get; }
        public int Minimum { get; }
        public int Maximum { get; }

        public MouseDisasterPawnHistorySkillRange(string skillDefName, int minimum, int maximum)
        {
            SkillDefName = skillDefName;
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    public sealed class MouseDisasterPawnHistoryPassionRule
    {
        public string SkillDefName { get; }
        public float ActivationChance { get; }
        public float MajorChance { get; }

        public MouseDisasterPawnHistoryPassionRule(string skillDefName, float activationChance, float majorChance)
        {
            SkillDefName = skillDefName;
            ActivationChance = Mathf.Clamp01(activationChance);
            MajorChance = Mathf.Clamp01(majorChance);
        }
    }

    public sealed class MouseDisasterPawnHistoryDefinition
    {
        public string Id { get; }
        public string BackstoryDefName { get; }
        public string Title { get; }
        public MouseDisasterPawnHistorySlot Slot { get; }
        public float Chance { get; }
        public float MinimumAgeYears { get; }
        public bool MinimumAgeInclusive { get; }
        public float MaximumAgeYears { get; }
        public bool MaximumAgeInclusive { get; }
        public float MinimumTemperature { get; }
        public float MaximumTemperature { get; }
        public Gender RequiredGender { get; }
        public IReadOnlyList<string> EventIds { get; }
        public MouseDisasterPawnHistoryEventCategory EventCategory { get; }
        public string AgeText { get; }
        public string EventText { get; }
        public string AdultLinkText { get; }
        public IReadOnlyList<string> AdultLinkTitles { get; }
        public string SpecialText { get; }
        public IReadOnlyList<MouseDisasterPawnHistorySkillRange> SkillRanges { get; }
        public IReadOnlyList<MouseDisasterPawnHistoryPassionRule> PassionRules { get; }
        public IReadOnlyList<string[]> PassionGroups { get; }
        public MouseDisasterPawnHistoryRestriction Restrictions { get; }

        public string DisplayLabel
        {
            get
            {
                BackstoryDef backstory = DefDatabase<BackstoryDef>.GetNamedSilentFail(BackstoryDefName);
                string title = backstory?.TitleFor(Gender.None);
                return Id + " " + (title.NullOrEmpty() ? Title : title);
            }
        }

        public string Tooltip
        {
            get
            {
                List<string> lines = new List<string>();
                if (!string.IsNullOrWhiteSpace(AgeText))
                {
                    lines.Add("MouseDisaster_Settings_PawnHistory_Age".Translate(AgeText).ToString());
                }

                string eventConstraint = FormatEventConstraint();
                if (!eventConstraint.NullOrEmpty())
                {
                    lines.Add("MouseDisaster_Settings_PawnHistory_Event".Translate(eventConstraint).ToString());
                }

                if (SkillRanges.Count > 0)
                {
                    lines.Add("MouseDisaster_Settings_PawnHistory_Skills".Translate(
                        string.Join(", ", SkillRanges.Select(FormatSkillRange).ToArray())).ToString());
                }

                string passionConstraint = FormatPassionConstraint();
                if (!passionConstraint.NullOrEmpty())
                {
                    lines.Add("MouseDisaster_Settings_PawnHistory_Passions".Translate(passionConstraint).ToString());
                }

                if ((Restrictions & MouseDisasterPawnHistoryRestriction.NoWeapons) != 0)
                {
                    lines.Add("MouseDisaster_Settings_PawnHistory_NoWeapons".Translate().ToString());
                }

                return string.Join("\n", lines);
            }
        }

        public MouseDisasterPawnHistoryDefinition(
            string id,
            string backstoryDefName,
            string title,
            MouseDisasterPawnHistorySlot slot,
            float chance,
            float minimumAgeYears,
            bool minimumAgeInclusive,
            float maximumAgeYears,
            bool maximumAgeInclusive,
            float minimumTemperature,
            float maximumTemperature,
            Gender requiredGender,
            string[] eventIds,
            MouseDisasterPawnHistoryEventCategory eventCategory,
            string ageText,
            string eventText,
            string adultLinkText,
            string specialText,
            MouseDisasterPawnHistorySkillRange[] skillRanges,
            MouseDisasterPawnHistoryPassionRule[] passionRules,
            string[][] passionGroups,
            MouseDisasterPawnHistoryRestriction restrictions)
        {
            Id = id;
            BackstoryDefName = backstoryDefName;
            Title = title;
            Slot = slot;
            Chance = Mathf.Max(0.001f, chance);
            MinimumAgeYears = minimumAgeYears;
            MinimumAgeInclusive = minimumAgeInclusive;
            MaximumAgeYears = maximumAgeYears;
            MaximumAgeInclusive = maximumAgeInclusive;
            MinimumTemperature = minimumTemperature;
            MaximumTemperature = maximumTemperature;
            RequiredGender = requiredGender;
            EventIds = eventIds ?? new string[0];
            EventCategory = eventCategory;
            AgeText = ageText ?? string.Empty;
            EventText = eventText ?? string.Empty;
            AdultLinkText = adultLinkText ?? string.Empty;
            AdultLinkTitles = SplitAdultLinkTitles(AdultLinkText);
            SpecialText = specialText ?? string.Empty;
            SkillRanges = skillRanges ?? new MouseDisasterPawnHistorySkillRange[0];
            PassionRules = passionRules ?? new MouseDisasterPawnHistoryPassionRule[0];
            PassionGroups = passionGroups ?? new string[0][];
            Restrictions = restrictions;
        }

        public bool Matches(Pawn pawn, DevelopmentalStage stage, float? temperature, string eventId)
        {
            if (pawn == null || (Slot == MouseDisasterPawnHistorySlot.Adult) != stage.Adult())
            {
                return false;
            }

            float age = pawn.ageTracker?.AgeBiologicalYearsFloat ?? -1f;
            if (age < 0f ||
                (MinimumAgeInclusive ? age < MinimumAgeYears : age <= MinimumAgeYears) ||
                (MaximumAgeInclusive ? age > MaximumAgeYears : age >= MaximumAgeYears))
            {
                return false;
            }

            return MatchesGenerationContext(pawn, temperature, eventId);
        }

        public bool MatchesGenerationContext(Pawn pawn, float? temperature, string eventId)
        {
            if (pawn == null)
            {
                return false;
            }

            if (RequiredGender != Gender.None && pawn.gender != RequiredGender)
            {
                return false;
            }

            if (temperature.HasValue &&
                (temperature.Value < MinimumTemperature - 0.001f ||
                 temperature.Value > MaximumTemperature + 0.001f))
            {
                return false;
            }

            return MatchesEventContext(eventId);
        }

        public bool LinksToAdultTitle(string title)
        {
            return !title.NullOrEmpty() && AdultLinkTitles.Any(link =>
                string.Equals(link, title, StringComparison.OrdinalIgnoreCase));
        }

        private string FormatEventConstraint()
        {
            List<string> constraints = new List<string>();
            if (EventIds.Count > 0)
            {
                constraints.Add(string.Join(", ", EventIds));
            }

            if (EventCategory != MouseDisasterPawnHistoryEventCategory.None)
            {
                constraints.Add(("MouseDisaster_Settings_PawnHistory_EventCategory_" + EventCategory)
                    .Translate().ToString());
            }

            return string.Join("; ", constraints);
        }

        private static string FormatSkillRange(MouseDisasterPawnHistorySkillRange range)
        {
            SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(range.SkillDefName);
            string label = skillDef?.skillLabel;
            if (label.NullOrEmpty())
            {
                label = range.SkillDefName;
            }

            return label + " " + range.Minimum + "-" + range.Maximum;
        }

        private string FormatPassionConstraint()
        {
            List<string> constraints = new List<string>();
            foreach (MouseDisasterPawnHistoryPassionRule rule in PassionRules)
            {
                string label = ResolveSkillLabel(rule.SkillDefName);
                string activation = FormatChance(rule.ActivationChance);
                string major = rule.MajorChance > 0f
                    ? " (" + "MouseDisaster_Settings_PawnHistory_Major".Translate(FormatChance(rule.MajorChance)).ToString() + ")"
                    : string.Empty;
                constraints.Add(label + " " + activation + major);
            }

            foreach (string[] group in PassionGroups)
            {
                string labels = string.Join(" / ", (group ?? new string[0]).Select(ResolveSkillLabel).ToArray());
                if (!labels.NullOrEmpty())
                {
                    constraints.Add("MouseDisaster_Settings_PawnHistory_AnyPassion".Translate(labels).ToString());
                }
            }

            return string.Join("; ", constraints);
        }

        private static string ResolveSkillLabel(string skillDefName)
        {
            SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(skillDefName);
            string label = skillDef == null ? null : skillDef.skillLabel;
            return label.NullOrEmpty() ? skillDefName : label;
        }

        private static string FormatChance(float chance)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(chance) * 100f) + "%";
        }

        private bool MatchesEventContext(string eventId)
        {
            bool hasEventConstraint = EventIds.Count > 0 || EventCategory != MouseDisasterPawnHistoryEventCategory.None;
            if (!hasEventConstraint)
            {
                return true;
            }

            if (eventId.NullOrEmpty())
            {
                return false;
            }

            if (EventIds.Any(id => string.Equals(id, eventId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return EventCategoryMatches(EventCategory, eventId);
        }

        private static string[] SplitAdultLinkTitles(string adultLinkText)
        {
            return (adultLinkText ?? string.Empty)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(link => link.Trim())
                .Where(link => !link.NullOrEmpty())
                .ToArray();
        }

        internal static bool EventCategoryMatches(MouseDisasterPawnHistoryEventCategory category, string eventId)
        {
            if (eventId.NullOrEmpty())
            {
                return false;
            }

            string normalized = eventId.Trim().ToUpperInvariant();
            if (normalized.Length < 5 || (normalized[0] != 'O' && normalized[0] != 'N') || normalized[1] != '-')
            {
                return false;
            }

            if (!int.TryParse(normalized.Substring(2), out int number))
            {
                return false;
            }

            bool theft = normalized == "O-006" || normalized == "O-007" || normalized == "N-039";
            bool siege = normalized == "O-014" || normalized == "N-026" || normalized == "N-041";
            bool plague = normalized[0] == 'N' && number >= 32 && number <= 46;

            switch (category)
            {
                case MouseDisasterPawnHistoryEventCategory.Plague:
                    return plague;
                case MouseDisasterPawnHistoryEventCategory.Theft:
                    return theft;
                case MouseDisasterPawnHistoryEventCategory.Conflict:
                    return theft || siege;
                case MouseDisasterPawnHistoryEventCategory.Siege:
                    return siege;
                default:
                    return false;
            }
        }
    }

    public static partial class MouseDisasterPawnHistoryCatalog
    {
        [ThreadStatic]
        private static HistoryGenerationContext currentContext;

        public static IReadOnlyList<MouseDisasterPawnHistoryDefinition> All => Definitions;

        public static bool IsKnown(string id)
        {
            return !id.NullOrEmpty() && Definitions.Any(definition =>
                string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsMouseDisasterHistory(BackstoryDef backstory)
        {
            return backstory != null && Definitions.Any(definition =>
                string.Equals(definition.BackstoryDefName, backstory.defName, StringComparison.OrdinalIgnoreCase));
        }

        public static IDisposable PushContext(string incidentDefName, Map map)
        {
            return PushContext(incidentDefName, map, null);
        }

        public static IDisposable PushContext(string incidentDefName, Map map, float? temperature)
        {
            HistoryGenerationContext previous = currentContext;
            currentContext = new HistoryGenerationContext(incidentDefName, map, temperature);
            return new HistoryContextScope(previous);
        }

        public static IDisposable PushMap(Map map)
        {
            return PushContext(CurrentIncidentDefName, map);
        }

        public static MouseDisasterPawnHistoryDefinition TryApply(Pawn pawn, DevelopmentalStage stage)
        {
            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (!MouseDisasterRuntime.AllowsNewContent || settings?.enablePawnHistories != true || pawn?.story == null)
            {
                return null;
            }

            float? temperature = ResolveTemperature();
            string eventId = CurrentEventDisplayId;
            List<MouseDisasterPawnHistoryDefinition> candidates = Definitions
                .Where(definition => settings.IsPawnHistoryEnabled(definition.Id))
                .Where(definition => definition.Matches(pawn, stage, temperature, eventId))
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            MouseDisasterPawnHistoryDefinition selected = SelectHistory(candidates, pawn, temperature, eventId, settings);
            if (selected == null)
            {
                return null;
            }

            BackstoryDef backstory = DefDatabase<BackstoryDef>.GetNamedSilentFail(selected.BackstoryDefName);
            if (backstory == null)
            {
                Log.Warning("[MouseDisaster] Enabled pawn history is missing its BackstoryDef: " + selected.BackstoryDefName);
                return null;
            }

            if (selected.Slot == MouseDisasterPawnHistorySlot.Adult)
            {
                pawn.story.Childhood = MouseDisasterDefOf.MouseDisaster_Newborn;
                pawn.story.Adulthood = backstory;
            }
            else
            {
                pawn.story.Childhood = backstory;
                pawn.story.Adulthood = null;
            }

            ApplySkillRanges(pawn, selected);
            ApplyPassions(pawn, selected);
            ApplyRestrictions(pawn, selected);
            MouseDisasterTrace.Log("pawn history selected; pawn=" + pawn +
                "; id=" + selected.Id + "; title=" + selected.Title +
                "; event=" + (eventId ?? "none") + "; candidates=" + candidates.Count);
            return selected;
        }

        private static MouseDisasterPawnHistoryDefinition SelectHistory(
            List<MouseDisasterPawnHistoryDefinition> candidates,
            Pawn pawn,
            float? temperature,
            string eventId,
            MouseDisasterSettings settings)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Any(definition => definition.Slot == MouseDisasterPawnHistorySlot.Adult))
            {
                return candidates.RandomElementByWeight(definition => definition.Chance);
            }

            // adult_link is only a probability prior for the current young-history
            // selection. It never writes or reserves the pawn's future adulthood.
            List<MouseDisasterPawnHistoryDefinition> adultCandidates = Definitions
                .Where(definition => settings.IsPawnHistoryEnabled(definition.Id))
                .Where(definition => definition.Slot == MouseDisasterPawnHistorySlot.Adult)
                .Where(definition => definition.MatchesGenerationContext(pawn, temperature, eventId))
                .ToList();
            if (adultCandidates.Count == 0)
            {
                return candidates.RandomElementByWeight(definition => definition.Chance);
            }

            List<MouseDisasterPawnHistoryDefinition> linkedCandidates = candidates
                .Where(definition => adultCandidates.Any(adult => definition.LinksToAdultTitle(adult.Title)))
                .ToList();
            if (linkedCandidates.Count == 0)
            {
                return candidates.RandomElementByWeight(definition => definition.Chance);
            }

            return linkedCandidates.RandomElementByWeight(definition =>
            {
                float linkedAdultWeight = adultCandidates
                    .Where(adult => definition.LinksToAdultTitle(adult.Title))
                    .Sum(adult => adult.Chance);
                return definition.Chance * Mathf.Max(0.001f, linkedAdultWeight);
            });
        }

        private static string CurrentIncidentDefName =>
            currentContext?.IncidentDefName ?? MouseDisasterEventExecution.Current?.incidentDefName;

        internal static string CurrentEventDisplayId
        {
            get
            {
                string incidentDefName = CurrentIncidentDefName;
                if (incidentDefName.NullOrEmpty())
                {
                    return null;
                }

                return MouseDisasterIncidentCatalog.GetDisplayId(incidentDefName) ?? incidentDefName;
            }
        }

        private static Map CurrentMap =>
            currentContext?.Map ?? MouseDisasterEventExecution.Current?.map;

        private static float? ResolveTemperature()
        {
            if (currentContext?.Temperature.HasValue == true)
            {
                return NormalizeTemperature(currentContext.Temperature);
            }

            float? mapTemperature = ResolveMapTemperature(currentContext?.Map);
            if (mapTemperature.HasValue)
            {
                return mapTemperature;
            }

            MouseDisasterEventExecution execution = MouseDisasterEventExecution.Current;
            if (execution?.temperature.HasValue == true)
            {
                return NormalizeTemperature(execution.temperature);
            }

            return ResolveMapTemperature(execution?.map);
        }

        private static float? ResolveMapTemperature(Map map)
        {
            return NormalizeTemperature(map?.mapTemperature?.OutdoorTemp);
        }

        private static float? NormalizeTemperature(float? temperature)
        {
            if (!temperature.HasValue || float.IsNaN(temperature.Value) || float.IsInfinity(temperature.Value))
            {
                return null;
            }

            return temperature.Value;
        }

        private static void ApplySkillRanges(Pawn pawn, MouseDisasterPawnHistoryDefinition history)
        {
            if (pawn.skills == null)
            {
                return;
            }

            foreach (MouseDisasterPawnHistorySkillRange range in history.SkillRanges)
            {
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(range.SkillDefName);
                SkillRecord record = skillDef == null ? null : pawn.skills.GetSkill(skillDef);
                if (record == null)
                {
                    continue;
                }

                record.levelInt = Rand.RangeInclusive(range.Minimum, range.Maximum);
                record.xpSinceLastLevel = 0f;
                record.xpSinceMidnight = 0f;
                record.Notify_SkillDisablesChanged();
            }
        }

        private static void ApplyPassions(Pawn pawn, MouseDisasterPawnHistoryDefinition history)
        {
            if (pawn.skills == null)
            {
                return;
            }

            foreach (MouseDisasterPawnHistoryPassionRule rule in history.PassionRules)
            {
                SkillRecord record = ResolveSkillRecord(pawn, rule.SkillDefName);
                if (record == null || record.TotallyDisabled)
                {
                    continue;
                }

                Passion passion = Passion.None;
                if (Rand.Chance(rule.ActivationChance))
                {
                    passion = Passion.Minor;
                    if (rule.MajorChance > 0f && Rand.Chance(rule.MajorChance))
                    {
                        passion = Passion.Major;
                    }
                }

                if (record.passion != passion)
                {
                    record.passion = passion;
                    record.Notify_SkillDisablesChanged();
                }
            }

            foreach (string[] group in history.PassionGroups)
            {
                List<SkillRecord> records = (group ?? new string[0])
                    .Select(skillName => ResolveSkillRecord(pawn, skillName))
                    .Where(record => record != null && !record.TotallyDisabled)
                    .ToList();
                if (records.Count == 0 || records.Any(record => record.passion > Passion.None))
                {
                    continue;
                }

                SkillRecord selected = records.RandomElement();
                selected.passion = Passion.Minor;
                selected.Notify_SkillDisablesChanged();
            }
        }

        private static SkillRecord ResolveSkillRecord(Pawn pawn, string skillDefName)
        {
            SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(skillDefName);
            return skillDef == null || pawn?.skills == null ? null : pawn.skills.GetSkill(skillDef);
        }

        private static void ApplyRestrictions(Pawn pawn, MouseDisasterPawnHistoryDefinition history)
        {
            if ((history.Restrictions & MouseDisasterPawnHistoryRestriction.NoWeapons) != 0)
            {
                pawn.equipment?.DestroyAllEquipment(DestroyMode.Vanish);
            }
        }

        private sealed class HistoryGenerationContext
        {
            public readonly string IncidentDefName;
            public readonly Map Map;
            public readonly float? Temperature;

            public HistoryGenerationContext(string incidentDefName, Map map, float? temperature)
            {
                IncidentDefName = incidentDefName;
                Map = map;
                Temperature = temperature;
            }
        }

        private sealed class HistoryContextScope : IDisposable
        {
            private readonly HistoryGenerationContext previous;
            private bool disposed;

            public HistoryContextScope(HistoryGenerationContext previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                currentContext = previous;
            }
        }
    }
}
