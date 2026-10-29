using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Base job driver for child play activities. Handles common mechanics:
    /// - Walking to the play target
    /// - Playing for a duration
    /// - Filling the child's play need
    /// - Dropping and unforbidding any carried toy when done
    /// 
    /// Subclasses override <see cref="PlayDurationTicks"/> and
    /// <see cref="PlayNeedGainPerTick"/> to tune specific activities.
    /// </summary>
    public abstract class JobDriver_ChildPlayBase : JobDriver
    {
        /// <summary>
        /// How many ticks the play activity lasts. Override in subclasses.
        /// </summary>
        protected virtual int PlayDurationTicks => 2500;

        /// <summary>
        /// How much play need is filled per tick while playing. Override in subclasses.
        /// Default fills ~30% over the default duration.
        /// </summary>
        protected virtual float PlayNeedGainPerTick => 0.3f / 2500f;

        /// <summary>
        /// The toy this child is playing with (if any). Tracked for cleanup.
        /// </summary>
        protected Thing? trackedToy;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (job.targetA.HasThing)
            {
                return ReservationUtility.Reserve(
                    pawn, job.targetA, job, 1, 1, null, errorOnFailed, false);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            trackedToy = job.targetA.Thing;
            this.job.count = 1;

            // Cleanup: drop and unforbid toy on any job end condition
            AddFinishAction(delegate (JobCondition condition)
            {
                if (condition == JobCondition.Succeeded)
                {
                    // Apply playground buff if in a playground room
                    Room? room = pawn.GetRoom();
                    if (room != null && room.Role != null
                        && room.Role.defName == "TPE_Playground")
                    {
                        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(
                            TPE_ThoughtDefOf.TPE_PlayedInPlayground);
                    }
                }

                if (trackedToy != null && pawn.carryTracker?.CarriedThing == trackedToy)
                {
                    if (pawn.carryTracker.TryDropCarriedThing(
                        pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                    {
                        TPE_ToyUtility.Unforbid(dropped);
                    }
                }
                trackedToy = null;
            });

            // --- Step 1: Go to the play target ---
            if (job.targetA.HasThing)
            {
                // Walk to the toy/destination
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

                // Pick up the toy if it's a haulable thing
                if (job.targetA.Thing.def.EverHaulable)
                {
                    yield return Toils_Haul.StartCarryThing(TargetIndex.A);
                }
            }
            else if (job.targetA.IsValid)
            {
                // Walk to a cell destination
                yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            }

            // --- Step 2: Play! ---
            Toil playToil = ToilMaker.MakeToil("ChildPlay");
            playToil.initAction = () =>
            {
                // Face a random direction for variety
                pawn.rotationTracker.FaceCell(pawn.Position + GenRadial.RadialPattern[Rand.Range(0, 8)]);
            };
            playToil.tickAction = () =>
            {
                // Fill play need gradually while playing and auto-end when satisfied
                PlayUtility.PlayTickCheckEnd(pawn, PlayNeedGainPerTick);

                // Occasionally make a small hop or face a new direction
                if (pawn.IsHashIntervalTick(120))
                {
                    pawn.rotationTracker.FaceCell(pawn.Position + GenRadial.RadialPattern[Rand.Range(0, 8)]);
                }
            };
            playToil.defaultCompleteMode = ToilCompleteMode.Delay;
            playToil.defaultDuration = PlayDurationTicks;
            playToil.handlingFacing = true;
            yield return playToil;

            // --- No "Step 3: return toy to toy box" (DELIBERATE) ---
            // The toy is left beside the child by the finish action above. This is
            // user-confirmed intended behavior ("kids leave their toys out").
            //
            // BUGFIX (save-load): the removed step was gated on
            // `pawn.carryTracker.CarriedThing != null`, evaluated HERE at
            // MakeNewToils time. On a fresh job that was always false (dead code),
            // but MakeNewToils also runs when a save is LOADED (JobDriver.ExposeData
            // -> SetupToils in PostLoadInit) — and a child saved mid-play IS carrying
            // the toy, so the branch fired, called FindNearestToyBox() on an
            // unspawned pawn's null map (NRE -> toils never built -> job killed on
            // load) and would also have changed the toil count vs. the save.
            // Never yield conditionally on live runtime state in MakeNewToils.
        }
    }

    /// <summary>
    /// Job driver for playing with a toy. Moderate duration, good play gain.
    /// Leaves the toy out beside the child when done (intended — see the base
    /// class's "No Step 3" note).
    /// </summary>
    public class JobDriver_ChildPlayWithToy : JobDriver_ChildPlayBase
    {
        protected override int PlayDurationTicks => 2500; // ~40 seconds
        protected override float PlayNeedGainPerTick => 0.35f / 2500f;
    }

    /// <summary>
    /// Job driver for reading a book as a PLAY activity (fills the child Play
    /// need — not Learning, not Joy, no book outcome effects; that's vanilla
    /// <see cref="JobDriver_Reading"/>'s job). Longer duration, moderate play gain.
    /// Child fetches the book (from the floor, a stockpile, OR from inside a
    /// bookcase), carries it to a nearby free chair (or reads standing), reads
    /// with the book visibly open, then hauls the book back to storage.
    ///
    /// Structure deliberately mirrors vanilla <see cref="JobDriver_Reading"/>:
    ///
    /// BUGFIX (user report — "kids stuck in a loop trying again and again to
    /// start reading", "started 10 jobs in one tick"): vanilla
    /// <see cref="BookUtility.TryGetRandomBookToRead"/> (which
    /// <see cref="PlayDesireGiver_ReadBook"/> uses to pick the book) ALSO returns
    /// books held INSIDE a <see cref="Building_Bookcase"/>. Those books are NOT
    /// spawned — they live in the bookcase's ThingOwner. The old driver walked to
    /// the book with plain <c>Toils_Goto.GotoThing(A, Touch)</c>, which carries a
    /// built-in <c>FailOnDespawnedOrNull(A)</c>, so for any bookcase book the job
    /// failed (Incompletable) on the very first tick, the think tree immediately
    /// re-issued another read job (often the same book), and the cycle repeated
    /// until the engine's "10 jobs in one tick" guard tripped — leaving the child
    /// standing frozen. Fixed by passing <c>canGotoSpawnedParent: true</c>, which
    /// paths to <see cref="Thing.SpawnedParentOrMe"/> (the bookcase) and fails
    /// only if BOTH the book and every parent are despawned
    /// (<c>FailOnSelfAndParentsDespawnedOrNull</c>) — the exact engine-provided
    /// mechanism for "go to a thing that may be stored in a container".
    ///
    /// Also fixed in the same rewrite:
    /// - The toil list is now FIXED-SHAPE (always the same 5 toils, no
    ///   runtime-conditional <c>yield</c>s). The old version decided whether to
    ///   yield the "walk to chair" and "return to toy box" toils from live state
    ///   at <c>MakeNewToils</c> time, which (a) meant the return step was dead
    ///   code (nothing is carried yet when toils are built) and (b) could change
    ///   the toil COUNT between save and reload, misaligning the saved
    ///   <c>curToilIndex</c>.
    /// - The read spot is chosen when the child actually has the book in hand
    ///   (vanilla <c>CarryToReadingSpot</c> pattern), not at job start from
    ///   wherever the child happened to be standing.
    /// - After a successful read the book is hauled back to the best storage
    ///   (i.e. back into the bookcase) via the vanilla finalizer-job pattern,
    ///   instead of being dropped on the floor.
    /// </summary>
    public class JobDriver_ChildReadBook : JobDriver_ChildPlayBase
    {
        protected override int PlayDurationTicks => 4000; // ~65 seconds
        protected override float PlayNeedGainPerTick => 0.3f / 4000f;

        /// <summary>Max distance (from where the book was picked up) to look for a chair.</summary>
        const float ChairSearchRadius = 15f;

        /// <summary>The book being read (TargetA). Null if it was destroyed/cleared.</summary>
        Book? Book => job.GetTarget(TargetIndex.A).Thing as Book;

        /// <summary>True while the child is physically carrying the target book.</summary>
        bool CarryingBook
        {
            get
            {
                Thing? book = job.GetTarget(TargetIndex.A).Thing;
                return book != null && pawn.carryTracker?.CarriedThing == book;
            }
        }

        // TryMakePreToilReservations is inherited from JobDriver_ChildPlayBase: it
        // reserves TargetA (the book). Reserving an unspawned, bookcase-held book is
        // fine — vanilla JobDriver_Reading reserves the exact same kind of target.

        protected override IEnumerable<Toil> MakeNewToils()
        {
            trackedToy = job.targetA.Thing;
            job.count = 1;

            // --- End-of-job handling for the carried book (mirrors vanilla JobDriver_Reading) ---
            // The engine calls this BEFORE CleanupCurrentJob/finish actions, exactly once
            // per job end (both EndCurrentJob and the interrupting-StartJob path).
            //  - Success  → haul the book back to storage (back into the bookcase). The
            //               engine keeps the book in the child's hands for that job.
            //  - Anything else, or no storage available → drop it beside the child and
            //               unforbid it (same end result as this driver's old behavior).
            SetFinalizerJob(condition =>
            {
                Thing? book = job?.GetTarget(TargetIndex.A).Thing;
                // Not carrying it (never picked up / already dropped) or the child is
                // off-map (caravan, death) → nothing to do; the engine's own cleanup
                // handles any carried thing in those edge cases.
                if (book == null || !pawn.Spawned || pawn.carryTracker?.CarriedThing != book)
                    return null;

                if (condition == JobCondition.Succeeded)
                {
                    Job? haulJob = HaulAIUtility.HaulToStorageJob(pawn, book, forced: false);
                    if (haulJob != null)
                        return haulJob;
                }

                if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                {
                    TPE_ToyUtility.Unforbid(dropped);
                }
                return null;
            });

            // Playground buff on success. Book dropping is NOT done here any more —
            // that's the finalizer's job above (finish actions run AFTER the finalizer
            // is requested, so dropping here would yank the book out of the child's
            // hands right before the haul-to-storage job starts).
            AddFinishAction(delegate (JobCondition condition)
            {
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
                trackedToy = null;
            });

            // Built first so the skip-ahead jump below can target it.
            Toil carryToReadSpot = CarryToReadingSpot();

            // --- Toil 0: already holding the book (e.g. job resumed) → skip the fetch ---
            yield return Toils_Jump.JumpIf(carryToReadSpot, () => CarryingBook);

            // --- Toil 1: walk to the book, OR to the bookcase holding it ---
            // canGotoSpawnedParent: true is THE fix for the reported infinite loop —
            // see the class summary.
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, canGotoSpawnedParent: true)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            // --- Toil 2: pick it up ---
            // canTakeFromInventory: true lets StartCarryThing pull the book out of the
            // bookcase's container (Thing.SplitOff removes it from its holdingOwner).
            yield return Toils_Haul.StartCarryThing(
                TargetIndex.A,
                putRemainderInQueue: false,
                subtractNumTakenFromJobCount: false,
                failIfStackCountLessThanJobCount: false,
                reserve: true,
                canTakeFromInventory: true);

            // --- Toil 3: carry it to a chair (or a good standing spot) ---
            yield return carryToReadSpot;

            // --- Toil 4: read ---
            Toil readToil = Toils_General.Wait(PlayDurationTicks);
            readToil.debugName = "ChildReadBook";
            readToil.FailOnDestroyedOrNull(TargetIndex.A);
            readToil.handlingFacing = true;
            readToil.initAction = () =>
            {
                pawn.pather.StopDead();
                FaceReadingDirection();
                // Show the open-book graphic in the child's hands (vanilla Book.DrawAt
                // swaps to OpenGraphic while IsOpen). Closed again in the finish action.
                Book? book = Book;
                if (book != null)
                    book.IsOpen = true;
                job.showCarryingInspectLine = false;
            };
            // tickIntervalAction (not per-tick tickAction): matches vanilla
            // JobDriver_Reading and this mod's Skydreaming/NatureRunning drivers.
            // Play gain and comfort are both scaled by delta, so totals are identical
            // to the old per-tick version at a fraction of the call count.
            readToil.tickIntervalAction = (int delta) =>
            {
                // intervalDelta: delta so PlayTickCheckEnd's idle-play override
                // check still reliably hits its 50-tick boundary (see
                // PlayUtility.CheckForBetterJobWhileIdlePlaying).
                if (PlayUtility.PlayTickCheckEnd(pawn, PlayNeedGainPerTick * delta, intervalDelta: delta))
                    return; // job ended (satisfied or overridden) — don't touch it further
                pawn.GainComfortFromCellIfPossible(delta);
            };
            readToil.AddFinishAction(() =>
            {
                Book? book = Book;
                if (book != null)
                    book.IsOpen = false;
            });
            yield return readToil;

            // No "return to toy box" step: on success the finalizer job hauls the
            // book back to storage (a toy box isn't a book's home anyway).
        }

        /// <summary>
        /// Vanilla <c>JobDriver_Reading.CarryToReadingSpot</c> pattern: once the
        /// book is in hand, pick a free chair nearby (else a vanilla "stand and
        /// chew" spot, else stay put) and walk there. Runs in an initAction so
        /// the search starts from where the book was actually collected, and so
        /// the toil always exists in the list (fixed toil count, save-safe).
        /// </summary>
        Toil CarryToReadingSpot()
        {
            Toil toil = ToilMaker.MakeToil("ChildCarryToReadingSpot");
            toil.initAction = () =>
            {
                IntVec3 cell = FindReadSpot();
                if (!cell.IsValid)
                {
                    // Nowhere better — read right here. ReadyForNextToil advances
                    // instantly instead of pathing to our own cell.
                    ReadyForNextToil();
                    return;
                }
                // Claim the seat/spot (also when it's the cell we're already on, so
                // nobody else sits on our chair mid-read). Tied to this job, so it's
                // released automatically when the job ends.
                pawn.ReserveSittableOrSpot(cell, job);
                if (cell == pawn.Position)
                {
                    ReadyForNextToil();
                    return;
                }
                pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, cell);
                pawn.pather.StartPath(cell, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            toil.FailOnDestroyedOrNull(TargetIndex.A);
            return toil;
        }

        /// <summary>
        /// Finds a reading spot: the closest free, safe, usable chair within
        /// <see cref="ChairSearchRadius"/>; otherwise vanilla's standing-spot finder;
        /// otherwise <see cref="IntVec3.Invalid"/> (= read where you are).
        /// Called ONCE per job, so its cost is negligible.
        /// </summary>
        IntVec3 FindReadSpot()
        {
            Map map = pawn.Map;
            // Validator mirrors vanilla JobDriver_Reading.ValidateChair (minus the
            // interaction-cell preference). NOTE: the free seat cell is deliberately
            // NOT captured out of the validator — GenClosest keeps validating any
            // CLOSER candidate after a match, and a closer candidate that fails would
            // overwrite a captured cell with Invalid. Re-derived below instead
            // (one extra OccupiedRect walk on a single building — trivial).
            Thing? chair = GenClosest.ClosestThingReachable(
                pawn.Position, map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.OnCell,
                TraverseParms.For(pawn),
                ChairSearchRadius,
                t => t.def.building != null && t.def.building.isSittable
                     && !t.IsForbidden(pawn)
                     && !t.Fogged()
                     && !t.IsBurning()
                     && t.IsSociallyProper(pawn)
                     && !t.HostileTo(pawn)
                     && t.Position.GetDangerFor(pawn, map) == Danger.None
                     && TryFindFreeSeatOn(t, out _));
            if (chair != null && TryFindFreeSeatOn(chair, out IntVec3 seat))
                return seat;

            // Vanilla fallback (same call + validator JobDriver_Reading uses).
            Book? book = Book;
            if (book != null)
            {
                IntVec3 stand = RCellFinder.SpotToChewStandingNear(pawn, book,
                    c => !c.Fogged(map) && pawn.CanReserveSittableOrSpot(c));
                if (stand.IsValid)
                    return stand;
            }
            return IntVec3.Invalid;
        }

        /// <summary>
        /// First cell of a (possibly multi-cell, e.g. a couch) sittable building
        /// the child can reserve to sit on. Mirrors vanilla
        /// <c>JobDriver_Reading.TryFindFreeSittingSpotOnThing</c>.
        /// </summary>
        bool TryFindFreeSeatOn(Thing t, out IntVec3 cell)
        {
            foreach (IntVec3 c in t.OccupiedRect())
            {
                if (!c.Fogged(pawn.Map) && pawn.CanReserveSittableOrSpot(c))
                {
                    cell = c;
                    return true;
                }
            }
            cell = IntVec3.Invalid;
            return false;
        }

        /// <summary>
        /// Sets the child's facing once at the start of reading (handlingFacing
        /// keeps it fixed afterwards). Mirrors vanilla FindAdjacentReadingSurface:
        /// face an adjacent table if there is one, else the way the chair faces;
        /// if still facing North (back to the camera — the book would be hidden)
        /// turn to a random visible direction, like vanilla's reading tick does.
        /// </summary>
        void FaceReadingDirection()
        {
            Map map = pawn.Map;
            IntVec3 pos = pawn.Position;
            for (int i = 0; i < 4; i++)
            {
                IntVec3 adj = pos + new Rot4(i).FacingCell;
                if (adj.InBounds(map) && adj.GetSurfaceType(map) == SurfaceType.Eat)
                {
                    pawn.rotationTracker.FaceCell(adj);
                    return;
                }
            }
            Building? seat = pos.GetFirstThing<Building>(map);
            if (seat != null && seat.def.building != null && seat.def.building.isSittable)
            {
                // Sit the way the chair faces (vanilla faces chair.Rotation.FacingCell),
                // even if that's North — turning sideways on a chair would look wrong.
                pawn.Rotation = seat.Rotation;
                return;
            }
            // Standing reader: avoid showing only the back of the head.
            if (pawn.Rotation == Rot4.North)
            {
                pawn.Rotation = new Rot4(Rand.Range(1, 4));
            }
        }
    }

    /// <summary>
    /// Job driver for floor drawing. Mirrors vanilla learning floor drawing
    /// exactly, but fills play need instead of learning.
    /// </summary>
    public class JobDriver_ChildFloorDrawing : JobDriver
    {
        const int DrawingIntervalTicks = 2500;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(base.TargetA, job, 1, -1, null, errorOnFailed))
            {
                return pawn.Reserve(base.TargetB, job, 1, -1, null, errorOnFailed);
            }
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnForbidden(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.B);
            this.FailOnChildLearningConditions();

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil toil = ToilMaker.MakeToil("ChildFloorDrawing");
            toil.initAction = delegate
            {
                pawn.jobs.posture = PawnPosture.Standing;
            };
            toil.handlingFacing = true;
            toil.tickIntervalAction = delegate (int delta)
            {
                pawn.rotationTracker.FaceCell(base.TargetB.Cell);
                if (!PlayUtility.PlayTickCheckEnd(pawn, delta) && pawn.IsHashIntervalTick(2500, delta))
                {
                    if (!FilthMaker.TryMakeFilth(base.TargetB.Cell, pawn.Map, ThingDefOf.Filth_Floordrawing))
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                    else
                    {
                        List<Thing> thingList = base.TargetB.Cell.GetThingList(pawn.Map);
                        for (int i = 0; i < thingList.Count; i++)
                        {
                            if (thingList[i].def == ThingDefOf.Filth_Floordrawing)
                            {
                                pawn.Reserve(thingList[i], job);
                            }
                        }
                    }
                }
            };
            toil.WithEffect(EffecterDefOf.Floordrawing, TargetIndex.A);
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = job.def.learningDuration;
            yield return toil;
        }
    }

    /// <summary>
    /// Job driver for nature running. Child sprints to outdoor nature spots
    /// and pauses to explore, looping between targets.
    /// </summary>
    public class JobDriver_ChildNatureRunning : JobDriver
    {
        static readonly IntRange WaitTicksRange = new IntRange(300, 600);

        /// <summary>
        /// Hard cap on the TOTAL ticks this job may run across every find-target /
        /// walk / wait loop iteration combined (the loop jumps back to
        /// <c>findInterestingThing</c> via <see cref="Toils_Jump"/> and would
        /// otherwise have no other way to end besides <see cref="PlayUtility.PlayTickCheckEnd"/>
        /// itself calling <c>EndCurrentJob</c>).
        ///
        /// That was harmless as long as every Nature Running job started BELOW
        /// the play-satisfied threshold -- true for every normal path, since
        /// <see cref="JobGiver_ChildPlay"/> never hands one out once play is
        /// ≥95%. <see cref="JobGiver_ChildPlayWhenIdle"/> ("Play when Idle")
        /// deliberately breaks that guarantee for jobs that start ALREADY
        /// satisfied (see <see cref="PlayUtility"/>'s idle-triggered-job
        /// detection), which would otherwise turn this loop genuinely infinite -- PlayTickCheckEnd
        /// would never end it, leaving nothing but a higher-priority need (hunger,
        /// rest) eventually winning out in the think tree to ever stop it, which
        /// could take a very long time. This cap guarantees the loop always ends
        /// on its own, matching every sibling play job driver's bounded-duration
        /// contract (same ~40s cap as <see cref="JobDriver_ChildActivePlay"/> and
        /// <see cref="JobDriver_ChildBubblePopping"/>).
        /// </summary>
        const int MaxDurationTicks = 2500;

        /// <summary>Total ticks elapsed across every loop iteration of this job. Reset at job start.</summary>
        int totalTicksPlayed;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        Toil FindInterestingThing()
        {
            Toil toil = ToilMaker.MakeToil("FindNatureTarget");
            toil.initAction = () =>
            {
                if (NatureRunningUtility.TryFindNatureInterestTarget(pawn, out LocalTargetInfo interestTarget))
                {
                    job.SetTarget(TargetIndex.A, interestTarget);
                }
                else
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);

            // Reset the max-duration counter once, at the start of the job. The
            // Toils_Jump loop below re-enters findInterestingThing directly, NOT
            // this outer method, so this line only ever runs once per job.
            totalTicksPlayed = 0;

            Toil findInterestingThing = FindInterestingThing();
            yield return findInterestingThing;

            Toil gotoToil = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);
            gotoToil.tickIntervalAction = (int delta) =>
            {
                totalTicksPlayed += delta;
                // intervalDelta: delta -- this toil only ticks every ~250 ticks (a
                // tickIntervalAction, not a per-tick tickAction), so the idle-play
                // override check inside PlayTickCheckEnd needs the real interval
                // size to reliably catch its 50-tick boundary. See
                // PlayUtility.CheckForBetterJobWhileIdlePlaying's doc comment.
                if (PlayUtility.PlayTickCheckEnd(pawn, 0.3f / 2500f, intervalDelta: delta) || totalTicksPlayed >= MaxDurationTicks)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
            };
            yield return gotoToil;

            Toil wait = ToilMaker.MakeToil("NatureRunningWait");
            wait.initAction = () =>
            {
                wait.actor.pather.StopDead();
            };
            wait.tickIntervalAction = (int delta) =>
            {
                totalTicksPlayed += delta;
                // See the identical intervalDelta note on the gotoToil above.
                if (PlayUtility.PlayTickCheckEnd(pawn, 0.3f / 2500f, intervalDelta: delta) || totalTicksPlayed >= MaxDurationTicks)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(TargetA);
            };
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = WaitTicksRange.RandomInRange;
            wait.handlingFacing = true;
            yield return wait;

            yield return Toils_Jump.Jump(findInterestingThing);
        }
    }

    /// <summary>
    /// Job driver for sky dreaming. Child finds an unroofed outdoor spot,
    /// lies down face-up, and watches the clouds.
    /// </summary>
    public class JobDriver_ChildSkydreaming : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);

            Toil toil = ToilMaker.MakeToil("Skydreaming");
            toil.initAction = () =>
            {
                pawn.jobs.posture = PawnPosture.LayingOnGroundFaceUp;
            };
            toil.tickIntervalAction = (int delta) =>
            {
                // intervalDelta: delta -- see the identical note in
                // JobDriver_ChildNatureRunning; this toil only ticks every
                // ~250 ticks, so PlayTickCheckEnd needs the real interval size
                // to reliably catch its 50-tick idle-play override-check boundary.
                PlayUtility.PlayTickCheckEnd(pawn, 0.3f / 4000f, intervalDelta: delta);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 4000;
            toil.FailOn(() => pawn.Position.Roofed(pawn.Map));
            yield return toil;
        }
    }

    /// <summary>
    /// Job driver for radio talking. Child goes to a powered comms console
    /// and chats over the radio for a set duration while gradually filling
    /// their play need. Job completes when duration expires or play need is satisfied.
    /// 
    /// Uses tickAction (every tick) to ensure consistent play need gain.
    /// Properly ends via ToilCompleteMode.Delay to allow need interrupts.
    /// </summary>
    public class JobDriver_ChildRadiotalking : JobDriver
    {
        // Duration of radio talking activity (~40 seconds)
        const int RadioTalkingDurationTicks = 2500;
        
        // Play need gained per tick (~0.35 play need over full duration)
        const float PlayNeedGainPerTick = 0.35f / RadioTalkingDurationTicks;

        Building_CommsConsole CommsConsole => (Building_CommsConsole)TargetThingA;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Fail conditions: ensure comms console is available and powered
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            this.FailOn(() => !CommsConsole.CanUseCommsNow);

            // Step 1: Walk to the comms console interaction cell
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell)
                .FailOn(() => !CommsConsole.CanUseCommsNow);

            // Step 2: Radio talk toil
            // Uses tickAction to run every tick, ensuring consistent play need gain.
            // Uses ToilCompleteMode.Delay with a duration cap to allow the job to
            // complete naturally and permit need-based job interrupts.
            Toil toil = ToilMaker.MakeToil("Radiotalking");
            
            toil.initAction = () =>
            {
                // Face the console as we start talking
                pawn.rotationTracker.FaceTarget(TargetA);
            };
            
            toil.tickAction = () =>
            {
                // Face the console every tick
                pawn.rotationTracker.FaceTarget(TargetA);
                
                // End immediately if comms console loses power or is destroyed.
                // Check both null and CanUseCommsNow to catch all edge cases.
                if (!CommsConsole.CanUseCommsNow)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                
                // Fill play need by a consistent amount each tick.
                // Ends early if play need is satisfied.
                PlayUtility.PlayTickCheckEnd(pawn, PlayNeedGainPerTick);
            };
            
            toil.WithEffect(EffecterDefOf.Radiotalking, TargetIndex.A);
            toil.handlingFacing = true;
            
            // FIX #1: Change from Never to Delay so the job completes after duration.
            // This allows need interrupts to work properly.
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            
            // FIX #2: Set a reasonable maximum duration so the job has a time limit.
            // Without this, combined with ToilCompleteMode.Never, jobs could run forever.
            toil.defaultDuration = RadioTalkingDurationTicks;
            
            yield return toil;
        }
    }
}
