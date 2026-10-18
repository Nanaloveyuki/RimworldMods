using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobDriver_RHAH_BroadcastHope : JobDriver
    {
        Building_CommsConsole Console => job.targetA.Thing as Building_CommsConsole;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !RHAH_Runtime.AllowsNewContent || RHAH_Mod.Settings == null ||
                !RHAH_Mod.Settings.broadcastEnabled || Console == null || !Console.CanUseCommsNow ||
                pawn.Faction != Faction.OfPlayer || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil speaking = Toils_General.WaitWith(TargetIndex.A, 150, true);
            speaking.WithEffect(EffecterDefOf.Radiotalking, TargetIndex.A);
            speaking.handlingFacing = true;
            yield return speaking;
            Toil finish = ToilMaker.MakeToil("BroadcastHope");
            finish.initAction = () =>
            {
                if (!RHAH_BroadcastMenu.Complete(Console))
                {
                    Messages.Message("RHAH_Broadcast_Unavailable".Translate(), pawn, MessageTypeDefOf.RejectInput);
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
