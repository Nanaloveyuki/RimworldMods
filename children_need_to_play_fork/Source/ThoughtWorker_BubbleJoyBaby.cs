using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Situational thought: babies and toddlers standing inside an active Bubble
    /// Machine's zone love watching, chasing, and popping the bubbles.
    /// </summary>
    /// <remarks>
    /// "Toddlers and babies" both live under vanilla Biotech's single
    /// <see cref="DevelopmentalStage.Baby"/> flag (Cyanobot's Toddlers mod doesn't add a
    /// separate engine-level developmental stage for toddlers — it layers toddler-specific
    /// behavior on top of the same Baby life stage bracket), so one age check here covers
    /// both groups exactly as the design calls for.
    /// </remarks>
    public class ThoughtWorker_BubbleJoyBaby : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            // Cheapest checks first: only humanlike babies/toddlers care about this at all.
            // (Explicit Humanlike guard matches this mod's established convention of never
            // relying on "animals probably don't have a mood need anyway" — see the animal
            // safeguards elsewhere in this mod for the same defense-in-depth reasoning.)
            if (!p.RaceProps.Humanlike || !p.DevelopmentalStage.Baby())
            {
                return ThoughtState.Inactive;
            }

            // Only reached for humanlike babies/toddlers, so the (slightly pricier) zone
            // scan only ever runs for pawns this thought could actually apply to.
            return BubbleMoodUtility.IsInActiveBubbleZone(p, out _)
                ? ThoughtState.ActiveDefault
                : ThoughtState.Inactive;
        }
    }
}
