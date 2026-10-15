using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_FoodTransaction
    {
        static readonly string[] RewardGoods = { "Silver", "ComponentIndustrial", "ComponentSpacer", "MedicineIndustrial", "MedicineHerbal", "Gold", "Jade", "Plasteel" };

        internal static bool IsUsableFood(Thing thing, List<Verse.Pawn> visitors)
        {
            if (thing == null || !thing.Spawned || !thing.IngestibleNow || thing.def.ingestible == null ||
                !thing.def.ingestible.HumanEdible || !thing.def.IsNutritionGivingIngestible || thing.def.IsDrug ||
                thing.IsForbidden(Faction.OfPlayer) || !thing.IsInValidStorage()) return false;
            Thing storage = StoreUtility.CurrentHaulDestinationOf(thing) as Thing;
            if (storage != null && storage.Faction != Faction.OfPlayer) return false;
            for (int i = 0; i < visitors.Count; i++)
            {
                Verse.Pawn pawn = visitors[i];
                if (pawn.RaceProps == null || !pawn.RaceProps.CanEverEat(thing.def)) return false;
            }
            return true;
        }

        internal static void Prepare(RHAH_ChoiceRecord record, Map map, List<Verse.Pawn> visitors)
        {
            record.TransactionVersion = RHAH_RequestRules.TransactionVersion;
            Dictionary<ThingDef, int> counts = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, Thing> examples = new Dictionary<ThingDef, Thing>();
            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!IsUsableFood(thing, visitors)) continue;
                int count;
                counts.TryGetValue(thing.def, out count);
                counts[thing.def] = count + thing.stackCount;
                examples[thing.def] = thing;
            }
            ThingDef chosen = RHAH_RequestRules.LargestFood(counts);
            if (chosen == null || visitors.Count == 0) return;
            record.QuotedFoodDefName = chosen.defName;
            Thing example = examples[chosen];
            record.QuotedFoodCount = RHAH_RequestRules.RequiredFoodUnits(visitors.Count, example.GetStatValue(StatDefOf.Nutrition));
            record.Amount = record.QuotedFoodCount;
            float multiplier = Rand.Range(0.8f, 1.2f);
            double budget = record.QuotedFoodCount * example.MarketValue * multiplier;
            List<ThingDef> goods = new List<ThingDef>();
            for (int i = 0; i < RewardGoods.Length; i++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(RewardGoods[i]);
                if (def != null && def.GetStatValueAbstract(StatDefOf.MarketValue) > 0f &&
                    def.GetStatValueAbstract(StatDefOf.MarketValue) <= budget) goods.Add(def);
            }
            if (goods.Count == 0) return;
            ThingDef rewardDef = goods[Rand.Range(0, goods.Count)];
            int remaining = RHAH_RequestRules.RewardUnits(record.QuotedFoodCount, example.MarketValue,
                rewardDef.GetStatValueAbstract(StatDefOf.MarketValue), multiplier);
            int total = remaining;
            Verse.Pawn carrier = null;
            for (int i = 0; i < visitors.Count; i++)
            {
                if (visitors[i].inventory != null) { carrier = visitors[i]; break; }
            }
            if (carrier == null) return;
            List<Thing> made = new List<Thing>();
            while (remaining > 0)
            {
                Thing reward = ThingMaker.MakeThing(rewardDef);
                reward.stackCount = Math.Min(remaining, rewardDef.stackLimit);
                if (!carrier.inventory.innerContainer.TryAdd(reward, false))
                {
                    reward.Destroy();
                    for (int i = 0; i < made.Count; i++) made[i].Destroy();
                    return;
                }
                remaining -= reward.stackCount;
                made.Add(reward);
            }
            record.RewardDefNames.Add(rewardDef.defName);
            record.RewardCounts.Add(total);
            for (int i = 0; i < made.Count; i++) record.RewardThingIds.Add(made[i].thingIDNumber);
        }

        internal static string Details(RHAH_ChoiceRecord record)
        {
            ThingDef food = DefDatabase<ThingDef>.GetNamedSilentFail(record.QuotedFoodDefName);
            string reward = string.Empty;
            for (int i = 0; i < record.RewardDefNames.Count && i < record.RewardCounts.Count; i++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(record.RewardDefNames[i]);
                if (def != null) reward += (reward.Length == 0 ? string.Empty : ", ") + record.RewardCounts[i] + " × " + def.LabelCap;
            }
            return food == null || reward.Length == 0 ? "RHAH_Transaction_NoQuote".Translate().ToString() :
                "RHAH_Transaction_Quote".Translate(record.QuotedFoodCount, food.LabelCap, reward).ToString();
        }

        sealed class Packet
        {
            internal Thing Thing;
            internal ThingOwner Owner;
            internal IntVec3 Cell;
            internal Thing Source;
        }

        static bool Plan(RHAH_ChoiceRecord record, out Map map, out Verse.Pawn carrier,
            out List<Thing> food, out List<Packet> rewards, out List<IntVec3> cells)
        {
            map = RHAH_ChoiceRuntime.FindMap(record.MapId);
            carrier = null;
            food = new List<Thing>();
            rewards = new List<Packet>();
            cells = new List<IntVec3>();
            if (!record.Open || map == null || record.QuotedFoodCount <= 0 || record.RewardThingIds.Count == 0) return false;
            List<Verse.Pawn> visitors = RHAH_ChoiceRuntime.Present(record);
            for (int i = 0; i < visitors.Count; i++)
            {
                if (visitors[i].inventory != null && !visitors[i].Downed && carrier == null) carrier = visitors[i];
                ThingOwner owner = visitors[i].inventory?.innerContainer;
                if (owner == null) continue;
                for (int j = 0; j < owner.Count; j++)
                    if (record.RewardThingIds.Contains(owner[j].thingIDNumber))
                        rewards.Add(new Packet { Thing = owner[j], Owner = owner });
            }
            if (carrier == null || rewards.Count != record.RewardThingIds.Count) return false;
            for (int i = 0; i < record.RewardDefNames.Count; i++)
            {
                int total = 0;
                for (int j = 0; j < rewards.Count; j++)
                    if (rewards[j].Thing.def.defName == record.RewardDefNames[i]) total += rewards[j].Thing.stackCount;
                if (i >= record.RewardCounts.Count || total != record.RewardCounts[i]) return false;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(record.QuotedFoodDefName);
            if (def == null) return false;
            int stock = 0;
            List<Thing> available = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < available.Count; i++)
            {
                Thing thing = available[i];
                if (!IsUsableFood(thing, visitors) || !carrier.CanReach(thing, PathEndMode.Touch, Danger.Deadly) ||
                    !carrier.inventory.innerContainer.CanAcceptAnyOf(thing, false)) continue;
                food.Add(thing);
                stock += thing.stackCount;
            }
            if (stock < record.QuotedFoodCount) return false;
            IReadOnlyList<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(carrier.Position, 30f, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetThingList(map).Count != 0 ||
                    !carrier.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) continue;
                bool reachable = false;
                for (int i = 0; i < colonists.Count; i++)
                    if (colonists[i].CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) { reachable = true; break; }
                if (!reachable) continue;
                cells.Add(cell);
                if (cells.Count == rewards.Count) return true;
            }
            return false;
        }

        internal static bool CanDeliver(RHAH_ChoiceRecord record)
        {
            Map map; Verse.Pawn carrier; List<Thing> food; List<Packet> rewards; List<IntVec3> cells;
            return Plan(record, out map, out carrier, out food, out rewards, out cells);
        }

        internal static bool TryExchange(RHAH_ChoiceRecord record)
        {
            Map map; Verse.Pawn carrier; List<Thing> food; List<Packet> rewards; List<IntVec3> cells;
            if (!Plan(record, out map, out carrier, out food, out rewards, out cells)) return false;
            List<Packet> payment = new List<Packet>();
            int remaining = record.QuotedFoodCount;
            try
            {
                for (int i = 0; i < food.Count && remaining > 0; i++)
                {
                    Thing source = food[i];
                    int count = Math.Min(remaining, source.stackCount);
                    IntVec3 origin = source.Position;
                    Thing split = source.SplitOff(count);
                    if (split.Spawned) split.DeSpawn();
                    Packet packet = new Packet { Thing = split, Cell = origin, Source = split == source ? null : source };
                    payment.Add(packet);
                    if (!carrier.inventory.innerContainer.TryAdd(split, false)) throw new InvalidOperationException("Food inventory refused payment");
                    packet.Owner = carrier.inventory.innerContainer;
                    remaining -= count;
                }
                for (int i = 0; i < rewards.Count; i++)
                {
                    rewards[i].Owner.Remove(rewards[i].Thing);
                    if (GenSpawn.Spawn(rewards[i].Thing, cells[i], map) == null)
                        throw new InvalidOperationException("Reward placement refused");
                    rewards[i].Thing.SetForbidden(false, false);
                }
                return true;
            }
            catch (Exception exception)
            {
                for (int i = 0; i < payment.Count; i++)
                {
                    Packet packet = payment[i];
                    packet.Thing.holdingOwner?.Remove(packet.Thing);
                    if (packet.Source == null || !packet.Source.Spawned || !packet.Source.TryAbsorbStack(packet.Thing, false))
                        GenSpawn.Spawn(packet.Thing, packet.Cell, map);
                }
                for (int i = 0; i < rewards.Count; i++)
                {
                    Thing thing = rewards[i].Thing;
                    if (thing.Spawned) thing.DeSpawn();
                    if (thing.holdingOwner != rewards[i].Owner && !rewards[i].Owner.TryAdd(thing, false))
                        Log.Error("[RHAH] Exchange rollback could not restore carried reward " + thing);
                }
                Log.Warning("[RHAH] Food exchange rolled back: " + exception.Message);
                return false;
            }
        }
    }
}
