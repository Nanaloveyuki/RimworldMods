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

        // ══════════════════════════════════════════════════════════════════
        //  IDLE-PLAY JOB OVERRIDE CHECK
        //
        // Idle-triggered jobs (see above) can run for a long time despite the
        // child having nothing urgent left to do -- that was the whole point. But
        // if a GENUINELY more important job becomes available partway through
        // (the child gets hungry, gets tired, a learning desire kicks in, work
        // becomes available, etc.), the child should drop the idle play session
        // and go do that instead, rather than finishing out the full ~40s-3h
        // session first. RimWorld does NOT automatically re-check a pawn's whole
        // think tree while a job is already running -- a job just runs to
        // completion unless something explicitly calls
        // Pawn_JobTracker.CheckForJobOverride(). Vanilla itself uses exactly this
        // pattern for its own long-running relaxation jobs -- see
        // JobDriver_Reading's "if (pawn.IsHashIntervalTick(600, delta))
        // pawn.jobs.CheckForJobOverride(9.1f);" inside its own tick loop.
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// How often (in ticks) an idle-triggered play job re-checks whether a
        /// genuinely more important job has become available. Matches the
        /// explicit "every 50 ticks" ask.
        /// </summary>
        const int IdlePlayOverrideCheckInterval = 50;

        /// <summary>
        /// Minimum think-tree priority a competing job's source node must report
        /// for <see cref="Pawn_JobTracker.CheckForJobOverride"/> to let it
        /// interrupt an idle-triggered play job. Deliberately a small POSITIVE
        /// value rather than 0f: <c>CheckForJobOverride</c> treats a literal 0f
        /// as "skip the priority comparison entirely", which would let ANY
        /// different job win -- including a freshly re-rolled instance of idle
        /// play itself, or vanilla's own plain wander job, neither of which is
        /// "something more important". A ThinkNode only reports a priority AT
        /// ALL if it explicitly overrides <c>GetPriority</c> -- true of every
        /// real need-driven JobGiver in this game (food ~9.5, learning
        /// ~9.1/9.4, rest ~8, child play 8.5 or 9.15 when urgent, work ~2/5.5/9, etc.) but
        /// deliberately NOT true of sequential idle/wander-tier nodes like
        /// <c>JobGiver_WanderColony</c>, <c>JobGiver_IdleJoy</c>, or our own
        /// <see cref="JobGiver_ChildPlayWhenIdle"/> -- so those can never
        /// satisfy this check and can never self-interrupt. Any small
        /// strictly-positive value works; an exact 0f boundary is avoided for
        /// float-precision safety.
        /// </summary>
        const float MinPriorityToInterruptIdlePlay = 0.01f;

        /// <summary>
        /// Re-checks (roughly every <see cref="IdlePlayOverrideCheckInterval"/>
        /// ticks, staggered per-pawn via <c>IsHashIntervalTick</c> so many
        /// idle-playing children don't all re-check on the same tick) whether
        /// the pawn's CURRENT job -- if and only if it's an idle-triggered play
        /// job (see <see cref="IsIdleTriggeredJob"/>) -- should yield to
        /// something genuinely more important.
        ///
        /// Called from inside <see cref="PlayTickCheckEnd(Pawn,float,bool,int)"/>
        /// so every play JobDriver gets this behaviour for free just by calling
        /// PlayTickCheckEnd like they already must -- no per-driver wiring
        /// needed. Deliberately scoped to ONLY idle-triggered jobs: a normal,
        /// need-driven play session was already chosen because play was
        /// genuinely low, so it's out of scope for this check; only
        /// idle-triggered sessions (which run despite play already being
        /// satisfied, precisely because there was nothing better to do at the
        /// moment they were handed out) need to keep re-verifying that's still
        /// true.
        ///
        /// If nothing more important is available this is a cheap no-op:
        /// <c>CheckForJobOverride</c> re-runs the whole think tree, finds
        /// nothing whose source node reports a priority ≥
        /// <see cref="MinPriorityToInterruptIdlePlay"/>, and silently discards
        /// the redetermined result back to the job pool without ever touching
        /// the CURRENTLY-RUNNING job -- exactly the "if not, just continue on"
        /// half of the requested behaviour.
        ///
        /// If something MORE important IS found, this is NOT a mere
        /// notification -- <c>Pawn_JobTracker.CheckForJobOverride</c> itself
        /// calls <c>StartJob(newJob, JobCondition.InterruptOptional, ...)</c>
        /// synchronously and internally, which runs the full vanilla job-switch
        /// pipeline: <c>CleanupCurrentJob</c> releases every reservation the old
        /// (idle-play) job held, invokes <c>JobDriver.Cleanup</c> (which fires
        /// every <c>AddFinishAction</c> callback the play JobDriver registered --
        /// e.g. <see cref="JobDriver_ChildActivePlay"/>'s movement-hediff
        /// removal, <see cref="JobDriver_ChildBubblePopping"/>'s path-stop, the
        /// toy-carrying jobs' drop-and-unforbid cleanup -- exactly as if the job
        /// had ended any other way), then immediately assigns the pawn's new
        /// job and driver. By the time this method returns, the play session is
        /// ALREADY fully torn down and the pawn is ALREADY running the new job
        /// -- there is nothing further this method (or its caller) needs to do
        /// to "cancel" it. The caller only needs to notice the swap happened (by
        /// comparing <c>pawn.CurJob</c> before/after, see
        /// <see cref="PlayTickCheckEnd(Pawn,float,bool,int)"/>) so it doesn't
        /// mistakenly act on a job that's no longer running -- the actual
        /// cancel-and-switch is unconditional and automatic the moment a
        /// qualifying job is found, with no separate confirmation step.
        /// </summary>
        /// <param name="intervalDelta">
        /// How many ticks have elapsed since this pawn's job driver last ran its
        /// tick callback -- 1 for a plain per-tick <c>tickAction</c>, or the
        /// actual interval size (commonly ~250) for a <c>tickIntervalAction</c>.
        /// Needed so the <c>IsHashIntervalTick</c> check still reliably catches
        /// a 50-tick boundary even for job drivers that only tick a few times a
        /// second.
        /// </param>
        static void CheckForBetterJobWhileIdlePlaying(Pawn pawn, int intervalDelta)
        {
            if (pawn?.jobs == null)
                return;

            // Only idle-triggered jobs opt into this check.
            if (!IsIdleTriggeredJob(pawn))
                return;

            if (!pawn.IsHashIntervalTick(IdlePlayOverrideCheckInterval, intervalDelta))
                return;

            if (TPE_Mod.Settings?.debugLogging ?? false)
            {
                Log.Message("[TPE DEBUG] " + pawn.LabelShort + ": idle-play override check firing (tick " +
                    Find.TickManager.TicksGame + ", curJob=" + pawn.CurJob + ")");
            }

            pawn.jobs.CheckForJobOverride(MinPriorityToInterruptIdlePlay);

            if ((TPE_Mod.Settings?.debugLogging ?? false) && pawn.CurJob?.jobGiver is not JobGiver_ChildPlayWhenIdle)
            {
                Log.Message("[TPE DEBUG] " + pawn.LabelShort + ": idle-play override check INTERRUPTED the job — new curJob=" + pawn.CurJob);
            }
        }

        /// <summary>
        /// Overload that accepts a tick <paramref name="delta"/> (used by
        /// <c>tickIntervalAction</c> toils). Converts delta to the appropriate
        /// play-need amount, mirroring vanilla
        /// <see cref="LearningUtility.LearningTickCheckEnd(Pawn,int,bool)"/>.
        /// Play fills ~10× faster than learning. The same <paramref name="delta"/>
        /// is also threaded through as the idle-play override check's interval
        /// window (see <see cref="CheckForBetterJobWhileIdlePlaying"/>).
        /// </summary>
        public static bool PlayTickCheckEnd(Pawn pawn, int delta, bool forced = false)
        {
            // Vanilla learning: 1.2E-05f per tick. Play fills ~10x faster.
            float amount = 1.2E-04f * delta;
            return PlayTickCheckEnd(pawn, amount, forced, delta);
        }

        /// <summary>
        /// Fills the child's play need by <paramref name="amount"/> and checks
        /// whether the need has reached <see cref="SatisfiedThreshold"/>.
        /// If satisfied (and neither <paramref name="forced"/> nor the pawn's
        /// current job is idle-triggered — see <see cref="IsIdleTriggeredJob"/>),
        /// the current job is ended with <see cref="JobCondition.Succeeded"/>.
        /// Also periodically gives an idle-triggered job the chance to yield to
        /// something more important — see
        /// <see cref="CheckForBetterJobWhileIdlePlaying"/>.
        /// </summary>
        /// <param name="intervalDelta">
        /// Ticks elapsed since this driver's tick callback last ran (default 1
        /// for the common per-tick <c>tickAction</c> case). Only matters for the
        /// idle-play override-check's timing — see
        /// <see cref="CheckForBetterJobWhileIdlePlaying"/>.
        /// </param>
        /// <returns>True if the job ended (either because play is satisfied, or because it was just interrupted in favour of a more important job).</returns>
        public static bool PlayTickCheckEnd(Pawn pawn, float amount, bool forced = false, int intervalDelta = 1)
        {
            if (amount > 0f)
            {
                Need_ChildPlay? playNeed = pawn.needs?.TryGetNeed(
                    TPE_ChildPlayDefOf.TPE_ChildPlay) as Need_ChildPlay;
                playNeed?.Play(amount);
            }

            // Snapshot the job BEFORE the override-check: CheckForJobOverride can
            // SYNCHRONOUSLY interrupt and replace pawn.jobs.curJob with a brand
            // new job (see Pawn_JobTracker.StartJob). If that happens here, we
            // must NOT fall through to the satisfaction check below -- unlike
            // JobDriver.EndJobWith (which guards itself with a "is this still my
            // job" check), the direct pawn.jobs.EndCurrentJob call below has no
            // such guard, and would erroneously end the BRAND NEW job instead of
            // the one we were originally ticking.
            Job? jobBeforeOverrideCheck = pawn.CurJob;
            CheckForBetterJobWhileIdlePlaying(pawn, intervalDelta);
            if (pawn.CurJob != jobBeforeOverrideCheck)
            {
                // Idle play was just interrupted in favour of something more
                // important. Report "ended" so callers that gate further
                // per-tick logic on this return value (e.g. floor drawing's
                // filth-making step) correctly skip it instead of acting on a
                // job that is no longer running.
                return true;
            }

            // An idle-triggered job (see IsIdleTriggeredJob above) is treated
            // exactly like an explicitly forced call — it keeps running instead
            // of instantly ending the moment it notices play is already
            // satisfied.
            bool effectiveForced = forced || IsIdleTriggeredJob(pawn);

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
