using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Situational thought: children standing inside an active Bubble Machine's zone
    /// enjoy chasing and popping the bubbles (a smaller mood buff than the baby/toddler
    /// version — kids find it fun but less overwhelmingly exciting).
    /// </summary>
    public class ThoughtWorker_BubbleJoyChild : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            // Cheapest checks first: only humanlike children care about this at all.
            if (!p.RaceProps.Humanlike || !p.DevelopmentalStage.Child())
            {
                return ThoughtState.Inactive;
            }

            return BubbleMoodUtility.IsInActiveBubbleZone(p, out _)
                ? ThoughtState.ActiveDefault
                : ThoughtState.Inactive;
        }
    }
}
