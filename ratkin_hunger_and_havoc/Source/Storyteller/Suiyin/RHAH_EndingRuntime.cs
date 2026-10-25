using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using HungerAndHavoc.Narrative;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_EndingRuntime
    {
        internal const string NarratorDefName = "RHAH_Suiyin";
        internal static bool IsNarrator(string defName)
        {
            return defName == NarratorDefName;
        }

        internal static bool CountsNow()
        {
            return Counts(Find.Storyteller?.def?.defName == NarratorDefName, RHAH_Mod.Settings);
        }

        internal static bool Counts(bool narrator, RHAH_Settings settings)
        {
            return narrator;
        }

    }

    // 只改穗音自己的家园 RandomMain 不改其它叙事者、任务袭击、商队和本模组事件池
    [HarmonyPatch(typeof(StorytellerComp_RandomMain), "ChooseRandomCategory")]
    internal static class RHAH_ThreatTempoPatch
    {
        internal static bool Prefix(
            StorytellerComp_RandomMain __instance,
            IIncidentTarget target,
            List<IncidentCategoryDef> skipCategories,
            ref IncidentCategoryDef __result)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (settings != null && !settings.suiYinThreatTempo)
            {
                return true;
            }

            if (Find.Storyteller?.def?.defName != RHAH_EndingRuntime.NarratorDefName || !(target is Map map) || !map.IsPlayerHome)
            {
                return true;
            }

            NarrativeState state = Current.Game?.GetComponent<NarrativeState>();
            int trust = state == null ? 0 : state.Snapshot().Trust;
            if (trust == 0 || __instance?.props is not StorytellerCompProperties_RandomMain props)
            {
                return true;
            }

            float factor = RHAH_EndingRules.ThreatFactor(trust);
            if (!skipCategories.Contains(IncidentCategoryDefOf.ThreatBig) &&
                Find.TickManager.TicksGame - target.StoryState.LastThreatBigTick > 60000f * props.maxThreatBigIntervalDays / factor)
            {
                __result = IncidentCategoryDefOf.ThreatBig;
            }
            else
            {
                __result = Weighted(props, skipCategories, factor);
            }

            return __result != null ? false : true;
        }

        static IncidentCategoryDef Weighted(StorytellerCompProperties_RandomMain props, List<IncidentCategoryDef> skip, float factor)
        {
            float total = 0f;
            List<IncidentCategoryEntry> weights = props.categoryWeights;
            for (int i = 0; i < weights.Count; i++)
            {
                IncidentCategoryEntry entry = weights[i];
                if (entry?.category == null || skip.Contains(entry.category))
                {
                    continue;
                }

                total += entry.category == IncidentCategoryDefOf.ThreatBig ? entry.weight * factor : entry.weight;
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = Rand.Value * total;
            for (int i = 0; i < weights.Count; i++)
            {
                IncidentCategoryEntry entry = weights[i];
                if (entry?.category == null || skip.Contains(entry.category))
                {
                    continue;
                }

                roll -= entry.category == IncidentCategoryDefOf.ThreatBig ? entry.weight * factor : entry.weight;
                if (roll <= 0f)
                {
                    return entry.category;
                }
            }

            return null;
        }
    }
}
