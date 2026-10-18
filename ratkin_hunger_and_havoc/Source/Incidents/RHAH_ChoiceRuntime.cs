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
    internal static class RHAH_ChoiceRuntime
    {
        internal static RHAH_ChoiceRecord Open(GameComponent_RHAH_Game game, RHAH_ChoiceRecord record)
        {
            if (game == null || record == null || string.IsNullOrEmpty(record.DisplayId))
            {
                return null;
            }

            List<RHAH_ChoiceRecord> records = Records(game);
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Open && records[i].BatchId == record.BatchId && records[i].MapId == record.MapId)
                {
                    return records[i];
                }
            }

            if (RHAH_MouseSwarm.IsSource(record.DisplayId)) record.ExpireTick = -1;
            record.Id = NextId(game);
            records.Add(record);
            return record;
        }

        internal static RHAH_ChoiceAction TrySettle(
            GameComponent_RHAH_Game game,
            int choiceId,
            RHAH_ChoiceAction requested,
            int tick,
            bool enabled,
            bool canDeliver,
            int allyFactionId = 0)
        {
            RHAH_ChoiceRecord record = FindRecord(game, choiceId);
            if (record == null)
            {
                return RHAH_ChoiceAction.None;
            }
            if (requested == RHAH_ChoiceAction.Feed &&
                !RHAH_RequestRules.ShowsFoodGive(record.DisplayId, record.Choice)) return RHAH_ChoiceAction.None;
            bool swarm = RHAH_MouseSwarm.IsSource(record.DisplayId);
            if (swarm)
            {
                if (requested == RHAH_ChoiceAction.Timeout) return RHAH_ChoiceAction.None;
                if (requested == RHAH_ChoiceAction.Feed)
                {
                    List<Verse.Pawn> present = Present(record);
                    for (int i = 0; i < present.Count; i++)
                        if (RHAH_MouseSwarm.IsProvoked(present[i])) return RHAH_ChoiceAction.None;
                }
            }

            RHAH_ChoiceAction action = requested;
            if (!swarm && record.ExpireTick >= 0 && tick >= record.ExpireTick && record.Open)
            {
                action = RHAH_ChoiceAction.Timeout;
            }

            RHAH_ChoiceAction settled = RHAH_RequestRules.Settle(action, enabled, !record.Open, canDeliver);
            if (settled == RHAH_ChoiceAction.None)
            {
                return RHAH_ChoiceAction.None;
            }

            if (swarm && (settled == RHAH_ChoiceAction.Reject || settled == RHAH_ChoiceAction.Attack ||
                settled == RHAH_ChoiceAction.Capture || settled == RHAH_ChoiceAction.Enslave))
            {
                if (!RHAH_MouseSwarm.Provoke(Present(record))) return RHAH_ChoiceAction.None;
                record.Settled = settled;
                return settled;
            }
            if (RHAH_RequestRules.Captures(settled) || RHAH_RequestRules.Enslaves(settled))
            {
                List<Verse.Pawn> present = Present(record);
                int converted = RHAH_RequestRules.Captures(settled)
                    ? RHAH_VisitorBatch.Capture(present, record.MapId)
                    : RHAH_VisitorBatch.Enslave(present, record.MapId);
                if (converted == 0)
                {
                    return RHAH_ChoiceAction.None;
                }
            }

            if ((RHAH_RequestRules.Joins(settled) && !Release(record, RHAH_ReleaseReason.JoinedPlayerFaction)) ||
                (RHAH_RequestRules.Hires(settled) && !Stay(record, RHAH_StayKind.Hire)) ||
                (RHAH_RequestRules.Recruits(settled) && !Stay(record, RHAH_StayKind.Recruit)) ||
                (RHAH_RequestRules.Attacks(settled) && !RHAH_BatchAttitude.Attack(Present(record))) ||
                (RHAH_RequestRules.SendsToAlly(settled) && !SendToAlly(record, Present(record), allyFactionId)))
            {
                return RHAH_ChoiceAction.None;
            }

            record.Settled = settled;
            return settled;
        }

        internal static void Tick(GameComponent_RHAH_Game game, int tick)
        {
            if (game == null)
            {
                return;
            }


            List<RHAH_ChoiceRecord> records = Records(game);
            for (int i = 0; i < records.Count; i++)
            {
                RHAH_ChoiceRecord record = records[i];
                RHAH_AllyTransfer.Tick(record, tick);
                if (RHAH_MouseSwarm.IsSource(record.DisplayId)) continue;
                if (RHAH_ArmedDemand.IsRequest(record))
                {
                    if (!record.Open) continue;
                    if (!RHAH_ArmedDemand.Enabled)
                    {
                        RHAH_ArmedDemand.Resolve(record, RHAH_ChoiceAction.Ignore);
                    }
                    else if (record.Open && record.ExpireTick >= 0 && tick >= record.ExpireTick)
                    {
                        RHAH_ArmedDemand.Resolve(record, RHAH_ChoiceAction.Timeout);
                    }
                    continue;
                }
                if (!record.Open || record.ExpireTick < 0 || tick < record.ExpireTick)
                {
                    continue;
                }

                RHAH_ChoiceAction settled = TrySettle(game, record.Id, RHAH_ChoiceAction.Timeout, tick, true, false);
                if (settled == RHAH_ChoiceAction.Timeout)
                {
                    Leave(record);
                }
            }

        }
        internal static Map FindMap(int mapId)
        {
            if (Find.Maps == null) return null;
            for (int i = 0; i < Find.Maps.Count; i++)
                if (Find.Maps[i].uniqueID == mapId) return Find.Maps[i];
            return null;
        }

        internal static RHAH_ChoiceAction TrySettleRequest(GameComponent_RHAH_Game game, int choiceId,
            RHAH_ChoiceAction requested, int tick, bool enabled, Verse.Pawn outgoingChild = null, bool foodAlternative = false)
        {
            RHAH_ChoiceRecord record = FindRecord(game, choiceId);
            if (record == null || !record.Open) return RHAH_ChoiceAction.None;
            if (requested == RHAH_ChoiceAction.Feed &&
                !RHAH_RequestRules.ShowsFoodGive(record.DisplayId, record.Choice)) return RHAH_ChoiceAction.None;
            if (RHAH_MouseSwarm.IsSource(record.DisplayId))
            {
                RHAH_ChoiceAction swarmAction = TrySettle(game, choiceId, requested, tick, enabled, true);
                if (swarmAction != RHAH_ChoiceAction.None) Apply(record);
                return swarmAction;
            }
            if (RHAH_ArmedDemand.IsRequest(record))
            {
                if (!RHAH_ArmedDemand.Enabled)
                {
                    RHAH_ArmedDemand.Resolve(record, RHAH_ChoiceAction.Ignore);
                    return RHAH_ChoiceAction.Ignore;
                }
                if (requested == RHAH_ChoiceAction.Deliver && !RHAH_ArmedDemand.TryPay(record))
                    return RHAH_ChoiceAction.None;
                RHAH_ArmedDemand.Resolve(record, requested);
                return requested;
            }
            if (record.ExpireTick >= 0 && tick >= record.ExpireTick) requested = RHAH_ChoiceAction.Timeout;
            bool canDeliver = requested != RHAH_ChoiceAction.Deliver;
            Map map = FindMap(record.MapId);
            if (requested == RHAH_ChoiceAction.Deliver)
            {
                canDeliver = record.TransactionVersion == RHAH_RequestRules.TransactionVersion
                    ? RHAH_FoodTransaction.CanDeliver(record)
                    : RHAH_ChildExchange.AcceptsChild(record) || record.Kind == RHAH_RequestKind.Baby
                        ? RHAH_ChildExchange.CanDeliver(record, outgoingChild, foodAlternative)
                        : map != null && RHAH_RequestRules.CanDeliver(record.Kind, Stock(map, record.Kind), record.Amount, false);
            }
            RHAH_ChoiceAction settled = RHAH_RequestRules.Settle(requested, enabled, false, canDeliver);
            if (settled == RHAH_ChoiceAction.None) return settled;
            if (settled == RHAH_ChoiceAction.Deliver)
            {
                bool paid;
                if (record.TransactionVersion == RHAH_RequestRules.TransactionVersion)
                    paid = RHAH_FoodTransaction.TryExchange(record);
                else if (RHAH_ChildExchange.AcceptsChild(record) || record.Kind == RHAH_RequestKind.Baby)
                    paid = RHAH_ChildExchange.TryExchange(record, outgoingChild, foodAlternative);
                else
                    paid = RHAH_RequestRules.CreatesSite(settled, record.Site)
                        ? TryCreateSite(map, record.Site, () => TryConsume(map, record.Kind, record.Amount))
                        : TryConsume(map, record.Kind, record.Amount);
                if (!paid) return RHAH_ChoiceAction.None;
            }
            record.Settled = settled;
            Apply(record);
            if (settled == RHAH_ChoiceAction.Deliver && record.TransactionVersion == RHAH_RequestRules.TransactionVersion &&
                record.Site != RHAH_IntelSiteKind.None && Rand.Chance(0.25f)) TryCreateSite(map, record.Site);
            return settled;
        }

        internal static bool TryConsume(Map map, RHAH_RequestKind kind, int amount)
        {
            if (map == null || amount < 0 || kind == RHAH_RequestKind.None || kind == RHAH_RequestKind.Baby)
            {
                return false;
            }


            if (amount == 0)
            {
                return true;
            }

            if (kind == RHAH_RequestKind.Medicine)
            {
                return ConsumeMedicine(map, amount);
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(RHAH_RequestRules.ThingDefName(kind));
            return def != null && Consume(map.listerThings.ThingsOfDef(def), amount);
        }

        internal static int Stock(Map map, RHAH_RequestKind kind)
        {
            if (map == null || kind == RHAH_RequestKind.None || kind == RHAH_RequestKind.Baby)
            {
                return 0;
            }

            if (kind == RHAH_RequestKind.Medicine)
            {
                return CountMedicine(map);
            }

            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(RHAH_RequestRules.ThingDefName(kind));
            if (def == null)
            {
                return 0;
            }

            return CountSpawned(map.listerThings.ThingsOfDef(def));
        }

        internal static bool TryCreateSite(Map map, RHAH_IntelSiteKind kind, System.Func<bool> complete = null)
        {
            string partName = RHAH_RequestRules.SitePartDefName(kind);
            if (map == null || Find.World == null || string.IsNullOrEmpty(partName))
            {
                return false;
            }

            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail(partName);
            PlanetTile tile;
            if (part == null || !TileFinder.TryFindNewSiteTile(out tile, 5, 22))
            {
                return false;
            }

            Faction faction = kind == RHAH_IntelSiteKind.Treasure ? null : Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Undefined);
            Site site = SiteMaker.MakeSite(part, tile, faction, true, null, null);
            if (site == null || (complete != null && !complete()))
            {
                return false;
            }

            Find.WorldObjects.Add(site);
            Find.LetterStack.ReceiveLetter(
                "RHAH_Intel_Label".Translate(),
                "RHAH_Intel_Text".Translate(),
                LetterDefOf.PositiveEvent,
                site);
            return true;
        }

        internal static void Apply(RHAH_ChoiceRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (RHAH_MouseSwarm.IsSource(record.DisplayId) &&
                (record.Settled == RHAH_ChoiceAction.Reject || record.Settled == RHAH_ChoiceAction.Timeout)) return;
            HungerAndHavoc.EventMgr.RHAH_EventFollowMood.OnChoice(record);
            if (record.Settled == RHAH_ChoiceAction.Deliver &&
                (record.TransactionVersion == RHAH_RequestRules.TransactionVersion || record.ChildExchangeVersion > 0))
            {
                RHAH_BatchAttitude.OrderVisitorLeave(Present(record));
                NoteEnding(record);
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }
            if (RHAH_RequestRules.Joins(record.Settled))
            {
                NoteEnding(record);
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }

            if (RHAH_RequestRules.Hires(record.Settled))
            {
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }

            if (RHAH_RequestRules.Recruits(record.Settled))
            {
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }

            if (RHAH_RequestRules.Enslaves(record.Settled) || RHAH_RequestRules.Captures(record.Settled))
            {
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }

            if (RHAH_RequestRules.Attacks(record.Settled))
            {
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }

            if (RHAH_RequestRules.SendsToAlly(record.Settled))
            {
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }

            if (RHAH_RequestRules.WaitsForFood(record.Settled))
            {
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }


            if (RHAH_RequestRules.Leaves(record.Settled))
            {
                if (record.TransactionVersion == RHAH_RequestRules.TransactionVersion)
                    RHAH_BatchAttitude.OrderVisitorLeave(Present(record));
                else Leave(record);
                HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
                return;
            }

            NoteEnding(record);
            HungerAndHavoc.Storyteller.Suiyin.RHAH_EntrustCare.OnChoice(record);
        }

        internal static bool HasAllyDestination()
        {
            if (Find.FactionManager == null || Find.WorldObjects == null) return false;
            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++)
                if (AcceptsAlly(factions[i])) return true;
            return false;
        }

        internal static List<Faction> AllyDestinations()
        {
            List<Faction> result = new List<Faction>();
            if (Find.FactionManager == null || Find.WorldObjects == null) return result;
            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++)
                if (AcceptsAlly(factions[i])) result.Add(factions[i]);
            result.Sort((left, right) => left.loadID.CompareTo(right.loadID));
            return result;
        }

        static bool AcceptsAlly(Faction faction)
        {
            if (faction == null || faction.def == null || faction.IsPlayer)
            {
                return false;
            }

            return RHAH_RequestRules.IsAllyDestination(
                faction.IsPlayer,
                faction.Hidden,
                faction.defeated,
                faction.temporary,
                faction.def.humanlikeFaction,
                faction.HostileTo(Faction.OfPlayer),
                faction.PlayerRelationKind == FactionRelationKind.Ally,
                HasSettlement(faction),
                RHAH_AttitudeFactions.IsAttitudeFaction(faction));
        }

        static bool HasSettlement(Faction faction)
        {
            List<Settlement> settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                Settlement settlement = settlements[i];
                if (settlement != null && settlement.Faction == faction && !settlement.Destroyed)
                {
                    return true;
                }
            }

            return false;
        }

        static bool SendToAlly(RHAH_ChoiceRecord record, List<Verse.Pawn> pawns, int allyFactionId)
        {
            Faction faction = FindFaction(allyFactionId);
            if (record == null || Find.WorldObjects == null || !AcceptsAlly(faction))
            {
                return false;
            }

            record.AllyFactionId = faction.loadID;
            record.AllyPawnIds.Clear();
            List<Verse.Pawn> leaving = new List<Verse.Pawn>();
            for (int i = 0; i < pawns.Count; i++)
            {
                Verse.Pawn pawn = pawns[i];
                if (!RHAH_BatchAttitude.CanOrderLeave(pawn) || !RHAH_ApiHost.CanTransfer(pawn))
                {
                    continue;
                }

                record.AllyPawnIds.Add(pawn.thingIDNumber);
                leaving.Add(pawn);
            }

            if (leaving.Count == 0)
            {
                record.AllyFactionId = 0;
                return false;
            }

            RHAH_BatchAttitude.OrderVisitorLeave(leaving);
            return true;
        }

        internal static bool TryJoinAlly(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.Spawned || pawn.Dead)
            {
                return false;
            }

            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            if (game == null)
            {
                return false;
            }

            List<RHAH_ChoiceRecord> records = Records(game);
            for (int i = 0; i < records.Count; i++)
            {
                RHAH_ChoiceRecord record = records[i];
                if (record.AllyFactionId <= 0 || record.AllyPawnIds == null || !record.AllyPawnIds.Contains(pawn.thingIDNumber))
                {
                    continue;
                }

                Faction faction = FindFaction(record.AllyFactionId);
                record.AllyPawnIds.Remove(pawn.thingIDNumber);

                if (faction != null)
                {
                    pawn.SetFaction(faction);
                    RHAH_AllyTransfer.OnArrival(record, faction, pawn, Find.TickManager.TicksGame);
                }

                return faction != null;
            }

            return false;
        }

        internal static Faction FindFaction(int loadId)
        {
            if (Find.FactionManager == null || loadId <= 0)
            {
                return null;
            }

            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++)
            {
                if (factions[i] != null && factions[i].loadID == loadId)
                {
                    return factions[i];
                }
            }

            return null;
        }

        static bool Release(RHAH_ChoiceRecord record, RHAH_ReleaseReason reason)
        {
            List<Verse.Pawn> pawns = Present(record);
            bool released = false;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (RHAH_Api.Allows(pawns[i], RHAH_BehaviorGate.JoinColony) && RHAH_Api.ReleaseToColony(pawns[i], reason))
                {
                    HungerAndHavoc.Pawn.RHAH_VisitorRules.ApplyPlayerIdeo(
                        pawns[i],
                        Core.RHAH_Mod.Settings == null ? 100 : Core.RHAH_Mod.Settings.playerIdeoPercent,
                        Rand.Value);
                    pawns[i].SetFaction(Faction.OfPlayer);
                    RHAH_VisitorMood.NotifySheltered(pawns[i]);
                    released = true;
                }
            }

            return released;
        }
        static bool Stay(RHAH_ChoiceRecord record, RHAH_StayKind kind)
        {
            List<Verse.Pawn> pawns = Present(record);
            bool stayed = false;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (RHAH_VisitorStay.Begin(pawns[i], kind))
                {
                    RHAH_VisitorStay.ClearTrade(pawns[i], RHAH_ReleaseReason.Recruited, true);
                    stayed = true;
                }
            }

            return stayed;
        }


        static void Leave(RHAH_ChoiceRecord record)
        {
            List<Verse.Pawn> pawns = Present(record);
            if (pawns.Count == 0)
            {
                return;
            }

            RHAH_BatchAttitude.TryShift(pawns[0], RHAH_BatchReaction.Reject);
        }
        static void NoteEnding(RHAH_ChoiceRecord record)
        {
            if (!RHAH_RequestRules.PromisesAid(record.Choice, record.Settled))
            {
                return;
            }

            Current.Game?.GetComponent<Narrative.NarrativeState>()?.Book?.MarkJournalDelivered(record.BatchId);
            RHAH_SuiyinTrust.Note(Pawns(record).Count, RHAH_SuiyinTrust.Value(RHAH_SuiyinTrust.Deliver, RHAH_Mod.Settings == null ? RHAH_SuiyinTrust.Deliver : RHAH_Mod.Settings.trustDeliver));
        }

        internal static List<Verse.Pawn> Pawns(RHAH_ChoiceRecord record)
        {
            List<Verse.Pawn> result = new List<Verse.Pawn>();
            if (record.PawnLoadIds == null)
            {
                return result;
            }

            for (int i = 0; i < record.PawnLoadIds.Count; i++)
            {
                Verse.Pawn pawn = FindPawn(record.PawnLoadIds[i]);
                if (pawn != null && !pawn.Dead && !pawn.Destroyed)
                {
                    result.Add(pawn);
                }
            }

            return result;
        }

        internal static List<Verse.Pawn> Present(RHAH_ChoiceRecord record)
        {
            List<Verse.Pawn> all = Pawns(record);
            List<Verse.Pawn> present = new List<Verse.Pawn>();
            for (int i = 0; i < all.Count; i++)
            {
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(all[i]);
                if (all[i].MapHeld != null && all[i].MapHeld.uniqueID == record.MapId &&
                    comp != null && comp.State.spawnBatchId == record.BatchId && RHAH_BatchAttitude.CanOrderLeave(all[i]))
                {
                    present.Add(all[i]);
                }
            }

            return present;
        }


        static Verse.Pawn FindPawn(int loadId)
        {
            if (loadId <= 0)
            {
                return null;
            }

            List<Verse.Pawn> pawns = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] != null && pawns[i].thingIDNumber == loadId)
                {
                    return pawns[i];
                }
            }

            return null;
        }

        static bool ConsumeMedicine(Map map, int amount)
        {
            List<Thing> medicines = new List<Thing>();
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && thing.Spawned && thing.def != null && thing.def.IsMedicine && thing.stackCount > 0)
                {
                    medicines.Add(thing);
                }
            }

            return Consume(medicines, amount);
        }

        static int CountMedicine(Map map)
        {
            List<Thing> medicines = new List<Thing>();
            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && thing.Spawned && thing.def != null && thing.def.IsMedicine)
                {
                    medicines.Add(thing);
                }
            }

            return CountSpawned(medicines);
        }

        internal static int CountSpawned(List<Thing> things)
        {
            int total = 0;
            if (things == null)
            {
                return total;
            }

            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] != null && things[i].Spawned)
                {
                    total += things[i].stackCount;
                }
            }

            return total;
        }

        static bool Consume(List<Thing> things, int amount)
        {
            if (CountSpawned(things) < amount)
            {
                return false;
            }

            int remaining = amount;
            for (int i = things.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing thing = things[i];
                if (thing == null || !thing.Spawned || thing.stackCount <= 0)
                {
                    continue;
                }

                int take = thing.stackCount < remaining ? thing.stackCount : remaining;
                if (take >= thing.stackCount)
                {
                    remaining -= thing.stackCount;
                    thing.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    thing.SplitOff(take).Destroy(DestroyMode.Vanish);
                    remaining -= take;
                }
            }

            return remaining == 0;
        }

        static RHAH_ChoiceRecord FindRecord(GameComponent_RHAH_Game game, int choiceId)
        {
            List<RHAH_ChoiceRecord> records = Records(game);
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Id == choiceId)
                {
                    return records[i];
                }
            }

            return null;
        }

        static List<RHAH_ChoiceRecord> Records(GameComponent_RHAH_Game game)
        {
            return (List<RHAH_ChoiceRecord>)game.OpenChoices;
        }

        static int NextId(GameComponent_RHAH_Game game)
        {
            int id = game.NextChoiceId;
            game.NextChoiceId = id + 1;
            return id;
        }
    }
}
