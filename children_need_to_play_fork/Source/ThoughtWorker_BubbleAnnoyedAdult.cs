using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Situational thought: adults standing inside an active Bubble Machine's zone find
    /// the bubbles annoying to work around — but ONLY when that zone is confined to an
    /// enclosed (fully-roofed) room, where there's no escaping the mess. Adults standing
    /// in an outdoor/partially-roofed bubble zone are unaffected (plenty of open air to
    /// step away from the popping bubbles, unlike being stuck in a small room with them).
    /// </summary>
    public class ThoughtWorker_BubbleAnnoyedAdult : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            // Cheapest checks first: only humanlike adults care about this at all.
            if (!p.RaceProps.Humanlike || !p.DevelopmentalStage.Adult())
            {
                return ThoughtState.Inactive;
            }

            bool inZone = BubbleMoodUtility.IsInActiveBubbleZone(p, out bool isEnclosed);

            // Requires BOTH: standing in an active zone, AND that zone being enclosed.
            // An adult in an active but open-air (circular fallback) zone is unaffected.
            return (inZone && isEnclosed)
                ? ThoughtState.ActiveDefault
                : ThoughtState.Inactive;
        }
    }
}
