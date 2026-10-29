using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Job driver for the "Bubble Popping" child play desire (age-gated 3–9 when age
    /// gating is enabled — see <see cref="PlayDesireGiver_BubblePopping"/>).
    ///
    /// The child repeatedly walks to a random reachable cell inside the target Bubble
    /// Machine's zone, then pauses briefly there ("popping" a bubble) before picking a
    /// new cell and moving again. Deliberately does NOT track or interact with any
    /// individual <see cref="Mote_Bubble"/> — the pause itself simulates the popping
    /// action, matching the ask ("you do not have to make them chase the actual bubble
    /// motes").
    /// </summary>
    public class JobDriver_ChildBubblePopping : JobDriver
    {
        /// <summary>The Bubble Machine whose zone the child is playing in (TargetA).</summary>
        Thing BubbleMachine => job.targetA.Thing;

        /// <summary>
        /// The machine's zone comp, cached once at job start. Reused every tick to check
        /// <see cref="CompBubbleZone.IsActive"/> and to pick new wander destinations via
        /// <see cref="CompBubbleZone.TryGetRandomZoneCell"/>.
        /// </summary>
        CompBubbleZone zoneComp;

        /// <summary>Maximum total duration of the play session, matching JobDriver_ChildActivePlay's own cap.</summary>
        const int MaxDurationTicks = 2500; // ~40 seconds

        /// <summary>Play need gained per tick, matching JobDriver_ChildActivePlay's gain rate.</summary>
        const float PlayNeedGainPerTick = 0.35f / MaxDurationTicks;

        /// <summary>Minimum ticks to stand still after arriving, simulating a bubble pop.</summary>
        const int PopPauseMinTicks = 20; // ~0.33s

        /// <summary>Maximum ticks to stand still after arriving, simulating a bubble pop.</summary>
        const int PopPauseMaxTicks = 50; // ~0.83s

        /// <summary>Search radius (in zone-cell candidates) handed to CompBubbleZone.TryGetRandomZoneCell.</summary>
        const int DestinationPickAttempts = 10;

        /// <summary>Ticks remaining in the current "pop" pause. 0 means the child should be walking.</summary>
        int pauseTicksLeft;

        /// <summary>Total ticks this job has been running (for the max-duration cutoff).</summary>
        int totalTicksPlayed;

        /// <summary>
        /// No reservation needed — several children can run around the same bubble zone
        /// at once without conflict, matching <see cref="JobDriver_ChildActivePlay"/>'s exact
        /// "open space activity, no reservation" convention (same class, same override,
        /// same unconditional <c>return true</c>).
        /// </summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Job-wide safety net: end the job if the machine despawns/is destroyed at
            // any point, not just during a single toil (matches JobDriver_ChildWatchTelevision
            // and JobDriver_ChildPlay's convention for jobs anchored to a Thing target).
            this.FailOnDespawnedOrNull(TargetIndex.A);

            zoneComp = (BubbleMachine as ThingWithComps)?.GetComp<CompBubbleZone>();

            Toil popToil = ToilMaker.MakeToil("ChildBubblePopping");
            popToil.initAction = () =>
            {
                totalTicksPlayed = 0;
                pauseTicksLeft = 0;
                PickNewDestination();
            };
            popToil.tickAction = () =>
            {
                totalTicksPlayed++;

                // End early if the machine has gone inactive (powered off / switched off) —
                // an idle machine isn't producing bubbles to pop anymore. Mirrors
                // JobDriver_ChildWatchTelevision's "end early if the TV loses power" check.
                if (zoneComp == null || !zoneComp.IsActive)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Fill play need and end when satisfied or the max duration is reached.
                // PlayUtility.PlayTickCheckEnd already calls EndCurrentJob internally when
                // satisfied; the extra EndJobWith below mirrors JobDriver_ChildActivePlay's
                // exact termination idiom for the max-duration case.
                if (PlayUtility.PlayTickCheckEnd(pawn, PlayNeedGainPerTick) || totalTicksPlayed >= MaxDurationTicks)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

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
            popToil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return popToil;

            AddFinishAction((JobCondition condition) =>
            {
                // Stop any remaining path movement so the child doesn't drift after the
                // job ends, matching JobDriver_ChildActivePlay's own finish-action cleanup.
                pawn.pather?.StopDead();
            });
        }

        /// <summary>
        /// Picks a new random cell inside the machine's CURRENT bubble zone (reusing the
        /// exact same zone geometry the bubbles themselves are confined to — see
        /// <see cref="CompBubbleZone.TryGetRandomZoneCell"/>) and starts walking there.
        /// If no valid cell can be found this attempt (e.g. an extremely cramped zone),
        /// this is a graceful no-op: the tick loop's "not moving" check will treat it as
        /// having "arrived" and retry via the next pause-then-move cycle rather than
        /// getting stuck.
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
        /// Returns true if the cell is suitable for a child to walk to and stand on:
        /// standable and not forbidden. Deliberately mirrors
        /// <see cref="JobDriver_ChildActivePlay.IsValidPlayCell"/> — no separate InBounds
        /// check is needed here since every cell CompBubbleZone hands out already came
        /// from an in-bounds source (a room's own cells, or a bounds-checked circular scan).
        /// No reachability (CanReach) check either, matching that same sibling driver's
        /// convention of not spending pathfinding budget on a simple "wander nearby" pick.
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
}
