using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobGiver_MouseDisasterGnaw : ThinkNode_JobGiver
    {
        private const int GnawThinkIntervalTicks = 750;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (MouseDisasterFeeding.IsSeekingSuppressed(pawn)) return null;
            if (GameComponent_MouseDisasterEventBehavior.HasBehavior(pawn, MouseDisasterPawnBehavior.ReliefOnly)) return null;
            if (!MouseDisasterUtility.IsGnawingEnabled || pawn?.Map == null || pawn.Downed || pawn.needs?.food == null)
            {
                return null;
            }

            if (MouseDisasterUtility.IsPlayerAffiliatedRatkin(pawn))
            {
                return null;
            }

            if (!pawn.IsHashIntervalTick(GnawThinkIntervalTicks))
            {
                return null;
            }

            if (pawn.needs.food.CurCategory != HungerCategory.Starving)
            {
                return null;
            }

            if (!MouseDisasterUtility.IsMalnourished(pawn) || MouseDisasterUtility.HasAccessibleFood(pawn))
            {
                return null;
            }

            Thing tree = MouseDisasterUtility.FindGnawableTree(pawn);
            if (tree != null)
            {
                return JobMaker.MakeJob(MouseDisasterDefOf.MouseDisaster_GnawTreeBark, tree);
            }

            if (pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears < 3)
            {
                Thing wall = MouseDisasterUtility.FindGnawableWall(pawn);
                if (wall != null)
                {
                    return JobMaker.MakeJob(MouseDisasterDefOf.MouseDisaster_GnawWall, wall);
                }
            }

            return null;
        }
    }
}
