using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_MouseDisasterFoodGive : ChoiceLetter
    {
        public List<Pawn> recipients;
        public Map map;

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack
        {
            get
            {
                return base.CanShowInLetterStack && GetValidRecipients().Count > 0;
            }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }

                List<Pawn> validRecipients = GetValidRecipients();
                if (validRecipients.Count == 0)
                {
                    yield return Option_Close;
                    yield break;
                }

                DiaOption giveFood = new DiaOption("MouseDisaster_FoodGive".Translate());
                giveFood.action = delegate
                {
                    if (MouseDisasterUtility.TryOrderFoodDelivery(ResolveMap(validRecipients), validRecipients, out string message))
                    {
                        Messages.Message(message, validRecipients, MessageTypeDefOf.PositiveEvent, historical: false);
                        Find.LetterStack.RemoveLetter(this);
                    }
                    else
                    {
                        Messages.Message(message, MessageTypeDefOf.RejectInput);
                    }
                };
                giveFood.resolveTree = true;

                DiaOption ignore = new DiaOption("MouseDisaster_Letter_Ignore".Translate());
                ignore.action = delegate
                {
                    Find.LetterStack.RemoveLetter(this);
                };
                ignore.resolveTree = true;

                yield return giveFood;
                yield return ignore;
                if (lookTargets.IsValid())
                {
                    yield return Option_JumpToLocationAndPostpone;
                }

                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref recipients, "recipients", LookMode.Reference);
            Scribe_References.Look(ref map, "map");
        }

        public override void OpenLetter()
        {
            if (GetValidRecipients().Count == 0)
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }

            base.OpenLetter();
        }

        private List<Pawn> GetValidRecipients()
        {
            return recipients?.Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned).ToList() ?? new List<Pawn>();
        }

        private Map ResolveMap(List<Pawn> validRecipients)
        {
            if (map != null)
            {
                return map;
            }

            return validRecipients.FirstOrDefault()?.Map;
        }
    }
}
