using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Automatically assigns colonists to let toddlers out of cribs when they
    /// have the <see cref="Hediff_TPE_WantsOutOfCrib"/> hediff. Runs independently
    /// of the work system so it works regardless of work priorities or Childcare settings.
    /// </summary>
    public class MapComponent_ToddlerCribRescue : MapComponent
    {
        // How often to scan for toddlers needing rescue (ticks)
        const int ScanInterval = 60;
        // How long a colonist remembers being assigned a rescue job (prevents re-assigning same job)
        const int AssignmentCooldown = 300;
        // Minimum ticks a toddler must be in crib before rescue is considered
        // Prevents interfering with normal crib placement
        const int MinTicksInCribBeforeRescue = 600; // 10 seconds

        // Colonist → tick when they were last assigned a rescue job
        readonly Dictionary<Pawn, int> lastAssignmentTick = new Dictionary<Pawn, int>();
        // Toddler → tick when they entered their crib
        readonly Dictionary<Pawn, int> timeEnteredCrib = new Dictionary<Pawn, int>();

        public MapComponent_ToddlerCribRescue(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % ScanInterval != 0) return;

            int currentTick = Find.TickManager.TicksGame;

            // FIX P1-4 / R1: Purge dead / despawned pawns from both tracking
            // dictionaries to prevent memory leaks.
            var deadOrGone = new List<Pawn>();
            foreach (var kvp in timeEnteredCrib)
            {
                if (kvp.Key.Destroyed || !kvp.Key.Spawned)
                    deadOrGone.Add(kvp.Key);
            }
            foreach (Pawn p in deadOrGone)
                timeEnteredCrib.Remove(p);

            var deadColonists = new List<Pawn>();
            foreach (var kvp in lastAssignmentTick)
            {
                if (kvp.Key.Destroyed || !kvp.Key.Spawned)
                    deadColonists.Add(kvp.Key);
            }
            foreach (Pawn p in deadColonists)
                lastAssignmentTick.Remove(p);

            // Update time-entered-crib tracking for all toddlers currently in cribs.
            // Copy the list first — AllPawnsSpawned is a cached mutable list that
            // vanilla modifies when pawns spawn/despawn.
            List<Pawn> allSpawned = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            foreach (Pawn p in allSpawned)
            {
                if (ToddlerUtility.IsToddler(p) && CribUtility.InCrib(p))
                {
                    if (!timeEnteredCrib.ContainsKey(p))
                        timeEnteredCrib[p] = currentTick;
                }
                else
                {
                    // Toddler left crib — remove tracking
                    timeEnteredCrib.Remove(p);
                }
            }

            // Find all toddlers who want out and haven't already been assigned a rescuer.
            // Copy the list first to avoid collection-modified exceptions.
            List<Pawn> allSpawned2 = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            List<Pawn> toddlersNeedingRescue = new List<Pawn>();
            foreach (Pawn p in allSpawned2)
            {
                if (!IsValidRescueTarget(p, currentTick)) continue;
                if (!HasBeenInCribLongEnough(p, currentTick)) continue;
                if (IsBeingRescued(p)) continue;
                toddlersNeedingRescue.Add(p);
            }

            if (toddlersNeedingRescue.Count == 0) return;

            // Find all capable colonists.
            // Copy the list first — FreeColonistsSpawned returns a cached
            // mutable list that gets cleared when other code accesses it.
            List<Pawn> freeColonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            foreach (Pawn colonist in freeColonists)
            {
                if (!IsValidRescuer(colonist)) continue;
                if (lastAssignmentTick.TryGetValue(colonist, out int lastTick) && currentTick - lastTick < AssignmentCooldown)
                    continue;

                // Find the best toddler for this colonist to rescue
                Pawn bestToddler = FindBestToddlerToRescue(colonist, toddlersNeedingRescue, currentTick);
                if (bestToddler == null) continue;

                Building_Bed crib = CribUtility.GetCurrentCrib(bestToddler);
                if (crib == null) continue;

                // Create and assign the job directly
                Job job = JobMaker.MakeJob(
                    DefDatabase<JobDef>.GetNamed("LetOutOfCrib", true),
                    bestToddler,
                    crib);
                job.count = 1;

                // Use TryTakeOrderedJob for reliable direct assignment
                // (StartJob can fail silently if the pawn is in certain states)
                if (colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                {
                    lastAssignmentTick[colonist] = currentTick;
                    toddlersNeedingRescue.Remove(bestToddler);
                }

                if (toddlersNeedingRescue.Count == 0) break;
            }
        }

        /// <summary>
        /// Returns true if the pawn is a toddler in a crib with the
        /// <see cref="Hediff_TPE_WantsOutOfCrib"/> hediff and is not crib-locked.
        /// </summary>
        static bool IsValidRescueTarget(Pawn p, int currentTick)
        {
            if (p == null || p.Dead || !p.Spawned) return false;
            if (!ToddlerUtility.IsToddler(p)) return false;
            if (!CribUtility.InCrib(p)) return false;
            if (p.health?.hediffSet == null) return false;
            // Must have the "wants out" hediff (appears after 3h grace + buildup)
            if (!p.health.hediffSet.HasHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib)) return false;
            // Crib-locked toddlers are intentionally confined by the player — do not rescue
            if (p.health.hediffSet.HasHediff(TPE_HediffDefOf.TPE_CribLocked)) return false;
            return true;
        }

        /// <summary>
        /// Checks if the toddler has been in their crib long enough to warrant rescue.
        /// Prevents interfering with normal crib placement.
        /// </summary>
        bool HasBeenInCribLongEnough(Pawn p, int currentTick)
        {
            if (!timeEnteredCrib.TryGetValue(p, out int enteredTick))
                return false;
            return currentTick - enteredTick >= MinTicksInCribBeforeRescue;
        }

        /// <summary>
        /// Returns true if the colonist can walk, manipulate, is not downed/mental/drafted/etc.
        /// </summary>
        static bool IsValidRescuer(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned) return false;
            if (p.Downed) return false;
            if (p.InMentalState) return false;
            if (p.Drafted) return false;
            // FIX N2: Colonists already carrying something cannot pick up a toddler.
            if (p.carryTracker?.CarriedThing != null) return false;
            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return false;
            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
            return true;
        }

        /// <summary>
        /// Checks if any colonist already has a LetOutOfCrib job targeting this toddler.
        /// </summary>
        static bool IsBeingRescued(Pawn toddler)
        {
            Map map = toddler.Map;
            if (map == null) return false;
            // FIX R4: Use def reference instead of string comparison so a
            // base-mod rename doesn't silently break our check.
            JobDef letOutDef = DefDatabase<JobDef>.GetNamed("LetOutOfCrib", false);
            if (letOutDef == null) return false;

            // Copy the list to avoid collection-modified exceptions.
            List<Pawn> colonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            foreach (Pawn p in colonists)
            {
                if (p.CurJob != null && p.CurJob.def == letOutDef)
                {
                    if (p.CurJob.targetA.Thing == toddler)
                        return true;
                }
                if (p.jobs.jobQueue != null)
                {
                    foreach (QueuedJob qj in p.jobs.jobQueue)
                    {
                        if (qj.job.def == letOutDef && qj.job.targetA.Thing == toddler)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Finds the nearest reachable toddler that this colonist can rescue.
        /// Only checks toddler reservation and reachability — the base mod's
        /// LetOutOfCrib job driver does not reserve the crib itself.
        /// </summary>
        Pawn FindBestToddlerToRescue(Pawn colonist, List<Pawn> toddlers, int currentTick)
        {
            Pawn best = null;
            float bestDist = float.MaxValue;

            foreach (Pawn toddler in toddlers)
            {
                // Must have been in crib long enough (prevents interfering with placement)
                if (!HasBeenInCribLongEnough(toddler, currentTick)) continue;

                if (!colonist.CanReserve(toddler, 1, -1, null, false)) continue;
                if (!colonist.CanReach(toddler, PathEndMode.Touch, Danger.Deadly)) continue;

                float dist = colonist.Position.DistanceToSquared(toddler.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = toddler;
                }
            }

            return best;
        }
    }
}
