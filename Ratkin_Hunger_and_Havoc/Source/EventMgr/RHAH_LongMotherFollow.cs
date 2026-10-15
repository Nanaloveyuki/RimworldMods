using System;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal static class RHAH_LongMotherFollow
    {
        const int Returning = 0;
        const int Recognition = 1;
        const int Meeting = 2;
        const int TakeChild = 3;
        const int RefusedWait = 4;
        const int LivingChild = 5;
        const int CalledMother = 6;
        const int PaidStay = 7;
        const int Burial = 8;
        const int DuskWait = 9;
        const int TransferredChild = 10;
        const int PairLeaving = 11;
        const int BegDelay = 12;
        const int Begging = 13;
        const int InquiryDelay = 14;
        const int Inquiry = 15;
        const int Theft = 16;
        const int Leaving = 17;
        const int DeathMemory = 18;
        const int ChildUnknown = 0;
        const int ChildLiving = 1;
        const int ChildDead = 2;
        const int ChildTransferred = 3;
        const string Prefix = "RHAH_LongMother_";

        internal static bool Handles(string id)
        {
            return id == "I-002" || id == "I-037";
        }

        internal static string TextKey(RHAH_LongFollowRecord record)
        {
            if (record.MedicalHold)
            {
                return Prefix + (record.DisplayId == "I-037" ? "TreatmentUntilHealed" : "TreatmentUntilWalking");
            }
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            if (record.Phase != Inquiry && RHAH_LongFollowRuntime.MedicallyPaused(RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick)))
            {
                return Prefix + (record.DisplayId == "I-002" ? "Collapsed" : "CollapsedIll");
            }
            switch (record.Phase)
            {
                case Recognition: return Prefix + "Recognition";
                case Meeting: return Prefix + "Meeting";
                case TakeChild: return Prefix + "TakeChild";
                case RefusedWait: return Prefix + "RefusedWait";
                case LivingChild: return Prefix + "LivingChild";
                case CalledMother: return Prefix + "CalledMother";
                case PaidStay: return Prefix + "PaidStay";
                case Burial: return Prefix + "Burial";
                case DuskWait: return Prefix + "DuskWait";
                case TransferredChild: return Prefix + "TransferredChild";
                case Begging: return Prefix + "Begging";
                case Inquiry: return Prefix + "Inquiry";
                case Theft: return Prefix + "Theft";
                default: return Prefix + "Leaving";
            }
        }

        internal static bool Ready(RHAH_LongFollowRecord record, Map map, int tick)
        {
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (record.Phase == Returning)
            {
                if (record.DueTick >= 0 && tick < record.DueTick) return false;
                Verse.Pawn source = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
                if (source == null || source.Dead) return true;
                if (record.DisplayId == "I-002" && MeetingDisabled(record, map, tick) != null)
                {
                    bool found = false;
                    for (int i = 0; i < record.ChildIds.Count; i++)
                    {
                        Verse.Pawn candidate = RHAH_LongFollowRuntime.Pawn(record.ChildIds[i], tick);
                        if (RHAH_LongFollowRuntime.AtHome(candidate, map) && RHAH_LongFollowRuntime.Adult(candidate)) { found = true; break; }
                    }
                    if (!found) return false;
                }
                if (record.DisplayId == "I-037")
                {
                    if (record.SubjectId <= 0 && record.ChildIds.Count > 0) record.SubjectId = record.ChildIds[0];
                    if (ChildStatus(record, map, tick) == ChildUnknown) return false;
                }
                return RHAH_LongFollowRuntime.CanBring(source, map);
            }
            if (record.Phase == DeathMemory) return child != null && (child.Dead || RHAH_LongFollowRuntime.Adult(child));
            if (record.Phase == InquiryDelay)
                return tick >= record.DueTick && !Present(RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick), map);
            if (record.Phase == Inquiry) return AllVisitorsGone(record, map, tick) || tick >= record.DeadlineTick;
            if (record.Phase == Theft)
            {
                if (AllVisitorsGone(record, map, tick)) return true;
                for (int i = 0; i < record.VisitorIds.Count; i++)
                {
                    if (record.SelectedIds.Contains(record.VisitorIds[i])) continue;
                    Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick);
                    if (!Present(visitor, map) || !RHAH_LongFollowRuntime.MedicallyPaused(visitor)) return true;
                }
                return false;
            }
            Verse.Pawn mother = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
            if (mother == null) return false;
            if (mother.Dead) return true;
            if (record.Phase == BegDelay)
                return tick >= record.DueTick && child != null && (child.Dead || record.RevisitCount >= 2 ||
                    RHAH_LongFollowRuntime.CanBring(mother, map) && RHAH_LongFollowRuntime.CanBring(child, map));
            if (record.Phase == Leaving) return !Present(mother, map);
            if (record.Phase == PairLeaving) return !Present(mother, map) && child != null && !Present(child, map);
            if (record.MedicalHold)
                return !RHAH_LongFollowRuntime.MedicallyPaused(mother) && (record.DisplayId != "I-037" || !RHAH_LongFollowRuntime.Sick(mother));
            if (RHAH_LongFollowRuntime.MedicallyPaused(mother)) return !record.AskedEarly && !record.LetterOpen;
            if (!RHAH_LongFollowRuntime.AtOriginalEdge(mother, map)) return false;
            if (record.DisplayId == "I-037")
            {
                int status = ChildStatus(record, map, tick);
                if (status == ChildUnknown) return false;
                if (status != record.Branch) return true;
            }
            if (record.AskedEarly) return true;
            if (record.Phase == Meeting)
                return !RHAH_LongFollowRuntime.AtHome(child, map) || RHAH_LongFollowRuntime.HasMetAtEdge(mother, child, map) || RHAH_LongFollowRuntime.CanMeetAtEdge(mother, child, map);
            if (record.Phase == LivingChild && (!record.ClaimAcknowledged || record.DeadlineTick < 0))
                return record.ClaimAcknowledged || RHAH_LongFollowRuntime.HasMetAtEdge(mother, child, map) || RHAH_LongFollowRuntime.CanMeetAtEdge(mother, child, map);
            if (record.Phase == PaidStay) return tick >= record.DueTick;
            switch (record.Phase)
            {
                case RefusedWait:
                case Recognition:
                case TakeChild:
                case LivingChild:
                case CalledMother:
                case Burial:
                case DuskWait:
                case TransferredChild:
                case Begging: return tick >= record.DeadlineTick;
                default: return false;
            }
        }

        internal static bool Tick(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (record.Completed || map == null || !Handles(record.DisplayId))
            {
                return false;
            }
            if (record.Phase == Returning)
            {
                return OpenReturn(record, map, tick);
            }
            if (record.Phase == DeathMemory)
            {
                return ApplyDeathMemory(record, tick);
            }
            if (record.Phase == InquiryDelay || record.Phase == Inquiry || record.Phase == Theft)
            {
                return TickInquiry(record, map, tick);
            }
            Verse.Pawn mother = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
            if (mother == null)
            {
                return false;
            }
            if (mother.Dead)
            {
                if (record.DisplayId == "I-002" && mother.MapHeld == map && record.Phase != BegDelay)
                {
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    record.MedicalHold = false;
                    record.Phase = DeathMemory;
                    RHAH_LongFollowRuntime.Say(record, Prefix + "MotherDied");
                    return ApplyDeathMemory(record, tick);
                }
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (record.Phase == BegDelay)
            {
                return ReturnToBeg(record, map, mother, tick);
            }
            if (record.Phase == PairLeaving || record.Phase == Leaving)
            {
                return FinishDeparture(record, map, mother, tick);
            }
            if (record.MedicalHold)
            {
                if (RHAH_LongFollowRuntime.MedicallyPaused(mother) ||
                    (record.DisplayId == "I-037" && RHAH_LongFollowRuntime.Sick(mother)))
                {
                    return false;
                }
                record.MedicalHold = false;
                record.AskedEarly = true;
                RHAH_LongFollowRuntime.EdgeWait(mother, map);
                RHAH_LongFollowRuntime.Say(record, Prefix + "Recovered");
                return true;
            }
            if (RHAH_LongFollowRuntime.MedicallyPaused(mother))
            {
                if (!record.AskedEarly && !record.LetterOpen)
                {
                    RHAH_LongFollowRuntime.OpenLetter(record, map);
                    return true;
                }
                return false;
            }
            if (!RHAH_LongFollowRuntime.AtOriginalEdge(mother, map))
            {
                return false;
            }
            if (record.DisplayId == "I-037")
            {
                int status = ChildStatus(record, map, tick);
                if (status == ChildUnknown) return false;
                if (status != record.Branch)
                {
                    record.AskedEarly = false;
                    SetChildPhase(record, status, map, tick);
                    return true;
                }
            }
            if (record.AskedEarly)
            {
                record.AskedEarly = false;
                RHAH_LongFollowRuntime.OpenLetter(record, map);
                return true;
            }
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            switch (record.Phase)
            {
                case Meeting:
                    if (!RHAH_LongFollowRuntime.Alive(child) || !RHAH_LongFollowRuntime.AtHome(child, map))
                    {
                        BeginDeparture(record, mother, Prefix + "MeetingUnavailable");
                        return true;
                    }
                    if (!RHAH_LongFollowRuntime.HasMetAtEdge(mother, child, map))
                    {
                        RHAH_LongFollowRuntime.MeetAtEdge(mother, child, map);
                        return false;
                    }
                    record.Phase = TakeChild;
                    record.DeadlineTick = AddTicks(tick, GenDate.TicksPerDay);
                    RHAH_LongFollowRuntime.Say(record, Prefix + (record.Branch == 0 ? "MetQuietly" : "MetAngrily"));
                    RHAH_LongFollowRuntime.OpenLetter(record, map);
                    return true;
                case RefusedWait:
                    if (tick < record.DeadlineTick) return false;
                    BeginDeparture(record, mother, Prefix + "WaitedAndLeft");
                    return true;
                case Recognition:
                    if (tick < record.DeadlineTick) return false;
                    RefuseMeeting(record, map, tick);
                    return true;
                case TakeChild:
                    if (tick < record.DeadlineTick) return false;
                    RefuseTaking(record, mother);
                    return true;
                case LivingChild:
                    if (!record.ClaimAcknowledged || record.DeadlineTick < 0)
                    {
                        if (!record.ClaimAcknowledged && !RHAH_LongFollowRuntime.HasMetAtEdge(mother, child, map))
                        {
                            RHAH_LongFollowRuntime.MeetAtEdge(mother, child, map);
                            return false;
                        }
                        AcknowledgeLivingMeeting(record, map, tick);
                        RHAH_LongFollowRuntime.OpenLetter(record, map);
                        return true;
                    }
                    if (tick < record.DeadlineTick) return false;
                    FinishLivingVisit(record, mother, tick);
                    return true;
                case CalledMother:
                    if (tick < record.DeadlineTick) return false;
                    FinishLivingVisit(record, mother, tick);
                    return true;
                case PaidStay:
                    if (tick < record.DueTick) return false;
                    FinishLivingVisit(record, mother, tick);
                    return true;
                case Burial:
                    if (tick < record.DeadlineTick) return false;
                    BeginDeparture(record, mother, Prefix + "DuskDeparture");
                    return true;
                case DuskWait:
                    if (tick < record.DeadlineTick) return false;
                    BeginDeparture(record, mother, Prefix + "DuskDeparture");
                    return true;
                case TransferredChild:
                    if (tick < record.DeadlineTick) return false;
                    StartInquiryDelay(record, mother, tick);
                    return true;
                case Begging:
                    if (tick < record.DeadlineTick) return false;
                    LeavePair(record, map, tick, Prefix + "BegRefused");
                    return true;
                default:
                    return false;
            }
        }

        internal static IEnumerable<RHAH_LongOption> Options(RHAH_LongFollowRecord record, Map map)
        {
            if (record.Completed || record.MedicalHold || map == null)
            {
                yield break;
            }
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            Verse.Pawn mother = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
            if (record.Phase != Inquiry && !RHAH_LongFollowRuntime.Alive(mother))
            {
                yield break;
            }
            if (CanTreat(record, mother) && RHAH_LongFollowRuntime.MedicallyPaused(mother))
            {
                yield return new RHAH_LongOption("treat", Prefix + "Treat");
                yield return new RHAH_LongOption("wait_untreated", Prefix + "WaitUntreated");
                yield break;
            }
            if (record.Phase != Inquiry && !RHAH_LongFollowRuntime.AtOriginalEdge(mother, map))
            {
                yield break;
            }
            if (CanTreat(record, mother))
            {
                yield return new RHAH_LongOption("treat", Prefix + "Treat");
            }
            switch (record.Phase)
            {
                case Recognition:
                    yield return new RHAH_LongOption("meet", Prefix + "Meet", record.SubjectId, MeetingDisabled(record, map, tick));
                    yield return new RHAH_LongOption("refuse_meeting", Prefix + "RefuseMeeting");
                    yield return Payment(record, map, "pay_leave", "PayLeave", false);
                    break;
                case TakeChild:
                    yield return new RHAH_LongOption("take", Prefix + "AllowTake", record.SubjectId, TakingDisabled(record, map, tick));
                    yield return new RHAH_LongOption("keep", Prefix + "KeepChild");
                    break;
                case LivingChild:
                    yield return new RHAH_LongOption("call", Prefix + "Call", 0, record.ClaimAcknowledged ? null : Prefix + "MeetingPending");
                    yield return new RHAH_LongOption("let_leave", Prefix + "LetLeave", 0, record.ClaimAcknowledged ? null : Prefix + "MeetingPending");
                    break;
                case CalledMother:
                    yield return Payment(record, map, "pay_stay", "PayStay", true);
                    yield return new RHAH_LongOption("let_leave", Prefix + "LetLeave");
                    break;
                case Burial:
                    yield return new RHAH_LongOption("burial_answer", Prefix + "AnswerBurial");
                    yield return new RHAH_LongOption("burial_silence", Prefix + "DoNotAnswer");
                    break;
                case TransferredChild:
                    yield return new RHAH_LongOption("acknowledge_transfer", Prefix + "AcknowledgeTransfer");
                    break;
                case Begging:
                    yield return Payment(record, map, "beg_pay", "GiveFood", false);
                    yield return new RHAH_LongOption("beg_refuse", Prefix + "RefuseFood");
                    break;
                case Inquiry:
                    yield return new RHAH_LongOption("tell_destination", Prefix + "TellDestination");
                    yield return new RHAH_LongOption("hide_destination", Prefix + "HideDestination");
                    break;
            }
        }

        internal static bool Choose(RHAH_LongFollowRecord record, Map map, string action, int pawnId, int tick)
        {
            if (record.Completed || record.MedicalHold || map == null)
            {
                return false;
            }
            if (record.DeadlineTick >= 0 && tick >= record.DeadlineTick && action != "treat" && action != "wait_untreated")
            {
                return false;
            }
            if (record.Phase == Inquiry)
            {
                if (action == "tell_destination")
                {
                    LeaveInquirers(record, map, tick, Prefix + "DestinationTold");
                    return true;
                }
                if (action == "hide_destination")
                {
                    BeginTheft(record, map, tick);
                    return true;
                }
                return false;
            }
            Verse.Pawn mother = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
            if (!Present(mother, map))
            {
                return false;
            }
            if (record.DisplayId == "I-037" && record.Phase >= LivingChild && record.Phase <= TransferredChild &&
                ChildStatus(record, map, tick) != record.Branch)
            {
                return false;
            }
            if (action == "treat" && CanTreat(record, mother))
            {
                record.MedicalHold = true;
                record.AskedEarly = false;
                RHAH_LongFollowRuntime.CloseLetter(record);
                RHAH_LongFollowRuntime.BeginMedicalCare(mother, map);
                RHAH_LongFollowRuntime.Say(record, Prefix + (record.DisplayId == "I-037" ? "TreatmentUntilHealed" : "TreatmentUntilWalking"));
                return true;
            }
            if (RHAH_LongFollowRuntime.MedicallyPaused(mother))
            {
                if (action != "wait_untreated") return false;
                record.AskedEarly = true;
                RHAH_LongFollowRuntime.CloseLetter(record);
                return true;
            }
            if (!RHAH_LongFollowRuntime.AtOriginalEdge(mother, map)) return false;
            switch (record.Phase)
            {
                case Recognition:
                    if (action == "meet" && pawnId == record.SubjectId && MeetingDisabled(record, map, tick) == null)
                    {
                        Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
                        if (!RHAH_LongFollowRuntime.MeetAtEdge(mother, child, map)) return false;
                        record.Branch = Rand.Bool ? 1 : 0;
                        record.Phase = Meeting;
                        record.DeadlineTick = -1;
                        RHAH_LongFollowRuntime.CloseLetter(record);
                        return true;
                    }
                    if (action == "refuse_meeting")
                    {
                        RefuseMeeting(record, map, tick);
                        return true;
                    }
                    if (action == "pay_leave" && RHAH_LongFollowRuntime.TryPay(map, record.FoodCount, 0, mother))
                    {
                        BeginDeparture(record, mother, Prefix + "PaidAndLeft");
                        return true;
                    }
                    return false;
                case TakeChild:
                    if (action == "keep")
                    {
                        RefuseTaking(record, mother);
                        return true;
                    }
                    if (action != "take" || pawnId != record.SubjectId || TakingDisabled(record, map, tick) != null) return false;
                    record.SelectedIds.Clear();
                    record.SelectedIds.Add(record.SubjectId);
                    if (!RHAH_LongFollowRuntime.SendChildren(record, map, record.SelectedIds, mother)) return false;
                    if (!record.DepartedIds.Contains(record.SubjectId)) record.DepartedIds.Add(record.SubjectId);
                    record.FollowupSelected = Rand.Bool;
                    record.Phase = PairLeaving;
                    record.DeadlineTick = -1;
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    RHAH_LongFollowRuntime.Say(record, Prefix + "ChildLeaving");
                    return true;
                case LivingChild:
                    if (!record.ClaimAcknowledged || record.DeadlineTick < 0)
                    {
                        Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
                        if (!record.ClaimAcknowledged && !RHAH_LongFollowRuntime.HasMetAtEdge(mother, child, map)) return false;
                        AcknowledgeLivingMeeting(record, map, tick);
                    }
                    if (action == "call")
                    {
                        record.Phase = CalledMother;
                        RHAH_LongFollowRuntime.OpenLetter(record, map);
                        return true;
                    }
                    if (action != "let_leave") return false;
                    FinishLivingVisit(record, mother, tick);
                    return true;
                case CalledMother:
                    if (action == "let_leave")
                    {
                        FinishLivingVisit(record, mother, tick);
                        return true;
                    }
                    if (action != "pay_stay" || !RHAH_LongFollowRuntime.TryPay(map, record.FoodCount, record.MedicineCount, mother)) return false;
                    record.Phase = PaidStay;
                    record.DueTick = AddTicks(tick, GenDate.TicksPerDay);
                    record.DeadlineTick = -1;
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    RHAH_LongFollowRuntime.Say(record, Prefix + "PaidStay");
                    return true;
                case Burial:
                    if (action == "burial_answer")
                    {
                        Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
                        bool buried = child?.Corpse?.ParentHolder is Building_Grave;
                        BeginDeparture(record, mother, Prefix + (buried ? "BurialAnswered" : "NotBuried"));
                        return true;
                    }
                    if (action != "burial_silence") return false;
                    record.Phase = DuskWait;
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    RHAH_LongFollowRuntime.Say(record, Prefix + "DuskWait");
                    return true;
                case TransferredChild:
                    if (action != "acknowledge_transfer") return false;
                    StartInquiryDelay(record, mother, tick);
                    return true;
                case Begging:
                    if (action == "beg_pay")
                    {
                        if (!RHAH_LongFollowRuntime.TryPay(map, record.FoodCount, 0, mother)) return false;
                        LeavePair(record, map, tick, Prefix + "BegPaid");
                        return true;
                    }
                    if (action != "beg_refuse") return false;
                    LeavePair(record, map, tick, Prefix + "BegRefused");
                    return true;
                default:
                    return false;
            }
        }

        static bool OpenReturn(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (record.DueTick >= 0 && tick < record.DueTick) return false;
            if (record.SourceAdultId <= 0)
            {
                RHAH_LongFollowRuntime.Say(record, Prefix + "MotherUnavailable");
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            Verse.Pawn mother = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
            if (mother == null)
            {
                RHAH_LongFollowRuntime.Say(record, Prefix + "MotherUnavailable");
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (mother.Dead)
            {
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (record.DisplayId == "I-002")
            {
                if (MeetingDisabled(record, map, tick) != null)
                {
                    for (int i = 0; i < record.ChildIds.Count; i++)
                    {
                        Verse.Pawn candidate = RHAH_LongFollowRuntime.Pawn(record.ChildIds[i], tick);
                        if (RHAH_LongFollowRuntime.Alive(candidate) && RHAH_LongFollowRuntime.AtHome(candidate, map) && RHAH_LongFollowRuntime.Adult(candidate))
                        {
                            record.SubjectId = candidate.thingIDNumber;
                            break;
                        }
                    }
                }
                if (MeetingDisabled(record, map, tick) != null) return false;
            }
            else
            {
                if (record.SubjectId <= 0 && record.ChildIds.Count > 0) record.SubjectId = record.ChildIds[0];
                record.Branch = ChildStatus(record, map, tick);
                if (record.Branch == ChildUnknown) return false;
            }
            if (!RHAH_LongFollowRuntime.CanBring(mother, map) || !RHAH_LongFollowRuntime.Bring(mother, map)) return false;
            if (!record.VisitorIds.Contains(record.SourceAdultId)) record.VisitorIds.Add(record.SourceAdultId);
            record.Opened = true;
            record.DueTick = -1;
            record.RevisitCount = 1;
            RHAH_LongFollowRuntime.Hold(mother);
            RHAH_LongFollowRuntime.EdgeWait(mother, map);
            if (record.DisplayId == "I-002")
            {
                record.Phase = Recognition;
                record.DeadlineTick = AddTicks(tick, GenDate.TicksPerDay);
                RHAH_LongFollowRuntime.OpenLetter(record, map);
            }
            else
            {
                SetChildPhase(record, record.Branch, map, tick);
            }
            return true;
        }

        static string MeetingDisabled(RHAH_LongFollowRecord record, Map map, int tick)
        {
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            return RHAH_LongFollowRuntime.Alive(child) && RHAH_LongFollowRuntime.AtHome(child, map) && RHAH_LongFollowRuntime.Adult(child)
                ? null : Prefix + "ChildUnavailable";
        }

        static string TakingDisabled(RHAH_LongFollowRecord record, Map map, int tick)
        {
            string disabled = MeetingDisabled(record, map, tick);
            if (disabled != null) return disabled;
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (child.IsPrisoner || child.IsSlave || RHAH_LongFollowRuntime.MedicallyPaused(child)) return Prefix + "CannotDepart";
            return null;
        }

        static bool CanTreat(RHAH_LongFollowRecord record, Verse.Pawn mother)
        {
            return RHAH_LongFollowRuntime.Alive(mother) &&
                ((record.Phase >= Recognition && record.Phase <= TransferredChild) || record.Phase == Begging) &&
                (RHAH_LongFollowRuntime.Sick(mother) || RHAH_LongFollowRuntime.MedicallyPaused(mother));
        }

        static RHAH_LongOption Payment(RHAH_LongFollowRecord record, Map map, string action, string key, bool medicine)
        {
            return new RHAH_LongOption(action, Prefix + key, 0,
                RHAH_LongFollowRuntime.CanPay(map, record.FoodCount, medicine ? record.MedicineCount : 0) ? null : Prefix + "InsufficientSupplies");
        }

        static void RefuseMeeting(RHAH_LongFollowRecord record, Map map, int tick)
        {
            record.Phase = RefusedWait;
            record.DeadlineTick = AddTicks(tick, GenDate.TicksPerDay);
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (RHAH_LongFollowRuntime.Alive(child) && RHAH_LongFollowRuntime.Adult(child))
            {
                RHAH_LongFollowRuntime.Mood(child, "RHAH_Thought_MotherOutside", -2);
            }
            RHAH_LongFollowRuntime.CloseLetter(record);
            RHAH_LongFollowRuntime.Say(record, Prefix + "RefusedWait");
        }

        static void RefuseTaking(RHAH_LongFollowRecord record, Verse.Pawn mother)
        {
            RHAH_LongFollowRuntime.Mood(mother, "RHAH_Thought_MotherCameWithoutTaking", -3);
            BeginDeparture(record, mother, Prefix + "ChildStayed");
        }

        static void BeginDeparture(RHAH_LongFollowRecord record, Verse.Pawn mother, string resultKey)
        {
            record.Phase = Leaving;
            record.DueTick = -1;
            record.DeadlineTick = -1;
            RHAH_LongFollowRuntime.CloseLetter(record);
            RHAH_LongFollowRuntime.Unhold(mother);
            RHAH_LongFollowRuntime.Leave(mother);
            RHAH_LongFollowRuntime.Say(record, resultKey);
        }

        static bool FinishDeparture(RHAH_LongFollowRecord record, Map map, Verse.Pawn mother, int tick)
        {
            if (Present(mother, map)) return false;
            if (record.Phase == PairLeaving)
            {
                Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
                if (child == null || Present(child, map)) return false;
                if (record.FollowupSelected && !child.Dead && record.RevisitCount < 2)
                {
                    record.Phase = BegDelay;
                    record.DueTick = AddTicks(tick, ReturnDelay());
                    return true;
                }
            }
            RHAH_LongFollowRuntime.Complete(record);
            return true;
        }

        static bool ReturnToBeg(RHAH_LongFollowRecord record, Map map, Verse.Pawn mother, int tick)
        {
            if (tick < record.DueTick) return false;
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (child == null) return false;
            if (child.Dead || record.RevisitCount >= 2)
            {
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (!RHAH_LongFollowRuntime.CanBring(mother, map) || !RHAH_LongFollowRuntime.CanBring(child, map)) return false;
            if (!RHAH_LongFollowRuntime.Bring(mother, map)) return false;
            record.ReturnRequested = true;
            RHAH_LongFollowRuntime.Hold(mother);
            RHAH_LongFollowRuntime.EdgeWait(mother, map);
            if (!RHAH_LongFollowRuntime.Bring(child, map)) return false;
            if (!record.VisitorIds.Contains(child.thingIDNumber)) record.VisitorIds.Add(child.thingIDNumber);
            RHAH_LongFollowRuntime.Hold(child);
            RHAH_LongFollowRuntime.EdgeWait(child, map);
            record.RevisitCount = 2;
            record.Phase = Begging;
            record.DueTick = -1;
            record.DeadlineTick = AddTicks(tick, GenDate.TicksPerDay);
            RHAH_LongFollowRuntime.OpenLetter(record, map);
            return true;
        }

        static void LeavePair(RHAH_LongFollowRecord record, Map map, int tick, string resultKey)
        {
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (RHAH_LongFollowRuntime.Alive(child))
            {
                RHAH_LongFollowRuntime.Unhold(child);
                RHAH_LongFollowRuntime.Leave(child);
            }
            BeginDeparture(record, RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick), resultKey);
            record.Phase = PairLeaving;
            record.FollowupSelected = false;
        }

        static bool ApplyDeathMemory(RHAH_LongFollowRecord record, int tick)
        {
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (child == null) return false;
            if (child.Dead)
            {
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (!RHAH_LongFollowRuntime.Adult(child)) return false;
            RHAH_LongFollowRuntime.Mood(child, "RHAH_Thought_MotherDiedAtDoor", -6, true);
            RHAH_LongFollowRuntime.Complete(record);
            return true;
        }

        static int ChildStatus(RHAH_LongFollowRecord record, Map map, int tick)
        {
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (child != null && child.Dead) return ChildDead;
            if (record.DepartedIds.Contains(record.SubjectId)) return ChildTransferred;
            if (RHAH_LongFollowRuntime.Alive(child) && child.Faction != null && child.Faction != Faction.OfPlayer &&
                RHAH_Api.Get(child)?.IsReleased == true) return ChildTransferred;
            if (RHAH_LongFollowRuntime.Alive(child) && RHAH_LongFollowRuntime.AtHome(child, map)) return ChildLiving;
            return ChildUnknown;
        }

        static void SetChildPhase(RHAH_LongFollowRecord record, int status, Map map, int tick)
        {
            RHAH_LongFollowRuntime.CloseLetter(record);
            record.Branch = status;
            record.DueTick = -1;
            record.DeadlineTick = status == ChildLiving ? -1 : DuskTick(map, tick);
            record.Phase = status == ChildLiving ? LivingChild : status == ChildDead ? Burial : TransferredChild;
            record.ClaimAcknowledged = false;
            if (status == ChildLiving)
            {
                Verse.Pawn mother = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
                Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
                if (RHAH_LongFollowRuntime.HasMetAtEdge(mother, child, map))
                {
                    AcknowledgeLivingMeeting(record, map, tick);
                }
                else
                {
                    RHAH_LongFollowRuntime.MeetAtEdge(mother, child, map);
                }
            }
            RHAH_LongFollowRuntime.OpenLetter(record, map);
        }

        internal static bool AcknowledgeLivingMeeting(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (record.Completed || record.DisplayId != "I-037" || record.Phase != LivingChild ||
                record.ClaimAcknowledged && record.DeadlineTick >= 0)
            {
                return false;
            }
            record.ClaimAcknowledged = true;
            if (record.DeadlineTick < 0) record.DeadlineTick = DepartureToday(map, tick);
            return true;
        }

        static void FinishLivingVisit(RHAH_LongFollowRecord record, Verse.Pawn mother, int tick)
        {
            RHAH_LongFollowRuntime.Mood(mother, "RHAH_Thought_ChildStillAlive", 2);
            BeginDeparture(record, mother, Prefix + "LivingVisitFinished");
        }

        static void StartInquiryDelay(RHAH_LongFollowRecord record, Verse.Pawn mother, int tick)
        {
            record.Phase = InquiryDelay;
            record.DeadlineTick = -1;
            record.DueTick = AddTicks(tick, ReturnDelay());
            RHAH_LongFollowRuntime.CloseLetter(record);
            RHAH_LongFollowRuntime.Unhold(mother);
            RHAH_LongFollowRuntime.Leave(mother);
            RHAH_LongFollowRuntime.Say(record, Prefix + "TransferHeard");
        }

        static bool TickInquiry(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (record.Phase == InquiryDelay)
            {
                Verse.Pawn mother = RHAH_LongFollowRuntime.Pawn(record.SourceAdultId, tick);
                if (Present(mother, map) || tick < record.DueTick) return false;
                record.VisitorIds.Remove(record.SourceAdultId);
                if (record.VisitorIds.Count < 2) RHAH_LongFollowRuntime.SpawnVisitors(record, map, 2 - record.VisitorIds.Count);
                if (record.VisitorIds.Count != 2) return false;
                record.RevisitCount = 2;
                record.Phase = Inquiry;
                record.DueTick = -1;
                record.DeadlineTick = AddTicks(tick, GenDate.TicksPerDay);
                for (int i = 0; i < record.VisitorIds.Count; i++)
                {
                    Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick);
                    RHAH_LongFollowRuntime.Hold(visitor);
                    RHAH_LongFollowRuntime.EdgeWait(visitor, map);
                }
                RHAH_LongFollowRuntime.OpenLetter(record, map);
                return true;
            }
            if (record.Phase == Inquiry)
            {
                if (AllVisitorsGone(record, map, tick))
                {
                    RHAH_LongFollowRuntime.Complete(record);
                    return true;
                }
                if (tick < record.DeadlineTick) return false;
                BeginTheft(record, map, tick);
                return true;
            }
            bool changed = false;
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                int id = record.VisitorIds[i];
                if (record.SelectedIds.Contains(id)) continue;
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(id, tick);
                if (!Present(visitor, map))
                {
                    record.SelectedIds.Add(id);
                    changed = true;
                    continue;
                }
                if (RHAH_LongFollowRuntime.MedicallyPaused(visitor)) continue;
                RHAH_LongFollowRuntime.Unhold(visitor);
                if (!RHAH_LongFollowRuntime.StartFoodTheft(visitor, map)) RHAH_LongFollowRuntime.Leave(visitor);
                record.SelectedIds.Add(id);
                changed = true;
            }
            if (AllVisitorsGone(record, map, tick))
            {
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            return changed;
        }

        static void BeginTheft(RHAH_LongFollowRecord record, Map map, int tick)
        {
            record.Phase = Theft;
            record.DeadlineTick = -1;
            record.SelectedIds.Clear();
            RHAH_LongFollowRuntime.CloseLetter(record);
            RHAH_LongFollowRuntime.Say(record, Prefix + "Theft");
            TickInquiry(record, map, tick);
        }

        static void LeaveInquirers(RHAH_LongFollowRecord record, Map map, int tick, string resultKey)
        {
            record.Phase = Theft;
            record.DeadlineTick = -1;
            record.SelectedIds.Clear();
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick);
                if (RHAH_LongFollowRuntime.Alive(visitor))
                {
                    RHAH_LongFollowRuntime.Unhold(visitor);
                    RHAH_LongFollowRuntime.Leave(visitor);
                }
                record.SelectedIds.Add(record.VisitorIds[i]);
            }
            RHAH_LongFollowRuntime.CloseLetter(record);
            RHAH_LongFollowRuntime.Say(record, resultKey);
        }

        static bool AllVisitorsGone(RHAH_LongFollowRecord record, Map map, int tick)
        {
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick);
                if (Present(visitor, map)) return false;
            }
            return true;
        }

        static bool Present(Verse.Pawn pawn, Map map)
        {
            return RHAH_LongFollowRuntime.Alive(pawn) && pawn.MapHeld == map;
        }

        static int ReturnDelay()
        {
            int days = Core.RHAH_Mod.Settings == null ? RHAH_EventFollowRules.MotherReturnDays : Core.RHAH_Mod.Settings.followMotherReturnDays;
            return Math.Max(1, days) * GenDate.TicksPerDay;
        }

        static int DuskTick(Map map, int tick)
        {
            int dayTick = GenLocalDate.DayTick(map);
            int dusk = 18 * GenDate.TicksPerHour;
            return AddTicks(tick, dayTick >= dusk ? 0 : dusk - dayTick);
        }

        static int DepartureToday(Map map, int tick)
        {
            int dusk = DuskTick(map, tick);
            return Math.Max(dusk, AddTicks(tick, GenDate.TicksPerHour));
        }

        static int AddTicks(int tick, int delay)
        {
            return tick > int.MaxValue - delay ? int.MaxValue : tick + delay;
        }
    }
}
