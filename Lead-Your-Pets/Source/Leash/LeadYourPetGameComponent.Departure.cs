using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        internal void PrepareTravelDeparture(Lord lord, bool interruptJobs = false)
        {
            int ticksGame = Find.TickManager?.TicksGame ?? 0;
            if (!LeadYourPetRules.ShouldPrepareTravelDeparture(
                currentTick: ticksGame,
                lastPreparedTick: lord != null && departurePreparedTicks.TryGetValue(lord.loadID, out int lastPreparedTick) ? lastPreparedTick : -99999,
                minIntervalTicks: 60,
                forceImmediate: interruptJobs,
                departing: lord?.faction != null && !lord.faction.IsPlayer && LeadYourPetUtility.IsDepartureToil(lord, lord.CurLordToil)))
            {
                return;
            }

            departurePreparedTicks[lord.loadID] = ticksGame;

            // Do not remove Lord members: TravelArrived and escort duties still own them.
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pet = lord.ownedPawns[i];
                MouseEggState state = GetMouseEggState(pet);
                if (state == null || !state.IsTravelStock || pet == null || pet.GetLord() != lord
                    || !LeadYourPetUtility.IsUnprotectedTravelStock(pet)
                    || !LeadYourPetUtility.IsNonPlayerTravelDeparture(pet))
                {
                    continue;
                }

                LeashLink link = GetLinkForPet(pet);

                bool hadLeashControl = link != null || state.IsPet || state.CurrentMaster != null;
                if (hadLeashControl || interruptJobs)
                {
                    StopInteractionAnimation(pet, finalizeTeleport: false);
                }

                if (link != null)
                {
                    link.MouseEggDutyState = LeashedMouseEggDutyState.FollowMaster;
                    UpdateMouseEggDutyReport(link);
                }

                bool mobile = pet.Spawned && !pet.Downed && LeadYourPetUtility.CanMouseEggMove(pet)
                    && !GodHandsCompat.IsGrabbed(pet)
                    && !LeadYourPetUtility.IsExternalToddlerHold(pet.CurJobDef?.defName, pet.jobs?.curDriver?.GetType().FullName);
                if (!mobile && pet.Spawned && !GodHandsCompat.IsGrabbed(pet)
                    && !LeadYourPetUtility.IsExternalToddlerHold(pet.CurJobDef?.defName, pet.jobs?.curDriver?.GetType().FullName)
                    && (hadLeashControl || interruptJobs || pet.jobs?.curDriver?.asleep == true))
                {
                    pet.pather?.StopDead();
                    pet.stances?.CancelBusyStanceHard();
                    pet.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
                }
            }
        }

        internal Job TryGetTravelDepartureCarryJob(Pawn carrier)
        {
            if (!LeadYourPetUtility.IsNonPlayerTravelDeparture(carrier)
                || !carrier.Spawned || carrier.Dead || carrier.Downed || carrier.InMentalState
                || !carrier.RaceProps.Humanlike || carrier.carryTracker == null
                || carrier.carryTracker.CarriedThing != null || GodHandsCompat.IsGrabbed(carrier)
                || !carrier.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return null;
            }

            Lord lord = carrier.GetLord();
            Pawn closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pet = lord.ownedPawns[i];
                MouseEggState state = GetMouseEggState(pet);
                if (pet == carrier || state == null || !LeadYourPetUtility.IsUnprotectedTravelStock(pet)
                    || LeadYourPetUtility.IsExternalToddlerHold(pet.CurJobDef?.defName, pet.jobs?.curDriver?.GetType().FullName)
                    || !pet.Spawned || pet.Dead || pet.InMentalState || GodHandsCompat.IsGrabbed(pet)
                    || (!pet.Downed && LeadYourPetUtility.CanMouseEggMove(pet)))
                {
                    continue;
                }

                // Already assembled infants must remain put until TravelArrived switches to ExitMap.
                if (lord.CurLordToil is LordToil_Travel travel
                    && pet.Position.InHorDistOf(travel.FlagLoc, 10f)
                    && pet.CanReach(travel.FlagLoc, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    continue;
                }

                float distance = carrier.Position.DistanceToSquared(pet.Position);
                if (distance < closestDistance
                    && carrier.CanReserveAndReach(pet, PathEndMode.Touch, Danger.Deadly))
                {
                    closest = pet;
                    closestDistance = distance;
                }
            }

            if (closest == null)
            {
                return null;
            }

            bool gathering = lord.CurLordToil is LordToil_Travel;
            IntVec3 destination;
            if (gathering)
            {
                destination = lord.CurLordToil.FlagLoc;
                if (!destination.IsValid || !carrier.CanReach(destination, PathEndMode.OnCell, Danger.Deadly))
                {
                    return null;
                }
            }
            else if (!RCellFinder.TryFindBestExitSpot(carrier, out destination))
            {
                return null;
            }

            Job job = JobMaker.MakeJob(LeadYourPetDefOf.LeadYourPet_CarryTravelMouseEgg, closest, destination);
            job.count = 1;
            job.exitMapOnArrival = !gathering;
            job.lord = lord;
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }
    }
}
