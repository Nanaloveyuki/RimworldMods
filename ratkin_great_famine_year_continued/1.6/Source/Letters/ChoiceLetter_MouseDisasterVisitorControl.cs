using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ChoiceLetter_MouseDisasterVisitorControl : ChoiceLetter
    {
        public List<Pawn> visitors;
        public Map map;
        public string incidentDefName;

        public override bool CanDismissWithRightClick => false;

        public override bool CanShowInLetterStack
        {
            get
            {
                return base.CanShowInLetterStack && MouseDisasterVisitorUtility.GetActiveVisitors(visitors).Count > 0;
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

                List<Pawn> validVisitors = MouseDisasterVisitorUtility.GetActiveVisitors(visitors);
                if (validVisitors.Count == 0)
                {
                    yield return Option_Close;
                    yield break;
                }

                List<Pawn> recruitableVisitors = MouseDisasterVisitorUtility.GetRecruitableVisitors(validVisitors);
                DiaOption temporaryRecruit = new DiaOption("MouseDisaster_VisitorControl_TemporaryRecruit".Translate(MouseDisasterVisitorUtility.TemporaryRecruitDurationLabel));
                if (recruitableVisitors.Count == 0)
                {
                    temporaryRecruit.Disable(null);
                }
                else
                {
                    temporaryRecruit.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryTemporaryRecruitAll(recruitableVisitors, out int recruitedCount))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_TemporaryRecruit_Success".Translate(recruitedCount, MouseDisasterVisitorUtility.TemporaryRecruitDurationLabel), recruitableVisitors, MessageTypeDefOf.PositiveEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    temporaryRecruit.resolveTree = true;
                }

                DiaOption hireAll = new DiaOption("MouseDisaster_VisitorControl_HireAll".Translate(MouseDisasterVisitorUtility.HiredWorkerDurationLabel));
                if (!MouseDisasterVisitorChoicePolicy.ShouldOfferBatchHire(incidentDefName, recruitableVisitors.Count))
                {
                    hireAll.Disable(null);
                }
                else
                {
                    hireAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryHireAll(recruitableVisitors, out int hiredCount))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_HireAll_Success".Translate(hiredCount, MouseDisasterVisitorUtility.HiredWorkerDurationLabel), recruitableVisitors, MessageTypeDefOf.PositiveEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    hireAll.resolveTree = true;
                }

                DiaOption joinAll = new DiaOption("MouseDisaster_VisitorControl_JoinAll".Translate());
                if (!MouseDisasterVisitorChoicePolicy.ShouldOfferBatchJoin(incidentDefName, validVisitors.Count))
                {
                    joinAll.Disable(null);
                }
                else
                {
                    joinAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryJoinAll(validVisitors, out int joinedCount))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_JoinAll_Success".Translate(joinedCount), validVisitors, MessageTypeDefOf.PositiveEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    joinAll.resolveTree = true;
                }

                DiaOption enslaveAll = new DiaOption("MouseDisaster_VisitorControl_EnslaveAll".Translate());
                if (!ModsConfig.IdeologyActive || recruitableVisitors.Count == 0)
                {
                    enslaveAll.Disable(null);
                }
                else
                {
                    enslaveAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryEnslaveAll(recruitableVisitors, out int enslavedCount, out string message))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_EnslaveAll_Success".Translate(enslavedCount), recruitableVisitors, MessageTypeDefOf.NeutralEvent, historical: false);
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
                if (recruitableVisitors.Count == 0)
                {
                    captureAll.Disable(null);
                }
                else
                {
                    captureAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryCaptureAll(recruitableVisitors, out int capturedCount, out string message))
                        {
                            Messages.Message("MouseDisaster_VisitorControl_CaptureAll_Success".Translate(capturedCount), recruitableVisitors, MessageTypeDefOf.NeutralEvent, historical: false);
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
                if (recruitableVisitors.Count == 0)
                {
                    attackAll.Disable(null);
                }
                else
                {
                    attackAll.action = delegate
                    {
                        if (MouseDisasterVisitorUtility.TryMakeHostile(recruitableVisitors, out int hostileCount) && hostileCount > 0)
                        {
                            Messages.Message("MouseDisaster_VisitorControl_ForcedAway_Success".Translate(hostileCount), recruitableVisitors, MessageTypeDefOf.NeutralEvent, historical: false);
                        }
                        else
                        {
                            Messages.Message("MouseDisaster_VisitorControl_NoValidTarget".Translate(), MessageTypeDefOf.RejectInput);
                        }

                        Find.LetterStack.RemoveLetter(this);
                    };
                    attackAll.resolveTree = true;
                }

                DiaOption giveFood = new DiaOption("MouseDisaster_FoodGive".Translate());
                giveFood.action = delegate
                {
                    if (MouseDisasterUtility.TryOrderFoodDelivery(map, validVisitors, out string message))
                    {
                        Messages.Message(message, validVisitors, MessageTypeDefOf.PositiveEvent, historical: false);
                        Find.LetterStack.RemoveLetter(this);
                    }
                    else
                    {
                        Messages.Message(message, MessageTypeDefOf.RejectInput);
                    }
                };
                giveFood.resolveTree = true;

                DiaOption ignore = new DiaOption("MouseDisaster_VisitorControl_NotImportant".Translate());
                ignore.action = delegate
                {
                    Find.LetterStack.RemoveLetter(this);
                };
                ignore.resolveTree = true;

                bool quarantineBlocked = MouseDisasterVisitorUtility.IsN007ControlBlocked(validVisitors);
                if (quarantineBlocked)
                {
                    string reason = "MouseDisaster_N007_VisitorControlBlocked".Translate();
                    temporaryRecruit.Disable(reason);
                    hireAll.Disable(reason);
                    joinAll.Disable(reason);
                    enslaveAll.Disable(reason);
                    captureAll.Disable(reason);
                    attackAll.Disable(reason);
                }

                yield return temporaryRecruit;
                yield return hireAll;
                yield return joinAll;
                yield return enslaveAll;
                yield return captureAll;
                yield return attackAll;
                yield return giveFood;

                if (MouseDisasterVisitorChoicePolicy.ShouldOfferPrisonTransfer(
                    incidentDefName,
                    validVisitors.Count,
                    MouseDisasterVisitorUtility.IsPrisonIntegrationEnabled()))
                {
                    DiaOption sendToPrison = new DiaOption("MouseDisaster_VisitorControl_SendToPrison".Translate());
                    if (quarantineBlocked)
                    {
                        sendToPrison.Disable("MouseDisaster_N007_VisitorControlBlocked".Translate());
                    }
                    else if (!MouseDisasterVisitorUtility.HasAvailablePrisonArea(map))
                    {
                        sendToPrison.Disable(null);
                    }
                    else
                    {
                        sendToPrison.action = delegate
                        {
                            if (MouseDisasterVisitorUtility.TrySendToPrison(validVisitors, map, out int imprisonedCount, out string message))
                            {
                                Messages.Message("MouseDisaster_VisitorControl_SendToPrison_Success".Translate(imprisonedCount), validVisitors, MessageTypeDefOf.PositiveEvent, historical: false);
                                Find.LetterStack.RemoveLetter(this);
                            }
                            else
                            {
                                Messages.Message(message, MessageTypeDefOf.RejectInput);
                            }
                        };
                        sendToPrison.resolveTree = true;
                    }

                    yield return sendToPrison;
                }

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
            Scribe_Collections.Look(ref visitors, "visitors", LookMode.Reference);
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref incidentDefName, "incidentDefName");
        }

        public override void OpenLetter()
        {
            if (MouseDisasterVisitorUtility.GetActiveVisitors(visitors).Count == 0)
            {
                Find.LetterStack.RemoveLetter(this);
                return;
            }

            base.OpenLetter();
        }
    }
}
