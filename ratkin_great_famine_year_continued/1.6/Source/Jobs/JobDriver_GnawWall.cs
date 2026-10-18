using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobDriver_GnawWall : JobDriver
    {
        private const int GnawTicks = 200;

        private Thing TargetThing => job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetThing, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.IsSlaveOfColony);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, GnawTicks, true);
            Toil finish = ToilMaker.MakeToil("GnawWallFinish");
            finish.initAction = delegate
            {
                TargetThing.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 5f, instigator: pawn));
                MouseDisasterUtility.AddNutrition(pawn, 0.5f);
                MouseDisasterUtility.AddOrRefreshHediff(pawn, MouseDisasterDefOf.MouseDisaster_GnawedWall, 0.2f);
                int count = MouseDisasterUtility.RecordWallGnaw(pawn);
                if (count >= 5)
                {
                    MouseDisasterUtility.AddOrRefreshHediff(pawn, MouseDisasterDefOf.MouseDisaster_OverGnawedWall, 0.3f);
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(MouseDisasterDefOf.MouseDisaster_OverGnawedWallThought);
                    MouseDisasterUtility.ClampFoodToMax(pawn);
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
