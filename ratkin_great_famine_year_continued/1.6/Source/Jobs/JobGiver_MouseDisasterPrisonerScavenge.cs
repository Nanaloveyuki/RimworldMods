using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobGiver_MouseDisasterPrisonerScavenge : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            if (!MouseDisasterUtility.CanPrisonerScavenge(pawn) || pawn?.needs?.food == null)
            {
                return 0f;
            }

            if (MouseDisasterUtility.IsExtremeCompatJobModeEnabled && pawn.needs.food.CurCategory != HungerCategory.Starving)
            {
                return 0f;
            }

            if (pawn.needs.food.CurCategory < HungerCategory.Hungry)
            {
                MouseDisasterUtility.ResetPrisonerScavengeDelayStateIfNotHungry(pawn, logDecision: false);
                return 0f;
            }

            if (MouseDisasterUtility.IsPrisonerScavengeDeferred(pawn))
            {
                return 0f;
            }

            if (MouseDisasterUtility.HasAccessibleFood(pawn))
            {
                return 0f;
            }

            if (MouseDisasterUtility.FindScavengeableFilth(pawn) == null)
            {
                return 0f;
            }

            return pawn.needs.food.CurCategory == HungerCategory.Starving ? 9.4f : 8.6f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (MouseDisasterUtility.IsExtremeCompatJobModeEnabled && (pawn?.needs?.food == null || pawn.needs.food.CurCategory != HungerCategory.Starving))
            {
                return null;
            }

            return MouseDisasterUtility.TryCreatePrisonerScavengeJob(pawn, logDecision: MouseDisasterUtility.IsPrisonerScavengeDebugLogEnabled);
        }
    }
}
