using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    // ==========================================================
    // Shared Enabled() rule for the three child Active Play workers
    // ==========================================================
    /// <summary>
    /// One definition of "may a child Active Play animation render right now", shared by
    /// all three workers below so they can't drift apart.
    /// </summary>
    internal static class ChildAnimationWorkerUtility
    {
        /// <summary>
        /// Render-level gate. Called by the engine for every animated render node every
        /// frame, on the PARALLEL pre-draw thread (PawnRenderTree.ParallelPreDraw) — so it
        /// only reads a static settings bool and two fields.
        /// <list type="number">
        ///   <item><description>"Disable child play animations" ON ⇒ false. The job driver
        ///   never STARTS these animations while it's ON; this makes one that was already
        ///   running when the player flipped the setting stop on the next frame, even
        ///   while paused.</description></item>
        ///   <item><description>Downed ⇒ false unless the def allows playing while downed.
        ///   BUGFIX: the old per-worker check was <c>!def.playWhenDowned || !Downed</c>,
        ///   which is ALWAYS true for these defs (all have playWhenDowned=false), i.e. it
        ///   never suppressed anything — the opposite of its own doc comment. Correct form
        ///   (same as the base Toddlers mod's AnimationWorker_Toddler): <c>playWhenDowned
        ///   || !Downed</c>.</description></item>
        /// </list>
        /// </summary>
        internal static bool Enabled(AnimationDef def, PawnDrawParms parms)
        {
            if (PlayAnimationUtility.ChildAnimationsDisabled)
                return false;
            return def.playWhenDowned || !parms.pawn.Downed;
        }
    }

    // ==========================================================
    // AnimationWorker_ChildSpinning
    // ==========================================================
    /// <summary>
    /// Vanilla AnimationWorker for the "Spinning Around" sub-activity.
    /// Rotates the Root render node continuously through 360 degrees.
    /// The angle is computed as a fraction of the AnimationDef's duration,
    /// so one full spin takes exactly <c>durationTicks</c> ticks.
    /// </summary>
    public class AnimationWorker_ChildSpinning : BaseAnimationWorker
    {
        /// <summary>
        /// Returns true unless child play animations are disabled, or the pawn is
        /// downed and the def forbids it. See <see cref="ChildAnimationWorkerUtility.Enabled"/>.
        /// </summary>
        public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return ChildAnimationWorkerUtility.Enabled(def, parms);
        }

        /// <summary>
        /// Computes the Y-axis rotation angle for the current tick.
        /// 0 → 360 degrees over the course of one animation loop.
        /// </summary>
        public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return (tick / (float)def.durationTicks) * 360f;
        }

        public override Vector3 OffsetAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return Vector3.zero;
        }

        public override Vector3 ScaleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return Vector3.one;
        }

        public override GraphicStateDef GraphicStateAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return null;
        }

        public override void PostDraw(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms, Matrix4x4 matrix)
        {
        }
    }

    // ==========================================================
    // AnimationWorker_ChildJumping
    // ==========================================================
    /// <summary>
    /// Marker AnimationWorker for the "Jumping Up and Down" sub-activity.
    ///
    /// The vanilla AnimationDef offset system does NOT support visible vertical
    /// displacement for standing pawns — no vanilla animation ever uses a
    /// non-zero Z in <c>OffsetAtTick</c> for this purpose.  Real vertical
    /// bounce is injected via a Harmony postfix on <c>PawnRenderer.GetBodyPos</c>
    /// (see <see cref="Patch_PawnRenderer_GetBodyPos"/>) by adding to the Z
    /// component, which is the screen-vertical axis in RimWorld's isometric
    /// camera.  This worker exists only so the renderer knows the pawn is in
    /// "jump" mode (<c>CurAnimation</c> check).
    /// </summary>
    public class AnimationWorker_ChildJumping : BaseAnimationWorker
    {
        /// <summary>
        /// Returns true unless child play animations are disabled, or the pawn is
        /// downed and the def forbids it. See <see cref="ChildAnimationWorkerUtility.Enabled"/>.
        /// (This worker applies no transform itself; the bounce lives in the
        /// GetBodyPos postfix, which has its own identical settings gate.)
        /// </summary>
        public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return ChildAnimationWorkerUtility.Enabled(def, parms);
        }

        /// <summary>
        /// No yaw rotation while jumping — the DrawPos patch handles all motion.
        /// </summary>
        public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return 0f;
        }

        /// <summary>
        /// Returns zero — vertical bounce is done in the DrawPos Harmony patch
        /// because the animation offset system cannot produce visible Y movement.
        /// </summary>
        public override Vector3 OffsetAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return Vector3.zero;
        }

        public override Vector3 ScaleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return Vector3.one;
        }

        public override GraphicStateDef GraphicStateAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return null;
        }

        public override void PostDraw(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms, Matrix4x4 matrix)
        {
        }
    }

    // ==========================================================
    // AnimationWorker_ChildSnailCrawling
    // ==========================================================
    /// <summary>
    /// Vanilla AnimationWorker for the "Snail Crawling" sub-activity.
    /// Leans the Root render node forward to mimic a crawling posture.
    /// 
    /// RimWorld's animation system applies <c>AngleAtTick</c> as a Y-axis rotation
    /// (yaw). From the isometric camera, a Y rotation on a horizontally-facing
    /// sprite (East/West) appears as a forward/back lean. Therefore the lean is
    /// only applied when the pawn faces East or West; North/South get no angle
    /// because yaw rotation on vertical sprites reads as a sideways tilt.
    /// 
    /// This mirrors the approach used by the base Toddlers mod for bug watching
    /// (<c>AnimationWorker_Bugwatch</c>), which tilts toddlers sideways via yaw.
    /// </summary>
    public class AnimationWorker_ChildSnailCrawling : BaseAnimationWorker
    {
        /// <summary>
        /// Returns true unless child play animations are disabled, or the pawn is
        /// downed and the def forbids it. See <see cref="ChildAnimationWorkerUtility.Enabled"/>.
        /// </summary>
        public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return ChildAnimationWorkerUtility.Enabled(def, parms);
        }

        /// <summary>
        /// Returns a forward-lean angle based on facing direction.
        ///
        /// Sign convention matches the base Toddlers mod exactly
        /// (<see cref="Toddlers.AnimationWorker_Bugwatch"/> and
        /// <see cref="Toddlers.AnimationWorker_ToddlerCrawl_Root"/>):
        /// <list type="bullet">
        ///   <item><description>East:  +45° — top of sprite rotates toward the camera (forward lean).</description></item>
        ///   <item><description>West:  -45° — mirrored forward lean.</description></item>
        ///   <item><description>Other: 0°   — no lean on vertical sprites.</description></item>
        /// </list>
        /// </summary>
        public override float AngleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            if (parms.facing == Rot4.East)
                return 45f;
            if (parms.facing == Rot4.West)
                return -45f;
            return 0f;
        }

        public override Vector3 OffsetAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return Vector3.zero;
        }

        public override Vector3 ScaleAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return Vector3.one;
        }

        public override GraphicStateDef GraphicStateAtTick(int tick, AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms)
        {
            return null;
        }

        public override void PostDraw(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms, Matrix4x4 matrix)
        {
        }
    }
}
