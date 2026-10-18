using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_FamineRefugees : ChoiceLetter
    {
        public List<Pawn> refugees;

        public Map map;

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack
        {
            get
            {
                return base.CanShowInLetterStack && GetValidRefugees().Count > 0;
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

                List<Pawn> validRefugees = GetValidRefugees();
                if (validRefugees.Count == 0)
                {
                    yield return Option_Close;
                    yield break;
                }

                DiaOption accept = new DiaOption("MouseDisaster_UI_AllowJoin".Translate().Resolve());
                accept.action = delegate
                {
                    List<Pawn> acceptedRefugees = GetValidRefugees();
                    for (int i = 0; i < acceptedRefugees.Count; i++)
                    {
                        if (!MouseDisasterVisitorUtility.TryJoin(acceptedRefugees[i]))
                        {
                            acceptedRefugees[i].SetFaction(Faction.OfPlayer);
                        }
                    }

                    Messages.Message("MouseDisaster_UI_RefugeesAccepted".Translate().Resolve(), acceptedRefugees, MessageTypeDefOf.PositiveEvent, historical: false);
                    Find.LetterStack.RemoveLetter(this);
                };
                accept.resolveTree = true;

                List<Pawn> recruitableRefugees = MouseDisasterVisitorUtility.GetRecruitableVisitors(validRefugees);
                DiaOption temporaryRecruit = new DiaOption("MouseDisaster_VisitorControl_TemporaryRecruit".Translate(MouseDisasterVisitorUtility.TemporaryRecruitDurationLabel));
                if (recruitableRefugees.Count == 0)
                {
                    temporaryRecruit.Disable(null);
                }
                else
                {
                    temporaryRecruit.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryTemporaryRecruitAll(recruitableRefugees, out int recruitedCount))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_TemporaryRecruit_Success".Translate(recruitedCount, MouseDisasterVisitorUtility.TemporaryRecruitDurationLabel), recruitableRefugees, MessageTypeDefOf.PositiveEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    temporaryRecruit.resolveTree = true;
                }

                DiaOption enslaveAll = new DiaOption("MouseDisaster_VisitorControl_EnslaveAll".Translate());
                if (!ModsConfig.IdeologyActive || recruitableRefugees.Count == 0)
                {
                    enslaveAll.Disable(null);
                }
                else
                {
                    enslaveAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryEnslaveAll(recruitableRefugees, out int enslavedCount, out string message))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_EnslaveAll_Success".Translate(enslavedCount), recruitableRefugees, MessageTypeDefOf.NeutralEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message(message, MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    enslaveAll.resolveTree = true;
                }

                DiaOption captureAll = new DiaOption("MouseDisaster_VisitorControl_CaptureAll".Translate());
                if (recruitableRefugees.Count == 0)
                {
                    captureAll.Disable(null);
                }
                else
                {
                    captureAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryCaptureAll(recruitableRefugees, out int capturedCount, out string message))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_CaptureAll_Success".Translate(capturedCount), recruitableRefugees, MessageTypeDefOf.NeutralEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message(message, MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    captureAll.resolveTree = true;
                }

                DiaOption attackAll = new DiaOption("MouseDisaster_VisitorControl_AttackAll".Translate());
                if (recruitableRefugees.Count == 0)
                {
                    attackAll.Disable(null);
                }
                else
                {
                    attackAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryMakeHostile(recruitableRefugees, out int hostileCount) && hostileCount > 0)
                        {
                            Messages.Message("MouseDisaster_VisitorControl_ForcedAway_Success".Translate(hostileCount), recruitableRefugees, MessageTypeDefOf.NeutralEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    attackAll.resolveTree = true;
                }

                DiaOption reject = new DiaOption("MouseDisaster_UI_Reject".Translate().Resolve());
                reject.action = delegate
                {
                    List<Pawn> currentRefugees = GetValidRefugees();
                    Map targetMap = ResolveMap(currentRefugees);
                    if (targetMap != null && currentRefugees.Count > 0)
                    {
                        MouseDisasterUtility.MakeTravelAndExitLord(targetMap, currentRefugees, targetMap.Center, includeBabiesInExit: false);
                    }

                    Messages.Message("MouseDisaster_UI_RefugeesRejected".Translate().Resolve(), currentRefugees, MessageTypeDefOf.NeutralEvent, historical: false);
                    Find.LetterStack.RemoveLetter(this);
                };
                reject.resolveTree = true;

                yield return accept;
                yield return temporaryRecruit;
                yield return enslaveAll;
                yield return captureAll;
                yield return attackAll;
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
            Scribe_Collections.Look(ref refugees, "refugees", LookMode.Reference);
            Scribe_References.Look(ref map, "map");
        }

        public override void OpenLetter()
        {
            if (GetValidRefugees().Count == 0)
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }

            base.OpenLetter();
        }

        private List<Pawn> GetValidRefugees()
        {
            return refugees?.Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned).ToList() ?? new List<Pawn>();
        }

        private Map ResolveMap(List<Pawn> validRefugees)
        {
            if (map != null)
            {
                return map;
            }

            return validRefugees.FirstOrDefault()?.Map;
        }
    }
}
