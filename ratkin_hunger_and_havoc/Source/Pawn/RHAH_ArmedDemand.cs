using System;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_ArmedDemand
    {
        internal static bool Enabled => RHAH_Mod.Settings == null || RHAH_Mod.Settings.armedDemandEnabled;

        internal static bool IsSiege(string displayId)
        {
            return displayId == "I-014" || displayId == "I-030" || displayId == "I-045";
        }

        internal static bool IsRequest(RHAH_ChoiceRecord record)
        {
            return record != null && IsSiege(record.DisplayId) && record.Kind == RHAH_RequestKind.SimpleMeal;
        }

        internal static RHAH_ChoiceRecord Find(int choiceId)
        {
            IReadOnlyList<RHAH_ChoiceRecord> records = Current.Game?.GetComponent<GameComponent_RHAH_Game>()?.OpenChoices;
            for (int i = 0; records != null && i < records.Count; i++)
            {
                if (records[i].Id == choiceId) return records[i];
            }
            return null;
        }
        internal static bool StartsAssault(bool enabled, bool alreadyAssaulting, RHAH_ChoiceAction action)
        {
            return enabled && !alreadyAssaulting &&
                (action == RHAH_ChoiceAction.Reject || action == RHAH_ChoiceAction.Timeout);
        }

        internal static bool Holds(Verse.Pawn pawn)
        {
            LordJob_RHAH_Visitor job = pawn?.GetLord()?.LordJob as LordJob_RHAH_Visitor;
            return Enabled && job != null && job.ArmedChoiceId > 0 && Find(job.ArmedChoiceId)?.Open == true;
        }

        internal static bool Refused(Verse.Pawn pawn)
        {
            return (pawn?.GetLord()?.LordJob as LordJob_RHAH_Visitor)?.ArmedAssault == true;
        }

        internal static void Initialize(RHAH_ChoiceRecord record, List<Verse.Pawn> members)
        {
            LordJob_RHAH_Visitor job = members.Count == 0 ? null : members[0].GetLord()?.LordJob as LordJob_RHAH_Visitor;
            if (job != null) job.ArmedChoiceId = record.Id;
        }

        internal static void Resolve(RHAH_ChoiceRecord record, RHAH_ChoiceAction action)
        {
            if (record == null || !record.Open) return;
            List<Verse.Pawn> members = RHAH_ChoiceRuntime.Present(record);
            LordJob_RHAH_Visitor job = members.Count == 0 ? null : members[0].GetLord()?.LordJob as LordJob_RHAH_Visitor;
            if (!Enabled) record.Settled = RHAH_ChoiceAction.Ignore;
            if (job == null || job.ArmedChoiceId != record.Id) return;
            if (!Enabled || action == RHAH_ChoiceAction.Deliver)
            {
                record.Settled = action == RHAH_ChoiceAction.Deliver ? action : RHAH_ChoiceAction.Ignore;
                RHAH_BatchAttitude.AcceptFood(members[0]);
                job.ArmedAssault = false;
                job.ArmedChoiceId = 0;
                RHAH_BatchAttitude.OrderVisitorLeave(members);
                if (action == RHAH_ChoiceAction.Deliver)
                    Messages.Message("RHAH_ArmedDemand_Paid".Translate(), new LookTargets(members), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (!StartsAssault(Enabled, job.ArmedAssault, action)) return;
            job.ArmedAssault = true;
            RHAH_BatchAttitude.StartArmedAssault(members);
            members[0].GetLord()?.ReceiveMemo("RHAH_ArmedAssault");
            Messages.Message("RHAH_ArmedDemand_Attack".Translate(members[0].LabelShortCap), new LookTargets(members), MessageTypeDefOf.ThreatBig);
        }

        internal static bool CanPay(RHAH_ChoiceRecord record)
        {
            Map map;
            Verse.Pawn carrier;
            List<Thing> food;
            return PlanPayment(record, out map, out carrier, out food);
        }

        static bool PlanPayment(RHAH_ChoiceRecord record, out Map map, out Verse.Pawn carrier, out List<Thing> food)
        {
            map = RHAH_ChoiceRuntime.FindMap(record.MapId);
            carrier = null;
            food = new List<Thing>();
            if (map == null || !record.Open || record.Amount <= 0) return false;
            List<Verse.Pawn> members = RHAH_ChoiceRuntime.Present(record);
            for (int i = 0; i < members.Count; i++)
            {
                Verse.Pawn member = members[i];
                if (!member.Downed && RHAH_BatchAttitude.CanOrderLeave(member) && member.inventory != null &&
                    (member.GetLord()?.LordJob as LordJob_RHAH_Visitor)?.ArmedChoiceId == record.Id)
                {
                    carrier = member;
                    break;
                }
            }
            if (carrier == null) return false;
            int stock = 0;
            List<Thing> available = map.listerThings.ThingsOfDef(ThingDefOf.MealSimple);
            for (int i = 0; i < available.Count; i++)
            {
                Thing thing = available[i];
                if (!RHAH_FoodTransaction.IsUsableFood(thing, members) ||
                    !carrier.CanReach(thing, PathEndMode.Touch, Danger.Deadly) ||
                    !carrier.inventory.innerContainer.CanAcceptAnyOf(thing, false)) continue;
                stock += thing.stackCount;
                food.Add(thing);
            }
            return stock >= record.Amount;
        }

        internal static bool TryPay(RHAH_ChoiceRecord record)
        {
            Map map;
            Verse.Pawn carrier;
            List<Thing> food;
            if (!PlanPayment(record, out map, out carrier, out food)) return false;
            List<Thing> payment = new List<Thing>();
            List<IntVec3> origins = new List<IntVec3>();
            int remaining = record.Amount;
            try
            {
                for (int i = 0; i < food.Count && remaining > 0; i++)
                {
                    Thing source = food[i];
                    int count = Math.Min(remaining, source.stackCount);
                    origins.Add(source.Position);
                    Thing split = source.SplitOff(count);
                    payment.Add(split);
                    if (!carrier.inventory.innerContainer.TryAdd(split, false))
                        throw new InvalidOperationException("Demand food inventory refused payment");
                    remaining -= count;
                }
                return remaining == 0;
            }
            catch (Exception exception)
            {
                for (int i = 0; i < payment.Count; i++)
                {
                    payment[i].holdingOwner?.Remove(payment[i]);
                    GenPlace.TryPlaceThing(payment[i], origins[i], map, ThingPlaceMode.Near);
                }
                Log.Warning("[RHAH] Armed demand payment rolled back: " + exception.Message);
                return false;
            }
        }
    }
}
