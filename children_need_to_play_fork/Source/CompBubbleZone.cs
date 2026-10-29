using System;
using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Comp for Bubble Machine buildings. Manages bubble spawning and zone calculation.
    /// </summary>
    public class CompBubbleZone : ThingComp
    {
        /// <summary>
        /// Properties for this comp loaded from the ThingDef.
        /// </summary>
        public CompProperties_BubbleZone Props => (CompProperties_BubbleZone)props;

        /// <summary>
        /// Ticks until next bubble spawn attempt.
        /// </summary>
        private int ticksUntilSpawn = 0;

        /// <summary>
        /// Cached list of cells in the bubble zone (for calculating max bubbles).
        /// </summary>
        private List<IntVec3> cachedZoneCells = null;

        /// <summary>
        /// Same cells as <see cref="cachedZoneCells"/>, but as a HashSet for O(1) "is this
        /// cell inside my zone" containment checks. Rebuilt alongside <see cref="cachedZoneCells"/>
        /// every time that cache refreshes (see <see cref="GetBubbleZoneCells"/>). Bubbles
        /// capture a reference to this exact set instance at spawn time (see
        /// <see cref="TrySpawnBubble"/>) and use it for the rest of their life as their hard
        /// movement boundary — see <see cref="Mote_Bubble"/>'s remarks for why a circular
        /// distance clamp isn't good enough for non-circular (room-shaped) zones.
        /// </summary>
        private HashSet<IntVec3> cachedZoneCellsSet = null;

        /// <summary>
        /// Whether the machine's current room is fully roofed (true) or not (false, e.g.
        /// outdoors or under a partial/open roof). Note this does NOT affect the shape of
        /// <see cref="cachedZoneCells"/> any more -- both cases use the exact same bounded,
        /// wall-respecting flood-fill (see <see cref="GetBubbleZoneCells"/>) -- this flag is
        /// purely about roof coverage. Rebuilt alongside <see cref="cachedZoneCells"/> every
        /// cache refresh from the exact same room check. Exposed publicly via
        /// <see cref="GetIsZoneEnclosed"/> for the adult "Annoying Bubbles" mood thought
        /// (only applies indoors, i.e. only when this is true).
        /// </summary>
        private bool cachedIsFullyRoofed = false;

        /// <summary>
        /// Tick when zone cells were last cached.
        /// </summary>
        private int lastCacheTick = -1;

        /// <summary>
        /// Cache validity duration in ticks (recalculate zone every 60 ticks).
        /// </summary>
        private const int CacheDurationTicks = 60;

        /// <summary>
        /// Radius of the bubble zone in tiles.
        /// </summary>
        private const float ZoneRadius = 15f;

        /// <summary>
        /// Color used to outline the bubble zone while the machine is selected (see
        /// <see cref="PostDrawExtraSelectionOverlays"/>). A soft cyan to read as "bubbles/water"
        /// without being visually loud, matching the weight of vanilla's own selection-overlay
        /// colors (e.g. room highlights, stockpile borders).
        /// </summary>
        private static readonly Color BubbleZoneOverlayColor = new Color(0.4f, 0.85f, 1f);

        /// <summary>
        /// If this machine currently has fewer than this many active bubbles, the next
        /// spawn attempt uses <see cref="LowBubbleCountSpawnInterval"/> instead of the
        /// normal randomized interval, so a freshly-turned-on (or just-placed) machine
        /// doesn't feel like it's trickling out bubbles one at a time with long gaps.
        /// </summary>
        private const int LowBubbleCountThreshold = 5;

        /// <summary>
        /// Fast spawn-attempt interval (in ticks) used while under <see cref="LowBubbleCountThreshold"/>.
        /// </summary>
        private const int LowBubbleCountSpawnInterval = 50;

        /// <summary>
        /// Normal spawn-attempt interval range (in ticks), used once the machine has at
        /// least <see cref="LowBubbleCountThreshold"/> bubbles active.
        /// </summary>
        private const int NormalSpawnIntervalMin = 150;
        private const int NormalSpawnIntervalMax = 250;

        /// <summary>
        /// Randomized total on-screen duration range (in seconds) for each bubble
        /// (fadeIn + solid + fadeOut combined). Applied per-bubble at spawn time via
        /// <see cref="Mote.solidTimeOverride"/>; see <see cref="Mote_Bubble.EndOfLife"/>
        /// for why the override alone isn't enough and needs that companion override.
        /// </summary>
        private const float BubbleDurationMinSeconds = 8f;
        private const float BubbleDurationMaxSeconds = 15f;

        /// <summary>
        /// Bubbles this specific machine has spawned and that are still alive. This is the
        /// single source of truth for "how many bubbles belong to this machine" and is
        /// the machine-side half of the bidirectional ownership link with <see cref="Mote_Bubble.SourceMachine"/>.
        /// </summary>
        /// <remarks>
        /// Previously <see cref="CountActiveBubbles"/> scanned the WHOLE map for every
        /// <c>TPE_Mote_Bubble</c> and attributed a bubble to this machine purely by
        /// checking horizontal distance to <c>parent.Position</c>. That has no real
        /// concept of ownership: two machines placed near each other (their 15-tile
        /// zones easily overlap) would each count bubbles the OTHER machine spawned,
        /// corrupting the per-machine cap, the low-bubble-count fast-spawn trigger, and
        /// the god-mode inspect string. Tracking the actual list a machine spawned fixes
        /// that regardless of how close two machines are, and is also strictly cheaper
        /// (no full-map scan every spawn attempt).
        /// </remarks>
        private readonly List<Mote_Bubble> trackedBubbles = new List<Mote_Bubble>();

        /// <summary>
        /// Gets the power comp to check if the machine is powered.
        /// </summary>
        private CompPowerTrader PowerComp => parent.GetComp<CompPowerTrader>();

        /// <summary>
        /// Gets the flickable comp to check if the machine is turned on.
        /// </summary>
        private CompFlickable FlickComp => parent.GetComp<CompFlickable>();

        /// <summary>
        /// Gets the refuelable comp to check if the machine has chemfuel. The machine
        /// requires BOTH power (see <see cref="PowerComp"/>) AND chemfuel to run — see
        /// <see cref="IsMachineActive"/>.
        /// </summary>
        private CompRefuelable RefuelComp => parent.GetComp<CompRefuelable>();

        /// <summary>
        /// Initialize the comp with a random spawn timer.
        /// </summary>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // Random interval between 150-250 ticks for first spawn
            ticksUntilSpawn = Rand.Range(NormalSpawnIntervalMin, NormalSpawnIntervalMax);
        }

        /// <summary>
        /// Called every tick to update bubble spawning.
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();

            // Only spawn bubbles if the machine is powered and turned on
            if (!IsMachineActive())
            {
                return;
            }

            // Decrement spawn timer
            ticksUntilSpawn--;

            // Time to try spawning a bubble
            if (ticksUntilSpawn <= 0)
            {
                // TrySpawnBubble returns the active-bubble count immediately after this
                // attempt (whether or not it actually spawned one), so we can decide the
                // next interval without a second, separate count scan.
                int bubbleCountAfterAttempt = TrySpawnBubble();

                // While under the low-count threshold, spawn much more often so bubbles
                // feel like they're appearing consistently instead of one at a time with
                // long gaps between them.
                ticksUntilSpawn = (bubbleCountAfterAttempt < LowBubbleCountThreshold)
                    ? LowBubbleCountSpawnInterval
                    : Rand.Range(NormalSpawnIntervalMin, NormalSpawnIntervalMax);
            }
        }

        /// <summary>
        /// Debug-only inspect string: shows how many bubbles are currently tied to this
        /// machine, and the current maximum this machine can have, as "active / max". Only
        /// visible with God Mode enabled (Dev Mode's god-mode toggle), matching the convention
        /// of debug-only inspect info elsewhere in RimWorld.
        /// </summary>
        public override string CompInspectStringExtra()
        {
            if (!DebugSettings.godMode)
            {
                return null;
            }

            // Max is recomputed from the current zone cells (not cached separately) so it
            // stays correct if the machine's room changes shape/roof state between calls.
            List<IntVec3> zoneCells = GetBubbleZoneCells();
            int maxBubbles = GetMaxBubblesForMachine(zoneCells);
            return $"[Dev] Bubbles tied to this machine: {CountActiveBubbles()} / {maxBubbles}";
        }

        /// <summary>
        /// Draws an outline around the machine's current bubble zone while it's selected —
        /// the same idea as the radius indicator vanilla shows for a lamp's glow radius while
        /// selected, except this traces the zone's ACTUAL shape — the real, wall-bounded,
        /// circle-trimmed area (see <see cref="FloodFillWithinRoom"/>), which is a roughly
        /// circular patch when in the open and an irregular (wall-clipped) room outline when
        /// enclosed by a room smaller than the zone radius — rather than assuming a plain,
        /// wall-ignoring circle the way a naive radius indicator would. Uses
        /// <see cref="GenDraw.DrawFieldEdges"/> —
        /// the same primitive vanilla uses for e.g. stockpile zone borders and room highlights
        /// — which works with any cell shape instead of assuming a radial pattern the way
        /// <see cref="GenDraw.DrawRadiusRing"/> does.
        /// </summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();

            List<IntVec3> zoneCells = GetBubbleZoneCells();
            if (!zoneCells.NullOrEmpty())
            {
                GenDraw.DrawFieldEdges(zoneCells, BubbleZoneOverlayColor);
            }
        }

        /// <summary>
        /// Called by a <see cref="Mote_Bubble"/> (via its <c>Destroy</c> override) the moment
        /// it's destroyed, so <see cref="trackedBubbles"/> stays accurate without waiting for
        /// the next defensive prune in <see cref="CountActiveBubbles"/>. Safe to call even if
        /// the bubble was already removed (e.g. by <see cref="PostDeSpawn"/>) — <c>List.Remove</c>
        /// is a no-op when the item isn't present.
        /// </summary>
        /// <param name="bubble">The bubble that was just destroyed.</param>
        public void NotifyBubbleDestroyed(Mote_Bubble bubble)
        {
            trackedBubbles.Remove(bubble);
        }

        /// <summary>
        /// Called when this machine leaves the map for ANY reason — destroyed, deconstructed,
        /// minified/picked up, or the whole map being removed (DeSpawn always runs before
        /// Destroy for a spawned thing, so this fires in every one of those cases). Clears the
        /// back-reference on every bubble this machine spawned so none of them are left holding
        /// a reference to a comp whose parent is no longer on a map, then clears our own list.
        /// </summary>
        /// <remarks>
        /// The bubbles themselves are deliberately NOT force-destroyed here. Each bubble already
        /// captured its own <c>zoneCenter</c>/<c>zoneRadius</c> at spawn time (see
        /// <see cref="Mote_Bubble.SetBubbleZone"/>) and drives its own movement/lifespan
        /// entirely independently of the machine after that — exactly like turning the machine
        /// off already lets in-flight bubbles finish out their life (see <see cref="IsMachineActive"/>)
        /// instead of vanishing abruptly. Losing the machine works the same way.
        /// </remarks>
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);

            foreach (Mote_Bubble bubble in trackedBubbles)
            {
                if (bubble != null)
                {
                    bubble.SourceMachine = null;
                }
            }
            trackedBubbles.Clear();
        }

        /// <summary>
        /// Public read of whether this machine is currently active (powered + switched on,
        /// i.e. actually producing bubbles right now). Used by <see cref="BubbleMoodUtility"/>
        /// so the bubble mood thoughts only ever apply while there are real bubbles to see —
        /// an idle/unpowered machine shouldn't make anyone happy OR annoyed.
        /// </summary>
        public bool IsActive => IsMachineActive();

        /// <summary>
        /// Checks if the bubble machine is active (powered and switched on).
        /// </summary>
        private bool IsMachineActive()
        {
            // Must have power comp and be powered
            CompPowerTrader powerComp = PowerComp;
            if (powerComp == null || !powerComp.PowerOn)
            {
                return false;
            }

            // If has flick comp, must be switched on
            CompFlickable flickComp = FlickComp;
            if (flickComp != null && !flickComp.SwitchIsOn)
            {
                return false;
            }

            // Must also have chemfuel — the machine requires BOTH power AND fuel to
            // actually run, not either/or. If the ThingDef somehow lacks the refuelable
            // comp (e.g. a future variant), skip this check entirely rather than treat
            // "no comp" as "no fuel" — mirrors the same null-safe pattern used above for
            // powerComp/flickComp.
            CompRefuelable refuelComp = RefuelComp;
            if (refuelComp != null && !refuelComp.HasFuel)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to spawn a single bubble in the zone.
        /// </summary>
        /// <returns>
        /// The active-bubble count for this machine immediately after the attempt
        /// (whether or not a bubble was actually spawned), so the caller can decide
        /// the next spawn interval without a redundant second count scan.
        /// </returns>
        private int TrySpawnBubble()
        {
            Map map = parent.Map;
            if (map == null)
            {
                return 0;
            }

            // Check global bubble limit
            if (!BubbleSettings.CanSpawnBubble(map))
            {
                return CountActiveBubbles();
            }

            // Get zone cells for this machine
            List<IntVec3> zoneCells = GetBubbleZoneCells();
            if (zoneCells.NullOrEmpty())
            {
                return CountActiveBubbles();
            }

            // Calculate max bubbles for this machine
            int maxBubblesPerMachine = GetMaxBubblesForMachine(zoneCells);

            // Count existing bubbles from this machine
            int activeBubbles = CountActiveBubbles();
            if (activeBubbles >= maxBubblesPerMachine)
            {
                return activeBubbles; // Already at max
            }

            // Pick a random cell in the zone and get a random position within that cell
            IntVec3 randomCell = zoneCells[Rand.Range(0, zoneCells.Count)];
            Vector3 spawnPos = randomCell.ToVector3Shifted();

            // Add some random offset within the cell for more natural positioning
            spawnPos += new Vector3(
                Rand.Range(-0.4f, 0.4f),
                0f,
                Rand.Range(-0.4f, 0.4f)
            );

            // Create the bubble mote
            Mote_Bubble bubble = (Mote_Bubble)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("TPE_Mote_Bubble"));
            if (bubble == null)
            {
                return activeBubbles;
            }

            // Give the bubble the ACTUAL zone cell set (whatever shape it is — a real room's
            // cells, or the wall-bounded flood-fill patch in the open) so it can hard-contain
            // its own movement to it. See
            // Mote_Bubble.SetBubbleZone / TimeInterval remarks for why a plain center+radius
            // clamp isn't good enough for a room-shaped zone.
            HashSet<IntVec3> zoneCellSet = GetBubbleZoneCellsSet();
            bubble.SetBubbleZone(zoneCells, zoneCellSet);

            // Random rotation for visual variety
            bubble.exactRotation = Rand.Range(0f, 360f);

            // Random scale between 0.5x and 1.2x
            float randomScale = Rand.Range(0.5f, 1.2f);
            bubble.Scale = randomScale;

            // Randomize total on-screen duration to 8-15 seconds (fadeIn + solid + fadeOut
            // combined). We back the "solid" portion out of the target total using the
            // def's ACTUAL fadeInTime/fadeOutTime (rather than hardcoding them) so this
            // stays correct even if those XML values change later. Mote_Bubble.EndOfLife
            // is overridden to read this same solidTimeOverride, keeping the fade-out
            // animation and the actual destruction point perfectly in sync.
            float totalDurationSeconds = Rand.Range(BubbleDurationMinSeconds, BubbleDurationMaxSeconds);
            float solidTimeSeconds = totalDurationSeconds - bubble.def.mote.fadeInTime - bubble.def.mote.fadeOutTime;
            bubble.solidTimeOverride = Mathf.Max(0f, solidTimeSeconds);

            // Spawn the bubble, then apply the jittered spawn position.
            // Mote.SpawnSetup() sets exactPosition from the unshifted cell corner
            // (IntVec3.ToVector3(), not ToVector3Shifted()), so the randomized
            // spawnPos computed above must be applied AFTER Spawn or it's discarded
            // (this mirrors the pattern used by vanilla MoteMaker.MakeStaticMote).
            GenSpawn.Spawn(bubble, randomCell, map);
            bubble.exactPosition = spawnPos;

            // Bidirectional ownership link: the bubble can trace back to the machine that
            // spawned it (SourceMachine), and this machine tracks exactly which bubbles are
            // its own (trackedBubbles). See the remarks on trackedBubbles for why a simple
            // proximity scan wasn't good enough.
            bubble.SourceMachine = this;
            trackedBubbles.Add(bubble);

            // Increment global bubble counter
            BubbleSettings.AddBubbleToMap(map);

            return activeBubbles + 1;
        }

        /// <summary>
        /// Gets the list of cells in the bubble zone.
        /// Uses caching to avoid expensive calculations every tick. Also rebuilds
        /// <see cref="cachedZoneCellsSet"/> (the HashSet form) in lockstep, so the two never
        /// drift out of sync with each other.
        /// </summary>
        private List<IntVec3> GetBubbleZoneCells()
        {
            // Use cache if still valid
            if (cachedZoneCells != null && (Find.TickManager.TicksGame - lastCacheTick) < CacheDurationTicks)
            {
                return cachedZoneCells;
            }

            cachedZoneCells = new List<IntVec3>();
            lastCacheTick = Find.TickManager.TicksGame;

            Map map = parent.Map;
            if (map == null)
            {
                cachedZoneCellsSet = new HashSet<IntVec3>(cachedZoneCells);
                cachedIsFullyRoofed = false;
                return cachedZoneCells;
            }

            IntVec3 machinePos = parent.Position;

            // Try to get the room the machine is in
            Room room = machinePos.GetRoom(map);
            bool isFullyRoofed = room != null && room.OpenRoofCount == 0;

            // ALWAYS use the bounded, wall-respecting flood-fill -- regardless of whether the
            // machine's room is fully roofed. Previously, a machine that WASN'T in a fully
            // roofed room (e.g. placed outdoors, or under a partial/open roof) fell back to a
            // raw Euclidean-distance circle that completely ignored walls: any building within
            // ZoneRadius tiles as the crow flies had its ENTIRE interior included in the zone,
            // even though the machine was standing outside it with a solid wall in between --
            // a real floating bubble obviously can't pass through that wall. FloodFillWithinRoom
            // fixes this by only ever stepping into a cardinally-adjacent cell that belongs to
            // the SAME Room as the one just visited -- a cell on the far side of any wall
            // (interior OR exterior, whether or not the machine's own room happens to be
            // roofed) is a member of a DIFFERENT Room and is correctly excluded. This also
            // fixed the earlier "huge merged room" bug (see remarks below) for free, since it's
            // the exact same mechanism applied uniformly instead of two different algorithms
            // for the roofed/unroofed cases.
            //
            // FloodFillWithinRoom ALSO trims its own raw cardinal-only exploration (which forms
            // a diamond, not a circle -- see that method's remarks) back down to a real,
            // roughly-circular patch of radius ZoneRadius, so an open-air zone reads as the
            // same circular shape players expect from vanilla radius indicators instead of a
            // visibly diamond-shaped patch.
            //
            // This is safe performance-wise even for the "standing in the open outdoors" case,
            // where the machine's Room could be the single enormous "outdoors" Room spanning
            // most of the revealed map -- the flood-fill's exploration phase never walks more
            // than a small, fixed multiple of ZoneRadius steps regardless of how large the
            // underlying Room object actually is, so it can never become the same "iterate
            // potentially thousands of cells" cost the old circular fallback was presumably
            // trying to avoid by using raw distance instead.
            //
            // A RimWorld Room can ALSO span an arbitrarily large, sprawling area if there's no
            // wall/closed door between structurally-distinct-looking chambers (e.g. several
            // separate rooms joined by open doorways all merge into ONE Room object) -- taking
            // every cell of such a room unconditionally (the ORIGINAL behaviour, before either
            // fix) let bubbles appear in a chamber many tiles and several doorways away from
            // the machine. Bounding the flood-fill's exploration handles that case too.
            cachedZoneCells.AddRange(FloodFillWithinRoom(machinePos, room, map, ZoneRadius));

            cachedZoneCellsSet = new HashSet<IntVec3>(cachedZoneCells);
            cachedIsFullyRoofed = isFullyRoofed;
            return cachedZoneCells;
        }

        /// <summary>
        /// Multiplier converting a desired real (Euclidean) zone radius into the Manhattan-
        /// distance step cap the cardinal-only exploration phase of <see cref="FloodFillWithinRoom"/>
        /// must walk out to in order to guarantee full coverage of that circle.
        /// </summary>
        /// <remarks>
        /// The worst case is a cell sitting at exactly 45 degrees from the origin (|dx| == |dz|):
        /// its real (Euclidean) distance is <c>dx * sqrt(2)</c>, but reaching it via
        /// cardinal-only (N/S/E/W) steps costs <c>|dx| + |dz| = dx * 2</c> steps -- i.e.
        /// <c>(Euclidean distance) * sqrt(2)</c> steps. <c>sqrt(2) ≈ 1.41421356</c> is therefore
        /// the continuous-space bound for the exploration cap: anything smaller would clip the
        /// diagonal corners of the desired circle (cells that are legitimately within radius,
        /// but whose cardinal-step "cost" is higher than their real distance). Rounding UP via
        /// <see cref="Mathf.CeilToInt(float)"/> (rather than truncating) is what actually
        /// guarantees this in integer-cell space -- the grid is discrete, so the true minimum
        /// step count needed can be a little less than the continuous-space value in places,
        /// but never more, so rounding up can only ever add a small, harmless amount of extra
        /// (later filtered-out) exploration budget, never leave a gap. Anything larger than
        /// this multiplier just wastes that budget on extra diamond cells that get filtered
        /// back out anyway (see that method's phase 2).
        /// </remarks>
        private const float ManhattanToEuclideanMultiplier = 1.41421356f;

        /// <summary>
        /// Breadth-first flood-fill starting at <paramref name="start"/>, confined to cells
        /// that belong to <paramref name="room"/>, and trimmed down to a roughly circular
        /// patch of the given real (Euclidean) <paramref name="radius"/> around
        /// <paramref name="start"/>. This is the SOLE zone-shape algorithm used by
        /// <see cref="GetBubbleZoneCells"/> -- both for a machine in a fully-roofed room AND
        /// for one that isn't (outdoors, under a partial roof, etc.). There is no separate
        /// "ignore walls" fallback any more; see the call site's comments for why an earlier
        /// raw-distance-circle fallback for the non-roofed case was itself a bug (it let
        /// bubbles appear inside a nearby building the machine was standing OUTSIDE of, since
        /// it never checked for walls at all).
        /// </summary>
        /// <remarks>
        /// A RimWorld <see cref="Room"/> can span an arbitrarily large, sprawling area if
        /// there's no wall or closed door between two structurally-distinct-looking chambers
        /// (e.g. several separate rooms joined by open doorways / missing walls all merge
        /// into ONE Room object). Taking every cell of such a room unconditionally (the
        /// original behaviour, before any fix) let bubbles spawn/float in a chamber many tiles
        /// and several doorways away from the machine -- a real floating bubble obviously has
        /// no way of drifting that far. This flood-fill bounds the zone to cells actually
        /// reachable within roughly <paramref name="radius"/> tiles of the machine, while
        /// always respecting the room's real wall layout.
        ///
        /// TWO PHASES:
        /// 1. EXPLORE -- cardinal-only (N/S/E/W, no diagonals; see below for why) BFS out to a
        ///    Manhattan-distance step cap of <c>ceil(radius * <see cref="ManhattanToEuclideanMultiplier"/>)</c>,
        ///    confined to cells belonging to <paramref name="room"/>. This guarantees every
        ///    cell within the desired real radius is reached, even though cardinal-only
        ///    movement makes a diagonal cell "cost" more steps than its real distance. This
        ///    phase does NOT filter by real distance while exploring/enqueueing -- a
        ///    legitimate in-radius cell that's only reachable via a detour around an odd room
        ///    shape (e.g. an L-shaped room, where the direct line to a nearby cell is blocked
        ///    by the room's own wall) must never be missed just because the detour path
        ///    briefly passes through cells that are technically outside the final circle.
        /// 2. FILTER -- only cells whose actual Euclidean distance from <paramref name="start"/>
        ///    is <c>&lt;= radius</c> are kept in the returned list. Without this second pass,
        ///    the raw phase-1 frontier is a DIAMOND (every cell reachable within N cardinal
        ///    steps, i.e. constant Manhattan distance) -- visibly non-circular in any open area
        ///    large enough to hit the step cap before hitting a wall (e.g. a machine placed
        ///    outdoors with nothing nearby). This filter trims that diamond down to the circle
        ///    players actually expect, while phase 1's larger Manhattan cap has already
        ///    guaranteed every legitimately-circular cell was reached and is available to keep.
        ///
        /// Cardinal-only (no diagonal) steps are used for phase 1 deliberately: a diagonal step
        /// between two room-member cells could still "corner-cut" past a wall corner that
        /// blocks true diagonal movement (the same flanking-corner rule vanilla pathfinding
        /// enforces) even though both cells are nominally in the same room. Restricting to
        /// cardinal steps sidesteps that entirely -- two cardinally-adjacent cells can only
        /// both be members of the same room if there is genuinely no wall between them. The
        /// zone's final CIRCULAR shape comes entirely from phase 2's distance filter, not from
        /// the traversal method itself, so we get both wall-safety AND a circular zone.
        ///
        /// Room membership is checked live via <see cref="GridsUtility.GetRoom(IntVec3, Map)"/>
        /// per candidate cell rather than materialising the room's full cell list into a
        /// separate collection up front -- for a huge merged room, that upfront copy would
        /// itself be an O(room size) cost every cache refresh regardless of how small the
        /// bounded flood-fill turns out to be.
        /// </remarks>
        private static List<IntVec3> FloodFillWithinRoom(IntVec3 start, Room room, Map map, float radius)
        {
            // Phase 1 result: every room-member cell reachable within the (deliberately
            // over-sized) Manhattan step cap -- a diamond shape, not yet a circle.
            List<IntVec3> allReachable = new List<IntVec3> { start };

            // Defensive: `room` should never be null for a spawned building's own position
            // (every buildable/standable tile -- including plain outdoors -- belongs to SOME
            // Room object; GetRoom only returns null for cells with no region at all, e.g.
            // solid rock), and the machine's own cell should always belong to its own room
            // (that's how `room` was obtained in the first place). But if either of those
            // ever disagrees, bail out to just the machine's own tile instead of an empty
            // zone. Checking `room == null` explicitly (rather than folding it into the
            // equality check below) matters: without it, `start.GetRoom(map) != room` would
            // read as `null != null` -- false -- and let the BFS below proceed treating
            // every OTHER null-room cell it meets (e.g. solid rock) as a "match" too, since
            // null would equal null there as well. That would let the fill leak outward
            // through unrelated no-region cells instead of stopping immediately.
            if (room == null || start.GetRoom(map) != room)
            {
                return allReachable;
            }

            HashSet<IntVec3> visited = new HashSet<IntVec3> { start };
            Queue<IntVec3> frontier = new Queue<IntVec3>();
            frontier.Enqueue(start);

            // See ManhattanToEuclideanMultiplier's remarks: this Manhattan-distance step cap
            // is intentionally larger than `radius` itself so the diagonal "corners" of the
            // desired circle are still fully explored despite cardinal-only movement.
            int explorationStepCap = Mathf.CeilToInt(radius * ManhattanToEuclideanMultiplier);

            int stepsTaken = 0;
            // Process the BFS in full "rings" (levels) so the step cap applies uniformly in
            // every direction, rather than depth-first threading further down one branch than
            // another before the cap is reached.
            while (frontier.Count > 0 && stepsTaken < explorationStepCap)
            {
                stepsTaken++;
                int cellsAtThisLevel = frontier.Count;
                for (int i = 0; i < cellsAtThisLevel; i++)
                {
                    IntVec3 cell = frontier.Dequeue();

                    // Four cardinal neighbours only -- see remarks above for why diagonals
                    // are deliberately excluded.
                    TryVisitRoomCell(new IntVec3(cell.x, 0, cell.z + 1), room, map, visited, frontier, allReachable);
                    TryVisitRoomCell(new IntVec3(cell.x, 0, cell.z - 1), room, map, visited, frontier, allReachable);
                    TryVisitRoomCell(new IntVec3(cell.x + 1, 0, cell.z), room, map, visited, frontier, allReachable);
                    TryVisitRoomCell(new IntVec3(cell.x - 1, 0, cell.z), room, map, visited, frontier, allReachable);
                }
            }

            // Phase 2: trim the diamond-shaped exploration result down to a real circle by
            // keeping only cells whose actual (squared) Euclidean distance from `start` is
            // within `radius`. LengthHorizontalSquared avoids a sqrt per cell -- comparing
            // squared distances against a squared radius is equivalent and cheaper.
            float radiusSquared = radius * radius;
            List<IntVec3> circularResult = new List<IntVec3>(allReachable.Count);
            for (int i = 0; i < allReachable.Count; i++)
            {
                IntVec3 cell = allReachable[i];
                if ((cell - start).LengthHorizontalSquared <= radiusSquared)
                {
                    circularResult.Add(cell);
                }
            }

            return circularResult;
        }

        /// <summary>
        /// Visits <paramref name="cell"/> during <see cref="FloodFillWithinRoom"/> if it's a
        /// member of <paramref name="room"/> that hasn't already been visited: marks it
        /// visited, adds it to the result list, and queues it so its own neighbours get
        /// explored on the next BFS level.
        /// </summary>
        private static void TryVisitRoomCell(IntVec3 cell, Room room, Map map, HashSet<IntVec3> visited, Queue<IntVec3> frontier, List<IntVec3> result)
        {
            if (visited.Contains(cell))
            {
                return;
            }
            // InBounds is checked implicitly by GetRoom (returns null for out-of-bounds
            // cells, which never equals a real Room reference), so no separate bounds check
            // is needed here.
            if (cell.GetRoom(map) != room)
            {
                return;
            }
            visited.Add(cell);
            frontier.Enqueue(cell);
            result.Add(cell);
        }

        /// <summary>
        /// Same zone cells as <see cref="GetBubbleZoneCells"/>, but as a HashSet for O(1)
        /// containment checks. Always calls <see cref="GetBubbleZoneCells"/> first so the
        /// cache-validity check (and rebuild, if needed) happens in exactly one place.
        /// </summary>
        private HashSet<IntVec3> GetBubbleZoneCellsSet()
        {
            GetBubbleZoneCells();
            return cachedZoneCellsSet;
        }

        /// <summary>
        /// Public O(1) "is this cell inside my current zone" check, without exposing the
        /// underlying HashSet reference to callers (keeps <see cref="cachedZoneCellsSet"/>
        /// safely private/immutable-from-outside). Used by <see cref="BubbleMoodUtility"/>
        /// to test a pawn's position against every Bubble Machine on the map.
        /// </summary>
        public bool ContainsCell(IntVec3 cell)
        {
            return GetBubbleZoneCellsSet().Contains(cell);
        }

        /// <summary>
        /// Attempts to pick a random cell within the CURRENT bubble zone that also
        /// satisfies <paramref name="validator"/> (e.g. standable, not forbidden).
        /// Used by <see cref="PlayDesireGiver_BubblePopping"/> / <see cref="JobDriver_ChildBubblePopping"/>
        /// so a child "running around popping bubbles" wanders the EXACT same area the
        /// bubbles themselves are confined to (an enclosed room's real shape, or the
        /// wall-bounded flood-fill patch in the open) — reusing the same zone-cell cache the
        /// bubbles use, rather than a separately-computed area that could
        /// drift out of sync with what's actually visible.
        /// </summary>
        /// <param name="validator">
        /// Extra per-cell check beyond zone membership (e.g. pathability/standability).
        /// Pass <c>null</c> to accept any zone cell.
        /// </param>
        /// <param name="cell">The chosen cell, or <see cref="IntVec3.Invalid"/> if none found.</param>
        /// <param name="maxAttempts">
        /// How many random candidates to try before giving up. Kept small since this is called
        /// fairly often (every time a wandering pawn arrives at its previous destination) —
        /// mirrors <see cref="Verse.CellFinder.TryFindRandomCellNear"/>'s own "random sample and
        /// retry, don't exhaustively search" approach.
        /// </param>
        public bool TryGetRandomZoneCell(Predicate<IntVec3> validator, out IntVec3 cell, int maxAttempts = 10)
        {
            List<IntVec3> cells = GetBubbleZoneCells();
            cell = IntVec3.Invalid;
            if (cells.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < maxAttempts; i++)
            {
                IntVec3 candidate = cells[Rand.Range(0, cells.Count)];
                if (validator == null || validator(candidate))
                {
                    cell = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Public read of whether the CURRENT zone is confined to a fully-roofed room (see
        /// <see cref="cachedIsFullyRoofed"/>). Always calls <see cref="GetBubbleZoneCells"/>
        /// first so the cache-validity check happens in exactly one place, same pattern as
        /// <see cref="GetBubbleZoneCellsSet"/>.
        /// </summary>
        public bool GetIsZoneEnclosed()
        {
            GetBubbleZoneCells();
            return cachedIsFullyRoofed;
        }

        /// <summary>
        /// Caps how many bubbles this machine may have alive at once: one quarter of its
        /// zone's cell count, hard-capped at 20 regardless of how big the zone is (so a huge
        /// open-air zone or a cathedral-sized room doesn't fill up with an absurd number of
        /// bubbles). Shared by <see cref="TrySpawnBubble"/> (the actual cap enforcement) and
        /// <see cref="CompInspectStringExtra"/> (the displayed "active / max") so the two can
        /// never disagree.
        /// </summary>
        private int GetMaxBubblesForMachine(List<IntVec3> zoneCells)
        {
            if (zoneCells.NullOrEmpty())
            {
                return 0;
            }

            return Mathf.Min(zoneCells.Count / 4, 20);
        }

        /// <summary>
        /// Counts how many active bubbles are spawned from this specific machine, using the
        /// real ownership list (<see cref="trackedBubbles"/>) rather than a map-wide proximity
        /// scan — see that field's remarks for why proximity alone isn't reliable ownership.
        /// </summary>
        private int CountActiveBubbles()
        {
            // Defensive prune: drop any entries that are no longer valid before counting.
            // Mote_Bubble.Destroy() already calls NotifyBubbleDestroyed() the moment a bubble
            // dies, and PostDeSpawn() above clears this list entirely if the machine itself
            // leaves the map, so in normal play this loop finds nothing to remove. It's a
            // safety net against any despawn path that bypasses those hooks (e.g. a bubble
            // being force-discarded rather than Destroy()ed).
            trackedBubbles.RemoveAll(bubble => bubble == null || bubble.Destroyed || !bubble.Spawned);
            return trackedBubbles.Count;
        }

        // Note: We don't save/load ticksUntilSpawn or trackedBubbles. The timer just resets on
        // map load, and bubbles are never persisted in the first place — MoteBase in vanilla
        // sets <isSaveable>false</isSaveable> (motes are pure-visual and "weirdly constructed",
        // per Ludeon's own comment on the Anomaly mote base), so there would be nothing valid
        // to reference back into on load even if we tried to Scribe this list.
    }

    /// <summary>
    /// Properties class for the BubbleZone comp.
    /// </summary>
    public class CompProperties_BubbleZone : CompProperties
    {
        public CompProperties_BubbleZone()
        {
            compClass = typeof(CompBubbleZone);
        }
    }
}
