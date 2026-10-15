using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_MouseDisasterN007 : ChoiceLetter
    {
        public Map map;
        public int mapId = -1;
        public MouseDisasterN007LetterStage stage;

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

                if (stage == MouseDisasterN007LetterStage.Entry)
                {
                    yield return BuildInitialOption(
                        "MouseDisaster_N007_Quarantine",
                        MouseDisasterN007InitialDecision.Quarantine,
                        MessageTypeDefOf.NeutralEvent);
                    yield return BuildInitialOption(
                        "MouseDisaster_N007_Release",
                        MouseDisasterN007InitialDecision.Release,
                        MessageTypeDefOf.NegativeEvent);
                    yield return BuildInitialOption(
                        "MouseDisaster_N007_Defer",
                        MouseDisasterN007InitialDecision.Defer,
                        MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    yield return BuildRecoveryOption(
                        "MouseDisaster_N007_RecoveredRelease",
                        MouseDisasterN007RecoveryDecision.Release,
                        MessageTypeDefOf.PositiveEvent);
                    yield return BuildRecoveryOption(
                        "MouseDisaster_N007_RecoveredAccept",
                        MouseDisasterN007RecoveryDecision.Accept,
                        MessageTypeDefOf.PositiveEvent);
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
            Scribe_Values.Look(ref stage, "stage", MouseDisasterN007LetterStage.Entry);
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

        private DiaOption BuildInitialOption(string key, MouseDisasterN007InitialDecision decision, MessageTypeDef messageType)
        {
            DiaOption option = new DiaOption(key.Translate());
            option.action = delegate
            {
                string message = "MouseDisaster_N007_ActionFailed".Translate().ToString();
                GameComponent_MouseDisasterNarrative narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
                if (narrative != null && narrative.ResolveN007Initial(mapId, decision, out message))
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

        private DiaOption BuildRecoveryOption(string key, MouseDisasterN007RecoveryDecision decision, MessageTypeDef messageType)
        {
            DiaOption option = new DiaOption(key.Translate());
            option.action = delegate
            {
                string message = "MouseDisaster_N007_ActionFailed".Translate().ToString();
                GameComponent_MouseDisasterNarrative narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
                if (narrative != null && narrative.ResolveN007Recovery(mapId, decision, out message))
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
            return Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.CanShowN007Letter(mapId, stage) == true;
        }
    }
}
