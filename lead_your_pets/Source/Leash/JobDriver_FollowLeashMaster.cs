using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public class JobDriver_FollowLeashMaster : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override string GetReport()
        {
            return base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => job.targetA.HasThing && !job.targetA.Thing.Spawned);

            Toil toil = ToilMaker.MakeToil("LeadYourPet_FollowLeashMaster");
            toil.tickIntervalAction = delegate
            {
                LeashLink link = LeadYourPetUtility.Component?.GetLinkForPet(pawn);
                Thing followThing = LeadYourPetUtility.GetCurrentFollowThing(link);
                LocalTargetInfo target = followThing != null
                    ? new LocalTargetInfo(followThing)
                    : new LocalTargetInfo(LeadYourPetUtility.GetAnchorCell(link));
                if (link == null || !target.IsValid || job.targetA != target)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                if (GodHandsCompat.IsGrabbed(pawn) || GodHandsCompat.IsGrabbed(link.Master)
                    || (followThing is Pawn anchor && GodHandsCompat.IsGrabbed(anchor))
                    || LeadYourPetUtility.ShouldPreserveMapExitMovement(pawn)
                    || LeadYourPetUtility.ShouldPreserveMapExitMovement(link.Master)
                    || LeadYourPetUtility.AllowsLeashedHostileAttack(link)
                    || pawn.Position.InHorDistOf(target.Cell, LeadYourPetUtility.FollowThreshold))
                {
                    pawn.pather.StopDead();
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                PathEndMode mode = target.HasThing ? PathEndMode.Touch : PathEndMode.OnCell;
                if (!pawn.CanReach(target, mode, Danger.Deadly))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!pawn.pather.Moving || pawn.pather.Destination != target)
                {
                    pawn.pather.StartPath(target, mode);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
        }
    }
}
