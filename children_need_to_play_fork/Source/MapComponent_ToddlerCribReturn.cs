using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Automatically returns crib-locked toddlers to their crib when they are
    /// temporarily out (e.g., after being fed). Runs independently of work
    /// priorities so locked toddlers are always returned promptly.
    ///
    /// <para>
    /// This component uses a <b>crib reservation system</b> to prevent the
    /// "multiple toddlers to one crib" bug. When a colonist is assigned a
    /// return job, the target crib is reserved for <see cref="CribReservationDuration"/>
    /// ticks. Other return jobs will skip reserved cribs. The reservation is
    /// purely internal to this component; it does not use vanilla
    /// <see cref="ReservationManager"/> because the vanilla system is
    /// per-pawn and would require every colonist to hold a reservation.
    /// </para>
    /// </summary>
    public class MapComponent_ToddlerCribReturn : MapComponent
    {
        /// <summary>How often (in ticks) this component scans for toddlers to return.</summary>
        const int ScanInterval = 60;

        /// <summary>Cooldown per colonist between return job assignments.</summary>
        const int AssignmentCooldown = 180;

        /// <summary>
        /// How long (in ticks) a crib stays reserved after a return job is
        /// assigned. 600 ticks ≈ 10 seconds at 60 t/s — enough time for a
        /// colonist to walk across a modest base and tuck the toddler in.
        /// </summary>
        const int CribReservationDuration = 600;

        /// <summary>
        /// Tracks the last tick each colonist was assigned a return job.
        /// Prevents spamming the same colonist with back-to-back jobs.
        /// </summary>
        readonly Dictionary<Pawn, int> lastAssignmentTick = new Dictionary<Pawn, int>();

        /// <summary>
        /// Tracks cribs that are already targeted by an in-flight return job.
        /// Key = crib, Value = tick at which the reservation expires.
        /// Prevents multiple colonists from being sent to the same unclaimed
        /// crib in the same scan tick (or in overlapping scans).
        /// </summary>
        readonly Dictionary<Building_Bed, int> cribReservationExpireTick = new Dictionary<Building_Bed, int>();

        public MapComponent_ToddlerCribReturn(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % ScanInterval != 0) return;
            int currentTick = Find.TickManager.TicksGame;

            // ── Cleanup phase ──────────────────────────────────────────────
            // Remove expired crib reservations. A reservation expires when
            // enough time has passed for the colonist to reach the crib and
            // complete TuckIntoBed (which claims the crib permanently).
            List<Building_Bed> expiredReservations = new List<Building_Bed>();
            foreach (var kvp in cribReservationExpireTick)
            {
                if (kvp.Key.Destroyed || kvp.Key.Map != map)
                    expiredReservations.Add(kvp.Key); // crib destroyed / despawned
                else if (currentTick > kvp.Value)
                    expiredReservations.Add(kvp.Key); // reservation timed out
            }
            foreach (Building_Bed bed in expiredReservations)
                cribReservationExpireTick.Remove(bed);

            // Remove dead / despawned colonists from assignment cooldown tracker.
            List<Pawn> deadColonists = new List<Pawn>();
            foreach (var kvp in lastAssignmentTick)
                if (kvp.Key.Destroyed || !kvp.Key.Spawned)
                    deadColonists.Add(kvp.Key);
            foreach (Pawn p in deadColonists)
                lastAssignmentTick.Remove(p);

            // ── Build candidate list ───────────────────────────────────────
            // Find locked toddlers who are not in a crib and not being carried.
            // MUST copy the list before iterating — AllPawnsSpawned is a
            // cached mutable list that vanilla modifies when pawns are
            // spawned/despawned (e.g., during pickup). Iterating the raw
            // list causes InvalidOperationException and dev-mode pause.
            List<Pawn> allSpawned = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            List<Pawn> toddlersToReturn = new List<Pawn>();
            foreach (Pawn p in allSpawned)
            {
                if (!CribLockUtility.IsCribLocked(p)) continue;
                if (CribUtility.InCrib(p)) continue;
                if (p.Downed || p.Dead) continue;
                // Skip toddlers who are currently being carried by anyone
                // (e.g., during vanilla feeding). Wait until they're dropped.
                if (p.ParentHolder is Pawn_CarryTracker) continue;

                // If a locked toddler does NOT have an assigned crib and
                // there are NO unowned cribs anywhere on the map, treat them
                // as if they are NOT crib-locked. Otherwise caregivers would
                // have nowhere valid to put them.
                if (p.ownership?.OwnedBed == null && !AnyUnownedCribOnMap(map))
                    continue;

                toddlersToReturn.Add(p);
            }

            if (toddlersToReturn.Count == 0) return;

            // ── Assign jobs ────────────────────────────────────────────────
            // MUST copy before iterating — FreeColonistsSpawned returns a
            // cached mutable list that gets cleared when any other code
            // accesses it, causing InvalidOperationException mid-loop.
            List<Pawn> freeColonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            foreach (Pawn colonist in freeColonists)
            {
                if (!IsValidReturner(colonist)) continue;
                if (lastAssignmentTick.TryGetValue(colonist, out int lastTick)
                    && currentTick - lastTick < AssignmentCooldown)
                    continue;

                Pawn? bestToddler = FindBestToddler(colonist, toddlersToReturn);
                if (bestToddler == null) continue;

                // Double-check the toddler hasn't been picked up since we
                // built the list (race with feeding jobs).
                if (bestToddler.ParentHolder is Pawn_CarryTracker) continue;

                Building_Bed? crib = FindBestCrib(bestToddler);
                if (crib == null) continue;

                Job job = JobMaker.MakeJob(
                    TPE_JobDefOf.TPE_ReturnToddlerToCrib,
                    bestToddler,
                    crib);
                job.count = 1;

                if (colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                {
                    lastAssignmentTick[colonist] = currentTick;

                    // RESERVE the crib so no other return job targets it
                    // while this colonist is walking there. The reservation
                    // is released after CribReservationDuration ticks.
                    cribReservationExpireTick[crib] = currentTick + CribReservationDuration;

                    toddlersToReturn.Remove(bestToddler);
                }

                if (toddlersToReturn.Count == 0) break;
            }
        }

        // ── Validation helpers ─────────────────────────────────────────────

        /// <summary>
        /// Returns true when the colonist is capable of picking up and carrying
        /// a toddler to a crib (not downed, not drafted, has hands, can walk).
        /// </summary>
        static bool IsValidReturner(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned) return false;
            if (p.Downed) return false;
            if (p.InMentalState) return false;
            if (p.Drafted) return false;
            if (p.carryTracker?.CarriedThing != null) return false;
            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return false;
            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return false;
            return true;
        }

        /// <summary>
        /// Finds the nearest toddler in <paramref name="toddlers"/> that this
        /// colonist can reserve and reach. Returns null if none are available.
        /// </summary>
        static Pawn? FindBestToddler(Pawn colonist, List<Pawn> toddlers)
        {
            Pawn? best = null;
            float bestDist = float.MaxValue;

            foreach (Pawn toddler in toddlers)
            {
                // Skip if toddler is currently reserved by another colonist
                // (e.g., being fed by a vanilla work giver).
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

        // ── Crib selection ─────────────────────────────────────────────────

        /// <summary>
        /// Picks the best crib for this toddler, respecting:
        /// <list type="bullet">
        /// <item>The toddler's assigned bed (if valid and not full)</item>
        /// <item>Unowned cribs with free slots that aren't reserved</item>
        /// <item>Any crib with free slots that isn't reserved (fallback)</item>
        /// </list>
        /// Returns null if no suitable crib exists.
        /// </summary>
        Building_Bed? FindBestCrib(Pawn toddler)
        {
            Map map = toddler.Map;

            // 1. Try to use the toddler's assigned bed first.
            Building_Bed? ownedBed = toddler.ownership?.OwnedBed;
            if (ownedBed != null
                && ownedBed.ForPrisoners == toddler.IsPrisoner
                && !ownedBed.Medical
                && ownedBed.SleepingSlotsCount > ownedBed.CurOccupants.Count()
                && !IsCribReserved(ownedBed))
            {
                return ownedBed;
            }

            // 2. Prefer an unowned crib with free sleeping slots that is
            //    not already reserved by another in-flight return job.
            Building_Bed? unowned = map.listerBuildings
                .AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b => IsValidCribCandidate(b, toddler, requireUnowned: true))
                .OrderBy(b => b.Position.DistanceToSquared(toddler.Position))
                .FirstOrDefault();
            if (unowned != null) return unowned;

            // 3. Fallback: nearest crib that simply has space (may be owned by
            //    another toddler, but still has an empty slot) and is not reserved.
            return map.listerBuildings
                .AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b => IsValidCribCandidate(b, toddler, requireUnowned: false))
                .OrderBy(b => b.Position.DistanceToSquared(toddler.Position))
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns true if <paramref name="bed"/> is a non-medical crib that
        /// matches the toddler's prisoner status and has at least one free slot.
        /// When <paramref name="requireUnowned"/> is true, the crib must also
        /// have no owners. Reserved cribs are always rejected.
        /// </summary>
        bool IsValidCribCandidate(Building_Bed bed, Pawn toddler, bool requireUnowned)
        {
            if (bed.def.defName != "Crib") return false;
            if (bed.Medical) return false;
            if (bed.ForPrisoners != toddler.IsPrisoner) return false;
            if (bed.SleepingSlotsCount <= bed.CurOccupants.Count()) return false;
            if (requireUnowned && bed.OwnersForReading.Any()) return false;
            if (IsCribReserved(bed)) return false;
            return true;
        }

        /// <summary>
        /// Returns true if the crib is currently reserved by an in-flight
        /// return job (i.e., another colonist has already been assigned to
        /// bring a toddler to this crib and hasn't arrived yet).
        /// </summary>
        bool IsCribReserved(Building_Bed bed)
        {
            return cribReservationExpireTick.ContainsKey(bed);
        }

        /// <summary>
        /// Returns true if there is at least one crib on the map that is not
        /// assigned to any pawn. Used to decide whether locked toddlers
        /// without their own assigned crib can realistically be returned.
        /// </summary>
        static bool AnyUnownedCribOnMap(Map map)
        {
            foreach (Building_Bed bed in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>())
            {
                if (bed.def.defName != "Crib" || bed.Medical) continue;
                if (!bed.OwnersForReading.Any()) return true;
            }
            return false;
        }
    }
}
