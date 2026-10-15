using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class FloatMenuOptionProvider_MouseDisasterVisitorControl : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool CanSelfTarget => true;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            Pawn actor = context.FirstSelectedPawn;
            if (actor == null || actor.Faction != Faction.OfPlayer || clickedPawn == null || !MouseDisasterVisitorUtility.IsManagedVisitor(clickedPawn))
            {
                yield break;
            }

            if (MouseDisasterVisitorUtility.CanHire(clickedPawn))
            {
                yield return new FloatMenuOption("MouseDisaster_VisitorControl_Hire".Translate(MouseDisasterVisitorUtility.HiredWorkerDurationLabel), delegate
                {
                    if (MouseDisasterVisitorUtility.TryHire(clickedPawn))
                    {
                        Messages.Message("MouseDisaster_VisitorControl_Hire_Success".Translate(clickedPawn.Named("PAWN"), MouseDisasterVisitorUtility.HiredWorkerDurationLabel), clickedPawn, MessageTypeDefOf.PositiveEvent, historical: false);
                    }
                    else
                    {
                        Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }, MenuOptionPriority.Low);
            }

            if (MouseDisasterVisitorUtility.CanEndHire(clickedPawn))
            {
                yield return new FloatMenuOption("MouseDisaster_VisitorControl_EndHire".Translate(), delegate
                {
                    if (MouseDisasterVisitorUtility.TryEndHire(clickedPawn))
                    {
                        Messages.Message("MouseDisaster_VisitorControl_EndHire_Success".Translate(clickedPawn.Named("PAWN")), clickedPawn, MessageTypeDefOf.NeutralEvent, historical: false);
                    }
                    else
                    {
                        Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }, MenuOptionPriority.Low);
            }

            if (MouseDisasterVisitorUtility.CanJoin(clickedPawn))
            {
                yield return new FloatMenuOption("MouseDisaster_VisitorControl_Join".Translate(), delegate
                {
                    if (MouseDisasterVisitorUtility.TryJoin(clickedPawn))
                    {
                        Messages.Message("MouseDisaster_VisitorControl_Join_Success".Translate(clickedPawn.Named("PAWN")), clickedPawn, MessageTypeDefOf.PositiveEvent, historical: false);
                    }
                    else
                    {
                        Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }, MenuOptionPriority.Low);
            }
        }
    }
}
