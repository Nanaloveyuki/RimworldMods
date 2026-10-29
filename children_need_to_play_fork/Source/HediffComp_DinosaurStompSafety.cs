using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// CompProperties for <see cref="HediffComp_DinosaurStompSafety"/>.
    /// Must be top-level (not nested) for RimWorld's XML type resolver.
    /// </summary>
    public class CompProperties_DinosaurStompSafety : HediffCompProperties
    {
        public CompProperties_DinosaurStompSafety()
        {
            compClass = typeof(HediffComp_DinosaurStompSafety);
        }
    }

    /// <summary>
    /// Safety comp for the <see cref="TPE_HediffDefOf.TPE_DinosaurStomp"/> hediff.
    /// Checks every 200 ticks whether the pawn is still performing a dinosaur
    /// stomping activity (the main <see cref="JobDriver_ToddlerDinosaur"/> job
    /// or any of its sub-activities). If the pawn has moved on to a different
    /// job, the hediff is removed immediately.
    ///
    /// This is a broad-spectrum fix: instead of patching every possible edge
    /// case where the hediff might be left behind (job interruption, mental
    /// break, downing, etc.), we simply poll and clean up.
    ///
    /// Vanilla comparison:
    ///   - Similar to <see cref="HediffComp_Disappears"/> in concept (timed
    ///     removal), but conditional on job state rather than a flat timer.
    ///   - Much simpler than trying to hook every job-exit path individually.
    /// </summary>
    public class HediffComp_DinosaurStompSafety : HediffComp
    {
        /// <summary>Tick interval between safety checks.</summary>
        const int CheckInterval = 200;

        /// <summary>
        /// Called every tick by the hediff. We only act every
        /// <see cref="CheckInterval"/> ticks to keep overhead minimal.
        /// </summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent.pawn;
            if (pawn == null) return;

            if (!pawn.IsHashIntervalTick(CheckInterval))
                return;

            // If the pawn is still doing our dinosaur stomping job, all is well.
            if (pawn.jobs?.curDriver is JobDriver_ToddlerDinosaur)
                return;

            // Also accept if the pawn is doing a related sub-activity job that
            // should logically keep the hediff (e.g. any custom dino sub-job).
            // Currently only JobDriver_ToddlerDinosaur exists, but this pattern
            // makes future expansion safe.
            var curJobDef = pawn.CurJobDef;
            if (curJobDef != null && curJobDef.defName.Contains("Dinosaur"))
                return;

            // Pawn is no longer dinosaur stomping — remove the hediff.
            // Using RemoveHediff on ourselves is safe; RimWorld handles the
            // collection-modified-during-iteration case internally.
            pawn.health?.RemoveHediff(parent);
        }
    }
}
