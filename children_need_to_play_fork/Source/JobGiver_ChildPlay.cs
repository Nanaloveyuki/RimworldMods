using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Think node that assigns play activities to children based on their
    /// <b>active play desires</b>. Every 8 hours a child receives 2 desires;
    /// this giver tries to satisfy one of them.
    ///
    /// Mirrors <see cref="JobGiver_Learn"/> from vanilla Biotech.
    /// </summary>
    public class JobGiver_ChildPlay : ThinkNode_JobGiver
    {
        /// <summary>
        /// Temporary list used to shuffle active desires without allocating
        /// every call.
        /// </summary>
        static readonly List<PlayDesireDef> tmpRandomDesires = new List<PlayDesireDef>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return null;

            if (!CanPlayRightNow(pawn))
                return null;

            // Get the pawn's active play desires from the tracker.
            Pawn_PlayTracker? tracker = Current.Game
                .GetComponent<GameComponent_PlayTracker>()
                ?.GetTracker(pawn);

            List<PlayDesireDef> activeDesires = tracker?.ActivePlayDesires;
            if (activeDesires.NullOrEmpty())
                return null;

            // Try each active desire in random order.
            tmpRandomDesires.Clear();
            tmpRandomDesires.AddRange(activeDesires.InRandomOrder());

            for (int i = 0; i < tmpRandomDesires.Count; i++)
            {
                PlayDesireDef desire = tmpRandomDesires[i];
                if (desire.Worker.CanDo(pawn))
                {
                    Job? job = desire.Worker.TryGiveJob(pawn);
                    if (job != null)
                    {
                        tmpRandomDesires.Clear();
                        return job;
                    }
                }
            }

            tmpRandomDesires.Clear();
            return null;
        }

        public override float GetPriority(Pawn pawn) => PlayScheduleUtility.ChildPlayPriority(pawn);

        static bool CanPlayRightNow(Pawn pawn) => PlayScheduleUtility.CanSelectChildPlay(pawn);
    }

    /// <summary>
    /// Think-node priority wrapper for child play, inserted into vanilla's
    /// MainColonistBehaviorCore PrioritySorter (Patches/ThinkTree_ChildPlay.xml).
    ///
    /// Two tiers (v1.2.2):
    /// <list type="bullet">
    /// <item><see cref="PriorityNormal"/> (8.5): below vanilla Learning (9.1), above
    /// Joy (7). Rest/medical and protected schedules are disqualified first.</item>
    /// <item><see cref="PriorityUrgent"/> (9.15): used when play is the more urgent
    /// need (<see cref="IsPlayMoreUrgentThanLearning"/>). Just above Learning, still
    /// below every survival/hygiene node (chemical 9.25, Dubs Bad Hygiene wash 9.25,
    /// food 9.5, DBH toilet/drink 9.6).</item>
    /// </list>
    /// Why the urgent tier exists: vanilla Learning keeps priority 9.1 until the need
    /// is 99.9% full and hands out learning jobs below 90% (95% in JobGiver_Learn;
    /// 99% with the "Learning Threshold for Learning Job Fix" mod). A freshly aged-up
    /// child starts with Learning at 50%, so at a flat 8.5 play got NO turn at all for
    /// ~2 in-game days while the play need drained (player report: a 3-year-old
    /// "refusing to play"). The urgent tier only reorders: if no play job can be
    /// found, the sorter falls through to Learning as before.
    ///
    /// Only Anything/Recreation assignments can select play. Work, Sleep and
    /// dedicated Learning return zero before even the urgent tier is considered;
    /// PlayScheduleUtility also gives vanilla sleep and medical rest precedence.
    /// </summary>
    public class ThinkNode_Priority_ChildPlay : ThinkNode_Priority
    {
        /// <summary>Normal play priority, after schedule/rest disqualification.</summary>
        public const float PriorityNormal = 8.5f;

        /// <summary>
        /// Urgent play priority: just above vanilla Learning (9.1); below chemical
        /// needs and Dubs Bad Hygiene washing (9.25), food (9.5) and DBH toilet/drink (9.6).
        /// </summary>
        public const float PriorityUrgent = 9.15f;

        /// <summary>
        /// Play below this level can become urgent: the play bar's "Low" tier starts
        /// here (<see cref="Need_ChildPlay.ThresholdSatisfied"/>, 30%), which is also
        /// where the low-play mood starts.
        /// </summary>
        public const float UrgentPlayThreshold = Need_ChildPlay.ThresholdSatisfied;

        public override float GetPriority(Pawn pawn) => PlayScheduleUtility.ChildPlayPriority(pawn);

        /// <summary>
        /// True when play is low (below <see cref="UrgentPlayThreshold"/>) AND lower
        /// than the child's Learning need, i.e. play is the more urgent of the two.
        /// Ties go to Learning (vanilla order). A child without a Learning need counts
        /// as Learning = 100%. Natural hysteresis: once a play session lifts play above
        /// 30% the child is back on the normal tier, so the two never flip-flop
        /// (the think tree is only consulted between jobs).
        /// </summary>
        public static bool IsPlayMoreUrgentThanLearning(Pawn pawn, float playLevel)
        {
            if (playLevel >= UrgentPlayThreshold)
                return false;

            float learningLevel = pawn.needs?.learning?.CurLevelPercentage ?? 1f;
            return playLevel < learningLevel;
        }
    }
}
