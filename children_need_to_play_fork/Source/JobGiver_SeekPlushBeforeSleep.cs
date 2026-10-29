using RimWorld;
using Verse;
using Verse.AI;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Makes toddlers seek and equip a plush toy before they go to sleep.
    /// Runs inside the toddler think tree's priority sorter, before
    /// <see cref="JobGiver_GetRest"/>, so a sleepy toddler without a plush
    /// will fetch one first, then fall asleep with it equipped.
    ///
    /// Only activates when:
    /// - Pawn is a toddler (age 1–3 or base-mod IsToddler)
    /// - Pawn is NOT already in a crib (can't leave to seek)
    /// - Pawn does NOT already have a plush equipped or carried
    /// - Rest is between 0.25 and 0.6 (sleepy but not critical)
    /// - A reachable plush is available
    /// </summary>
    public class JobGiver_SeekPlushBeforeSleep : ThinkNode_JobGiver
    {
        const float RestThresholdHigh = 0.75f;
        const float RestThresholdLow = 0.10f;
        const float MaxPlushDistance = 25f;
        const float PriorityWhenActive = 9f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!IsValidCandidate(pawn, out Thing? plush)) return null;
            return JobMaker.MakeJob(TPE_JobDefOf.TPE_SeekPlushToy, plush);
        }

        public override float GetPriority(Pawn pawn)
        {
            if (!IsValidCandidate(pawn, out _)) return 0f;
            return PriorityWhenActive;
        }

        static bool IsValidCandidate(Pawn pawn, out Thing? plush)
        {
            plush = null;
            if (pawn == null || pawn.Dead || !pawn.Spawned) return false;
            if (!IsToddler(pawn)) return false;
            if (CribUtility.InCrib(pawn)) return false;
            if (HasPlushEquippedOrCarried(pawn)) return false;
            if (pawn.needs?.rest == null) return false;

            float rest = pawn.needs.rest.CurLevel;
            if (rest > RestThresholdHigh || rest < RestThresholdLow) return false;

            plush = FindNearestPlush(pawn);
            return plush != null;
        }

        static bool IsToddler(Pawn pawn)
        {
            // CRITICAL: Only humanlike pawns can seek toys before sleep.
            // Animals should never go collect toys, even if somehow marked as toddlers.
            if (!pawn.RaceProps.Humanlike) return false;
            
            if (ToddlerUtility.IsToddler(pawn)) return true;
            float age = pawn.ageTracker?.AgeBiologicalYearsFloat ?? 0f;
            return age >= 1f && age < 3f;
        }

        static bool HasPlushEquippedOrCarried(Pawn pawn)
        {
            if (pawn.apparel?.WornApparel != null)
            {
                foreach (Apparel a in pawn.apparel.WornApparel)
                {
                    if (IsPlushDef(a.def)) return true;
                }
            }
            if (pawn.carryTracker?.CarriedThing != null && IsPlushDef(pawn.carryTracker.CarriedThing.def))
                return true;
            return false;
        }

        static Thing? FindNearestPlush(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                MaxPlushDistance,
                t => IsPlushDef(t.def) && !t.IsForbidden(pawn) && pawn.CanReserve(t));
        }

        static bool IsPlushDef(ThingDef def)
        {
            return def == TPE_ThingDefOf.TPE_PlushTeddy
                || def == TPE_ThingDefOf.TPE_PlushMuffalo
                || def == TPE_ThingDefOf.TPE_PlushSheep
                || def == TPE_ThingDefOf.TPE_PlushDino
                || def == TPE_ThingDefOf.TPE_PlushMonster
                || def == TPE_ThingDefOf.TPE_PlushFox;
        }
    }
}
