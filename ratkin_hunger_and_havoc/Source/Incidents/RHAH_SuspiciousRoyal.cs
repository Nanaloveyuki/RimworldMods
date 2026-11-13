using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_SuspiciousRoyal
    {
        internal const string DisplayId = "I-057";
        internal const int Price = 2500;

        internal static bool Available(int lastArrivalTick, int tick, int silver)
        {
            return silver > (lastArrivalTick < 0 ? Price : 5000) &&
                (lastArrivalTick < 0 || (long)tick - lastArrivalTick >= 15L * GenDate.TicksPerYear);
        }

        internal static void HideHistory(Verse.Pawn pawn)
        {
            pawn.story.Childhood = DefDatabase<BackstoryDef>.GetNamed("RHAH_RoyalUnknownChildhood");
            pawn.story.Adulthood = DefDatabase<BackstoryDef>.GetNamed("RHAH_RoyalUnknownAdulthood");
            pawn.Notify_DisabledWorkTypesChanged();
        }

        internal static bool Open(RHAH_IncidentContext context, List<Verse.Pawn> arrived)
        {
            int tick = Find.TickManager.TicksGame;
            RHAH_ChoiceRecord record = RHAH_ChoiceRuntime.Open(Current.Game.GetComponent<GameComponent_RHAH_Game>(),
                new RHAH_ChoiceRecord
                {
                    DisplayId = DisplayId,
                    MapId = context.Map.uniqueID,
                    BatchId = context.SpawnBatchId,
                    Kind = RHAH_RequestKind.Silver,
                    Choice = RHAH_ChoiceKind.Aid,
                    Amount = Price,
                    RoyalGenuine = Rand.Chance(0.5f),
                    ExpireTick = RHAH_WarlordBombardment.NextTick(tick,
                        GenDate.TicksPerDay * (RHAH_Mod.Settings?.requestDays ?? RHAH_RequestRules.RequestDays))
                });
            if (record == null) return false;
            for (int i = 0; i < arrived.Count; i++) record.PawnLoadIds.Add(arrived[i].thingIDNumber);
            ChoiceLetter_RHAH_Request letter = (ChoiceLetter_RHAH_Request)LetterMaker.MakeLetter(
                "RHAH_Incident_SuspiciousRoyal_Label".Translate(),
                "RHAH_Royal_Arrival".Translate() + "\n\n" + "RHAH_Royal_Request".Translate(),
                RHAH_DefOf.RHAH_ChoiceRequest, new LookTargets(arrived));
            letter.choiceId = record.Id;
            letter.mapId = record.MapId;
            letter.kind = record.Kind;
            letter.amount = Price;
            letter.expireTick = record.ExpireTick;
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }

        internal static bool CanPay(RHAH_ChoiceRecord record)
        {
            return RHAH_RoyalSilver.CanPay(RHAH_ChoiceRuntime.FindMap(record.MapId), RHAH_ChoiceRuntime.Present(record));
        }

        internal static RHAH_ChoiceAction Settle(RHAH_ChoiceRecord record, RHAH_ChoiceAction requested, int tick, bool enabled)
        {
            if (!record.Open) return RHAH_ChoiceAction.None;
            if (record.ExpireTick >= 0 && tick >= record.ExpireTick) requested = RHAH_ChoiceAction.Timeout;
            if (requested != RHAH_ChoiceAction.Deliver && requested != RHAH_ChoiceAction.Reject &&
                requested != RHAH_ChoiceAction.Ignore && requested != RHAH_ChoiceAction.Timeout)
                return RHAH_ChoiceAction.None;
            if (requested == RHAH_ChoiceAction.Deliver)
            {
                if (!enabled || RHAH_ChoiceRuntime.FindMap(record.MapId) == null ||
                    RHAH_ChoiceRuntime.Present(record).Count == 0) return RHAH_ChoiceAction.None;
                int dueTick = RHAH_RoyalRepayment.DueTick(tick);
                if (dueTick < 0 || !CanPay(record)) return RHAH_ChoiceAction.None;
                int chainId = RHAH_RoyalRepayment.Schedule(record, tick, dueTick);
                if (chainId <= 0) return RHAH_ChoiceAction.None;
                if (!RHAH_RoyalSilver.TryPay(RHAH_ChoiceRuntime.FindMap(record.MapId), RHAH_ChoiceRuntime.Present(record)))
                {
                    RHAH_RoyalRepayment.Cancel(chainId, tick);
                    return RHAH_ChoiceAction.None;
                }
                record.RoyalRepaymentInstanceId = chainId;
            }
            record.Settled = requested;
            RHAH_BatchAttitude.OrderVisitorLeave(RHAH_ChoiceRuntime.Present(record));
            return requested;
        }
    }
}
