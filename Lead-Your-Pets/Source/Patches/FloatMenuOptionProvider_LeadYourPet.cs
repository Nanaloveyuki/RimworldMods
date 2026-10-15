using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LeadYourPet
{
    public class FloatMenuOptionProvider_LeadYourPet : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            return LeadYourPetUtility.CanPlayerCommand(context.FirstSelectedPawn);
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || clickedPawn == null)
            {
                yield break;
            }

            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component == null)
            {
                yield break;
            }

            if (LeadYourPetUtility.HasFixedAnchor(component.GetLinkForPet(clickedPawn)))
            {
                string label = "LeadYourPet_FeedLeashedPet".Translate(clickedPawn.LabelShort).Resolve();
                if (!actor.CanReach(clickedPawn, PathEndMode.Touch, Danger.Deadly))
                {
                    yield return DisabledOption(label, "LeadYourPet_Reason_Unreachable", true);
                }
                else if (!LeadYourPetUtility.TryFindManualFeedFood(actor, clickedPawn, out _, out string reason))
                {
                    yield return DisabledOption(label, reason, true);
                }
                else
                {
                    yield return new FloatMenuOption(label, delegate
                    {
                        LeadYourPetUtility.TryFeedTarget(actor, clickedPawn, out _, out _);
                    }, MenuOptionPriority.InitiateSocial);
                }
            }
            if (LeadYourPetUtility.IsEligibleAnimalPet(clickedPawn))
            {
                foreach (FloatMenuOption option in AnimalOptions(actor, clickedPawn, component))
                {
                    yield return option;
                }
            }

            if (LeadYourPetUtility.IsMouseEgg(clickedPawn))
            {
                foreach (FloatMenuOption option in MouseEggOptions(actor, clickedPawn, component))
                {
                    yield return option;
                }
            }
        }

        private IEnumerable<FloatMenuOption> AnimalOptions(Pawn actor, Pawn pet, LeadYourPetGameComponent component)
        {
            LeashLink current = component.GetLinkForPet(pet);
            if (current != null && current.Master == actor)
            {
                yield return new FloatMenuOption(T("LeadYourPet_StopLeash"), delegate
                {
                    component.EndLeashForPet(pet);
                }, MenuOptionPriority.Default);
            }

            if (!actor.CanReach(pet, PathEndMode.Touch, Danger.Deadly))
            {
                yield return DisabledOption("LeadYourPet_LeadPet", "LeadYourPet_Reason_Unreachable");
                yield break;
            }

            if (current == null || current.Master != actor)
            {
                yield return new FloatMenuOption(T("LeadYourPet_LeadPet"), delegate
                {
                    component.StartLeash(actor, pet, false);
                }, MenuOptionPriority.InitiateSocial);
            }
        }

        private IEnumerable<FloatMenuOption> MouseEggOptions(Pawn actor, Pawn pet, LeadYourPetGameComponent component)
        {
            MouseEggState state = component.GetMouseEggState(pet);
            LeashLink current = component.GetLinkForPet(pet);
            bool ownedByActor = (state != null && state.CurrentMaster == actor) || (current != null && current.Master == actor && current.Kind == LeashLinkKind.MouseEggPet);
            bool canBePet = LeadYourPetUtility.CanBePetMouseEgg(pet, out string petBlockReasonKey);

            if (ownedByActor)
            {
                yield return new FloatMenuOption(T("LeadYourPet_StopMouseEggLeash"), delegate
                {
                    component.ClearMouseEggPetState(pet);
                }, MenuOptionPriority.Default);

                if (!canBePet)
                {
                    yield return DisabledOption("LeadYourPet_MouseEggNoLongerPettable", petBlockReasonKey);
                    yield break;
                }

                bool bidirectionalBlocked = LeadYourPetUtility.ShouldBlockColonistBidirectionalInteraction(actor, pet, LeadYourPetUtility.BidirectionalInteractions.First());
                if (LeadYourPetRules.ShouldOfferManualMouseEggInteractionMenu(ownedByActor, true, bidirectionalBlocked))
                {
                    yield return BuildInteractionMenu("LeadYourPet_MouseEggTwoWay", actor, pet, component, LeadYourPetUtility.BidirectionalInteractions);
                }
                yield break;
            }

            if (!canBePet)
            {
                yield return DisabledOption("LeadYourPet_SetAsPetMouseEggAndLeash", petBlockReasonKey);
                yield break;
            }

            if (!LeadYourPetUtility.CanStartPlayerPetLeash(actor, pet, true, out string leashReasonKey))
            {
                yield return DisabledOption("LeadYourPet_SetAsPetMouseEggAndLeash", leashReasonKey);
                yield break;
            }

            if (!actor.CanReach(pet, PathEndMode.Touch, Danger.Deadly))
            {
                yield return DisabledOption("LeadYourPet_SetAsPetMouseEggAndLeash", "LeadYourPet_Reason_Unreachable");
                yield break;
            }

            yield return new FloatMenuOption(T("LeadYourPet_SetAsPetMouseEggAndLeash"), delegate
            {
                component.StartLeash(actor, pet, true);
            }, MenuOptionPriority.InitiateSocial);
        }

        private FloatMenuOption BuildInteractionMenu(string labelKey, Pawn actor, Pawn pet, LeadYourPetGameComponent component, IEnumerable<LeadYourPetInteractionKind> kinds)
        {
            if (!actor.CanReach(pet, PathEndMode.Touch, Danger.Deadly))
            {
                return DisabledOption(labelKey, "LeadYourPet_Reason_Unreachable");
            }

            return new FloatMenuOption(T(labelKey), delegate
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (LeadYourPetInteractionKind kind in kinds)
                {
                    LeadYourPetInteractionKind localKind = kind;
                    if (!LeadYourPetUtility.CanTriggerInteraction(actor, pet, localKind, out string partReasonKey))
                    {
                        if (!partReasonKey.NullOrEmpty())
                        {
                            options.Add(DisabledOption(LeadYourPetUtility.InteractionLabel(pet, localKind), partReasonKey, true));
                        }

                        continue;
                    }

                    options.Add(new FloatMenuOption(LeadYourPetUtility.InteractionLabel(pet, localKind), delegate
                    {
                        component.TriggerManualInteraction(actor, pet, localKind);
                    }, MenuOptionPriority.Low));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }, MenuOptionPriority.Low);
        }

        private static FloatMenuOption DisabledOption(string labelKey, string reasonKey)
        {
            return new FloatMenuOption(F("LeadYourPet_LabelWithReason", T(labelKey), T(reasonKey)), null);
        }

        private static FloatMenuOption DisabledOption(string label, string reasonKey, bool rawLabel = true)
        {
            return new FloatMenuOption(F("LeadYourPet_LabelWithReason", label, T(reasonKey)), null);
        }

        private static string T(string key)
        {
            return key.Translate().Resolve();
        }

        private static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }
    }
}
