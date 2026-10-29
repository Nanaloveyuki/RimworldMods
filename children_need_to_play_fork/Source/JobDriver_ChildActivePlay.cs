using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Job driver for child "Active Play" — an umbrella activity where children
    /// cycle between several energetic sub-activities at random:
    /// <list type="bullet">
    ///   <item><description>Running Around — speed boost + random wandering</description></item>
    ///   <item><description>Spinning Around — rapid in-place rotation via AnimationDef</description></item>
    ///   <item><description>Jumping Up and Down — vertical hops via AnimationDef</description></item>
    ///   <item><description>Snail Crawling — very slow straight-line movement (1 tile / 10 sec)</description></item>
    ///   <item><description>People Chasing — follow and catch another pawn (with running speed)</description></item>
    /// </list>
    /// The job uses an internal state machine within a single toil.
    /// Every ~25 seconds it randomly switches to a new sub-activity.
    /// Play need fills gradually throughout the session.
    /// </summary>
    public class JobDriver_ChildActivePlay : JobDriver
    {
        // ==========================================================
        // Static tracking — which children are currently running around
        // ==========================================================
        /// <summary>
        /// Set of all pawns currently in the Running sub-activity.
        /// Other Active Play children scan this set to find someone to chase.
        /// </summary>
        public static readonly HashSet<Pawn> CurrentlyRunningChildren = new HashSet<Pawn>();
        // ==========================================================
        // Sub-activity enum
        // ==========================================================
        /// <summary>
        /// The five possible sub-activities within active play.
        /// </summary>
        enum SubActivity
        {
            Running,
            Spinning,
            Jumping,
            SnailCrawling,
            PeopleChasing
        }

        // ==========================================================
        // Constants
        // ==========================================================
        /// <summary>
        /// How many ticks before potentially switching sub-activities.
        /// Set to ~40 seconds so children stay in each activity long enough
        /// for the animation and behaviour to be visually distinct.
        /// </summary>
        const int ActivitySwitchInterval = 2500; // ~40 seconds

        /// <summary>Duration of the PeopleChasing sub-activity (longer so kids have time to catch up).</summary>
        const int ChaseActivityDuration = 4500; // ~75 seconds

        /// <summary>How often to scan for running children to chase reactively.</summary>
        const int ReactiveChaseCheckInterval = 180; // ~3 seconds

        /// <summary>How often to check if a better chase target has appeared.</summary>
        const int BetterTargetCheckInterval = 300; // ~5 seconds

        /// <summary>Maximum distance to initiate a reactive chase on a running child.</summary>
        const float ReactiveChaseMaxDistance = 25f;

        /// <summary>Maximum total duration of the active play session.</summary>
        const int MaxDurationTicks = 2500; // ~40 seconds

        /// <summary>How many jump cycles to perform per jumping segment.</summary>
        const int JumpCyclesPerSegment = 3;

        /// <summary>How many ticks each jump cycle lasts.</summary>
        const int JumpCycleTicks = 120; // ~2 seconds per spot

        /// <summary>How many ticks the child spins at each spot before moving on.</summary>
        const int SpinSpotDuration = 600; // ~10 seconds per spot

        /// <summary>How close the chaser must get to "catch" the target.</summary>
        const float ChaseCatchDistance = 2.5f;

        /// <summary>
        /// When the chaser is within this distance of the target, they orbit
        /// around the target instead of pathing directly to them. This prevents
        /// children from clumping together and standing still.
        /// </summary>
        const float OrbitRadius = 5f;

        /// <summary>How often to re-path while chasing.</summary>
        const int ChaseRepathInterval = 60; // every 1 second

        /// <summary>Radius for picking random wander destinations.</summary>
        const int WanderRadius = 8;

        /// <summary>Maximum distance for running destinations.</summary>
        const float MaxRunDistance = 15f;

        // (TicksPerSnailTile removed — snail crawling now uses the pathing system
        // with a strong slow-speed hediff instead of manual tile stepping.)

        // ==========================================================
        // State fields
        // ==========================================================
        /// <summary>Currently active sub-activity.</summary>
        SubActivity currentActivity;

        /// <summary>Ticks remaining in the current sub-activity before a switch is considered.</summary>
        int activityTicksLeft;

        /// <summary>Total ticks this job has been running.</summary>
        int totalTicksPlayed;

        /// <summary>Jump cycles remaining in the current jumping segment.</summary>
        int jumpCyclesLeft;

        /// <summary>Ticks remaining in the current jump cycle.</summary>
        int jumpSpotTicksLeft;

        /// <summary>Ticks remaining in the current spin spot. Zero or negative means walking to next spot.</summary>
        int spinSpotTicksLeft;

        /// <summary>Current chase target (null if not chasing).</summary>
        Pawn? chaseTarget;

        /// <summary>Ticks until next chase re-path.</summary>
        int chaseRepathTicks;

        /// <summary>Hediff currently applied for movement modifiers (running).</summary>
        Hediff? appliedMovementHediff;

        /// <summary>
        /// Destination cell for snail crawling. The pawn paths toward this cell
        /// using the normal pathfinder, but with a strong slow-speed hediff that
        /// reduces MoveSpeed to its engine minimum (~0.15 c/s).
        /// </summary>
        IntVec3 snailDest;

        /// <summary>
        /// Ticks until the next snail-crawling destination pick.
        /// Prevents spamming path requests when the pawn is temporarily
        /// blocked or when the pathfinder needs time to compute a route.
        /// </summary>
        int snailRepathCooldown;

        /// <summary>Ticks until next reactive-chase scan.</summary>
        int reactiveChaseCheckTicks;

        /// <summary>Ticks until next better-target evaluation while chasing.</summary>
        int betterTargetCheckTicks;

        /// <summary>
        /// Cached orbit destination while circling a chase target.
        /// Invalid when not orbiting.
        /// </summary>
        IntVec3 orbitDest;

        // ==========================================================
        // Job overrides
        // ==========================================================
        /// <summary>
        /// Dynamic report string that reflects the current sub-activity.
        /// </summary>
        public override string GetReport()
        {
            return currentActivity switch
            {
                SubActivity.Running => "TPE_Report_Running".Translate(),
                SubActivity.Spinning => "TPE_Report_Spinning".Translate(),
                SubActivity.Jumping => "TPE_Report_Jumping".Translate(),
                SubActivity.SnailCrawling => "TPE_Report_SnailCrawling".Translate(),
                SubActivity.PeopleChasing => chaseTarget != null
                    ? "TPE_Report_ChasingPawn".Translate(chaseTarget.NameShortColored)
                    : "TPE_Report_ChasingSomeone".Translate(),
                _ => "TPE_Report_ActivePlay".Translate()
            };
        }

        /// <summary>
        /// Active Play does not need to reserve anything — the child moves
        /// dynamically and the starting cell is only used for the initial walk.
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        /// <summary>
        /// Builds the toil sequence: walk to start cell, then enter the
        /// main active-play state-machine toil.
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // --- Step 1: Go to a good open starting spot ---
            // BUGFIX (save-load): this used to call FindStartCell() EAGERLY here and
            // yield the goto toil only when the cell differed from the pawn's position.
            // MakeNewToils also runs when a save is LOADED (JobDriver.ExposeData →
            // SetupToils during PostLoadInit), before the pawn is spawned, so
            // FindStartCell → IsValidPlayCell dereferenced a null map (NRE → toils
            // never built → job killed on load), and the conditional yield could change
            // the toil COUNT between save and load. Now: the search happens inside an
            // initAction (only ever on a fresh run, pawn spawned), and the goto toil is
            // ALWAYS present. Going to your own cell is harmless: Pawn_PathFollower.StartPath
            // arrives immediately when already at the destination.
            Toil pickStartCell = ToilMaker.MakeToil("PickActivePlayStartCell");
            pickStartCell.initAction = () =>
            {
                IntVec3 startCell = FindStartCell();
                job.targetA = startCell.IsValid ? startCell : pawn.Position;
            };
            pickStartCell.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pickStartCell;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            // --- Step 2: Main active play toil (state machine) ---
            Toil activePlayToil = ToilMaker.MakeToil("ActivePlay");

            activePlayToil.initAction = () =>
            {
                totalTicksPlayed = 0;
                SwitchToRandomSubActivity();
            };

            activePlayToil.tickAction = () =>
            {
                totalTicksPlayed++;

                // End job if play need is filled or max duration reached
                if (PlayUtility.PlayTickCheckEnd(pawn, 0.35f / 2500f)
                    || totalTicksPlayed >= MaxDurationTicks)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                // Decrement sub-activity timer and switch if expired
                activityTicksLeft--;
                if (activityTicksLeft <= 0)
                {
                    SwitchToRandomSubActivity();
                }

                // Reactive chasing: if another child is running around,
                // switch to chasing them (creates emergent "tag" games).
                if (currentActivity != SubActivity.PeopleChasing)
                {
                    reactiveChaseCheckTicks--;
                    if (reactiveChaseCheckTicks <= 0)
                    {
                        reactiveChaseCheckTicks = ReactiveChaseCheckInterval;
                        Pawn? runningChild = FindBestRunningChildToChase();
                        if (runningChild != null)
                        {
                            CleanupHediffs();
                            CleanupAnimation();
                            currentActivity = SubActivity.PeopleChasing;
                            InitPeopleChasing(runningChild);
                        }
                    }
                }

                // Execute one tick of the current sub-activity
                ExecuteSubActivityTick();
            };

            activePlayToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return activePlayToil;

            // --- Job-level finish action: runs when the entire job ends ---
            AddFinishAction((JobCondition condition) =>
            {
                // Stop any remaining path movement so the child doesn't drift
                // after the job ends (e.g. running or snail-crawling momentum).
                pawn.pather?.StopDead();

                // Apply playground buff if the job ended successfully in a playground
                if (condition == JobCondition.Succeeded)
                {
                    Room? room = pawn.GetRoom();
                    if (room != null && room.Role != null
                        && room.Role.defName == "TPE_Playground")
                    {
                        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(
                            TPE_ThoughtDefOf.TPE_PlayedInPlayground);
                    }
                }

                CleanupHediffs();
                CleanupAnimation();
            });
        }

        // ==========================================================
        // Sub-activity switching
        // ==========================================================
        /// <summary>
        /// Randomly selects a new sub-activity, cleans up the old one
        /// (hediffs + animation), and initializes the new one.
        /// Avoids picking the same activity twice in a row when possible.
        /// </summary>
        void SwitchToRandomSubActivity()
        {
            // Unregister from running tracker before switching
            CurrentlyRunningChildren.Remove(pawn);

            // Clear chase-specific state
            orbitDest = IntVec3.Invalid;

            // Clean up old activity state
            CleanupHediffs();
            CleanupAnimation();

            // Pick a new random sub-activity, trying to avoid repeats
            SubActivity oldActivity = currentActivity;
            SubActivity newActivity = oldActivity;
            int attempts = 0;
            while (newActivity == oldActivity && attempts < 5)
            {
                newActivity = (SubActivity)Rand.Range(0, 5);
                attempts++;
            }
            currentActivity = newActivity;
            activityTicksLeft = ActivitySwitchInterval;

            // Initialize the new sub-activity
            switch (currentActivity)
            {
                case SubActivity.Running:
                    InitRunning();
                    break;
                case SubActivity.Spinning:
                    InitSpinning();
                    break;
                case SubActivity.Jumping:
                    InitJumping();
                    break;
                case SubActivity.SnailCrawling:
                    InitSnailCrawling();
                    break;
                case SubActivity.PeopleChasing:
                    InitPeopleChasing();
                    break;
            }
        }

        // ==========================================================
        // Running Around initialization & tick
        // ==========================================================
        /// <summary>
        /// Applies the running speed-boost hediff and picks the first
        /// random destination.
        /// </summary>
        void InitRunning()
        {
            // Register so other Active Play children can see us and chase
            CurrentlyRunningChildren.Add(pawn);

            appliedMovementHediff = HediffMaker.MakeHediff(TPE_HediffDefOf.TPE_Running, pawn);
            pawn.health.AddHediff(appliedMovementHediff);
            PickNewRunDestination();
        }

        /// <summary>
        /// Picks a new random reachable cell within wander radius and
        /// starts the pawn pathing toward it.
        /// </summary>
        void PickNewRunDestination()
        {
            IntVec3 dest = PickNearbyCell(WanderRadius, MaxRunDistance);
            if (dest.IsValid)
            {
                job.targetA = dest;
                pawn.pather.StartPath(dest, PathEndMode.OnCell);
            }
        }

        /// <summary>
        /// Per-tick update for Running: if the pawn reaches its destination,
        /// pick a new one immediately.
        /// </summary>
        void TickRunning()
        {
            if (!pawn.pather.Moving)
            {
                PickNewRunDestination();
            }
        }

        // ==========================================================
        // Spinning Around initialization & tick
        // ==========================================================
        /// <summary>
        /// <summary>
        /// Initializes the spinning sub-activity. The child paths to a nearby
        /// cell first; the spin animation only starts once they arrive. After
        /// spinning for <see cref="SpinSpotDuration"/> ticks they walk to a
        /// new nearby spot and spin again, matching the toddler behaviour
        /// pattern (move → spin → move → spin).
        /// </summary>
        void InitSpinning()
        {
            spinSpotTicksLeft = 0;
            // (A redundant unconditional SetAnimation(null) used to sit here: the only
            // caller, SwitchToRandomSubActivity, has already run CleanupAnimation, so it
            // only cost an extra render-tree rebuild per spin segment.)

            IntVec3 dest = PickNearbyCell(3, 5f);
            if (dest.IsValid && dest != pawn.Position)
            {
                job.targetA = dest;
                pawn.pather.StartPath(dest, PathEndMode.OnCell);
            }
            else
            {
                // Already at a valid spot — start spinning immediately (the spin
                // visual only if child play animations are allowed; with them off the
                // child just stands here facing forward for the spin duration).
                spinSpotTicksLeft = SpinSpotDuration;
                pawn.Rotation = Rot4.South;
                PlayAnimationUtility.MaintainChildAnimation(pawn, TPE_AnimationDefOf.TPE_ChildSpinning);
            }
        }

        /// <summary>
        /// Per-tick update for Spinning: when <see cref="spinSpotTicksLeft"/>
        /// is positive the child is actively spinning at the current spot
        /// (facing locked to South). Once the timer reaches zero the animation
        /// is cleared and the child paths to a new nearby cell. When they
        /// arrive the timer resets and spinning resumes.
        /// </summary>
        void TickSpinning()
        {
            if (spinSpotTicksLeft > 0)
            {
                // Actively spinning at the current spot.
                pawn.Rotation = Rot4.South;
                // Keep/stop the spin visual if the setting is flipped mid-spin
                // (no-op otherwise).
                PlayAnimationUtility.MaintainChildAnimation(pawn, TPE_AnimationDefOf.TPE_ChildSpinning);
                spinSpotTicksLeft--;
                if (spinSpotTicksLeft <= 0)
                {
                    // Spin finished — start walking to the next spot (clear only our
                    // spin; nothing to clear while animations are disabled).
                    PlayAnimationUtility.ClearIfCurrent(pawn, TPE_AnimationDefOf.TPE_ChildSpinning);
                    IntVec3 dest = PickNearbyCell(3, 5f);
                    if (dest.IsValid && dest != pawn.Position)
                    {
                        job.targetA = dest;
                        pawn.pather.StartPath(dest, PathEndMode.OnCell);
                    }
                }
            }
            else
            {
                // Walking to the next spin spot — begin spinning on arrival.
                if (!pawn.pather.Moving)
                {
                    spinSpotTicksLeft = SpinSpotDuration;
                    pawn.Rotation = Rot4.South;
                    PlayAnimationUtility.MaintainChildAnimation(pawn, TPE_AnimationDefOf.TPE_ChildSpinning);
                }
            }
        }

        // ==========================================================
        // Jumping Up and Down initialization & tick
        // ==========================================================
        /// <summary>
        /// Initializes jump state for 3 cycles at the current spot and
        /// starts the jumping animation (TPE_ChildJumping).
        /// </summary>
        void InitJumping()
        {
            jumpCyclesLeft = JumpCyclesPerSegment;
            jumpSpotTicksLeft = JumpCycleTicks;
            pawn.pather.StopDead();
            // The jump AnimationDef is only a marker — the visible bounce is added by
            // Patch_PawnRenderer_GetBodyPos while it is the current animation. Not set
            // at all while child play animations are disabled (child stands still).
            PlayAnimationUtility.MaintainChildAnimation(pawn, TPE_AnimationDefOf.TPE_ChildJumping);
        }

        /// <summary>
        /// Per-tick update for Jumping: only counts down while the pawn is
        /// stationary. When a jump cycle ends, the child paths to a new nearby
        /// spot; jumping resumes once they arrive. After all cycles finish,
        /// the job switches to a new sub-activity.
        /// </summary>
        void TickJumping()
        {
            // While walking to the next spot, pause the jump animation
            // so the child doesn't hop mid-stride.
            if (pawn.pather.Moving)
            {
                PlayAnimationUtility.ClearIfCurrent(pawn, TPE_AnimationDefOf.TPE_ChildJumping);
                return;
            }

            // Restore jump animation when stationary — or keep it off while child
            // play animations are disabled (same per-tick rule as before, now gated).
            PlayAnimationUtility.MaintainChildAnimation(pawn, TPE_AnimationDefOf.TPE_ChildJumping);

            jumpSpotTicksLeft--;
            if (jumpSpotTicksLeft <= 0)
            {
                jumpCyclesLeft--;
                if (jumpCyclesLeft <= 0)
                {
                    // All jump cycles finished — pick a new activity
                    SwitchToRandomSubActivity();
                    return;
                }

                // Move to a new spot; jumping will resume once we arrive
                IntVec3 newSpot = PickNearbyCell(3, 5f);
                if (newSpot.IsValid && newSpot != pawn.Position)
                {
                    job.targetA = newSpot;
                    pawn.pather.StartPath(newSpot, PathEndMode.OnCell);
                }
                // Reset timer for the next stationary jump session
                jumpSpotTicksLeft = JumpCycleTicks;
            }
        }

        // ==========================================================
        // Snail Crawling initialization & tick
        // ==========================================================
        /// <summary>
        /// Picks an axis-aligned destination up to 10 tiles away, clears any
        /// stale job targets, applies the snail-crawling slow hediff, and
        /// starts pathing toward the destination.
        ///
        /// The slow hediff reduces MoveSpeed to the engine minimum (~0.15 c/s),
        /// so the child visibly crawls along the path at a snail's pace.
        /// Using the normal pathfinder avoids all the visual glitches that
        /// manual position-setting caused (teleporting, stale path indicators,
        /// tweener desync).
        /// </summary>
        /// <summary>
        /// Minimum ticks between snail-crawling destination picks.
        /// Prevents path-request spam when the pawn is trapped or the
        /// pathfinder fails to find a route.
        /// </summary>
        const int SnailRepathInterval = 60; // ~1 second

        /// <summary>
        /// Picks an axis-aligned destination up to 10 tiles away, clears any
        /// stale job targets, applies the snail-crawling slow hediff, and
        /// starts pathing toward the destination.
        ///
        /// The slow hediff reduces MoveSpeed to the engine minimum (~0.15 c/s),
        /// so the child visibly crawls along the path at a snail's pace.
        /// Using the normal pathfinder avoids all the visual glitches that
        /// manual position-setting caused (teleporting, stale path indicators,
        /// tweener desync).
        /// </summary>
        void InitSnailCrawling()
        {
            // Clear stale targets from previous sub-activities so no yellow
            // path indicators point to old cells.
            job.targetA = LocalTargetInfo.Invalid;
            job.targetB = LocalTargetInfo.Invalid;

            // Apply the strong slow-speed hediff
            appliedMovementHediff = HediffMaker.MakeHediff(TPE_HediffDefOf.TPE_SnailCrawling, pawn);
            pawn.health.AddHediff(appliedMovementHediff);

            // Pick a destination and start pathing
            snailRepathCooldown = 0;
            snailDest = PickSnailDestination();
            if (snailDest.IsValid)
            {
                pawn.pather.StartPath(snailDest, PathEndMode.OnCell);
            }

            // Start the crawling lean animation — only if child play animations are
            // allowed (otherwise the child walks the same slow path upright).
            PlayAnimationUtility.MaintainChildAnimation(pawn, TPE_AnimationDefOf.TPE_ChildSnailCrawling);
        }

        /// <summary>
        /// Per-tick update for Snail Crawling: if the pawn has finished pathing
        /// to the current destination, pick a new axis-aligned destination and
        /// start pathing again. The pather handles all visual interpolation,
        /// facing, and tweening automatically.
        ///
        /// A cooldown ensures we don't spam path requests when the pawn is
        /// temporarily blocked or trapped.
        /// </summary>
        void TickSnailCrawling()
        {
            // Keep/stop the lean if the setting is flipped mid-crawl (no-op otherwise).
            PlayAnimationUtility.MaintainChildAnimation(pawn, TPE_AnimationDefOf.TPE_ChildSnailCrawling);

            snailRepathCooldown--;
            if (snailRepathCooldown <= 0 && !pawn.pather.Moving)
            {
                snailRepathCooldown = SnailRepathInterval;
                snailDest = PickSnailDestination();
                if (snailDest.IsValid)
                {
                    pawn.pather.StartPath(snailDest, PathEndMode.OnCell);
                }
            }
        }

        /// <summary>
        /// Picks a cell up to 10 tiles away that lies on the same X or Z axis
        /// as the pawn (straight horizontal or vertical line).
        /// Tries up to 12 random candidates and returns the first valid one.
        /// </summary>
        IntVec3 PickSnailDestination()
        {
            Map map = pawn.Map;
            IntVec3 current = pawn.Position;

            for (int i = 0; i < 12; i++)
            {
                bool sameZ = Rand.Bool; // true = horizontal movement (varying X)
                int distance = Rand.Range(3, 11); // 3–10 tiles
                if (Rand.Bool) distance = -distance;

                IntVec3 dest = sameZ
                    ? new IntVec3(current.x + distance, 0, current.z)
                    : new IntVec3(current.x, 0, current.z + distance);

                if (dest.InBounds(map) && IsValidPlayCell(dest))
                {
                    return dest;
                }
            }

            return IntVec3.Invalid;
        }

        // ==========================================================
        // People Chasing initialization & tick
        // ==========================================================
        /// <summary>
        /// Finds a chase target (or uses a preferred one), applies the running
        /// speed hediff so the child runs while chasing, and starts pathing
        /// toward the target. Chasing lasts longer than other sub-activities
        /// so the child has enough time to reach the target.
        /// If no valid target exists, forces an immediate activity switch.
        /// </summary>
        void InitPeopleChasing(Pawn? preferredTarget = null)
        {
            // Apply running hediff so chasing is visually energetic
            appliedMovementHediff = HediffMaker.MakeHediff(TPE_HediffDefOf.TPE_Running, pawn);
            pawn.health.AddHediff(appliedMovementHediff);

            // Use the preferred target (e.g. a running child) or find one
            chaseTarget = preferredTarget ?? FindChaseTarget();
            chaseRepathTicks = 0;
            betterTargetCheckTicks = BetterTargetCheckInterval;
            orbitDest = IntVec3.Invalid;

            if (chaseTarget != null)
            {
                job.targetB = chaseTarget;
                pawn.pather.StartPath(chaseTarget.Position, PathEndMode.Touch);
            }
            else
            {
                // No valid target — force a switch next tick.
                activityTicksLeft = 1;
                return;
            }

            // Chasing gets its own longer duration
            activityTicksLeft = ChaseActivityDuration;
        }

        /// <summary>
        /// Per-tick update for People Chasing: keeps pathing toward the target.
        /// When within <see cref="OrbitRadius"/> tiles, the child runs in a
        /// circle around the target instead of standing on top of them. This
        /// prevents multiple children from clumping together motionless.
        /// Target switching only happens when the current one becomes invalid
        /// OR a significantly better target appears.
        /// </summary>
        void TickPeopleChasing()
        {
            if (chaseTarget == null || chaseTarget.Dead || !chaseTarget.Spawned || chaseTarget.Downed)
            {
                // Target lost — pick a new activity
                SwitchToRandomSubActivity();
                return;
            }

            // Re-path periodically
            chaseRepathTicks--;
            if (chaseRepathTicks <= 0)
            {
                chaseRepathTicks = ChaseRepathInterval;
                RepathChase();
            }

            // If we've arrived at the orbit dest, pick a new one immediately
            if (orbitDest.IsValid && pawn.Position == orbitDest)
            {
                RepathChase();
            }

            // Check if a better target has appeared (only every so often)
            betterTargetCheckTicks--;
            if (betterTargetCheckTicks <= 0)
            {
                betterTargetCheckTicks = BetterTargetCheckInterval;
                Pawn? betterTarget = FindBetterChaseTarget();
                if (betterTarget != null && betterTarget != chaseTarget)
                {
                    chaseTarget = betterTarget;
                    job.targetB = chaseTarget;
                    orbitDest = IntVec3.Invalid;
                    RepathChase();
                }
            }
        }

        /// <summary>
        /// Computes the next chase destination. If the child is within
        /// <see cref="OrbitRadius"/> of the target, picks a random point on
        /// a circle of that radius around the target. Otherwise paths directly
        /// toward the target's current cell.
        /// </summary>
        void RepathChase()
        {
            if (chaseTarget == null) return;

            float dist = chaseTarget.Position.DistanceTo(pawn.Position);
            if (dist <= OrbitRadius)
            {
                IntVec3 orbit = PickOrbitDestination(chaseTarget.Position);
                if (orbit.IsValid)
                {
                    orbitDest = orbit;
                    pawn.pather.StartPath(orbitDest, PathEndMode.OnCell);
                    return;
                }
            }

            // Fallback: path directly to target
            orbitDest = IntVec3.Invalid;
            pawn.pather.StartPath(chaseTarget.Position, PathEndMode.Touch);
        }

        /// <summary>
        /// Picks a random valid cell on a circle of <see cref="OrbitRadius"/>
        /// around the given center. Tries up to 8 random angles.
        /// </summary>
        IntVec3 PickOrbitDestination(IntVec3 center)
        {
            Map map = pawn.Map;
            for (int i = 0; i < 8; i++)
            {
                float angle = Rand.Range(0f, 360f) * Mathf.Deg2Rad;
                IntVec3 dest = new IntVec3(
                    Mathf.RoundToInt(center.x + Mathf.Cos(angle) * OrbitRadius),
                    0,
                    Mathf.RoundToInt(center.z + Mathf.Sin(angle) * OrbitRadius));

                if (dest.InBounds(map) && IsValidPlayCell(dest))
                    return dest;
            }
            return IntVec3.Invalid;
        }

        // ==========================================================
        // Target finding helpers
        // ==========================================================
        /// <summary>
        /// Finds a good open starting cell for active play.
        /// Prefers the child's current position if it's already suitable.
        /// </summary>
        IntVec3 FindStartCell()
        {
            if (IsValidPlayCell(pawn.Position))
                return pawn.Position;

            if (CellFinder.TryFindRandomCellNear(
                pawn.Position, pawn.Map, 5, IsValidPlayCell, out IntVec3 result))
            {
                return result;
            }

            return pawn.Position;
        }

        /// <summary>
        /// Finds a valid pawn for the child to chase.
        /// Uses <see cref="IsValidChaseTarget"/> for filtering and
        /// <see cref="ChaseTargetWeight"/> for weighted random selection.
        /// </summary>
        Pawn? FindChaseTarget()
        {
            Map map = pawn.Map;
            List<Pawn> candidates = new List<Pawn>();

            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (IsValidChaseTarget(p))
                    candidates.Add(p);
            }

            if (candidates.Count == 0)
                return null;

            return candidates.RandomElementByWeight(ChaseTargetWeight);
        }

        /// <summary>
        /// Returns true if <paramref name="target"/> is a valid chase target:
        /// humanlike, same faction, not prisoner/slave, alive, spawned,
        /// awake, not downed, and not standing in deadly danger.
        /// </summary>
        bool IsValidChaseTarget(Pawn target)
        {
            if (target == null) return false;
            if (target == pawn) return false;
            if (target.Dead || !target.Spawned) return false;
            if (target.Downed) return false;
            if (!target.Awake()) return false;
            if (!target.RaceProps.Humanlike) return false;
            if (target.Faction != pawn.Faction) return false;
            if (target.IsPrisoner || target.IsSlave) return false;
            if (target.Position.GetDangerFor(target, pawn.Map) == Danger.Deadly)
                return false;
            return true;
        }

        /// <summary>
        /// Scans for a chase target that is significantly better than the
        /// current one. Returns null if no such target exists.
        /// "Significantly better" means the new target's weight is at least
        /// 2× the current target's weight, preventing constant flicker.
        /// </summary>
        Pawn? FindBetterChaseTarget()
        {
            if (chaseTarget == null) return null;

            float currentWeight = ChaseTargetWeight(chaseTarget);
            Pawn? best = null;
            float bestWeight = currentWeight * 2f; // must be 2× better to switch

            foreach (Pawn p in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (!IsValidChaseTarget(p)) continue;
                if (p == chaseTarget) continue;

                float w = ChaseTargetWeight(p);
                if (w > bestWeight)
                {
                    bestWeight = w;
                    best = p;
                }
            }

            return best;
        }

        /// <summary>
        /// Scans <see cref="CurrentlyRunningChildren"/> for the closest
        /// running child within <see cref="ReactiveChaseMaxDistance"/>.
        /// Returns the best candidate, or null if none are nearby.
        /// Also purges stale (dead/despawned) entries from the set.
        /// </summary>
        Pawn? FindBestRunningChildToChase()
        {
            Pawn? best = null;
            float bestDistSq = ReactiveChaseMaxDistance * ReactiveChaseMaxDistance;
            List<Pawn> stale = new List<Pawn>();

            foreach (Pawn runningChild in CurrentlyRunningChildren)
            {
                if (runningChild == null || runningChild.Destroyed || !runningChild.Spawned)
                {
                    stale.Add(runningChild);
                    continue;
                }
                if (runningChild == pawn) continue;
                if (runningChild.Map != pawn.Map) continue;
                if (!IsValidChaseTarget(runningChild)) continue;

                float distSq = runningChild.Position.DistanceToSquared(pawn.Position);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = runningChild;
                }
            }

            foreach (Pawn p in stale)
                CurrentlyRunningChildren.Remove(p);

            return best;
        }

        /// <summary>
        /// Calculates the selection weight for a potential chase target.
        /// Children get a 5× bonus, and children with the Active Play desire
        /// get an additional 3× bonus. Closer targets are weighted higher.
        /// </summary>
        float ChaseTargetWeight(Pawn target)
        {
            float weight = 1f;

            // Distance factor — closer is better
            float dist = target.Position.DistanceTo(pawn.Position);
            weight *= Mathf.Max(0.1f, 1f - (dist / 30f));

            // Strongly prefer other children
            if (target.DevelopmentalStage.Child())
            {
                weight *= 5f;

                // Even stronger preference for children also doing active play
                var tracker = Current.Game.GetComponent<GameComponent_PlayTracker>()?.GetTracker(target);
                if (tracker != null
                    && tracker.ActivePlayDesires.Any(d => d.defName == "TPE_ActivePlay"))
                {
                    weight *= 3f;
                }
            }

            return weight;
        }

        /// <summary>
        /// Picks a random nearby cell suitable for active play.
        /// </summary>
        /// <param name="radius">Search radius around the pawn.</param>
        /// <param name="maxDistance">Maximum allowed distance from current position.</param>
        IntVec3 PickNearbyCell(int radius, float maxDistance)
        {
            Map map = pawn.Map;
            if (CellFinder.TryFindRandomCellNear(
                pawn.Position, map, radius,
                c => IsValidPlayCell(c) && c.DistanceTo(pawn.Position) <= maxDistance,
                out IntVec3 result))
            {
                return result;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// Returns true if the cell is suitable for active play:
        /// in-bounds, standable, not avoid-wander terrain, and not forbidden.
        /// </summary>
        bool IsValidPlayCell(IntVec3 c)
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

        // ==========================================================
        // Per-tick dispatcher
        // ==========================================================
        /// <summary>
        /// Dispatches to the appropriate per-tick update method based on
        /// the current sub-activity.
        /// </summary>
        void ExecuteSubActivityTick()
        {
            switch (currentActivity)
            {
                case SubActivity.Running:
                    TickRunning();
                    break;
                case SubActivity.Spinning:
                    TickSpinning();
                    break;
                case SubActivity.Jumping:
                    TickJumping();
                    break;
                case SubActivity.SnailCrawling:
                    TickSnailCrawling();
                    break;
                case SubActivity.PeopleChasing:
                    TickPeopleChasing();
                    break;
            }
        }

        // ==========================================================
        // Cleanup
        // ==========================================================
        /// <summary>
        /// Removes any movement modifier hediff (running) that was applied
        /// during the current sub-activity, and unregisters from the running
        /// tracker so other children no longer chase this pawn.
        /// </summary>
        void CleanupHediffs()
        {
            if (appliedMovementHediff != null)
            {
                pawn.health.RemoveHediff(appliedMovementHediff);
                appliedMovementHediff = null;
            }

            // Always remove from running tracker on cleanup;
            // Add() happens only in InitRunning().
            CurrentlyRunningChildren.Remove(pawn);
        }

        /// <summary>
        /// Clears this job's Active Play animation (spin / jump marker / snail lean)
        /// from the pawn's renderer. Called when switching sub-activities or when the
        /// job ends, ensuring animations do not persist beyond their intended scope.
        ///
        /// Only clears one of OUR three animations, and only when one is actually
        /// playing: the old unconditional <c>SetAnimation(null)</c> also wiped any
        /// unrelated animation (another mod's) and forced a render-tree rebuild on
        /// every sub-activity switch — even for Running/Chasing (no animation) and
        /// whenever child play animations are disabled (nothing was ever set).
        /// </summary>
        void CleanupAnimation()
        {
            PawnRenderer? renderer = pawn.Drawer?.renderer;
            if (renderer == null)
                return;
            AnimationDef current = renderer.CurAnimation;
            if (current != null
                && (current == TPE_AnimationDefOf.TPE_ChildSpinning
                    || current == TPE_AnimationDefOf.TPE_ChildJumping
                    || current == TPE_AnimationDefOf.TPE_ChildSnailCrawling))
            {
                renderer.SetAnimation(null);
            }
        }
    }
}
