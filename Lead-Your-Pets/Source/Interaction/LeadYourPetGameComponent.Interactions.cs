using RimWorld;
using Verse;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public void TriggerManualInteraction(Pawn master, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (master == null || pet == null)
            {
                return;
            }

            if (!LeadYourPetUtility.CanTriggerInteraction(master, pet, kind, out _))
            {
                return;
            }

            if (!LeadYourPetUtility.IsBidirectionalInteraction(kind) && !LeadYourPetUtility.CanUseSingleDirectionInteractions(master, pet))
            {
                return;
            }

            ApplyInteraction(master, pet, kind, LeadYourPetUtility.IsBidirectionalInteraction(kind), false);
        }

        private void ApplyInteraction(Pawn master, Pawn pet, LeadYourPetInteractionKind kind, bool applyPhysicalEffects)
        {
            ApplyInteraction(master, pet, kind, applyPhysicalEffects, true);
        }

        private void ApplyInteraction(Pawn master, Pawn pet, LeadYourPetInteractionKind kind, bool applyPhysicalEffects, bool automatic)
        {
            if (GodHandsCompat.IsGrabbed(master) || GodHandsCompat.IsGrabbed(pet)
                || !LeadYourPetUtility.CanPerformInteraction(pet, kind, out _))
            {
                return;
            }

            if (LeadYourPetUtility.ShouldPreserveMapExitMovement(pet)
                || (automatic && ShouldSuppressTravelAutoInteraction(GetLinkForPet(pet))))
            {
                return;
            }

            if (applyPhysicalEffects)
            {
                LeadYourPetUtility.ApplyInteractionPhysicalEffects(master, pet, kind);
            }

            LeadYourPetUtility.ApplyInteractionMemories(master, pet, kind);
            LeadYourPetUtility.ApplyInteractionJoy(master, pet, kind);
            ApplyImmediateInteractionEffects(master, pet, kind);
            ShowInteractionText(pet, kind);
            StartInteractionAnimation(master, pet, kind);
        }

        private void ApplyImmediateInteractionEffects(Pawn master, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (pet == null || LeadYourPetUtility.ShouldPreserveMapExitMovement(pet))
            {
                return;
            }

            switch (kind)
            {
                case LeadYourPetInteractionKind.TapHead:
                case LeadYourPetInteractionKind.Pat:
                    pet.pather?.StopDead();
                    pet.stances?.stunner?.StunFor(120, master, addBattleLog: false, showMote: true);
                    break;
                case LeadYourPetInteractionKind.PullTail:
                case LeadYourPetInteractionKind.PullEar:
                    pet.pather?.StopDead();
                    pet.stances?.stunner?.StunFor(240, master, addBattleLog: false, showMote: false);
                    EmitCryingMotes(pet);
                    break;
                case LeadYourPetInteractionKind.Whirl:
                    pet.pather?.StopDead();
                    pet.stances?.stunner?.StunFor(AnimationDurationFor(kind) * 2, master, addBattleLog: false, showMote: false);
                    break;
                case LeadYourPetInteractionKind.Slap:
                    pet.pather?.StopDead();
                    pet.stances?.stunner?.StunFor(AnimationDurationFor(kind) * 2, master, addBattleLog: false, showMote: false);
                    EmitCryingMotes(pet);
                    break;
                case LeadYourPetInteractionKind.Kick:
                case LeadYourPetInteractionKind.KickButt:
                    pet.pather?.StopDead();
                    pet.stances?.stunner?.StunFor(AnimationDurationFor(kind) * 2, master, addBattleLog: false, showMote: false);
                    break;
                case LeadYourPetInteractionKind.Drag:
                    pet.pather?.StopDead();
                    break;
            }
        }

        private void ShowInteractionText(Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (pet?.Map == null || !pet.Spawned || LeadYourPetMod.Settings != null && !LeadYourPetMod.Settings.showInteractionText)
            {
                return;
            }

            MoteMaker.ThrowText(LeadYourPetUtility.GetVisualDrawPos(pet), pet.Map, LeadYourPetUtility.InteractionLabel(pet, kind), 3f);
        }

        public void NotifyMouseEggFedByMasterMeal(LeashLink link)
        {
            if (link?.Master == null || link.Pet == null)
            {
                return;
            }

            ApplyInteraction(link.Master, link.Pet, LeadYourPetInteractionKind.Fed, false);
        }
    }
}
