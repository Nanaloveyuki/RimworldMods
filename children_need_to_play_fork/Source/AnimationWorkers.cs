using UnityEngine;
using Verse;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Rotates the toddler 360° over the animation duration while they are
    /// performing the "Spinning Around" play job. Hooks into the Toddlers mod
    /// animation system via <see cref="AnimationWorker_Toddler"/>.
    /// </summary>
    public class AnimationWorker_ToddlerSpinning : AnimationWorker_Toddler
    {
        public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            // Render-level gate for "Disable toddler play animations": the job driver
            // never STARTS the spin while it's disabled, but a spin already running when
            // the player flips the setting must stop on the next frame (incl. paused) —
            // returning false here applies no rotation at all. Runs on the parallel
            // pre-draw thread: a static bool read only (see PlayAnimationUtility).
            if (PlayAnimationUtility.ToddlerAnimationsDisabled)
                return false;
            if (!base.Enabled(def, node, part, parms))
                return false;
            return parms.pawn.CurJobDef == TPE_JobDefOf.TPE_ToddlerSpinning;
        }

        public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            // Continuous 360° rotation over the animation duration
            return (float)tick / (float)def.durationTicks * 360f;
        }
    }

    // ===========================
    // PILLOW NESTING
    // ===========================
    // PILLOW NESTING ANIMATION WORKER — SHELVED FOR LATER
    // Commented out due to animation caching/rendering issues.
    // To restore: uncomment the entire class below.
    /*
    /// <summary>
    /// Animation worker for Pillow Nesting: reads the current body-angle
    /// from JobDriver_ToddlerPillowNesting.angle and returns it every rendering frame.
    ///
    /// DESIGN: Mirrors base Toddlers' AnimationWorker_LayAngleInCrib exactly.
    /// The job driver stores the angle; this worker just reads and returns it.
    /// The animation system calls this every frame, so the angle updates
    /// naturally as the job driver modifies the field. The animation itself
    /// is set ONCE and never restarted.
    /// </summary>
    public class AnimationWorker_ToddlerPillowNesting : AnimationWorker_Toddler
    {
        public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            if (!base.Enabled(def, node, part, parms))
                return false;
            // Enable only when doing Pillow Nesting activity and at the correct toil.
            // Matches base mod's LayAngleInCrib pattern exactly.
            if (parms.pawn.CurJobDef == TPE_JobDefOf.TPE_ToddlerPillowNesting
                && parms.pawn.jobs?.curDriver?.CurToilString == "PillowNesting")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Return the current body angle from the job driver.
        /// Called every rendering frame by the animation system.
        /// Simply reads the angle field; never restarts the animation.
        /// </summary>
        public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            // Cast to our job driver and read the angle field.
            // If the cast fails, return 0 (no rotation).
            JobDriver_ToddlerPillowNesting? driver = parms.pawn.jobs?.curDriver as JobDriver_ToddlerPillowNesting;
            if (driver == null)
                return 0f;

            // Return the angle. This is queried every frame, so updates to driver.angle
            // will be reflected immediately without needing to restart the animation.
            return driver.angle;
        }
    }
    */

}
