using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;

namespace OhYouAreSoFast
{
    [HarmonyPatch(typeof(PathFinder), "ParameterizePathJob")]
    internal static class Patch_PathFinder_ParameterizePathJob
    {
        private const float VanillaHeuristicCardinalCost = 13f;
        private const float DiagonalDistanceFactor = 1.41421356f;

        private static void Postfix(ref PathFinderJob job)
        {
            if (!(OhYouAreSoFastMod.Settings?.adaptivePathHeuristic ?? true))
                return;

            if (job.moveTicksCardinal <= 0 || job.moveTicksDiagonal <= 0)
                return;

            // PathFinderJob uses a single octile-distance multiplier. Use the
            // smaller cardinal/diagonal scale so h never exceeds movement cost.
            float cardinalScale = job.moveTicksCardinal / VanillaHeuristicCardinalCost;
            float diagonalScale = job.moveTicksDiagonal
                / (VanillaHeuristicCardinalCost * DiagonalDistanceFactor);
            float admissibleScale = Mathf.Min(cardinalScale, diagonalScale);

            job.heuristicStrength = Mathf.Min(job.heuristicStrength, admissibleScale);
        }
    }
}
