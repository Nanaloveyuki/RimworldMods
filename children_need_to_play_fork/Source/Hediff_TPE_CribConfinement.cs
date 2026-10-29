using RimWorld;
using Verse;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Tracks how long a toddler has been confined in a crib.
    ///
    /// Cycle:
    /// 1. Toddler placed in crib → 1-hour grace period (severity stays at 0).
    /// 2. After grace period → severity climbs from 0→1 over ~4 hours awake time.
    /// 3. At severity 1.0 → <see cref="Hediff_TPE_WantsOutOfCrib"/> is applied.
    /// 4. That hediff drives crying mental state and caregiver rescue.
    /// 5. When "wants out" hediff ends (timeout or rescue) → ResetCycle() is called
    ///    and the 1-hour grace period starts over.
    ///
    /// While the "wants out" hediff is active, this hediff is <b>frozen at 1.0</b>
    /// so the health tab stays consistent and severity does not decay during the
    /// outburst phase.
    ///
    /// Sleep behavior: when the toddler falls asleep in their crib, both severity
    /// and the grace-period timer are paused. When they wake up, accumulation
    /// resumes from exactly where it left off.
    /// </summary>
    public class Hediff_TPE_CribConfinement : Hediff
    {
        // 1 game hour = 2500 ticks
        const int GracePeriodTicks = 2500;
        // ~4 hours awake to climb 0→1 after grace period
        const float TicksToTrigger = 10000f;
        // 1 hour to decay when out of crib
        const float TicksToRecover = 2500f;

        // Tick when the toddler entered the crib (or when cycle was last reset)
        int timeEnteredCrib = -1;

        // Tracks when the toddler fell asleep in crib (-1 = not sleeping).
        // Used to push timeEnteredCrib forward on wake, pausing the grace timer.
        int sleepStartTick = -1;

        public override bool ShouldRemove
        {
            get
            {
                if (Severity > 0.001f) return false;
                // Keep hediff while asleep in crib so accumulation can resume on wake
                if (pawn != null && CribUtility.InCrib(pawn) && !pawn.Awake()) return false;
                return !ShouldAccumulate;
            }
        }

        /// <summary>
        /// Returns true when the toddler is awake, in a crib, and the
        /// "wants out" hediff is not already active. Crawling and walking
        /// toddlers both accumulate restlessness.
        ///
        /// Accumulation continues at the normal rate even when the toddler is
        /// actively playing via <see cref="JobDriver_ToddlerCribJumping"/>
        /// (standing bounce in crib). The toddler is still confined to the crib,
        /// so restlessness builds regardless of whether they are lying still or
        /// jumping.
        /// </summary>
        bool ShouldAccumulate
        {
            get
            {
                if (pawn == null || !pawn.Spawned) return false;
                if (!pawn.Awake()) return false;
                if (!CribUtility.InCrib(pawn)) return false;
                // Block accumulation while the "wants out" hediff is active
                if (pawn.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib) == true)
                    return false;
                return true;
            }
        }

        /// <summary>
        /// Returns true when the toddler has the crib-lock hediff. While locked,
        /// restlessness severity must never decrease (even when temporarily out
        /// of the crib for feeding). It only decreases after the lock is removed.
        /// </summary>
        bool IsCribLocked => pawn?.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_CribLocked) == true;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            Severity = 0f;
            timeEnteredCrib = Find.TickManager.TicksGame;
            sleepStartTick = -1;
        }

        public override void Tick()
        {
            base.Tick();
            if (!pawn.IsHashIntervalTick(60)) return;

            // FIX N3: If timeEnteredCrib was never set (loaded from an old save
            // where the field didn't exist, or some edge-case construction path),
            // start the grace period from now instead of skipping it entirely.
            if (timeEnteredCrib < 0)
                timeEnteredCrib = Find.TickManager.TicksGame;

            // Track sleep-in-crib state and push the grace timer forward so that
            // sleep time does not count against the grace period or accumulation.
            bool inCrib = CribUtility.InCrib(pawn);
            bool awake = pawn.Awake();

            if (inCrib && !awake && sleepStartTick < 0)
            {
                // Just fell asleep in crib — start tracking sleep
                sleepStartTick = Find.TickManager.TicksGame;
            }
            else if (inCrib && awake && sleepStartTick >= 0)
            {
                // Woke up in crib — push timer forward by sleep duration
                timeEnteredCrib += Find.TickManager.TicksGame - sleepStartTick;
                sleepStartTick = -1;
            }
            else if (!inCrib && sleepStartTick >= 0)
            {
                // Left crib while sleeping — stop tracking, don't push timer
                sleepStartTick = -1;
            }

            // CRITICAL FIX P1-1: While the "wants out" hediff is active, pin
            // severity at 1.0. Do not accumulate or decay. The wants-out hediff
            // owns this phase; when it ends (timeout or rescue) it calls
            // ResetCycle() which sets severity back to 0.
            if (pawn.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib) == true)
            {
                Severity = 1f;
                return;
            }

            if (ShouldAccumulate)
            {
                int elapsed = Find.TickManager.TicksGame - timeEnteredCrib;
                if (elapsed >= GracePeriodTicks)
                {
                    Severity += 60f / TicksToTrigger;
                    if (Severity > 1f) Severity = 1f;
                }

                // Trigger "wants out" hediff at max severity
                if (Severity >= 1f
                    && pawn.health?.hediffSet != null
                    && !pawn.health.hediffSet.HasHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib))
                {
                    pawn.health.AddHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib);
                }
            }
            else if (!IsCribLocked)
            {
                // Only decay if not locked, not asleep in crib, and not
                // actively jumping in crib (play pauses frustration/decay).
                bool asleepInCrib = inCrib && !awake;
                bool jumpingInCrib = pawn.jobs?.curDriver is JobDriver_ToddlerCribJumping;
                if (!asleepInCrib && !jumpingInCrib)
                {
                    Severity -= 60f / TicksToRecover;
                    if (Severity < 0f) Severity = 0f;
                }
            }
            // If IsCribLocked: severity stays frozen at current value

            UpdateStageThoughts();
        }

        /// <summary>
        /// Called when the "wants out" hediff is removed (timeout or rescue).
        /// Resets severity to 0 and restarts the 1-hour grace period.
        /// </summary>
        public void ResetCycle()
        {
            Severity = 0f;
            timeEnteredCrib = Find.TickManager.TicksGame;
            sleepStartTick = -1;
        }

        /// <summary>
        /// Applies or removes stage-specific mood thoughts based on current severity.
        /// Content (0): none. Restless (0→0.5): -1. Frustrated (0.5→1): -2.
        /// Wants-out phase is handled separately by the mental state.
        /// </summary>
        void UpdateStageThoughts()
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;
            var mem = pawn.needs.mood.thoughts.memories;

            // Wants-out phase: clean up stage thoughts; mental state handles its own
            if (pawn.health?.hediffSet?.HasHediff(TPE_HediffDefOf.TPE_WantsOutOfCrib) == true
                || Severity >= 1f)
            {
                mem.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribRestless);
                mem.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribFrustrated);
                return;
            }

            if (Severity >= 0.5f)
            {
                mem.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribRestless);
                TryAddStageThought(TPE_ThoughtDefOf.TPE_CribFrustrated);
            }
            else if (Severity > 0.001f)
            {
                mem.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribFrustrated);
                TryAddStageThought(TPE_ThoughtDefOf.TPE_CribRestless);
            }
            else
            {
                mem.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribRestless);
                mem.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribFrustrated);
            }
        }

        static void TryAddStageThought(Pawn pawn, ThoughtDef def)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;
            if (pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(def) != null) return;
            Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(def);
            thought.permanent = true;
            pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }

        void TryAddStageThought(ThoughtDef def) => TryAddStageThought(pawn, def);

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;
            pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribRestless);
            pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(TPE_ThoughtDefOf.TPE_CribFrustrated);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref timeEnteredCrib, "timeEnteredCrib", -1);
            Scribe_Values.Look(ref sleepStartTick, "sleepStartTick", -1);
        }


    }
}
