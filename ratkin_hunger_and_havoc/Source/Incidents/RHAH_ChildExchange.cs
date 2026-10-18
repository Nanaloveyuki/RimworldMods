using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_ChildExchange
    {
        internal static bool AcceptsChild(RHAH_ChoiceRecord record)
        {
            return record != null && (record.DisplayId == "I-013" || record.DisplayId == "I-019");
        }

        internal static int FoodCost(RHAH_ChoiceRecord record)
        {
            if (!AcceptsChild(record)) return 0;
            return RHAH_RequestRules.FoodForChildren(record.DisplayId == "I-019" ? 1 : IncomingCount(record)) *
                (record.DisplayId == "I-019" ? 2 : 1);
        }

        internal static bool FoodAlternative(RHAH_ChoiceRecord record)
        {
            return record != null && record.Settled == RHAH_ChoiceAction.Deliver &&
                (record.ChildExchangeVersion > 0 ? AcceptsChild(record) && record.ChildFoodAlternative :
                    record.Choice == RHAH_ChoiceKind.ChildExchange && record.Kind != RHAH_RequestKind.Baby);
        }

        internal static bool IsYoung(Verse.Pawn pawn)
        {
            return pawn != null && pawn.RaceProps != null && pawn.RaceProps.Humanlike && pawn.ageTracker != null &&
                pawn.ageTracker.AgeBiologicalYearsFloat < 14f;
        }

        internal static bool Eligible(Verse.Pawn pawn, Map map)
        {
            return IsYoung(pawn) && pawn.Spawned && pawn.Map == map && !pawn.Dead && !RHAH_VisitorStay.PausesStay(pawn) &&
                pawn.Faction == Faction.OfPlayer && !pawn.IsPrisoner && !pawn.IsSlave &&
                (!RHAH_Api.IsOrigin(pawn) || RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Transfer));
        }

        internal static List<Verse.Pawn> Candidates(RHAH_ChoiceRecord record)
        {
            List<Verse.Pawn> result = new List<Verse.Pawn>();
            Map map = RHAH_EventMap(record);
            if (map == null || !AcceptsChild(record)) return result;
            IReadOnlyList<Verse.Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++) if (Eligible(pawns[i], map)) result.Add(pawns[i]);
            result.Sort((left, right) => left.thingIDNumber.CompareTo(right.thingIDNumber));
            return result;
        }

        static Map RHAH_EventMap(RHAH_ChoiceRecord record)
        {
            return record == null ? null : RHAH_ChoiceRuntime.FindMap(record.MapId);
        }

        internal static int IncomingCount(RHAH_ChoiceRecord record)
        {
            List<Verse.Pawn> visitors = RHAH_ChoiceRuntime.Present(record);
            int count = 0;
            for (int i = 0; i < visitors.Count; i++) if (IsYoung(visitors[i])) count++;
            return count;
        }

        static bool ReleaseAllowed(Verse.Pawn pawn, RHAH_ReleaseReason reason)
        {
            return !RHAH_Api.IsOrigin(pawn) || RHAH_ApiHost.CanReleaseToColony(pawn, reason);
        }

        static bool Joinable(List<Verse.Pawn> incoming)
        {
            if (incoming.Count == 0) return false;
            for (int i = 0; i < incoming.Count; i++)
            {
                Verse.Pawn pawn = incoming[i];
                if (RHAH_Plague.HasActive(pawn) || !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.JoinColony)) return false;
                if (!ReleaseAllowed(pawn, RHAH_ReleaseReason.JoinedPlayerFaction)) return false;
            }
            return true;
        }

        internal static bool CanDeliver(RHAH_ChoiceRecord record, Verse.Pawn outgoing, bool foodAlternative)
        {
            return UnavailableReason(record, outgoing, foodAlternative) == null;
        }

        internal static string UnavailableReason(RHAH_ChoiceRecord record, Verse.Pawn outgoing, bool foodAlternative)
        {
            Map map = RHAH_EventMap(record);
            if (!AcceptsChild(record) || map == null || !record.Open) return "RHAH_Choice_Stale";
            List<Verse.Pawn> visitors = RHAH_ChoiceRuntime.Present(record);
            if (visitors.Count == 0 || visitors[0].Faction == null || visitors[0].Faction == Faction.OfPlayer)
                return "RHAH_Choice_NoPresent";
            if (record.DisplayId == "I-013" && !Joinable(visitors.FindAll(IsYoung)))
                return "RHAH_ChildExchange_NoIncoming";
            if (foodAlternative)
            {
                if (RHAH_Mod.Settings != null && !RHAH_Mod.Settings.childExchangeFoodSubstitution)
                    return "RHAH_ChildExchange_FoodDisabled";
                return RHAH_ChoiceRuntime.Stock(map, RHAH_RequestKind.SimpleMeal) >= FoodCost(record)
                    ? null : "RHAH_ChildExchange_NoFood";
            }
            return Eligible(outgoing, map) && ReleaseAllowed(outgoing, RHAH_ReleaseReason.ModRequest)
                ? null : "RHAH_ChildExchange_NoChild";
        }

        internal static bool TryExchange(RHAH_ChoiceRecord record, Verse.Pawn outgoing, bool foodAlternative)
        {
            if (!CanDeliver(record, outgoing, foodAlternative)) return false;
            Map map = RHAH_EventMap(record);
            List<Verse.Pawn> visitors = RHAH_ChoiceRuntime.Present(record);
            List<Verse.Pawn> incoming = record.DisplayId == "I-013" ? visitors.FindAll(IsYoung) : new List<Verse.Pawn>();
            Faction counterpart = visitors[0].Faction;
            Verse.Pawn responsible = Responsible(map, outgoing);
            // All join and payment gates are checked before changing custody.
            if (foodAlternative && !RHAH_ChoiceRuntime.TryConsume(map, RHAH_RequestKind.SimpleMeal, FoodCost(record))) return false;
            for (int i = 0; i < incoming.Count; i++)
            {
                RHAH_ApiHost.CommitReleaseToColony(incoming[i], RHAH_ReleaseReason.JoinedPlayerFaction);
                RHAH_VisitorRules.ApplyPlayerIdeo(incoming[i], RHAH_Mod.Settings == null ? 100 : RHAH_Mod.Settings.playerIdeoPercent, Rand.Value);
                incoming[i].SetFaction(Faction.OfPlayer);
                RHAH_VisitorMood.NotifySheltered(incoming[i]);
            }
            if (!foodAlternative)
            {
                Faction faction = counterpart;
                if (RHAH_Api.IsOrigin(outgoing)) RHAH_ApiHost.CommitReleaseToColony(outgoing, RHAH_ReleaseReason.ModRequest);
                RHAH_VisitorGroup.NotifyReleased(outgoing);
                outgoing.SetFaction(faction);
                outgoing.ExitMap(false, Rot4.Invalid);
                if (!Find.WorldPawns.Contains(outgoing)) Find.WorldPawns.PassToWorld(outgoing, PawnDiscardDecideMode.KeepForever);
                record.OutgoingChildThingId = outgoing.thingIDNumber;
            }
            record.ChildExchangeVersion = 1;
            record.ChildFoodAlternative = foodAlternative;
            record.ResponsibleAdultThingId = responsible?.thingIDNumber ?? 0;
            List<int> incomingIds = new List<int>();
            for (int i = 0; i < incoming.Count; i++) incomingIds.Add(incoming[i].thingIDNumber);
            EventMgr.RHAH_LongFollowRuntime.NoteChildTransfer(record.DisplayId, record.BatchId, record.MapId,
                foodAlternative ? null : outgoing, responsible, incomingIds, foodAlternative);
            return true;
        }

        static Verse.Pawn Responsible(Map map, Verse.Pawn child)
        {
            if (child?.relations != null)
            {
                List<DirectPawnRelation> relations = child.relations.DirectRelations;
                for (int i = 0; i < relations.Count; i++)
                {
                    Verse.Pawn adult = relations[i].otherPawn;
                    if (relations[i].def == PawnRelationDefOf.Parent && adult != null && adult.Spawned && adult.Map == map &&
                        adult.Faction == Faction.OfPlayer && !IsYoung(adult)) return adult;
                }
            }
            IReadOnlyList<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            Verse.Pawn chosen = null;
            for (int i = 0; i < colonists.Count; i++)
                if (!IsYoung(colonists[i]) && (chosen == null || colonists[i].thingIDNumber < chosen.thingIDNumber)) chosen = colonists[i];
            return chosen;
        }
    }
}
