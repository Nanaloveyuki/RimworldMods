using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobGiver_MouseDisasterTailBite : ThinkNode_JobGiver
    {
        private const int TailBiteThinkIntervalTicks = 240;

        public override float GetPriority(Pawn pawn)
        {
            if (!MouseDisasterUtility.CanPrisonerBiteTail(pawn) || pawn?.needs?.food == null)
            {
                return 0f;
            }

            if (!pawn.IsHashIntervalTick(TailBiteThinkIntervalTicks))
            {
                return 0f;
            }

            if (MouseDisasterUtility.HasAccessibleFood(pawn))
            {
                return 0f;
            }

            return pawn.needs.food.CurCategory == HungerCategory.Starving ? 8.9f : 7.8f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!MouseDisasterUtility.CanPrisonerBiteTail(pawn))
            {
                return null;
            }

            if (!pawn.IsHashIntervalTick(TailBiteThinkIntervalTicks))
            {
                return null;
            }

            if (MouseDisasterUtility.HasAccessibleFood(pawn))
            {
                return null;
            }

            if (!MouseDisasterUtility.CanAttemptTailBite(pawn))
            {
                return null;
            }

            if (!Rand.Chance(0.05f))
            {
                return null;
            }

            Pawn target = MouseDisasterUtility.FindTailBiteTarget(pawn);
            if (target == null)
            {
                return null;
            }

            MouseDisasterUtility.RecordTailBiteAttempt(pawn);
            if (MouseDisasterUtility.IsPrisonerScavengeDebugLogEnabled)
            {
                Log.Message("[MouseDisaster][TailBite] assign job: " + pawn.LabelShortCap + " -> " + target.LabelShortCap);
            }

            Job job = JobMaker.MakeJob(MouseDisasterDefOf.MouseDisaster_TailBite, target);
            job.maxNumMeleeAttacks = 1;
            return job;
        }
    }
}
