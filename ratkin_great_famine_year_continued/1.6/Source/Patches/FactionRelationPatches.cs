using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(FactionGenerator), nameof(FactionGenerator.GenerateFactionsIntoWorldLayer))]
    internal static class MouseDisasterFactionGenerationDiagnostics
    {
        public static void Postfix(PlanetLayer layer, List<FactionDef> factions)
        {
            if (layer == null || !layer.IsRootSurface || factions == null || Find.FactionManager == null) return;
            var missing = factions.Where(def => def != null && !def.hidden && !def.isPlayer &&
                (def.layerBlacklist.NullOrEmpty() || !def.layerBlacklist.Contains(layer.Def)) &&
                (def.layerWhitelist.NullOrEmpty() || def.layerWhitelist.Contains(layer.Def)))
                .GroupBy(def => def).Where(group => Find.FactionManager.AllFactionsListForReading.Count(f => f.def == group.Key) < group.Count())
                .Select(group => group.Key.defName).ToList();
            if (missing.Count > 0)
                Log.Warning("[MouseDisaster][FactionGeneration] Configured factions missing after world generation: " +
                    string.Join(", ", missing) + ". No factions were added or removed by this diagnostic.");
        }
    }

    [HarmonyPatch(typeof(Faction), nameof(Faction.RelationWith))]
    public static class MouseDisasterFactionRelationPatch
    {
        [System.ThreadStatic]
        private static bool repairingRelation;

        public static bool Prefix(Faction __instance, Faction other, bool allowNull, ref FactionRelation __result)
        {
            bool involvesManagedFaction =
                MouseDisasterUtility.IsMouseDisasterManagedFaction(__instance) ||
                MouseDisasterUtility.IsMouseDisasterManagedFaction(other);
            if (!MouseDisasterFactionRelationPolicy.ShouldInterceptRelationLookup(
                    creatingWorld: Current.CreatingWorld != null,
                    allowNull: allowNull,
                    repairingRelation: repairingRelation,
                    ownerMissing: __instance == null,
                    otherMissing: other == null,
                    sameFaction: __instance == other,
                    involvesManagedFaction: involvesManagedFaction))
            {
                return true;
            }

            try
            {
                repairingRelation = true;
                if (!MouseDisasterUtility.TryGetOrRepairManagedFactionRelation(__instance, other, out __result) ||
                    __result == null)
                {
                    return true;
                }

                return false;
            }
            finally
            {
                repairingRelation = false;
            }
        }
    }
}
