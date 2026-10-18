using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobDriver_BroadcastHope : JobDriver
    {
        private const int BroadcastWarmupTicks = 150;

        private Building_CommsConsole CommsConsole => job.GetTarget(TargetIndex.A).Thing as Building_CommsConsole;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => CommsConsole == null || !CommsConsole.CanUseCommsNow);

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell).FailOn(() => CommsConsole == null || !CommsConsole.CanUseCommsNow);

            Toil warmup = Toils_General.WaitWith(TargetIndex.A, BroadcastWarmupTicks, true);
            warmup.WithEffect(EffecterDefOf.Radiotalking, TargetIndex.A);
            warmup.handlingFacing = true;
            yield return warmup;

            Toil broadcast = ToilMaker.MakeToil("BroadcastHope");
            broadcast.initAction = delegate
            {
                Building_CommsConsole commsConsole = CommsConsole;
                Map map = commsConsole?.Map;
                if (map == null)
                {
                    return;
                }

                if (GameComponent_MouseDisasterBroadcastHope.TryQueueBroadcast(map, out int count, out float delayHours))
                {
                    string message = "MouseDisaster_UI_BroadcastScheduled".Translate(delayHours.ToString("0.0"), count).Resolve();
                    Messages.Message(message, new TargetInfo(commsConsole.Position, map), MessageTypeDefOf.NeutralEvent, historical: true);
                }
                else
                {
                    Messages.Message("MouseDisaster_UI_BroadcastFailed".Translate().Resolve(), new TargetInfo(pawn.PositionHeld, pawn.MapHeld), MessageTypeDefOf.RejectInput, historical: false);
                }
            };
            broadcast.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return broadcast;
        }
    }
}
