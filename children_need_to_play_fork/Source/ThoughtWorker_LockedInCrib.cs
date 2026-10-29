using RimWorld;
using Toddlers;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Situational thought worker that applies a mild mood debuff to toddlers
    /// who are currently crib-locked by the player AND physically inside their crib.
    /// The thought is removed when the toddler is temporarily out of the crib
    /// (e.g. for feeding) or when the lock is toggled off.
    /// </summary>
    public class ThoughtWorker_LockedInCrib : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p?.health?.hediffSet == null)
                return ThoughtState.Inactive;

            bool isLocked = p.health.hediffSet.HasHediff(TPE_HediffDefOf.TPE_CribLocked);
            bool isInCrib = CribUtility.InCrib(p);

            return (isLocked && isInCrib)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }
}
