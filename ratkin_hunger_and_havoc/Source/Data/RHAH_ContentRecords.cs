using System;
using System.Collections.Generic;

namespace HungerAndHavoc.Data
{
    internal enum RHAH_HistorySlot
    {
        Childhood = 0,
        Adulthood = 1
    }

    internal enum RHAH_ContentGender
    {
        Any = 0,
        Female = 1,
        Male = 2
    }

    internal enum RHAH_ContentCategory
    {
        None = 0,
        Plague = 1,
        Theft = 2,
        Conflict = 3,
        Siege = 4
    }

    internal enum RHAH_TraitSlot
    {
        Any = 0,
        Childhood = 1,
        Adulthood = 2
    }

    internal sealed class RHAH_SkillRange
    {
        internal RHAH_SkillRange(string skillDefName, int minimum, int maximum)
        {
            SkillDefName = skillDefName;
            Minimum = minimum;
            Maximum = maximum;
        }

        internal string SkillDefName { get; }
        internal int Minimum { get; }
        internal int Maximum { get; }
    }

    internal sealed class RHAH_PassionRule
    {
        internal RHAH_PassionRule(string skillDefName, float activationChance, float majorChance)
        {
            SkillDefName = skillDefName;
            ActivationChance = activationChance;
            MajorChance = majorChance;
        }

        internal string SkillDefName { get; }
        internal float ActivationChance { get; }
        internal float MajorChance { get; }
    }

    internal sealed class RHAH_HistoryRecord
    {
        internal RHAH_HistoryRecord(
            string displayId,
            string backstoryDefName,
            RHAH_HistorySlot slot,
            float weight,
            float? minimumAge,
            bool minimumInclusive,
            float? maximumAge,
            bool maximumInclusive,
            float minimumTemperature,
            float maximumTemperature,
            RHAH_ContentGender gender,
            string[] incidentDisplayIds,
            RHAH_ContentCategory category,
            string[] linkedAdultDisplayIds,
            RHAH_SkillRange[] skills,
            RHAH_PassionRule[] passions,
            string[][] passionGroups,
            bool stripWeapons)
        {
            DisplayId = displayId;
            BackstoryDefName = backstoryDefName;
            Slot = slot;
            Weight = weight < 0.001f ? 0.001f : weight;
            MinimumAge = minimumAge;
            MinimumInclusive = minimumInclusive;
            MaximumAge = maximumAge;
            MaximumInclusive = maximumInclusive;
            MinimumTemperature = minimumTemperature;
            MaximumTemperature = maximumTemperature;
            Gender = gender;
            IncidentDisplayIds = incidentDisplayIds ?? Array.Empty<string>();
            Category = category;
            LinkedAdultDisplayIds = linkedAdultDisplayIds ?? Array.Empty<string>();
            Skills = skills ?? Array.Empty<RHAH_SkillRange>();
            Passions = passions ?? Array.Empty<RHAH_PassionRule>();
            PassionGroups = passionGroups ?? Array.Empty<string[]>();
            StripWeapons = stripWeapons;
        }

        internal string DisplayId { get; }
        internal string BackstoryDefName { get; }
        internal RHAH_HistorySlot Slot { get; }
        internal float Weight { get; }
        internal float? MinimumAge { get; }
        internal bool MinimumInclusive { get; }
        internal float? MaximumAge { get; }
        internal bool MaximumInclusive { get; }
        internal float MinimumTemperature { get; }
        internal float MaximumTemperature { get; }
        internal RHAH_ContentGender Gender { get; }
        internal IReadOnlyList<string> IncidentDisplayIds { get; }
        internal RHAH_ContentCategory Category { get; }
        internal IReadOnlyList<string> LinkedAdultDisplayIds { get; }
        internal IReadOnlyList<RHAH_SkillRange> Skills { get; }
        internal IReadOnlyList<RHAH_PassionRule> Passions { get; }
        internal IReadOnlyList<string[]> PassionGroups { get; }
        internal bool StripWeapons { get; }
    }

    internal sealed class RHAH_TraitRecord
    {
        internal RHAH_TraitRecord(
            string displayId,
            string traitDefName,
            RHAH_TraitSlot slot,
            float chance,
            string[] historyDisplayIds,
            string[] incidentDisplayIds,
            RHAH_ContentCategory category,
            float red,
            float green,
            float blue)
        {
            DisplayId = displayId;
            TraitDefName = traitDefName;
            Slot = slot;
            Chance = chance < 0f ? 0f : (chance > 1f ? 1f : chance);
            HistoryDisplayIds = historyDisplayIds ?? Array.Empty<string>();
            IncidentDisplayIds = incidentDisplayIds ?? Array.Empty<string>();
            Category = category;
            Red = red;
            Green = green;
            Blue = blue;
        }

        internal string DisplayId { get; }
        internal string TraitDefName { get; }
        internal RHAH_TraitSlot Slot { get; }
        internal float Chance { get; }
        internal IReadOnlyList<string> HistoryDisplayIds { get; }
        internal IReadOnlyList<string> IncidentDisplayIds { get; }
        internal RHAH_ContentCategory Category { get; }
        internal float Red { get; }
        internal float Green { get; }
        internal float Blue { get; }
    }
}
