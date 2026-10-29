using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Auto-WorkGiver: colonists assigned to Childcare will let toddlers out of cribs
    /// when they enter the "Wants to be let out" mental state.
    /// </summary>
    public class WorkGiver_TPE_LetToddlerOutOfCrib : WorkGiver_Scanner
    {
        // Minimum ticks the mental state must be active before colonists respond.
        // Prevents instant extraction when the state just started.
        const int MinTicksBeforeResponse = 180; // 3 seconds

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            // Copy the list to avoid collection-modified exceptions during
            // work-giver scanning (vanilla may spawn/despawn pawns mid-scan).
            List<Pawn> allSpawned = new List<Pawn>(pawn.Map.mapPawns.AllPawnsSpawned);
            foreach (Pawn p in allSpawned)
            {
                if (ShouldLetOut(p))
                    yield return p;
            }
        }

        public override bool HasJobOnThing(Pawn worker, Thing t, bool forced = false)
        {
            if (t is not Pawn toddler) return false;
            if (!ShouldLetOut(toddler)) return false;
            if (!worker.CanReserve(toddler, 1, -1, null, forced)) return false;

            Building_Bed crib = CribUtility.GetCurrentCrib(toddler);
            if (crib == null) return false;
            if (!worker.CanReserve(crib, 1, -1, null, forced)) return false;
            if (!worker.CanReach(toddler, PathEndMode.ClosestTouch, Danger.Deadly)) return false;

            return true;
        }

        public override Job JobOnThing(Pawn worker, Thing t, bool forced = false)
        {
            Pawn toddler = (Pawn)t;
            Building_Bed crib = CribUtility.GetCurrentCrib(toddler);
            if (crib == null) return null;

            Job job = JobMaker.MakeJob(
                DefDatabase<JobDef>.GetNamed("LetOutOfCrib", true),
                toddler,
                crib);
            job.count = 1;
            return job;
        }

        /// <summary>
        /// Returns true only when the toddler:
        /// - Is alive and spawned
        /// - Is a toddler (base-mod check)
        /// - Is currently in a crib
        /// - Has our "wants out" mental state active for at least MinTicksBeforeResponse
        /// </summary>
        static bool ShouldLetOut(Pawn p)
        {
            // Defensive: bail if our DefOf hasn't resolved
            if (TPE_MentalStateDefOf.TPE_WantsOutOfCrib == null)
            {
                if (TPE_Mod.Settings?.debugLogging ?? false)
                    Log.WarningOnce("[TPE] TPE_MentalStateDefOf.TPE_WantsOutOfCrib is null — WorkGiver disabled until resolved.", 12345678);
                return false;
            }

            if (!p.Spawned || p.Dead) return false;
            if (!ToddlerUtility.IsToddler(p)) return false;
            if (!CribUtility.InCrib(p)) return false;
            if (p.MentalStateDef != TPE_MentalStateDefOf.TPE_WantsOutOfCrib) return false;

            // Must have been in the mental state for a short grace period
            if (p.MentalState is MentalState_WantsOutOfCrib wantOut)
            {
                if (wantOut.Age < MinTicksBeforeResponse)
                    return false;
            }

            return true;
        }
    }
}
