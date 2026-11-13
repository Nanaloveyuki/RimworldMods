using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_RoyalSilver
    {
        internal static bool StoredByPlayer(Thing thing)
        {
            if (thing == null || thing.def != ThingDefOf.Silver || !thing.Spawned || thing.stackCount <= 0 ||
                !thing.IsInValidStorage()) return false;
            Thing storage = StoreUtility.CurrentHaulDestinationOf(thing) as Thing;
            return storage == null || storage.Faction == Faction.OfPlayer;
        }

        internal static int Stored(Map map)
        {
            if (map?.listerThings == null) return 0;
            List<Thing> silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            long count = 0;
            for (int i = 0; i < silver.Count; i++)
                if (StoredByPlayer(silver[i])) count += silver[i].stackCount;
            return (int)Math.Min(int.MaxValue, count);
        }

        static bool Plan(Map map, List<Verse.Pawn> visitors, out List<Thing> stacks, out List<int> counts, out List<Verse.Pawn> receivers)
        {
            stacks = new List<Thing>();
            counts = new List<int>();
            receivers = new List<Verse.Pawn>();
            if (map == null || visitors.Count == 0) return false;
            IReadOnlyList<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            List<Thing> silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            int remaining = RHAH_SuspiciousRoyal.Price;
            for (int i = 0; i < silver.Count; i++)
            {
                Thing stack = silver[i];
                if (!StoredByPlayer(stack) || stack.IsForbidden(Faction.OfPlayer)) continue;
                for (int j = 0; j < visitors.Count; j++)
                {
                    Verse.Pawn visitor = visitors[j];
                    if (visitor.Downed || visitor.inventory == null ||
                        !visitor.inventory.innerContainer.CanAcceptAnyOf(stack, false) ||
                        !RHAH_FoodTransaction.CanDeliverFood(stack, visitor, colonists)) continue;
                    int count = Math.Min(remaining, stack.stackCount);
                    stacks.Add(stack);
                    counts.Add(count);
                    receivers.Add(visitor);
                    remaining -= count;
                    break;
                }
                if (remaining == 0) return true;
            }
            return false;
        }

        internal static bool CanPay(Map map, List<Verse.Pawn> visitors) => Plan(map, visitors, out _, out _, out _);

        internal static bool TryPay(Map map, List<Verse.Pawn> visitors)
        {
            if (!Plan(map, visitors, out List<Thing> stacks, out List<int> counts, out List<Verse.Pawn> receivers)) return false;
            List<Thing> payments = new List<Thing>();
            List<IntVec3> origins = new List<IntVec3>();
            try
            {
                for (int i = 0; i < stacks.Count; i++)
                {
                    origins.Add(stacks[i].Position);
                    Thing split = stacks[i].SplitOff(counts[i]);
                    payments.Add(split);
                    if (split.Spawned) split.DeSpawn();
                    if (!receivers[i].inventory.innerContainer.TryAdd(split, false))
                        throw new InvalidOperationException("Royal visitor refused the silver payment");
                }
                return true;
            }
            catch (Exception exception)
            {
                for (int i = 0; i < payments.Count; i++)
                {
                    Thing payment = payments[i];
                    payment.holdingOwner?.Remove(payment);
                    if (stacks[i] != payment && stacks[i].Spawned && stacks[i].TryAbsorbStack(payment, false)) continue;
                    if (!payment.Spawned && !GenPlace.TryPlaceThing(payment, origins[i], map, ThingPlaceMode.Near))
                        Log.Error("[RHAH] Could not restore royal silver payment at " + origins[i]);
                }
                Log.Warning("[RHAH] Royal silver payment rolled back: " + exception.Message);
                return false;
            }
        }
    }
}
