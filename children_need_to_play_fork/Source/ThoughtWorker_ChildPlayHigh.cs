using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Thought worker that gives a positive mood buff when a child's play need
    /// is at or above 90%.
    /// </summary>
    public class ThoughtWorker_ChildPlayHigh : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return ThoughtState.Inactive;

            // Only children have the play need
            if (!p.DevelopmentalStage.Child())
            {
                return ThoughtState.Inactive;
            }

            Need_ChildPlay? playNeed = p.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
            if (playNeed == null)
            {
                return ThoughtState.Inactive;
            }

            return playNeed.CurLevel >= 0.90f
                ? ThoughtState.ActiveDefault
                : ThoughtState.Inactive;
        }
    }
}
