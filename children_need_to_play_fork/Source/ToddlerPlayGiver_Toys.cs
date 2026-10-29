using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    // ============================================================
    // AUTONOMOUS: Toddler finds a real toy and plays with it
    // ============================================================
    /// <summary>
    /// Autonomous play giver: toddler finds the nearest reachable real toy,
    /// picks it up out of storage, and plays with it.
    /// Respects rest level — won't trigger if toddler is too sleepy (&lt; 45%).
    /// </summary>
    public class ToddlerPlayGiver_PlayWithRealToy : ToddlerPlayGiver
    {
        const float MaxToyDistance = 15.9f;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            if (!base.CanDo(pawn))
                return false;
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevel <= 0.45f)
                return false;
            // If already carrying a toy, don't start a "play with toy" job —
            // it would fail because StartCarryThing can't pick up an already-carried item.
            if (pawn.carryTracker?.CarriedThing != null)
                return false;
            Thing toy = FindNearbyToy(pawn);
            return toy != null && ReservationUtility.CanReserveAndReach(pawn, toy, PathEndMode.Touch, Danger.Some, 1, -1, null, false);
        }

        public override bool CanDoFromCrib(Pawn pawn)
        {
            return false;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            Thing toy = FindNearbyToy(pawn);
            if (toy == null) return null;
            Job job = JobMaker.MakeJob(TPE_JobDefOf.TPE_PlayWithToy, toy);
            job.count = 1;
            return job;
        }

        Thing FindNearbyToy(Pawn pawn)
        {
            // Search room first
            Room room = RegionAndRoomQuery.GetRoom(pawn, RegionType.Set_Passable);
            if (room != null)
            {
                foreach (ThingDef toyDef in ToyDefs)
                {
                    foreach (Thing thing in room.ContainedThings(toyDef))
                        if (IsValidToy(thing, pawn)) return thing;
                }
            }

            // Fallback: closest reachable toy
            Thing best = null;
            float bestDist = float.MaxValue;
            Map map = pawn.Map;
            foreach (ThingDef toyDef in ToyDefs)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(toyDef))
                {
                    if (!IsValidToy(thing, pawn)) continue;
                    float dist = thing.Position.DistanceToSquared(pawn.Position);
                    if (dist < bestDist && dist <= MaxToyDistance * MaxToyDistance)
                    {
                        bestDist = dist;
                        best = thing;
                    }
                }
            }
            return best;
        }

        bool IsValidToy(Thing toy, Pawn pawn)
        {
            if (toy == null || toy.IsBurning()) return false;
            if (ForbidUtility.IsForbidden(toy, pawn)) return false;
            if (!ReservationUtility.CanReserveAndReach(pawn, toy, PathEndMode.Touch, Danger.Some, 1, -1, null, false)) return false;
            return true;
        }

        static readonly List<ThingDef> ToyDefs = new List<ThingDef>
        {
            TPE_ThingDefOf.TPE_PlushTeddy,
            TPE_ThingDefOf.TPE_PlushMuffalo,
            TPE_ThingDefOf.TPE_PlushSheep,
            TPE_ThingDefOf.TPE_PlushDino,
            TPE_ThingDefOf.TPE_PlushMonster,
            TPE_ThingDefOf.TPE_PlushFox,
            TPE_ThingDefOf.TPE_ToySword
        };
    }

    // ============================================================
    // AUTONOMOUS: Toddler finds a real toy and carries it around
    // ============================================================
    /// <summary>
    /// Autonomous play giver: toddler finds the nearest reachable real toy,
    /// picks it up, and wanders around carrying it for ~40 seconds.
    /// Won't trigger if the toddler is already carrying something.
    /// </summary>
    public class ToddlerPlayGiver_CarryToy : ToddlerPlayGiver
    {
        const float MaxToyDistance = 15.9f;

        public override bool CanDo(Pawn pawn)
        {
            if (TPE_PlayUtility.IsScheduledToSleep(pawn))
                return false;
            if (!base.CanDo(pawn))
                return false;
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevel <= 0.45f)
                return false;
            // Don't carry if already carrying something
            if (pawn.carryTracker.CarriedThing != null)
                return false;
            Thing toy = FindNearbyToy(pawn);
            return toy != null && ReservationUtility.CanReserveAndReach(pawn, toy, PathEndMode.ClosestTouch, Danger.Some, 1, -1, null, false);
        }

        public override bool CanDoFromCrib(Pawn pawn)
        {
            return false;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            Thing toy = FindNearbyToy(pawn);
            if (toy == null) return null;
            Job job = JobMaker.MakeJob(TPE_JobDefOf.TPE_CarryToy, toy);
            job.count = 1;
            return job;
        }

        Thing FindNearbyToy(Pawn pawn)
        {
            Room room = RegionAndRoomQuery.GetRoom(pawn, RegionType.Set_Passable);
            if (room != null)
            {
                foreach (ThingDef toyDef in ToyDefs)
                {
                    foreach (Thing thing in room.ContainedThings(toyDef))
                        if (IsValidToy(thing, pawn)) return thing;
                }
            }

            Thing best = null;
            float bestDist = float.MaxValue;
            Map map = pawn.Map;
            foreach (ThingDef toyDef in ToyDefs)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(toyDef))
                {
                    if (!IsValidToy(thing, pawn)) continue;
                    float dist = thing.Position.DistanceToSquared(pawn.Position);
                    if (dist < bestDist && dist <= MaxToyDistance * MaxToyDistance)
                    {
                        bestDist = dist;
                        best = thing;
                    }
                }
            }
            return best;
        }

        bool IsValidToy(Thing toy, Pawn pawn)
        {
            if (toy == null || toy.IsBurning()) return false;
            if (ForbidUtility.IsForbidden(toy, pawn)) return false;
            if (!ReservationUtility.CanReserveAndReach(pawn, toy, PathEndMode.ClosestTouch, Danger.Some, 1, -1, null, false)) return false;
            return true;
        }

        static readonly List<ThingDef> ToyDefs = new List<ThingDef>
        {
            TPE_ThingDefOf.TPE_PlushTeddy,
            TPE_ThingDefOf.TPE_PlushMuffalo,
            TPE_ThingDefOf.TPE_PlushSheep,
            TPE_ThingDefOf.TPE_PlushDino,
            TPE_ThingDefOf.TPE_PlushMonster,
            TPE_ThingDefOf.TPE_PlushFox,
            TPE_ThingDefOf.TPE_ToySword
        };
    }
}
