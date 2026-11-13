using System;
using System.Collections.Generic;
using System.Globalization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Storyteller.Suiyin;
using Oxygendsl.Core.Events;
using Oxygendsl.Mod.Events;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.EventMgr
{
    internal sealed class RHAH_RoyalRepayment : IEventChainDefinition
    {
        public string Id => RHAH_SuspiciousRoyal.DisplayId;
        public int Version => 1;

        internal static int DueTick(int tick)
        {
            long due = (long)tick + GenDate.TicksPerYear;
            return due > int.MaxValue ? -1 : (int)due;
        }

        internal static int Schedule(RHAH_ChoiceRecord record, int tick, int dueTick)
        {
            EventChainRuntime runtime = OxygenEventChainHost.Runtime;
            return runtime == null ? 0 : runtime.Start(RHAH_EventChainProvider.ProviderKey, RHAH_SuspiciousRoyal.DisplayId,
                record.MapId, RHAH_EventChainMigration.SiteTile(record.MapId), tick, -1,
                record.Id.ToString(CultureInfo.InvariantCulture), 0, dueTick);
        }

        internal static void Cancel(int chainId, int tick)
        {
            OxygenEventChainHost.Runtime.End(RHAH_EventChainProvider.ProviderKey, chainId, tick, EventChainEnd.Cancelled);
        }

        internal static int Reward(int rank, int goodwill)
        {
            int multiplier;
            switch (rank)
            {
                case 0: multiplier = 18; break;
                case 1: multiplier = 15; break;
                case 2: multiplier = 12; break;
                case 3: multiplier = 8; break;
                case 4: multiplier = 5; break;
                default: throw new ArgumentOutOfRangeException(nameof(rank));
            }
            return multiplier * RHAH_SuspiciousRoyal.Price + Math.Max(0, goodwill + 15 - 100) * 1000;
        }

        internal static List<Thing> SilverStacks(int amount)
        {
            List<Thing> silver = new List<Thing>();
            while (amount > 0)
            {
                Thing stack = ThingMaker.MakeThing(ThingDefOf.Silver);
                stack.stackCount = Math.Min(amount, ThingDefOf.Silver.stackLimit);
                silver.Add(stack);
                amount -= stack.stackCount;
            }
            return silver;
        }

        public void Prepare(EventChainInstance instance)
        {
            if (instance.DefinitionId != Id || instance.DefinitionVersion != Version || instance.SiteId < 0 || instance.Stage != 0)
                throw new FormatException("Invalid RHAH royal repayment instance.");
            if (instance.End != EventChainEnd.None) return;
            if (!int.TryParse(instance.Payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out int choiceId) || choiceId <= 0)
                throw new FormatException("Invalid RHAH royal repayment payload.");
            instance.PreparedState = choiceId;
        }

        public EventChainDecision Evaluate(EventChainInstance instance, int tick)
        {
            return EventChainDecision.Action("royal_reply", -1);
        }

        public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
        {
            if (actionId != "royal_reply" || !(instance.PreparedState is int choiceId))
                return EventChainActionResult.Failed("Royal repayment is not prepared or has an unknown action.");
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            RHAH_ChoiceRecord record = game == null ? null : RHAH_ChoiceRuntime.FindRecord(game, choiceId);
            if (record == null || record.Settled != RHAH_ChoiceAction.Deliver || record.RoyalReplySent)
                return EventChainActionResult.Applied("no_pending_promise", EventChainEnd.Completed);
            if (record.DisplayId != Id || record.MapId != instance.SiteId || record.RoyalRepaymentInstanceId != instance.Id)
                return EventChainActionResult.Failed("Royal promise does not match the original request.");
            if (!record.RoyalGenuine)
            {
                CleanupImpostors(record, tick);
                record.RoyalReplySent = true;
                Reply("RHAH_Royal_Fraud", LetterDefOf.NeutralEvent);
                return EventChainActionResult.Applied("fraud_revealed", EventChainEnd.Completed);
            }
            Faction kingdom = Kingdom();
            if (kingdom == null)
            {
                record.RoyalReplySent = true;
                Reply("RHAH_Royal_Lost", LetterDefOf.NeutralEvent);
                return EventChainActionResult.Applied("kingdom_unavailable", EventChainEnd.Completed);
            }
            Map map = RHAH_ChoiceRuntime.FindMap(record.MapId);
            if (map == null) return EventChainActionResult.Applied("original_map_unavailable", EventChainEnd.Completed);
            IntVec3 cell = DropCellFinder.TradeDropSpot(map);
            if (!cell.IsValid) return EventChainActionResult.Failed("Royal repayment has no valid drop location.");
            int goodwill = kingdom.BaseGoodwillWith(Faction.OfPlayer);
            bool hostile = kingdom.HostileTo(Faction.OfPlayer);
            if (record.RoyalRank < 0) record.RoyalRank = Rand.Range(0, 5);
            int amount = Reward(record.RoyalRank, goodwill);
            List<Thing> silver = SilverStacks(amount);
            DropPodUtility.DropThingsNear(cell, map, silver, 110, false, false, true, false, true, kingdom);
            kingdom.ChangeGoodwill_Debug(Faction.OfPlayer, hostile ? -goodwill : 15);
            record.RoyalReplySent = true;
            Find.LetterStack.ReceiveLetter("RHAH_Royal_ReplyLabel".Translate(),
                "RHAH_Royal_Thanks".Translate(("RHAH_Royal_Rank" + record.RoyalRank).Translate()),
                LetterDefOf.PositiveEvent, new TargetInfo(cell, map));
            if (hostile) Reply("RHAH_Royal_Peace", LetterDefOf.PositiveEvent);
            return EventChainActionResult.Applied("royal_repaid", EventChainEnd.Completed);
        }

        internal static Faction Kingdom()
        {
            List<Settlement> settlements = Find.WorldObjects?.Settlements;
            if (settlements == null) return null;
            for (int i = 0; i < settlements.Count; i++)
            {
                Settlement settlement = settlements[i];
                if (settlement == null) continue;
                Faction faction = settlement?.Faction;
                if (!settlement.Destroyed && faction?.def?.defName == "Rakinia" && !faction.defeated &&
                    Find.FactionManager?.AllFactionsListForReading.Contains(faction) == true) return faction;
            }
            return null;
        }

        internal static void CleanupImpostors(RHAH_ChoiceRecord record, int tick)
        {
            if (Find.WorldPawns == null) return;
            for (int i = 0; i < record.PawnLoadIds.Count; i++)
            {
                Verse.Pawn pawn = RHAH_PawnIndex.Find(record.PawnLoadIds[i], tick);
                if (pawn == null || pawn.Destroyed || pawn.SpawnedOrAnyParentSpawned || !Find.WorldPawns.Contains(pawn)) continue;
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
            RHAH_PawnIndex.Clear();
        }

        static void Reply(string key, LetterDef def)
        {
            Find.LetterStack.ReceiveLetter("RHAH_Royal_ReplyLabel".Translate(), key.Translate(), def);
        }
    }
}
