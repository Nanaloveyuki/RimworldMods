using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Shared helpers used by all ToddlerPlayGiver subclasses:
    /// walking checks and safe-cell finding for play activities.
    /// </summary>
    public static class TPE_PlayUtility
    {
        /// <summary>
        /// Returns true if the pawn is currently scheduled to sleep.
        /// </summary>
        public static bool IsScheduledToSleep(Pawn pawn)
        {
            if (pawn?.timetable == null)
                return false;
            return pawn.timetable.CurrentAssignment == TimeAssignmentDefOf.Sleep;
        }

        /// <summary>Autonomous in-crib play uses the same schedule/rest policy as all play.</summary>
        public static bool CanStartInCribPlay(Pawn? pawn) => PlayScheduleUtility.CanPlayNow(pawn);

        public static bool CanWalk(Pawn pawn)
        {
            // Crawler = stage 0 of LearningToWalk = cannot walk
            return !ToddlerLearningUtility.IsCrawler(pawn);
        }

        /// <summary>
        /// Symmetric counterpart to <see cref="CanWalk"/> — returns true if the toddler
        /// is still at the crawling (pre-walking) stage of <c>LearningToWalk</c>. Used by
        /// Bubble Machine activities that are restricted to crawlers only (Popping Bubbles,
        /// Watching Bubbles) so the check reads symmetrically with <see cref="CanWalk"/>
        /// at call sites instead of reaching into <see cref="ToddlerLearningUtility"/> directly.
        /// </summary>
        public static bool IsCrawler(Pawn pawn)
        {
            return ToddlerLearningUtility.IsCrawler(pawn);
        }

        /// <summary>
        /// Maximum search distance for a usable Bubble Machine from a toddler's current
        /// position. Matches <see cref="PlayDesireGiver_BubblePopping.MaxMachineDistance"/>
        /// (the child equivalent) for consistency — a Bubble Machine's own zone already
        /// reaches out up to 15 tiles from itself, so a toddler noticeably farther away
        /// than that still has a reasonable machine to walk toward.
        /// </summary>
        const float MaxBubbleMachineDistance = 40f;

        /// <summary>
        /// Finds the nearest reachable, currently-ACTIVE (powered + switched on) Bubble
        /// Machine the pawn can walk to. Deliberately does NOT reserve the machine —
        /// several toddlers can share the same bubble zone at once, matching
        /// <see cref="PlayDesireGiver_BubblePopping"/>'s "no reservation needed" convention
        /// for open-space activities. Shared by every toddler bubble-themed play giver
        /// (Popping Bubbles, Chasing Bubbles) that needs to walk TO a machine first.
        /// </summary>
        public static Thing? FindNearestActiveBubbleMachine(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(TPE_ThingDefOf.TPE_BubbleMachine),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                MaxBubbleMachineDistance,
                t => IsUsableBubbleMachine(t, pawn));
        }

        /// <summary>
        /// A Bubble Machine is usable for a bubble-themed play activity if it's not
        /// forbidden and its <see cref="CompBubbleZone"/> reports
        /// <see cref="CompBubbleZone.IsActive"/> — the same "powered + switched on" gate
        /// that governs whether it's actually producing bubbles right now, keeping every
        /// bubble activity's availability consistent with what the player can see happening.
        /// </summary>
        public static bool IsUsableBubbleMachine(Thing t, Pawn pawn)
        {
            if (t.IsForbidden(pawn))
                return false;

            CompBubbleZone? comp = (t as ThingWithComps)?.GetComp<CompBubbleZone>();
            return comp != null && comp.IsActive;
        }

        /// <summary>
        /// Finds a valid nearby cell for a toddler play activity (Spinning, Jumping,
        /// Running, Dinosaur, MonsterHunt, etc.), bounded to <paramref name="radius"/>
        /// tiles of the toddler's CURRENT position.
        ///
        /// BUG HISTORY: the original implementation searched the toddler's entire
        /// <see cref="Room"/> via <c>room.Regions</c>, taking the first region whose
        /// <c>RCellFinder.TryFindRandomCellInRegionUnforbidden</c> call succeeded.
        /// <see cref="Room.Regions"/> simply concatenates every district's region list
        /// in storage order -- it has NO relationship to distance from the pawn. On an
        /// unenclosed/outdoor toddler (no walls, and critically no Allowed Area
        /// restriction to force the validator to reject far-away cells), "outdoors" is
        /// usually ONE giant Room spanning most or all of the map, so the "first
        /// successful region" could be hundreds of tiles away -- typically near the
        /// map's origin (bottom-left corner), which is where the observed "toddler
        /// instantly paths to the corner of the map" bug came from. An assigned
        /// Allowed Area happened to mask the bug because the validator would reject
        /// cells outside it, forcing the loop to keep searching regions until it
        /// finally hit one that actually overlapped the (typically small, nearby)
        /// area -- but with no area assigned, or an empty/unpainted one (which counts
        /// as "unrestricted" per <see cref="ForbidUtility.InAllowedArea"/>), literally
        /// any region cell would satisfy the validator, so whatever the first region in
        /// storage order happened to be won every time.
        ///
        /// FIX: use a radius-bounded search (<see cref="CellFinder.TryFindRandomCellNear"/>)
        /// centered on the toddler's actual position instead. This is the same approach
        /// vanilla wander jobs use and the same approach our own job drivers already use
        /// for their mid-job re-target logic (see <c>PickNewTargetCell</c>/<c>PickNewWanderCell</c>/
        /// <c>PickNewStompCell</c>/<c>PickNearbyCell</c> in JobDrivers.cs) -- it guarantees
        /// the result is within <paramref name="radius"/> tiles of the pawn regardless of
        /// room/region topology, and falls back to an exhaustive shuffled scan of that
        /// same bounded box before giving up, so it's no less thorough than the old code.
        /// </summary>
        public static IntVec3 FindCellForPlay(Pawn pawn, int radius = 8)
        {
            Map map = pawn.Map;
            System.Predicate<IntVec3> validator = c =>
                GenGrid.InBounds(c, map)
                && GenGrid.Standable(c, map)
                && !c.GetTerrain(map).avoidWander
                && GenTemperature.SafeTemperatureAtCell(pawn, c, map)
                && !ForbidUtility.IsForbidden(c, pawn)
                && c.InAllowedArea(pawn)
                && ReservationUtility.CanReserve(pawn, c, 1, -1, null, false);

            if (CellFinder.TryFindRandomCellNear(pawn.Position, map, radius, validator, out IntVec3 result))
                return result;

            return IntVec3.Invalid;
        }
    }

    // ===========================
    // SPINNING AROUND
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler spins around on the ground.
    /// Requires walking ability and a valid play cell.
    /// </summary>
    public class ToddlerPlayGiver_Spinning : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return null;
            IntVec3 cell = TPE_PlayUtility.FindCellForPlay(pawn, 8);
            if (cell == IntVec3.Invalid) return null;
            return JobMaker.MakeJob(def.jobDef, cell);
        }
    }

    // ===========================
    // WORK WATCHING
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler finds a nearby working colonist and watches them.
    /// Only triggers if a colonist within 30 tiles is performing a work job.
    /// </summary>
    public class ToddlerPlayGiver_WorkWatching : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn) && TryFindWorkingColonist(pawn) != null;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return null;
            Pawn worker = TryFindWorkingColonist(pawn);
            if (worker == null) return null;
            return JobMaker.MakeJob(def.jobDef, worker);
        }

        static Pawn TryFindWorkingColonist(Pawn toddler)
        {
            Map map = toddler.Map;
            float bestDist = 9999f;
            Pawn best = null;
            // Copy the list to avoid collection-modified exceptions.
            List<Pawn> freeColonists = new List<Pawn>(map.mapPawns.FreeColonistsSpawned);
            foreach (Pawn p in freeColonists)
            {
                if (p == toddler) continue;
                if (ToddlerUtility.IsToddler(p)) continue;
                if (p.CurJob == null) continue;
                if (!IsWorkJob(p)) continue;
                if (!p.Position.InAllowedArea(toddler)) continue;
                float dist = p.Position.DistanceTo(toddler.Position);
                if (dist < 30f && dist < bestDist)
                {
                    bestDist = dist;
                    best = p;
                }
            }
            return best;
        }

        static bool IsWorkJob(Pawn p)
        {
            Job job = p.CurJob;
            return job != null && (job.workGiverDef != null || job.jobGiver is JobGiver_Work);
        }
    }

    // ===========================
    // JUMPING UP AND DOWN
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler jumps up and down in place.
    /// Draw offset is handled by a Harmony postfix on <see cref="Pawn.DrawPos"/>.
    /// </summary>
    public class ToddlerPlayGiver_Jumping : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn) && TPE_PlayUtility.CanWalk(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return null;
            IntVec3 cell = TPE_PlayUtility.FindCellForPlay(pawn, 6);
            if (cell == IntVec3.Invalid) return null;
            return JobMaker.MakeJob(def.jobDef, cell);
        }
    }

    // ===========================
    // RUNNING AROUND
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler runs around with a temporary speed boost hediff.
    /// </summary>
    public class ToddlerPlayGiver_Running : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn) && TPE_PlayUtility.CanWalk(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return null;
            IntVec3 cell = TPE_PlayUtility.FindCellForPlay(pawn, 8);
            if (cell == IntVec3.Invalid) return null;
            return JobMaker.MakeJob(def.jobDef, cell);
        }
    }

    // ===========================
    // MONSTER HUNT
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler wanders and "hunts" imaginary monsters.
    /// Awards a mood thought on completion (+1 "Found a Monster!" or -1 "Didn't Find Any Monsters..").
    /// </summary>
    public class ToddlerPlayGiver_MonsterHunt : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn) && TPE_PlayUtility.CanWalk(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return null;
            IntVec3 cell = TPE_PlayUtility.FindCellForPlay(pawn, 10);
            if (cell == IntVec3.Invalid) return null;
            Job job = JobMaker.MakeJob(def.jobDef, cell);
            // If a toy sword is available, carry it while hunting
            Thing? sword = FindToySword(pawn);
            if (sword != null)
            {
                job.targetC = sword;
                job.count = 1;
            }
            return job;
        }

        static Thing? FindToySword(Pawn pawn)
        {
            if (pawn.carryTracker?.CarriedThing?.def == TPE_ThingDefOf.TPE_ToySword) return null;
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(TPE_ThingDefOf.TPE_ToySword),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                30f,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }
    }

    // ===========================
    // BEING A DINOSAUR
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler stomps around, roars, and investigates trees/rocks.
    /// Plays the custom <see cref="TPE_SoundDefOf.TPE_DinoRoar"/> sound effect.
    /// </summary>
    public class ToddlerPlayGiver_Dinosaur : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            /// Block play activities during sleep schedule time.
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn) && TPE_PlayUtility.CanWalk(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return null;
            IntVec3 cell = TPE_PlayUtility.FindCellForPlay(pawn, 8);
            if (cell == IntVec3.Invalid) return null;
            Job job = JobMaker.MakeJob(def.jobDef, cell);
            // If a dino plush is available, the job will pick it up and equip it
            Thing? dinoPlush = FindDinoPlush(pawn);
            if (dinoPlush != null)
            {
                job.targetC = dinoPlush;
                job.count = 1;
            }
            return job;
        }

        static Thing? FindDinoPlush(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel != null)
            {
                foreach (Apparel a in pawn.apparel.WornApparel)
                {
                    if (a.def == TPE_ThingDefOf.TPE_PlushDino) return null; // Already wearing one
                }
            }
            if (pawn.carryTracker?.CarriedThing?.def == TPE_ThingDefOf.TPE_PlushDino) return null; // Already carrying one
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(TPE_ThingDefOf.TPE_PlushDino),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                30f,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }
    }

    // ===========================
    // LOOKING THROUGH STORAGE
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler finds the nearest reachable storage building
    /// (vanilla <see cref="Building_Storage"/> or Adaptive Storage Framework) and
    /// looks through it. Available to crawlers and walkers alike.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="ToddlerPlayGiver_Bugwatching"/> in the base mod:
    ///     finds a target, creates a job, and lets the job driver handle animation.
    ///   - Differs in target type (storage building vs. outdoor cell) and does NOT
    ///     require walking ability (crawlers can reach storage too).
    /// </summary>
    public class ToddlerPlayGiver_LookThroughStorage : ToddlerPlayGiver
    {
        /// <summary>Maximum search distance for storage buildings, in tiles.</summary>
        const float MaxSearchDistance = 15f;

        /// <summary>
        /// This activity is for out-of-crib toddlers only.
        /// </summary>
        public override bool CanDoFromCrib(Pawn pawn) => false;

        /// <summary>
        /// Returns true if the toddler is eligible and a reachable storage building
        /// exists within <see cref="MaxSearchDistance"/>.
        /// </summary>
        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn) && FindNearbyStorage(pawn) != null;
        }

        /// <summary>
        /// Creates a job targeting the nearest valid storage building.
        /// Optionally includes a magnifying glass if one is available and reachable.
        /// </summary>
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!base.CanDo(pawn)) return null;
            Thing? storage = FindNearbyStorage(pawn);
            if (storage == null) return null;
            
            Job job = JobMaker.MakeJob(def.jobDef, storage);
            
            // If a magnifying glass is available, add it as TargetC
            Thing? glass = FindMagnifyingGlass(pawn);
            if (glass != null)
            {
                job.targetC = glass;
                job.count = 1;
            }
            
            return job;
        }

        /// <summary>
        /// Finds the nearest reachable magnifying glass within 30 tiles.
        /// Returns null if none are available or if the toddler already carries one.
        /// </summary>
        static Thing? FindMagnifyingGlass(Pawn pawn)
        {
            // If already carrying a magnifying glass, don't bother searching
            if (pawn.carryTracker?.CarriedThing?.def == TPE_ThingDefOf.TPE_MagnifyingGlass)
                return null;
            
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForDef(TPE_ThingDefOf.TPE_MagnifyingGlass),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                30f,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }

        /// <summary>
        /// Finds the closest reachable storage building within
        /// <see cref="MaxSearchDistance"/> tiles.
        /// </summary>
        static Thing? FindNearbyStorage(Pawn pawn)
        {
            Map map = pawn.Map;
            Thing? best = null;
            float bestDistSq = float.MaxValue;
            float maxDistSq = MaxSearchDistance * MaxSearchDistance;

            // Scan all colonist buildings — efficient cached list.
            foreach (Thing t in map.listerBuildings.allBuildingsColonist)
            {
                if (!IsValidStorage(t, pawn)) continue;

                float distSq = t.Position.DistanceToSquared(pawn.Position);
                if (distSq < bestDistSq && distSq <= maxDistSq)
                {
                    bestDistSq = distSq;
                    best = t;
                }
            }

            return best;
        }

        /// <summary>
        /// Validates whether a given thing counts as a storage container that a
        /// toddler can look through.
        ///
        /// Supported:
        ///   - Vanilla <see cref="Building_Storage"/> (shelves, stockpiles, etc.)
        ///   - Adaptive Storage Framework buildings (detected via reflection on
        ///     <c>CompStorage</c> so we don't create a hard assembly dependency)
        /// </summary>
        static bool IsValidStorage(Thing t, Pawn pawn)
        {
            if (t == null || t.IsBurning()) return false;
            if (ForbidUtility.IsForbidden(t, pawn)) return false;
            if (!pawn.CanReserve(t)) return false;
            if (!pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly)) return false;

            // Vanilla storage buildings (shelves, stockpile zones, etc.)
            if (t is Building_Storage)
                return true;

            // Adaptive Storage Framework compatibility — check for CompStorage
            // by type name so we don't need to reference the ASF assembly.
            if (t is ThingWithComps twc)
            {
                foreach (ThingComp comp in twc.GetComps<ThingComp>())
                {
                    string typeName = comp.GetType().Name;
                    if (typeName == "CompStorage" || typeName == "CompStore")
                        return true;
                }
            }

            return false;
        }
    }

    // ===========================
    // BUG WATCHING ENHANCEMENT
    // ===========================
    /// <summary>
    /// Enhancement patch for bug watching: adds magnifying glass support to the base
    /// Toddlers mod's ToddlerBugwatching activity via a postfix on JobGiver_ToddlerPlay.
    /// 
    /// Instead of replacing the activity entirely, we intercept the base mod's
    /// ToddlerBugwatching job and attach an optional magnifying glass as TargetC.
    /// The magnifying glass is purely cosmetic and optional — if unavailable, the
    /// job proceeds without it.
    /// </summary>
    // NOTE: The actual implementation is in HarmonyPatches.cs:
    // Patch_JobGiver_ToddlerPlay_TryGiveJob_BugWatchingMagnifyingGlass
    // This intercepts ToddlerBugwatching jobs and adds optional glass pickup.

    // ===========================
    // SELF SOOTHING
    // ===========================
    /// <summary>
    /// Autonomous play giver: upset toddler in a crib attempts to calm
    /// themselves down. Crib-only — both <see cref="CanDo"/> and
    /// <see cref="CanDoFromCrib"/> require the toddler to be physically
    /// inside a crib. Gated by the <c>enableSelfSoothing</c> mod setting.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="ToddlerPlayGiver"/> base behaviour:
    ///     checks toddler status, needs, and play level before offering.
    ///   - Differs in being crib-bound and having mood-weighted priority
    ///     (handled by a Harmony postfix on
    ///     <see cref="JobGiver_ToddlerPlay.TryGiveJob"/>).
    /// </summary>
    public class ToddlerPlayGiver_SelfSoothing : ToddlerPlayGiver
    {
        /// <summary>
        /// Tracks when each toddler's Self Soothing cooldown expires.
        /// Key = pawn.thingIDNumber, Value = tick at which the cooldown ends.
        /// The expiration tick is set once (end tick + random 1–2 hour duration)
        /// so the cooldown duration is fixed, not re-randomized on every check.
        /// </summary>
        static readonly Dictionary<int, int> selfSoothingCooldownExpireTick = new Dictionary<int, int>();

        /// <summary>
        /// Returns true if the toddler's personal cooldown has elapsed.
        /// If the toddler has never self-soothed before, this always returns true.
        /// </summary>
        static bool CooldownElapsed(Pawn pawn)
        {
            if (!selfSoothingCooldownExpireTick.TryGetValue(pawn.thingIDNumber, out int expireTick))
                return true; // Never soothed before — no cooldown.

            return Find.TickManager.TicksGame >= expireTick;
        }

        /// <summary>
        /// Records that the given toddler has just finished Self Soothing,
        /// starting their personal cooldown of a random 1–2 in-game hours
        /// (2500–5000 ticks). Called by <see cref="JobDriver_ToddlerSelfSoothing"/>
        /// when the job ends.
        /// </summary>
        public static void RecordSelfSoothingEnd(Pawn pawn)
        {
            if (pawn != null)
            {
                int duration = Rand.Range(2500, 5000); // 1–2 in-game hours
                selfSoothingCooldownExpireTick[pawn.thingIDNumber] = Find.TickManager.TicksGame + duration;
            }
        }

        public override bool CanDo(Pawn pawn)
        {
            if (!TPE_Mod.Settings.enableSelfSoothing)
                return false;
            if (!CooldownElapsed(pawn))
                return false;
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDo(pawn) && CribUtility.InCrib(pawn);
        }

        public override bool CanDoFromCrib(Pawn pawn)
        {
            if (!TPE_Mod.Settings.enableSelfSoothing)
                return false;
            if (!CooldownElapsed(pawn))
                return false;
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDoFromCrib(pawn) && CribUtility.InCrib(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            // Use this.CanDo (not base.CanDo) so the crib check, mod-setting
            // gate, cooldown, and play-need threshold are all enforced even if
            // a caller invokes TryGiveJob without checking CanDo first.
            if (!CanDo(pawn)) return null;

            // Pass the crib as TargetA so the job driver can register the
            // toddler as "in bed" and keep them lying down when the job ends.
            Building_Bed? crib = CribUtility.GetCurrentCrib(pawn);
            if (crib != null)
                return JobMaker.MakeJob(def.jobDef, crib);

            return null;
        }
    }

    // ===========================
    // CRIB JUMPING
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler bounces up and down while standing in
    /// their crib. Reuses the same vertical-bounce visual as
    /// <see cref="ToddlerPlayGiver_Jumping"/>.
    ///
    /// Requirements:
    ///   - Walker only: <see cref="TPE_PlayUtility.CanWalk"/> excludes crawlers.
    ///   - Crib only: <see cref="CanDo"/> always returns false;
    ///     <see cref="CanDoFromCrib"/> checks crib occupancy.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="ToddlerPlayGiver_Jumping"/> (same bounce
    ///     animation, walker-only).
    ///   - Similar to <see cref="ToddlerPlayGiver_SelfSoothing"/> (crib-only,
    ///     passes crib as TargetA, no mod-setting gate).
    ///   - Differs from both in being stationary — the toddler does not move
    ///     around, only bounces in place.
    /// </summary>
    public class ToddlerPlayGiver_CribJumping : ToddlerPlayGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            // Out-of-crib toddlers can never do this activity.
            return false;
        }

        public override bool CanDoFromCrib(Pawn pawn)
        {
            // Walker-only crib activity. Crawlers lack the leg strength
            // to bounce while standing. Not during sleep schedule.
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            return base.CanDoFromCrib(pawn)
                && CribUtility.InCrib(pawn)
                && TPE_PlayUtility.CanWalk(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            // Crib-only. Use CanDoFromCrib so the crib check, walker
            // check, and play-need threshold are all enforced.
            if (!CanDoFromCrib(pawn)) return null;

            // Pass the crib as TargetA so the job driver can reserve it
            // and keep the toddler in the crib when the job ends.
            Building_Bed? crib = CribUtility.GetCurrentCrib(pawn);
            if (crib != null)
                return JobMaker.MakeJob(def.jobDef, crib);

            return null;
        }
    }

    // ===========================
    // CEILING WATCHING
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler lies down and gazes up at the ceiling.
    /// The inverse of the base mod's Skydreaming — this requires a roof
    /// overhead instead of forbidding one.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="Toddlers.ToddlerPlayGiver_Skydreaming"/>:
    ///     both use <c>PawnPosture.LayingOnGroundFaceUp</c>, call
    ///     <c>ToddlerPlayTickCheckEnd</c>, and last 2000 ticks.
    ///   - Differs in gating on <c>Roofed</c> (true) instead of
    ///     <c>!Roofed</c> (false), and requiring an indoor room.
    ///   - Works both in-crib (if the crib is roofed) and out-of-crib
    ///     (any roofed indoor spot). The toddler never moves — they
    ///     ceiling-watch from their current position.
    /// </summary>
    public class ToddlerPlayGiver_CeilingWatching : ToddlerPlayGiver
    {
        /// <summary>
        /// Out-of-crib eligibility. Toddler must be under a roof, indoors,
        /// at a safe temperature, and their current position must not be
        /// forbidden to them.
        /// </summary>
        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            if (!base.CanDo(pawn)) return false;

            Map map = pawn.Map;
            IntVec3 pos = pawn.Position;

            // Must be under a roof.
            if (!pos.Roofed(map)) return false;

            // Must be indoors (not psychologically outdoors).
            Room room = pos.GetRoom(map);
            if (room == null || room.PsychologicallyOutdoors) return false;

            // Must be at a safe temperature.
            if (!GenTemperature.SafeTemperatureAtCell(pawn, pos, map)) return false;

            // Current cell must not be forbidden.
            if (ForbidUtility.IsForbidden(pos, pawn)) return false;

            return true;
        }

        /// <summary>
        /// In-crib eligibility. The crib must be roofed and at a safe
        /// temperature; all other checks (play need, not downed, etc.) are
        /// handled by <see cref="ToddlerPlayGiver.CanDoFromCrib"/>.
        /// </summary>
        public override bool CanDoFromCrib(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            if (!base.CanDoFromCrib(pawn)) return false;
            Map map = pawn.Map;
            IntVec3 pos = pawn.Position;
            if (!pos.Roofed(map)) return false;
            if (!GenTemperature.SafeTemperatureAtCell(pawn, pos, map)) return false;
            return true;
        }

        /// <summary>
        /// Creates the job at the toddler's current position. No movement
        /// is required — the toddler simply lays down where they are and
        /// looks up at the ceiling.
        /// </summary>
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!CanDo(pawn)) return null;
            return JobMaker.MakeJob(def.jobDef, pawn.Position);
        }

        /// <summary>
        /// Creates the job from a crib. If the toddler is in a bed, the bed
        /// is passed as <c>TargetA</c> so <see cref="JobInBedUtility"/> can
        /// keep them lying down. Otherwise the current cell is used.
        /// </summary>
        public Job TryGiveJobFromCrib(Pawn pawn)
        {
            if (!pawn.Position.Roofed(pawn.Map)) return null;

            if (RestUtility.InBed(pawn))
                return JobMaker.MakeJob(def.jobDef, RestUtility.CurrentBed(pawn));

            return JobMaker.MakeJob(def.jobDef, pawn.Position);
        }
    }

    // ===========================
    // PILLOW NESTING
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler nestles into their crib pillows and
    /// blankets. Crib-only — <see cref="CanDo"/> always returns false,
    /// while <see cref="CanDoFromCrib"/> returns true for any toddler
    /// physically inside a crib.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="ToddlerPlayGiver_SelfSoothing"/>:
    ///     both are crib-only activities that pass the crib as TargetA.
    ///   - Differs in having no mod-setting gate (standard play expansion),
    ///     no mood-weighted priority, and being available to all toddlers
    ///     in cribs regardless of mood.
    /// </summary>
    // PILLOW NESTING PLAY GIVER — SHELVED FOR LATER
    // Entire ToddlerPlayGiver_PillowNesting class commented out due to animation caching issues.
    // To restore: uncomment the class below.
    /*
    public class ToddlerPlayGiver_PillowNesting : ToddlerPlayGiver
    {
        public override bool CanDo(Pawn pawn)
        {
            // Out-of-crib toddlers can never do this activity.
            return false;
        }

        public override bool CanDoFromCrib(Pawn pawn)
        {
            // Any toddler physically inside a crib qualifies.
            return base.CanDoFromCrib(pawn) && CribUtility.InCrib(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            // Pillow Nesting is crib-only. Use CanDoFromCrib so the crib
            // check and play-need threshold are both enforced, even when
            // called from the in-crib Harmony postfix.
            if (!CanDoFromCrib(pawn)) return null;

            // Pass the crib as TargetA so the job driver can keep the
            // toddler lying down when the job ends.
            Building_Bed? crib = CribUtility.GetCurrentCrib(pawn);
            if (crib != null)
                return JobMaker.MakeJob(def.jobDef, crib);

            return null;
        }
    }
    */

    // ===========================
    // SPEAKING PRACTICE
    // ===========================
    /// <summary>
    /// Autonomous play giver: toddler practices talking by babbling random
    /// syllables. Works both out-of-crib (wandering with pauses) and in-crib
    /// (stationary). Both play defs share this single worker class; the
    /// <see cref="Def.defName"/> determines which mode is being requested.
    ///
    /// Out-of-crib:
    ///   - Available to all toddlers (crawlers and walkers).
    ///   - Toddler moves to a nearby cell, stands/crawls there for 3–7
    ///     seconds, then moves to another cell.
    ///   - Continuous babble audio plays throughout the entire activity.
    ///
    /// In-crib:
    ///   - Available to all toddlers currently in a crib.
    ///   - Toddler stays lying down and babbles.
    ///   - Continuous babble audio plays throughout.
    ///
    /// Vanilla comparison:
    ///   - Out-of-crib similar to <see cref="ToddlerPlayGiver_Running"/>:
    ///     both wander to nearby cells, use <c>FindCellForPlay</c>, and
    ///     have a max duration. Differs in having stand-still pauses and
    ///     continuous audio.
    ///   - In-crib similar to <see cref="ToddlerPlayGiver_SelfSoothing"/>:
    ///     both are stationary crib activities that pass the crib as TargetA
    ///     and use <c>KeepLyingDown</c>. Differs in having no mood system,
    ///     no cooldown, and no sub-activities.
    /// </summary>
    public class ToddlerPlayGiver_SpeechPractice : ToddlerPlayGiver
    {
        /// <summary>
        /// Out-of-crib eligibility. Returns false for toddlers currently in
        /// a crib (they should use the in-crib version instead). Also blocks
        /// during sleep schedule time.
        /// </summary>
        public override bool CanDo(Pawn pawn)
        {
            /// Block play activities during sleep schedule time.
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            // The in-crib play def handles crib toddlers.
            if (CribUtility.InCrib(pawn))
                return false;
            return base.CanDo(pawn);
        }

        /// <summary>
        /// In-crib eligibility. Returns true for any toddler physically
        /// inside a crib (unless during sleep schedule). Also blocks during
        /// sleep schedule time.
        /// </summary>
        public override bool CanDoFromCrib(Pawn pawn)
        {
            /// Block play activities during sleep schedule time.
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            // Only the in-crib play def should match this.
            if (def.defName == "TPE_ToddlerSpeechPractice")
                return false;
            return base.CanDoFromCrib(pawn) && CribUtility.InCrib(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            // The in-crib play def always targets the current crib.
            if (def.defName == "TPE_ToddlerSpeechPracticeInCrib")
            {
                if (!CanDoFromCrib(pawn))
                    return null;

                Building_Bed? crib = CribUtility.GetCurrentCrib(pawn);
                if (crib != null)
                    return JobMaker.MakeJob(def.jobDef, crib);

                return null;
            }

            // Out-of-crib play def: find a nearby cell and walk there.
            if (!CanDo(pawn))
                return null;

            IntVec3 cell = TPE_PlayUtility.FindCellForPlay(pawn, 6);
            if (cell == IntVec3.Invalid)
                return null;

            return JobMaker.MakeJob(def.jobDef, cell);
        }
    }

    // ===========================
    // HEATER SNUGGLING
    // ===========================
    /// <summary>
    /// Autonomous play giver: when outdoor temperature drops below a toddler's
    /// comfortable minimum, the toddler seeks an indoor heater and idles next
    /// to it for warmth. If a plush toy is available, the toddler will pick it
    /// up and carry it to the heater, dropping it when the activity ends.
    ///
    /// Temperature threshold:
    ///   - Uses the base Toddlers mod's configurable comfortable temperature
    ///     settings (<see cref="Toddlers_Settings.minComfortableTemperature_Toddler"/>
    ///     and <see cref="Toddlers_Settings.minComfortableTemperature_Baby"/>).
    ///   - Checks map outdoor temperature; if it is below the threshold, the
    ///     activity becomes eligible.
    ///
    /// Heater selection:
    ///   - Finds buildings with <see cref="CompHeatPusher"/> that are currently
    ///     pushing heat (<c>ShouldPushHeatNow</c>).
    ///   - Excludes anything in the base mod's Fire Gazing whitelist
    ///     (<see cref="Toddlers_DefOf.FiregazingTargets"/>) — this blacklists
    ///     campfires, braziers, and other fire-based sources.
    ///   - Optionally includes geothermal generators and steam geysers when the
    ///     <see cref="TPE_Mod.Settings.allowGeothermalSnuggling"/> setting is ON.
    ///   - Heater must be indoors (roofed) and reachable.
    ///
    /// Plush toy:
    ///   - Searches for the nearest reachable plush toy within 15 tiles.
    ///   - If found, the toy is passed as <c>TargetB</c> so the
    ///     <see cref="JobDriver_ToddlerHeaterSnuggling"/> can pick it up.
    ///   - The toddler drops the toy when snuggling ends (handled by job driver).
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="ToddlerPlayGiver_Firegazing"/> (base mod): both
    ///     find a heat source, an adjacent spot, and create a job with two targets.
    ///   - Differs in using the fire-gazing whitelist as a blacklist, requiring
    ///     cold outdoor temperature, and optionally carrying a plush toy.
    /// </summary>
    public class ToddlerPlayGiver_HeaterSnuggling : ToddlerPlayGiver
    {
        const float MaxHeaterDistance = 15.9f;
        const float MaxToyDistance = 15.9f;

        /// <summary>
        /// Lazy-initialised reference to the base mod's fire-gazing whitelist,
        /// used here as a blacklist for non-fire heaters.
        /// </summary>
        static List<ThingDef>? FireBlacklist;

        /// <summary>
        /// The outdoor temperature must be at least this many degrees below the
        /// toddler's comfy minimum before snuggling is considered. A small
        /// buffer prevents the activity from flickering on/off at the threshold.
        /// </summary>
        const float TempThresholdBuffer = 2f;

        public override bool CanDo(Pawn pawn)
        {
            /// Block play activities during sleep schedule time.
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
                
            if (!base.CanDo(pawn))
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] CanDo FAILED: base.CanDo false for {pawn.NameShortColored}");
                return false;
            }

            // Feature disabled in mod settings.
            if (TPE_Mod.Settings?.enableHeaterSnuggling != true)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] CanDo FAILED: enableHeaterSnuggling=false (Settings={TPE_Mod.Settings != null}) for {pawn.NameShortColored}");
                return false;
            }

            // Must be cold outside for this toddler.
            if (!IsColdForToddler(pawn))
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                {
                    float outdoorTemp = pawn.Map?.mapTemperature.OutdoorTemp ?? -999f;
                    float comfyMin = GetComfyMinTemp(pawn);
                    Log.Message($"[TPE_HEATER] CanDo FAILED: not cold enough. Outdoor={outdoorTemp:F1}°C, ComfyMin={comfyMin:F1}°C, NeedBelow={comfyMin - TempThresholdBuffer:F1}°C for {pawn.NameShortColored}");
                }
                return false;
            }

            // Must find a valid indoor heater.
            Thing? heater = FindNearbyHeater(pawn);
            if (heater == null)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] CanDo FAILED: no valid heater found within {MaxHeaterDistance} tiles for {pawn.NameShortColored}");
                return false;
            }

            // Must find a valid snuggling spot next to the heater.
            IntVec3 spot = FindSnugglingSpot(pawn, heater);
            if (spot == IntVec3.Invalid)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] CanDo FAILED: no valid snuggling spot next to {heater.LabelShort} for {pawn.NameShortColored}");
                return false;
            }

            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE_HEATER] CanDo PASSED for {pawn.NameShortColored}. Heater={heater.LabelShort}, Spot={spot}");
            return true;
        }

        public override bool CanDoFromCrib(Pawn pawn)
        {
            return false;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            Thing? heater = FindNearbyHeater(pawn);
            if (heater == null)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] TryGiveJob RETURNED NULL: no heater for {pawn.NameShortColored}");
                return null;
            }

            IntVec3 spot = FindSnugglingSpot(pawn, heater);
            if (spot == IntVec3.Invalid)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] TryGiveJob RETURNED NULL: no snuggling spot for {pawn.NameShortColored}");
                return null;
            }

            // Optional: find a plush toy to carry.  This is purely cosmetic —
            // the activity works fine without one.
            Thing? toy = null;
            try
            {
                toy = FindNearbyPlushToy(pawn);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[TPE_HEATER] FindNearbyPlushToy threw for {pawn.NameShortColored}: {ex}");
            }

            Job job = JobMaker.MakeJob(def.jobDef, heater, spot);
            job.count = 1;
            if (toy != null)
                job.SetTarget(TargetIndex.C, toy);

            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE_HEATER] TryGiveJob RETURNED JOB for {pawn.NameShortColored}: heater={heater.LabelShort}, spot={spot}, hasToy={toy != null}");
            return job;
        }

        // ── Temperature ──

        /// <summary>
        /// Returns true when the outdoor temperature on the toddler's map is
        /// below their race's comfortable minimum (with a 2° buffer).
        /// </summary>
        bool IsColdForToddler(Pawn pawn)
        {
            Map? map = pawn.Map;
            if (map == null)
                return false;

            float outdoorTemp = map.mapTemperature.OutdoorTemp;
            float comfyMin = GetComfyMinTemp(pawn);
            return outdoorTemp < comfyMin - TempThresholdBuffer;
        }

        /// <summary>
        /// Reads the base Toddlers mod's configurable comfortable temperature.
        /// Babies use <c>minComfortableTemperature_Baby</c>; toddlers use
        /// <c>minComfortableTemperature_Toddler</c>.
        /// </summary>
        float GetComfyMinTemp(Pawn pawn)
        {
            // Babies (0–1) use the baby threshold; toddlers (1–3) use toddler threshold.
            // We detect babies by life stage — simpler than calling CanSuckle and avoids
            // referencing the BreastfeedFailReason type which is internal to the base mod.
            if (pawn.ageTracker?.CurLifeStage == LifeStageDefOf.HumanlikeBaby)
                return Toddlers_Settings.minComfortableTemperature_Baby;

            return Toddlers_Settings.minComfortableTemperature_Toddler;
        }

        // ── Heater search ──

        /// <summary>
        /// Finds the nearest valid indoor heater that is currently producing heat.
        /// </summary>
        Thing? FindNearbyHeater(Pawn pawn)
        {
            Map map = pawn.Map;
            Room? room = RegionAndRoomQuery.GetRoom(pawn, RegionType.Set_Passable);

            InitBlacklistIfNeeded();

            // Search the toddler's current room first.
            // We track the closest valid heater rather than returning the
            // first one, because ContainedAndAdjacentThings iteration order
            // is undefined and we don't want toddlers walking to a far
            // heater when a closer one is available.
            if (room != null)
            {
                Thing? bestRoomHeater = null;
                float bestRoomDistSq = float.MaxValue;
                foreach (Thing thing in room.ContainedAndAdjacentThings)
                {
                    if (thing.def.category != ThingCategory.Building || !IsValidHeater(thing, pawn))
                        continue;
                    float distSq = thing.Position.DistanceToSquared(pawn.Position);
                    if (distSq < bestRoomDistSq)
                    {
                        bestRoomDistSq = distSq;
                        bestRoomHeater = thing;
                    }
                }
                if (bestRoomHeater != null)
                    return bestRoomHeater;
            }

            // Fallback: closest reachable heater anywhere on the map.
            return GenClosest.ClosestThingReachable(
                pawn.Position, map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some, TraverseMode.ByPawn, false, false, false, true),
                MaxHeaterDistance,
                t => IsValidHeater(t, pawn),
                lookInHaulSources: false);
        }

        /// <summary>
        /// A valid heater for snuggling:
        /// 1. Has <see cref="CompHeatPusher"/> and is currently pushing heat.
        /// 2. Is NOT in the fire-gazing blacklist (no campfires, braziers, etc.).
        /// 3. Is indoors (roofed).
        /// 4. Is reachable, not forbidden, and within range.
        /// 5. If it's a geothermal source, only allowed when the setting is ON.
        /// </summary>
        bool IsValidHeater(Thing heater, Pawn pawn)
        {
            if (heater == null || heater.IsBurning())
                return false;
            if (ForbidUtility.IsForbidden(heater, pawn))
                return false;
            if (heater.Map == null)
                return false;
            if (!ReachabilityUtility.CanReach(pawn, heater, PathEndMode.Touch, Danger.Some, false, false, TraverseMode.ByPawn))
                return false;
            if (heater.Position.DistanceTo(pawn.Position) >= MaxHeaterDistance)
                return false;

            // Must be indoors.
            if (!heater.Position.Roofed(heater.Map))
                return false;

            // Must be a heat-producing building.
            // We accept two categories:
            //   1) Buildings with CompHeatPusher (campfires, braziers, etc.).
            //   2) Buildings with CompTempControl and positive energyPerSecond
            //      (vanilla electric Heater, modded heaters).  Coolers have
            //      negative energyPerSecond and are excluded.
            // We intentionally do NOT require ShouldPushHeatNow == true,
            // because a heater that has cycled off when the room is warm
            // is still a valid snuggling target for a cold toddler.
            bool hasHeatPusher = ThingCompUtility.TryGetComp<CompHeatPusher>(heater) != null;
            bool hasTempControl = false;
            CompTempControl? tempControl = ThingCompUtility.TryGetComp<CompTempControl>(heater);
            if (tempControl != null && tempControl.Props.energyPerSecond > 0f)
                hasTempControl = true;

            if (!hasHeatPusher && !hasTempControl)
                return false;

            // Blacklist: anything the base mod allows for fire gazing is NOT
            // a safe heater for toddlers to snuggle with.
            if (FireBlacklist != null && FireBlacklist.Contains(heater.def))
                return false;

            // Geothermal sources require the mod setting.
            bool isGeothermal = heater.def == ThingDefOf.GeothermalGenerator
                             || heater.def == ThingDefOf.SteamGeyser;
            if (isGeothermal && TPE_Mod.Settings?.allowGeothermalSnuggling != true)
                return false;

            return true;
        }

        // ── Snuggling spot ──

        /// <summary>
        /// Finds an adjacent cell next to the heater that the toddler can stand
        /// on and reserve. Tries all 8 surrounding cells in random order.
        /// </summary>
        IntVec3 FindSnugglingSpot(Pawn pawn, Thing heater)
        {
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(heater).InRandomOrder())
            {
                if (!GenGrid.Standable(cell, heater.Map))
                    continue;
                if (ForbidUtility.IsForbidden(cell, pawn))
                    continue;
                if (!ReservationUtility.CanReserveAndReach(pawn, cell, PathEndMode.OnCell, Danger.Some, 1, -1, null, false))
                    continue;
                return cell;
            }
            return IntVec3.Invalid;
        }

        // ── Plush toy search ──

        /// <summary>
        /// Finds the nearest reachable plush toy within range. Returns null if
        /// none are available — the activity works fine without a toy.
        /// </summary>
        Thing? FindNearbyPlushToy(Pawn pawn)
        {
            Map map = pawn.Map;
            Thing? best = null;
            float bestDist = float.MaxValue;

            foreach (ThingDef toyDef in PlushDefs)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(toyDef))
                {
                    if (!IsValidToy(thing, pawn))
                        continue;
                    float dist = thing.Position.DistanceToSquared(pawn.Position);
                    if (dist < bestDist && dist <= MaxToyDistance * MaxToyDistance)
                    {
                        bestDist = dist;
                        best = thing;
                    }
                }
            }
            return best;
        }

        bool IsValidToy(Thing toy, Pawn pawn)
        {
            if (toy == null || toy.IsBurning())
                return false;
            if (ForbidUtility.IsForbidden(toy, pawn))
                return false;
            if (!ReservationUtility.CanReserveAndReach(pawn, toy, PathEndMode.ClosestTouch, Danger.Some, 1, -1, null, false))
                return false;
            return true;
        }

        void InitBlacklistIfNeeded()
        {
            if (FireBlacklist != null)
                return;

            // The base mod stores its fire-gazing whitelist in a DefListDef.
            // We use that same list as our blacklist.
            FireBlacklist = Toddlers_DefOf.FiregazingTargets?.whitelist;
            if (FireBlacklist == null)
                FireBlacklist = new List<ThingDef>(); // empty = nothing blacklisted
        }

        static readonly List<ThingDef> PlushDefs = new List<ThingDef>
        {
            TPE_ThingDefOf.TPE_PlushTeddy,
            TPE_ThingDefOf.TPE_PlushMuffalo,
            TPE_ThingDefOf.TPE_PlushSheep,
            TPE_ThingDefOf.TPE_PlushDino,
            TPE_ThingDefOf.TPE_PlushMonster,
            TPE_ThingDefOf.TPE_PlushFox
        };
    }

    // ===========================
    // POPPING BUBBLES
    // ===========================
    /// <summary>
    /// Autonomous play giver: crawler-only toddler wanders inside an active Bubble
    /// Machine's zone, pausing occasionally to "pop" a bubble. Out-of-crib only.
    ///
    /// Movement is copied from the child "Bubble Popping" play desire (see
    /// <see cref="PlayDesireGiver_BubblePopping"/> / <see cref="JobDriver_ChildBubblePopping"/>) —
    /// deliberately does NOT track or chase individual bubble motes, only wanders to
    /// random cells within the machine's zone and pauses there.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="ToddlerPlayGiver_LookThroughStorage"/>: both target a
    ///     specific Thing (a storage building / a Bubble Machine) rather than a bare cell,
    ///     and both are available to crawlers.
    ///   - Differs from Chasing Bubbles (walker-only, Running Around's movement) by using
    ///     the child Bubble Popping job's zone-confined wander-and-pause movement instead.
    /// </summary>
    public class ToddlerPlayGiver_PoppingBubbles : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            // Crawler-only — walkers get the more energetic Chasing Bubbles instead.
            if (!TPE_PlayUtility.IsCrawler(pawn))
                return false;
            if (!base.CanDo(pawn))
                return false;
            // Most expensive check last: scans the map for a reachable active machine.
            return TPE_PlayUtility.FindNearestActiveBubbleMachine(pawn) != null;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            // Re-verify the crawler restriction here too (not just in CanDo) so this
            // activity can never be handed to a walking toddler regardless of caller
            // (e.g. the "Force play activity" debug tool already checks CanDo first,
            // but this guards any future caller that might not).
            if (!TPE_PlayUtility.IsCrawler(pawn))
                return null;
            if (!base.CanDo(pawn))
                return null;

            Thing? machine = TPE_PlayUtility.FindNearestActiveBubbleMachine(pawn);
            if (machine == null)
                return null;

            return JobMaker.MakeJob(def.jobDef, machine);
        }
    }

    // ===========================
    // CHASING BUBBLES
    // ===========================
    /// <summary>
    /// Autonomous play giver: walker-only toddler runs over to an active Bubble
    /// Machine and then runs around near it with a temporary speed boost hediff.
    /// Out-of-crib only.
    ///
    /// Movement (once running) is copied verbatim from
    /// <see cref="ToddlerPlayGiver_Running"/> / <see cref="JobDriver_ToddlerRunning"/> —
    /// the toddler picks random nearby cells and refreshes its target periodically,
    /// exactly like ordinary Running Around. The only difference is the toddler walks
    /// to a reachable, currently-active Bubble Machine first (so the "chase" visibly
    /// starts near real bubbles), and the job ends early if that machine goes inactive.
    /// Deliberately does NOT chase or target individual bubble motes.
    /// </summary>
    public class ToddlerPlayGiver_ChasingBubbles : ToddlerPlayGiver
    {
        public override bool CanDoFromCrib(Pawn pawn) => false;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            if (!TPE_PlayUtility.CanWalk(pawn))
                return false;
            if (!base.CanDo(pawn))
                return false;
            return TPE_PlayUtility.FindNearestActiveBubbleMachine(pawn) != null;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            if (!TPE_PlayUtility.CanWalk(pawn))
                return null;
            if (!base.CanDo(pawn))
                return null;

            Thing? machine = TPE_PlayUtility.FindNearestActiveBubbleMachine(pawn);
            if (machine == null)
                return null;

            return JobMaker.MakeJob(def.jobDef, machine);
        }
    }
}
