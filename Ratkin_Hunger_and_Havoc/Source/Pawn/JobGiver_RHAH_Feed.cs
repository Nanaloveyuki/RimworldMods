using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_Feed : ThinkNode_JobGiver
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

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.FeedFromRelief))
            {
                return null;
            }

            if (pawn.Map == null || JobDefOf.Ingest == null)
            {
                return null;
            }

            if (RHAH_VisitorRules.BlocksReliefFeed(
                pawn.Downed,
                ModsConfig.IsActive("cyanobot.toddlers"),
                Compat.RHAH_ChildMovement.ManipulationStage(pawn)))
            {
                return null;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot != null && snapshot.HasBeenFed)
            {
                return null;
            }

            Need_Food need = pawn.needs != null ? pawn.needs.food : null;
            if (need == null)
            {
                return null;
            }

            Thing carried = FoodInInventory(pawn);
            if (carried != null)
            {
                return RHAH_ReliefFood.MakeJob(pawn, carried);
            }

            MapComponent_RHAH_Map mapState = pawn.Map.GetComponent<MapComponent_RHAH_Map>();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (mapState != null && !mapState.FoodSearchReady(pawn.thingIDNumber, now))
            {
                return null;
            }

            bool insideOnly = RHAH_ReliefFood.ReliefRulesApply && !RHAH_ReliefFood.MayEatOutside(pawn);
            if (insideOnly && RHAH_ReliefArea.IsEmpty(pawn.Map))
            {
                RememberMiss(mapState, pawn, now);
                return null;
            }

            Thing food = FindFood(pawn, true);
            if (food == null && !insideOnly)
            {
                food = FindFood(pawn, false);
            }

            if (food == null)
            {
                RememberMiss(mapState, pawn, now);
                return null;
            }

            Identity.CompRHAH_Pawn fed = Identity.CompRHAH_Pawn.TryGet(pawn);
            if (fed != null)
            {
                fed.SetFoodWait(-1);
            }

            return RHAH_ReliefFood.MakeJob(pawn, food);
        }

        static void RememberMiss(MapComponent_RHAH_Map mapState, Verse.Pawn pawn, int now)
        {
            if (mapState == null || pawn == null)
            {
                return;
            }

            int wait = RHAH_ReliefFood.RetryBaseTicks + (pawn.thingIDNumber % 60);
            mapState.SetFoodSearchTick(pawn.thingIDNumber, now + wait);
        }

        static Thing FoodInInventory(Verse.Pawn pawn)
        {
            if (pawn.inventory == null || pawn.inventory.innerContainer == null)
            {
                return null;
            }

            ThingOwner<Thing> container = pawn.inventory.innerContainer;
            for (int i = 0; i < container.Count; i++)
            {
                Thing thing = container[i];
                if (RHAH_ReliefFood.Reject(pawn, thing, false) == RHAH_FoodReject.None)
                {
                    return thing;
                }
            }

            return null;
        }

        static Thing FindFood(Verse.Pawn pawn, bool insideZone)
        {
            if (insideZone && RHAH_ReliefArea.IsEmpty(pawn.Map))
            {
                return null;
            }

            Thing best = null;
            float bestScore = float.NegativeInfinity;
            MapComponent_RHAH_Map mapState = pawn.Map.GetComponent<MapComponent_RHAH_Map>();
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            List<Thing> foods;
            List<Thing> plants;
            if (mapState != null)
            {
                mapState.FoodLists(now, out foods, out plants);
            }
            else
            {
                foods = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree);
                plants = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HarvestablePlant);
            }

            Consider(pawn, foods, insideZone, ref best, ref bestScore);
            Consider(pawn, plants, insideZone, ref best, ref bestScore);
            return best;
        }

        const int PathChecks = 3;

        static void Consider(
            Verse.Pawn pawn,
            List<Thing> things,
            bool insideZone,
            ref Thing best,
            ref float bestScore)
        {
            if (things == null || things.Count == 0)
            {
                return;
            }

            float bonus = RHAH_Mod.Settings == null ? RHAH_VisitorRules.DefaultReliefScoreBonus : RHAH_Mod.Settings.reliefFoodScoreBonus;
            int keep = PathChecks < things.Count ? PathChecks : things.Count;
            Thing[] picked = new Thing[keep];
            float[] scores = new float[keep];
            int count = 0;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (RHAH_ReliefFood.Reject(pawn, thing, insideZone, false, false) != RHAH_FoodReject.None)
                {
                    continue;
                }

                float distance = (thing.PositionHeld - pawn.Position).LengthHorizontal;
                bool inRelief = RHAH_ReliefArea.Contains(pawn.Map, thing.PositionHeld);
                float score = RHAH_VisitorRules.Score(-distance, FoodFit(thing), inRelief, bonus);
                Insert(picked, scores, ref count, keep, thing, score);
            }

            for (int i = 0; i < count; i++)
            {
                if (RHAH_ReliefFood.Reject(pawn, picked[i], insideZone) != RHAH_FoodReject.None)
                {
                    continue;
                }

                if (scores[i] > bestScore)
                {
                    bestScore = scores[i];
                    best = picked[i];
                }
            }
        }

        static void Insert(Thing[] picked, float[] scores, ref int count, int keep, Thing thing, float score)
        {
            int index = count < keep ? count : keep - 1;
            if (count == keep && score <= scores[index])
            {
                return;
            }

            while (index > 0 && score > scores[index - 1])
            {
                picked[index] = picked[index - 1];
                scores[index] = scores[index - 1];
                index--;
            }

            picked[index] = thing;
            scores[index] = score;
            if (count < keep)
            {
                count++;
            }
        }

        static float FoodFit(Thing thing)
        {
            ThingDef eaten = RHAH_ReliefFood.EatenDef(thing);
            if (eaten == null || eaten.ingestible == null)
            {
                return 0f;
            }

            return (float)eaten.ingestible.preferability;
        }
    }
}
