using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(StorytellerComp_RandomMain), "ChooseRandomCategory")]
    public static class MouseDisasterNarratorThreatTempoPatch
    {
        public static bool Prefix(StorytellerComp_RandomMain __instance, IIncidentTarget target,
            List<IncidentCategoryDef> skipCategories, ref IncidentCategoryDef __result)
        {
            if (Find.Storyteller?.def?.defName != GameComponent_MouseDisasterNarrative.NarratorDefName ||
                !(target is Map map) || !map.IsPlayerHome ||
                !MouseDisasterRuntime.AllowsNewContent || MouseDisasterMod.Settings?.enableNarrative == false)
                return true;

            int trust = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NarratorTrust ?? 0;
            if (trust == 0) return true;
            var props = (StorytellerCompProperties_RandomMain)__instance.props;
            float factor = MouseDisasterNarrativePolicy.ThreatFrequencyFactor(trust);
            // Keep the original category retry and overdue-threat behavior without mutating shared Defs.
            if (!skipCategories.Contains(IncidentCategoryDefOf.ThreatBig) &&
                Find.TickManager.TicksGame - target.StoryState.LastThreatBigTick >
                60000f * props.maxThreatBigIntervalDays / factor)
                __result = IncidentCategoryDefOf.ThreatBig;
            else
                __result = props.categoryWeights.Where(cw => !skipCategories.Contains(cw.category))
                    .RandomElementByWeight(cw => cw.weight *
                        (cw.category == IncidentCategoryDefOf.ThreatBig ? factor : 1f)).category;
            return false;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.CanFireNow))]
    public static class MouseDisasterIncidentTogglePatch
    {
        public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
        {
            if (!MouseDisasterRuntime.AllowsNewContent && MouseDisasterIncidentCatalog.IsKnownIncident(__instance?.def?.defName))
            {
                __result = false;
                return false;
            }

            if (MouseDisasterIncidentTargetPolicy.ShouldBlockEnvironmentTemperature(__instance?.def?.defName, parms?.target))
            {
                MouseDisasterTrace.Log("incident CanFireNow blocked by map temperature; def=" +
                    (__instance?.def?.defName ?? "unknown") + "; " + MouseDisasterTrace.DescribeTarget(parms?.target));
                __result = false;
                return false;
            }

            if (!MouseDisasterIncidentTargetPolicy.ShouldBlockCanFireNowForInvalidTarget(
                    __instance?.def?.defName,
                    parms?.target != null,
                    parms?.target is Map,
                    parms?.target is Caravan))
            {
                return true;
            }

            __result = false;
            return false;
        }

        public static void Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
        {
            if (!__result || __instance?.def?.defName == null || MouseDisasterMod.Settings == null)
            {
                return;
            }

            if (!MouseDisasterIncidentCatalog.IsIncidentEnabled(__instance.def.defName, MouseDisasterMod.Settings.disabledIncidentDefNames))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class MouseDisasterIncidentExecuteTogglePatch
    {
        public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result, out MouseDisasterEventExecution __state)
        {
            __state = null;
            string defName = __instance?.def?.defName;
            if (!MouseDisasterIncidentCatalog.IsKnownIncident(defName))
            {
                return true;
            }

            MouseDisasterTrace.Log("incident execute begin; def=" + defName + "; " +
                MouseDisasterTrace.DescribeTarget(parms?.target) + "; forced=" + (parms?.forced ?? false));

            if (MouseDisasterRuntime.AllowsNewContent &&
                (GameComponent_MouseDisasterNarrative.DebugForcing || MouseDisasterIncidentCatalog.IsIncidentEnabled(defName, MouseDisasterMod.Settings?.disabledIncidentDefNames)))
            {
                __state = new MouseDisasterEventExecution
                {
                    previous = MouseDisasterEventExecution.Current,
                    groupId = GameComponent_MouseDisasterEventBehavior.Component?.CreateGroup(__instance.def) ?? 0,
                    before = parms?.target is Map map ? new HashSet<Pawn>(map.mapPawns.AllPawns) : null,
                    incidentDefName = defName,
                    map = parms?.target as Map,
                    temperature = ResolveTargetTemperature(parms?.target)
                };
                MouseDisasterEventExecution.Current = __state;
                return true;
            }

            __result = false;
            return false;
        }

        private static float? ResolveTargetTemperature(IIncidentTarget target)
        {
            if (target is Map map)
            {
                return NormalizeTemperature(map.mapTemperature?.OutdoorTemp);
            }

            if (target is Caravan caravan && caravan.Tile.Valid)
            {
                return NormalizeTemperature(GenTemperature.GetTemperatureAtTile(caravan.Tile));
            }

            return null;
        }

        private static float? NormalizeTemperature(float? temperature)
        {
            if (!temperature.HasValue || float.IsNaN(temperature.Value) || float.IsInfinity(temperature.Value))
            {
                return null;
            }

            return temperature.Value;
        }

        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result, MouseDisasterEventExecution __state)
        {
            if (!__result || !MouseDisasterIncidentCatalog.IsKnownIncident(__instance?.def?.defName))
            {
                return;
            }

            var participants = new System.Collections.Generic.List<Pawn>();
            if (__state?.before != null && parms?.target is Map map)
                foreach (Pawn pawn in map.mapPawns.AllPawns)
                    if (!__state.before.Contains(pawn)) participants.Add(pawn);
            if (__state != null) GameComponent_MouseDisasterEventBehavior.Component?.Register(__state.groupId, participants);
            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordIncidentExecuted(__instance.def, parms, participants);
            MouseDisasterTrace.Log("incident execute end; def=" + __instance.def.defName + "; success=" +
                __result + "; participants=" + participants.Count + "; " + MouseDisasterTrace.DescribeTarget(parms?.target));
        }

        public static void Finalizer(MouseDisasterEventExecution __state)
        {
            if (__state != null) MouseDisasterEventExecution.Current = __state.previous;
        }
    }

    [HarmonyPatch(typeof(IncidentDef), nameof(IncidentDef.TargetAllowed))]
    public static class MouseDisasterIncidentTargetAllowedPatch
    {
        public static bool Prefix(IncidentDef __instance, IIncidentTarget target, ref bool __result)
        {
            if (__instance == null)
            {
                return true;
            }

            if (target == null)
            {
                __result = false;
                return false;
            }

            if (MouseDisasterIncidentTargetPolicy.TryEvaluateSafeTargetAllowed(
                    __instance.defName,
                    target is Map,
                    target is Map map && map.IsPlayerHome,
                    target is Caravan,
                    target is Caravan caravan && caravan.IsPlayerControlled,
                    out bool safeResult))
            {
                __result = safeResult;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), "get_BaseChanceThisGame")]
    public static class MouseDisasterIncidentBaseChancePatch
    {
        public static void Postfix(IncidentWorker __instance, ref float __result)
        {
            if (__instance?.def?.defName == null)
            {
                return;
            }

            if (!__instance.def.defName.StartsWith("MouseDisaster_", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (__instance.def.defName.IndexOf("Plague", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                __result *= 0.5f;
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.ChanceFactorNow))]
    public static class MouseDisasterIncidentSeasonChancePatch
    {
        public static void Postfix(IncidentWorker __instance, IIncidentTarget target, ref float __result)
        {
            if (__instance?.def?.defName == null || target == null)
            {
                return;
            }

            if (!__instance.def.defName.StartsWith("MouseDisaster_", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!target.Tile.Valid)
            {
                return;
            }

            Season season = GenLocalDate.Season(target.Tile);
            if (season == Season.Spring || season == Season.Winter)
            {
                __result *= 1.1f;
            }
        }
    }
}
