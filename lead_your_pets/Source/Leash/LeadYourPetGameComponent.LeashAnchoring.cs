using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public void AnchorLeashedPetsToThing(Pawn master, Thing anchorThing)
        {
            if (master == null || anchorThing == null)
            {
                return;
            }

            foreach (LeashLink link in GetLinksForMaster(master))
            {
                if (link.Pet == anchorThing || link.Master == anchorThing)
                {
                    continue;
                }

                link.AnchorMode = LeashAnchorMode.Thing;
                link.AnchorThing = anchorThing;
                link.AnchorCell = IntVec3.Invalid;
                ResetAnchorControl(link);
            }
        }

        public void AnchorLeashedPetsToCell(Pawn master, IntVec3 cell)
        {
            if (master == null || !cell.IsValid || !master.Spawned || master.Map == null || !cell.InBounds(master.Map))
            {
                return;
            }

            foreach (LeashLink link in GetLinksForMaster(master))
            {
                link.AnchorMode = LeashAnchorMode.Cell;
                link.AnchorThing = null;
                link.AnchorCell = cell;
                ResetAnchorControl(link);
            }
        }

        public void ClearAnchorsForMaster(Pawn master)
        {
            foreach (LeashLink link in GetLinksForMaster(master))
            {
                link.AnchorMode = LeashAnchorMode.None;
                link.AnchorThing = null;
                link.AnchorCell = IntVec3.Invalid;
                link.ForceImmediateUpdate = true;
                link.CachedHasValidAutoInteractionPool = false;
            }
        }

        private void ResetAnchorControl(LeashLink link)
        {
            StopInteractionAnimation(link.Pet, finalizeTeleport: false);
            link.MasterSleepSuspended = false;
            link.YoungRatkinDutyState = LeashedYoungRatkinDutyState.FollowMaster;
            UpdateYoungRatkinDutyReport(link);
            link.IsDragging = false;
            MaintainDraggedSlow(link.Pet, false);
            link.ForceImmediateUpdate = true;
            link.CachedHasValidAutoInteractionPool = false;
            link.Pet.pather?.StopDead();
            if (link.Pet.CurJob != null && link.Pet.CurJobDef == LeadYourPetDefOf.LeadYourPet_FollowMaster)
            {
                link.Pet.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            }
        }
    }
}
