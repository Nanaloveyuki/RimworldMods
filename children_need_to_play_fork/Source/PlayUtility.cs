using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Static utility methods for the child play-desire system.
    /// Mirrors <see cref="LearningUtility"/> from vanilla Biotech.
    /// </summary>
    public static class PlayUtility
    {
        /// <summary>
        /// Threshold at which a child's play need is considered satisfied.
        /// Jobs end automatically when this level is reached.
        /// </summary>
        public const float SatisfiedThreshold = 0.999f;

        // ══════════════════════════════════════════════════════════════════
        //  IDLE-TRIGGERED JOB DETECTION
        //
        // Every child play JobDriver calls PlayTickCheckEnd on a timer/tick and
        // relies on it to end the job once play is satisfied — correct for a
        // NORMAL play session, since JobGiver_ChildPlay never hands out a job
        // once play is ≥95% full (see JobGiver_ChildPlay.CanPlayRightNow), so a
        // normal job can never START already at the 99.9% SatisfiedThreshold.
        //
        // JobGiver_ChildPlayWhenIdle ("Play when Idle") deliberately breaks that
        // invariant — by design it can hand a job to a child whose play need is
        // already full, since there's nothing better for them to do. Without
        // detecting that here, such a job's very first PlayTickCheckEnd call
        // sees play already satisfied and ends the job on the spot
        // (JobCondition.Succeeded) — often before the child has even reached
        // the destination. Symptom: a child walks to a play spot, then
        // immediately walks to ANOTHER one instead of playing, on a loop.
        //
        // Fix: rather than maintaining our own separate "is this an idle job"
        // record, we use the exact same bookkeeping the ENGINE ITSELF already
        // does. Pawn_JobTracker.StartJob (called for both the initial
        // no-current-job assignment AND every CheckForJobOverride reselection —
        // see TryFindAndStartJob and CheckForJobOverride in Pawn_JobTracker.cs)
        // always sets Job.jobGiver to the exact ThinkNode instance whose
        // TryIssueJobPackage produced it. So "is the child's current job one
        // JobGiver_ChildPlayWhenIdle handed out" is simply a type check on
        // pawn.CurJob.jobGiver — no separate dictionary, no TTL/staleness
        // concerns, nothing that can desync from what the job tracker itself
        // believes. (An earlier version of this method kept its own
        // Job.loadID-keyed dictionary with a manual expiry; that extra moving
        // part was unnecessary complexity now that the simpler, engine-native
        // check covers the exact same case with zero bookkeeping.)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true if the pawn's CURRENT job was handed out by
        /// <see cref="JobGiver_ChildPlayWhenIdle"/> (an idle-triggered "play
        /// despite the need being full" job), by checking the job's
        /// engine-recorded <see cref="Job.jobGiver"/> — the same field
        /// <c>Pawn_JobTracker.StartJob</c> populates for every job, whether it
        /// came from the pawn's initial no-job assignment or a later
        /// <c>CheckForJobOverride</c> reselection.
        /// </summary>
        static bool IsIdleTriggeredJob(Pawn pawn)
        {
            return pawn?.CurJob?.jobGiver is JobGiver_ChildPlayWhenIdle;
        }


        /// <summary>Converts an interval into play gain, like LearningTickCheckEnd.</summary>
        public static bool PlayTickCheckEnd(Pawn pawn, int delta, bool forced = false)
        {
            // Vanilla learning: 1.2E-05f per tick. Play fills ~10x faster.
            float amount = 1.2E-04f * delta;
            return PlayTickCheckEnd(pawn, amount, forced, delta);
        }

        /// <summary>Fills play and ends satisfied non-idle sessions. Schedule/health
        /// interruption is shared by every play driver via PlayScheduleUtility.</summary>
        public static bool PlayTickCheckEnd(Pawn pawn, float amount, bool forced = false, int intervalDelta = 1)
        {
            if (amount > 0f)
            {
                Need_ChildPlay? playNeed = pawn.needs?.TryGetNeed(
                    TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
                playNeed?.Play(amount);
            }


            // An idle-triggered job (see IsIdleTriggeredJob above) is treated
            // exactly like an explicitly forced call — it keeps running instead
            // of instantly ending the moment it notices play is already
            // satisfied.
            bool effectiveForced = forced || pawn.CurJob?.playerForced == true || IsIdleTriggeredJob(pawn);

            if (!effectiveForced && PlaySatisfied(pawn))
            {
                pawn.jobs?.EndCurrentJob(JobCondition.Succeeded);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the child's play need is at or above the satisfied
        /// threshold.
        /// </summary>
        public static bool PlaySatisfied(Pawn pawn)
        {
            Need_ChildPlay? playNeed = pawn.needs?.TryGetNeed(
                TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
            return playNeed != null && playNeed.CurLevel >= SatisfiedThreshold;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PLAY DESIRE ↔ LEARNING DESIRE MUTUAL EXCLUSION (shared helper)
        //
        // A child should never be handed the SAME activity as both an active
        // Play desire and an active Learning desire — see
        // PlayDesireDef.conflictingLearningDesire for the data-side mapping
        // (5 of 10 Play Desires map to a vanilla LearningDesireDef of the same
        // activity: Floor Drawing, Nature Running, Radio Talking, Reading, Sky
        // Dreaming).
        //
        // BUGFIX (caught in review): this check has TWO call sites, and BOTH
        // must use it, or a child can still end up DOING the same activity
        // for both purposes through the path that skips the check. The
        // normal 8-hour desire roll (Pawn_PlayTracker.AddNewPlayDesire) is
        // the obvious one. The easy-to-miss second one is
        // JobGiver_ChildPlayWhenIdle's "try every registered PlayDesireDef"
        // fallback pool ("Play when Idle", opt-in) — it bypasses the tracked
        // 2-desire list entirely and hands out a ONE-OFF job directly from
        // any PlayDesireDef, so without this check here too, an idle child
        // could still be sent to literally perform "Floor Drawing" as play
        // while ALSO currently holding "Floordrawing" as an active Learning
        // desire — exactly the outcome this whole feature exists to prevent,
        // even though that one-off job is never added to the pawn's tracked
        // ActivePlayDesires list. (Vanilla's own JobGiver_Learn has no
        // equivalent "try every LearningDesireDef" fallback — it only ever
        // tries the tracked 2 — so there is no symmetric gap on the Learning
        // side needing the same treatment.)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// True if <paramref name="playDesire"/> maps (via
        /// <see cref="PlayDesireDef.conflictingLearningDesire"/>) to a
        /// LearningDesireDef that's currently active in
        /// <paramref name="activeLearningDesires"/>. Desires with no mapped
        /// Learning counterpart (the majority — anything without an XML
        /// <c>conflictingLearningDesire</c>) are never blocked by this check.
        /// </summary>
        public static bool ConflictsWithActiveLearningDesire(PlayDesireDef playDesire,
            List<LearningDesireDef>? activeLearningDesires)
        {
            if (playDesire.conflictingLearningDesire == null)
                return false;
            if (activeLearningDesires.NullOrEmpty())
                return false;
            // NullOrEmpty() just proved this non-null/non-empty, but the analyzer
            // can't infer that through an extension method (it isn't annotated
            // [NotNullWhen(false)]) — null-forgiving operator silences the false
            // positive without masking any real null-safety gap.
            return activeLearningDesires!.Contains(playDesire.conflictingLearningDesire);
        }

        /// <summary>
        /// Convenience overload: fetches <paramref name="pawn"/>'s active
        /// Learning desires (via the vanilla <c>Pawn.learning</c> field, NOT
        /// <c>Pawn.needs.learning</c> — easy to get wrong) and delegates to
        /// the list-based overload. Returns false (never blocks) if the pawn
        /// has no <see cref="Pawn.learning"/> tracker at all (e.g. Biotech
        /// inactive, or somehow not a Child-stage pawn).
        /// </summary>
        public static bool ConflictsWithActiveLearningDesire(PlayDesireDef playDesire, Pawn pawn)
        {
            return ConflictsWithActiveLearningDesire(playDesire, pawn.learning?.ActiveLearningDesires);
        }
    }
}
