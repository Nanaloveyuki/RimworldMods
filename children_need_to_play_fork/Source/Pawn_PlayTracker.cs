using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Per-pawn tracker for active play desires. Mirrors
    /// <see cref="Pawn_LearningTracker"/> from vanilla Biotech.
    ///
    /// Each child maintains a list of up to 2 active desires. Every
    /// <see cref="PlayDesireIntervalTicks"/> the list is refreshed with
    /// 2 new desires chosen from the child's eligible age bracket.
    /// </summary>
    public class Pawn_PlayTracker : IExposable
    {
        /// <summary>Maximum number of simultaneous active play desires.</summary>
        public const int MaxActivePlayDesires = 2;

        /// <summary>
        /// How many ticks between desire refreshes: 8 in-game hours (2500 ticks per
        /// hour = 20000 ticks), the documented design. Counted down 150 ticks at a
        /// time from Need_ChildPlay.NeedInterval, so it pauses while the need is frozen.
        /// BUGFIX: this was 48000 (= 19.2 hours) despite every comment, the in-game
        /// description and Architecture.md saying 8 hours.
        /// </summary>
        public const int PlayDesireIntervalTicks = 20000;

        /// <summary>The pawn this tracker belongs to.</summary>
        private Pawn pawn;

        /// <summary>Currently active play desires for this pawn.</summary>
        private List<PlayDesireDef> active = new List<PlayDesireDef>();

        /// <summary>Ticks remaining until the next desire refresh.</summary>
        private int newDesireTicksLeft;

        /// <summary>
        /// When true (dev-mode only), the pawn's play need is locked at 50%
        /// and will not decay or rise from play activities.
        /// </summary>
        public bool lockPlayNeed;

        /// <summary>Public accessor for the active desire list.</summary>
        public List<PlayDesireDef> ActivePlayDesires => active;

        /// <summary>Constructs an empty tracker (required for Scribe).</summary>
        public Pawn_PlayTracker()
        {
        }

        /// <summary>Constructs a tracker for the given pawn.</summary>
        public Pawn_PlayTracker(Pawn pawn)
        {
            this.pawn = pawn;
            // 0 causes immediate assignment on the first tick interval.
            newDesireTicksLeft = 0;
        }

        /// <summary>
        /// Called every <paramref name="delta"/> ticks. Counts down the refresh
        /// timer and assigns new desires when it expires.
        /// </summary>
        public void PlayTickInterval(int delta)
        {
            if (pawn == null || pawn.Dead)
                return;

            newDesireTicksLeft -= delta;
            if (newDesireTicksLeft <= 0)
            {
                RefreshDesires();
                newDesireTicksLeft = PlayDesireIntervalTicks;
            }
        }

        /// <summary>
        /// Clears the old desire list and fills it with 2 new desires
        /// appropriate for the pawn's current age.
        /// </summary>
        void RefreshDesires()
        {
            active.Clear();

            // ── Setting: guarantee Active Play ─────────────────────────────
            // When enabled, Active Play is always locked into one of the 2
            // slots (if the pawn is currently eligible for it at all — age
            // bracket + CanGiveDesire) instead of competing with everything
            // else in the normal weighted roll. Falls through to a normal
            // roll for that slot if ActivePlay isn't eligible right now (it's
            // age-gated 3–13, so in practice this only matters for very
            // unusual CanGiveDesire overrides — never for age alone). The
            // SECOND slot still rolls normally below via the standard loop —
            // AddNewPlayDesire's own "!active.Contains(d)" check already
            // prevents re-picking ActivePlay for that slot.
            if (TPE_Mod.Settings?.guaranteeActivePlayDesire == true)
            {
                PlayDesireDef activePlay = TPE_PlayDesireDefOf.TPE_ActivePlay;
                if (activePlay != null
                    && activePlay.Worker.CanGiveDesire(pawn)
                    && IsEligibleForAge(activePlay, pawn))
                {
                    active.Add(activePlay);
                }
            }

            for (int i = active.Count; i < MaxActivePlayDesires; i++)
            {
                AddNewPlayDesire();
            }
        }

        /// <summary>
        /// Adds one new random desire that the pawn is age-eligible for
        /// and that is not already in the active list.
        /// </summary>
        void AddNewPlayDesire()
        {
            // Snapshot once per call rather than per-candidate inside the
            // Where() below — pawn.learning's own list doesn't change mid-call,
            // and re-reading a field per LINQ evaluation is wasted work.
            List<LearningDesireDef>? activeLearningDesires = pawn.learning?.ActiveLearningDesires;

            var eligible = DefDatabase<PlayDesireDef>.AllDefsListForReading
                .Where(d => !active.Contains(d)
                    // Pawn-aware: lets desires gate themselves on colony-wide
                    // state (e.g. "has a TV/comms console/Bubble Machine been
                    // built anywhere") in addition to global state (research,
                    // DLC) — see PlayDesireGiver.CanGiveDesire and ColonyHasBuilding.
                    && d.Worker.CanGiveDesire(pawn)
                    && IsEligibleForAge(d, pawn)
                    // Mutual exclusion with vanilla Learning Desires of the same
                    // activity (e.g. don't hand out "floor drawing" as a play
                    // desire while the child already has it as an active
                    // LEARNING desire, and vice versa — see
                    // PlayDesireDef.conflictingLearningDesire, the shared helper
                    // PlayUtility.ConflictsWithActiveLearningDesire (also used by
                    // JobGiver_ChildPlayWhenIdle's fallback pool), and the Harmony
                    // patch on Pawn_LearningTracker.AddNewLearningDesire in
                    // HarmonyPatches.cs for the reverse direction). Both
                    // directions check the OTHER tracker's live, already-current
                    // state, so this stays correct even if both trackers happen
                    // to refresh on the exact same game tick: RimWorld ticks
                    // are processed strictly sequentially (never concurrently),
                    // so whichever roll runs second on a given tick always sees
                    // the first one's already-applied result, never a stale
                    // snapshot — no extra locking/ordering logic is needed to
                    // make same-tick refreshes race-safe.
                    && !PlayUtility.ConflictsWithActiveLearningDesire(d, activeLearningDesires))
                .ToList();

            if (eligible.Count == 0)
                return;

            PlayDesireDef chosen = eligible.RandomElementByWeight(d => d.selectionWeight);
            active.Add(chosen);
        }

        /// <summary>
        /// Returns true if <paramref name="def"/>'s age bracket includes
        /// the pawn's current biological age. When age gating is disabled
        /// in mod settings, every desire is considered eligible.
        /// </summary>
        static bool IsEligibleForAge(PlayDesireDef def, Pawn pawn)
        {
            if (!TPE_Mod.Settings.ageGatedPlayDesires)
                return true;

            int age = pawn.ageTracker.AgeBiologicalYears;
            if (def.minAge >= 0 && age < def.minAge)
                return false;
            if (def.maxAge >= 0 && age > def.maxAge)
                return false;
            return true;
        }

        /// <summary>
        /// Debug helper: forcefully sets the active desire list to a single def.
        /// </summary>
        public void Debug_SetPlayDesire(PlayDesireDef desire)
        {
            active.Clear();
            if (desire != null)
                active.Add(desire);
        }

        /// <summary>
        /// Serializes the active desire list and the refresh timer.
        /// </summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref newDesireTicksLeft, "newDesireTicksLeft", 0);
            Scribe_Values.Look(ref lockPlayNeed, "lockPlayNeed", false);
            Scribe_Collections.Look(ref active, "active", LookMode.Def);

            // After loading, ensure we have a valid pawn reference.
            // Scribe_Deep on the parent dictionary re-creates the tracker,
            // then sets the pawn field via the game component.
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (active == null)
                    active = new List<PlayDesireDef>();
            }
        }

        /// <summary>
        /// Must be called by <see cref="GameComponent_PlayTracker"/> after
        /// construction or loading to bind the pawn reference.
        /// </summary>
        public void BindPawn(Pawn pawn)
        {
            this.pawn = pawn;
        }
    }
}
