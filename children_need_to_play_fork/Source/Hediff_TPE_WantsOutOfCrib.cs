using RimWorld;
using Verse;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Applied to toddlers who have been confined long enough to want out.
    /// Drives the <see cref="MentalState_WantsOutOfCrib"/> mental state (crying,
    /// mood debuff) and signals caregivers to let the toddler out.
    ///
    /// Duration: ~45 game minutes with random variation. Removed immediately
    /// if the toddler is let out of the crib. When removed (timeout or rescue),
    /// the parent <see cref="Hediff_TPE_CribConfinement"/> cycle resets to its
    /// 3-hour grace period.
    /// </summary>
    public class Hediff_TPE_WantsOutOfCrib : Hediff
    {
        // Range: 1.45h → 3625 ticks,  2.5h → 6250 ticks (1 game hour = 2500 ticks)
        const int BaseDurationTicks = 4937;   // midpoint
        const int DurationVariation = 1313;   // half-range

        // Tick at which this hediff auto-removes. -1 = not yet initialised.
        int endTick = -1;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            endTick = Find.TickManager.TicksGame
                      + BaseDurationTicks
                      + Rand.Range(-DurationVariation, DurationVariation);

            // FIX P4-1: Do not start a duplicate mental state if the pawn is
            // already in (or recovering from) this mental state.
            var msh = pawn.mindState?.mentalStateHandler;
            if (msh?.CurState is not MentalState_WantsOutOfCrib)
            {
                msh?.TryStartMentalState(
                    TPE_MentalStateDefOf.TPE_WantsOutOfCrib,
                    causedByMood: false,
                    forceWake: false);
            }
        }

        public override void Tick()
        {
            base.Tick();
            // FIX R2: Only check every 60 ticks to avoid calling
            // CribUtility.InCrib (which scans ThingList) every single tick.
            if (!pawn.IsHashIntervalTick(60)) return;

            // FIX P1-5: Safety fallback if endTick was never set (missing save
            // data, corruption, or constructor path). Give a full duration.
            if (endTick < 0)
                endTick = Find.TickManager.TicksGame + BaseDurationTicks;

            // Remove immediately if the toddler is no longer in a crib
            // (caregivers successfully let them out)
            if (!CribUtility.InCrib(pawn))
            {
                pawn.health?.RemoveHediff(this);
                return;
            }

            // Time's up — remove and let the confinement cycle restart
            if (Find.TickManager.TicksGame >= endTick)
            {
                pawn.health?.RemoveHediff(this);
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            // Notify the confinement hediff to reset its cycle so the toddler
            // gets a fresh 3-hour grace period before frustration builds again.
            if (pawn.health?.hediffSet != null)
            {
                var confinement = pawn.health.hediffSet
                    .GetFirstHediffOfDef(TPE_HediffDefOf.TPE_CribConfinement)
                    as Hediff_TPE_CribConfinement;
                confinement?.ResetCycle();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref endTick, "endTick", -1);
        }
    }
}
