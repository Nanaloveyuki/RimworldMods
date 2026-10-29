using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// "Play when Idle" — an opt-in (default OFF) <see cref="ThinkNode_JobGiver"/>
    /// spliced into vanilla's own "Idle colonist" branch of the <c>Humanlike</c>
    /// ThinkTreeDef via <c>Patches/ThinkTree_ChildPlayWhenIdle.xml</c>. It is inserted
    /// as a sibling immediately BEFORE <c>JobGiver_WanderColony</c> and immediately
    /// AFTER vanilla's own <c>JobGiver_IdleJoy</c> attempt.
    ///
    /// <para><b>Why this is needed:</b> children never have a vanilla Joy need —
    /// <c>NeedDefOf.Joy</c> declares <c>developmentalStageFilter="Adult"</c> — so
    /// <c>JobGiver_IdleJoy</c> bails out immediately (<c>pawn.needs.joy == null</c>)
    /// and always returns null for them. That means every time a child reaches this
    /// point in the think tree with nothing else to do — no food/rest/chemical need,
    /// no active learning desire, no eligible job from the normal
    /// <see cref="JobGiver_ChildPlay"/> priority slot, no work — they fall straight
    /// through to <c>JobGiver_WanderColony</c> and wander aimlessly instead of playing.
    /// </para>
    ///
    /// <para><b>What this does instead:</b> when the "Play when Idle" mod setting is
    /// ON, a truly-idle child is offered a play job instead of a wander job —
    /// deliberately ignoring the play need's normal fullness cap (the regular
    /// <see cref="JobGiver_ChildPlay"/> refuses once play is ≥95%, via its
    /// <c>CanPlayRightNow</c> check). There is nothing else left for the child to do at
    /// this point in the tree, so there's no downside to letting an already-satisfied
    /// child keep playing instead of wandering in circles.</para>
    ///
    /// <para>Uses the same schedule/health policy as ordinary and urgent play:
    /// only Anything/Recreation hours, never Work, Sleep or dedicated Learning.
    /// Idle play additionally ignores fullness, but yields to any prioritized job.
    /// Eligibility does not ban prisoners; assigned labor remains protected.</para>
    /// </summary>
    public class JobGiver_ChildPlayWhenIdle : ThinkNode_JobGiver
    {

        /// <summary>
        /// Scratch list used to shuffle candidate desires without allocating on every
        /// call. Deliberately a separate field from <see cref="JobGiver_ChildPlay"/>'s
        /// own scratch list — that one is private to its class and this is a distinct
        /// ThinkNode instance/class anyway.
        /// </summary>
        static readonly List<PlayDesireDef> tmpDesires = new List<PlayDesireDef>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!IsEligible(pawn))
                return null;

            // --- Preferred pool: the child's currently active (rolled) play desires,
            // same pool JobGiver_ChildPlay draws from — so idle play still "counts" as
            // pursuing a desire the child already wants to do, when one is available. ---
            Pawn_PlayTracker? tracker = Current.Game
                .GetComponent<GameComponent_PlayTracker>()
                ?.GetTracker(pawn);

            Job? job = TryFromDesireList(tracker?.ActivePlayDesires, pawn, checkCanGiveDesire: false);
            if (job == null)
            {
                // --- Fallback pool: every registered play desire. Guarantees an idle child
                // still plays SOMETHING instead of wandering even if neither of their 2
                // currently-active desires happens to be feasible right now (e.g. no toy
                // nearby, no powered TV). Mirrors vanilla JobGiver_IdleJoy trying every
                // JoyGiverDef in the database rather than only a pre-rolled subset. ---
                job = TryFromDesireList(DefDatabase<PlayDesireDef>.AllDefsListForReading, pawn, checkCanGiveDesire: true);
            }

            // NOTE: this giver is the ONLY thing in the whole mod that can hand a play
            // job to a child whose play need is already satisfied (that's the entire
            // point of "Play when Idle" -- ignore the fullness cap). Every play
            // JobDriver calls PlayUtility.PlayTickCheckEnd on a timer, which would
            // normally end the job the instant it sees play already satisfied -- fine
            // for a NORMAL play session (which can never start already-satisfied, since
            // JobGiver_ChildPlay refuses to hand one out past 95%), but catastrophic
            // here. PlayTickCheckEnd handles this automatically: it recognises an
            // "idle-triggered" job by checking whether pawn.CurJob.jobGiver is THIS
            // class (see PlayUtility.IsIdleTriggeredJob) -- Pawn_JobTracker.StartJob
            // sets that field to whichever ThinkNode produced the job, for every job,
            // automatically, the moment this method returns one. No extra bookkeeping
            // call is needed here at all.
            if ((TPE_Mod.Settings?.debugLogging ?? false) && job != null)
            {
                Log.Message("[TPE DEBUG] " + pawn.LabelShort + ": Play when Idle giving job " + job +
                    " (play satisfied=" + PlayUtility.PlaySatisfied(pawn) + ")");
            }

            return job;
        }

        /// <summary>
        /// Shuffles <paramref name="desires"/> and returns the first job any of them can
        /// produce, or null if none can. When <paramref name="checkCanGiveDesire"/> is
        /// true, desires whose <see cref="PlayDesireGiver.CanGiveDesire"/> is false
        /// (e.g. Radiotalking before Microelectronics is researched) are skipped —
        /// this mirrors the exact filter <c>Pawn_PlayTracker.AddNewPlayDesire</c> uses
        /// when rolling a pawn's active desires, so the "try everything" fallback can
        /// never hand out a desire that was never eligible to be rolled in the first
        /// place. This check is skipped for the "active desires" pool because those
        /// desires were already filtered by <c>CanGiveDesire</c> at roll time.
        ///
        /// BUGFIX (caught in review): the same reasoning applies to the Play↔Learning
        /// mutual-exclusion check (<see cref="PlayUtility.ConflictsWithActiveLearningDesire(PlayDesireDef,Pawn)"/>)
        /// — without it here too, this fallback could still hand an idle child a
        /// ONE-OFF job for an activity (e.g. Floor Drawing) they currently hold as
        /// an active LEARNING desire, even though that same activity could never
        /// have been rolled into their tracked ActivePlayDesires in the first
        /// place. Also gated behind <paramref name="checkCanGiveDesire"/> for the
        /// same reason: the "active desires" pool was already exclusion-checked at
        /// roll time, so re-checking it here would be redundant (not wrong, just
        /// wasted work on the hot idle-tick path).
        /// </summary>
        static Job? TryFromDesireList(List<PlayDesireDef>? desires, Pawn pawn, bool checkCanGiveDesire)
        {
            if (desires.NullOrEmpty())
                return null;

            tmpDesires.Clear();
            tmpDesires.AddRange(desires.InRandomOrder());

            for (int i = 0; i < tmpDesires.Count; i++)
            {
                PlayDesireDef desire = tmpDesires[i];
                if (checkCanGiveDesire)
                {
                    if (!desire.Worker.CanGiveDesire(pawn))
                        continue;
                    if (PlayUtility.ConflictsWithActiveLearningDesire(desire, pawn))
                        continue;
                }

                if (desire.Worker.CanDo(pawn))
                {
                    Job? job = desire.Worker.TryGiveJob(pawn);
                    if (job != null)
                    {
                        tmpDesires.Clear();
                        return job;
                    }
                }
            }

            tmpDesires.Clear();
            return null;
        }

        static bool IsEligible(Pawn pawn)
        {
            return TPE_Mod.Settings?.playWhenIdle == true
                && PlayScheduleUtility.CanSelectChildPlay(pawn, ignoreFullness: true);
        }
    }
}
