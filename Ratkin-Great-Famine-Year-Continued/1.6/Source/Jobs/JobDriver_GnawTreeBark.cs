using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobDriver_GnawTreeBark : JobDriver
    {
        private const int GnawTicks = 180;

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
            Toil finish = ToilMaker.MakeToil("GnawTreeFinish");
            finish.initAction = delegate
            {
                TargetThing.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 2f, instigator: pawn));
                MouseDisasterUtility.AddNutrition(pawn, 0.2f);
                MouseDisasterUtility.AddOrRefreshHediff(pawn, MouseDisasterDefOf.MouseDisaster_GnawedTreeBark, 0.2f);
                Hediff tox = pawn.health.GetOrAddHediff(HediffDefOf.ToxicBuildup);
                tox.Severity += 0.08f;
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
