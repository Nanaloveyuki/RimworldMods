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
        /// How long to wait after game start before children start playing
        /// (prevents immediate play-rush on map load).
        /// </summary>
        const int GameStartNoPlayTicks = 5000;

        /// <summary>
        /// Priority when the child is on a Joy/Recreation schedule assignment.
        /// </summary>
        const float PriorityJoyAssignment = 8.5f;

        /// <summary>
        /// Priority when the child is on Anything and play need is low.
        /// </summary>
        const float PriorityAnythingLow = 6f;

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

        public override float GetPriority(Pawn pawn)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return 0f;

            if (!IsValidCandidate(pawn))
                return 0f;

            TimeAssignmentDef currentAssignment = pawn.timetable?.CurrentAssignment
                ?? TimeAssignmentDefOf.Anything;

            // --- Defensive: no play during Learning time ---
            if (currentAssignment == TPE_TimeAssignmentDefOf.TPE_Learning)
                return 0f;

            if (currentAssignment == TimeAssignmentDefOf.Joy)
            {
                // During recreation hours, always seek play if not full
                return PriorityJoyAssignment;
            }

            if (currentAssignment == TimeAssignmentDefOf.Anything)
            {
                Need_ChildPlay? playNeed = pawn.needs?.TryGetNeed(
                    TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
                float curLevel = playNeed?.CurLevelPercentage ?? 1f;

                if (curLevel < 0.35f)
                    return PriorityAnythingLow;
            }

            return 0f;
        }

        /// <summary>
        /// Returns true if the pawn can be given a play job right now.
        /// </summary>
        static bool CanPlayRightNow(Pawn pawn)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return false;

            if (!IsValidCandidate(pawn))
                return false;

            TimeAssignmentDef currentAssignment = pawn.timetable?.CurrentAssignment
                ?? TimeAssignmentDefOf.Anything;

            // --- Defensive: no play during Learning time ---
            if (currentAssignment == TPE_TimeAssignmentDefOf.TPE_Learning)
                return false;

            // Must allow joy (play uses the same schedule slot as recreation)
            if (!currentAssignment.allowJoy)
                return false;

            Need_ChildPlay? playNeed = pawn.needs?.TryGetNeed(
                TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
            if (playNeed == null || playNeed.CurLevelPercentage >= 0.95f)
                return false;

            return true;
        }

        /// <summary>
        /// Returns true if the pawn is a child who should have the play need system.
        /// </summary>
        static bool IsValidCandidate(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return false;

            if (Find.TickManager.TicksGame < GameStartNoPlayTicks)
                return false;

            if (!pawn.DevelopmentalStage.Child())
                return false;

            if (pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay) == null)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Think-node priority wrapper for child play, inserted into vanilla's
    /// MainColonistBehaviorCore PrioritySorter (Patches/ThinkTree_ChildPlay.xml).
    ///
    /// Two tiers (v1.2.2):
    /// <list type="bullet">
    /// <item><see cref="PriorityNormal"/> (8.5): below vanilla Learning (9.1), above
    /// Rest (8) and Joy (7). Normally a child tops up Learning first, like vanilla.</item>
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
    /// When a child is explicitly on the TPE_Learning assignment, this node
    /// returns 0f so that play jobs are suppressed — the child should only do
    /// vanilla learning activities (or fall through to idle wander) during
    /// learning hours.  This mirrors how vanilla ThinkNode_Priority_GetJoy
    /// suppresses recreation during Work / Meditation assignments.
    /// </summary>
    public class ThinkNode_Priority_ChildPlay : ThinkNode_Priority
    {
        /// <summary>Normal play priority: below vanilla Learning (9.1), above Rest (8) and Joy (7).</summary>
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

        public override float GetPriority(Pawn pawn)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return 0f;

            if (!ModsConfig.BiotechActive)
                return 0f;

            if (pawn?.timetable == null)
                return 0f;

            TimeAssignmentDef current = pawn.timetable.CurrentAssignment
                ?? TimeAssignmentDefOf.Anything;

            // --- CRITICAL FIX: Suppress play during Learning time ---
            // When a child is on TPE_Learning they should only do vanilla
            // learning jobs (or critical needs).  Play is for Joy/Anything.
            if (current == TPE_TimeAssignmentDefOf.TPE_Learning)
                return 0f;

            if (!current.allowJoy)
                return 0f;

            if (Find.TickManager.TicksGame < 5000)
                return 0f;

            if (!pawn.DevelopmentalStage.Child())
                return 0f;

            Need_ChildPlay? playNeed = pawn.needs?.TryGetNeed(
                TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
            if (playNeed == null)
                return 0f;

            float playLevel = playNeed.CurLevelPercentage;
            if (playLevel >= 0.95f)
                return 0f;

            return IsPlayMoreUrgentThanLearning(pawn, playLevel) ? PriorityUrgent : PriorityNormal;
        }

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
