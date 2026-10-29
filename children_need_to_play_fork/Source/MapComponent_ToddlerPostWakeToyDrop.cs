using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Detects when toddlers (or any pawn) get out of bed after sleeping,
    /// waits a random delay, then automatically drops any plush toy they
    /// are wearing or carrying. The dropped toy is always unforbidden so
    /// colonists can haul it back to storage.
    ///
    /// Design notes:
    ///  - Checks every 60 ticks (~1 real second at 1× speed) for performance.
    ///  - Uses <see cref="RestUtility.InBed"/> to detect bed occupancy; this
    ///    correctly handles cribs, beds, and medical beds.
    ///  - A pawn that goes back into bed before the timer expires has their
    ///    pending drop cancelled.
    ///  - Dead or despawned pawns are cleaned up automatically.
    ///  - Transient state (<see cref="inBed"/>, <see cref="pendingDrops"/>) is
    ///    NOT saved across loads. On reload a pawn still in bed is simply
    ///    re-added to <see cref="inBed"/> on the next tick; no false
    ///    "just woke up" transition can occur because the set starts empty.
    /// </summary>
    public class MapComponent_ToddlerPostWakeToyDrop : MapComponent
    {
        // ═══════════════════════════════════════════════════════════════════
        //  CONSTANTS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// How often we poll bed state, in ticks. 60 ticks ≈ 1 real second at
        /// 1× game speed — frequent enough to catch wake-up events promptly
        /// without burning CPU.
        /// </summary>
        private const int CheckInterval = 60;

        /// <summary>
        /// Random delay between detection and drop. Lower bound = 60 ticks
        /// (~1 in-game minute / 1 real second). Upper bound = 2500 ticks
        /// (1 in-game hour / ~42 real seconds at 1×).
        /// </summary>
        private static readonly IntRange DropDelayTicks = new IntRange(60, 2500);

        // ═══════════════════════════════════════════════════════════════════
        //  RUNTIME STATE  (not persisted — see class doc comment)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set of pawn ThingIDs that were detected as in-bed on the last check.
        /// Used to detect the transition from in-bed → out-of-bed.
        /// </summary>
        private readonly HashSet<string> inBed = new HashSet<string>();

        /// <summary>
        /// Maps pawn ThingID → game tick at which the plush should be dropped.
        /// Only contains pawns who left bed and still have an active timer.
        /// </summary>
        private readonly Dictionary<string, int> pendingDrops = new Dictionary<string, int>();

        // ═══════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════

        public MapComponent_ToddlerPostWakeToyDrop(Map map) : base(map) { }

        // ═══════════════════════════════════════════════════════════════════
        //  TICK
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called every <see cref="CheckInterval"/> ticks. Iterates all
        /// spawned humanlike pawns, updates bed-state tracking, handles
        /// pending drop timers, and cleans up stale entries.
        /// </summary>
        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckInterval != 0)
                return;

            int currentTick = Find.TickManager.TicksGame;

            // Defensive copy: AllPawnsSpawned is a cached mutable list that
            // vanilla may modify when pawns spawn/despawn or die.
            List<Pawn> allSpawned = new List<Pawn>(map.mapPawns.AllPawnsSpawned);

            foreach (Pawn pawn in allSpawned)
            {
                if (!pawn.RaceProps.Humanlike)
                    continue;

                string key = pawn.ThingID;
                bool currentlyInBed = RestUtility.InBed(pawn);

                // ── Handle active pending drop timers first ───────────────
                if (pendingDrops.TryGetValue(key, out int dropTick))
                {
                    if (currentlyInBed)
                    {
                        // Pawn went back to bed before the timer fired.
                        // Cancel the drop and mark them as in-bed again.
                        pendingDrops.Remove(key);
                        inBed.Add(key);
                        continue;
                    }

                    if (currentTick >= dropTick)
                    {
                        // Timer expired — attempt to drop the plush.
                        TryDropPlush(pawn);
                        pendingDrops.Remove(key);
                    }

                    // Still waiting for timer — skip bed-transition logic.
                    continue;
                }

                // ── Track bed-state transitions ────────────────────────────
                if (currentlyInBed)
                {
                    // Pawn is in bed — record for next check.
                    inBed.Add(key);
                }
                else if (inBed.Contains(key))
                {
                    // Transition: was in bed, now out of bed.
                    inBed.Remove(key);

                    // Only start the drop timer if the pawn actually has a
                    // plush toy equipped or carried.
                    if (TPE_ToyUtility.HasPlushEquippedOrCarried(pawn))
                    {
                        pendingDrops[key] = currentTick + DropDelayTicks.RandomInRange;
                    }
                }
            }

            // ── Cleanup: remove entries for dead/despawned pawns ──────────
            CleanupStaleEntries(allSpawned);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DROP LOGIC
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempts to drop the first plush toy found on the pawn.
        /// Checks worn apparel first, then the carry tracker as a fallback.
        /// In both cases the dropped thing is explicitly unforbidden.
        /// </summary>
        private static void TryDropPlush(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return;

            // ── Try worn apparel first ───────────────────────────────────
            if (pawn.apparel?.WornApparel != null)
            {
                // Iterate over a copy because TryDrop may modify the collection.
                foreach (Apparel apparel in pawn.apparel.WornApparel.ToList())
                {
                    if (TPE_ToyUtility.IsPlushThing(apparel))
                    {
                        Apparel? dropped;
                        if (pawn.apparel.TryDrop(apparel, out dropped, pawn.PositionHeld, forbid: false))
                        {
                            // TryDrop with forbid:false already unforbids, but we
                            // call Unforbid again as a defensive second layer.
                            if (dropped != null)
                                TPE_ToyUtility.Unforbid(dropped);
                        }
                        return; // Drop at most one plush per timer.
                    }
                }
            }

            // ── Fallback: carried plush ──────────────────────────────────
            if (pawn.carryTracker?.CarriedThing != null
                && TPE_ToyUtility.IsPlushThing(pawn.carryTracker.CarriedThing))
            {
                Thing? dropped;
                if (pawn.carryTracker.TryDropCarriedThing(
                        pawn.PositionHeld, ThingPlaceMode.Near, out dropped))
                {
                    if (dropped != null)
                        TPE_ToyUtility.Unforbid(dropped);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CLEANUP
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Removes bed-state and pending-drop entries for any pawn that is
        /// no longer spawned (died, despawned, or left the map).
        /// </summary>
        private void CleanupStaleEntries(List<Pawn> currentSpawned)
        {
            HashSet<string> aliveKeys = new HashSet<string>();
            foreach (Pawn p in currentSpawned)
            {
                aliveKeys.Add(p.ThingID);
            }

            inBed.RemoveWhere(id => !aliveKeys.Contains(id));

            // Dictionary has no RemoveWhere — collect keys first.
            List<string> staleDropKeys = pendingDrops.Keys
                .Where(id => !aliveKeys.Contains(id))
                .ToList();

            foreach (string key in staleDropKeys)
            {
                pendingDrops.Remove(key);
            }
        }
    }
}
