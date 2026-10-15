using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.EventMgr
{
    internal static class RHAH_LongClaimFollow
    {
        const int KeepsakeOpening = 1;
        const int KeepsakeWaiting = 2;
        const int KeepsakeCustody = 3;
        const int KeepsakeOrigin = 4;
        const int KeepsakeArrival = 5;
        const int KeepsakeClaimant = 6;
        const int SellingStigma = 7;
        const int KeepsakeClosedCustody = 8;
        const int ClaimOpening = 10;
        const int ClaimProbe = 11;
        const int ClaimArrival = 12;
        const int ClaimLegacy = 13;
        const int ClaimRevengeWaiting = 14;
        const int ClaimRevenge = 15;
        const int ClaimTitle = 20;
        const string Prefix = "RHAH_LongClaim_";

        internal static bool Handles(string id)
        {
            return id == "I-012" || id == "I-038" || id == "I-041";
        }

        internal static string TextKey(RHAH_LongFollowRecord record)
        {
            switch (record.Phase)
            {
                case KeepsakeOpening: return Prefix + "KeepsakeOpening";
                case KeepsakeCustody: return Prefix + "KeepsakeCustody";
                case KeepsakeClosedCustody: return Prefix + "KeepsakeWithheld";
                case KeepsakeOrigin: return Prefix + "KeepsakeOrigin";
                case KeepsakeClaimant: return Prefix + "KeepsakeClaimant";
                case ClaimOpening: return Prefix + "ClaimOpening";
                case ClaimProbe: return Prefix + (record.Branch == 1 ? "ClaimDemand" : "ClaimRetract");
                case ClaimLegacy: return Prefix + "ClaimLegacy";
                default: return null;
            }
        }

        internal static bool Ready(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (PendingReady(record, map, tick)) return true;
            Verse.Pawn child = Subject(record, map, tick);
            if (!RHAH_LongFollowRuntime.Alive(child)) return record.SubjectId != 0 && record.Opened;
            if (record.Phase == 0)
            {
                if (record.DisplayId == "I-041" && RHAH_Plague.HasActive(child)) { record.MedicalHold = true; return false; }
                if (!AtPlayerHome(record, child, map) || !CanExplain(child) || RHAH_LongFollowRuntime.MedicallyPaused(child)) return false;
                if (record.DisplayId != "I-041") return child.inventory != null && RHAH_Api.TryGetExtra(child, RHAH_EventFollowMood.BoughtKey, out string _);
                HediffDef plague = RHAH_Plague.Def;
                return record.MedicalHold || plague != null && child.health?.immunity != null && child.health.immunity.GetImmunity(plague) >= 1f;
            }
            if (!AtPlayerHome(record, child, map) || RHAH_LongFollowRuntime.MedicallyPaused(child)) return false;
            switch (record.Phase)
            {
                case SellingStigma:
                case ClaimTitle: return SocialReady(record, map, child, tick);
                case KeepsakeClosedCustody: return InInventory(record.Keepsake, child) || !record.LetterOpen;
                case KeepsakeCustody: return InInventory(record.Keepsake, child) || RHAH_LongFollowRuntime.Adult(child);
                case KeepsakeOpening:
                case KeepsakeWaiting: return RHAH_LongFollowRuntime.Adult(child);
                case KeepsakeArrival:
                case ClaimArrival:
                case ClaimRevengeWaiting: return tick >= record.DueTick;
                default: return false;
            }
        }

        static bool PendingReady(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (record.SubjectId != 0 && record.DepartedIds.Contains(record.SubjectId)) return true;
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (!RHAH_LongFollowRuntime.Alive(child)) return false;
            return PlayerKilledClaimant(record, tick) || record.Opened && record.DisplayId != "I-041" && (record.Keepsake == null || record.Keepsake.Destroyed) ||
                (record.Phase == ClaimLegacy || record.Phase == ClaimRevenge || record.Phase == KeepsakeClaimant) && Scattered(record, map, tick) ||
                record.Phase == ClaimRevengeWaiting && OriginalPartyDefeated(record, tick);
        }

        internal static bool ResolvePending(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (record.Completed || map == null)
            {
                return false;
            }
            if (record.SubjectId != 0 && record.DepartedIds.Contains(record.SubjectId))
            {
                record.RevengePending = false;
                Depart(record, tick);
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (!RHAH_LongFollowRuntime.Alive(child))
            {
                return false;
            }
            if (PlayerKilledClaimant(record, tick))
            {
                bool scared = Rand.Chance(0.5f);
                record.PlayerKilledVisitor = true;
                record.RevengePending = false;
                record.Branch = scared ? 3 : 4;
                string thought = scared ? "RHAH_LongClaimScared" : record.DisplayId == "I-041" ? "RHAH_LongClaimViolent" : "RHAH_LongClaimRelieved";
                RHAH_LongFollowRuntime.Mood(child, thought, scared ? -6 : 4);
                RHAH_LongFollowRuntime.Say(record, Prefix + (scared ? "KilledScared" : record.DisplayId == "I-041" ? "KilledViolent" : "KilledRelieved"), child.LabelShort);
                Depart(record, tick);
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (record.Opened && record.DisplayId != "I-041" && (record.Keepsake == null || record.Keepsake.Destroyed))
            {
                Depart(record, tick);
                LoseKeepsake(record, child);
                return true;
            }
            if ((record.Phase == ClaimLegacy || record.Phase == ClaimRevenge) && Scattered(record, map, tick) || record.Phase == ClaimRevengeWaiting && OriginalPartyDefeated(record, tick))
            {
                record.RevengePending = false;
                RHAH_LongFollowRuntime.Mood(child, "RHAH_LongClaimUnchallenged", 8);
                RHAH_LongFollowRuntime.Say(record, Prefix + "GroupScattered", child.LabelShort);
                Depart(record, tick);
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (record.Phase == KeepsakeClaimant && Scattered(record, map, tick))
            {
                Depart(record, tick);
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            return false;
        }

        internal static bool Tick(RHAH_LongFollowRecord record, Map map, int tick)
        {
            if (record.Completed || map == null)
            {
                return false;
            }
            if (ResolvePending(record, map, tick))
            {
                return true;
            }

            Verse.Pawn child = Subject(record, map, tick);
            if (!RHAH_LongFollowRuntime.Alive(child))
            {
                if (record.SubjectId != 0 && record.Opened)
                {
                    Depart(record, tick);
                    RHAH_LongFollowRuntime.Complete(record);
                    return true;
                }
                return false;
            }

            if (record.Phase == 0)
            {
                return Open(record, map, child);
            }

            if (!AtPlayerHome(record, child, map) || RHAH_LongFollowRuntime.MedicallyPaused(child))
            {
                return false;
            }

            if (record.Phase == SellingStigma || record.Phase == ClaimTitle)
            {
                return SocialTick(record, map, child, tick);
            }

            if (record.Phase == KeepsakeClosedCustody)
            {
                if (!InInventory(record.Keepsake, child))
                {
                    if (!record.LetterOpen)
                    {
                        Present(record, map, KeepsakeClosedCustody);
                        return true;
                    }
                    return false;
                }
                record.KeepsakeTaken = false;
                RHAH_LongFollowRuntime.RemoveMood(child, "RHAH_LongKeepsakeTaken");
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }

            if (record.Phase == KeepsakeCustody)
            {
                if (InInventory(record.Keepsake, child))
                {
                    record.KeepsakeTaken = false;
                    RHAH_LongFollowRuntime.RemoveMood(child, "RHAH_LongKeepsakeTaken");
                    if (RHAH_LongFollowRuntime.Adult(child))
                    {
                        Present(record, map, KeepsakeOrigin);
                    }
                    else
                    {
                        record.Phase = KeepsakeWaiting;
                        RHAH_LongFollowRuntime.CloseLetter(record);
                    }
                    return true;
                }
                if (RHAH_LongFollowRuntime.Adult(child))
                {
                    RHAH_LongFollowRuntime.Say(record, Prefix + "KeepsakeWithheld", child.LabelShort);
                    Present(record, map, KeepsakeClosedCustody);
                    return true;
                }
            }
            else if ((record.Phase == KeepsakeOpening || record.Phase == KeepsakeWaiting) && RHAH_LongFollowRuntime.Adult(child))
            {
                Present(record, map, KeepsakeOrigin);
                return true;
            }
            else if (record.Phase == KeepsakeArrival && tick >= record.DueTick)
            {
                if (record.Branch == 0)
                {
                    RHAH_LongFollowRuntime.Say(record, Prefix + "NobodyCame", child.LabelShort);
                    RHAH_LongFollowRuntime.Complete(record);
                    return true;
                }
                if (!RHAH_LongFollowRuntime.SpawnVisitors(record, map, 1))
                {
                    return false;
                }
                HoldVisitors(record, tick);
                Present(record, map, KeepsakeClaimant);
                return true;
            }
            else if (record.Phase == ClaimArrival && tick >= record.DueTick)
            {
                if (!RHAH_LongFollowRuntime.SpawnVisitors(record, map, 3))
                {
                    return false;
                }
                HoldVisitors(record, tick);
                Present(record, map, ClaimLegacy);
                return true;
            }
            else if (record.Phase == ClaimRevengeWaiting && tick >= record.DueTick)
            {
                // 原来的人保留在离场名单里，新来的队伍单独追踪
                for (int i = 0; i < record.VisitorIds.Count; i++)
                {
                    if (!record.DepartedIds.Contains(record.VisitorIds[i]))
                    {
                        record.DepartedIds.Add(record.VisitorIds[i]);
                    }
                }
                record.VisitorIds.Clear();
                if (!RHAH_LongFollowRuntime.StartRevenge(record, map))
                {
                    return false;
                }
                record.RevengePending = false;
                record.Phase = ClaimRevenge;
                RHAH_LongFollowRuntime.Say(record, Prefix + "RevengeArrived", child.LabelShort);
                return true;
            }

            return false;
        }

        internal static IEnumerable<RHAH_LongOption> Options(RHAH_LongFollowRecord record, Map map)
        {
            switch (record.Phase)
            {
                case KeepsakeOpening:
                    yield return new RHAH_LongOption("ask", Prefix + "AskNow");
                    yield return new RHAH_LongOption("wait", Prefix + "WaitAdult");
                    yield return new RHAH_LongOption("take", Prefix + "TakeKeepsake");
                    yield return new RHAH_LongOption("discard", Prefix + "DiscardKeepsake");
                    break;
                case KeepsakeCustody:
                    yield return new RHAH_LongOption("return", Prefix + "ReturnKeepsake", 0, AvailableKeepsake(record, map) ? null : Prefix + "ItemUnavailable");
                    yield return new RHAH_LongOption("discard", Prefix + "DiscardKeepsake", 0, AvailableKeepsake(record, map) ? null : Prefix + "ItemUnavailable");
                    break;
                case KeepsakeClosedCustody:
                    yield return new RHAH_LongOption("return", Prefix + "ReturnKeepsake", 0, AvailableKeepsake(record, map) ? null : Prefix + "ItemUnavailable");
                    break;
                case KeepsakeOrigin:
                    yield return new RHAH_LongOption("listen", Prefix + "HearOrigin");
                    break;
                case KeepsakeClaimant:
                case ClaimLegacy:
                    yield return new RHAH_LongOption("handover", Prefix + "LetLeave");
                    yield return new RHAH_LongOption("keep", Prefix + "KeepChild");
                    break;
                case ClaimOpening:
                    yield return new RHAH_LongOption("accept", Prefix + "AcceptClaim");
                    yield return new RHAH_LongOption("deny", Prefix + "DenyClaim");
                    yield return new RHAH_LongOption("probe", Prefix + "ProbeClaim");
                    break;
                case ClaimProbe:
                    if (record.Branch == 1)
                    {
                        yield return new RHAH_LongOption("approve", Prefix + "ApproveReturn");
                        yield return new RHAH_LongOption("deny", Prefix + "RefuseReturn");
                    }
                    else
                    {
                        yield return new RHAH_LongOption("retract", Prefix + "AcceptRetraction");
                    }
                    break;
            }
        }

        internal static bool Choose(RHAH_LongFollowRecord record, Map map, string action, int pawnId, int tick)
        {
            if (ResolvePending(record, map, tick))
            {
                return false;
            }
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (record.Completed || !RHAH_LongFollowRuntime.Alive(child) || !AtPlayerHome(record, child, map) || RHAH_LongFollowRuntime.MedicallyPaused(child) || PlayerKilledClaimant(record, tick))
            {
                return false;
            }

            if ((record.Phase == KeepsakeOpening || record.Phase == KeepsakeWaiting || record.Phase == KeepsakeCustody) && RHAH_LongFollowRuntime.Adult(child))
            {
                Tick(record, map, tick);
                return false;
            }

            if (record.Phase == KeepsakeOpening)
            {
                if (action == "ask" || action == "wait")
                {
                    record.AskedEarly = action == "ask";
                    record.Phase = KeepsakeWaiting;
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    if (record.AskedEarly)
                    {
                        RHAH_LongFollowRuntime.Say(record, Prefix + "EarlyAnswer", child.LabelShort);
                    }
                    return true;
                }
                if (action == "take" && InInventory(record.Keepsake, child))
                {
                    if (!child.inventory.innerContainer.TryDrop(record.Keepsake, child.PositionHeld, map, ThingPlaceMode.Near, out Thing dropped))
                    {
                        return false;
                    }
                    record.Keepsake = dropped;
                    record.KeepsakeTaken = true;
                    RHAH_LongFollowRuntime.Mood(child, "RHAH_LongKeepsakeTaken", -6, true);
                    Present(record, map, KeepsakeCustody);
                    return true;
                }
            }
            if (action == "discard" && (record.Phase == KeepsakeOpening || record.Phase == KeepsakeCustody) && AvailableKeepsake(record, map))
            {
                record.Keepsake.Destroy(DestroyMode.Vanish);
                LoseKeepsake(record, child);
                return true;
            }
            if ((record.Phase == KeepsakeCustody || record.Phase == KeepsakeClosedCustody) && action == "return" && AvailableKeepsake(record, map) && child.inventory != null)
            {
                if (!ReturnKeepsake(record.Keepsake, child))
                {
                    return false;
                }
                record.KeepsakeTaken = false;
                RHAH_LongFollowRuntime.RemoveMood(child, "RHAH_LongKeepsakeTaken");
                RHAH_LongFollowRuntime.CloseLetter(record);
                if (record.Phase == KeepsakeClosedCustody)
                {
                    RHAH_LongFollowRuntime.Complete(record);
                    return true;
                }
                if (RHAH_LongFollowRuntime.Adult(child))
                {
                    Present(record, map, KeepsakeOrigin);
                }
                else
                {
                    record.Phase = KeepsakeWaiting;
                }
                return true;
            }
            if (record.Phase == KeepsakeOrigin && action == "listen")
            {
                record.Branch = Rand.Chance(0.5f) ? 1 : 0;
                record.DueTick = Later(tick, GenDate.TicksPerDay * 3);
                record.Phase = KeepsakeArrival;
                RHAH_LongFollowRuntime.CloseLetter(record);
                return true;
            }
            if (record.Phase == ClaimOpening)
            {
                if (action == "accept")
                {
                    record.ClaimAcknowledged = true;
                    record.Phase = ClaimTitle;
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    if (child.story != null)
                    {
                        child.story.Title = (Prefix + "PrivateTitle").Translate().ToString();
                    }
                    RHAH_LongFollowRuntime.Mood(child, "RHAH_LongClaimRecognized", 6);
                    RHAH_LongFollowRuntime.Say(record, Prefix + "Recognized", child.LabelShort);
                    return true;
                }
                if (action == "probe")
                {
                    record.Branch = Rand.Chance(0.5f) ? 1 : 2;
                    Present(record, map, ClaimProbe);
                    return true;
                }
            }
            if ((record.Phase == ClaimOpening || record.Phase == ClaimProbe && record.Branch == 1) && action == "deny")
            {
                RHAH_LongFollowRuntime.Mood(child, "RHAH_LongClaimDenied", -4);
                RHAH_LongFollowRuntime.Complete(record);
                return true;
            }
            if (record.Phase == ClaimProbe)
            {
                if (record.Branch == 2 && action == "retract")
                {
                    RHAH_LongFollowRuntime.Mood(child, "RHAH_LongClaimRetracted", -2);
                    RHAH_LongFollowRuntime.Complete(record);
                    return true;
                }
                if (record.Branch == 1 && action == "approve")
                {
                    record.ReturnRequested = true;
                    record.DueTick = Later(tick, GenDate.TicksPerDay * 3);
                    record.Phase = ClaimArrival;
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    return true;
                }
            }
            if (record.Phase == KeepsakeClaimant || record.Phase == ClaimLegacy)
            {
                if (Scattered(record, map, tick))
                {
                    return false;
                }
                if (action == "handover")
                {
                    Verse.Pawn receiver = Visitor(record, map, tick);
                    if (receiver == null)
                    {
                        return false;
                    }
                    record.SelectedIds.Clear();
                    record.SelectedIds.Add(child.thingIDNumber);
                    if (!RHAH_LongFollowRuntime.SendChildren(record, map, record.SelectedIds, receiver))
                    {
                        record.SelectedIds.Clear();
                        return false;
                    }
                    Depart(record, tick);
                    RHAH_LongFollowRuntime.Complete(record);
                    return true;
                }
                if (action == "keep")
                {
                    RHAH_LongFollowRuntime.CloseLetter(record);
                    if (record.Phase == KeepsakeClaimant)
                    {
                        record.Branch = 2;
                        record.Phase = SellingStigma;
                        record.DeadlineTick = Later(tick, GenDate.TicksPerDay);
                        record.SelectedIds.Clear();
                        record.SelectedIds.AddRange(record.VisitorIds);
                        RHAH_LongFollowRuntime.Say(record, Prefix + "KeptAfterClaim", child.LabelShort);
                        SocialTick(record, map, child, tick);
                    }
                    else
                    {
                        record.RevengePending = true;
                        record.DueTick = Later(tick, GenDate.TicksPerDay * 5);
                        record.Phase = ClaimRevengeWaiting;
                        Depart(record, tick);
                        RHAH_LongFollowRuntime.Say(record, Prefix + "LegacyRefused", child.LabelShort);
                    }
                    return true;
                }
            }
            return false;
        }

        internal static void OnVisitors(RHAH_LongFollowRecord record, Map map, IList<int> ids, int tick)
        {
            if (record.Completed || ids == null || ids.Count == 0 || (record.Phase != SellingStigma && record.Phase != ClaimTitle))
            {
                return;
            }
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (!RHAH_LongFollowRuntime.Alive(child) || !AtPlayerHome(record, child, map) || record.NextDependentBatchId == ids[0])
            {
                return;
            }
            record.NextDependentBatchId = ids[0];
            for (int i = 0; i < ids.Count; i++)
            {
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(ids[i], tick);
                if (visitor != child && RHAH_LongFollowRuntime.Alive(visitor) && RHAH_Api.IsRatkin(visitor) && !record.SelectedIds.Contains(ids[i]))
                {
                    record.SelectedIds.Add(ids[i]);
                }
            }
        }

        static Verse.Pawn Subject(RHAH_LongFollowRecord record, Map map, int tick)
        {
            Verse.Pawn current = RHAH_LongFollowRuntime.Pawn(record.SubjectId, tick);
            if (record.Opened || RHAH_LongFollowRuntime.Alive(current) && AtPlayerHome(record, current, map) && (record.DisplayId == "I-041" || RHAH_Api.TryGetExtra(current, RHAH_EventFollowMood.BoughtKey, out string _)))
            {
                return current;
            }
            for (int i = 0; i < record.ChildIds.Count; i++)
            {
                Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(record.ChildIds[i], tick);
                if (!RHAH_LongFollowRuntime.Alive(child) || !AtPlayerHome(record, child, map))
                {
                    continue;
                }
                if (record.DisplayId != "I-041" && !RHAH_Api.TryGetExtra(child, RHAH_EventFollowMood.BoughtKey, out string _))
                {
                    continue;
                }
                record.SubjectId = child.thingIDNumber;
                return child;
            }
            return null;
        }

        static bool Open(RHAH_LongFollowRecord record, Map map, Verse.Pawn child)
        {
            if (record.DisplayId == "I-041" && RHAH_Plague.HasActive(child))
            {
                record.MedicalHold = true;
                return false;
            }
            if (!AtPlayerHome(record, child, map) || !CanExplain(child) || RHAH_LongFollowRuntime.MedicallyPaused(child))
            {
                return false;
            }
            if (record.DisplayId != "I-041" && !RHAH_Api.TryGetExtra(child, RHAH_EventFollowMood.BoughtKey, out string _))
            {
                return false;
            }
            if (record.DisplayId == "I-041")
            {
                HediffDef plague = RHAH_Plague.Def;
                if (!record.MedicalHold && (plague == null || child.health?.immunity == null || child.health.immunity.GetImmunity(plague) < 1f))
                {
                    return false;
                }
                if (!RHAH_LongFollowRuntime.TryReserveClaim(record))
                {
                    RHAH_LongFollowRuntime.Complete(record);
                    return true;
                }
                record.VisitorIds.Clear();
                record.Opened = true;
                Present(record, map, ClaimOpening);
                return true;
            }

            if (child.inventory == null)
            {
                return false;
            }
            if (record.Keepsake == null)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamed("RHAH_LongKeepsake");
                Thing thing = ThingMaker.MakeThing(def);
                if (!child.inventory.innerContainer.TryAdd(thing, false))
                {
                    thing.Destroy(DestroyMode.Vanish);
                    return false;
                }
                record.Keepsake = thing;
            }
            record.VisitorIds.Clear();
            record.Opened = true;
            Present(record, map, RHAH_LongFollowRuntime.Adult(child) ? KeepsakeOrigin : KeepsakeOpening);
            return true;
        }
        static bool AtPlayerHome(RHAH_LongFollowRecord record, Verse.Pawn child, Map map)
        {
            return RHAH_LongFollowRuntime.AtHome(child, map) || (record.DisplayId != "I-041" && RHAH_LongFollowRuntime.Alive(child) && child.MapHeld == map && child.IsSlaveOfColony);
        }

        static bool CanExplain(Verse.Pawn child)
        {
            return child.DevelopmentalStage != DevelopmentalStage.Baby && child.health?.capacities != null && child.health.capacities.CapableOf(PawnCapacityDefOf.Talking);
        }

        static void Present(RHAH_LongFollowRecord record, Map map, int phase)
        {
            record.Phase = phase;
            record.LetterOpen = false;
            RHAH_LongFollowRuntime.OpenLetter(record, map);
        }

        static bool InInventory(Thing thing, Verse.Pawn pawn)
        {
            return thing != null && !thing.Destroyed && pawn.inventory?.innerContainer.Contains(thing) == true;
        }

        static bool AvailableKeepsake(RHAH_LongFollowRecord record, Map map)
        {
            return record.Keepsake != null && !record.Keepsake.Destroyed && record.Keepsake.MapHeld == map;
        }

        static void LoseKeepsake(RHAH_LongFollowRecord record, Verse.Pawn child)
        {
            record.KeepsakeDiscarded = true;
            record.KeepsakeTaken = false;
            RHAH_LongFollowRuntime.RemoveMood(child, "RHAH_LongKeepsakeTaken");
            RHAH_LongFollowRuntime.Mood(child, "RHAH_LongKeepsakeLost", -8, true);
            RHAH_LongFollowRuntime.Say(record, Prefix + "KeepsakeDestroyed", child.LabelShort);
            RHAH_LongFollowRuntime.Complete(record);
        }

        static bool PlayerKilledClaimant(RHAH_LongFollowRecord record, int tick)
        {
            if (record.Phase < KeepsakeClaimant || record.Phase == ClaimOpening || record.Phase == ClaimProbe || record.Phase == ClaimArrival || record.Phase == ClaimTitle)
            {
                return false;
            }
            if (record.PlayerKilledVisitor)
            {
                return true;
            }
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                if (RHAH_LongFollowRuntime.WasPlayerKilled(RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick)))
                {
                    return true;
                }
            }
            for (int i = 0; i < record.DepartedIds.Count; i++)
            {
                if (RHAH_LongFollowRuntime.WasPlayerKilled(RHAH_LongFollowRuntime.Pawn(record.DepartedIds[i], tick)))
                {
                    return true;
                }
            }
            return false;
        }

        static Verse.Pawn Visitor(RHAH_LongFollowRecord record, Map map, int tick)
        {
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick);
                if (RHAH_LongFollowRuntime.Alive(visitor) && visitor.MapHeld == map && !visitor.Downed && !visitor.IsPrisonerOfColony && visitor.Faction != Faction.OfPlayer)
                {
                    return visitor;
                }
            }
            return null;
        }

        static bool Scattered(RHAH_LongFollowRecord record, Map map, int tick)
        {
            return record.VisitorIds.Count > 0 && Visitor(record, map, tick) == null;
        }

        static bool OriginalPartyDefeated(RHAH_LongFollowRecord record, int tick)
        {
            bool defeated = false;
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                defeated = true;
                if (!Defeated(RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick)))
                {
                    return false;
                }
            }
            for (int i = 0; i < record.DepartedIds.Count; i++)
            {
                int id = record.DepartedIds[i];
                if (id == record.SubjectId || record.ChildIds.Contains(id))
                {
                    continue;
                }
                defeated = true;
                if (!Defeated(RHAH_LongFollowRuntime.Pawn(id, tick)))
                {
                    return false;
                }
            }
            return defeated;
        }

        static bool Defeated(Verse.Pawn visitor)
        {
            return visitor != null && (!RHAH_LongFollowRuntime.Alive(visitor) || visitor.Downed || visitor.IsPrisonerOfColony || visitor.Faction == Faction.OfPlayer);
        }

        static void HoldVisitors(RHAH_LongFollowRecord record, int tick)
        {
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                RHAH_LongFollowRuntime.Hold(RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick));
            }
        }

        static void Depart(RHAH_LongFollowRecord record, int tick)
        {
            for (int i = 0; i < record.VisitorIds.Count; i++)
            {
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(record.VisitorIds[i], tick);
                RHAH_LongFollowRuntime.Unhold(visitor);
                if (RHAH_LongFollowRuntime.Alive(visitor) && !visitor.IsPrisonerOfColony && visitor.Faction != Faction.OfPlayer)
                {
                    RHAH_LongFollowRuntime.Leave(visitor);
                }
            }
            for (int i = 0; i < record.DepartedIds.Count; i++)
            {
                int id = record.DepartedIds[i];
                if (id == record.SubjectId || record.ChildIds.Contains(id) || record.VisitorIds.Contains(id))
                {
                    continue;
                }
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(id, tick);
                RHAH_LongFollowRuntime.Unhold(visitor);
                if (RHAH_LongFollowRuntime.Alive(visitor) && !visitor.IsPrisonerOfColony && visitor.Faction != Faction.OfPlayer)
                {
                    RHAH_LongFollowRuntime.Leave(visitor);
                }
            }
        }

        static bool SocialReady(RHAH_LongFollowRecord record, Map map, Verse.Pawn child, int tick)
        {
            for (int i = 0; i < record.SelectedIds.Count; i++)
            {
                int id = record.SelectedIds[i];
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(id, tick);
                if (!RHAH_LongFollowRuntime.Alive(visitor) || visitor.MapHeld != map || visitor.IsPrisonerOfColony || visitor.HostileTo(child) ||
                    record.VisitorIds.Contains(id) && record.DeadlineTick >= 0 && tick >= record.DeadlineTick) return true;
                Verse.Pawn initiator = record.Phase == SellingStigma ? visitor : child;
                Verse.Pawn recipient = record.Phase == SellingStigma ? child : visitor;
                InteractionDef interaction = record.Phase == SellingStigma ? InteractionDefOf.Insult : InteractionDefOf.Chitchat;
                if (initiator.interactions?.CanInteractNowWith(recipient, interaction) == true) return true;
                if (initiator.Spawned && recipient.Spawned && !initiator.Drafted && !initiator.Downed && initiator.Awake() && initiator.jobs != null &&
                    initiator.CurJob?.playerForced != true && initiator.Position.DistanceToSquared(recipient.Position) > 4f && initiator.CurJobDef != JobDefOf.Goto &&
                    initiator.CanReach(recipient, PathEndMode.Touch, Danger.Some)) return true;
            }
            return false;
        }

        static bool SocialTick(RHAH_LongFollowRecord record, Map map, Verse.Pawn child, int tick)
        {
            bool changed = false;
            for (int i = record.SelectedIds.Count - 1; i >= 0; i--)
            {
                int id = record.SelectedIds[i];
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(id, tick);
                if (!RHAH_LongFollowRuntime.Alive(visitor) || visitor.MapHeld != map || visitor.IsPrisonerOfColony || visitor.HostileTo(child))
                {
                    record.SelectedIds.RemoveAt(i);
                    if (record.VisitorIds.Contains(id) && RHAH_LongFollowRuntime.Alive(visitor))
                    {
                        RHAH_LongFollowRuntime.Unhold(visitor);
                        RHAH_LongFollowRuntime.Leave(visitor);
                    }
                    changed = true;
                    continue;
                }
                if (record.VisitorIds.Contains(id) && record.DeadlineTick >= 0 && tick >= record.DeadlineTick)
                {
                    record.SelectedIds.RemoveAt(i);
                    RHAH_LongFollowRuntime.Unhold(visitor);
                    RHAH_LongFollowRuntime.Leave(visitor);
                    changed = true;
                    continue;
                }
                bool insult = record.Phase == SellingStigma;
                Verse.Pawn initiator = insult ? visitor : child;
                Verse.Pawn recipient = insult ? child : visitor;
                InteractionDef interaction = insult ? InteractionDefOf.Insult : InteractionDefOf.Chitchat;
                if (initiator.interactions?.TryInteractWith(recipient, interaction) == true)
                {
                    RHAH_LongFollowRuntime.Say(record, Prefix + (insult ? "SellingMention" : "TitleMention"), child.LabelShort, visitor.LabelShort);
                    record.SelectedIds.RemoveAt(i);
                    if (record.VisitorIds.Contains(id))
                    {
                        RHAH_LongFollowRuntime.Unhold(visitor);
                        RHAH_LongFollowRuntime.Leave(visitor);
                    }
                    return true;
                }
                if (initiator.Spawned && recipient.Spawned && !initiator.Drafted && !initiator.Downed && initiator.Awake() && initiator.jobs != null && initiator.CurJob?.playerForced != true && initiator.CanReach(recipient, PathEndMode.Touch, Danger.Some))
                {
                    if (initiator.Position.DistanceToSquared(recipient.Position) > 4f && initiator.CurJobDef != JobDefOf.Goto)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, recipient);
                        job.locomotionUrgency = LocomotionUrgency.Walk;
                        if (initiator.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                        {
                            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                            wait.expiryInterval = GenDate.TicksPerHour * 2;
                            initiator.jobs.jobQueue.EnqueueLast(wait, JobTag.Misc);
                            changed = true;
                        }
                    }
                }
            }
            return changed;
        }

        static bool ReturnKeepsake(Thing thing, Verse.Pawn child)
        {
            Map oldMap = thing.Map;
            IntVec3 oldPosition = thing.Position;
            bool spawned = thing.Spawned;
            if (spawned)
            {
                thing.DeSpawn();
            }
            if (child.inventory.innerContainer.TryAddOrTransfer(thing, false))
            {
                return true;
            }
            if (spawned)
            {
                GenSpawn.Spawn(thing, oldPosition, oldMap);
            }
            return false;
        }

        static int Later(int tick, int delay)
        {
            return tick > int.MaxValue - delay ? int.MaxValue : tick + delay;
        }
    }
}
