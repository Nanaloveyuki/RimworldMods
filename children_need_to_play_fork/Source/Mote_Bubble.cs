using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Custom mote class for bubbles spawned by Bubble Machines.
    /// Bubbles float around slowly within a confined zone, drifting along gentle curves
    /// rather than straight lines or sudden direction snaps.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: All zone/movement math in this class is horizontal-only (X/Z plane).
    /// <see cref="Verse.Mote.DrawMote"/> forcibly overwrites <c>exactPosition.y</c> with
    /// the def's altitude-layer constant (e.g. ~10.24 for MoteOverhead) on every render
    /// call, so Y is NOT usable as "height above ground" here and must never be included
    /// in distance/boundary calculations, or those calculations get contaminated by that
    /// constant (which is large relative to the 15-tile zone radius).
    ///
    /// Each bubble is bidirectionally linked to the machine that spawned it: this bubble
    /// holds a reference to that machine via <see cref="SourceMachine"/>, and the machine
    /// (<c>CompBubbleZone.trackedBubbles</c>) holds a reference back to this bubble. That
    /// link is what lets each machine accurately count/cap only ITS OWN bubbles instead of
    /// any bubble that merely happens to be nearby (see <c>CompBubbleZone.trackedBubbles</c>
    /// remarks for the bug this replaced).
    ///
    /// Zone containment is a HARD cell-membership check against <see cref="validCells"/>, not
    /// a distance-based circle clamp. A closed room's actual shape is very often NOT a
    /// circle, so clamping purely by distance from a center point would let a bubble drift
    /// straight through a wall as long as it stayed within a fixed radius — checking real
    /// cell membership instead makes whatever shape the zone actually is (room or circle)
    /// the one true boundary. <see cref="zoneCenter"/>/<see cref="zoneRadius"/> still exist,
    /// but only drive a SOFT pre-emptive steering bias now, not the actual containment.
    /// </remarks>
    public class Mote_Bubble : Mote
    {
        /// <summary>
        /// The Bubble Machine comp that spawned this bubble. Set by <see cref="CompBubbleZone.TrySpawnBubble"/>
        /// immediately after spawning — this is the bubble-side half of the bidirectional
        /// ownership link (the machine's own half is <c>CompBubbleZone.trackedBubbles</c>).
        /// </summary>
        /// <remarks>
        /// Can legitimately be <c>null</c>: <see cref="CompBubbleZone.PostDeSpawn"/> nulls this
        /// out if the owning machine leaves the map (destroyed/deconstructed/picked up) while
        /// this bubble is still alive, so it never holds a dangling reference to a comp whose
        /// parent is gone. The bubble keeps floating and fading out normally either way — see
        /// that method's remarks for why it isn't force-destroyed when orphaned.
        /// </remarks>
        public CompBubbleZone SourceMachine;

        /// <summary>
        /// The exact set of cells this bubble is allowed to occupy, captured once at spawn
        /// time from CompBubbleZone's cached zone cells (whatever shape they are — a circular
        /// fill or an actual room's cells). This is the bubble's HARD containment boundary: a
        /// move is only ever applied if its destination cell is in this set (see
        /// <see cref="TimeInterval"/>). Null only in the defensive (should-never-happen) case
        /// of an empty zone, in which case containment is skipped entirely rather than
        /// permanently freezing the bubble in place.
        /// </summary>
        private HashSet<IntVec3> validCells;

        /// <summary>
        /// The centroid ("center of mass") of <see cref="validCells"/>. NOT a hard boundary —
        /// only used, together with <see cref="zoneRadius"/>, as a soft heuristic so the bubble
        /// still curves gently back toward the middle of the zone as it nears an edge, instead
        /// of only reacting once it's already hit the real (cell-based) wall.
        /// </summary>
        public Vector3 zoneCenter = Vector3.zero;

        /// <summary>
        /// The greatest horizontal distance from <see cref="zoneCenter"/> to any cell in
        /// <see cref="validCells"/>. Used only for the soft steering heuristic described above
        /// — NOT a hard clamp radius (that job belongs entirely to <see cref="validCells"/>).
        /// </summary>
        public float zoneRadius = 15f;

        /// <summary>
        /// Current horizontal velocity of the bubble in world tiles per tick (X component).
        /// </summary>
        private float velocityX;

        /// <summary>
        /// Current horizontal velocity of the bubble in world tiles per tick (Z component).
        /// </summary>
        private float velocityZ;

        /// <summary>
        /// Current horizontal heading of the bubble, in degrees (standard polar angle:
        /// 0 = +X, 90 = +Z). Wanders slightly every tick to produce smooth curved paths
        /// instead of straight-line segments with sudden direction changes.
        /// </summary>
        private float headingDegrees;

        /// <summary>
        /// Horizontal speed of the bubble, in world tiles per tick. Kept very small so
        /// bubbles drift lazily rather than "flying" around their zone.
        /// At 60 ticks/sec, 0.008 tiles/tick \u2248 0.48 tiles/sec.
        /// </summary>
        private const float BubbleSpeed = 0.008f;

        /// <summary>
        /// Maximum random change to <see cref="headingDegrees"/> per tick, in degrees.
        /// Small values produce gentle curves; this is intentionally tiny so the bubble's
        /// path bends smoothly instead of zig-zagging.
        /// </summary>
        private const float MaxSteerPerTick = 2.5f;

        /// <summary>
        /// How strongly (0\u20131 per tick, used as a Lerp factor) the heading is pulled toward
        /// the zone center once the bubble nears/crosses the zone boundary. Low values keep
        /// the turn-back gradual and curved rather than an abrupt bounce.
        /// </summary>
        private const float BoundarySteerStrength = 0.08f;

        /// <summary>
        /// Once the bubble's horizontal distance from <see cref="zoneCenter"/> exceeds this
        /// fraction of <see cref="zoneRadius"/>, boundary steering kicks in (before the hard clamp).
        /// </summary>
        private const float BoundarySteerThreshold = 0.85f;

        /// <summary>
        /// Overrides the base game's EndOfLife check so this mote's total on-screen
        /// duration can be randomized per-instance (8–15 seconds, set by
        /// CompBubbleZone via <see cref="Mote.solidTimeOverride"/> at spawn time).
        /// The base <c>Mote.EndOfLife</c> checks <c>def.mote.Lifespan</c>, which is
        /// derived purely from the DEF's static fadeInTime/solidTime/fadeOutTime and
        /// ignores any instance-level <c>solidTimeOverride</c> — that would desync it
        /// from the <c>Alpha</c> fade calculation (which DOES respect the override via
        /// the inherited <c>SolidTime</c> property), causing the bubble to either vanish
        /// mid-fade or sit invisible for a while before being destroyed. Recomputing
        /// EndOfLife from the same fadeInTime/SolidTime/fadeOutTime values used by Alpha
        /// keeps both perfectly in sync for any duration we assign.
        /// </summary>
        protected override bool EndOfLife => AgeSecs >= def.mote.fadeInTime + SolidTime + def.mote.fadeOutTime;

        /// <summary>
        /// Initializes the bubble with its zone's cells and a random starting heading.
        /// </summary>
        /// <param name="cells">Every cell in the machine's current bubble zone (room-shaped or circular).</param>
        /// <param name="cellSet">The same cells as <paramref name="cells"/>, pre-built as a HashSet for O(1) containment checks.</param>
        public void SetBubbleZone(List<IntVec3> cells, HashSet<IntVec3> cellSet)
        {
            validCells = cellSet;

            // Compute the zone's centroid and effective (soft-steer) radius from the actual
            // cell list, rather than assuming a circle around a machine position — this keeps
            // the gentle center-seeking bias sensible even for an irregular room shape.
            if (!cells.NullOrEmpty())
            {
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < cells.Count; i++)
                {
                    sum += cells[i].ToVector3Shifted();
                }
                Vector3 centroid = sum / cells.Count;

                float maxDistSq = 0f;
                for (int i = 0; i < cells.Count; i++)
                {
                    Vector3 cellPos = cells[i].ToVector3Shifted();
                    float dx = cellPos.x - centroid.x;
                    float dz = cellPos.z - centroid.z;
                    float distSq = dx * dx + dz * dz;
                    if (distSq > maxDistSq)
                    {
                        maxDistSq = distSq;
                    }
                }

                zoneCenter = centroid;
                zoneRadius = Mathf.Sqrt(maxDistSq);
            }
            else
            {
                // Defensive fallback: an empty zone means there's nothing meaningful to steer
                // toward or contain against. validCells stays null, which TimeInterval treats
                // as "skip containment/steering entirely" rather than trapping the bubble.
                zoneCenter = Vector3.zero;
                zoneRadius = 0f;
            }

            headingDegrees = Rand.Range(0f, 360f);
            UpdateVelocityFromHeading();
        }

        /// <summary>
        /// Recomputes the horizontal (X/Z) velocity components from <see cref="headingDegrees"/>.
        /// Called every tick since the heading continuously wanders.
        /// </summary>
        private void UpdateVelocityFromHeading()
        {
            velocityX = Mathf.Cos(headingDegrees * Mathf.Deg2Rad) * BubbleSpeed;
            velocityZ = Mathf.Sin(headingDegrees * Mathf.Deg2Rad) * BubbleSpeed;
        }

        /// <summary>
        /// Called when the mote is destroyed/despawned.
        /// Unregisters the bubble from the global bubble counter.
        /// </summary>
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // Unregister from global bubble count when despawning
            if (Map != null)
            {
                BubbleSettings.RemoveBubbleFromMap(Map);
            }

            // Tell the owning machine (if it's still around — see SourceMachine's remarks)
            // that this bubble is going away, so its tracked-bubble list stays accurate for
            // the per-machine cap, the low-bubble-count fast-spawn check, and the god-mode
            // inspect string. Null-conditional handles the orphaned case cleanly.
            SourceMachine?.NotifyBubbleDestroyed(this);

            base.Destroy(mode);
        }

        /// <summary>
        /// Called every tick via TimeInterval. Updates bubble heading/position (horizontal
        /// plane only) and applies the live-adjustable opacity setting.
        /// </summary>
        protected override void TimeInterval(float deltaTime)
        {
            base.TimeInterval(deltaTime);

            // Continuous gentle random steering: the heading wanders by a small random
            // amount every tick, producing smooth curved paths instead of straight-line
            // segments connected by sudden direction snaps.
            headingDegrees += Rand.Range(-MaxSteerPerTick, MaxSteerPerTick);

            if (validCells != null)
            {
                // Soft pre-emptive steering: bias the heading back toward the zone's centroid
                // once the bubble drifts past BoundarySteerThreshold of the zone's effective
                // radius, so it usually starts curving inward well before it would ever reach
                // the hard cell-membership boundary below. Purely cosmetic — validCells is
                // what actually guarantees containment, this just makes the turn-back look
                // like a smooth curve instead of only reacting right at the wall.
                float dx = exactPosition.x - zoneCenter.x;
                float dz = exactPosition.z - zoneCenter.z;
                float distanceFromCenter = Mathf.Sqrt(dx * dx + dz * dz);
                if (distanceFromCenter > zoneRadius * BoundarySteerThreshold)
                {
                    // Angle pointing FROM the bubble TOWARD the zone center.
                    float angleToCenter = Mathf.Atan2(-dz, -dx) * Mathf.Rad2Deg;
                    headingDegrees = Mathf.LerpAngle(headingDegrees, angleToCenter, BoundarySteerStrength);
                }
            }

            UpdateVelocityFromHeading();

            // Tentative new position from the current velocity (horizontal plane only —
            // exactPosition.y is intentionally never touched here; see class remarks for why).
            // Not applied yet — see the hard containment check below.
            float newX = exactPosition.x + velocityX;
            float newZ = exactPosition.z + velocityZ;

            // HARD containment: a move is only ever accepted if its destination cell is
            // actually part of validCells. This is what fixes bubbles visually escaping a
            // closed room — see class remarks for why a distance-based clamp isn't enough.
            bool moveAllowed = true;
            if (validCells != null)
            {
                IntVec3 destCell = new Vector3(newX, 0f, newZ).ToIntVec3();
                moveAllowed = validCells.Contains(destCell);
            }

            if (moveAllowed)
            {
                exactPosition.x = newX;
                exactPosition.z = newZ;
            }
            else
            {
                // Blocked by the zone boundary: don't move this tick, and snap the heading
                // straight at the centroid (a hard override, not just a gentle Lerp) so the
                // bubble reliably turns around and heads back inward on the very next tick
                // instead of repeatedly bumping into the same wall from the same angle.
                //
                // Note for a concave zone (e.g. a U-shaped room): the geometric centroid can
                // technically sit outside the zone's actual cells (in the "notch"), so this
                // snap isn't guaranteed to point at an immediately-valid cell every time. That's
                // fine in practice — the random per-tick jitter above (headingDegrees +=
                // Rand.Range(...)) keeps applying every tick regardless of whether a move was
                // blocked, so the heading can never get stuck at one exact angle. Worst case is
                // a few ticks of the bubble visibly "wiggling" at a dead end before it finds a
                // valid direction — imperceptible for a slow, subtle decorative mote like this.
                float dxToCenter = zoneCenter.x - exactPosition.x;
                float dzToCenter = zoneCenter.z - exactPosition.z;
                headingDegrees = Mathf.Atan2(dzToCenter, dxToCenter) * Mathf.Rad2Deg;
                UpdateVelocityFromHeading();
            }

            // Live opacity: read the current mod setting every tick so the settings slider
            // affects already-spawned bubbles immediately, not just new ones.
            // Graphic_Mote.DrawMote multiplies (data.color * instanceColor) and then
            // multiplies alpha by the mote's own fade-based Alpha, so this scales final
            // rendered opacity without touching the fade-in/out animation.
            instanceColor = new Color(1f, 1f, 1f, TPE_Mod.Settings.bubbleOpacity);
        }
    }
}
