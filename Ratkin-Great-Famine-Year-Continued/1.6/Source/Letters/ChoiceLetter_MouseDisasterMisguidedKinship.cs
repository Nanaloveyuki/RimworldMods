using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{

    public class ChoiceLetter_MouseDisasterMisguidedKinship : ChoiceLetter
    {
        public List<Pawn> babies;
        public Map map;

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack
        {
            get
            {
                return base.CanShowInLetterStack &&
                       (babies?.Any(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned) == true);
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

                List<Pawn> valid = babies?.Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned).ToList() ?? new List<Pawn>();
                if (valid.Count == 0)
                {
                    yield return Option_Close;
                    yield break;
                }

                DiaOption accept = new DiaOption("MouseDisaster_UI_AcceptKinship".Translate().Resolve());
                accept.action = delegate
                {
                    for (int i = 0; i < valid.Count; i++)
                    {
                        valid[i].SetFaction(Faction.OfPlayer);
                        MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(valid[i]);
                    }

                    Messages.Message("MouseDisaster_UI_KinshipAccepted".Translate().Resolve(), valid, MessageTypeDefOf.PositiveEvent, historical: false);
                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;
                yield return accept;

                DiaOption reject = new DiaOption("MouseDisaster_UI_Reject".Translate().Resolve());
                reject.action = delegate
                {
                    Messages.Message("MouseDisaster_UI_KinshipRejected".Translate().Resolve(), valid, MessageTypeDefOf.NeutralEvent, historical: false);
                    Find.LetterStack.RemoveLetter(this);
                };
                reject.resolveTree = true;
                yield return reject;

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
            Scribe_Collections.Look(ref babies, "babies", LookMode.Reference);
            Scribe_References.Look(ref map, "map");
        }

        public override void OpenLetter()
        {
            if (!ArchivedOnly && !(babies?.Any(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned) == true))
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }

            base.OpenLetter();
        }
    }
}
