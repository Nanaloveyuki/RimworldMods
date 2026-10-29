using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Single source of truth for the two "disable play animations" settings
    /// (<see cref="TPE_Settings.disableToddlerPlayAnimations"/> /
    /// <see cref="TPE_Settings.disableChildPlayAnimations"/>) and the shared helpers every
    /// animating play job driver uses to start / keep / stop its animation.
    ///
    /// <para><b>Two-level gating (see Architecture.md §5.3):</b></para>
    /// <list type="number">
    ///   <item><description><b>Start site</b> — job drivers never call
    ///   <c>PawnRenderer.SetAnimation</c> while the matching setting is ON
    ///   (<see cref="MaintainToddlerAnimation"/> / <see cref="MaintainChildAnimation"/>).
    ///   Nothing set ⇒ no render-tree rebuild (<c>SetAnimation</c> always calls
    ///   <c>renderTree.SetDirty()</c>), vanilla's cheap cached-pawn render path stays
    ///   available (it is skipped whenever <c>renderTree.currentAnimation != null</c>),
    ///   and the base Toddlers mod's own crawl/wobble locomotion animation is never
    ///   displaced. Because the maintain helpers are also called every tick, flipping the
    ///   setting mid-activity removes (or restores) the animation on the next tick.</description></item>
    ///   <item><description><b>Render site</b> — the animation workers'
    ///   <c>Enabled</c>, the base-mod Bugwatch/WiggleInCrib <c>Enabled</c> prefixes, and the
    ///   <c>PawnRenderer.GetBodyPos</c> bounce postfix read the same flags, so an animation
    ///   that was already running when the player flips the setting stops on the very next
    ///   FRAME — even while paused (ticks, and so the maintain helpers, don't run while
    ///   paused). The two jump bounces have no AnimationDef at all for toddlers, so for
    ///   them the render site is the only gate.</description></item>
    /// </list>
    ///
    /// <para><b>Thread-safety:</b> <see cref="ToddlerAnimationsDisabled"/> /
    /// <see cref="ChildAnimationsDisabled"/> are read from the PARALLEL pre-draw jobs
    /// (<c>PawnRenderer.ParallelGetPreRenderResults</c> → <c>GetBodyPos</c>, and
    /// <c>PawnRenderTree.ParallelPreDraw</c> → <c>AnimationWorker.Enabled</c>). They only
    /// read one static reference and one bool (an atomic read), never write — safe. Everything
    /// else in this class (SetAnimation, the lazy def lookups) is main-thread only: it
    /// is called exclusively from job-driver toil actions.</para>
    /// </summary>
    public static class PlayAnimationUtility
    {
        // ══════════════════════════════════════════════════════════════════
        //  Settings reads (hot path — render threads, every frame)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// True when the player has turned toddler play animations OFF. Null-safe
        /// (settings can be absent very early in load) and defaults to "animations play".
        /// Cost: one static field read + one bool read — fine for per-frame render code.
        /// </summary>
        public static bool ToddlerAnimationsDisabled
        {
            get
            {
                TPE_Settings settings = TPE_Mod.Settings;
                return settings != null && settings.disableToddlerPlayAnimations;
            }
        }

        /// <summary>
        /// True when the player has turned child (3–13) play animations OFF. Same
        /// semantics and cost as <see cref="ToddlerAnimationsDisabled"/>.
        /// </summary>
        public static bool ChildAnimationsDisabled
        {
            get
            {
                TPE_Settings settings = TPE_Mod.Settings;
                return settings != null && settings.disableChildPlayAnimations;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Start / keep / stop helpers (main thread — toil actions only)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Keeps <paramref name="animation"/> playing on a toddler while toddler play
        /// animations are allowed, and makes sure it is NOT playing while they're
        /// disabled. Safe (and cheap) to call every tick: it only calls
        /// <c>SetAnimation</c> when the state actually needs to change.
        /// </summary>
        public static void MaintainToddlerAnimation(Pawn pawn, AnimationDef? animation)
        {
            Maintain(pawn, animation, ToddlerAnimationsDisabled);
        }

        /// <summary>
        /// Child (3–13) counterpart of <see cref="MaintainToddlerAnimation"/>, gated on
        /// <see cref="ChildAnimationsDisabled"/>.
        /// </summary>
        public static void MaintainChildAnimation(Pawn pawn, AnimationDef? animation)
        {
            Maintain(pawn, animation, ChildAnimationsDisabled);
        }

        /// <summary>
        /// Stops <paramref name="animation"/> on <paramref name="pawn"/> ONLY if it is the
        /// animation currently playing. Never clears some other animation (e.g. the base
        /// Toddlers mod's crawl/wobble locomotion, or another mod's) and never triggers a
        /// pointless <c>SetAnimation(null)</c> render-tree rebuild when nothing of ours is
        /// playing (the normal case while animations are disabled).
        /// </summary>
        public static void ClearIfCurrent(Pawn pawn, AnimationDef? animation)
        {
            PawnRenderer? renderer = pawn?.Drawer?.renderer;
            if (renderer == null || animation == null)
                return;
            if (renderer.CurAnimation == animation)
                renderer.SetAnimation(null);
        }

        /// <summary>
        /// Shared body of the two maintain helpers.
        /// disabled ⇒ stop <paramref name="animation"/> if (and only if) it is playing;
        /// allowed ⇒ start it if something else (or nothing) is playing. Idempotent: a
        /// second call in the same state does nothing, so it never resets the animation's
        /// start tick (SetAnimation would) or rebuilds the render tree needlessly.
        /// </summary>
        static void Maintain(Pawn pawn, AnimationDef? animation, bool disabled)
        {
            PawnRenderer? renderer = pawn?.Drawer?.renderer;
            if (renderer == null || animation == null)
                return;

            AnimationDef current = renderer.CurAnimation;
            if (disabled)
            {
                if (current == animation)
                    renderer.SetAnimation(null);
            }
            else if (current != animation)
            {
                renderer.SetAnimation(animation);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  Base Toddlers mod animations reused by our toddler activities
        // ══════════════════════════════════════════════════════════════════
        // Toddlers' own Toddlers_AnimationDefOf is `internal`, so we resolve these by
        // name, ONCE. A separate "searched" flag (instead of `cached ?? lookup`) means a
        // missing def (e.g. a future Toddlers rename) is looked up exactly once rather
        // than on every tick. Callers must null-check — a null def simply means "no
        // animation", which every Maintain/Clear helper above already tolerates.
        // Previously each driver kept its own copy of this cache, and Bug Watching with
        // a Magnifying Glass did two DefDatabase lookups EVERY TICK instead.

        static bool bugwatchSearched;
        static AnimationDef? bugwatchAnimation;

        /// <summary>
        /// The base Toddlers mod's "Bugwatch" body-tilt AnimationDef, reused by Looking
        /// Through Storage and Bug Watching with a Magnifying Glass. Null if missing.
        /// Main thread only (lazy, unsynchronised one-time init).
        /// </summary>
        public static AnimationDef? BugwatchAnimation
        {
            get
            {
                if (!bugwatchSearched)
                {
                    bugwatchSearched = true;
                    bugwatchAnimation = DefDatabase<AnimationDef>.GetNamed("Bugwatch", errorOnFail: false);
                }
                return bugwatchAnimation;
            }
        }

        static bool wiggleSearched;
        static AnimationDef? wiggleInCribAnimation;

        /// <summary>
        /// The base Toddlers mod's "WiggleInCrib" AnimationDef, reused by Self Soothing.
        /// Null if missing. Main thread only (lazy, unsynchronised one-time init).
        /// </summary>
        public static AnimationDef? WiggleInCribAnimation
        {
            get
            {
                if (!wiggleSearched)
                {
                    wiggleSearched = true;
                    wiggleInCribAnimation = DefDatabase<AnimationDef>.GetNamed("WiggleInCrib", errorOnFail: false);
                }
                return wiggleInCribAnimation;
            }
        }
    }
}
