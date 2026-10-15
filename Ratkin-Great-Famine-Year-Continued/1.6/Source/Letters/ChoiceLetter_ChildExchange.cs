using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_ChildExchange : ChoiceLetter
    {
        public Pawn trader;

        public Map map;

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack
        {
            get
            {
                return base.CanShowInLetterStack && MouseDisasterUtility.IsChildExchangeTrader(trader);
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

                if (!MouseDisasterUtility.IsChildExchangeTrader(trader))
                {
                    yield return Option_Close;
                    yield break;
                }

                yield return BuildOption(MouseDisasterUtility.ChildExchangeModeColonist, "MouseDisaster_UI_ExchangeColonistBaby".Translate().Resolve());
                yield return BuildOption(MouseDisasterUtility.ChildExchangeModeSlave, "MouseDisaster_UI_ExchangeSlaveBaby".Translate().Resolve());
                yield return BuildOption(MouseDisasterUtility.ChildExchangeModePrisoner, "MouseDisaster_UI_ExchangePrisonerBaby".Translate().Resolve());
                yield return BuildRejectOption();

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
            Scribe_References.Look(ref trader, "trader");
            Scribe_References.Look(ref map, "map");
        }

        public override void OpenLetter()
        {
            if (!MouseDisasterUtility.IsChildExchangeTrader(trader))
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }

            base.OpenLetter();
        }

        private DiaOption BuildOption(int mode, string prefix)
        {
            Pawn baby = MouseDisasterUtility.FindExchangeOfferBaby(map ?? trader?.Map, mode);
            string label = baby == null
                ? "MouseDisaster_UI_ExchangeNoTarget".Translate(prefix).Resolve()
                : "MouseDisaster_UI_ExchangeTarget".Translate(prefix, baby.LabelShortCap).Resolve();

            DiaOption option = new DiaOption(label);
            if (baby == null)
            {
                option.Disable(null);
                return option;
            }

            option.action = delegate
            {
                if (MouseDisasterUtility.TryExecuteChildExchange(trader, mode, out string message))
                {
                    Messages.Message(message, trader, MessageTypeDefOf.PositiveEvent, historical: false);
                }
                else
                {
                    Messages.Message(message, trader, MessageTypeDefOf.RejectInput, historical: false);
                }

                Find.LetterStack.RemoveLetter(this);
            };
            option.resolveTree = true;
            return option;
        }

        private DiaOption BuildRejectOption()
        {
            DiaOption option = new DiaOption("MouseDisaster_N005_Reject".Translate());
            option.action = delegate
            {
                if (MouseDisasterUtility.TryRejectChildExchange(trader, out string message))
                {
                    Messages.Message(message, trader, MessageTypeDefOf.NeutralEvent, historical: false);
                }
                else
                {
                    Messages.Message(message, trader, MessageTypeDefOf.RejectInput, historical: false);
                }

                Find.LetterStack.RemoveLetter(this);
            };
            option.resolveTree = true;
            return option;
        }

    }
}
