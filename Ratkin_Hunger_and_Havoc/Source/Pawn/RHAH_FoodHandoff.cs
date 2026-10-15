using System;
using Verse.AI.Group;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_FoodHandoff
    {
        internal static int Begin(List<Verse.Pawn> pawns)
        {
            if (pawns == null || pawns.Count == 0)
            {
                return 0;
            }

            int each = RHAH_RequestRules.FoodRequestCount(1);
            int started = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (Refusal(pawns[i]) == null && Start(pawns[i], each))
                {
                    started++;
                }
            }

            return started;
        }

        internal static bool Start(Verse.Pawn receiver, int count)
        {
            if (count <= 0 || Refusal(receiver) != null)
            {
                return false;
            }

            Lord lord = receiver.GetLord();
            LordJob_RHAH_Visitor job = lord == null ? null : lord.LordJob as LordJob_RHAH_Visitor;
            if (job == null)
            {
                return false;
            }

            if (job.BeginFoodWait(receiver, count))
            {
                ArmWait(receiver);
            }

            if (CanEnterFoodWait(lord.CurLordToil))
            {
                lord.ReceiveMemo("RHAH_WaitFood");
            }

            if (!(lord.CurLordToil is LordToil_RHAH_WaitFood))
            {
                return false;
            }

            if (job.ArmedChoiceId == 0) RHAH_BatchAttitude.AcceptFood(receiver);
            return true;
        }

        internal static bool CanEnterFoodWait(LordToil toil)
        {
            return toil is LordToil_RHAH_VisitorSeek || toil is LordToil_RHAH_VisitorTravel;
        }


        internal static string Refusal(Verse.Pawn pawn)
        {
            Lord lord = pawn == null ? null : pawn.GetLord();
            if (!RHAH_Api.IsVisitor(pawn) || pawn.Dead || !pawn.Spawned || pawn.Downed ||
                !(lord != null && lord.LordJob is LordJob_RHAH_Visitor) ||
                (!CanEnterFoodWait(lord.CurLordToil) && !(lord.CurLordToil is LordToil_RHAH_WaitFood)) ||
                RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Leaving)
            {
                return "RHAH_Choice_FeedNoVisitor";
            }

            if (RHAH_Api.Get(pawn)?.SourceIncidentDisplayId == "I-053" || RHAH_MouseSwarm.IsProvoked(pawn))
            {
                return "RHAH_Choice_FeedBlocked";
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.FeedFromRelief))
            {
                return "RHAH_Choice_FeedBlocked";
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot != null && snapshot.HasBeenFed)
            {
                return "RHAH_Choice_FeedNotHungry";
            }

            return null;
        }

        static void ArmWait(Verse.Pawn receiver)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(receiver);
            if (comp == null || Find.TickManager == null)
            {
                return;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            bool wait = settings == null || settings.waitWhenNoFood;
            float days = settings == null ? RHAH_VisitorRules.DefaultNoFoodWaitDays : settings.noFoodWaitDays;
            comp.SetFoodWait(RHAH_VisitorRules.NextWaitTick(
                Find.TickManager.TicksGame,
                -1,
                false,
                wait,
                days));
        }

        internal static Thing FindFood(Verse.Pawn giver)
        {
            List<Thing> foods = new List<Thing>();
            CollectFood(giver, foods);
            return foods.Count == 0 ? null : foods[0];
        }

        internal static void CollectFood(Verse.Pawn giver, List<Thing> result)
        {
            if (result == null || giver == null || giver.Map == null || giver.Map.listerThings == null)
            {
                return;
            }

            List<Thing> things = giver.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
            if (things == null)
            {
                return;
            }

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (Accepts(giver, thing))
                {
                    result.Add(thing);
                }
            }

            result.Sort((left, right) =>
            {
                float leftDistance = left == null ? float.MaxValue : (left.PositionHeld - giver.Position).LengthHorizontalSquared;
                float rightDistance = right == null ? float.MaxValue : (right.PositionHeld - giver.Position).LengthHorizontalSquared;
                int distance = leftDistance.CompareTo(rightDistance);
                if (distance != 0)
                {
                    return distance;
                }

                string leftName = left == null || left.def == null ? null : left.def.defName;
                string rightName = right == null || right.def == null ? null : right.def.defName;
                return string.Compare(leftName, rightName, StringComparison.Ordinal);
            });
        }

        internal static int Received(Verse.Pawn receiver, ThingDef food)
        {
            if (receiver == null || receiver.inventory == null || food == null)
            {
                return 0;
            }

            int count = 0;
            ThingOwner<Thing> container = receiver.inventory.innerContainer;
            for (int i = 0; i < container.Count; i++)
            {
                if (container[i] != null && container[i].def == food)
                {
                    count += container[i].stackCount;
                }
            }

            return count;
        }

        internal static int BeingHauledTo(Verse.Pawn receiver)
        {
            if (receiver == null || receiver.Map == null || receiver.Map.mapPawns == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<Verse.Pawn> spawned = receiver.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                Verse.Pawn hauler = spawned[i];
                JobDriver_GiveToPawn driver = hauler == null || hauler.jobs == null
                    ? null
                    : hauler.jobs.curDriver as JobDriver_GiveToPawn;
                if (driver != null && driver.job.GetTarget(TargetIndex.B).Pawn == receiver)
                {
                    count += driver.CountBeingHauled;
                }
            }

            return count;
        }

        internal static bool TryCountLeft(Verse.Pawn receiver, out int left)
        {
            left = 0;
            LordJob_RHAH_Visitor job = receiver == null || receiver.GetLord() == null
                ? null
                : receiver.GetLord().LordJob as LordJob_RHAH_Visitor;
            if (job == null || !job.ListedForFood(receiver))
            {
                return false;
            }

            left = job.FoodLeftToCollect(receiver);
            return true;
        }

        static bool Accepts(Verse.Pawn giver, Thing thing)
        {
            return thing != null &&
                thing.Spawned &&
                !thing.IsForbidden(giver) &&
                giver.CanReserve(thing) &&
                RHAH_ReliefFood.GiveFoodAllowed(thing.def) &&
                thing.def.IsIngestible &&
                thing.def.ingestible.preferability >= FoodPreferability.MealAwful;
        }
    }
}
