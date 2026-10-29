using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Shared placement rules + settings gate for dirt castles
    /// (<see cref="Building_DirtCastle"/>).
    ///
    /// WHY THIS EXISTS: the castle ThingDef is an edifice-style Building, and
    /// <c>GenSpawn.Spawn</c> defaults to <c>WipeMode.Vanish</c>. Per vanilla's
    /// <c>GenSpawn.SpawningWipes</c>, spawning a Building like this silently
    /// DESTROYS any plant in the cell (grass AND crops — via
    /// <c>BlocksPlanting</c>) and any other edifice. The original driver only
    /// checked <c>Standable</c>, which lets sown crops, growing-zone cells and
    /// non-blocking buildings (traps, conduits, …) through, so a finished castle
    /// could eat a colonist's crop or sit on a building. These helpers give BOTH
    /// call sites — the cell-picking giver
    /// (<see cref="PlayDesireGiver_BuildDirtCastles"/>) and the spawn step in
    /// <see cref="JobDriver_ChildBuildDirtCastles"/> — ONE definition of "a cell a
    /// castle may occupy", per Architecture.md §2.8 (don't duplicate a check
    /// across giver + driver files).
    ///
    /// SCOPE (user-confirmed): a castle must never override BUILDINGS or CROPS.
    /// Wild grass and other trivial ground clutter (items, filth, blueprints,
    /// stockpile zones, …) are deliberately NOT protected — a castle may replace
    /// wild plants/filth like any vanilla building would.
    ///
    /// PERFORMANCE: every method is allocation-free (index loops over the
    /// engine's own per-cell Thing list, O(1) grid lookups, no LINQ, no
    /// closures) and ordered cheapest-first, because the giver evaluates them
    /// on up to dozens of random candidate cells per call. Nothing here runs per
    /// tick — only at job-selection time and once when a session ends.
    /// </summary>
    public static class DirtCastleUtility
    {
        /// <summary>
        /// True when finished dirt-castle sessions should leave a castle behind
        /// (<see cref="TPE_Settings.showDirtCastles"/>, default ON). Falls back
        /// to ON if settings aren't available yet, preserving the original
        /// always-spawns behaviour. Read fresh each call so toggling the setting
        /// takes effect immediately (Architecture.md §3: runtime check, never a
        /// conditional patch).
        /// </summary>
        public static bool CastlesEnabled
        {
            get { return TPE_Mod.Settings?.showDirtCastles ?? true; }
        }

        /// <summary>
        /// The "is this natural, open-air, walkable ground" half of the rules
        /// (the checks the giver always had): in bounds, unroofed, standable,
        /// and on natural terrain — not water, not a bridge, not constructed
        /// flooring (<c>layerable</c>; natural ground isn't, floors are).
        /// Split out from <see cref="IsCellFreeOfBuildingsAndCrops"/> so the giver
        /// can skip the "buildings/crops" half when castles are switched OFF
        /// (no castle is ever spawned then, so there is nothing to override).
        /// </summary>
        public static bool IsNaturalOutdoorCell(IntVec3 c, Map map)
        {
            // Null/bounds first — InBounds(null) would NRE, and every grid
            // lookup below is only valid for an in-bounds cell.
            if (map == null || !c.InBounds(map))
                return false;
            if (c.Roofed(map))
                return false;
            if (!c.Standable(map))
                return false;

            TerrainDef terrain = c.GetTerrain(map);
            if (terrain == null)
                return false;
            // Reject water and bridges.
            if (terrain.IsWater || terrain.bridge)
                return false;
            // Reject constructed flooring (floors are layerable; natural ground isn't).
            if (terrain.layerable)
                return false;

            return true;
        }

        /// <summary>
        /// The "castles can't override buildings or crops" half of the rules.
        /// A cell is NOT free if it:
        ///  • is a growing-zone cell (<c>Zone_Growing</c>) — crop land, whether
        ///    currently planted or fallow (a castle would wipe the plant and, for
        ///    its ~3-day lifespan, block sowing there); or
        ///  • holds a colony-sown plant (<c>Plant.sown</c>) — the same
        ///    "colonist planted this" flag vanilla's own
        ///    <c>DropCellFinder.CellHasCrops</c> uses to avoid trampling crops
        ///    (kept even outside a zone, e.g. leftovers after the zone was
        ///    deleted); or
        ///  • holds ANY Building-category thing — built buildings, construction
        ///    frames, traps, conduits, other dirt castles, … (a castle, being an
        ///    edifice, would wipe or sit on it).
        /// Everything else is allowed: wild grass/bushes (<c>sown == false</c>),
        /// items, filth, blueprints, non-growing zones, pawns, motes. NOTE the
        /// resulting spawn WILL still wipe wild plants and filth in the cell
        /// (default <c>WipeMode.Vanish</c> semantics — same as any vanilla
        /// building placed on grass); that is intended per the user ("grass and
        /// stuff like that is fine").
        /// Self-protecting: an out-of-bounds (or null-map) cell is reported as
        /// NOT free instead of reaching the unchecked fast grid accessors below
        /// (which would throw IndexOutOfRange). Both current callers also
        /// bounds-check first via <see cref="IsNaturalOutdoorCell"/>, so in
        /// practice this is a cheap (4 integer compares) guard for future callers.
        /// </summary>
        public static bool IsCellFreeOfBuildingsAndCrops(IntVec3 c, Map map)
        {
            if (map == null || !c.InBounds(map))
                return false;

            // Growing zone check is a single array read + type test — do it
            // first (cheapest reject).
            if (map.zoneManager.ZoneAt(c) is Zone_Growing)
                return false;

            // ThingsListAtFast returns the engine's own live list (no copy, no
            // allocation); we only read it, never mutate, so a plain indexed
            // loop is safe and enumerator-free.
            List<Thing> things = map.thingGrid.ThingsListAtFast(c);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                switch (t.def.category)
                {
                    case ThingCategory.Building:
                        // Any building / frame / other castle.
                        return false;
                    case ThingCategory.Plant:
                        // Only COLONY-SOWN plants are crops; wild plants are fine.
                        if (t is Plant plant && plant.sown)
                            return false;
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Full spawn-time guard: natural open-air ground AND no building/crop
        /// there. Used as the final authority right before spawning, because the
        /// world can change during the ~3-hour build session (the player lays a
        /// floor, builds something, sows the cell, or zones it for growing) even
        /// though the cell was valid when the child picked it.
        /// </summary>
        public static bool CanSpawnCastleAt(IntVec3 c, Map map)
        {
            return IsNaturalOutdoorCell(c, map) && IsCellFreeOfBuildingsAndCrops(c, map);
        }
    }
}
