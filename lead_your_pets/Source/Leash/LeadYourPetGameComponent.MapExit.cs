using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        private readonly HashSet<Pawn> mapExitHandling = new HashSet<Pawn>();

        public void HandleMasterExitMap(Pawn master, Rot4 exitDir)
        {
            if (master == null || !master.Spawned || !mapExitHandling.Add(master))
            {
                return;
            }

            try
            {
                List<LeashLink> masterLinks = GetLinksForMaster(master);
                if (masterLinks == null || masterLinks.Count == 0)
                {
                    return;
                }

                List<LeashLink> snapshot = new List<LeashLink>(masterLinks);
                LeashedPawnMapExitBehavior behavior = LeadYourPetUtility.IsPlayerCaravanMaster(master)
                    ? LeashedPawnMapExitBehavior.LeaveMap
                    : ConfiguredMapExitBehavior();
                for (int i = 0; i < snapshot.Count; i++)
                {
                    LeashLink link = snapshot[i];
                    if (link == null || GetLinkForPet(link.Pet) != link)
                    {
                        continue;
                    }

                    LeashedPawnMapExitBehavior linkBehavior = LeadYourPetUtility.IsUnprotectedTravelStock(link.Pet)
                        ? LeashedPawnMapExitBehavior.LeaveMap
                        : behavior;
                    HandleLeashedPawnAtMapExit(link, linkBehavior, exitDir, master.Map);
                }
            }
            finally
            {
                mapExitHandling.Remove(master);
            }
        }

        private LeashedPawnMapExitBehavior ConfiguredMapExitBehavior()
        {
            LeashedPawnMapExitBehavior behavior = LeadYourPetMod.Settings == null
                ? LeashedPawnMapExitBehavior.StayInPlace
                : LeadYourPetMod.Settings.leashedPawnMapExitBehavior;
            return behavior == LeashedPawnMapExitBehavior.LeaveMap || behavior == LeashedPawnMapExitBehavior.Disappear
                ? behavior
                : LeashedPawnMapExitBehavior.StayInPlace;
        }


        private void HandleLeashedPawnAtMapExit(LeashLink link, LeashedPawnMapExitBehavior behavior, Rot4 exitDir, Map map)
        {
            Pawn pet = link.Pet;
            if (pet == null || pet.DestroyedOrNull())
            {
                EndLink(link, false);
                return;
            }

            if (GodHandsCompat.IsGrabbed(pet) || pet.CarriedBy != null
                || LeadYourPetUtility.IsExternalToddlerHold(pet.CurJobDef?.defName, pet.jobs?.curDriver?.GetType().FullName))
            {
                // The holder owns placement and release; the Lord keeps its carry responsibility.
                EndLink(link, false);
                return;
            }

            if (behavior == LeashedPawnMapExitBehavior.Disappear)
            {
                DetachPetFromMasterLord(link);
                EndLink(link, false);
                ClearTravelStock(pet);
                if (!pet.Destroyed)
                {
                    pet.Destroy(DestroyMode.Vanish);
                }

                return;
            }

            if (behavior == LeashedPawnMapExitBehavior.LeaveMap && TryMakeLeashedPawnLeaveMap(pet, link.Master, map, exitDir))
            {
                if (GetLinkForPet(pet) == link)
                {
                    EndLink(link, false);
                }

                return;
            }

            if (LeadYourPetUtility.IsUnprotectedTravelStock(pet))
            {
                // Its Lord still owns departure, including carrying immobile members.
                EndLink(link, false);
                return;
            }

            DetachPetFromMasterLord(link);
            EndLink(link, false);
        }

        private bool TryMakeLeashedPawnLeaveMap(Pawn pawn, Pawn master, Map map, Rot4 exitDir)
        {
            if (pawn == null
                || map == null
                || !pawn.Spawned
                || pawn.Map != map
                || pawn.Dead
                || pawn.Downed
                || pawn.InMentalState
                || pawn.CarriedBy != null
                || LeadYourPetUtility.IsExternalToddlerHold(pawn.CurJobDef?.defName, pawn.jobs?.curDriver?.GetType().FullName)
                || !map.CanEverExit
                || LifeStageUtility.AlwaysDowned(pawn)
                || pawn.health?.capacities == null
                || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return false;
            }

            RestUtility.WakeUp(pawn, false);
            if (pawn.Downed || pawn.InMentalState || !pawn.Spawned || pawn.Map != map || !pawn.CanReachMapEdge())
            {
                return false;
            }

            if (master != null && master.Spawned && master.Map == map
                && master.Position.CloseToEdge(map, 2) && pawn.Position.InHorDistOf(master.Position, 2f))
            {
                pawn.ExitMap(false, exitDir);
                return true;
            }

            if (pawn.jobs == null || !RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 destination))
            {
                return false;
            }

            if (pawn.CurJob?.exitMapOnArrival == true)
            {
                return true;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, destination);
            job.exitMapOnArrival = true;
            job.lord = pawn.GetLord();
            job.locomotionUrgency = LocomotionUrgency.Jog;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            return true;
        }

        private void DetachPetFromMasterLord(LeashLink link)
        {
            Lord masterLord = link?.Master?.GetLord();
            Lord petLord = link?.Pet?.GetLord();
            if (masterLord != null && petLord == masterLord)
            {
                bool hasMasterLordJob = link.Pet.CurJob != null && link.Pet.CurJob.lord == masterLord;
                masterLord.RemovePawn(link.Pet);
                if (hasMasterLordJob && link.Pet.Spawned && link.Pet.jobs != null && link.Pet.CurJob != null)
                {
                    link.Pet.jobs.EndCurrentJob(JobCondition.InterruptForced, false, true);
                }
            }
        }
    }
}
