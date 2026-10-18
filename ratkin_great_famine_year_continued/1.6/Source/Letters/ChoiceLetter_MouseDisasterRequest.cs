using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_MouseDisasterRequest : ChoiceLetter
    {
        public Map map;
        public int amount;
        public MouseDisasterRequestKind requestKind;
        public bool createsIntelSite;
        public MouseDisasterIntelSiteKind intelSiteKind;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }

                DiaOption accept = new DiaOption("MouseDisaster_UI_Deliver".Translate().Resolve());
                accept.action = delegate
                {
                    Site site = null;
                    if (createsIntelSite && !MouseDisasterPhase2Utility.TryCreateIntelSite(map, intelSiteKind, out site, out string intelFailure))
                    {
                        Messages.Message(intelFailure, MessageTypeDefOf.RejectInput);
                        return;
                    }

                    if (!MouseDisasterPhase2Utility.TryConsumeRequest(map, requestKind, amount, out string failure))
                    {
                        if (site != null)
                        {
                            Find.WorldObjects.Remove(site);
                        }

                        Messages.Message(failure, MessageTypeDefOf.RejectInput);
                        return;
                    }

                    if (createsIntelSite)
                    {
                        Find.LetterStack.ReceiveLetter("MouseDisaster_UI_IntelLetterLabel".Translate().Resolve(), "MouseDisaster_UI_IntelSiteDiscovered".Translate().Resolve(), LetterDefOf.PositiveEvent, site);
                    }
                    else
                    {
                        Messages.Message("MouseDisaster_UI_RequestCompleted".Translate().Resolve(), MessageTypeDefOf.PositiveEvent);
                    }

                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;

                DiaOption reject = new DiaOption("MouseDisaster_UI_Reject".Translate().Resolve());
                reject.action = delegate
                {
                    Messages.Message("MouseDisaster_UI_RequestRejected".Translate().Resolve(), MessageTypeDefOf.NeutralEvent);
                    Find.LetterStack.RemoveLetter(this);
                };
                reject.resolveTree = true;

                yield return accept;
                yield return reject;
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref amount, "amount", 0);
            Scribe_Values.Look(ref requestKind, "requestKind", MouseDisasterRequestKind.SimpleMeal);
            Scribe_Values.Look(ref createsIntelSite, "createsIntelSite", false);
            Scribe_Values.Look(ref intelSiteKind, "intelSiteKind", MouseDisasterIntelSiteKind.Treasure);
        }
    }
}
