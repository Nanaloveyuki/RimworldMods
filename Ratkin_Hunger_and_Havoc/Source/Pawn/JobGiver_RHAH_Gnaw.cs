using System;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_Gnaw : ThinkNode_JobGiver
    {
        const float SearchRadius = 40f;
        internal const float StarvationLevel = 0.05f;


        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return null;
        }

        // 饥饿觅食失败或两次巴掌后的自救才发啃食
        internal static Job TryCreate(Verse.Pawn pawn, bool needFood)
        {
            if (!RHAH_Api.IsVisitor(pawn))
            {
                return null;
            }

            if (!RHAH_VisitorRules.AllowsModBehavior(RHAH_VisitorStay.Kind(pawn)))
            {
                return null;
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Gnaw))
            {
                return null;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot != null && snapshot.HasBeenFed)
            {
                return null;
            }

            bool busy = pawn.jobs != null && pawn.jobs.curJob != null;
            bool downed = pawn != null && pawn.Downed;
            Need_Food food = pawn.needs != null ? pawn.needs.food : null;
            float level = food == null ? 1f : food.CurLevelPercentage;
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return null;
            }
            Job current = pawn.jobs?.curJob;
            if (comp.State.PrefersGnaw && current != null && !current.playerForced &&
                (current.def == JobDefOf.Wait || current.def == JobDefOf.GotoWander))
            {
                busy = false;
            }
            float threshold = comp != null && comp.State.PrefersGnaw ? 0.3f : StarvationLevel;
            if (!RHAH_VisitorRules.AllowsGnaw(true, true, true, needFood, level, threshold, busy, downed))
            {
                return null;
            }

            if (pawn.Map == null)
            {
                return null;
            }

            RHAH_PawnState state = comp.State;
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (now < state.gnawSearchUntilTick)
            {
                return null;
            }

            Thing target = FindGnawTarget(pawn);
            if (target == null)
            {
                state.gnawSearchUntilTick = now + RHAH_ReliefFood.RetryBaseTicks;

                return null;
            }

            return JobMaker.MakeJob(RHAH_DefOf.RHAH_Gnaw, target);
        }

        static Thing FindGnawTarget(Verse.Pawn pawn)
        {
            TraverseParms traverse = TraverseParms.For(pawn);
            Thing tree = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Plant),
                PathEndMode.Touch,
                traverse,
                SearchRadius,
                thing => IsPlant(thing, pawn));
            Thing wall = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.Touch,
                traverse,
                SearchRadius,
                thing => IsWallLike(thing, pawn));
            return RHAH_GnawHealth.NearestTarget(pawn.Position, tree, wall);
        }

        static bool IsPlant(Thing thing, Verse.Pawn pawn)
        {
            if (thing == null || thing.Destroyed || thing.IsForbidden(pawn) ||
                !thing.def.useHitPoints || thing.def.plant == null || !thing.def.plant.IsTree)
            {
                return false;
            }

            return pawn.CanReserve(thing);
        }

        static bool IsWallLike(Thing thing, Verse.Pawn pawn)
        {
            if (thing == null || thing.Destroyed || thing.IsForbidden(pawn) ||
                !thing.def.useHitPoints || thing.def.category != ThingCategory.Building)
            {
                return false;
            }

            if (thing.def.Fillage != FillCategory.Full)
            {
                return false;
            }

            return pawn.CanReserve(thing);
        }
    }
}
