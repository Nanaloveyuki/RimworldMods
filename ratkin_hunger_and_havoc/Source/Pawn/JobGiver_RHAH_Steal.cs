using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_Steal : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        // 供无 Lord 的总 JobGiver 调度
        internal static Job TryCreate(Verse.Pawn pawn)
        {
            if (!RHAH_Api.IsVisitor(pawn))
            {
                return null;
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Steal))
            {
                return null;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot != null && snapshot.HasBeenFed)
            {
                return null;
            }

            if (pawn.Map == null || pawn.Downed || JobDefOf.Steal == null)
            {
                return null;
            }

            if (GenAI.InDangerousCombat(pawn))
            {
                return null;
            }

            MapComponent_RHAH_Map mapState = pawn.Map.GetComponent<MapComponent_RHAH_Map>();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (mapState != null && !mapState.FoodSearchReady(pawn.thingIDNumber, now))
            {
                return null;
            }

            IntVec3 exit;
            if (!RCellFinder.TryFindBestExitSpot(pawn, out exit))
            {
                RememberMiss(mapState, pawn, now);
                return null;
            }

            Thing item;
            if (!StealAIUtility.TryFindBestItemToSteal(pawn.Position, pawn.Map, 12f, out item, pawn, null) ||
                item == null)
            {
                Job taken = TryTakeFoodFromInventory(pawn);
                if (taken == null)
                {
                    RememberMiss(mapState, pawn, now);
                }

                return taken;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Steal, item, exit);
            float volume = item.def.VolumePerUnit;
            int carry = volume > 0f
                ? (int)(pawn.GetStatValue(StatDefOf.CarryingCapacity) / volume)
                : item.stackCount;
            job.count = Mathf.Max(1, Mathf.Min(item.stackCount, carry));
            return job;
        }

        const int PathChecks = 3;

        static void RememberMiss(MapComponent_RHAH_Map mapState, Verse.Pawn pawn, int now)
        {
            if (mapState == null || pawn == null)
            {
                return;
            }

            mapState.SetFoodSearchTick(pawn.thingIDNumber, now + RHAH_ReliefFood.RetryBaseTicks);
        }

        static Job TryTakeFoodFromInventory(Verse.Pawn pawn)
        {
            if (JobDefOf.TakeFromOtherInventory == null)
            {
                return null;
            }

            Verse.Pawn[] picked = new Verse.Pawn[PathChecks];
            float[] scores = new float[PathChecks];
            int count = 0;
            foreach (Verse.Pawn colonist in pawn.Map.mapPawns.FreeColonistsSpawned)
            {
                if (!CarriesFood(pawn, colonist))
                {
                    continue;
                }

                Insert(picked, scores, ref count, colonist, colonist.Position.DistanceToSquared(pawn.Position));
            }

            for (int i = 0; i < count; i++)
            {
                Verse.Pawn colonist = picked[i];
                if (!pawn.CanReach(colonist, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                Thing food = FirstFood(pawn, colonist.inventory.innerContainer);
                if (food == null)
                {
                    continue;
                }

                Job job = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, food, colonist);
                job.count = FoodUtility.WillIngestStackCountOf(
                    pawn,
                    food.def,
                    FoodUtility.NutritionForEater(pawn, food));
                return job;
            }

            return null;
        }

        static void Insert(Verse.Pawn[] picked, float[] scores, ref int count, Verse.Pawn colonist, float distance)
        {
            int index = count < PathChecks ? count : PathChecks - 1;
            if (count == PathChecks && distance >= scores[index])
            {
                return;
            }

            while (index > 0 && distance < scores[index - 1])
            {
                picked[index] = picked[index - 1];
                scores[index] = scores[index - 1];
                index--;
            }

            picked[index] = colonist;
            scores[index] = distance;
            if (count < PathChecks)
            {
                count++;
            }
        }

        static bool CarriesFood(Verse.Pawn pawn, Verse.Pawn colonist)
        {
            return colonist != null &&
                colonist != pawn &&
                colonist.inventory != null &&
                FirstFood(pawn, colonist.inventory.innerContainer) != null;
        }

        static Thing FirstFood(Verse.Pawn pawn, ThingOwner inner)
        {
            if (inner == null)
            {
                return null;
            }

            for (int i = 0; i < inner.Count; i++)
            {
                Thing thing = inner[i];
                if (thing != null &&
                    thing.IngestibleNow &&
                    (pawn.RaceProps == null || pawn.RaceProps.CanEverEat(thing)))
                {
                    return thing;
                }
            }

            return null;
        }
    }
}
