using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using Toddlers;

namespace ToddlersPlayExpanded
{
    // ===========================
    // SPINNING AROUND
    // ===========================
    /// <summary>
    /// Toddler lies down and spins 360° continuously. Uses <see cref="AnimationWorker_ToddlerSpinning"/>
    /// for the rotation effect. Protected from locomotion animation override.
    ///
    /// With "Disable toddler play animations" ON (<see cref="PlayAnimationUtility.ToddlerAnimationsDisabled"/>)
    /// the toddler simply lies still on the spot for the whole session (user-confirmed):
    /// no spin animation AND no mid-session relocation — relocating means sliding across
    /// the floor in a lying posture, which only reads as "spinning around the room" while
    /// the spin is visible. Play gain and duration are unchanged.
    /// </summary>
    public class JobDriver_ToddlerSpinning : JobDriver
    {
        int moveTicks;
        const int MoveInterval = 600; // pick a new spot every ~10 sec
        const int MaxDuration = 1800; // 30 seconds max

        public override string GetReport()
        {
            return "Spinning around";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);
            moveTicks = MoveInterval;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil spinToil = ToilMaker.MakeToil("Spinning");
            spinToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.LayingOnGroundNormal;
                // Starts the spin only if toddler play animations are allowed.
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, TPE_AnimationDefOf.TPE_ToddlerSpinning);
            };
            spinToil.tickAction = delegate
            {
                // Per-tick keep/stop: no-op unless the setting was flipped mid-spin, in
                // which case the spin is removed (or restored) on this tick.
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, TPE_AnimationDefOf.TPE_ToddlerSpinning);

                // Relocating only happens while the spin is visible — with animations
                // off the toddler lies still instead of sliding to new spots.
                if (PlayAnimationUtility.ToddlerAnimationsDisabled)
                {
                    // Setting flipped while a relocation slide was under way: stop it
                    // so the toddler really does lie still. Only ever true for that one
                    // tick — the spin toil starts after GotoCell has arrived, and no new
                    // path is started while disabled.
                    if (pawn.pather.Moving)
                        pawn.pather.StopDead();
                }
                else
                {
                    moveTicks--;
                    if (moveTicks <= 0)
                    {
                        moveTicks = MoveInterval;
                        IntVec3 newCell = PickNearbyCell(5);
                        if (newCell.IsValid && newCell != pawn.Position)
                            pawn.pather.StartPath(newCell, PathEndMode.OnCell);
                    }
                }

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            spinToil.defaultCompleteMode = ToilCompleteMode.Delay;
            spinToil.defaultDuration = MaxDuration;
            spinToil.AddFinishAction(delegate
            {
                // Only clear OUR spin — never the base mod's crawl/wobble locomotion
                // animation (which is what's playing when animations are disabled).
                PlayAnimationUtility.ClearIfCurrent(pawn, TPE_AnimationDefOf.TPE_ToddlerSpinning);
            });
            yield return spinToil;
        }

        /// <summary>
        /// Picks a random nearby standable, unforbidden, in-allowed-area cell for the
        /// toddler to re-locate to mid-spin. Bounded to <paramref name="radius"/> tiles
        /// of the toddler's current position (see <see cref="TPE_PlayUtility.FindCellForPlay"/>
        /// for why bounded, position-relative search matters here — an unbounded or
        /// room-region-based search can hand back a cell far away, e.g. near the map's
        /// origin corner, when the toddler has no confining Allowed Area).
        /// </summary>
        IntVec3 PickNearbyCell(int radius)
        {
            Map map = pawn.Map;
            if (CellFinder.TryFindRandomCellNear(pawn.Position, map, radius,
                c => GenGrid.Standable(c, map) && !c.GetTerrain(map).avoidWander && !ForbidUtility.IsForbidden(c, pawn) && c.InAllowedArea(pawn), out IntVec3 result))
                return result;
            return IntVec3.Invalid;
        }
    }

    // ===========================
    // WORK WATCHING
    // ===========================
    /// <summary>
    /// Toddler follows a working colonist, faces them, and dynamically reports
    /// "Watching [Name] do [work]". Re-evaluates worker position every 2 seconds.
    /// </summary>
    public class JobDriver_ToddlerWorkWatching : JobDriver
    {
        Pawn Worker => (Pawn)TargetA.Thing;
        int followTicks;
        const int FollowInterval = 120; // re-follow every 2 sec
        const int MaxDuration = 1800;

        public override string GetReport()
        {
            Pawn worker = Worker;
            if (worker != null && worker.CurJob != null)
            {
                string doing = worker.CurJob.GetReport(worker) ?? "work";
                return string.Format("Watching {0} do {1}", worker.NameShortColored, doing);
            }
            return base.GetReport();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);
            ToilFailConditions.FailOnDespawnedOrNull(this, TargetIndex.A);

            // Go near worker
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil watchToil = ToilMaker.MakeToil("WorkWatching");
            watchToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
                followTicks = FollowInterval;
            };
            watchToil.handlingFacing = true;
            watchToil.tickAction = delegate
            {
                if (Worker == null || Worker.CurJob == null)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                pawn.rotationTracker.FaceCell(Worker.Position);

                followTicks--;
                if (followTicks <= 0)
                {
                    followTicks = FollowInterval;
                    // Move closer if worker moved away
                    if (Worker.Position.DistanceTo(pawn.Position) > 3f)
                    {
                        // Also require the re-follow cell to be within the toddler's
                        // Allowed Area — otherwise a distant worker could pull the
                        // toddler outside their assigned zone mid-job.
                        //
                        // NOTE: CellFinder.RandomClosewalkCellNear falls back to
                        // returning `root` (Worker.Position here) UNCHANGED if no
                        // cell satisfies the validator -- it does NOT return
                        // IntVec3.Invalid on failure like TryFindRandomCellNear's
                        // "discard and return Invalid" callers do elsewhere in this
                        // file. That means the validator's InAllowedArea check alone
                        // can't stop us from pathing to Worker.Position when the
                        // worker has since walked outside the toddler's area, so we
                        // explicitly re-check InAllowedArea on the result before
                        // starting the path.
                        IntVec3 nearWorker = CellFinder.RandomClosewalkCellNear(Worker.Position, pawn.Map, 2,
                            c => GenGrid.Standable(c, pawn.Map) && !ForbidUtility.IsForbidden(c, pawn) && c.InAllowedArea(pawn));
                        if (nearWorker.IsValid && nearWorker != pawn.Position && nearWorker.InAllowedArea(pawn))
                            pawn.pather.StartPath(nearWorker, PathEndMode.OnCell);
                    }
                }

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            watchToil.AddFailCondition(() => Worker == null || Worker.CurJob == null);
            watchToil.defaultCompleteMode = ToilCompleteMode.Delay;
            watchToil.defaultDuration = MaxDuration;
            yield return watchToil;
        }
    }

    // ===========================
    // JUMPING UP AND DOWN
    // ===========================
    /// <summary>
    /// Toddler hops vertically with a small pause between each hop.
    /// The visual bounce is injected via a Harmony postfix on
    /// <see cref="PawnRenderer.GetBodyPos"/> using discrete hop cycles
    /// (30 ticks up+down, 15 ticks landed) with the Z axis.
    /// </summary>
    public class JobDriver_ToddlerJumping : JobDriver
    {
        int moveTicks;
        const int MoveInterval = 300; // move every ~5 sec
        const int MaxDuration = 1800;

        public override string GetReport()
        {
            return "Jumping up and down";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);
            moveTicks = MoveInterval;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil jumpToil = ToilMaker.MakeToil("Jumping");
            jumpToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
            };
            jumpToil.handlingFacing = true;
            jumpToil.tickAction = delegate
            {
                // Face movement direction
                if (pawn.pather.Moving)
                {
                    pawn.rotationTracker.FaceCell(pawn.pather.Destination.Cell);
                }

                moveTicks--;
                if (moveTicks <= 0)
                {
                    moveTicks = MoveInterval;
                    IntVec3 newCell = PickNearbyCell(4);
                    if (newCell.IsValid && newCell != pawn.Position)
                        pawn.pather.StartPath(newCell, PathEndMode.OnCell);
                }

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            jumpToil.defaultCompleteMode = ToilCompleteMode.Delay;
            jumpToil.defaultDuration = MaxDuration;
            yield return jumpToil;
        }

        /// <summary>
        /// Picks a random nearby standable, unforbidden, in-allowed-area cell for the
        /// toddler to hop to next. Bounded to <paramref name="radius"/> tiles of the
        /// toddler's current position — see <see cref="TPE_PlayUtility.FindCellForPlay"/>
        /// doc comment for why bounded, position-relative search is required.
        /// </summary>
        IntVec3 PickNearbyCell(int radius)
        {
            Map map = pawn.Map;
            if (CellFinder.TryFindRandomCellNear(pawn.Position, map, radius,
                c => GenGrid.Standable(c, map) && !c.GetTerrain(map).avoidWander && !ForbidUtility.IsForbidden(c, pawn) && c.InAllowedArea(pawn), out IntVec3 result))
                return result;
            return IntVec3.Invalid;
        }
    }

    // ===========================
    // LOOKING THROUGH STORAGE
    // ===========================
    /// <summary>
    /// Toddler crawls or walks to a nearby storage building, pauses, and plays
    /// the Bugwatch animation while facing the storage. The animation tilts the
    /// toddler's body toward the container, giving the visual impression of
    /// peering inside. Compatible with vanilla <see cref="Building_Storage"/> and
    /// Adaptive Storage Framework (detected via reflection on <c>CompStorage</c>).
    ///
    /// Vanilla comparison:
    ///   - Mirrors <see cref="JobDriver_ToddlerBugwatching"/> from the base
    ///     Toddlers mod: Goto → pause → Bugwatch animation → play tick check.
    ///   - Differs in target selection (storage building vs. outdoor cell) and
    ///     facing behavior (faces the storage vs. random East/West).
    /// </summary>
    /// <summary>
    /// Toddler walks to a nearby storage building and looks through it using the Bugwatch
    /// animation. Optionally carries a magnifying glass to enhance the observation experience.
    ///
    /// Job targets:
    ///   - TargetA = the storage building to look through.
    ///   - TargetC = optional magnifying glass to carry.
    ///
    /// Vanilla comparison:
    ///   - Mirrors base Toddlers mod's <see cref="JobDriver_ToddlerBugwatching"/> pattern:
    ///     goto target, play animation, stay for duration, clean up animation.
    ///   - Differs in optional toy pickup (magnifying glass) and being storage-specific
    ///     rather than a single outdoor cell.
    /// </summary>
    public class JobDriver_ToddlerLookThroughStorage : JobDriver
    {
        /// <summary>Minimum pause duration at the storage, in ticks (~3 sec).</summary>
        const int MinLookTicks = 180;

        /// <summary>Maximum pause duration at the storage, in ticks (~5 sec).</summary>
        const int MaxLookTicks = 300;

        /// <summary>
        /// Optional magnifying glass being carried during the observation.
        /// Tracked so we can drop and unforbid it regardless of job outcome.
        /// </summary>
        Thing? trackedGlass;

        /// <summary>
        /// The magnifying glass this job is targeting, if any.
        /// </summary>
        Thing? Glass => job.GetTarget(TargetIndex.C).Thing;

        /// <summary>
        /// True if this job has a magnifying glass target.
        /// </summary>
        bool HasGlass => Glass != null;

        public override string GetReport()
        {
            if (trackedGlass != null)
                return "Looking through storage with " + trackedGlass.LabelShort;
            return "Looking through storage: " + TargetA.Thing.LabelShort;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Reserve the storage building
            bool storageReserved = ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
            if (!storageReserved)
                return false;

            // Optionally reserve the magnifying glass. If reservation fails, clear the target
            // and continue without the glass — it's purely cosmetic.
            if (HasGlass)
            {
                bool glassReserved = ReservationUtility.Reserve(pawn, TargetC, job, 1, -1, null, errorOnFailed, false);
                if (!glassReserved)
                    job.SetTarget(TargetIndex.C, LocalTargetInfo.Invalid);
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Job-level safety net: drop the magnifying glass whenever the job ends,
            // even if it never reached the look toil (e.g. interrupted while walking).
            AddFinishAction((JobCondition _) => DropAndUnforbidTrackedGlass());

            ToilFailConditions.FailOnChildLearningConditions(this);
            ToilFailConditions.FailOnDespawnedOrNull(this, TargetIndex.A);

            // ── Step 1: Pick up the magnifying glass if available ──────────
            if (HasGlass)
            {
                this.FailOnDestroyedOrNull(TargetIndex.C);
                ToilFailConditions.FailOnForbidden(this, TargetIndex.C);
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.C, putRemainderInQueue: false, subtractNumTakenFromJobCount: true, failIfStackCountLessThanJobCount: false, reserve: false);
                trackedGlass = Glass;
            }

            // ── Step 2: Path to the storage building ───────────────────────
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // ── Step 3: Pause at storage and play Bugwatch animation ───────
            Toil lookToil = ToilMaker.MakeToil("LookThroughStorage");
            lookToil.initAction = delegate
            {
                // Face the storage container so the tilt animation points toward it.
                // (Facing is kept even with animations disabled — it isn't an animation.)
                pawn.rotationTracker.FaceCell(TargetA.Thing.Position);

                // Base-mod Bugwatch tilt — only if toddler play animations are allowed.
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, PlayAnimationUtility.BugwatchAnimation);
            };
            lookToil.handlingFacing = true; // Prevent locomotion from rotating the pawn
            lookToil.tickAction = delegate
            {
                // Re-apply the tilt if it was cleared (defensive, matches base mod), or
                // remove it if toddler animations were disabled mid-look.
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, PlayAnimationUtility.BugwatchAnimation);

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            lookToil.defaultCompleteMode = ToilCompleteMode.Delay;
            lookToil.defaultDuration = Rand.Range(MinLookTicks, MaxLookTicks);
            lookToil.AddFinishAction(delegate
            {
                // Only clear the tilt if it's ours to clear (it isn't playing at all
                // while animations are disabled — leave locomotion animations alone).
                PlayAnimationUtility.ClearIfCurrent(pawn, PlayAnimationUtility.BugwatchAnimation);
            });
            yield return lookToil;
        }

        /// <summary>
        /// Drops the carried magnifying glass at the toddler's feet and un-forbids it
        /// so other toddlers can use it. Safe to call even if no glass was tracked.
        ///
        /// Uses <see cref="Glass"/> (the job target) as a fallback because <c>trackedGlass</c>
        /// is set in <see cref="MakeNewToils"/> and is NOT re-initialised after a save/load —
        /// the job target survives serialization, the local field does not.
        /// </summary>
        void DropAndUnforbidTrackedGlass()
        {
            // Job target (TargetC) survives save/load; trackedGlass does not.
            Thing? glass = trackedGlass ?? Glass;
            if (glass != null)
            {
                if (pawn.carryTracker?.CarriedThing == glass)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                TPE_ToyUtility.Unforbid(glass);
            }
            trackedGlass = null;
        }
    }

    // ===========================
    // RUNNING AROUND
    // ===========================
    /// <summary>
    /// Toddler runs around with a +1.2 MoveSpeed hediff. Picks random cells
    /// within 15 tiles, refreshing target every ~1.5 seconds.
    /// </summary>
    public class JobDriver_ToddlerRunning : JobDriver
    {
        int targetCellTicks;
        const int TargetRefreshInterval = 90; // new target every ~1.5 sec
        const int MaxDuration = 1800;
        const float MaxRunDistance = 15f;

        public override string GetReport()
        {
            return "Running around";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);
            targetCellTicks = TargetRefreshInterval;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil runToil = ToilMaker.MakeToil("Running");
            runToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
                PickNewTargetCell();
                // Apply speed boost hediff
                var hediff = HediffMaker.MakeHediff(TPE_HediffDefOf.TPE_Running, pawn);
                pawn.health.AddHediff(hediff);
            };
            runToil.tickAction = delegate
            {
                if (pawn.jobs.curDriver.ticksLeftThisToil <= 0)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    return;
                }

                if (pawn.pather.Moving)
                {
                    ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
                    return;
                }

                targetCellTicks--;
                if (targetCellTicks <= 0 || pawn.Position == TargetA.Cell)
                {
                    targetCellTicks = TargetRefreshInterval;
                    PickNewTargetCell();
                }

                if (TargetA.Cell.IsValid && TargetA.Cell != pawn.Position)
                {
                    pawn.pather.StartPath(TargetA.Cell, PathEndMode.OnCell);
                }

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            runToil.AddFinishAction(delegate
            {
                // Remove speed boost hediff
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(TPE_HediffDefOf.TPE_Running);
                if (hediff != null)
                    pawn.health.RemoveHediff(hediff);
            });
            runToil.defaultCompleteMode = ToilCompleteMode.Delay;
            runToil.defaultDuration = MaxDuration;
            yield return runToil;
        }

        /// <summary>
        /// Picks a random nearby standable, unforbidden, in-allowed-area cell within
        /// <see cref="MaxRunDistance"/> tiles for the toddler to run to next. Falls
        /// back to the toddler's current position (never an off-map/invalid cell) if
        /// no candidate is found.
        /// </summary>
        void PickNewTargetCell()
        {
            Map map = pawn.Map;
            if (!CellFinder.TryFindRandomCellNear(pawn.Position, map, 8,
                c => GenGrid.Standable(c, map)
                     && !c.GetTerrain(map).avoidWander
                     && !ForbidUtility.IsForbidden(c, pawn)
                     && c.InAllowedArea(pawn)
                     && c.DistanceTo(pawn.Position) <= MaxRunDistance, out IntVec3 result))
            {
                result = pawn.Position;
            }
            job.targetA = result;
        }
    }

    // ===========================
    // MONSTER HUNT
    // ===========================
    /// <summary>
    /// Toddler wanders and "hunts" imaginary monsters. Alternates between
    /// walking and looking around (spinning). Awards +1 or -1 mood thought on finish.
    /// </summary>
    public class JobDriver_ToddlerMonsterHunt : JobDriver
    {
        int stateTicks;
        // alertCooldownTicks was removed along with the alert notification system.
        const int MaxDuration = 3600; // ~60 seconds
        const int LookAroundDuration = 180; // ~3 sec looking
        const float HuntRadius = 12f;
        bool lookingAround;

        /// <summary>
        /// The toy sword this toddler picked up. Tracked so we can drop and
        /// unforbid it even if the job ends before the normal finish action.
        /// </summary>
        Thing? trackedSword;

        public override string GetReport()
        {
            return lookingAround ? "looking for monsters" : "hunting monsters";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool ok = ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
            if (ok && TargetC.IsValid && TargetC.Thing != null)
            {
                ok = ReservationUtility.Reserve(pawn, TargetC, job, 1, -1, null, errorOnFailed, false);
            }
            return ok;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);
            stateTicks = 0;
            // alertCooldownTicks removed: alert notification system removed.
            lookingAround = false;
            trackedSword = null;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // If a toy sword was found, pick it up before hunting
            if (TargetC.IsValid && TargetC.Thing != null)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.C);
                trackedSword = TargetC.Thing;
            }

            Toil huntToil = ToilMaker.MakeToil("MonsterHunt");
            huntToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
                PickNewWanderCell();
                // Notification removed: toddler silently hunts monsters without alerts.
            };
            huntToil.tickAction = delegate
            {
                stateTicks--;

                // Periodically stop to look around
                if (!lookingAround && stateTicks <= 0 && !pawn.pather.Moving)
                {
                    lookingAround = true;
                    stateTicks = LookAroundDuration;
                }
                else if (lookingAround && stateTicks <= 0)
                {
                    lookingAround = false;
                    stateTicks = Rand.Range(300, 600); // 5-10 sec walking
                    PickNewWanderCell();
                }

                // During look-around: spin slowly
                if (lookingAround)
                {
                    if (Find.TickManager.TicksGame % 20 == 0)
                    {
                        pawn.rotationTracker.FaceCell(pawn.Position + GenRadial.RadialPattern[Rand.Range(0, 20)]);
                    }
                }

                // Movement
                if (!lookingAround && !pawn.pather.Moving && TargetA.Cell.IsValid && TargetA.Cell != pawn.Position)
                {
                    pawn.pather.StartPath(TargetA.Cell, PathEndMode.OnCell);
                }

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            huntToil.AddFinishAction(delegate
            {
                // Drop and unforbid tracked toy sword regardless of job condition.
                // If the job succeeded the sword was carried; if it failed early
                // trackedSword still points to the carried instance.
                if (trackedSword != null && pawn.carryTracker?.CarriedThing == trackedSword)
                {
                    if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                    {
                        TPE_ToyUtility.Unforbid(dropped);
                    }
                }
                trackedSword = null;

                // 75% chance: Found a Monster! (+1 mood)
                // 25% chance: Didn't Find Any Monsters.. (-1 mood)
                if (Rand.Chance(0.75f))
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(TPE_ThoughtDefOf.TPE_FoundMonster);
                }
                else
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(TPE_ThoughtDefOf.TPE_NoMonsters);
                }
            });
            huntToil.defaultCompleteMode = ToilCompleteMode.Delay;
            huntToil.defaultDuration = MaxDuration;
            yield return huntToil;
        }

        /// <summary>
        /// Picks a random nearby standable, unforbidden, in-allowed-area cell within
        /// <see cref="HuntRadius"/> tiles for the toddler to wander to while hunting.
        /// Falls back to the toddler's current position if no candidate is found.
        /// </summary>
        void PickNewWanderCell()
        {
            Map map = pawn.Map;
            if (!CellFinder.TryFindRandomCellNear(pawn.Position, map, 6,
                c => GenGrid.Standable(c, map)
                     && !c.GetTerrain(map).avoidWander
                     && !ForbidUtility.IsForbidden(c, pawn)
                     && c.InAllowedArea(pawn)
                     && c.DistanceTo(pawn.Position) <= HuntRadius, out IntVec3 result))
            {
                result = pawn.Position;
            }
            job.targetA = result;
        }
    }

    // ===========================
    // BEING A DINOSAUR
    // ===========================
    /// <summary>
    /// Toddler stomps around slowly (hediff reduces speed), roars occasionally
    /// with a 15–25 second individual cooldown, and investigates nearby trees/rocks.
    /// </summary>
    public class JobDriver_ToddlerDinosaur : JobDriver
    {
        int stateTicks;
        int roarCooldownTicks;
        /// <summary>
        /// Initialized to -1 so the first roar check (TicksGame >= -1) always passes,
        /// allowing roars to fire immediately upon job start. This prevents overwhelming
        /// sound buildup when multiple toddlers are dinosaurs simultaneously.
        /// </summary>
        int nextRoarTick = -1;
        const int MaxDuration = 3600; // ~60 seconds
        const int RoarCooldown = 300; // 5 sec min between roars
        static readonly IntRange PersonalRoarCooldownRange = new IntRange(900, 1500); // 15-25 sec individual cooldown
        bool isStomping;
        bool isChasingPrey;
        Thing? investigateTarget;
        /// <summary>
        /// Tracks whether the toddler has reached the investigation target.
        /// Investigation timing only starts after target is reached.
        /// </summary>
        bool investigateTargetReached = false;

        /// <summary>
        /// The dino plush this toddler picked up. Tracked so we can drop and
        /// unforbid it if the job ends before the equip toil succeeds.
        /// </summary>
        Thing? trackedPlush;

        public override string GetReport()
        {
            if (investigateTarget != null) return "Being a Dinosaur - Investigating";
            if (isChasingPrey) return "Being a Dinosaur - Chasing Imaginary Prey";
            if (isStomping) return "Being a Dinosaur - Stomping Around";
            return "Being a Dinosaur - Dinosaur Stomping";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool ok = ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
            if (ok && TargetC.IsValid && TargetC.Thing != null)
            {
                ok = ReservationUtility.Reserve(pawn, TargetC, job, 1, -1, null, errorOnFailed, false);
            }
            return ok;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);
            // Start with no activity scheduled (-1 means idle, waiting for random activity picker)
            stateTicks = -1;
            roarCooldownTicks = 0;
            isStomping = false;
            isChasingPrey = false;
            investigateTarget = null;
            trackedPlush = null;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // If a dino plush was found, go pick it up and equip it first
            if (TargetC.IsValid && TargetC.Thing != null)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.C);
                trackedPlush = TargetC.Thing;

                Toil equipPlush = ToilMaker.MakeToil("EquipDinoPlush");
                equipPlush.initAction = delegate
                {
                    Thing? carried = pawn.carryTracker?.CarriedThing;
                    if (carried is Apparel apparel && apparel.def == TPE_ThingDefOf.TPE_PlushDino)
                    {
                        // Must remove from carry tracker before Wear() to avoid container conflict
                        pawn.carryTracker.innerContainer.Remove(apparel);
                        pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                        // Equip succeeded — plush is now worn, no longer need to track for drop
                        trackedPlush = null;
                    }
                };
                equipPlush.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return equipPlush;
            }

            // Apply slow stomp hediff
            var slowHediff = HediffMaker.MakeHediff(TPE_HediffDefOf.TPE_DinosaurStomp, pawn);
            pawn.health.AddHediff(slowHediff);

            Toil dinoToil = ToilMaker.MakeToil("Dinosaur");
            dinoToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
                PickNewStompCell();
            };
            dinoToil.tickAction = delegate
            {
                stateTicks--;
                roarCooldownTicks--;

                // Roar occasionally — individual cooldown per toddler (15-25 sec)
                if (roarCooldownTicks <= 0 && Rand.Chance(0.001f) && Find.TickManager.TicksGame >= nextRoarTick)
                {
                    roarCooldownTicks = RoarCooldown;
                    nextRoarTick = Find.TickManager.TicksGame + PersonalRoarCooldownRange.RandomInRange;
                    if (pawn.Map != null)
                        TPE_SoundDefOf.TPE_DinoRoar.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }

                // State machine for dinosaur activities: stomp, chase, investigate, or idle
                // All four are equally likely (25% each) when transitioning from idle to a new activity
                bool isInActivity = isStomping || isChasingPrey || investigateTarget != null;

                // Transition out of active activity states when timer expires.
                // CRITICAL: stateTicks counts down from positive (600-900) to 0.
                // When exactly 0, the activity duration is complete and we transition to idle.
                // sentinel value -1 = idle, no activity active. Positive = activity running with timer.
                if (stateTicks == 0 && isInActivity)
                {
                    // Activity timer expired — end current activity and return to idle
                    isStomping = false;
                    isChasingPrey = false;
                    investigateTarget = null;
                    investigateTargetReached = false;
                    // Set to -1 (idle) so activity picker can select next activity
                    stateTicks = -1;
                }

                // When idle (not in any activity), pick the next activity at random (25% each)
                if (!isInActivity && !pawn.pather.Moving && Rand.Chance(0.005f))
                {
                    PickNextActivityIfIdle();
                }

                // Investigation handling: Two-phase behavior
                if (investigateTarget != null)
                {
                    // CRITICAL SAFETY: If target was destroyed or is invalid, abort investigation.
                    // Without this check, investigation never expires (stateTicks never reaches 0 if target unreachable),
                    // and the toddler gets stuck in investigation mode forever trying to path to a dead target.
                    if (investigateTarget.Destroyed || investigateTarget.Map != pawn.Map)
                    {
                        // Target is gone; clear investigation and return to idle
                        investigateTarget = null;
                        investigateTargetReached = false;
                        stateTicks = -1; // Reset to idle state
                    }
                    else if (pawn.Position.AdjacentTo8WayOrInside(investigateTarget.Position))
                    {
                        // TARGET REACHED: Start observation countdown.
                        // CRITICAL: Only start the timer AFTER reaching the target.
                        // This prevents immediate expiration due to our 0-based sentinel system.
                        if (!investigateTargetReached)
                        {
                            investigateTargetReached = true;
                            // Start the 10–15 second observation timer now that target is adjacent
                            stateTicks = Rand.Range(600, 900);
                        }
                        // Toddler stays at target, waiting for timer to expire
                    }
                    else if (!pawn.pather.Moving)
                    {
                        // Target not yet reached and pawn stopped moving — re-path to investigation target
                        pawn.pather.StartPath(investigateTarget, PathEndMode.Touch);
                    }
                    // If pawn is still moving toward target, do nothing; path is active
                }
                else if (!pawn.pather.Moving && TargetA.Cell.IsValid && TargetA.Cell != pawn.Position)
                {
                    pawn.pather.StartPath(TargetA.Cell, PathEndMode.OnCell);
                }

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            dinoToil.AddFinishAction(delegate
            {
                // Remove stomp hediff if present
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(TPE_HediffDefOf.TPE_DinosaurStomp);
                if (hediff != null)
                    pawn.health.RemoveHediff(hediff);

                // If the job failed before the plush was equipped, drop and unforbid it
                if (trackedPlush != null && pawn.carryTracker?.CarriedThing == trackedPlush)
                {
                    if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                    {
                        TPE_ToyUtility.Unforbid(dropped);
                    }
                }
                trackedPlush = null;
            });
            dinoToil.defaultCompleteMode = ToilCompleteMode.Delay;
            dinoToil.defaultDuration = MaxDuration;
            yield return dinoToil;
        }

        /// <summary>
        /// Picks a random nearby standable, unforbidden, in-allowed-area cell for the
        /// dinosaur toddler to stomp to next (radius widens during a "chase" or "stomping" burst).
        /// Falls back to the toddler's current position if no candidate is found.
        /// </summary>
        void PickNewStompCell()
        {
            Map map = pawn.Map;
            int radius = (isStomping || isChasingPrey) ? 8 : 4;
            if (!CellFinder.TryFindRandomCellNear(pawn.Position, map, radius,
                c => GenGrid.Standable(c, map)
                     && !c.GetTerrain(map).avoidWander
                     && !ForbidUtility.IsForbidden(c, pawn)
                     && c.InAllowedArea(pawn), out IntVec3 result))
            {
                result = pawn.Position;
            }
            job.targetA = result;
        }

        /// <summary>
        /// Finds a nearby tree or rock for the toddler to "investigate" during
        /// dinosaur stomping. Targets are filtered to the toddler's allowed area
        /// so they never path outside their restriction to investigate something.
        /// </summary>
        Thing FindNearbyTreeOrRock()
        {
            Map map = pawn.Map;
            // Look for plants (trees) or mineable rocks within 10 tiles.
            // Filter to allowed area so the toddler never leaves their zone.
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(pawn.Position, map, 10f, true))
            {
                if (!t.Position.InAllowedArea(pawn))
                    continue;
                if (t.def.category == ThingCategory.Plant && t.def.plant.IsTree)
                    return t;
                if (t.def.mineable)
                    return t;
            }
            return null;
        }

        /// <summary>
        /// Selects the next dinosaur activity with equal probability (25% each).
        /// All four activities (Stomping, Chasing, Investigating, Idle) run for 600–900 ticks = 10–15 seconds.
        /// Investigation is two-phase: travel phase (variable) + observation phase (600–900 ticks).
        /// This ensures the toddler reaches their target before the observation timer expires.
        /// </summary>
        void PickNextActivityIfIdle()
        {
            // Choose one of four activities with equal probability (25% each)
            // CRITICAL: Choose one of four activities with equal probability (25% each).
            // All four activities (stomping, chasing, investigating, idle) last the same: 10–15 seconds.
            int activityChoice = Rand.Range(0, 4);
            // Duration for this activity: 600–900 ticks = 10–15 seconds (at 60 ticks/sec)
            int activityDuration = Rand.Range(600, 900);
            
            switch (activityChoice)
            {
                case 0:
                    // Stomping Around (Case 0): toddler moves quickly in small bursts (10–15 seconds).
                    // CRITICAL: All four activities use independent 600–900 tick duration rolls,
                    // but within a single PickNextActivityIfIdle() call, cases 0/1/3 share activityDuration.
                    isStomping = true;
                    isChasingPrey = false;
                    investigateTarget = null;
                    investigateTargetReached = false;
                    // Set timer to activityDuration: activity will run 600–900 ticks, then expire at tick 0
                    stateTicks = activityDuration;
                    PickNewStompCell();
                    break;

                case 1:
                    // Chasing Imaginary Prey (Case 1): toddler charges around in wider circles (10–15 seconds).
                    // CRITICAL: Same duration as Stomping and Idle to ensure balanced activity rotation.
                    isStomping = false;
                    isChasingPrey = true;
                    investigateTarget = null;
                    investigateTargetReached = false;
                    // Set timer to activityDuration: activity will run 600–900 ticks, then expire at tick 0
                    stateTicks = activityDuration;
                    PickNewStompCell();
                    break;

                case 2:
                    // Investigating (Case 2): Two-phase activity totaling ~10–15+ seconds.
                    // PHASE 1 (Travel): Find nearby tree/rock, path to it (variable duration, no timer).
                    // PHASE 2 (Observation): Once adjacent, observe for 600–900 ticks independently.
                    // CRITICAL: Investigation doesn't use pre-rolled activityDuration; it independently
                    // rolls Rand.Range(600, 900) when target is reached in the tick action.
                    isStomping = false;
                    isChasingPrey = false;
                    investigateTarget = FindNearbyTreeOrRock();
                    investigateTargetReached = false; // Phase 1: haven't reached target yet
                    // CRITICAL: Explicitly reset stateTicks to -1 (idle sentinel) during travel phase.
                    // DO NOT rely on implicit state; be explicit for code clarity and robustness.
                    // Once target is reached (Phase 2), the tick action will set stateTicks = Rand.Range(600, 900).
                    stateTicks = -1;
                    if (investigateTarget != null)
                    {
                        job.targetB = investigateTarget;
                        pawn.pather.StartPath(investigateTarget, PathEndMode.Touch);
                    }
                    else
                    {
                        // No suitable target found: investigation falls back to idle wandering.
                        // stateTicks remains -1, and no active activity flags set,
                        // so next tick the idle picker can immediately select a new activity.
                        investigateTarget = null;
                    }
                    break;

                case 3:
                default:
                    // Idle/Dinosaur Stomping (Case 3): slow wandering with no special behaviors (10–15 seconds).
                    // CRITICAL: Provides a non-interactive resting state between active bursts.
                    // Uses same duration as Stomping and Chasing for balanced rotation.
                    isStomping = false;
                    isChasingPrey = false;
                    investigateTarget = null;
                    investigateTargetReached = false;
                    // Set timer to activityDuration: activity will run 600–900 ticks, then expire at tick 0
                    stateTicks = activityDuration;
                    PickNewStompCell();
                    break;
            }
        }
    }

    // ===========================
    // PLAY WITH TOY (right-click ordered + autonomous)
    // ===========================
    /// <summary>
    /// Toddler picks up a real toy from storage, carries it to a clear spot,
    /// drops it (un-forbidden), and plays with it while orbiting toy motes spin around them.
    /// </summary>
    public class JobDriver_TPE_PlayWithToy : JobDriver
    {
        const int MaxDuration = 2500;
        const int ToysCount = 5;
        const float ToyDistanceFactor = 0.5f;
        static readonly FloatRange ToyRandomAngleOffset = new FloatRange(-5f, 5f);

        Mote[] motesToMaintain;

        /// <summary>
        /// The toy this toddler picked up. Tracked so we can drop and unforbid
        /// it regardless of when or why the job ends.
        /// </summary>
        Thing? trackedToy;

        public override string GetReport()
        {
            return "Playing with " + TargetA.Thing.LabelShort;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.job.count = 1;
            trackedToy = null;

            // Global finish action: unconditionally drop and unforbid the tracked toy.
            // This fires on success, failure, or interruption, ensuring the toy is
            // never left forbidden on the ground.
            AddFinishAction(delegate (JobCondition condition)
            {
                if (trackedToy != null)
                {
                    // If still carried, drop it first
                    if (pawn.carryTracker?.CarriedThing == trackedToy)
                    {
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                    }
                    // Unforbid regardless of whether it was carried or already placed
                    TPE_ToyUtility.Unforbid(trackedToy);
                }
                trackedToy = null;
            });

            // Only fail if the toy is destroyed or becomes null — do NOT fail on
            // forbidden status, because the toy may have been forbidden from a
            // prior failed job and we want this job to succeed so we can unforbid it.
            this.FailOnDestroyedOrNull(TargetIndex.A);

            // Go to toy
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Pick up toy (take it out of storage)
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);

            // Remember the exact thing instance so we can unforbid it later
            // even if TargetA gets stale.
            trackedToy = TargetA.Thing;

            // Find a nearby clear cell to play at — keep the toy carried so
            // it renders in the toddler's hands, matching the "carry toy" activity.
            Toil findPlayCell = ToilMaker.MakeToil("FindPlayCell");
            findPlayCell.initAction = delegate
            {
                IntVec3 storagePos = TargetA.Thing.Position;
                Map map = pawn.Map;
                IntVec3 playCell = IntVec3.Invalid;
                // Try to find a cell near the toy box that's not a storage building
                // Both the primary and fallback searches require the play cell to be
                // within the toddler's Allowed Area, so carrying a toy away from the
                // storage building never pulls them outside their assigned zone.
                if (!CellFinder.TryFindRandomReachableNearbyCell(
                        storagePos, map, 4,
                        TraverseParms.For(pawn, Danger.Some, TraverseMode.ByPawn, false),
                        c => GenGrid.Standable(c, map)
                             && !ForbidUtility.IsForbidden(c, pawn)
                             && c.InAllowedArea(pawn)
                             && !(c.GetEdifice(map) is Building_Storage)
                             && c != storagePos,
                        null, out playCell))
                {
                    // Fallback: any standable cell nearby, excluding storage buildings
                    playCell = CellFinder.RandomClosewalkCellNear(storagePos, map, 3,
                        c => GenGrid.Standable(c, map)
                             && c.InAllowedArea(pawn)
                             && !(c.GetEdifice(map) is Building_Storage)
                             && c != storagePos);
                }
                // Final fallback: current position (guaranteed valid).
                //
                // NOTE: CellFinder.RandomClosewalkCellNear (used in the fallback
                // search above) returns `root` (storagePos) UNCHANGED when no cell
                // satisfies its validator -- it never yields IntVec3.Invalid. That
                // means `playCell` can come back "valid" (IsValid only checks
                // y >= 0) yet still sit outside the toddler's Allowed Area if the
                // storage building itself is outside it. We explicitly re-check
                // InAllowedArea here so a failed search can't hand the toddler a
                // walk target beyond their assigned zone.
                if (!playCell.IsValid || !playCell.InAllowedArea(pawn))
                    playCell = pawn.Position;
                job.targetB = playCell;
            };
            findPlayCell.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return findPlayCell;

            // Carry toy to play cell (toy stays in hands)
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // Play with toy — toy remains carried so it renders in hands.
            // The motes spin around the toddler for visual flair.
            Toil playToil = ToilMaker.MakeToil("PlayWithToy");
            playToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
                SpawnToyMotes();
            };
            playToil.tickAction = delegate
            {
                MaintainMotes();
                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            playToil.defaultCompleteMode = ToilCompleteMode.Delay;
            playToil.defaultDuration = MaxDuration;
            playToil.AddFinishAction(delegate
            {
                motesToMaintain = null;
            });
            yield return playToil;
        }

        void SpawnToyMotes()
        {
            if (!motesToMaintain.NullOrEmpty()) return;

            Vector3 center = GenThing.TrueCenter(pawn);
            Vector3 baseOffset = IntVec3.North.ToVector3();
            float angleStep = 72f;
            motesToMaintain = new Mote[ToysCount];
            for (int i = 0; i < ToysCount; i++)
            {
                float angle = angleStep * i + ToyRandomAngleOffset.RandomInRange;
                Vector3 pos = center + Vector3Utility.RotatedBy(baseOffset, angle) * ToyDistanceFactor;
                motesToMaintain[i] = MoteMaker.MakeStaticMote(pos, pawn.Map, ThingDefOf.Mote_Toy, 1f);
            }
        }

        void MaintainMotes()
        {
            if (motesToMaintain.NullOrEmpty()) return;
            for (int i = 0; i < motesToMaintain.Length; i++)
            {
                motesToMaintain[i]?.Maintain();
            }
        }
    }

    // ===========================
    // CARRY TOY (right-click ordered — toddler picks up and holds toy)
    // ===========================
    /// <summary>
    /// Toddler picks up a toy, wanders around carrying it for ~40 seconds,
    /// then drops it (un-forbidden) so other toddlers can play with it.
    /// </summary>
    public class JobDriver_TPE_CarryToy : JobDriver
    {
        const int CarryDuration = 2500;
        int wanderTicks;
        const int WanderInterval = 120;

        /// <summary>
        /// The toy this toddler picked up. Tracked so we can drop and unforbid
        /// it regardless of when or why the job ends.
        /// </summary>
        Thing? trackedToy;

        public override string GetReport()
        {
            Thing carried = pawn.carryTracker.CarriedThing;
            if (carried != null)
                return "Carrying " + carried.LabelShort;
            return "Going to pick up toy";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.job.count = 1;
            trackedToy = null;

            // Only fail on destroyed/null — do NOT fail on forbidden, because we
            // want this job to run so it can unforbid the toy.
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: true, failIfStackCountLessThanJobCount: false, reserve: false);
            trackedToy = TargetA.Thing;

            // Wander around while carrying the toy
            Toil carryToil = ToilMaker.MakeToil("CarryToyWander");
            carryToil.initAction = delegate
            {
                wanderTicks = 0;
                PickNewWanderCell();
            };
            carryToil.tickAction = delegate
            {
                if (!pawn.pather.Moving)
                {
                    wanderTicks--;
                    if (wanderTicks <= 0)
                    {
                        wanderTicks = WanderInterval;
                        PickNewWanderCell();
                    }
                }
                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            carryToil.defaultCompleteMode = ToilCompleteMode.Delay;
            carryToil.defaultDuration = CarryDuration;
            carryToil.AddFinishAction(delegate
            {
                if (trackedToy != null)
                {
                    if (pawn.carryTracker?.CarriedThing == trackedToy)
                    {
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                    }
                    TPE_ToyUtility.Unforbid(trackedToy);
                }
                trackedToy = null;
            });
            yield return carryToil;
        }

        /// <summary>
        /// Picks a random nearby standable, unforbidden, in-allowed-area cell for the
        /// toddler to wander to while carrying a toy, and immediately starts pathing
        /// there if it differs from the current position.
        /// </summary>
        void PickNewWanderCell()
        {
            Map map = pawn.Map;
            if (CellFinder.TryFindRandomCellNear(pawn.Position, map, 5,
                c => GenGrid.Standable(c, map) && !c.GetTerrain(map).avoidWander && !ForbidUtility.IsForbidden(c, pawn) && c.InAllowedArea(pawn), out IntVec3 result))
            {
                if (result != pawn.Position)
                    pawn.pather.StartPath(result, PathEndMode.OnCell);
            }
        }
    }

    // ===========================
    // BUG WATCHING WITH MAGNIFYING GLASS
    // ===========================
    /// <summary>
    /// Enhanced bug watching activity: toddler picks up a magnifying glass,
    /// carries it to an outdoor location, then watches bugs while holding it.
    /// Based on the CarryToy pattern (visible carrying) + base mod's bugwatching animation.
    ///
    /// Vanilla comparison:
    ///   - Similar to base mod's JobDriver_ToddlerBugwatching (animation + outdoor check)
    ///   - Similar to JobDriver_TPE_CarryToy (haul glass + carry visible)
    ///   - Combines both: picks up glass, goes to outdoor cell, plays animation
    /// </summary>
    public class JobDriver_TPE_BugwatchingWithMagnifyingGlass : JobDriver
    {
        /// <summary>How long the toddler watches bugs while holding the glass (ticks).</summary>
        const int BugwatchDuration = 2000;

        /// <summary>Tracks the glass so we can drop it and unforbid it when the job ends.</summary>
        Thing? trackedGlass;

        public override string GetReport()
        {
            Thing carried = pawn.carryTracker.CarriedThing;
            if (carried != null)
                return "Watching bugs with magnifying glass";
            if (trackedGlass != null)
                return "Going to get magnifying glass to watch bugs";
            return "Going to watch bugs";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Reserve the magnifying glass (TargetA)
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            trackedGlass = null;
            job.count = 1; // Required for Toils_Haul.StartCarryThing()

            // Only fail if glass is destroyed. Don't fail if forbidden — we'll unforbid it.
            this.FailOnDestroyedOrNull(TargetIndex.A);

            // 1. Go to the magnifying glass location
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);

            // 2. Pick up the glass using the same haul system as CarryToy
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: true, failIfStackCountLessThanJobCount: false, reserve: false);
            trackedGlass = TargetA.Thing;

            // 3. Go to the bug watching location (TargetB = outdoor cell)
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // 4. Create the bug watching toil (animation + duration)
            Toil bugwatchToil = ToilMaker.MakeToil("BugwatchWithGlass");
            bugwatchToil.initAction = delegate
            {
                // Set upright posture and play animation
                pawn.jobs.posture = PawnPosture.Standing;
                // Face East or West (same as base-mod bug watching). Kept even with
                // animations disabled — facing isn't an animation. Rand.Bool replaces a
                // throw-away List<Rot4> allocation + RandomElement (same 50/50 result).
                pawn.Rotation = Rand.Bool ? Rot4.East : Rot4.West;
                // Base-mod Bugwatch tilt — only if toddler play animations are allowed.
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, PlayAnimationUtility.BugwatchAnimation);
            };
            bugwatchToil.handlingFacing = true;
            bugwatchToil.tickAction = delegate
            {
                // Keep playing the tilt if it stops, or remove it if toddler animations
                // were disabled mid-activity. Uses the shared one-time def lookup instead
                // of the two DefDatabase lookups per tick this used to do.
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, PlayAnimationUtility.BugwatchAnimation);
                // Check if play should end (based on play need)
                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            bugwatchToil.defaultCompleteMode = ToilCompleteMode.Delay;
            bugwatchToil.defaultDuration = BugwatchDuration;
            // Fail if we're not in an outdoor room (same check as base mod)
            bugwatchToil.FailOn(() => !GridsUtility.GetRoom(pawn.Position, pawn.Map).PsychologicallyOutdoors);
            bugwatchToil.AddFinishAction(delegate
            {
                // Stop the tilt — only if it's actually ours (never clear the base mod's
                // locomotion animation, which is what plays while ours is disabled).
                PlayAnimationUtility.ClearIfCurrent(pawn, PlayAnimationUtility.BugwatchAnimation);
                // Drop the glass if still carrying it
                if (trackedGlass != null && pawn.carryTracker?.CarriedThing == trackedGlass)
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                // Unforbid the glass so it can be used again
                TPE_ToyUtility.Unforbid(trackedGlass);
                trackedGlass = null;
            });
            yield return bugwatchToil;
        }
    }

    // ===========================
    // SELF SOOTHING
    // ===========================
    /// <summary>
    /// Crib-only activity for upset toddlers. The toddler attempts to calm
    /// themselves through one of several sub-activities (thumb sucking,
    /// blanket clutching, etc.). At the end there is a 50% chance to gain
    /// a mood thought — either positive or negative — referencing the adult
    /// the toddler has the highest opinion of ("Mommy"/"Daddy" if a parent).
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="JobDriver_WiggleInCrib"/> and
    ///     <see cref="JobDriver_RestIdleInCrib"/> from the base Toddlers mod:
    ///     <see cref="JobDriver_RelaxAlone"/> (vanilla bed activity) and
    ///     <see cref="Toddlers.JobDriver_ToddlerSkydreaming"/> (base-mod crib
    ///     activity). Both pass the bed as <c>TargetA</c>, call
    ///     <see cref="JobInBedUtility.KeepLyingDown"/>, and use
    ///     <see cref="Toils_LayDown.LayDown"/> so the pawn stays in bed.
    ///   - Differs in being a PLAY activity (calls
    ///     <see cref="ToddlerPlayUtility.ToddlerPlayTickCheckEnd"/>), having
    ///     dynamic report strings, and granting conditional mood thoughts.
    /// </summary>
    public class JobDriver_ToddlerSelfSoothing : JobDriver
    {
        /// <summary>Pool of sub-activity names displayed in the job report.</summary>
        static readonly string[] SubActivities = new string[]
        {
            "Thumb Sucking",
            "Blanket Clutching",
            "Curling up into a ball",
            "Eye covering",
            "Ear Covering",
            "Pillow Cuddling",
            "Thinking Happy Thoughts"
        };

        /// <summary>The sub-activity chosen for this job instance.</summary>
        string currentSubActivity = "";

        // The base mod's wiggle AnimationDef is resolved once in
        // PlayAnimationUtility.WiggleInCribAnimation (shared, null-tolerant).

        /// <summary>The crib this toddler is soothing in, if any.</summary>
        Building_Bed? Bed => job.GetTarget(TargetIndex.A).Thing as Building_Bed;

        /// <summary>True when the job was created with a valid bed target.</summary>
        bool FromBed => Bed != null;

        public override string GetReport()
        {
            if (!string.IsNullOrEmpty(currentSubActivity))
                return "Self Soothing - " + currentSubActivity;
            return "Self Soothing";
        }

        /// <summary>
        /// Allows the job to start while the toddler is already lying down in
        /// the crib, preventing them from getting up and walking around just
        /// to start self soothing.
        /// </summary>
        public override void Notify_Starting()
        {
            base.Notify_Starting();
            // Pick the sub-activity exactly once when the job starts.
            // Previously this was in AddPreInitAction which could re-run
            // if the LayDown toil re-initialised, causing the label to flicker.
            currentSubActivity = SubActivities[Rand.Range(0, SubActivities.Length)];
        }

        public override bool CanBeginNowWhileLyingDown()
        {
            if (FromBed)
            {
                // Check if toddler is in the crib location. Self Soothing is a
                // lying-down activity, so they'll typically be in sleep posture,
                // but we use CribUtility.InCrib for consistency with other crib jobs.
                return CribUtility.InCrib(pawn);
            }
            return false;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No explicit reservation needed — the toddler is already in the
            // crib. KeepLyingDown handles returning them to bed after play.
            // Attempting to reserve here conflicts with the toddler's existing
            // LayDown/Sleep reservation on the same bed.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // If the mod setting was disabled after the job was created,
            // end immediately rather than running a disabled activity.
            if (!TPE_Mod.Settings.enableSelfSoothing)
            {
                yield return ToilMaker.MakeToil("SelfSoothingDisabled");
                yield break;
            }

            // Fail if the toddler leaves the crib or becomes downed.
            AddFailCondition(() => pawn.Downed || !CribUtility.InCrib(pawn));

            if (FromBed)
            {
                // When the job ends, automatically re-queue a LayDown job so
                // the toddler stays in the crib instead of standing up and
                // wandering off. This mirrors vanilla RelaxAlone and base-mod
                // Skydreaming behaviour.
                this.KeepLyingDown(TargetIndex.A);

                // We intentionally do NOT call ClaimBedIfNonMedical here.
                // The toddler is already in the crib from their LayDown sleep
                // job, which claimed the bed. Re-claiming during play causes
                // ownership battles when two toddlers share a crib (colonists
                // can mistakenly place multiple toddlers in one crib), leading
                // to rapid job failures and visible sub-activity flickering.
                //
                // GotoBed is kept as a safe no-op (completes instantly when
                // already in bed) and ensures the pawn is on the correct cell.
                yield return Toils_Bed.GotoBed(TargetIndex.A);
            }

            Toil toil;
            if (FromBed)
            {
                // Use the vanilla LayDown toil with sleep and rest gain
                // disabled — this keeps the toddler in bed posture and
                // handles all bed-related logic (comfort, bed effects, etc.).
                toil = Toils_LayDown.LayDown(
                    TargetIndex.A,
                    hasBed: true,
                    lookForOtherJobs: false,
                    canSleep: false,
                    gainRestAndHealth: false);
            }
            else
            {
                // Fallback for the impossible case where no bed was targeted.
                toil = ToilMaker.MakeToil("SelfSoothing");
                toil.initAction = delegate
                {
                    pawn.jobs.posture = PawnPosture.LayingOnGroundNormal;
                };
            }

            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            // Reduced from 300–900 ticks (5–15 sec) to 180–420 ticks (3–7 sec)
            // so the activity finishes a little quicker, reducing the window
            // for bed-conflict edge cases and feeling snappier in-game.
            toil.defaultDuration = Rand.Range(180, 420);
            toil.handlingFacing = true;

            // Run BEFORE the LayDown toil's initAction so our rotation is
            // preserved (handlingFacing=true prevents locomotion override).
            toil.AddPreInitAction(delegate
            {
                float roll = Rand.Value;
                pawn.Rotation = roll < 0.5f
                    ? Rot4.South
                    : (roll < 0.75f ? Rot4.East : Rot4.West);

                // Start the wiggle animation so the toddler visibly squirms
                // in the crib while self-soothing — only if toddler play animations
                // are allowed (otherwise the toddler just lies still in the crib).
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, PlayAnimationUtility.WiggleInCribAnimation);
            });

            // Run BEFORE the LayDown toil's tickAction each tick.
            toil.AddPreTickAction(delegate
            {
                // Keep/stop the wiggle if the setting is flipped mid-activity (no-op
                // otherwise — SetAnimation is only called when the state must change).
                PlayAnimationUtility.MaintainToddlerAnimation(pawn, PlayAnimationUtility.WiggleInCribAnimation);

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            });

            // Run AFTER the LayDown toil's finishAction when the toil ends.
            toil.AddFinishAction(delegate
            {
                // Record the end time for the personal cooldown.
                // This prevents the toddler from immediately re-starting Self
                // Soothing (especially important when two toddlers share a crib,
                // where rapid re-starts cause visible animation glitches).
                ToddlerPlayGiver_SelfSoothing.RecordSelfSoothingEnd(pawn);

                // Clear the wiggle animation so it doesn't persist after the job
                // (only if it's the one playing — same rule as before, now shared).
                PlayAnimationUtility.ClearIfCurrent(pawn, PlayAnimationUtility.WiggleInCribAnimation);

                // 50% chance at the end to reflect on the experience.
                if (Rand.Chance(0.5f))
                {
                    GiveSelfSoothingThought();
                }
            });

            yield return toil;
        }

        /// <summary>
        /// Grants either a positive or negative Self Soothing mood thought.
        /// The thought references the adult on the map that the toddler has
        /// the highest opinion of; if that adult is a parent, their name is
        /// replaced with "Mommy" or "Daddy" in the description.
        /// </summary>
        void GiveSelfSoothingThought()
        {
            // Safety: if the toddler was despawned or has no mood need,
            // we cannot grant a mood thought.
            if (pawn?.Map == null || pawn.needs?.mood == null)
                return;

            // A toddler should not stack positive and negative self-soothing
            // thoughts. If either one is already active, skip granting a new one.
            var memories = pawn.needs.mood.thoughts.memories;
            if (memories.GetFirstMemoryOfDef(TPE_ThoughtDefOf.TPE_SelfSoothingPositive) != null
                || memories.GetFirstMemoryOfDef(TPE_ThoughtDefOf.TPE_SelfSoothingNegative) != null)
            {
                return;
            }

            // Find the adult the toddler likes most among all spawned pawns.
            Pawn bestAdult = null;
            int bestOpinion = int.MinValue;

            foreach (Pawn p in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (p == pawn) continue;
                if (!p.RaceProps.Humanlike) continue;
                // Only consider adults (not children, not other toddlers).
                if (p.DevelopmentalStage != DevelopmentalStage.Adult) continue;

                int opinion = pawn.relations?.OpinionOf(p) ?? 0;
                if (opinion > bestOpinion)
                {
                    bestOpinion = opinion;
                    bestAdult = p;
                }
            }

            // 50/50 positive vs negative outcome.
            bool isPositive = Rand.Chance(0.5f);
            ThoughtDef thoughtDef = isPositive
                ? TPE_ThoughtDefOf.TPE_SelfSoothingPositive
                : TPE_ThoughtDefOf.TPE_SelfSoothingNegative;

            Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(thoughtDef);

            // Store the outcome flag so the custom thought class can build
            // the correct description string.
            if (thought is Thought_SelfSoothing ssThought)
            {
                ssThought.isPositive = isPositive;
            }

            // CRITICAL: Pass bestAdult as the second argument.
            // MemoryThoughtHandler.TryGainMemory overwrites newThought.otherPawn
            // with its 'otherPawn' parameter. If we omit it, the parameter
            // defaults to null and wipes out the adult we just found.
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought, bestAdult);
        }
    }

    // ===========================
    // CRIB JUMPING
    // ===========================
    /// <summary>
    /// Toddler stands in their crib and bounces up and down repeatedly.
    /// Reuses the same vertical-bounce visual as <see cref="JobDriver_ToddlerJumping"/>
    /// (handled by the Harmony postfix on <see cref="PawnRenderer.GetBodyPos"/>).
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="JobDriver_ToddlerJumping"/>: both use
    ///     <c>PawnPosture.Standing</c> and the same GetBodyPos bounce arc.
    ///   - Differs in being crib-only (no movement, targets the crib as
    ///     TargetA) and walker-only (crawlers excluded).
    ///   - Mirrors <see cref="JobDriver_ToddlerSelfSoothing"/> for crib
    ///     reservation, <c>CanBeginNowWhileLyingDown</c>, and fail-if-leaves-crib.
    /// </summary>
    public class JobDriver_ToddlerCribJumping : JobDriver
    {
        const int MaxDuration = 1800; // 30 seconds max

        /// <summary>The crib this toddler is jumping in, if any.</summary>
        Building_Bed? Bed => job.GetTarget(TargetIndex.A).Thing as Building_Bed;

        /// <summary>True when the job was created with a valid bed target.</summary>
        bool FromBed => Bed != null;

        public override string GetReport()
        {
            return "Jumping in crib";
        }

        /// <summary>
        /// Allows the job to start while the toddler is already in the crib.
        /// CRITICAL: This check prevents colonists from repeatedly picking up
        /// the toddler during sleep schedules or crib lock. During crib jumping,
        /// the toddler is standing (for animation), not lying down. But they ARE
        /// already in their sleep location. Returning true here tells vanilla AI:
        /// "This toddler is already in bed—don't try to move them."
        /// Without this, vanilla LayDown jobs interrupt CribJumping repeatedly.
        /// </summary>
        public override bool CanBeginNowWhileLyingDown()
        {
            if (FromBed)
            {
                // Check if toddler is in the crib location (regardless of posture/animation).
                return CribUtility.InCrib(pawn);
            }
            return false;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No explicit reservation needed — the toddler is already in the
            // crib. KeepLyingDown handles returning them to bed after play.
            // Attempting to reserve here conflicts with the toddler's existing
            // LayDown/Sleep reservation on the same bed.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);

            // Fail if the toddler leaves the crib or becomes downed.
            AddFailCondition(() => pawn.Downed || !CribUtility.InCrib(pawn));

            if (FromBed)
            {
                // When the job ends, automatically re-queue a LayDown job so
                // the toddler settles back into the crib instead of standing
                // up and wandering off. Mirrors Self Soothing behaviour.
                this.KeepLyingDown(TargetIndex.A);

                // We intentionally do NOT call ClaimBedIfNonMedical here.
                // Re-claiming during play causes ownership battles when two
                // toddlers share a crib (colonists can mistakenly place
                // multiple toddlers in one crib), leading to rapid job
                // failures and animation glitches.
            }

            Toil jumpToil = ToilMaker.MakeToil("CribJumping");
            jumpToil.initAction = delegate
            {
                // Standing posture is required so the GetBodyPos postfix
                // (which gates on PawnPosture.Standing) applies the bounce.
                pawn.jobs.posture = PawnPosture.Standing;
                // Always face forward (South / toward the camera) for
                // consistent crib-jumping visuals.
                pawn.Rotation = Rot4.South;
            };
            jumpToil.handlingFacing = true;
            jumpToil.tickAction = delegate
            {
                // Enforce forward-facing every tick so the toddler never
                // spins sideways or backward during the bounce.
                if (pawn.Rotation != Rot4.South)
                    pawn.Rotation = Rot4.South;

                // Standard play tick check — ends the job when play need
                // is full (unless our play-need-gate patch is active).
                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            jumpToil.defaultCompleteMode = ToilCompleteMode.Delay;
            jumpToil.defaultDuration = MaxDuration;
            yield return jumpToil;
        }
    }

    // ===========================
    // CEILING WATCHING
    // ===========================
    /// <summary>
    /// Toddler lies down and gazes up at the ceiling. The inverse of the
    /// base mod's Skydreaming — this requires a roof overhead instead of
    /// forbidding one.
    ///
    /// Vanilla comparison:
    ///   - Mirrors <see cref="Toddlers.JobDriver_ToddlerSkydreaming"/>:
    ///     same 2000-tick duration, same <c>PawnPosture.LayingOnGroundNormal</c>
    ///     (with <c>LayingInBed</c> when in a crib), same
    ///     <see cref="ToddlerPlayUtility.ToddlerPlayTickCheckEnd"/> tick action,
    ///     and same <see cref="JobInBedUtility.KeepLyingDown"/> crib behaviour.
    ///   - Differs in the roof check: <c>FailOn(!Roofed)</c> instead of
    ///     <c>FailOn(Roofed)</c>.
    ///   - Works both in-crib (TargetA = bed) and out-of-crib (TargetA = cell).
    ///     When out-of-crib, the toddler does not move — they use their
    ///     current cell.
    /// </summary>
    public class JobDriver_ToddlerCeilingWatching : JobDriver
    {
        /// <summary>True when TargetA is a bed (crib context).</summary>
        bool FromBed => job.GetTarget(TargetIndex.A).Thing is Building_Bed;

        public override string GetReport()
        {
            return "Gazing at the ceiling";
        }

        /// <summary>
        /// Allows the job to start while the toddler is already in the crib.
        /// This check is critical during sleep schedules to prevent colonists from
        /// repeatedly picking up and putting down the toddler. Ceiling Watching
        /// is a lying-down activity, so toddlers will be in their sleep posture,
        /// making this check particularly important for seamless sleep transitions.
        /// </summary>
        public override bool CanBeginNowWhileLyingDown()
        {
            if (FromBed)
            {
                // Check if toddler is in the crib location (whether lying or standing).
                // During ceiling watching in-crib they'll be lying down anyway, but
                // using CribUtility.InCrib ensures consistency across all crib activities.
                return CribUtility.InCrib(pawn);
            }
            return false;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No reservation needed — the toddler stays exactly where they are.
            // Reserving the current cell conflicts with other toddlers who may
            // be doing floor-drawing or other stationary play on the same tile.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);

            // Job ends immediately if the toddler leaves the roofed area
            // (e.g. walks outside or the roof is destroyed).
            AddFailCondition(() => !pawn.Position.Roofed(pawn.Map));

            // In a crib/bed: when this job ends, re-queue LayDown so the toddler settles back
            // into bed (and, below 60% rest, falls asleep, now undisturbed: see
            // TPE_PlayUtility.CanStartInCribPlay) instead of standing up in the crib.
            // BUGFIX (v1.2.1 passover): this used to be registered inside the watch toil's
            // initAction. A toil initAction does NOT re-run when a saved, in-progress job is
            // restored (only MakeNewToils does, via SetupToils in PostLoadInit), so after loading
            // a save mid-activity the toddler never lay back down. Registered here at driver
            // level, exactly like vanilla JobDriver_RelaxAlone / JobDriver_WatchBuilding. FromBed
            // reads the SAVED job target, so this condition is safe in MakeNewToils (Architecture
            // §6 rule). MakeNewToils runs once per driver instance, so it is registered once.
            if (FromBed)
                this.KeepLyingDown(TargetIndex.A);

            // Only move when not starting from a bed. In practice this is a
            // no-op when the toddler is already at their own Position cell.
            if (!FromBed)
            {
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            }

            Toil watchToil = ToilMaker.MakeToil("CeilingWatching");
            watchToil.initAction = delegate
            {
                // Use the EXACT same posture as vanilla/base Skydreaming:
                // LayingOnGroundFaceUp (value 3) has the FaceUpMask bit set,
                // which tells RimWorld's renderer to show the pawn facing
                // upward toward the camera — the "lying on back" sprite.
                // LayingOnGroundNormal (value 1) lacks FaceUpMask, which is
                // why the toddler was appearing sideways before.
                // When in a crib we OR with InBedMask to get LayingInBedFaceUp
                // (value 7), matching JobDriver_ToddlerSkydreaming exactly.
                // (KeepLyingDown is registered at driver level above, not here.)
                pawn.jobs.posture = FromBed
                    ? PawnPosture.LayingInBedFaceUp
                    : PawnPosture.LayingOnGroundFaceUp;
            };
            watchToil.tickIntervalAction = delegate(int delta)
            {
                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn, delta);
            };
            watchToil.defaultCompleteMode = ToilCompleteMode.Delay;
            watchToil.defaultDuration = 2000; // Same as base Skydreaming.
            yield return watchToil;
        }
    }

    // ===========================
    // PILLOW NESTING
    // ===========================
    /// <summary>
    // PILLOW NESTING JOB DRIVER — SHELVED FOR LATER
    // Entire JobDriver_ToddlerPillowNesting class commented out due to animation caching issues.
    // To restore: uncomment all code below.
    /*
    /// Toddler lies in their crib and subtly shifts position and rotation,
    /// as if nestling into pillows and blankets.  Instead of a smooth
    /// animation, the toddler snaps to a new pose (slightly different body
    /// angle and position within the crib) every 3–5 seconds and holds it.
    ///
    /// Uses <see cref="PawnPosture.LayingOnGroundNormal"/> (same as the
    /// base mod's <see cref="Toddlers.JobDriver_ToddlerSkydreaming"/> and
    /// Spinning Around) so the <see cref="AnimationWorker_ToddlerPillowNesting"/>
    /// angle is actually rendered.  <see cref="PawnPosture.LayingInBed"/>
    /// (used by <see cref="Toils_LayDown.LayDown"/>) overrides animation
    /// angles, which is why the rotation was invisible before.
    ///
    /// Crib-only. No mood thoughts — ambient visual comfort activity.
    /// </summary>
    /// <summary>
    /// Toddler Pillow Nesting: lay in crib while shifting to discrete poses every 3–5 seconds.
    /// 
    /// DESIGN: Closely mirrors base Toddlers' JobDriver_LayAngleInCrib to ensure animation
    /// system compatibility. The key insight is that the animation worker (AnimationWorker_ToddlerPillowNesting)
    /// reads the CURRENT angle field every frame—the animation itself is set ONCE and never restarted.
    /// Restarting the animation breaks the rendering cache, which is why previous attempts failed.
    /// 
    /// IMPLEMENTATION:
    /// - Store the current body angle in the public `angle` field (job driver property)
    /// - Store a counter `ticksAtAngle` to track when to snap to the next pose
    /// - Update only these fields; the animation worker will naturally pick up the new angle
    /// - Use AddPreTickAction (not animation restart) to update the angle every 3–5 seconds
    /// </summary>
    public class JobDriver_ToddlerPillowNesting : JobDriver
    {
        /// <summary>
        /// Current body-angle offset (degrees) for this pose.
        /// AnimationWorker_ToddlerPillowNesting.AngleAtTick() reads this field directly
        /// and returns it every rendering frame. The animation system is set once and left alone.
        /// </summary>
        public float angle;

        /// <summary>
        /// Counter for ticks at the current pose. When this exceeds 300 (5 seconds),
        /// we pick a new random angle and reset the counter. Follows base mod's pattern exactly.
        /// Range is 180-300 ticks (3-5 seconds).
        /// </summary>
        public int ticksAtAngle;

        /// <summary>
        /// Current position offset within the crib. The Harmony postfix on
        /// PawnRenderer.GetBodyPos adds this vector so the toddler appears
        /// to have shifted slightly along the crib's axis.
        /// </summary>
        public Vector3 currentOffset = Vector3.zero;

        /// <summary>The crib this toddler is nesting in, if any.</summary>
        Building_Bed? Bed => job.GetTarget(TargetIndex.A).Thing as Building_Bed;

        /// <summary>True when the job was created with a valid bed target.</summary>
        bool FromBed => Bed != null;

        public override string GetReport()
        {
            return "Pillow Nesting";
        }

        /// <summary>
        /// Allows the job to start while the toddler is already lying down
        /// in the crib, preventing them from getting up just to re-lie down.
        /// </summary>
        public override bool CanBeginNowWhileLyingDown()
        {
            if (FromBed)
                return JobInBedUtility.InBedOrRestSpotNow(pawn, job.GetTarget(TargetIndex.A));
            return false;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Bed != null)
                return pawn.Reserve(Bed, job, Bed.SleepingSlotsCount, 0, null, errorOnFailed);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // End the job if the toddler has an imminent basic need (food,
            // rest, etc.) — standard for all toddler play activities.
            ToilFailConditions.FailOnChildLearningConditions(this);

            // Fail if the toddler leaves the crib or becomes downed.
            AddFailCondition(() => pawn.Downed || !CribUtility.InCrib(pawn));

            Toil toil = ToilMaker.MakeToil("PillowNesting");

            // INITIALIZATION: set posture once, pick first pose, set animation once.
            // The animation is NEVER restarted; it just keeps running while we update
            // the angle field. This is the critical difference from the previous broken approach.
            toil.AddPreInitAction(delegate
            {
                // LayingInBedFaceUp (7 = LayingMask | FaceUpMask | InBedMask)
                // keeps the toddler IN the bed while allowing our animation angle to render.
                // Matches base mod exactly.
                pawn.jobs.posture = (PawnPosture)7;

                // Pick the first angle right away so toddler doesn't start at 0°.
                angle = Rand.Range(-45f, 45f);
                ticksAtAngle = 0;
                SnapOffset();

                // SET ANIMATION ONCE. Do not restart it later.
                AnimationDef anim = GetPillowNestingAnimation();
                if (anim != null)
                    pawn.Drawer.renderer.SetAnimation(anim);
            });

            // PRE-TICK: every tick, check if we've hit the snap time.
            // If so, pick a new random angle and offset. NEVER restart the animation.
            toil.AddPreTickAction(delegate
            {
                ticksAtAngle++;

                // Snap to a new pose every 180-300 ticks (3-5 seconds).
                // Max ticks before forced snap is 300. When we reach it, reset counter
                // and pick a new angle. This follows base mod pattern exactly.
                if (ticksAtAngle > 300)
                {
                    angle = Rand.Range(-45f, 45f);
                    ticksAtAngle = 0;
                    SnapOffset();
                }

                // If something (e.g., another system) cleared the animation,
                // restore it. But don't restart it—just set it back.
                AnimationDef anim = GetPillowNestingAnimation();
                if (anim != null && pawn.Drawer.renderer.CurAnimation != anim)
                    pawn.Drawer.renderer.SetAnimation(anim);
            });

            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = Rand.Range(2500, 4000); // ~40–65 seconds
            toil.handlingFacing = true;

            // CLEANUP: clear animation when job ends.
            toil.AddFinishAction(delegate
            {
                AnimationDef anim = GetPillowNestingAnimation();
                if (anim != null && pawn.Drawer.renderer.CurAnimation == anim)
                    pawn.Drawer.renderer.SetAnimation(null);
            });

            yield return toil;
        }

        /// <summary>
        /// Recalculate the position offset within the crib based on the crib's rotation.
        /// Called at job init and every time angle snaps to a new value.
        /// </summary>
        void SnapOffset()
        {
            Building_Bed? crib = CribUtility.GetCurrentCrib(pawn);
            if (crib != null)
            {
                // Calculate the crib's forward and right vectors based on its rotation.
                Vector3 forward = crib.Rotation.FacingCell.ToVector3();
                Vector3 right = new Vector3(forward.z, 0f, -forward.x);

                // Pick small random offsets along and across the crib's axis.
                float along = Rand.Range(-0.08f, 0.08f);
                float across = Rand.Range(-0.04f, 0.04f);
                currentOffset = forward * along + right * across;
            }
            else
            {
                currentOffset = Vector3.zero;
            }
        }

        /// <summary>Lazily resolved Pillow Nesting animation def.</summary>
        static AnimationDef? PillowNestingAnimationDef;

        /// <summary>
        /// Get (or cache) the Pillow Nesting animation def.
        /// </summary>
        static AnimationDef GetPillowNestingAnimation()
        {
            if (PillowNestingAnimationDef == null)
                PillowNestingAnimationDef = DefDatabase<AnimationDef>.GetNamed("TPE_PillowNesting", errorOnFail: false);
            return PillowNestingAnimationDef;
        }
    }
    */

    // ===========================
    // SPEAKING PRACTICE (OUT OF CRIB)
    // ===========================
    /// <summary>
    /// Toddler wanders around the map, stopping at various spots to practice
    /// talking. A continuous babble sustainer plays random clips from the
    /// <see cref="TPE_SoundDefOf.TPE_SpeechPracticeBabble"/> folder with a
    /// 1–2 second gap between each. The sustainer starts when the job begins
    /// and is forcibly ended when the job ends, ensuring no audio leaks.
    ///
    /// Movement pattern:
    ///   1. Pick a random nearby standable cell.
    ///   2. Walk to it.
    ///   3. Stand there for 3–7 seconds (Random.Range 180–420 ticks).
    ///   4. Repeat from step 1 until the max duration (30 sec) expires.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="JobDriver_ToddlerRunning"/> (cell-based
    ///     wandering, max duration, speed-boost hediff optional).
    ///   - Differs in having discrete stand-and-talk pauses rather than
    ///     continuous movement.
    ///   - Sound pattern mirrors <see cref="TPE_ChildCryingSound"/> (folder
    ///     grain, sustainLoop=False, random sequential playback).
    /// </summary>
    public class JobDriver_ToddlerSpeechPractice : JobDriver
    {
        const int MaxDuration = 1800; // 30 seconds max
        const int PauseDurationMin = 180; // 3 seconds
        const int PauseDurationMax = 420; // 7 seconds
        const float MaxWanderDistance = 12f;

        /// <summary>Currently playing babble sustainer, or null.</summary>
        Sustainer? activeSustainer;

        /// <summary>Ticks remaining at the current pause spot.</summary>
        int pauseTicksRemaining;

        /// <summary>True while the toddler is in the "stand and talk" phase.</summary>
        bool isPausing;

        public override string GetReport()
        {
            return isPausing ? "practicing talking" : "walking around practicing talking";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Reserve the target cell so two toddlers don't stack on the
            // same spot. Same pattern as Running Around.
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);

            // Safety net: ensure audio stops even if the job is interrupted
            // between toils (e.g. by drafting or a higher-priority emergency).
            AddFinishAction(delegate { EndBabbleSustainer(); });

            // Initial goto — walk to the first target cell.
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil speechToil = ToilMaker.MakeToil("SpeechPractice");
            speechToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
                isPausing = true;
                pauseTicksRemaining = Rand.Range(PauseDurationMin, PauseDurationMax);
                StartBabbleSustainer();
            };
            speechToil.tickAction = delegate
            {
                // Keep the sustainer alive — PerTick maintenance requires
                // this call every tick or the sound dies.
                activeSustainer?.Maintain();

                // Standard play-need check — ends the job when play need is
                // full (unless our play-need-gate patch is active).
                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);

                if (isPausing)
                {
                    pauseTicksRemaining--;
                    if (pauseTicksRemaining <= 0)
                    {
                        // Pause finished — pick a new cell and transition to
                        // movement mode.
                        isPausing = false;
                        PickNewTargetCell();
                        if (TargetA.Cell.IsValid && TargetA.Cell != pawn.Position)
                        {
                            pawn.pather.StartPath(TargetA.Cell, PathEndMode.OnCell);
                        }
                    }
                }
                else
                {
                    // Moving to the next spot.
                    if (!pawn.pather.Moving)
                    {
                        // Arrived — start a new pause.
                        isPausing = true;
                        pauseTicksRemaining = Rand.Range(PauseDurationMin, PauseDurationMax);
                    }
                }
            };
            speechToil.AddFinishAction(delegate
            {
                EndBabbleSustainer();
            });
            speechToil.defaultCompleteMode = ToilCompleteMode.Delay;
            speechToil.defaultDuration = MaxDuration;
            yield return speechToil;
        }

        /// <summary>
        /// Spawns the babble sustainer so random clips from the ToddlerSpeech
        /// folder play continuously with 1–2 second gaps.
        /// </summary>
        void StartBabbleSustainer()
        {
            if (TPE_SoundDefOf.TPE_SpeechPracticeBabble != null)
            {
                SoundInfo info = SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map), MaintenanceType.PerTick);
                activeSustainer = TPE_SoundDefOf.TPE_SpeechPracticeBabble.TrySpawnSustainer(info);
            }
        }

        /// <summary>
        /// Forcibly ends the babble sustainer so audio stops the instant the
        /// toddler stops practicing.
        /// </summary>
        void EndBabbleSustainer()
        {
            if (activeSustainer != null)
            {
                activeSustainer.End();
                activeSustainer = null;
            }
        }

        /// <summary>
        /// Picks a new random nearby cell for the toddler to walk to.
        /// </summary>
        void PickNewTargetCell()
        {
            Map map = pawn.Map;
            if (!CellFinder.TryFindRandomCellNear(pawn.Position, map, 6,
                c => GenGrid.Standable(c, map)
                     && !c.GetTerrain(map).avoidWander
                     && !ForbidUtility.IsForbidden(c, pawn)
                     && c.InAllowedArea(pawn)
                     && c.DistanceTo(pawn.Position) <= MaxWanderDistance,
                out IntVec3 result))
            {
                // Fallback: just stay where we are.
                result = pawn.Position;
            }
            job.SetTarget(TargetIndex.A, result);
        }
    }

    // ===========================
    // SPEAKING PRACTICE (IN CRIB)
    // ===========================
    /// <summary>
    /// Toddler practices talking while lying in their crib. A continuous
    /// babble sustainer plays random clips from the
    /// <see cref="TPE_SoundDefOf.TPE_SpeechPracticeBabble"/> folder with a
    /// 1–2 second gap between each. The sustainer starts when the job begins
    /// and is forcibly ended when the job ends.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="JobDriver_ToddlerSelfSoothing"/> (crib-based,
    ///     stationary, CanBeginNowWhileLyingDown, KeepLyingDown).
    ///   - Differs in having no sub-activities, no wiggle animation, and no
    ///     mood-weighted thought system — it is purely ambient.
    /// </summary>
    public class JobDriver_ToddlerSpeechPracticeInCrib : JobDriver
    {
        const int MaxDuration = 1800; // 30 seconds max

        /// <summary>The crib this toddler is practicing in, if any.</summary>
        Building_Bed? Bed => job.GetTarget(TargetIndex.A).Thing as Building_Bed;

        /// <summary>True when the job was created with a valid bed target.</summary>
        bool FromBed => Bed != null;

        /// <summary>Currently playing babble sustainer, or null.</summary>
        Sustainer? activeSustainer;

        public override string GetReport()
        {
            return "practicing talking in crib";
        }

        /// <summary>
        /// Allows the job to start while the toddler is already lying down
        /// in the crib, preventing them from getting up just to re-lie down.
        /// </summary>
        public override bool CanBeginNowWhileLyingDown()
        {
            if (FromBed)
                return JobInBedUtility.InBedOrRestSpotNow(pawn, job.GetTarget(TargetIndex.A));
            return false;
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // No explicit reservation needed — the toddler is already in the
            // crib. KeepLyingDown handles returning them to bed after play.
            // Attempting to reserve here conflicts with the toddler's existing
            // LayDown/Sleep reservation on the same bed.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);

            // Safety net: ensure audio stops even if the job is interrupted
            // between toils (e.g. by drafting or a higher-priority emergency).
            AddFinishAction(delegate { EndBabbleSustainer(); });

            // Fail if the toddler leaves the crib or becomes downed.
            AddFailCondition(() => pawn.Downed || !CribUtility.InCrib(pawn));

            if (FromBed)
            {
                // When the job ends, automatically re-queue a LayDown job so
                // the toddler settles back into the crib instead of standing
                // up and wandering off. Mirrors Self Soothing behaviour.
                this.KeepLyingDown(TargetIndex.A);

                // We intentionally do NOT call ClaimBedIfNonMedical here.
                // Re-claiming during play causes ownership battles when two
                // toddlers share a crib.

                // GotoBed is a safe no-op when already in bed.
                yield return Toils_Bed.GotoBed(TargetIndex.A);
            }

            Toil toil;
            if (FromBed)
            {
                // Use the vanilla LayDown toil with sleep and rest gain
                // disabled — this keeps the toddler in bed posture and
                // handles all bed-related logic.
                toil = Toils_LayDown.LayDown(
                    TargetIndex.A,
                    hasBed: true,
                    lookForOtherJobs: false,
                    canSleep: false,
                    gainRestAndHealth: false);
            }
            else
            {
                // Fallback for the impossible case where no bed was targeted.
                toil = ToilMaker.MakeToil("SpeechPracticeInCrib");
                toil.initAction = delegate
                {
                    pawn.jobs.posture = PawnPosture.LayingOnGroundNormal;
                };
            }

            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = Rand.Range(1200, MaxDuration); // 20–30 seconds

            toil.AddPreInitAction(delegate
            {
                StartBabbleSustainer();
            });

            toil.AddPreTickAction(delegate
            {
                // Keep the sustainer alive — PerTick maintenance requires
                // this call every tick or the sound dies.
                activeSustainer?.Maintain();

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            });

            toil.AddFinishAction(delegate
            {
                EndBabbleSustainer();
            });

            yield return toil;
        }

        /// <summary>
        /// Spawns the babble sustainer so random clips from the ToddlerSpeech
        /// folder play continuously with 1–2 second gaps.
        /// </summary>
        void StartBabbleSustainer()
        {
            if (TPE_SoundDefOf.TPE_SpeechPracticeBabble != null)
            {
                SoundInfo info = SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map), MaintenanceType.PerTick);
                activeSustainer = TPE_SoundDefOf.TPE_SpeechPracticeBabble.TrySpawnSustainer(info);
            }
        }

        /// <summary>
        /// Forcibly ends the babble sustainer so audio stops the instant the
        /// toddler stops practicing.
        /// </summary>
        void EndBabbleSustainer()
        {
            if (activeSustainer != null)
            {
                activeSustainer.End();
                activeSustainer = null;
            }
        }
    }

    // ===========================
    // HEATER SNUGGLING
    // ===========================
    /// <summary>
    /// Toddler walks to an indoor heater and idles next to it for warmth when
    /// outdoor temperatures are low. If a plush toy was found by the play
    /// giver, the toddler picks it up before going to the heater and drops it
    /// when the activity ends.
    ///
    /// Job targets:
    ///   - TargetA = the heater (used for facing direction).
    ///   - TargetB = the cell next to the heater to stand on.
    ///   - TargetC = optional plush toy to pick up and carry.
    ///
    /// Vanilla comparison:
    ///   - Mirrors <see cref="JobDriver_ToddlerFiregazing"/> (base mod): both
    ///     use two targets (heat source + adjacent spot), face the heat source,
    ///     stand still for a duration, and use <c>ToilFailConditions</c>.
    ///   - Differs in optionally carrying a plush toy and using a random
    ///     duration (20–30 seconds) instead of a fixed 2000 ticks.
    /// </summary>
    public class JobDriver_ToddlerHeaterSnuggling : JobDriver
    {
        const int DurationMin = 1800; // 30 seconds
        const int DurationMax = 2700; // 45 seconds

        /// <summary>The heater this toddler is snuggling by.</summary>
        Thing Heater => job.GetTarget(TargetIndex.A).Thing;

        /// <summary>The cell next to the heater.</summary>
        IntVec3 Spot => job.GetTarget(TargetIndex.B).Cell;

        /// <summary>The optional plush toy to carry, or null.</summary>
        Thing? Toy => job.GetTarget(TargetIndex.C).Thing;

        /// <summary>True if this job was created with a toy target.</summary>
        bool HasToy => Toy != null;

        /// <summary>Tracks the toy so we can drop it even if the job ends early.</summary>
        Thing? trackedToy;

        public override string GetReport()
        {
            Thing carried = pawn.carryTracker?.CarriedThing;
            if (carried != null)
                return "snuggling by heater with " + carried.LabelShort;
            return "snuggling by heater";
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE_HEATER] TryMakePreToilReservations START for {pawn.NameShortColored} — heater={Heater?.LabelShort ?? "null"}, spot={Spot}, hasToy={HasToy}");

            // Reserve the heater so another toddler doesn't try to use it.
            bool heaterReserved = ReservationUtility.Reserve(pawn, TargetA, job, 1, -1, null, errorOnFailed, false);
            if (!heaterReserved)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] TryMakePreToilReservations FAILED: heater reservation failed for {Heater?.LabelShort ?? "null"}");
                return false;
            }

            // Reserve the snuggling spot.
            bool spotReserved = ReservationUtility.Reserve(pawn, TargetB, job, 1, -1, null, errorOnFailed, false);
            if (!spotReserved)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] TryMakePreToilReservations FAILED: spot reservation failed for {Spot}");
                return false;
            }

            // Optionally reserve the toy.  If the toy can't be reserved
            // (someone else took it, it was forbidden, etc.) we simply clear
            // the target and continue — the toy is purely cosmetic.
            if (HasToy)
            {
                bool toyReserved = ReservationUtility.Reserve(pawn, TargetC, job, 1, -1, null, errorOnFailed, false);
                if (!toyReserved)
                {
                    if (TPE_Mod.Settings?.debugLogging ?? false)
                        Log.Message($"[TPE_HEATER] TryMakePreToilReservations: toy reservation failed for {Toy?.LabelShort ?? "null"} — continuing without toy.");
                    job.SetTarget(TargetIndex.C, LocalTargetInfo.Invalid);
                }
            }

            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE_HEATER] TryMakePreToilReservations SUCCESS for {pawn.NameShortColored}");
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE_HEATER] MakeNewToils START for {pawn.NameShortColored} — heater={Heater?.LabelShort ?? "null"}, spot={Spot}, hasToy={HasToy}");

            // Job-level safety net: drop the toy whenever the job ends,
            // even if it never reached the snuggle toil (e.g. interrupted
            // while walking to the heater).
            AddFinishAction((JobCondition _) => DropAndUnforbidTrackedToy());

            this.FailOnDestroyedOrNull(TargetIndex.A);
            ToilFailConditions.FailOnForbidden(this, TargetIndex.A);
            ToilFailConditions.FailOnForbidden(this, TargetIndex.B);
            ToilFailConditions.FailOnChildLearningConditions(this);

            // If we have a toy, pick it up first.
            if (HasToy)
            {
                this.FailOnDestroyedOrNull(TargetIndex.C);
                ToilFailConditions.FailOnForbidden(this, TargetIndex.C);
                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.C, putRemainderInQueue: false, subtractNumTakenFromJobCount: true, failIfStackCountLessThanJobCount: false, reserve: false);
                trackedToy = Toy;
            }

            // Walk to the snuggling spot next to the heater.
            if (TPE_Mod.Settings?.debugLogging ?? false)
                Log.Message($"[TPE_HEATER] MakeNewToils: yielding GotoCell to {Spot}");
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // Stand by the heater and face it.
            Toil snuggleToil = ToilMaker.MakeToil("HeaterSnuggling");
            snuggleToil.initAction = delegate
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.Message($"[TPE_HEATER] Snuggle toil initAction for {pawn.NameShortColored} — facing {Heater.Position}, duration={snuggleToil.defaultDuration}");
                pawn.jobs.posture = PawnPosture.Standing;
            };
            snuggleToil.handlingFacing = true;
            snuggleToil.tickAction = delegate
            {
                // Face the heater.
                pawn.rotationTracker.FaceCell(Heater.Position);
                if ((TPE_Mod.Settings?.debugLogging ?? false) && Find.TickManager.TicksGame % 60 == 0)
                    Log.Message($"[TPE_HEATER] Snuggle toil tick {Find.TickManager.TicksGame} for {pawn.NameShortColored}");
                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            snuggleToil.defaultCompleteMode = ToilCompleteMode.Delay;
            snuggleToil.defaultDuration = Rand.Range(DurationMin, DurationMax);
            snuggleToil.AddFinishAction(delegate
            {
                DropAndUnforbidTrackedToy();
            });
            yield return snuggleToil;
        }

        /// <summary>
        /// Drops the carried toy at the toddler's feet and un-forbids it so
        /// other toddlers can play with it. Safe to call even if no toy was
        /// tracked or the toy was already dropped.
        ///
        /// Uses <see cref="Toy"/> (the job target) as a fallback because
        /// <c>trackedToy</c> is set in <see cref="MakeNewToils"/> and is
        /// NOT re-initialised after a save/load — the job target survives
        /// serialization, the local field does not.
        /// </summary>
        void DropAndUnforbidTrackedToy()
        {
            // Job target (TargetC) survives save/load; trackedToy does not.
            Thing? toy = trackedToy ?? Toy;
            if (toy != null)
            {
                if (pawn.carryTracker?.CarriedThing == toy)
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                TPE_ToyUtility.Unforbid(toy);
            }
            trackedToy = null;
        }
    }

    // ===========================
    // POPPING BUBBLES
    // ===========================
    /// <summary>
    /// Crawler-only toddler wanders inside an active Bubble Machine's zone, pausing
    /// occasionally to "pop" a bubble. Movement is copied from
    /// <see cref="JobDriver_ChildBubblePopping"/> (the child "Bubble Popping" play
    /// desire) — deliberately does NOT track or interact with any individual
    /// <see cref="Mote_Bubble"/>; the pause itself simulates the popping action.
    /// </summary>
    public class JobDriver_ToddlerPoppingBubbles : JobDriver
    {
        /// <summary>The Bubble Machine whose zone the toddler is playing in (TargetA).</summary>
        Thing BubbleMachine => job.GetTarget(TargetIndex.A).Thing;

        /// <summary>
        /// The machine's zone comp, cached once at job start. Reused every tick to check
        /// <see cref="CompBubbleZone.IsActive"/> and to pick new wander destinations via
        /// <see cref="CompBubbleZone.TryGetRandomZoneCell"/>.
        /// </summary>
        CompBubbleZone? zoneComp;

        /// <summary>Minimum ticks to stand still after arriving, simulating a bubble pop. Copied from JobDriver_ChildBubblePopping.</summary>
        const int PopPauseMinTicks = 20; // ~0.33s

        /// <summary>Maximum ticks to stand still after arriving, simulating a bubble pop. Copied from JobDriver_ChildBubblePopping.</summary>
        const int PopPauseMaxTicks = 50; // ~0.83s

        /// <summary>How many candidate cells CompBubbleZone.TryGetRandomZoneCell tries before giving up on a destination pick.</summary>
        const int DestinationPickAttempts = 10;

        /// <summary>Ticks remaining in the current "pop" pause. 0 means the toddler should be crawling toward its next destination.</summary>
        int pauseTicksLeft;

        public override string GetReport()
        {
            return "Popping bubbles";
        }

        /// <summary>
        /// No reservation needed — several toddlers can wander the same bubble zone at
        /// once without conflict. Matches JobDriver_ChildBubblePopping's exact convention
        /// for open-space bubble-zone activities.
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);

            // Job-wide safety net: end the job if the machine despawns/is destroyed at
            // any point. Matches JobDriver_ChildBubblePopping's convention for jobs
            // anchored to a Thing target.
            this.FailOnDespawnedOrNull(TargetIndex.A);

            zoneComp = (BubbleMachine as ThingWithComps)?.GetComp<CompBubbleZone>();

            Toil popToil = ToilMaker.MakeToil("ToddlerPoppingBubbles");
            popToil.initAction = delegate
            {
                pauseTicksLeft = 0;
                PickNewDestination();
            };
            popToil.tickAction = delegate
            {
                // End early if the machine has gone inactive (powered off / switched off) —
                // an idle machine isn't producing bubbles to pop anymore. Mirrors
                // JobDriver_ChildBubblePopping's "end early if the machine deactivates" check.
                if (zoneComp == null || !zoneComp.IsActive)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                // Fill the toddler play need every tick. ToddlerPlayUtility.ToddlerPlayTickCheckEnd
                // ends the job itself (JobCondition.Succeeded) once the need is satisfied —
                // matches every other toddler job driver's per-tick call convention.
                if (ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn))
                    return;

                if (pauseTicksLeft > 0)
                {
                    // Currently "popping" a bubble — stand still.
                    pauseTicksLeft--;
                    if (pauseTicksLeft <= 0)
                    {
                        PickNewDestination();
                    }
                    return;
                }

                if (!pawn.pather.Moving)
                {
                    // Arrived at the destination (or never got a valid one to begin with) —
                    // pause briefly to simulate popping a bubble before moving again.
                    pauseTicksLeft = Rand.Range(PopPauseMinTicks, PopPauseMaxTicks);
                }
            };
            // No fixed defaultDuration — matches JobDriver_ChildBubblePopping's
            // ToilCompleteMode.Never; the job only ends via ToddlerPlayTickCheckEnd
            // (need satisfied) or the EndCurrentJob(Incompletable) call above.
            popToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return popToil;

            AddFinishAction(delegate
            {
                // Stop any remaining path movement so the toddler doesn't drift after the
                // job ends, matching JobDriver_ChildBubblePopping's finish-action cleanup.
                pawn.pather?.StopDead();
            });
        }

        /// <summary>
        /// Picks a new random cell inside the machine's CURRENT bubble zone. Copied from
        /// <see cref="JobDriver_ChildBubblePopping.PickNewDestination"/> — see that class
        /// for full reasoning. Gracefully no-ops if no valid cell is found this attempt.
        /// </summary>
        void PickNewDestination()
        {
            if (zoneComp != null && zoneComp.TryGetRandomZoneCell(IsValidWanderCell, out IntVec3 dest, DestinationPickAttempts))
            {
                job.targetB = dest;
                pawn.pather.StartPath(dest, PathEndMode.OnCell);
            }
        }

        /// <summary>
        /// Returns true if the cell is suitable for a toddler to crawl to and settle on:
        /// standable and not forbidden. Copied from
        /// <see cref="JobDriver_ChildBubblePopping.IsValidWanderCell"/>.
        /// </summary>
        bool IsValidWanderCell(IntVec3 c)
        {
            Map map = pawn.Map;
            if (!GenGrid.Standable(c, map))
                return false;
            if (ForbidUtility.IsForbidden(c, pawn))
                return false;
            return true;
        }
    }

    // ===========================
    // CHASING BUBBLES
    // ===========================
    /// <summary>
    /// Walker-only toddler runs over to an active Bubble Machine, then runs around near
    /// it with a temporary speed boost hediff. The running movement itself is copied
    /// VERBATIM from <see cref="JobDriver_ToddlerRunning"/> — see that class for the
    /// original reasoning. The only additions here are the initial walk to the machine
    /// and the early-end check if the machine goes inactive mid-job.
    /// </summary>
    public class JobDriver_ToddlerChasingBubbles : JobDriver
    {
        /// <summary>The Bubble Machine the toddler first ran to before starting the chase (TargetA).</summary>
        Thing BubbleMachine => job.GetTarget(TargetIndex.A).Thing;

        /// <summary>
        /// The machine's zone comp, cached once at job start. Used only to check
        /// <see cref="CompBubbleZone.IsActive"/> each tick — the chase movement below
        /// does NOT use the zone's cell list at all (matches the ask: copy Running
        /// Around's movement, do not confine to / chase actual bubble motes).
        /// </summary>
        CompBubbleZone? zoneComp;

        int targetCellTicks;
        const int TargetRefreshInterval = 90; // new target every ~1.5 sec — copied from JobDriver_ToddlerRunning.
        const int MaxDuration = 1800; // copied from JobDriver_ToddlerRunning.
        const float MaxRunDistance = 15f; // copied from JobDriver_ToddlerRunning.

        public override string GetReport()
        {
            return "Chasing bubbles";
        }

        /// <summary>
        /// No reservation on the machine — several toddlers can share the same bubble
        /// zone / chase around the same machine at once, matching every other bubble
        /// activity's "open space, no reservation" convention.
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            ToilFailConditions.FailOnChildLearningConditions(this);

            // Job-wide safety net: end the job if the machine despawns/is destroyed at
            // any point (matches JobDriver_ToddlerPoppingBubbles's convention for jobs
            // anchored to a Thing target).
            this.FailOnDespawnedOrNull(TargetIndex.A);

            zoneComp = (BubbleMachine as ThingWithComps)?.GetComp<CompBubbleZone>();

            // Walk over to the machine first so the "chase" visibly starts near the
            // bubbles, then hand off to the exact same running-around toil below.
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            targetCellTicks = TargetRefreshInterval;

            Toil runToil = ToilMaker.MakeToil("ChasingBubbles");
            runToil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
                PickNewTargetCell();
                // Apply speed boost hediff — copied verbatim from JobDriver_ToddlerRunning.
                var hediff = HediffMaker.MakeHediff(TPE_HediffDefOf.TPE_Running, pawn);
                pawn.health.AddHediff(hediff);
            };
            runToil.tickAction = delegate
            {
                // End early if the machine has gone inactive (powered off / switched off) —
                // an idle machine isn't producing bubbles to chase anymore. The ONLY addition
                // vs. JobDriver_ToddlerRunning's tick logic below.
                if (zoneComp == null || !zoneComp.IsActive)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                if (pawn.jobs.curDriver.ticksLeftThisToil <= 0)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    return;
                }

                if (pawn.pather.Moving)
                {
                    ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
                    return;
                }

                targetCellTicks--;
                if (targetCellTicks <= 0 || pawn.Position == TargetB.Cell)
                {
                    targetCellTicks = TargetRefreshInterval;
                    PickNewTargetCell();
                }

                if (TargetB.Cell.IsValid && TargetB.Cell != pawn.Position)
                {
                    pawn.pather.StartPath(TargetB.Cell, PathEndMode.OnCell);
                }

                ToddlerPlayUtility.ToddlerPlayTickCheckEnd(pawn);
            };
            runToil.AddFinishAction(delegate
            {
                // Remove speed boost hediff — copied verbatim from JobDriver_ToddlerRunning.
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(TPE_HediffDefOf.TPE_Running);
                if (hediff != null)
                    pawn.health.RemoveHediff(hediff);
            });
            runToil.defaultCompleteMode = ToilCompleteMode.Delay;
            runToil.defaultDuration = MaxDuration;
            yield return runToil;
        }

        /// <summary>
        /// Picks a random nearby standable, unforbidden, in-allowed-area cell within
        /// <see cref="MaxRunDistance"/> tiles for the toddler to run to next. Copied
        /// verbatim from <see cref="JobDriver_ToddlerRunning.PickNewTargetCell"/> —
        /// searches relative to the toddler's CURRENT position (not the machine), so the
        /// chase naturally drifts exactly the way ordinary Running Around does. Falls
        /// back to the toddler's current position (never an off-map/invalid cell) if no
        /// candidate is found.
        /// </summary>
        void PickNewTargetCell()
        {
            Map map = pawn.Map;
            if (!CellFinder.TryFindRandomCellNear(pawn.Position, map, 8,
                c => GenGrid.Standable(c, map)
                     && !c.GetTerrain(map).avoidWander
                     && !ForbidUtility.IsForbidden(c, pawn)
                     && c.InAllowedArea(pawn)
                     && c.DistanceTo(pawn.Position) <= MaxRunDistance, out IntVec3 result))
            {
                result = pawn.Position;
            }
            job.targetB = result;
        }
    }


}
