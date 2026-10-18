using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_MouseDisasterStory : ChoiceLetter
    {
        public string stage;
        private GameComponent_MouseDisasterNarrative Narrative => Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
        public override bool CanDismissWithRightClick => false;
        public override bool CanShowInLetterStack => base.CanShowInLetterStack && Narrative?.CanUseStoryLetter(stage) == true;
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref stage, "stage"); }
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || Narrative?.CanUseStoryLetter(stage) != true) { yield return Option_Close; yield break; }
                if (stage == "Envoy")
                {
                    yield return Choice("Trade");
                    if (!Narrative.HasEnvoyContact) yield return Choice("Verify");
                    yield return Choice("Reject");
                    yield return Choice("Drive");
                }
                else if (stage == "Return") { yield return Choice("Receive"); yield return Choice("Reject"); }
                else { yield return Choice("Ask"); yield return Choice("Quiet"); }
                if (lookTargets.IsValid()) yield return Option_JumpToLocationAndPostpone;
                yield return Option_Postpone;
            }
        }
        private DiaOption Choice(string action)
        {
            var option = new DiaOption(("MouseDisaster_Story_Choice" + action).Translate());
            option.action = delegate
            {
                string message = "MouseDisaster_Story_Stale".Translate();
                if (Narrative != null && Narrative.ResolveStoryChoice(stage, action, out message)) Find.LetterStack.RemoveLetter(this);
                else Messages.Message(message, MessageTypeDefOf.RejectInput);
            };
            option.resolveTree = true;
            return option;
        }
    }
}
