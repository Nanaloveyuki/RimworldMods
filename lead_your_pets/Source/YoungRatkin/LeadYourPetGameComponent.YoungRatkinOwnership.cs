using RimWorld;
using Verse;

using Verse.AI;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public void MarkAsTravelYoungRatkin(Pawn pawn, Pawn carrier, Pawn trader, bool sellAsPrisoner, bool preserveOwnership = false)
        {
            if (!LeadYourPetUtility.CanBePetYoungRatkin(pawn, out _))
            {
                return;
            }

            YoungRatkinState state = EnsureYoungRatkinState(pawn);
            ApplyOwnershipSnapshot(state, new YoungRatkinOwnershipSnapshot(
                isPet: true,
                isTravelStock: true,
                sellAsPrisoner: sellAsPrisoner,
                hasCurrentMaster: carrier != null));
            state.CurrentMaster = carrier;
            if (YoungRatkinOwnershipStateMachine.ShouldClearPawnFactionForTravelRegistration(
                preserveOwnership,
                pawn.Faction == carrier?.Faction))
            {
                pawn.SetFaction(null);
            }

            if (YoungRatkinOwnershipStateMachine.ShouldForceGuestSlaveForTravelRegistration(
                preserveOwnership,
                pawn.guest != null))
            {
                pawn.guest.joinStatus = JoinStatus.JoinAsSlave;
            }

            TryDropCarriedPawnAfterBecomingYoungRatkinPet(pawn);
        }

        public void ClearTravelStock(Pawn pawn)
        {
            YoungRatkinState state = GetYoungRatkinState(pawn);
            if (state == null)
            {
                return;
            }

            ApplyOwnershipSnapshot(state, YoungRatkinOwnershipStateMachine.ResolveBoughtTravelYoungRatkinState(ToOwnershipSnapshot(state)));
            PruneYoungRatkinStateIfEmpty(pawn);
        }

        public void HandleBoughtTravelYoungRatkin(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            EndLeashForPet(pawn, false);
            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);
            if (pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            ApplyBoughtYoungRatkinStatus(pawn);
            EndLeashForPet(pawn, false);
            if (LeadYourPetUtility.BoughtYoungRatkinStatus == BoughtYoungRatkinStatus.Prisoner)
            {
                TryMoveBoughtPrisonerNearPrisonBed(pawn);
            }

            ClearTravelStock(pawn);
            LeadYourPetUtility.ClearRatkinDisasterVisitorCover(pawn);
            LeadYourPetUtility.NotifyRatkinDisasterPawnIdentityChanged(pawn);
        }

        private static void ApplyBoughtYoungRatkinStatus(Pawn pawn)
        {
            if (pawn?.guest == null)
            {
                return;
            }

            switch (LeadYourPetUtility.BoughtYoungRatkinStatus)
            {
                case BoughtYoungRatkinStatus.Colonist:
                    pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
                    break;
                case BoughtYoungRatkinStatus.Slave:
                    if (ModsConfig.IdeologyActive)
                    {
                        pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                        break;
                    }

                    goto default;
                default:
                    pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                    pawn.guest.resistance = LeadYourPetRules.ResolveTravelYoungRatkinPrisonerResistance();
                    pawn.guest.will = LeadYourPetRules.ResolveTravelYoungRatkinPrisonerWill();
                    break;
            }
        }

        private static YoungRatkinOwnershipSnapshot ToOwnershipSnapshot(YoungRatkinState state)
        {
            return new YoungRatkinOwnershipSnapshot(
                isPet: state != null && state.IsPet,
                isTravelStock: state != null && state.IsTravelStock,
                sellAsPrisoner: state != null && state.SellAsPrisoner,
                hasCurrentMaster: state != null && state.CurrentMaster != null);
        }

        private static void ApplyOwnershipSnapshot(YoungRatkinState state, YoungRatkinOwnershipSnapshot snapshot)
        {
            if (state == null)
            {
                return;
            }

            state.IsPet = snapshot.IsPet;
            state.IsTravelStock = snapshot.IsTravelStock;
            state.SellAsPrisoner = snapshot.SellAsPrisoner;
            state.CurrentMaster = snapshot.HasCurrentMaster ? state.CurrentMaster : null;
        }

        private void PruneYoungRatkinStateIfEmpty(Pawn pawn)
        {
            YoungRatkinState state = GetYoungRatkinState(pawn);
            if (state == null)
            {
                return;
            }

            if (!state.IsPet && state.CurrentMaster == null && !state.IsTravelStock)
            {
                youngRatkinStates.Remove(state);
                UnregisterYoungRatkinState(state);
            }
        }

        private void RegisterYoungRatkinState(YoungRatkinState state)
        {
            if (state?.Pawn == null)
            {
                return;
            }

            youngRatkinStateByPawn[state.Pawn] = state;
        }

        private void UnregisterYoungRatkinState(YoungRatkinState state)
        {
            if (state?.Pawn == null)
            {
                return;
            }

            youngRatkinStateByPawn.Remove(state.Pawn);
        }

        private void TryDropCarriedPawnAfterBecomingYoungRatkinPet(Pawn pawn)
        {
            ReleaseExternalToddlerHold(pawn);
            Pawn carrier = pawn?.CarriedBy;
            Pawn_CarryTracker carryTracker = carrier?.carryTracker;
            EndExternalToddlerHoldJobs(pawn, carrier);
            if (!LeadYourPetRules.ShouldDropCarriedPawnAfterBecomingYoungRatkinPet(
                isProtectedYoungRatkinPet: LeadYourPetUtility.IsProtectedLeashedYoungRatkin(pawn),
                pawnHasCarrier: carrier != null,
                carrierHasCarryTracker: carryTracker != null,
                carrierIsCarryingPawn: carryTracker?.CarriedThing == pawn))
            {
                return;
            }

            carryTracker.TryDropCarriedThing(carrier.Position, ThingPlaceMode.Near, out Thing _);
        }

        private void ReleaseExternalToddlerHold(Pawn pawn)
        {
            if (pawn?.jobs == null)
            {
                return;
            }

            if (!LeadYourPetUtility.IsExternalToddlerHold(pawn.CurJobDef?.defName, pawn.jobs.curDriver?.GetType().FullName))
            {
                return;
            }

            LeadYourPetUtility.TryDismountExternalToddlerHold(pawn);
            if (LeadYourPetUtility.IsExternalToddlerHold(pawn.CurJobDef?.defName, pawn.jobs.curDriver?.GetType().FullName))
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }
        }

        private void EndExternalToddlerHoldJobs(Pawn pawn, Pawn carrier)
        {
            if (LeadYourPetUtility.ShouldEndExternalToddlerHoldJob(pawn, pawn))
            {
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }

            if (LeadYourPetUtility.ShouldEndExternalToddlerHoldJob(carrier, pawn))
            {
                carrier.jobs?.EndCurrentJob(JobCondition.InterruptForced, true, true);
            }
        }
    }
}
