using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Thought worker for the child play need (TPE_ChildPlay).
    /// Maps need level to thought stages:
    ///   Stage 0 (Empty):    CurLevel ≤ 0.15  → "desperate for play" (-10)
    ///   Stage 1 (Very Low): CurLevel ≤ 0.30  → "needs play badly" (-5)
    ///   Stage 2 (Low):      CurLevel ≤ 0.45  → "wants to play" (-2)
    ///   Otherwise: inactive
    /// </summary>
    public class ThoughtWorker_NeedChildPlay : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return ThoughtState.Inactive;

            Need? need = p.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay);
            if (need == null)
                return ThoughtState.Inactive;

            float level = need.CurLevel;
            if (level <= 0.15f)
                return ThoughtState.ActiveAtStage(0);
            if (level <= 0.30f)
                return ThoughtState.ActiveAtStage(1);
            if (level <= 0.45f)
                return ThoughtState.ActiveAtStage(2);

            return ThoughtState.Inactive;
        }
    }
}
