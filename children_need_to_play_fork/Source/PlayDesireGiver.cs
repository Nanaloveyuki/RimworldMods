using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Base class for all play desire givers. Each subclass implements
    /// <see cref="CanDo(Pawn)"/> and <see cref="TryGiveJob(Pawn)"/> to provide
    /// a specific play activity (e.g. play with toys, read books, watch TV).
    ///
    /// Mirrors <see cref="LearningGiver"/> from vanilla Biotech.
    /// </summary>
    public abstract class PlayDesireGiver
    {
        /// <summary>The def that configured this giver.</summary>
        public PlayDesireDef def;

        /// <summary>
        /// Returns true if this desire type is allowed to be assigned to
        /// <paramref name="pawn"/> AT ALL — i.e. whether it should even be in the
        /// pool of candidates when a new desire is rolled (see
        /// <see cref="Pawn_PlayTracker.AddNewPlayDesire"/>) or tried as an
        /// idle-fallback (see <c>JobGiver_ChildPlayWhenIdle.TryFromDesireList</c>).
        /// Override to disable desires globally (e.g. missing DLC/research) OR
        /// colony-wide (e.g. no TV/comms console/Bubble Machine has ever been
        /// built) — see <see cref="ColonyHasBuilding(ThingDef)"/>.
        ///
        /// This is DELIBERATELY separate from <see cref="CanDo(Pawn)"/>: CanDo
        /// answers "can this pawn do this RIGHT NOW" (distance, reservation,
        /// power state, reachability — all of which fluctuate tick to tick) and
        /// is checked every time a job is actually requested. CanGiveDesire
        /// answers the coarser, much more stable question "does this desire even
        /// make sense to hold as one of the pawn's 2 precious active-desire slots
        /// for the next 8 hours" — gating it here means a colony that has simply
        /// never built a TV never wastes a child's desire slot on "Watch
        /// Television", which could otherwise never be satisfied until one
        /// eventually gets built.
        /// </summary>
        public virtual bool CanGiveDesire(Pawn pawn) => true;

        /// <summary>
        /// Returns true if any map has at least one PLAYER-OWNED (colonist
        /// faction) building of def <paramref name="requiredDef"/>. Used by
        /// <see cref="CanGiveDesire"/> overrides that require a specific
        /// player-built structure (a TV, a comms console, a Bubble Machine) to
        /// exist before the desire is ever offered.
        ///
        /// Mirrors vanilla's own colony-wide "has the required building been
        /// built anywhere" checks — e.g. <c>Alert_NeedResearchBench</c> and
        /// <c>PassingShip</c>'s comms-console check both loop every
        /// <see cref="Find.Maps"/> and test <c>ListerBuildings.ColonistsHaveBuilding</c>
        /// rather than restricting to the pawn's current map or player-home maps
        /// only — so a desire correctly becomes available the moment ANY of the
        /// colony's maps has the building, matching how multi-map colonies
        /// (e.g. a second base, an off-map caravan's home colony) already work
        /// for every other "do we have X" check in the base game.
        ///
        /// Deliberately checks EXISTENCE only, not power/on state — a
        /// temporarily unpowered building (blown fuse, brownout, deliberately
        /// switched off) shouldn't yank an already-rolled desire out from under
        /// a child for the rest of its 8-hour window. <see cref="CanDo(Pawn)"/>
        /// still separately requires the building to be powered/active before
        /// actually USING it, so a temporarily-unpowered building just means
        /// "can't play with it RIGHT NOW" — exactly like any other transient
        /// unavailability (e.g. someone else currently using the only toy) —
        /// rather than stripping the desire outright.
        /// </summary>
        protected static bool ColonyHasBuilding(ThingDef requiredDef)
        {
            if (requiredDef == null)
                return false;

            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i].listerBuildings.ColonistsHaveBuilding(requiredDef))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Overload of <see cref="ColonyHasBuilding(ThingDef)"/> for desires
        /// satisfied by ANY of several interchangeable building defs (e.g. the
        /// three television tiers) — true if the colony has built at least one
        /// building matching any def in <paramref name="requiredDefs"/>, on any
        /// map.
        /// </summary>
        protected static bool ColonyHasBuilding(List<ThingDef> requiredDefs)
        {
            if (requiredDefs.NullOrEmpty())
                return false;

            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                ListerBuildings lister = maps[i].listerBuildings;
                for (int j = 0; j < requiredDefs.Count; j++)
                {
                    if (lister.ColonistsHaveBuilding(requiredDefs[j]))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the given child can perform this play activity right now.
        /// Checks age bracket, physical capability, and runtime availability
        /// (e.g. presence of toys, books, or a powered TV).
        /// </summary>
        public virtual bool CanDo(Pawn pawn)
        {
            // Must be a child (age 3–13)
            if (!pawn.DevelopmentalStage.Child())
                return false;

            // Must be alive and mobile
            if (pawn.Dead || pawn.Downed)
                return false;

            // Don't give play jobs when basic needs are urgent
            if (PawnUtility.WillSoonHaveBasicNeed(pawn, 0.1f))
                return false;

            // Age bracket check (skipped when age gating is disabled)
            if (TPE_Mod.Settings.ageGatedPlayDesires)
            {
                int age = pawn.ageTracker.AgeBiologicalYears;
                if (def.minAge >= 0 && age < def.minAge)
                    return false;
                if (def.maxAge >= 0 && age > def.maxAge)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to create a Job for this play activity. Returns <c>null</c>
        /// if no valid target is available (e.g. no toys nearby).
        /// </summary>
        public abstract Job TryGiveJob(Pawn pawn);
    }

    // ==========================================================
    // Concrete desire givers
    // ==========================================================

    /// <summary>
    /// Play desire giver: child walks to a nearby toy and plays with it.
    /// Works with any toy defined in <see cref="TPE_ToyUtility.IsAnyToyThing"/>.
    /// Only available to age group 1 (3–6).
    /// </summary>
    public class PlayDesireGiver_PlayWithToy : PlayDesireGiver
    {
        const float MaxToyDistance = 25f;

        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            return FindNearestToy(pawn) != null;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            Thing toy = FindNearestToy(pawn);
            if (toy == null)
                return null;

            Job job = JobMaker.MakeJob(def.jobDef, toy);
            job.count = 1;
            return job;
        }

        /// <summary>
        /// Finds the nearest reachable <b>plush toy</b> (category "Plush toys")
        /// that the child can reserve.  Deliberately excludes the toy sword
        /// and any other non-plush toys so the desire matches its label.
        /// </summary>
        static Thing FindNearestToy(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                MaxToyDistance,
                t => TPE_ToyUtility.IsPlushCategory(t) && !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }
    }

    /// <summary>
    /// Play desire giver: child reads a book. Only available to age groups
    /// 2 and 3 (7–13) and when a readable book is available.
    /// </summary>
    public class PlayDesireGiver_ReadBook : PlayDesireGiver
    {
        // ------------------------------------------------------------------
        // One-shot CanDo → TryGiveJob hand-off cache (performance + correctness).
        //
        // Both child job givers (JobGiver_ChildPlay, JobGiver_ChildPlayWhenIdle)
        // always call CanDo(pawn) and then, if true, TryGiveJob(pawn) back-to-back
        // on the same tick. Vanilla BookUtility.TryGetRandomBookToRead walks EVERY
        // book on the map plus every bookcase's contents, with a reservation +
        // reachability check per book, and allocates via LINQ — doing that twice
        // per job request was pure waste. It also rolled a DIFFERENT random book
        // in TryGiveJob than the one CanDo had just approved.
        //
        // CanDo stores the book it found; TryGiveJob consumes it only if it's for
        // the SAME pawn on the SAME tick (otherwise falls back to a fresh scan),
        // and always clears it, so a stale entry can never be reused. Statics are
        // safe: givers are per-def singletons and the job-giving code runs on the
        // main thread only.
        // ------------------------------------------------------------------
        static Pawn? cachedPawn;
        static int cachedTick = -1;
        static Book? cachedBook;

        public override bool CanDo(Pawn pawn)
        {
            ClearCache(); // never let an older result survive a fresh check
            if (!base.CanDo(pawn))
                return false;
            if (!BookUtility.CanReadNow(pawn))
                return false;

            Book? book = FindBook(pawn);
            if (book == null)
                return false;

            cachedPawn = pawn;
            cachedTick = Find.TickManager.TicksGame;
            cachedBook = book;
            return true;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            // Reuse CanDo's pick when valid; otherwise (e.g. a caller that skips
            // CanDo) do the normal scan.
            Book? book = TakeCachedBook(pawn) ?? FindBook(pawn);
            if (book == null)
                return null;

            Job job = JobMaker.MakeJob(def.jobDef, book);
            job.count = 1;
            return job;
        }

        /// <summary>
        /// Returns the book cached by <see cref="CanDo"/> if it was for this exact
        /// pawn on this exact tick and the book still exists; always clears the
        /// cache (one-shot).
        /// </summary>
        static Book? TakeCachedBook(Pawn pawn)
        {
            Book? book = null;
            if (cachedPawn == pawn
                && cachedTick == Find.TickManager.TicksGame
                && cachedBook != null
                && !cachedBook.Destroyed)
            {
                book = cachedBook;
            }
            ClearCache();
            return book;
        }

        /// <summary>Drops all cached references (also avoids pinning a Pawn/Book in memory).</summary>
        static void ClearCache()
        {
            cachedPawn = null;
            cachedTick = -1;
            cachedBook = null;
        }

        /// <summary>
        /// Finds a readable book on the child's map — loose/stockpiled books AND
        /// books stored inside bookcases (vanilla <see cref="BookUtility.TryGetRandomBookToRead"/>
        /// returns both; <see cref="JobDriver_ChildReadBook"/> handles both).
        /// </summary>
        static Book? FindBook(Pawn pawn)
        {
            // Defensive: vanilla TryGetRandomBookToRead dereferences pawn.Map
            // unconditionally. Think-tree callers are always spawned, but a
            // debug/other caller might not be — one bool check, zero cost.
            if (!pawn.Spawned)
                return null;
            if (!BookUtility.TryGetRandomBookToRead(pawn, out Book book))
                return null;
            return book;
        }
    }

    /// <summary>
    /// Play desire giver: child draws on the floor. Available to age groups
    /// 1 and 2 (3–9).
    /// </summary>
    /// <summary>
    /// Play desire giver: child draws on the floor with chalk.
    /// Uses vanilla floor-drawing spot finding (two adjacent cells).
    /// </summary>
    public class PlayDesireGiver_FloorDrawing : PlayDesireGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            IntVec3 drawFrom;
            IntVec3 drawOn;
            return LearningGiver_Floordrawing.TryFindFloordrawingSpots(pawn, out drawFrom, out drawOn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 drawFrom;
            IntVec3 drawOn;
            if (!LearningGiver_Floordrawing.TryFindFloordrawingSpots(pawn, out drawFrom, out drawOn))
                return null;

            return JobMaker.MakeJob(def.jobDef, drawFrom, drawOn);
        }
    }

    /// <summary>
    /// Play desire giver: child watches television. Available to all age groups
    /// when a powered TV is reachable.
    /// </summary>
    public class PlayDesireGiver_WatchTelevision : PlayDesireGiver
    {
        const float MaxDistance = 25f;

        /// <summary>
        /// Gate desire SELECTION on the colony having built at least one TV of
        /// any tier, ANYWHERE — not just reachable/powered right now (that's
        /// what <see cref="CanDo(Pawn)"/> already checks every time). Without
        /// this, a colony that has never built a TV could still roll "Watch
        /// Television" as one of a child's 2 active desires, permanently
        /// wasting that slot for a full 8-hour window since the desire could
        /// never once be satisfied.
        /// </summary>
        public override bool CanGiveDesire(Pawn pawn) => ColonyHasBuilding(TVDefs);

        /// <summary>Cached TV thing defs to avoid repeated database lookups.</summary>
        static readonly List<ThingDef> TVDefs = new List<ThingDef>();

        static PlayDesireGiver_WatchTelevision()
        {
            // Core television defs — safe to cache because defs never change at runtime.
            AddTVDef("TubeTelevision");
            AddTVDef("FlatscreenTelevision");
            AddTVDef("MegascreenTelevision");
        }

        static void AddTVDef(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null)
                TVDefs.Add(def);
        }

        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            return FindNearestPoweredTV(pawn) != null;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            Thing tv = FindNearestPoweredTV(pawn);
            if (tv == null)
                return null;

            if (!WatchBuildingUtility.TryFindBestWatchCell(tv, pawn, desireSit: false, out IntVec3 watchCell, out Building chair))
                return null;

            Job job = JobMaker.MakeJob(def.jobDef, tv, watchCell);
            if (chair != null)
                job.targetC = chair;
            job.count = 1;
            return job;
        }

        /// <summary>
        /// Finds the nearest powered, reachable TV the child can reserve.
        /// </summary>
        static Thing FindNearestPoweredTV(Pawn pawn)
        {
            if (TVDefs.Count == 0)
                return null;

            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                MaxDistance,
                t => IsPoweredTV(t) && !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }

        /// <summary>
        /// Returns true if the thing is a television and its power trader is on.
        /// </summary>
        static bool IsPoweredTV(Thing t)
        {
            if (t == null)
                return false;

            bool isTV = false;
            for (int i = 0; i < TVDefs.Count; i++)
            {
                if (t.def == TVDefs[i])
                {
                    isTV = true;
                    break;
                }
            }
            if (!isTV)
                return false;

            CompPowerTrader power = t.TryGetComp<CompPowerTrader>();
            return power != null && power.PowerOn;
        }
    }

    /// <summary>
    /// Play desire giver: child finds an outdoor dirt/stone spot and builds a dirt castle.
    /// Available to age groups 1 and 2 (3–9). Requires an outdoor cell on natural terrain.
    /// </summary>
    public class PlayDesireGiver_BuildDirtCastles : PlayDesireGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            return FindOutdoorDirtCell(pawn).IsValid;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 cell = FindOutdoorDirtCell(pawn);
            if (!cell.IsValid)
                return null;

            Job job = JobMaker.MakeJob(def.jobDef, cell);
            job.expiryInterval = 8000;
            return job;
        }

        /// <summary>
        /// Finds a reachable outdoor cell on natural terrain (dirt, stone, gravel, sand).
        /// Prefers cells within 20 tiles; expands to 60 if desperate.
        /// When the "show dirt castles" setting is ON, only cells where the finished
        /// castle won't override a building or a crop are considered (see
        /// <see cref="DirtCastleUtility"/>) so the child doesn't spend 3 hours
        /// building at a spot where the castle would then be refused.
        /// </summary>
        static IntVec3 FindOutdoorDirtCell(Pawn pawn)
        {
            IntVec3 result = IntVec3.Invalid;

            // Read the setting ONCE per search (not per candidate cell). If castles
            // are OFF nothing is ever spawned, so there is nothing to override and
            // the "no building / crop here" rule is skipped — children keep the full
            // pool of play spots exactly as before.
            bool protectBuildingsAndCrops = DirtCastleUtility.CastlesEnabled;

            // Try close first
            if (TryFindCell(pawn, 20, protectBuildingsAndCrops, out result))
                return result;

            // Expand search if desperate
            if (TryFindCell(pawn, 60, protectBuildingsAndCrops, out result))
                return result;

            return IntVec3.Invalid;
        }

        static bool TryFindCell(Pawn pawn, int radius, bool protectBuildingsAndCrops, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            return CellFinder.TryFindRandomCellNear(
                pawn.Position, pawn.Map, radius,
                c => IsValidDirtCastleCell(c, pawn, protectBuildingsAndCrops),
                out cell);
        }

        static bool IsValidDirtCastleCell(IntVec3 c, Pawn pawn, bool protectBuildingsAndCrops)
        {
            Map map = pawn.Map;

            // Cheap, allocation-free grid checks FIRST (bounds, roof, standable,
            // natural terrain — shared with the spawn-time guard), so the
            // expensive reservation/reachability query below only runs on cells
            // that already passed everything else. (The old order ran
            // CanReserveAndReach before the terrain checks.)
            if (!DirtCastleUtility.IsNaturalOutdoorCell(c, map))
                return false;

            // Castles must not override buildings or crops: skip cells holding a
            // building/frame/other castle, a colony-sown plant, or growing-zone land.
            // (Wild grass, items, filth etc. are fine — user-confirmed.)
            if (protectBuildingsAndCrops && !DirtCastleUtility.IsCellFreeOfBuildingsAndCrops(c, map))
                return false;

            if (c.IsForbidden(pawn))
                return false;
            if (!pawn.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Deadly))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Play desire giver: child runs around in nature, exploring outdoor spots.
    /// Available to age groups 1 and 2 (3–9).
    /// </summary>
    public class PlayDesireGiver_NatureRunning : PlayDesireGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            return NatureRunningUtility.TryFindNatureInterestTarget(pawn, out _);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!NatureRunningUtility.TryFindNatureInterestTarget(pawn, out LocalTargetInfo target))
                return null;

            Job job = JobMaker.MakeJob(def.jobDef, target);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }
    }

    /// <summary>
    /// Play desire giver: child finds an unroofed outdoor spot and watches the sky.
    /// Available to all age groups (3–13).
    /// </summary>
    public class PlayDesireGiver_Skydreaming : PlayDesireGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            return RCellFinder.TryFindAllowedUnroofedSpotOutsideColony(pawn.Position, pawn, out _);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!RCellFinder.TryFindAllowedUnroofedSpotOutsideColony(pawn.Position, pawn, out IntVec3 spot))
                return null;

            return JobMaker.MakeJob(def.jobDef, spot);
        }
    }

    /// <summary>
    /// Play desire giver: child engages in active play — running, spinning,
    /// jumping, snail crawling, and people chasing. Cycles through these
    /// sub-activities randomly within a single play session.
    /// Available to all age groups (3–13) and requires only open space.
    /// </summary>
    public class PlayDesireGiver_ActivePlay : PlayDesireGiver
    {
        /// <summary>
        /// Maximum distance to search for a valid starting cell.
        /// </summary>
        const int StartCellSearchRadius = 5;

        public override Job TryGiveJob(Pawn pawn)
        {
            IntVec3 startCell = FindStartCell(pawn);
            if (!startCell.IsValid)
                return null;

            Job job = JobMaker.MakeJob(def.jobDef, startCell);
            return job;
        }

        /// <summary>
        /// Finds a good starting cell for active play.
        /// Prefers the pawn's current cell if it's already suitable.
        /// </summary>
        static IntVec3 FindStartCell(Pawn pawn)
        {
            if (IsValidPlayCell(pawn.Position, pawn))
                return pawn.Position;

            if (CellFinder.TryFindRandomCellNear(
                pawn.Position, pawn.Map, StartCellSearchRadius,
                c => IsValidPlayCell(c, pawn), out IntVec3 result))
            {
                return result;
            }

            return pawn.Position;
        }

        /// <summary>
        /// Returns true if the cell is suitable for active play:
        /// in-bounds, standable, not avoid-wander terrain, and not forbidden.
        /// </summary>
        static bool IsValidPlayCell(IntVec3 c, Pawn pawn)
        {
            Map map = pawn.Map;
            if (!c.InBounds(map))
                return false;
            if (!GenGrid.Standable(c, map))
                return false;
            if (c.GetTerrain(map).avoidWander)
                return false;
            if (ForbidUtility.IsForbidden(c, pawn))
                return false;
            return true;
        }
    }

    /// <summary>
    /// Play desire giver: child runs around inside an active Bubble Machine's zone,
    /// pausing occasionally to "pop" a bubble. Available to age groups 1 and 2 (3–9)
    /// and requires a reachable, currently-active (powered + switched on) Bubble Machine
    /// nearby — an idle/unpowered machine isn't producing any bubbles to pop, so it
    /// doesn't count as available (mirrors <see cref="PlayDesireGiver_WatchTelevision"/>'s
    /// "must be powered on" requirement for its TV).
    /// </summary>
    public class PlayDesireGiver_BubblePopping : PlayDesireGiver
    {
        /// <summary>
        /// Maximum search distance for a usable Bubble Machine. Generous compared to
        /// e.g. <see cref="PlayDesireGiver_WatchTelevision"/>'s 25f since a Bubble Machine's
        /// own zone already reaches out up to 15 tiles from itself, so a child noticeably
        /// farther away than that still has a reasonable machine to walk toward.
        /// </summary>
        const float MaxMachineDistance = 40f;

        /// <summary>
        /// Gate desire SELECTION on the colony having built at least one Bubble
        /// Machine, ANYWHERE — not just an active/reachable one right now
        /// (that's what <see cref="CanDo(Pawn)"/> already checks every time).
        /// Without this, a colony that has never built one could still roll
        /// "Bubble Popping" as an active desire that can never once be
        /// satisfied, wasting the slot for a full 8-hour window.
        /// </summary>
        public override bool CanGiveDesire(Pawn pawn) => ColonyHasBuilding(TPE_ThingDefOf.TPE_BubbleMachine);

        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            return FindNearestActiveBubbleMachine(pawn) != null;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            Thing machine = FindNearestActiveBubbleMachine(pawn);
            if (machine == null)
                return null;

            return JobMaker.MakeJob(def.jobDef, machine);
        }

        /// <summary>
        /// Finds the nearest reachable, currently-active Bubble Machine the child can
        /// walk to. Deliberately does NOT reserve the machine — unlike a TV (limited
        /// seats) or a toy (single item), several children can all run around the same
        /// bubble zone at once without conflict, matching <see cref="PlayDesireGiver_ActivePlay"/>'s
        /// "no reservation needed" convention for open-space activities.
        /// </summary>
        static Thing FindNearestActiveBubbleMachine(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(TPE_ThingDefOf.TPE_BubbleMachine),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                MaxMachineDistance,
                t => IsUsableBubbleMachine(t, pawn));
        }

        /// <summary>
        /// A Bubble Machine is usable for this desire if it's not forbidden and its
        /// <see cref="CompBubbleZone"/> reports <see cref="CompBubbleZone.IsActive"/> —
        /// the exact same "powered + switched on" gate that governs whether it's actually
        /// spawning bubbles right now, keeping this desire's availability consistent with
        /// what the player can see happening.
        /// </summary>
        static bool IsUsableBubbleMachine(Thing t, Pawn pawn)
        {
            if (t.IsForbidden(pawn))
                return false;

            CompBubbleZone comp = (t as ThingWithComps)?.GetComp<CompBubbleZone>();
            return comp != null && comp.IsActive;
        }
    }

    /// <summary>
    /// Play desire giver: child chats over a powered comms console.
    /// Available to age groups 2 and 3 (7–13). Requires Microelectronics research.
    /// </summary>
    public class PlayDesireGiver_Radiotalking : PlayDesireGiver
    {
        /// <summary>
        /// Gate desire SELECTION on BOTH prerequisites: the research must be
        /// finished AND the colony must have actually built a comms console
        /// somewhere. Research alone used to be the only gate here, which meant
        /// a colony that had researched Microelectronics but not yet built a
        /// comms console could still roll "Radio Talking" as an active desire —
        /// permanently wasting one of the child's 2 desire slots until a console
        /// eventually got built.
        /// </summary>
        public override bool CanGiveDesire(Pawn pawn)
            => ResearchProjectDefOf.MicroelectronicsBasics.IsFinished
            && ColonyHasBuilding(ThingDefOf.CommsConsole);

        public override bool CanDo(Pawn pawn)
        {
            if (!base.CanDo(pawn))
                return false;
            return TryFindCommsConsole(pawn, out _);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!TryFindCommsConsole(pawn, out Thing console))
                return null;

            return JobMaker.MakeJob(def.jobDef, console);
        }

        static bool TryFindCommsConsole(Pawn pawn, out Thing commsConsole)
        {
            commsConsole = GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(ThingDefOf.CommsConsole),
                PathEndMode.InteractionCell,
                TraverseParms.For(pawn), 9999f,
                t => t is Building_CommsConsole building
                     && building.CanUseCommsNow
                     && pawn.CanReserve(building)
                     && !building.IsForbidden(pawn));
            return commsConsole != null;
        }
    }
}
