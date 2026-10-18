using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    // Use vanilla eligibility, weighting and firing bookkeeping without changing shared IncidentDefs.
    public sealed class MouseDisasterIncidentPool : StorytellerComp
    {
        public MouseDisasterIncidentPool() { props = new StorytellerCompProperties(); }

        public FiringIncident Select(IIncidentTarget target, bool? positive, IncidentParms raid = null)
        {
            var settings = MouseDisasterMod.Settings;
            if (settings == null || !MouseDisasterRuntime.AllowsNewContent) return null;
            if (target is Map map && map.mapTemperature != null &&
                !settings.IsMouseDisasterEnvironmentTemperatureAllowed(map.mapTemperature.OutdoorTemp))
            {
                MouseDisasterTrace.Log("incident pool skipped; " + MouseDisasterTrace.DescribeMap(map) +
                    "; allowedRange=" + settings.mouseDisasterMinimumEnvironmentTemperature.ToString("0.0") +
                    ".." + settings.mouseDisasterMaximumEnvironmentTemperature.ToString("0.0"));
                return null;
            }
            var candidates = new List<FiringIncident>(MouseDisasterIncidentCatalog.AllEntries.Count);
            foreach (var entry in MouseDisasterIncidentCatalog.AllEntries)
            {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(entry.DefName);
                if (def == null || !settings.IsIncidentEnabled(entry.DefName) || !def.TargetAllowed(target) ||
                    MouseDisasterIncidentTargetPolicy.ShouldBlockEnvironmentTemperature(entry.DefName, target) ||
                    (positive.HasValue ? settings.IsPositiveIncident(def) != positive.Value : !settings.ReplacesRaid(entry.DefName))) continue;
                // This protection lives in Storyteller, not IncidentWorker.CanFireNow.
                if (def.category == IncidentCategoryDefOf.ThreatBig && ModsConfig.AnomalyActive &&
                    Find.TickManager.TicksGame - Find.Anomaly.metalHellClosedTick < 300000) continue;
                var parms = GenerateParms(def.category, target);
                if (raid != null) parms.points = raid.points;
                if (def.Worker.CanFireNow(parms)) candidates.Add(new FiringIncident(def, this, parms));
            }
            return candidates.RandomElementByWeightWithFallback(fi => IncidentChanceFinal(fi.def, target), null);
        }
    }

    [HarmonyPatch(typeof(StorytellerComp), "UsableIncidentsInCategory", new Type[] { typeof(IncidentCategoryDef), typeof(Func<IncidentDef, IncidentParms>) })]
    public static class MouseDisasterSeparateVanillaPoolPatch
    {
        public static void Postfix(ref IEnumerable<IncidentDef> __result) =>
            __result = __result.Where(def => !MouseDisasterIncidentCatalog.IsKnownIncident(def.defName));
    }

    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.MakeIncidentsForInterval), new Type[] { })]
    public static class MouseDisasterIndependentPoolsPatch
    {
        public static void Postfix(Storyteller __instance, ref IEnumerable<FiringIncident> __result) =>
            __result = Generate(__instance, __result);

        private static IEnumerable<FiringIncident> Generate(Storyteller storyteller, IEnumerable<FiringIncident> original)
        {
            foreach (var incident in original)
                if (!MouseDisasterIncidentCatalog.IsKnownIncident(incident.def.defName) || incident.sourceQuestPart != null)
                    yield return incident;
            var settings = MouseDisasterMod.Settings;
            if (!MouseDisasterRuntime.AllowsNewContent || settings == null || GenDate.DaysPassedSinceSettleFloat <= 1f) yield break;
            var pool = new MouseDisasterIncidentPool();
            foreach (var target in storyteller.AllIncidentTargets)
            {
                for (int i = 0; i < 2; i++)
                {
                    float days = i == 0 ? settings.positiveIncidentDays : settings.negativeIncidentDays;
                    if (days <= 0f || !Rand.MTBEventOccurs(days, GenDate.TicksPerDay, Storyteller.CheckInterval)) continue;
                    var incident = pool.Select(target, i == 0);
                    if (incident != null) yield return incident;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class MouseDisasterRaidReplacementPatch
    {
        private static readonly AccessTools.FieldRef<StoryState, int> LastThreatBigTick =
            AccessTools.FieldRefAccess<StoryState, int>("lastThreatBigTick");

        public static bool Prefix(Storyteller __instance, FiringIncident fi, bool queued, ref bool __result)
        {
            // Never replace queued or quest raids; unsuccessful replacements retain the original raid.
            if (queued || fi.def != IncidentDefOf.RaidEnemy || fi.sourceQuestPart != null || fi.parms.quest != null || fi.parms.forced ||
                !(fi.parms.target is Map map) || !map.IsPlayerHome) return true;
            var replacement = new MouseDisasterIncidentPool().Select(map, null, fi.parms);
            if (replacement == null || !__instance.TryFire(replacement)) return true;
            // Consume the scheduled threat slot without recording a raid that never happened.
            if (replacement.def.category != IncidentCategoryDefOf.ThreatBig)
                LastThreatBigTick(map.StoryState) = Find.TickManager.TicksGame;
            __result = true;
            return false;
        }
    }
}
