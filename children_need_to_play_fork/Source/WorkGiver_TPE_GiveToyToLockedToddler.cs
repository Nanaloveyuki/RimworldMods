using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Work giver that assigns colonists to bring a plush toy to a crib-locked
    /// toddler who is not currently wearing one.
    /// </summary>
    public class WorkGiver_TPE_GiveToyToLockedToddler : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
            => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // Copy the list first — AllPawnsSpawned is a cached mutable list.
            List<Pawn> allSpawned = new List<Pawn>(pawn.Map.mapPawns.AllPawnsSpawned);
            return allSpawned
                .Where(p => CribLockUtility.IsCribLocked(p)
                    && ToddlerUtility.IsToddler(p)
                    && !HasPlushToyEquipped(p));
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t is not Pawn toddler) return null;
            if (!CribLockUtility.IsCribLocked(toddler)) return null;
            if (toddler.ParentHolder is Pawn_CarryTracker) return null;
            if (HasPlushToyEquipped(toddler)) return null;

            Thing? plush = FindNearestPlush(pawn, toddler);
            if (plush == null) return null;

            if (!pawn.CanReserve(toddler, 1, -1, null, forced)) return null;
            if (!pawn.CanReserve(plush, 1, -1, null, forced)) return null;
            if (!pawn.CanReach(toddler, PathEndMode.Touch, Danger.Deadly)) return null;
            if (!pawn.CanReach(plush, PathEndMode.ClosestTouch, Danger.Deadly)) return null;

            Job job = JobMaker.MakeJob(TPE_JobDefOf.TPE_GiveToyToLockedToddler, toddler, plush);
            job.count = 1;
            return job;
        }

        static bool HasPlushToyEquipped(Pawn p)
        {
            if (p.apparel?.WornApparel == null) return false;
            foreach (Apparel a in p.apparel.WornApparel)
            {
                if (TPE_ToyUtility.IsPlushThing(a))
                    return true;
            }
            return false;
        }

        static Thing? FindNearestPlush(Pawn colonist, Pawn toddler)
        {
            Map map = colonist.Map;
            Thing? best = null;
            float bestScore = float.MaxValue;

            foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
            {
                if (!TPE_ToyUtility.IsPlushThing(thing)) continue;
                if (thing.IsForbidden(colonist)) continue;
                if (!StoreUtility.IsInAnyStorage(thing) && thing.Spawned) continue;

                float dist = colonist.Position.DistanceToSquared(thing.Position)
                           + toddler.Position.DistanceToSquared(thing.Position);
                if (dist < bestScore)
                {
                    bestScore = dist;
                    best = thing;
                }
            }
            return best;
        }
    }
}
