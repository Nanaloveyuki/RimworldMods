using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Standalone job: toddler walks to a plush toy, picks it up, and equips it as apparel.
    /// Used by the autonomous plush-seeking system and can be triggered manually.
    /// </summary>
    public class JobDriver_SeekPlushToy : JobDriver
    {
        /// <summary>
        /// The plush this toddler picked up. Tracked so we can drop and unforbid
        /// it if the job ends before the equip toil succeeds.
        /// </summary>
        Thing? trackedPlush;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return ReservationUtility.Reserve(pawn, TargetA, job, 1, 1, null, errorOnFailed, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.job.count = 1;
            trackedPlush = null;

            // On success: queue sleep job immediately so the toddler goes straight
            // to bed with the plush. Use the bed target stored by the redirect patch
            // (targetB) if available; otherwise find a fresh bed.
            // On failure: drop any carried plush.
            AddFinishAction(delegate (JobCondition condition)
            {
                if (condition == JobCondition.Succeeded)
                {
                    // Only queue sleep if the pawn is still actually tired.
                    // If rest recovered while fetching the plush, let them play.
                    // This safety cap (40%) is independent of the redirect
                    // thresholds in HarmonyPatches — redirect fires when rest is
                    // HIGH, but after fetching we still confirm they need sleep.
                    if (pawn.needs?.rest?.CurLevel <= 0.4f)
                    {
                        Thing bed = job.targetB.Thing;
                        if (bed == null || bed.Destroyed)
                        {
                            bed = RestUtility.FindBedFor(pawn);
                        }
                        if (bed != null)
                        {
                            Job sleepJob = JobMaker.MakeJob(JobDefOf.LayDown, bed);
                            pawn.jobs.jobQueue.EnqueueFirst(sleepJob);
                        }
                    }
                }

                if (trackedPlush != null && pawn.carryTracker?.CarriedThing == trackedPlush)
                {
                    if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                    {
                        TPE_ToyUtility.Unforbid(dropped);
                    }
                }
                trackedPlush = null;
            });

            // 1. Custom path to plush — vanilla GotoThing + StartCarryThing
            // can skip pathing under some conditions, causing teleport-pickup.
            Toil gotoPlush = ToilMaker.MakeToil("GotoPlush");
            int gotoStartTick = -1;
            gotoPlush.initAction = () =>
            {
                Thing? t = job.targetA.Thing;
                if (t != null && !t.Destroyed && t.Spawned && pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly))
                {
                    pawn.pather.StartPath(t, PathEndMode.Touch);
                    gotoStartTick = Find.TickManager.TicksGame;
                }
                else
                {
                    gotoStartTick = -1;
                }
            };
            gotoPlush.tickAction = () =>
            {
                if (gotoStartTick < 0)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                if (Find.TickManager.TicksGame - gotoStartTick < 2)
                    return;
                if (!pawn.pather.Moving)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                }
            };
            gotoPlush.defaultCompleteMode = ToilCompleteMode.Never;
            yield return gotoPlush;

            // 2. Pick up — only when adjacent, with retry ticks
            Toil pickupPlush = ToilMaker.MakeToil("PickupPlush");
            pickupPlush.initAction = () => { };
            pickupPlush.tickAction = () =>
            {
                Thing? t = job.targetA.Thing;
                if (t == null || t.Destroyed || !t.Spawned)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                if (pawn.carryTracker == null)
                {
                    pawn.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                if (pawn.carryTracker.innerContainer.Contains(t))
                {
                    trackedPlush = t;
                    pawn.jobs.curDriver.ReadyForNextToil();
                    return;
                }
                if (!pawn.Position.AdjacentTo8WayOrInside(t.Position))
                    return;

                int pickupCount = job.count;
                if (pickupCount > t.stackCount) pickupCount = t.stackCount;
                if (pickupCount < 1) pickupCount = 1;
                int pickedUp = pawn.carryTracker.TryStartCarry(t, pickupCount);
                if (pickedUp > 0)
                {
                    trackedPlush = t;
                }
                pawn.jobs.curDriver.ReadyForNextToil();
            };
            pickupPlush.defaultCompleteMode = ToilCompleteMode.Never;
            yield return pickupPlush;

            // 3. Equip it as apparel
            Toil equipPlush = ToilMaker.MakeToil("EquipPlush");
            equipPlush.initAction = delegate
            {
                if (HasPlushToyWorn(pawn))
                {
                    // Already wearing one — drop the carried one and we're done
                    if (trackedPlush != null && pawn.carryTracker?.CarriedThing == trackedPlush)
                    {
                        if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                        {
                            TPE_ToyUtility.Unforbid(dropped);
                        }
                    }
                    trackedPlush = null;
                    return;
                }

                Thing? plush = pawn.carryTracker?.CarriedThing;
                if (plush == null || plush.Destroyed)
                    return;

                if (plush is Apparel apparel && pawn.apparel != null)
                {
                    // 1. Check if wearable BEFORE removing from carry tracker.
                    //    If we remove first and Wear() fails, the item is despawned
                    //    and lost.
                    if (!apparel.PawnCanWear(pawn, ignoreGender: true))
                    {
                        if (TPE_Mod.Settings?.debugLogging ?? false)
                            Log.Warning($"[TPE] {pawn.Name} ({pawn.ageTracker.AgeBiologicalYearsFloat}y, stage {pawn.DevelopmentalStage}) cannot wear {apparel.def.defName} (filter: {apparel.def.apparel.developmentalStageFilter}). Dropping.");
                        if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                        {
                            TPE_ToyUtility.Unforbid(dropped);
                        }
                        trackedPlush = null;
                        return;
                    }

                    // 2. BUGFIX: verify equipping won't silently orphan a currently-worn
                    //    item on a conflicting layer (e.g. an adult's shield belt — also
                    //    on the "Belt" layer our plush toys use). See
                    //    TPE_ToyUtility.HasConflictingWornApparel's doc comment for why
                    //    Wear(dropReplacedApparel:false) is otherwise unsafe here.
                    if (TPE_ToyUtility.HasConflictingWornApparel(pawn, apparel.def))
                    {
                        if (TPE_Mod.Settings?.debugLogging ?? false)
                            Log.Warning($"[TPE] {pawn.Name} cannot equip {apparel.def.defName}: conflicts with a currently worn item on the same apparel layer. Dropping instead of risking data loss.");
                        if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing conflictDropped))
                        {
                            TPE_ToyUtility.Unforbid(conflictDropped);
                        }
                        trackedPlush = null;
                        return;
                    }

                    // 3. Remove from carry tracker, THEN Wear().
                    //    Wear() will call DeSpawnOrDeselect() and then add to
                    //    apparel — it fails if the thing is still owned by
                    //    another container (carry tracker).
                    pawn.carryTracker.innerContainer.Remove(apparel);
                    pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                    trackedPlush = null; // equip succeeded — plush is now worn
                }
                else
                {
                    // Not an apparel somehow — drop it
                    if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                    {
                        TPE_ToyUtility.Unforbid(dropped);
                    }
                    trackedPlush = null;
                }
            };
            equipPlush.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return equipPlush;
        }

        static bool HasPlushToyWorn(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel == null) return false;
            foreach (Apparel a in pawn.apparel.WornApparel)
            {
                if (TPE_ToyUtility.IsPlushThing(a)) return true;
            }
            return false;
        }
    }
}
