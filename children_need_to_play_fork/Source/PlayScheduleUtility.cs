using System.Collections.Generic;
using RimWorld;
using Toddlers;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>Shared selection and running-job policy; no faction/prisoner exclusion.</summary>
    [StaticConstructorOnStartup]
    internal static class PlayScheduleUtility
    {
        private static readonly JobGiver_GetRest RestGiver = new JobGiver_GetRest();
        private static readonly HashSet<JobDef> PlayJobs = new HashSet<JobDef>();
        internal const int OverrideCheckInterval = 50;

        static PlayScheduleUtility()
        {
            foreach (ToddlerPlayDef def in DefDatabase<ToddlerPlayDef>.AllDefsListForReading)
                if (def.jobDef != null) PlayJobs.Add(def.jobDef);
            foreach (PlayDesireDef def in DefDatabase<PlayDesireDef>.AllDefsListForReading)
                if (def.jobDef != null) PlayJobs.Add(def.jobDef);
            // Enhances the base toddler bugwatching job rather than having its own play def.
            PlayJobs.Add(TPE_JobDefOf.TPE_BugwatchingWithGlass);
        }

        internal static bool IsPlayJob(Job job) => job != null && PlayJobs.Contains(job.def);

        internal static bool CanPlayNow(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike || !pawn.Awake())
                return false;

            TimeAssignmentDef assignment = pawn.timetable?.CurrentAssignment ?? TimeAssignmentDefOf.Anything;
            if (!PlayScheduleRules.AllowsPlay(assignment, pawn.CurJob))
                return false;

            if (JoyUtility.LordPreventsGettingJoy(pawn))
                return false;

            // PatientGoToBed and in-bed joy use this medical predicate. It also
            // catches tended injuries, not just Downed/untended emergencies.
            if (HealthAIUtility.ShouldSeekMedicalRest(pawn))
                return false;

            // Ask vanilla rather than duplicating its sleep threshold, sleep-blocking
            // hediffs, disturbance, canSleepTick and Lord checks.
            if (RestGiver.GetPriority(pawn) > 0f)
                return false;

            return !PawnUtility.WillSoonHaveBasicNeed(pawn, 0f);
        }

        internal static bool CanSelectChildPlay(Pawn pawn, bool ignoreFullness = false)
        {
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true || pawn == null
                || !pawn.DevelopmentalStage.Child() || Find.TickManager.TicksGame < 5000)
                return false;

            Need_ChildPlay play = pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
            return play != null && (ignoreFullness || play.CurLevelPercentage < 0.95f) && CanPlayNow(pawn);
        }

        internal static float ChildPlayPriority(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || !CanSelectChildPlay(pawn))
                return 0f;
            float playLevel = pawn.needs.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay).CurLevelPercentage;
            return ThinkNode_Priority_ChildPlay.IsPlayMoreUrgentThanLearning(pawn, playLevel)
                ? ThinkNode_Priority_ChildPlay.PriorityUrgent : ThinkNode_Priority_ChildPlay.PriorityNormal;
        }

        /// <summary>Called before any toil tick, including travel, not just while gaining play.</summary>
        internal static bool ContinuePlaying(JobDriver driver)
        {
            Job job = driver.job;
            Pawn pawn = driver.pawn;
            if (job.playerForced || !pawn.IsHashIntervalTick(OverrideCheckInterval))
                return true;
            if (!CanPlayNow(pawn))
            {
                driver.EndJobWith(JobCondition.InterruptOptional);
                return false;
            }

            // Idle leisure yields to any need/work priority. Ordinary play yields
            // only to a strictly higher priority, avoiding same-play self-restarts.
            float priority;
            if (job.jobGiver is JobGiver_ChildPlayWhenIdle)
                priority = 0.01f;
            else if (pawn.DevelopmentalStage.Child())
            {
                float playLevel = pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay)?.CurLevelPercentage ?? 1f;
                priority = (ThinkNode_Priority_ChildPlay.IsPlayMoreUrgentThanLearning(pawn, playLevel)
                    ? ThinkNode_Priority_ChildPlay.PriorityUrgent : ThinkNode_Priority_ChildPlay.PriorityNormal) + 0.01f;
            }
            else
                priority = job.jobGiver != null ? job.jobGiver.GetPriority(pawn) + 0.01f : 6.01f;
            pawn.jobs.CheckForJobOverride(priority);
            return pawn.CurJob == job;
        }
    }

    internal static class PlayScheduleRules
    {
        internal static bool AllowsPlay(TimeAssignmentDef assignment, Job currentJob)
        {
            // Explicit whitelist leaves dedicated Learning/Meditation/custom
            // schedules to their providers; allowJoy alone is not sufficient.
            if (!assignment.allowJoy || (assignment != TimeAssignmentDefOf.Anything && assignment != TimeAssignmentDefOf.Joy))
                return false;
            if (currentJob?.playerForced == true)
                return false;
            // Labor mods can assign prisoners work without a timetable. Do not
            // let ordinary play steal such a running assignment. Recreation is
            // explicit leisure and retains vanilla's precedence over normal work.
            return assignment == TimeAssignmentDefOf.Joy || currentJob == null
                || (currentJob.workGiverDef == null && !(currentJob.jobGiver is JobGiver_Work));
        }
    }
}
