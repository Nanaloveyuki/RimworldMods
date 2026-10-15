using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class MouseDisasterTraitDegreeData : TraitDegreeData
    {
        public Color color = Color.white;
    }

    public enum MouseDisasterTraitSlot
    {
        Any = 0,
        Young = 1,
        Adult = 2
    }

    public enum MouseDisasterTraitPolarity
    {
        Positive = 0,
        Negative = 1,
        Mixed = 2
    }

    public sealed class MouseDisasterTraitDefinition
    {
        public string Id { get; }
        public string TraitDefName { get; }
        public MouseDisasterTraitSlot Slot { get; }
        public MouseDisasterTraitPolarity Polarity { get; }
        public float Chance { get; }
        public IReadOnlyList<string> HistoryIds { get; }
        public IReadOnlyList<string> EventIds { get; }
        public MouseDisasterPawnHistoryEventCategory EventCategory { get; }

        public MouseDisasterTraitDefinition(
            string id,
            string traitDefName,
            MouseDisasterTraitSlot slot,
            MouseDisasterTraitPolarity polarity,
            float chance,
            string[] historyIds,
            string[] eventIds,
            MouseDisasterPawnHistoryEventCategory eventCategory)
        {
            Id = id;
            TraitDefName = traitDefName;
            Slot = slot;
            Polarity = polarity;
            Chance = Mathf.Clamp01(chance);
            HistoryIds = historyIds ?? new string[0];
            EventIds = eventIds ?? new string[0];
            EventCategory = eventCategory;
        }

        public string DisplayLabel
        {
            get
            {
                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(TraitDefName);
                string label = MouseDisasterTraitCatalog.TryGetDegreeData(traitDef)?.label;
                return Id + " " + (label.NullOrEmpty() ? TraitDefName : label);
            }
        }

        public Color LabelColor
        {
            get
            {
                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(TraitDefName);
                return MouseDisasterTraitCatalog.GetLabelColor(traitDef);
            }
        }

        public string Tooltip
        {
            get
            {
                List<string> lines = new List<string>();
                TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(TraitDefName);
                string description = MouseDisasterTraitCatalog.TryGetDegreeData(traitDef)?.description;
                if (!description.NullOrEmpty())
                {
                    lines.Add(description);
                }

                lines.Add("MouseDisaster_Settings_PawnTrait_Slot".Translate(
                    ("MouseDisaster_Settings_PawnTrait_Slot_" + Slot).Translate()).ToString());

                string eventConstraint = FormatEventConstraint();
                if (!eventConstraint.NullOrEmpty())
                {
                    lines.Add("MouseDisaster_Settings_PawnTrait_Event".Translate(eventConstraint).ToString());
                }

                if (HistoryIds.Count > 0)
                {
                    lines.Add("MouseDisaster_Settings_PawnTrait_History".Translate().ToString());
                }

                return string.Join("\n", lines);
            }
        }

        private string FormatEventConstraint()
        {
            var constraints = new List<string>();
            if (EventCategory != MouseDisasterPawnHistoryEventCategory.None)
            {
                constraints.Add(("MouseDisaster_Settings_PawnHistory_EventCategory_" + EventCategory)
                    .Translate().ToString());
            }

            if (EventIds.Count > 0)
            {
                constraints.Add("MouseDisaster_Settings_PawnTrait_SpecificEvents".Translate().ToString());
            }

            return string.Join(", ", constraints);
        }

        public bool MatchesSlot(DevelopmentalStage stage)
        {
            switch (Slot)
            {
                case MouseDisasterTraitSlot.Young:
                    return !stage.Adult();
                case MouseDisasterTraitSlot.Adult:
                    return stage.Adult();
                default:
                    return true;
            }
        }

        public bool MatchesHistory(MouseDisasterPawnHistoryDefinition history)
        {
            if (history == null || HistoryIds.Count == 0)
            {
                return false;
            }

            return HistoryIds.Any(id => string.Equals(id, history.Id, StringComparison.OrdinalIgnoreCase));
        }

        public bool MatchesEvent(string eventId)
        {
            if (eventId.NullOrEmpty())
            {
                return false;
            }

            if (EventIds.Any(id => string.Equals(id, eventId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return MouseDisasterPawnHistoryDefinition.EventCategoryMatches(EventCategory, eventId);
        }
    }

    public static partial class MouseDisasterTraitCatalog
    {
        public static IReadOnlyList<MouseDisasterTraitDefinition> All => Definitions;

        public static bool IsKnown(string id)
        {
            return !id.NullOrEmpty() && Definitions.Any(definition =>
                string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static float GetDefaultWeight(string id)
        {
            MouseDisasterTraitDefinition definition = Definitions.FirstOrDefault(entry =>
                string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
            return definition == null
                ? 50f
                : Mathf.Clamp(Mathf.Round(definition.Chance * 100f), 0f, 100f);
        }

        // Vanilla TraitDef.DataAtDegree logs when the requested degree is missing.
        // Spectrum traits such as SpeedOffset only define -1/1/2, so never call it from LabelCap.
        public static TraitDegreeData TryGetDegreeData(TraitDef def, int? degree = null)
        {
            if (def?.degreeDatas == null || def.degreeDatas.Count == 0)
            {
                return null;
            }

            if (degree.HasValue)
            {
                for (int i = 0; i < def.degreeDatas.Count; i++)
                {
                    TraitDegreeData data = def.degreeDatas[i];
                    if (data != null && data.degree == degree.Value)
                    {
                        return data;
                    }
                }
            }

            return def.degreeDatas[0];
        }

        public static Color GetLabelColor(TraitDef def, int? degree = null)
        {
            if (def?.degreeDatas == null)
            {
                return Color.white;
            }

            MouseDisasterTraitDegreeData fallback = null;
            for (int i = 0; i < def.degreeDatas.Count; i++)
            {
                if (!(def.degreeDatas[i] is MouseDisasterTraitDegreeData data) || data.color.a <= 0.01f)
                {
                    continue;
                }

                if (degree.HasValue && data.degree == degree.Value)
                {
                    return data.color;
                }

                if (fallback == null)
                {
                    fallback = data;
                }
            }

            return fallback != null ? fallback.color : Color.white;
        }

        public static void TryApply(Pawn pawn, DevelopmentalStage stage, MouseDisasterPawnHistoryDefinition history)
        {
            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (!MouseDisasterRuntime.AllowsNewContent || settings?.enablePawnTraits != true || pawn?.story?.traits == null)
            {
                return;
            }

            if (pawn.story.traits.allTraits.Any(trait =>
                trait != null && MouseDisasterGenerationPolicy.IsOwnedTrait(trait.def)))
            {
                return;
            }

            string eventId = MouseDisasterPawnHistoryCatalog.CurrentEventDisplayId;
            List<MouseDisasterTraitDefinition> slotMatches = Definitions
                .Where(definition => definition.MatchesSlot(stage) &&
                    settings.IsPawnTraitEnabled(definition.Id) &&
                    settings.GetPawnTraitWeight(definition.Id) > 0f)
                .ToList();
            if (slotMatches.Count == 0)
            {
                return;
            }

            List<MouseDisasterTraitDefinition> historyMatches = slotMatches
                .Where(definition => definition.MatchesHistory(history))
                .ToList();
            if (historyMatches.Count > 0)
            {
                TrySelectAndApply(pawn, historyMatches, "history");
                return;
            }

            List<MouseDisasterTraitDefinition> eventMatches = slotMatches
                .Where(definition => definition.MatchesEvent(eventId))
                .ToList();
            if (eventMatches.Count > 0)
            {
                TrySelectAndApply(pawn, eventMatches, "event");
                return;
            }

            TrySelectAndApply(pawn, slotMatches, "slot");
        }

        private static bool TrySelectAndApply(Pawn pawn, List<MouseDisasterTraitDefinition> candidates, string reason)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            List<MouseDisasterTraitDefinition> remaining = candidates.ToList();
            int attempts = Mathf.Min(8, remaining.Count);
            for (int attempt = 0; attempt < attempts && remaining.Count > 0; attempt++)
            {
                MouseDisasterTraitDefinition selected = remaining.RandomElementByWeight(definition =>
                    Mathf.Max(0.001f, settings?.GetPawnTraitWeight(definition.Id) ?? definition.Chance * 100f));
                float applyChance = (settings?.GetPawnTraitWeight(selected.Id) ?? selected.Chance * 100f) / 100f;
                if (!Rand.Chance(applyChance))
                {
                    return false;
                }

                if (TryGain(pawn, selected, reason))
                {
                    return true;
                }

                remaining.Remove(selected);
            }

            return false;
        }

        private static bool TryGain(Pawn pawn, MouseDisasterTraitDefinition definition, string reason)
        {
            if (definition == null || definition.TraitDefName.NullOrEmpty())
            {
                return false;
            }

            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(definition.TraitDefName);
            if (traitDef == null)
            {
                Log.Warning("[MouseDisaster] Enabled pawn trait is missing its TraitDef: " + definition.TraitDefName);
                return false;
            }

            if (!MouseDisasterUtility.CanAssignGeneratedTrait(pawn, traitDef))
            {
                return false;
            }

            int degree = 0;
            if (!traitDef.degreeDatas.NullOrEmpty())
            {
                degree = traitDef.degreeDatas[0].degree;
            }

            pawn.story.traits.GainTrait(new Trait(traitDef, degree, false), suppressConflicts: false);
            MouseDisasterTrace.Log("pawn trait selected; pawn=" + pawn +
                "; id=" + definition.Id + "; def=" + definition.TraitDefName +
                "; reason=" + reason);
            return true;
        }
    }

    [HarmonyPatch(typeof(Trait), nameof(Trait.LabelCap), MethodType.Getter)]
    internal static class MouseDisasterTraitLabelColorPatch
    {
        private static void Postfix(Trait __instance, ref string __result)
        {
            if (__instance?.def == null ||
                __instance.Suppressed ||
                __result.NullOrEmpty() ||
                __result.IndexOf("<color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                !MouseDisasterGenerationPolicy.IsOwnedTrait(__instance.def))
            {
                return;
            }

            Color color = MouseDisasterTraitCatalog.GetLabelColor(__instance.def, __instance.Degree);
            if (color.a <= 0.01f || color == Color.white)
            {
                return;
            }

            __result = __result.Colorize(color);
        }
    }
}
