using System.Collections.Generic;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Shared helper for the Bubble Machine mood thoughts (baby/toddler joy, child joy,
    /// adult annoyance). Determines whether a pawn is currently standing inside an ACTIVE
    /// Bubble Machine's zone, and if so, whether that specific zone is enclosed (confined
    /// to a fully-roofed room).
    /// </summary>
    /// <remarks>
    /// Deliberately reuses <see cref="CompBubbleZone"/>'s own zone geometry and "active" gate
    /// (<see cref="CompBubbleZone.ContainsCell"/> / <see cref="CompBubbleZone.IsActive"/> /
    /// <see cref="CompBubbleZone.GetIsZoneEnclosed"/>) rather than re-deriving any of that —
    /// this guarantees the mood always matches exactly what the player can see happening
    /// (same cells bubbles actually float in, same room-vs-circle shape, same "machine must
    /// be powered + switched on" requirement that gates real bubble spawning).
    /// </remarks>
    public static class BubbleMoodUtility
    {
        /// <summary>
        /// Returns true if <paramref name="pawn"/> is currently standing inside the bubble
        /// zone of at least one ACTIVE (powered + switched on) Bubble Machine on its current
        /// map. <paramref name="isEnclosed"/> reports whether at least one of the qualifying
        /// zones is confined to a fully-roofed room — only meaningful when this returns true,
        /// and only relevant to the adult "Annoying Bubbles" thought (babies/toddlers/children
        /// don't care whether they're indoors or outdoors).
        /// </summary>
        public static bool IsInActiveBubbleZone(Pawn pawn, out bool isEnclosed)
        {
            isEnclosed = false;

            // Defensive checks: a pawn with no map/position (e.g. mid-caravan-travel, or
            // between maps during a load) obviously can't be standing in any zone.
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            Map map = pawn.Map;
            IntVec3 pos = pawn.Position;

            // Scan every Bubble Machine on this map. A colony realistically has at most a
            // handful of these, and this method is only ever invoked from a ThoughtWorker on
            // the game's own periodic mood-thought refresh cadence (not every tick, and not
            // for every pawn every frame), so a plain list scan is cheap enough here — no
            // extra caching needed beyond what CompBubbleZone already does internally for
            // its own zone-cell cache.
            List<Thing> machines = map.listerThings.ThingsOfDef(TPE_ThingDefOf.TPE_BubbleMachine);
            if (machines.NullOrEmpty())
            {
                return false;
            }

            bool foundAny = false;
            for (int i = 0; i < machines.Count; i++)
            {
                if (machines[i] is not ThingWithComps machineThing)
                {
                    continue;
                }

                CompBubbleZone comp = machineThing.GetComp<CompBubbleZone>();
                if (comp == null || !comp.IsActive)
                {
                    continue;
                }

                if (!comp.ContainsCell(pos))
                {
                    continue;
                }

                foundAny = true;
                if (comp.GetIsZoneEnclosed())
                {
                    // An enclosed zone is the "strongest" reading for the adult thought —
                    // once we've found one enclosed zone covering this pawn, there's no need
                    // to keep scanning the rest of the machines on the map.
                    isEnclosed = true;
                    break;
                }
            }

            return foundAny;
        }
    }
}
