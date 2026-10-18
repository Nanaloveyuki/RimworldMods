using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LeadYourPet
{
    public class FloatMenuOptionProvider_LeadYourPetAnchor : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool RequiresManipulation => true;

        protected override bool AppliesInt(FloatMenuContext context)
        {
            return LeadYourPetUtility.CanPlayerCommand(context.FirstSelectedPawn)
                && LeadYourPetUtility.Component != null
                && LeadYourPetUtility.Component.AnyLeashedPetFor(context.FirstSelectedPawn);
        }

        protected override FloatMenuOption GetSingleOption(FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || !context.ClickedCell.IsValid || !context.ClickedCell.InBounds(context.map))
            {
                return null;
            }

            return new FloatMenuOption("LeadYourPet_AnchorPetsHere".Translate().Resolve(), delegate
            {
                LeadYourPetUtility.Component.AnchorLeashedPetsToCell(actor, context.ClickedCell);
            }, MenuOptionPriority.Low);
        }

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (clickedThing is Pawn)
            {
                return null;
            }

            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || clickedThing == null || clickedThing == actor)
            {
                return null;
            }

            return new FloatMenuOption("LeadYourPet_AnchorPetsToThing".Translate(clickedThing.LabelShort).Resolve(), delegate
            {
                LeadYourPetUtility.Component.AnchorLeashedPetsToThing(actor, clickedThing);
            }, MenuOptionPriority.Low);
        }

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || clickedPawn == null || clickedPawn == actor || !clickedPawn.Spawned || clickedPawn.Map != actor.Map)
            {
                return null;
            }

            return new FloatMenuOption("LeadYourPet_AnchorPetsToPawn".Translate(clickedPawn.LabelShort).Resolve(), delegate
            {
                LeadYourPetUtility.Component.AnchorLeashedPetsToCell(actor, clickedPawn.Position);
            }, MenuOptionPriority.Low);
        }
    }
}
