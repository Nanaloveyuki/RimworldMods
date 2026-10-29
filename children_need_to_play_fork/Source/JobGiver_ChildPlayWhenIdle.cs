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
    /// <para><b>Still respects the Learning/Recreation split:</b> exactly like
    /// <see cref="JobGiver_ChildPlay"/> and <see cref="ThinkNode_Priority_ChildPlay"/>,
    /// this refuses to act while the child's current schedule slot is
    /// <see cref="TPE_TimeAssignmentDefOf.TPE_Learning"/> — dedicated learning time
    /// stays learning-only (or a plain idle wander if there's truly nothing to learn),
    /// never play, even when idle. It also mirrors vanilla <c>JobGiver_IdleJoy</c>'s own
    /// <c>JoyUtility.TimetablePreventsGettingJoy</c> check, so an idle child during
    /// Work-scheduled hours still just wanders — matching how idle adults don't get
    /// random joy activities during Work hours either.</para>
    /// </summary>
    public class JobGiver_ChildPlayWhenIdle : ThinkNode_JobGiver
    {
        /// <summary>
        /// Same "let the world settle" grace period <see cref="JobGiver_ChildPlay"/>
        /// uses, so idle play can't fire on the very first tick of a freshly
        /// generated/loaded map.
        /// </summary>
        const int GameStartNoPlayTicks = 5000;

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

        /// <summary>
        /// Returns true if this idle child should be offered a play job right now.
        /// </summary>
        static bool IsEligible(Pawn pawn)
        {
            // Master opt-in — default OFF.
            if (TPE_Mod.Settings?.playWhenIdle != true)
                return false;

            // Without the play-need system there are no PlayDesireDefs worth offering
            // (and no Need_ChildPlay to fill), so there's nothing meaningful to do.
            if (TPE_Mod.Settings?.enableChildrenPlayNeed != true)
                return false;

            if (pawn == null || pawn.Dead || pawn.Downed)
                return false;

            if (Find.TickManager.TicksGame < GameStartNoPlayTicks)
                return false;

            if (!pawn.DevelopmentalStage.Child())
                return false;

            // Defensive: only pawns actually enrolled in our play-need system (player-
            // faction children — see Patch_Pawn_NeedsTracker_AddOrRemoveNeedsAsAppropriate
            // in HarmonyPatches.cs) have this need at all. Guests/prisoners/other-faction
            // children never get it, so they're correctly excluded here too.
            if (pawn.needs?.TryGetNeed(TPE_ChildPlayDefOf.TPE_ChildPlay) == null)
                return false;

            TimeAssignmentDef current = pawn.timetable?.CurrentAssignment ?? TimeAssignmentDefOf.Anything;

            // Dedicated learning time stays learning-only (or a plain idle wander) —
            // never play, even when there's genuinely nothing left to learn. Mirrors
            // ThinkNode_Priority_ChildPlay's own TPE_Learning suppression so the
            // Learning/Play schedule split holds even at the idle-fallback level.
            if (current == TPE_TimeAssignmentDefOf.TPE_Learning)
                return false;

            // Mirrors vanilla JobGiver_IdleJoy's own gate — no idle play during
            // Work-assigned hours, matching how idle adults don't get random joy
            // activities while scheduled to Work either.
            if (JoyUtility.TimetablePreventsGettingJoy(pawn))
                return false;

            return true;
        }
    }
}
