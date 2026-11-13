using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public sealed class JobDriver_RHAH_WarlordSabotage : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !JobGiver_RHAH_WarlordSabotage.CanAct(pawn) ||
                !JobGiver_RHAH_WarlordSabotage.ValidTarget(TargetThingA));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, 180, true);
            Toil finish = ToilMaker.MakeToil("WarlordSabotage");
            finish.initAction = delegate
            {
                TryApply(pawn, TargetThingA, job.def == RHAH_DefOf.RHAH_WarlordIgnite);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        internal static bool TryApply(Verse.Pawn pawn, Thing target, bool ignite)
        {
            if (!JobGiver_RHAH_WarlordSabotage.CanAct(pawn) ||
                !JobGiver_RHAH_WarlordSabotage.ValidTarget(target) || target.Map != pawn.Map ||
                !pawn.Position.AdjacentTo8WayOrInside(target)) return false;
            if (ignite && target.FlammableNow && FireUtility.TryStartFireIn(
                target.OccupiedRect().ClosestCellTo(pawn.Position), pawn.Map, 0.3f, pawn)) return true;
            target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 6f, instigator: pawn));
            return true;
        }
    }
}
