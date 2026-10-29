using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Makes adult colonists autonomously dress toddlers who aren't wearing baby bodywear
    /// (onesies or tribal babywear). Uses the vanilla <see cref="JobDefOf.ForceTargetWear"/>
    /// job driver to handle the dressing action.
    ///
    /// This work giver operates in the <see cref="WorkTypeDefOf.Childcare"/> work type,
    /// with priority 75 (below feeding at 80, but still fairly high since naked toddlers
    /// receive mood debuffs and can develop hypothermia).
    ///
    /// For each adult pawn with Childcare enabled, this giver:
    /// 1. Scans all spawned toddlers
    /// 2. Identifies those not wearing baby bodywear
    /// 3. Finds available baby apparel (Apparel_BabyOnesie or Apparel_BabyTribal)
    /// 4. Creates a <see cref="JobDefOf.ForceTargetWear"/> job to dress the toddler
    ///
    /// Design note: Toddlers who can dress themselves via <see cref="JobGiver_OptimizeBabyApparel"/>
    /// might not do so reliably. This work giver ensures ALL naked toddlers get dressed,
    /// regardless of their own job givers.
    /// </summary>
    public class WorkGiver_DressToddler : WorkGiver_Scanner
    {
        // ═══════════════════════════════════════════════════════════════════
        //  CONSTANTS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Maximum distance to search for baby apparel.</summary>
        const float MaxApparelSearchDistance = 30f;

        // ═══════════════════════════════════════════════════════════════════
        //  CACHE (keeps HasJobOnThing and JobOnThing synchronized)
        // ═══════════════════════════════════════════════════════════════════
        // Work givers are singletons; these fields bridge the gap between
        // the cheap HasJobOnThing check and the JobOnThing job creation.
        // Cleared after each JobOnThing call to avoid stale data.

        Pawn? cachedTarget;
        Thing? cachedApparel;

        // ═══════════════════════════════════════════════════════════════════
        //  WORKGIVER_SCANNER API
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>We're looking for toddler pawns to dress.</summary>
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        /// <summary>
        /// Pathing approach: get to the toddler's cell so we can interact with them.
        /// </summary>
        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        /// <summary>
        /// We can attempt dressing work even in dangerous situations. The player
        /// might want a colonist to dress a toddler before fleeing danger.
        /// </summary>
        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        /// <summary>
        /// Returns all spawned toddlers on the map. Filtered by HasJobOnThing.
        /// </summary>
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.mapPawns.AllPawnsSpawned;
        }

        /// <summary>
        /// Checks if the given pawn (toddler) should be dressed by the worker (adult).
        /// </summary>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn toddler))
                return false;

            // ── Toddler validity ──────────────────────────────────────────
            // Explicit humanlike check: base-mod ToddlerUtility.IsToddler can
            // match baby animals (they share the Baby life stage), so we
            // gatekeep here to prevent hares and other animals from passing.
            if (!toddler.RaceProps.Humanlike)
                return false;

            if (!TPE_ToyUtility.IsToddler(toddler))
                return false;

            if (toddler.Dead || !toddler.Spawned)
                return false;

            // ── Adult validity ────────────────────────────────────────────
            if (pawn.Dead || !pawn.Spawned)
                return false;

            // Only colonists can perform childcare work
            if (!pawn.IsColonistPlayerControlled)
                return false;

            if (!pawn.CanReach(toddler, PathEndMode.OnCell, Danger.Deadly))
                return false;

            // ── Toddler needs dressing ────────────────────────────────────
            // Check if toddler is already wearing baby bodywear
            if (IsWearingBabyBodywear(toddler))
                return false;

            // ── Available apparel ─────────────────────────────────────────
            // Must have reachable, available baby apparel on the map.
            // Cache the result so JobOnThing uses the same apparel instance.
            Thing? apparel = FindNearestBabyApparel(pawn, MaxApparelSearchDistance);
            if (apparel == null)
                return false;

            cachedTarget = toddler;
            cachedApparel = apparel;
            return true;
        }

        /// <summary>
        /// Creates a ForceTargetWear job to dress the toddler.
        /// </summary>
        public override Job? JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn toddler))
                return null;

            // ── Re-validate in case conditions changed ────────────────────
            if (!toddler.RaceProps.Humanlike)
                return null;

            if (!HasJobOnThing(pawn, t, forced))
                return null;

            // ── Find apparel ─────────────────────────────────────────────
            // Prefer the cached apparel from HasJobOnThing to avoid desync
            // (another pawn might have picked up the nearest one between calls).
            Thing? apparel = null;
            if (cachedTarget == toddler && cachedApparel != null && cachedApparel.Spawned)
            {
                apparel = cachedApparel;
            }
            else
            {
                apparel = FindNearestBabyApparel(pawn, MaxApparelSearchDistance);
            }

            // Clear cache regardless of outcome
            cachedTarget = null;
            cachedApparel = null;

            if (apparel == null)
            {
                JobFailReason.Is("NoBabyApparelAvailable".Translate());
                return null;
            }

            // ── Verify toddler can wear it ───────────────────────────────
            if (!ApparelUtility.HasPartsToWear(toddler, apparel.def))
            {
                JobFailReason.Is("CannotWearBecauseOfMissingBodyParts".Translate());
                return null;
            }

            // ── Create ForceTargetWear job ───────────────────────────────
            // TargetIndex.A = target pawn (toddler)
            // TargetIndex.B = apparel
            Job job = JobMaker.MakeJob(JobDefOf.ForceTargetWear, toddler, apparel);
            return job;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true if the toddler is wearing baby bodywear (onesie or tribalwear).
        /// Does NOT include accessory baby apparel like hats.
        /// </summary>
        static bool IsWearingBabyBodywear(Pawn toddler)
        {
            if (toddler.apparel?.WornApparel == null)
                return false;

            foreach (var apparel in toddler.apparel.WornApparel)
            {
                // Check for onesie or tribalwear (the body-covering baby apparel)
                if (apparel.def.defName == "Apparel_BabyOnesie" ||
                    apparel.def.defName == "Apparel_BabyTribal")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the nearest available baby apparel (onesie or tribalwear) that the
        /// adult pawn can reach. Returns null if no suitable apparel is found.
        /// </summary>
        static Thing? FindNearestBabyApparel(Pawn adult, float maxDistance)
        {
            Thing? nearest = null;
            float nearestDistance = maxDistance;

            // ── Get baby apparel defs from database ──────────────────────
            ThingDef? onesie = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_BabyOnesie");
            ThingDef? tribalwear = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_BabyTribal");

            if (onesie == null)
                return null; // Base mod not loaded or defs missing

            // ── Search for available onesies ─────────────────────────────
            foreach (Thing thing in adult.Map.listerThings.ThingsOfDef(onesie))
            {
                if (!(thing is Apparel apparel))
                    continue;

                if (!apparel.Spawned || apparel.IsBurning())
                    continue;

                if (apparel.IsForbidden(adult))
                    continue;

                if (!adult.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Deadly))
                    continue;

                float distance = adult.Position.DistanceTo(apparel.Position);
                if (distance < nearestDistance)
                {
                    nearest = apparel;
                    nearestDistance = distance;
                }
            }

            // ── If no onesie found, try tribalwear ──────────────────────
            if (nearest == null && tribalwear != null)
            {
                foreach (Thing thing in adult.Map.listerThings.ThingsOfDef(tribalwear))
                {
                    if (!(thing is Apparel apparel))
                        continue;

                    if (!apparel.Spawned || apparel.IsBurning())
                        continue;

                    if (apparel.IsForbidden(adult))
                        continue;

                    if (!adult.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Deadly))
                        continue;

                    float distance = adult.Position.DistanceTo(apparel.Position);
                    if (distance < nearestDistance)
                    {
                        nearest = apparel;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }
    }
}
