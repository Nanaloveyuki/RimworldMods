using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_MouseDisasterAbandonedChildren : ChoiceLetter
    {
        public Pawn mother;
        public List<Pawn> children;
        public Map map;
        public IntVec3 foodCell = IntVec3.Invalid;

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack
        {
            get
            {
                return base.CanShowInLetterStack && GetValidPawns().Count > 0;
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

                List<Pawn> validPawns = GetValidPawns();
                if (validPawns.Count == 0)
                {
                    yield return Option_Close;
                    yield break;
                }

                DiaOption accept = new DiaOption("MouseDisaster_N004_Accept".Translate());
                accept.action = delegate
                {
                    validPawns = GetValidPawns();
                    ResolveDecision(MouseDisasterN004Decision.Accepted);
                    MouseDisasterUtility.CancelAbandonedDelivery(mother, children);
                    for (int i = 0; i < validPawns.Count; i++)
                    {
                        validPawns[i].SetFaction(Faction.OfPlayer);
                    }

                    Messages.Message("MouseDisaster_N004_AcceptMessage".Translate(), validPawns, MessageTypeDefOf.PositiveEvent, historical: false);
                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;

                DiaOption reject = new DiaOption("MouseDisaster_N004_Reject".Translate());
                reject.action = delegate
                {
                    validPawns = GetValidPawns();
                    ResolveDecision(MouseDisasterN004Decision.Rejected);
                    MouseDisasterUtility.CancelAbandonedDelivery(mother, children);
                    if (IsEligiblePawn(mother) && mother.Spawned)
                    {
                        MouseDisasterUtility.MakeTravelAndExitLord(map, new[] { mother }, map.Center);
                    }

                    List<Pawn> livingChildren = (children ?? new List<Pawn>()).Where(IsEligiblePawn).ToList();
                    for (int i = 0; i < livingChildren.Count; i++)
                    {
                        livingChildren[i].SetFaction(null);
                    }

                    Messages.Message("MouseDisaster_N004_RejectMessage".Translate(), validPawns, MessageTypeDefOf.NeutralEvent, historical: false);
                    Find.LetterStack.RemoveLetter(this);
                };
                reject.resolveTree = true;

                DiaOption ignore = new DiaOption("MouseDisaster_N004_Ignore".Translate());
                ignore.action = delegate
                {
                    ResolveDecision(MouseDisasterN004Decision.Ignored);
                    if (IsEligiblePawn(mother) && mother.Spawned)
                        MouseDisasterUtility.RegisterAbandonedDelivery(mother, (children ?? new List<Pawn>()).Where(IsEligiblePawn), foodCell);
                    Find.LetterStack.RemoveLetter(this);
                };
                ignore.resolveTree = true;

                yield return accept;
                yield return reject;
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
            Scribe_References.Look(ref mother, "mother");
            Scribe_Collections.Look(ref children, "children", LookMode.Reference);
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref foodCell, "foodCell", IntVec3.Invalid);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                children ??= new List<Pawn>();
                children.RemoveAll(child => child == null);
            }
        }

        public override void OpenLetter()
        {
            if (GetValidPawns().Count == 0)
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }

            base.OpenLetter();
        }

        private void ResolveDecision(MouseDisasterN004Decision decision)
        {
            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.RecordN004Decision(mother, decision);
        }

        private List<Pawn> GetValidPawns()
        {
            List<Pawn> result = new List<Pawn>();
            if (IsEligiblePawn(mother))
            {
                result.Add(mother);
            }

            if (children != null)
            {
                result.AddRange(children.Where(IsEligiblePawn));
            }

            return result.Distinct().ToList();
        }

        private bool IsEligiblePawn(Pawn pawn)
        {
            return map != null && pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.MapHeld == map &&
                   !MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn);
        }
    }
}
