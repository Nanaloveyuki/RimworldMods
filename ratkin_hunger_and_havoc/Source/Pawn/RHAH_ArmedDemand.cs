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

        internal static RHAH_ChoiceRecord FoodRequest(Verse.Pawn pawn)
        {
            LordJob_RHAH_Visitor job = pawn?.GetLord()?.LordJob as LordJob_RHAH_Visitor;
            if (job == null || job.ArmedAssault || job.ArmedChoiceId <= 0) return null;
            RHAH_ChoiceRecord record = Find(job.ArmedChoiceId);
            return IsRequest(record) && record.Open ? record : null;
        }

        internal static void Initialize(RHAH_ChoiceRecord record, List<Verse.Pawn> members)
        {
            LordJob_RHAH_Visitor job = members.Count == 0 ? null : members[0].GetLord()?.LordJob as LordJob_RHAH_Visitor;
            if (job != null) job.ArmedChoiceId = record.Id;
        }

        internal static Lord Owner(RHAH_ChoiceRecord record)
        {
            Map map = record == null ? null : RHAH_ChoiceRuntime.FindMap(record.MapId);
            List<Lord> lords = map?.lordManager?.lords;
            for (int i = 0; lords != null && i < lords.Count; i++)
            {
                Lord lord = lords[i];
                if (!(lord.LordJob is LordJob_RHAH_Visitor job) || job.ArmedChoiceId != record.Id) continue;
                for (int j = 0; j < lord.ownedPawns.Count; j++)
                {
                    Verse.Pawn pawn = lord.ownedPawns[j];
                    IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
                    if (snapshot?.SpawnBatchId == record.BatchId && record.PawnLoadIds.Contains(pawn.thingIDNumber) &&
                        pawn.Spawned && !pawn.Downed && RHAH_BatchAttitude.CanOrderLeave(pawn)) return lord;
                }
            }
            return null;
        }

        internal static bool Refresh(RHAH_ChoiceRecord record)
        {
            if (record == null || !record.Open) return false;
            if (Owner(record) != null) return true;
            Finish(record, RHAH_ChoiceAction.Ignore);
            return false;
        }

        static void Finish(RHAH_ChoiceRecord record, RHAH_ChoiceAction action)
        {
            record.Settled = action;
            List<Letter> letters = Verse.Find.LetterStack?.LettersListForReading;
            for (int i = (letters?.Count ?? 0) - 1; i >= 0; i--)
                if (letters[i] is ChoiceLetter_RHAH_Request request && request.choiceId == record.Id)
                    Verse.Find.LetterStack.RemoveLetter(request);
        }

        internal static void Tick(RHAH_ChoiceRecord record, int tick)
        {
            if (!record.Open) return;
            if (!Enabled) { Resolve(record, RHAH_ChoiceAction.Ignore); return; }
            if (tick % GenDate.TicksPerHour != 0 && tick != record.ExpireTick) return;
            if (!Refresh(record)) return;
            LordJob_RHAH_Visitor job = (LordJob_RHAH_Visitor)Owner(record).LordJob;
            if (!job.ArmedAssault && record.ExpireTick >= 0 && tick >= record.ExpireTick)
                Resolve(record, RHAH_ChoiceAction.Timeout);
        }

        internal static bool NotifyPlayerHarm(int choiceId)
        {
            RHAH_ChoiceRecord record = Find(choiceId);
            if (!Enabled || !IsRequest(record) || !record.Open) return false;
            Resolve(record, RHAH_ChoiceAction.Reject);
            return true;
        }

        internal static int Remaining(RHAH_ChoiceRecord record)
        {
            return Math.Max(0, record.Amount - record.ArmedFoodDelivered);
        }

        internal static void NoteFood(Verse.Pawn receiver, int count)
        {
            Lord lord = receiver?.GetLord();
            LordJob_RHAH_Visitor job = lord?.LordJob as LordJob_RHAH_Visitor;
            RHAH_ChoiceRecord record = job == null ? null : Find(job.ArmedChoiceId);
            if (count <= 0 || !IsRequest(record) || !record.Open || Owner(record) != lord ||
                record.BatchId != RHAH_Api.Get(receiver)?.SpawnBatchId ||
                !record.PawnLoadIds.Contains(receiver.thingIDNumber)) return;
            record.ArmedFoodDelivered += Math.Min(count, Remaining(record));
            if (Remaining(record) == 0) Resolve(record, RHAH_ChoiceAction.Deliver);
        }
        internal static void Resolve(RHAH_ChoiceRecord record, RHAH_ChoiceAction action)
        {
            if (record == null || !record.Open) return;
            Lord lord = Owner(record);
            LordJob_RHAH_Visitor job = lord?.LordJob as LordJob_RHAH_Visitor;
            if (job == null)
            {
                Finish(record, RHAH_ChoiceAction.Ignore);
                return;
            }
            List<Verse.Pawn> members = new List<Verse.Pawn>();
            for (int i = 0; i < lord.ownedPawns.Count; i++)
                if (RHAH_BatchAttitude.CanOrderLeave(lord.ownedPawns[i])) members.Add(lord.ownedPawns[i]);
            if (!Enabled || action == RHAH_ChoiceAction.Deliver)
            {
                Finish(record, action == RHAH_ChoiceAction.Deliver ? action : RHAH_ChoiceAction.Ignore);
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
            if (map == null || !record.Open || record.Amount <= 0 || !Refresh(record)) return false;
            List<Verse.Pawn> members = Owner(record).ownedPawns;
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
            IReadOnlyList<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            List<Thing> available = map.listerThings.ThingsOfDef(ThingDefOf.MealSimple);
            for (int i = 0; i < available.Count; i++)
            {
                Thing thing = available[i];
                if (!RHAH_FoodTransaction.IsUsableFood(thing, members) ||
                    !RHAH_FoodTransaction.CanDeliverFood(thing, carrier, colonists) ||
                    !carrier.inventory.innerContainer.CanAcceptAnyOf(thing, false)) continue;
                stock += thing.stackCount;
                food.Add(thing);
            }
            return stock >= Remaining(record);
        }

        internal static bool TryPay(RHAH_ChoiceRecord record)
        {
            Map map;
            Verse.Pawn carrier;
            List<Thing> food;
            if (!PlanPayment(record, out map, out carrier, out food)) return false;
            List<Thing> payment = new List<Thing>();
            List<IntVec3> origins = new List<IntVec3>();
            int remaining = Remaining(record);
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
