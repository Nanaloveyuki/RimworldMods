using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonLaborReachFix
{
    /// <summary>
    /// Replaces Prison Labor's cell scan with vanilla 1.7 semantics.
    ///
    /// <para>
    /// <c>JobGiver_Labor.TryGetJobOnCell</c> copies an older vanilla scan that picked the nearest
    /// cell by distance only. Vanilla 1.7 additionally requires
    /// <c>pawn.CanReach(cell, scanner.PathEndMode, scanner.MaxPathDanger(pawn))</c> before a cell may
    /// become a work target. Without that check a prisoner is handed unreachable cell work
    /// (roof removal, cleaning, smoothing, ...), the path fails, and the same job is issued again.
    /// </para>
    ///
    /// <para>
    /// The thing branch of Prison Labor already searches through
    /// <c>GenClosest.ClosestThingReachable</c>, so only the cell branch is replaced. Choosing among
    /// cells still follows Prison Labor's own order: priority first for prioritized scanners,
    /// otherwise the nearest cell, with Prison Labor's prisoner-specific forbidden check.
    /// </para>
    /// </summary>
    internal static class Patch_JobGiver_Labor
    {
        private const string LaborGiverType = "PrisonLabor.Core.AI.JobGivers.JobGiver_Labor";
        private const string ForbiddenCheckName = "PrisonLabor.Core.Other.CustomForbidenUtil:IsForbiddenForPrisoner";

        // Prison Labor uses its prisoner-specific forbidden check for prioritized scanners only;
        // the distance branch uses vanilla IsForbidden. Keep both so this patch changes reachability
        // alone rather than silently widening or narrowing what prisoners may touch.
        private static Func<IntVec3, Pawn, bool> isForbiddenForPrisoner;

        internal static void TryInstall()
        {
            if (AccessTools.TypeByName(LaborGiverType) == null)
            {
                Log.Message("[PLReachFix] Prison Labor is not loaded; nothing to patch.");
                return;
            }
            var target = AccessTools.Method(LaborGiverType + ":TryGetJobOnCell", new[]
            {
                typeof(Pawn),
                typeof(TargetInfo).MakeByRefType(),
                typeof(WorkGiver_Scanner).MakeByRefType(),
                typeof(WorkGiver_Scanner),
            });
            if (target == null)
            {
                Log.Warning("[PLReachFix] Prison Labor's cell scan has a different signature; reachability fix disabled.");
                return;
            }
            try
            {
                // Reachability is the only change here, so an unbound forbidden check disables the
                // patch instead of silently swapping in vanilla's prisoner rules.
                isForbiddenForPrisoner = ResolveForbiddenCheck();
                if (isForbiddenForPrisoner == null)
                {
                    Log.Warning("[PLReachFix] Prison Labor's prisoner forbidden check could not be bound; reachability fix disabled.");
                    return;
                }
                new Harmony(PrisonLaborReachFixMod.HarmonyId).Patch(
                    target,
                    prefix: new HarmonyMethod(typeof(Patch_JobGiver_Labor), nameof(ScanReachableCells)));
                Log.Message("[PLReachFix] Cell work targets now require prisoner reachability.");
            }
            catch (Exception exception)
            {
                isForbiddenForPrisoner = null;
                Log.Error("[PLReachFix] Could not patch Prison Labor's cell scan: " + exception);
            }
        }

        /// <summary>
        /// Mirrors <c>JobGiver_Work.TryIssueJobPackage</c>'s cell selection: forbidden and job-less
        /// cells are dropped first, then unreachable cells, then the best remaining cell wins.
        /// Returning false always, because this method owns the whole scan.
        /// </summary>
        private static bool ScanReachableCells(Pawn pawn, ref TargetInfo targetInfo,
            ref WorkGiver_Scanner workGiver_Scanner, WorkGiver_Scanner scanner)
        {
            // Owning the scan means invalid input yields no job; the original method would throw.
            if (pawn == null || scanner == null || pawn.Map == null)
                return false;

            IntVec3 origin = pawn.Position;
            float bestDistanceSquared = 99999f;
            float bestPriority = float.MinValue;
            bool prioritized = scanner.Prioritized;
            // Vanilla explicitly opts out for scanners that register targets they cannot walk to
            // (for example roof building and grow zones), so those keep their existing behaviour.
            bool requireReachability = !scanner.AllowUnreachable;
            Danger maxPathDanger = scanner.MaxPathDanger(pawn);

            foreach (IntVec3 cell in scanner.PotentialWorkCellsGlobal(pawn))
            {
                float distanceSquared = (cell - origin).LengthHorizontalSquared;
                float priority = 0f;
                bool selected = false;

                if (prioritized)
                {
                    if (!isForbiddenForPrisoner(cell, pawn) && scanner.HasJobOnCell(pawn, cell, false))
                    {
                        // PathEndMode is read only here, as vanilla does: some scanners only define it
                        // for cells that passed HasJobOnCell.
                        if (requireReachability && !pawn.CanReach(cell, scanner.PathEndMode, maxPathDanger))
                            continue;
                        priority = scanner.GetPriority(pawn, cell);
                        selected = priority > bestPriority
                            || (priority == bestPriority && distanceSquared < bestDistanceSquared);
                    }
                }
                else if (distanceSquared < bestDistanceSquared
                    && !ForbidUtility.IsForbidden(cell, pawn)
                    && scanner.HasJobOnCell(pawn, cell, false))
                {
                    if (requireReachability && !pawn.CanReach(cell, scanner.PathEndMode, maxPathDanger))
                        continue;
                    selected = true;
                }

                if (!selected) continue;
                bestDistanceSquared = distanceSquared;
                bestPriority = priority;
                targetInfo = new TargetInfo(cell, pawn.Map, false);
                workGiver_Scanner = scanner;
            }
            return false;
        }

        private static Func<IntVec3, Pawn, bool> ResolveForbiddenCheck()
        {
            var method = AccessTools.Method(ForbiddenCheckName, new[] { typeof(IntVec3), typeof(Pawn) });
            return method == null
                ? null
                : (Func<IntVec3, Pawn, bool>)Delegate.CreateDelegate(typeof(Func<IntVec3, Pawn, bool>), method);
        }
    }
}
