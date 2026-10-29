using RimWorld;
using Verse;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Static helper for the crib-lock system. Centralises all checks so that
    /// patches, job drivers and work givers behave consistently.
    /// </summary>
    public static class CribLockUtility
    {
        /// <summary>
        /// Returns true when the pawn is a toddler who currently has the
        /// <see cref="TPE_HediffDefOf.TPE_CribLocked"/> hediff.
        /// </summary>
        public static bool IsCribLocked(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return false;
            if (!ToddlerUtility.IsToddler(pawn))
                return false;
            return pawn.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_CribLocked) == true;
        }

        /// <summary>
        /// Returns true when the pawn is crib-locked AND does not need medical
        /// rest. Medical emergencies are the only reason a locked toddler is
        /// allowed to leave their crib.
        /// </summary>
        public static bool IsCribLockedAndNotMedical(Pawn pawn)
        {
            if (!IsCribLocked(pawn))
                return false;
            // Medical override — allow hospital bed transport
            if (HealthAIUtility.ShouldSeekMedicalRest(pawn))
                return false;
            return true;
        }

        /// <summary>
        /// Removes the crib-lock hediff from a pawn only when they have aged up
        /// and are no longer a toddler. The lock intentionally persists when the
        /// toddler is temporarily out of the crib (e.g., for feeding) so the
        /// return system can bring them back.
        /// </summary>
        public static void RemoveLockIfInvalid(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return;

            Hediff lockHediff = pawn.health.hediffSet.GetFirstHediffOfDef(
                TPE_HediffDefOf.TPE_CribLocked);
            if (lockHediff == null)
                return;

            // Auto-remove only when no longer a toddler (aged up)
            if (!ToddlerUtility.IsToddler(pawn))
            {
                pawn.health.RemoveHediff(lockHediff);
            }
        }
    }
}
