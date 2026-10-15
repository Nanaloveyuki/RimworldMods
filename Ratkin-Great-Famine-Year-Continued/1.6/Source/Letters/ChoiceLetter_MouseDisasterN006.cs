using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_MouseDisasterN006 : ChoiceLetter
    {
        public Map map;
        public int mapId = -1;
        public MouseDisasterN006LetterStage stage;

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack
        {
            get
            {
                return base.CanShowInLetterStack && IsCurrentStageAvailable();
            }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !IsCurrentStageAvailable())
                {
                    yield return Option_Close;
                    yield break;
                }

                if (stage == MouseDisasterN006LetterStage.Entry)
                {
                    yield return BuildInitialOption(
                        "MouseDisaster_N006_Seal",
                        MouseDisasterN006InitialDecision.Seal,
                        MessageTypeDefOf.PositiveEvent);
                    yield return BuildInitialOption(
                        "MouseDisaster_N006_LeaveBait",
                        MouseDisasterN006InitialDecision.LeaveBait,
                        MessageTypeDefOf.NeutralEvent);
                    yield return BuildInitialOption(
                        "MouseDisaster_N006_ForceClean",
                        MouseDisasterN006InitialDecision.ForceClean,
                        MessageTypeDefOf.NeutralEvent);
                    yield return BuildInitialOption(
                        "MouseDisaster_N006_Ignore",
                        MouseDisasterN006InitialDecision.Ignore,
                        MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    yield return BuildBaitOption(
                        "MouseDisaster_N006_Trace",
                        MouseDisasterN006BaitDecision.Trace,
                        MessageTypeDefOf.PositiveEvent);
                    yield return BuildBaitOption(
                        "MouseDisaster_N006_Stop",
                        MouseDisasterN006BaitDecision.Stop,
                        MessageTypeDefOf.NeutralEvent);
                }

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
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref stage, "stage", MouseDisasterN006LetterStage.Entry);
        }

        public override void OpenLetter()
        {
            if (!IsCurrentStageAvailable())
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }

            base.OpenLetter();
        }

        private DiaOption BuildInitialOption(string key, MouseDisasterN006InitialDecision decision, MessageTypeDef messageType)
        {
            DiaOption option = new DiaOption(key.Translate());
            option.action = delegate
            {
                string message = "MouseDisaster_N006_ActionFailed".Translate().ToString();
                GameComponent_MouseDisasterNarrative narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
                if (narrative != null && narrative.ResolveN006Initial(mapId, decision, out message))
                {
                    Messages.Message(message, messageType);
                    Find.LetterStack.RemoveLetter(this);
                }
                else
                {
                    Messages.Message(message, MessageTypeDefOf.RejectInput);
                }
            };
            option.resolveTree = true;
            return option;
        }

        private DiaOption BuildBaitOption(string key, MouseDisasterN006BaitDecision decision, MessageTypeDef messageType)
        {
            DiaOption option = new DiaOption(key.Translate());
            option.action = delegate
            {
                string message = "MouseDisaster_N006_ActionFailed".Translate().ToString();
                GameComponent_MouseDisasterNarrative narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
                if (narrative != null && narrative.ResolveN006Bait(mapId, decision, out message))
                {
                    Messages.Message(message, messageType);
                    Find.LetterStack.RemoveLetter(this);
                }
                else
                {
                    Messages.Message(message, MessageTypeDefOf.RejectInput);
                }
            };
            option.resolveTree = true;
            return option;
        }

        private bool IsCurrentStageAvailable()
        {
            return Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.CanShowN006Letter(mapId, stage) == true;
        }
    }
}
