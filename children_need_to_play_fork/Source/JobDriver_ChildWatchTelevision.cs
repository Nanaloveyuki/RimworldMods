using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Job driver for a child watching television as a play activity.
    /// The child walks to a watch spot near a powered TV, faces it,
    /// and gains play need for the duration of the program.
    ///
    /// Ends early if the TV loses power or is destroyed.
    /// </summary>
    public class JobDriver_ChildWatchTelevision : JobDriver
    {
        /// <summary>The television being watched (TargetA).</summary>
        Thing TV => job.targetA.Thing;

        /// <summary>The cell the child stands/sits on to watch (TargetB).</summary>
        IntVec3 WatchCell => job.targetB.Cell;

        /// <summary>How many ticks the child watches TV.</summary>
        const int WatchDurationTicks = 2500;

        /// <summary>Play need gained per tick while watching.</summary>
        const float PlayNeedGainPerTick = 0.3f / WatchDurationTicks;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (TV == null)
                return false;

            if (!pawn.Reserve(TV, job, job.def.joyMaxParticipants, 0, null, errorOnFailed))
                return false;

            if (!pawn.ReserveSittableOrSpot(WatchCell, job, errorOnFailed))
                return false;

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            // Walk to the watch spot
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            // Watch the TV
            Toil watch = ToilMaker.MakeToil("ChildWatchTelevision");
            watch.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceCell(TV.Position);
            };
            watch.tickAction = () =>
            {
                // Always face the television
                pawn.rotationTracker.FaceCell(TV.Position);

                // End early if TV is unpowered
                CompPowerTrader? power = TV.TryGetComp<CompPowerTrader>();
                if (power == null || !power.PowerOn)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                // Fill play need and auto-end when satisfied
                PlayUtility.PlayTickCheckEnd(pawn, PlayNeedGainPerTick);

                // Gain a small amount of comfort from the spot
                pawn.GainComfortFromCellIfPossible(1);
            };
            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = WatchDurationTicks;
            watch.handlingFacing = true;
            yield return watch;
        }
    }
}
