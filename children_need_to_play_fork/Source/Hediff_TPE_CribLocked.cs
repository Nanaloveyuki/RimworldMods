using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Hidden hediff that marks a toddler as crib-locked. Overrides <see cref="Visible"/>
    /// so it never appears in the health tab or pawn inspect pane. The lock state is
    /// communicated to the player via the crib gizmo instead.
    /// </summary>
    public class Hediff_TPE_CribLocked : Hediff
    {
        /// <summary>
        /// Always returns false so the hediff is never shown in the UI.
        /// Code still detects it via <see cref="HediffSet.HasHediff"/> as normal.
        /// </summary>
        public override bool Visible => false;
    }
}
