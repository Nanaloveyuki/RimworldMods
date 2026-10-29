using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// The four categories shown as tabs in the mod-settings window.
    /// </summary>
    public enum TPE_SettingsTab
    {
        /// <summary>Toddlers and newborn-related settings.</summary>
        ToddlersNewborns,
        /// <summary>Child-related settings that are NOT part of the play-need system.</summary>
        Children,
        /// <summary>The toggleable play-need system for children (3–13).</summary>
        ChildrensPlayNeed,
        /// <summary>Adult (13+) plush-sleep settings — see <see cref="TPE_Settings.enableAdultPlushSleep"/>.</summary>
        Adults
    }

    /// <summary>
    /// The four ways an adult (13+, includes the Teenager life stage — see
    /// <see cref="TPE_Settings.enableAdultPlushSleep"/> for why 13 is the cutoff)
    /// can react, mood-wise, to sleeping with or without a plush toy. Unlike the
    /// toddler/child brackets (which always apply both directions unconditionally),
    /// adults get a player-chosen dial because "my grown colonist gets sad without
    /// a teddy bear" is a much more opinionated default than the same mechanic for
    /// a 3-year-old.
    /// </summary>
    public enum AdultPlushieMoodMode
    {
        /// <summary>Adults never gain a mood thought from plush sleep either way.</summary>
        None,
        /// <summary>Only the negative "missed my comfort item" thought can ever apply.</summary>
        NegativeOnly,
        /// <summary>Only the positive "slept with a comfort item" thought can ever apply.</summary>
        PositiveOnly,
        /// <summary>Both thoughts can apply, matching the toddler/child behavior exactly.</summary>
        Both
    }

    /// <summary>
    /// Persistent mod settings. Values are saved to the RimWorld config folder.
    /// The <see cref="currentTab"/> field is NOT persisted — it only tracks which
    /// category the player last viewed in the settings UI for convenience.
    /// </summary>
    public class TPE_Settings : ModSettings
    {
        // ══════════════════════════════════════════════════════════════════════
        //  TODDLERS / NEWBORNS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Enables the random toddler mood-swing system (20 situation-agnostic
        /// mood memories that appear spontaneously on awake toddlers).
        /// </summary>
        public bool enableRandomToddlerMoods = true;

        /// <summary>
        /// Maximum number of random mood thoughts a toddler can have active
        /// at the same time. Default 3, range 1–5.
        /// </summary>
        public int randomMoodMaxActive = 3;

        /// <summary>
        /// Hourly probability (0.0–1.0) that an awake toddler gains a random mood.
        /// Default 0.15 (≈15%).
        /// </summary>
        public float randomMoodFrequency = 0.15f;

        /// <summary>
        /// Enables event-triggered mood memories (raids, weather, death, etc.).
        /// </summary>
        public bool enableEventTriggeredMoods = true;

        /// <summary>
        /// Enables the Self Soothing toddler play activity. When active, upset
        /// toddlers in cribs may attempt to calm themselves down through
        /// sub-activities like thumb sucking, blanket clutching, etc.
        /// </summary>
        public bool enableSelfSoothing = true;

        /// <summary>
        /// Enables the Heater Snuggling toddler play activity. When active and
        /// outdoor temperature drops below the toddler's comfortable minimum,
        /// toddlers will seek indoor heaters and idle next to them for warmth.
        /// </summary>
        public bool enableHeaterSnuggling = true;

        /// <summary>
        /// When true, toddlers may snuggle next to geothermal generators and
        /// steam geysers in addition to regular heaters. Default OFF for safety.
        /// </summary>
        public bool allowGeothermalSnuggling = false;

        /// <summary>
        /// When true, suppresses the crying sound effect for babies and toddlers
        /// (vanilla MentalState_BabyCry and our TPE_WantsOutOfCrib mental state).
        /// Visual tears and mood effects remain. Accessibility option.
        /// </summary>
        public bool muteBabyCryingSound = false;

        /// <summary>
        /// Master visual toggle for the sleeping-plush rendering system, applying
        /// uniformly to ALL FOUR plush-sleep age brackets (toddler, child 3–6,
        /// child 7–12, adult) — not just toddlers, despite living in this tab.
        /// When ON (default): a sleeping pawn currently wearing a plush toy has it
        /// drawn at a fixed position beside their head/pillow instead of flat on
        /// their body (which the normal apparel-render path does unreliably for a
        /// laying pawn). When OFF: the plush is simply not drawn at all while the
        /// pawn sleeps — it never falls back to the old on-body look, since that
        /// was the actual (or at least unreliable-looking) rendering being
        /// replaced. Equip behavior, mood thoughts, and everything else about the
        /// plush-sleep systems are completely unaffected either way — this is a
        /// purely cosmetic toggle. See
        /// <c>Patch_PawnRenderer_RenderPawnAt_DrawSleepingPlush</c> in
        /// HarmonyPatches.cs and Architecture.md §5.1.
        /// </summary>
        public bool renderPlushWhileSleeping = true;

        /// <summary>
        /// Master override for the rest-threshold "give up" safety valve every
        /// plush-sleep redirect patch has (see e.g.
        /// <c>Patch_JobDriver_LayDown_SetupToils_Adult</c>'s own
        /// "Rest threshold guard" comment) — normally, once a pawn's rest need
        /// has already dropped very low before they even reach a bed, the
        /// redirect deliberately gives up on fetching a plush so it doesn't
        /// delay actually getting them to sleep any further. When this is ON,
        /// that safety valve is skipped entirely: ALL FOUR plush-sleep brackets
        /// (toddler, child 3–6, child 7–12, adult if enabled) will always walk
        /// off to fetch and equip a plush before every single bedtime, no matter
        /// how exhausted the pawn already is. Applies uniformly to every
        /// bracket, matching <see cref="renderPlushWhileSleeping"/>'s
        /// all-brackets scope. Default OFF — this is an opinionated trade
        /// (guarantees the comfort thought every time, at the cost of a slightly
        /// later bedtime) that shouldn't silently change existing behavior for
        /// players who haven't asked for it. Does NOT bypass the separate
        /// "already in bed" / "already has a plush" / redirect-cooldown checks
        /// each patch also has — only the rest-percentage "give up" check.
        /// </summary>
        public bool alwaysSeekPlushRegardlessOfRest = false;

        /// <summary>
        /// Purely cosmetic accessibility/preference toggle. When ON, toddlers doing
        /// THIS mod's play activities no longer animate: no body spin (Spinning Around —
        /// the toddler lies still on the spot instead, without sliding to new spots), no
        /// hop/bounce (Jumping Up and Down, Crib Jumping), no forward tilt (Looking
        /// Through Storage, Bug Watching with a Magnifying Glass — these borrow the base
        /// Toddlers mod's bug-watching tilt) and no in-crib wiggle (Self Soothing — borrows
        /// the base mod's crib wiggle). The activities themselves still run exactly as
        /// before (same jobs, same durations, same Play gain); only the visuals stop.
        /// The base Toddlers mod's OWN activities (its regular bug watching, crib wiggle,
        /// crawl/wobble locomotion) are deliberately NOT affected (user-confirmed scope).
        /// Read at runtime via <see cref="PlayAnimationUtility.ToddlerAnimationsDisabled"/>
        /// both where an animation is started (job drivers — so nothing is ever set while
        /// OFF) and where it is rendered (animation workers / the GetBodyPos bounce patch —
        /// so flipping it mid-animation takes effect on the very next frame, even paused).
        /// Default OFF (animations play). See Architecture.md §5.3.
        /// </summary>
        public bool disableToddlerPlayAnimations = false;

        // ══════════════════════════════════════════════════════════════════════
        //  BUBBLE MACHINE
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Global hard limit on the number of bubbles that can render simultaneously
        /// across the entire map. Default 40. Prevents performance issues from
        /// excessive mote rendering when many bubble machines are active.
        /// Range: 1–100.
        /// </summary>
        public int maxBubblesPerMap = 40;

        /// <summary>
        /// Opacity multiplier applied to every bubble mote's rendered alpha, on top of its
        /// normal fade-in/fade-out animation. 1.0 = fully opaque (bubble texture as authored),
        /// lower values make bubbles more see-through. Applied live every tick, so adjusting
        /// the slider affects already-spawned bubbles immediately. Range: 0–1.
        /// </summary>
        public float bubbleOpacity = 1f;

        /// <summary>
        /// When true, escape-crib job priority increases as the confinement tier increases.
        /// Tier 1 (Restless): 2.0f | Tier 2 (Frustrated): 3.0f | Tier 3 (Desperate): 4.5f.
        /// When mental break (WantsOutOfCrib) is active, priority RESETS to 2.0f (panic mode).
        /// Default ON for more dynamic gameplay; disable for vanilla-like flat priority.
        /// </summary>
        public bool enableConfinementPriorityScaling = true;

        // ══════════════════════════════════════════════════════════════════════
        //  CHILDREN (non-play-need)
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Enables the children (3–6) plush-to-bed system. When active, children
        /// who are sleepy will autonomously seek and equip a plush toy before
        /// going to sleep, receiving comfort thoughts when bedding down with one.
        /// </summary>
        public bool enableChildPlushSleep = true;

        /// <summary>
        /// Enables the OLDER children (7–12, still <see cref="DevelopmentalStage.Child"/>
        /// but past the original 3–6 bracket above) plush-to-bed system. Fully independent
        /// of <see cref="enableChildPlushSleep"/> — either can be toggled without affecting
        /// the other. Reuses the exact same seek/equip job and the exact same
        /// <c>TPE_ChildSleptWithPlushToy</c>/<c>TPE_ChildNoPlushToy</c> thoughts as the 3–6
        /// bracket (the flavor text reads fine for an older child too), just with a slightly
        /// higher "give up and sleep anyway" rest threshold to reflect a bit more patience/maturity.
        /// See <c>Patch_JobDriver_LayDown_SetupToils_OlderChild</c> in HarmonyPatches.cs.
        /// </summary>
        public bool enableOlderChildPlushSleep = true;

        /// <summary>
        /// Enables the child crying system for growth-tier-1 children (ages 3–6).
        /// When active, children who drop below 30% mood may enter crying bouts,
        /// seek comfort from adults, and apply mood debuffs to nearby colonists.
        /// </summary>
        public bool enableChildCrying = true;

        /// <summary>
        /// When true, suppresses the crying sound effect for children (ages 3–6)
        /// in the TPE_ChildCrying or TPE_ChildTantrum mental state. Visual tears
        /// and mood effects remain. Accessibility option for players who find
        /// child crying audio distressing.
        /// </summary>
        public bool muteChildCryingSound = false;

        // ══════════════════════════════════════════════════════════════════════
        //  CHILDREN'S PLAY NEED
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Enables the Children (3–13) Play Need system. When active, children
        /// gain a dedicated Play need alongside Learning, with growth points
        /// blended from both so they must balance playtime and study time.
        /// </summary>
        public bool enableChildrenPlayNeed = true;

        /// <summary>
        /// How many days it takes for a child's learning need to drain from 100% to 0%.
        /// Vanilla learning decays in ~5.5 days; we default to 6 days to stay close to
        /// vanilla pressure while allowing children enough time for both learning and play.
        /// Range: 3–14 days (minimum drives more learning, maximum is very relaxed).
        /// </summary>
        public float playNeedDecayDays = 6f;

        /// <summary>
        /// How much weight the play need has in growth-point calculation.
        /// 0.5 = average of learning and play (default).
        /// 0.0 = learning only, 1.0 = play only.
        /// </summary>
        public float playWeightInGrowth = 0.5f;

        /// <summary>
        /// When enabled, play desires are restricted to the age brackets
        /// defined in each PlayDesireDef (e.g. plush toys only for 3–6).
        /// When disabled, all children (3–13) can receive any play desire.
        /// </summary>
        public bool ageGatedPlayDesires = true;

        /// <summary>
        /// When true, the Psychopath trait no longer nullifies mood from this
        /// mod's play-related mood thoughts: the child play-need thoughts
        /// (<c>TPE_ChildPlayHigh</c> / <c>TPE_ChildPlayEmpty</c>), the toddler
        /// Self Soothing outcome thoughts (<c>TPE_SelfSoothingPositive</c> /
        /// <c>TPE_SelfSoothingNegative</c>), AND vanilla Biotech's own baby
        /// play-need thought (<c>NeedPlay</c> — the "entertained"/"very
        /// entertained"/"wants to play" stages driven by <c>Need_Play</c>
        /// percentage; this is vanilla's, not ours, but ships with the same
        /// Psychopath nullification and is the baby-side counterpart to
        /// TPE_ChildPlayHigh/Empty). Default OFF: by default these thoughts
        /// declare <c>nullifyingTraits: Psychopath</c> (ours in XML; vanilla's
        /// NeedPlay ships that way out of the box), matching vanilla's own
        /// convention for child/baby-facing mood thoughts (see Biotech's
        /// MyChildHappy/MyChildSad/MyParentHappy/MyParentsHappy) — a
        /// psychopathic toddler/child (almost always via a forced-trait gene,
        /// since backstory traits aren't assigned this young) still "has" the
        /// thought (it's listed, greyed out, in the Needs tab) but it contributes
        /// zero mood. Enabling this setting suppresses that nullification for
        /// just these protected thoughts via
        /// <c>Patch_ThoughtUtility_NullifyingTrait_PlayMood</c> in
        /// HarmonyPatches.cs — every other Psychopath-nullified thought in the
        /// game (vanilla or modded) is left untouched.
        /// </summary>
        public bool keepPlayMoodForPsychopaths = false;

        /// <summary>
        /// When true, a child with genuinely nothing else to do (no food/rest/chemical
        /// need, no active learning desire, no eligible normal play-desire job, no work)
        /// plays instead of falling through to vanilla's idle wander — ignoring the play
        /// need's normal fullness cap, since there's nothing better for them to be doing
        /// anyway. Implemented by <see cref="JobGiver_ChildPlayWhenIdle"/>, spliced into
        /// vanilla's own "Idle colonist" think-node branch right before
        /// <c>JobGiver_WanderColony</c>. Still respects the TPE_Learning schedule slot
        /// (never plays during dedicated learning time) and Work-hour idle suppression
        /// (mirrors vanilla's own <c>JobGiver_IdleJoy</c> gate). While an idle-triggered
        /// session is running, it re-checks roughly every 50 ticks (see
        /// <see cref="PlayUtility.CheckForBetterJobWhileIdlePlaying"/>) whether something
        /// genuinely more important has become available (hunger, tiredness, a learning
        /// desire, work, etc.) and immediately drops the idle play session for it if so —
        /// idle play never blocks a child from responding to a real need. Default OFF.
        /// </summary>
        public bool playWhenIdle = false;

        /// <summary>
        /// When true, every child's desire refresh guarantees Active Play as
        /// one of their 2 active play desires (if the pawn is currently
        /// eligible for it at all), instead of it just being one more option
        /// in the normal weighted roll for both slots. The other slot still
        /// rolls normally. Default OFF — preserves the original fully-random
        /// 2-slot roll unless the player opts in. See
        /// <see cref="Pawn_PlayTracker.RefreshDesires"/>.
        /// </summary>
        public bool guaranteeActivePlayDesire = false;

        /// <summary>
        /// Controls whether a child who finishes the "Building dirt castles" play
        /// desire leaves a visible dirt castle (<see cref="Building_DirtCastle"/>)
        /// behind. ON (default): a castle appears where the child played and
        /// crumbles after ~3 days, but ONLY when the play session fully completes
        /// (job ends Succeeded — never when interrupted), and it is only ever
        /// placed on a cell where it won't override a building or a crop (no
        /// building/frame/other castle, no colony-sown plant, not growing-zone
        /// land — see <see cref="DirtCastleUtility"/>; wild grass etc. is fine).
        /// OFF: no castle is spawned at all; the child still plays for the full
        /// session and gains Play exactly as normal, and the desire's spot-picking
        /// no longer avoids buildings/crops.
        /// Only affects castles built AFTER the change — castles already on the map
        /// are left alone and expire on their own timer. Read at runtime (never a
        /// conditional patch), so toggling takes effect immediately.
        /// </summary>
        public bool showDirtCastles = true;

        /// <summary>
        /// Purely cosmetic accessibility/preference toggle — the child (3–13) counterpart
        /// of <see cref="disableToddlerPlayAnimations"/>. When ON, children doing Active
        /// Play no longer animate during its Spinning (body spin — they stand still facing
        /// forward), Jumping (hop/bounce — they stand still) and Snail Crawling (forward
        /// lean — they walk slowly upright) sub-activities. These three are the ONLY child
        /// play animations in the mod; no other child play desire animates. The activity
        /// still runs exactly as before (same sub-activity rotation, timings, Play gain).
        /// Read at runtime via <see cref="PlayAnimationUtility.ChildAnimationsDisabled"/>
        /// at the start site (JobDriver_ChildActivePlay) and the render site (child
        /// animation workers + the GetBodyPos bounce patch), so it takes effect on the next
        /// frame. Default OFF (animations play). See Architecture.md §5.3.
        /// </summary>
        public bool disableChildPlayAnimations = false;

        // ══════════════════════════════════════════════════════════════════════
        //  ADULTS (13+)
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Enables the adult (13+, i.e. <see cref="DevelopmentalStage.Adult"/> — this
        /// includes the Teenager life stage, since RimWorld's own
        /// <c>HumanlikeTeenager</c> LifeStageDef never overrides the
        /// <c>developmentalStage</c> field away from its class default of Adult; only
        /// the label "teenager" vs "adult" changes at 18, not the underlying
        /// developmental bucket everything else in the game keys off) plush-to-bed
        /// system. Reuses the exact same seek/equip job as toddlers and children, but
        /// with its own dedicated <c>TPE_AdultSleptWithPlushToy</c>/<c>TPE_AdultNoPlushToy</c>
        /// thoughts and its own <see cref="adultPlushieMoodMode"/> dial — unlike the
        /// child/toddler brackets, mood from this feature is fully opt-out-able.
        /// Default OFF: this is a whimsical extra outside the mod's core toddler/child
        /// scope, so it doesn't surprise players who didn't ask for their adult
        /// colonists to start sleeping with teddy bears.
        /// See <c>Patch_JobDriver_LayDown_SetupToils_Adult</c> in HarmonyPatches.cs.
        /// </summary>
        public bool enableAdultPlushSleep = false;

        /// <summary>
        /// Controls which of the two adult plush-sleep mood thoughts (if any) can
        /// ever be applied. Only consulted when <see cref="enableAdultPlushSleep"/>
        /// is true. See <see cref="AdultPlushieMoodMode"/> for the four options.
        /// Default <see cref="AdultPlushieMoodMode.Both"/> — matches the unconditional
        /// both-directions behavior every other age bracket already has.
        /// </summary>
        public AdultPlushieMoodMode adultPlushieMoodMode = AdultPlushieMoodMode.Both;

        // ══════════════════════════════════════════════════════════════════════
        //  DEBUG
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Enable detailed debug logging across the entire mod (crib gate, heater snuggling,
        /// play activities, job allocation, etc.). Useful for diagnosing issues with toddler
        /// and child AI. Default OFF (performance). Logs are conditional and do not affect
        /// control flow.
        /// </summary>
        public bool debugLogging = false;

        // ══════════════════════════════════════════════════════════════════════
        //  UI STATE (not persisted)
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Which settings tab is currently open. Not saved — resets to
        /// <see cref="TPE_SettingsTab.ToddlersNewborns"/> on restart.
        /// </summary>
        public TPE_SettingsTab currentTab = TPE_SettingsTab.ToddlersNewborns;

        public override void ExposeData()
        {
            // Toddlers / Newborns
            Scribe_Values.Look(ref enableRandomToddlerMoods, "enableRandomToddlerMoods", true);
            Scribe_Values.Look(ref randomMoodMaxActive, "randomMoodMaxActive", 3);
            Scribe_Values.Look(ref randomMoodFrequency, "randomMoodFrequency", 0.15f);
            Scribe_Values.Look(ref enableEventTriggeredMoods, "enableEventTriggeredMoods", true);
            Scribe_Values.Look(ref enableSelfSoothing, "enableSelfSoothing", true);
            Scribe_Values.Look(ref enableHeaterSnuggling, "enableHeaterSnuggling", true);
            Scribe_Values.Look(ref allowGeothermalSnuggling, "allowGeothermalSnuggling", false);
            Scribe_Values.Look(ref muteBabyCryingSound, "muteBabyCryingSound", false);
            Scribe_Values.Look(ref renderPlushWhileSleeping, "renderPlushWhileSleeping", true);
            Scribe_Values.Look(ref alwaysSeekPlushRegardlessOfRest, "alwaysSeekPlushRegardlessOfRest", false);
            Scribe_Values.Look(ref disableToddlerPlayAnimations, "disableToddlerPlayAnimations", false);
            Scribe_Values.Look(ref maxBubblesPerMap, "maxBubblesPerMap", 40);
            Scribe_Values.Look(ref bubbleOpacity, "bubbleOpacity", 1f);
            Scribe_Values.Look(ref enableConfinementPriorityScaling, "enableConfinementPriorityScaling", true);

            // Children (non-play-need)
            Scribe_Values.Look(ref enableChildPlushSleep, "enableChildPlushSleep", true);
            Scribe_Values.Look(ref enableOlderChildPlushSleep, "enableOlderChildPlushSleep", true);
            Scribe_Values.Look(ref enableChildCrying, "enableChildCrying", true);
            Scribe_Values.Look(ref muteChildCryingSound, "muteChildCryingSound", false);

            // Adults (13+)
            Scribe_Values.Look(ref enableAdultPlushSleep, "enableAdultPlushSleep", false);
            Scribe_Values.Look(ref adultPlushieMoodMode, "adultPlushieMoodMode", AdultPlushieMoodMode.Both);

            // Children's Play Need
            Scribe_Values.Look(ref enableChildrenPlayNeed, "enableChildrenPlayNeed", true);
            Scribe_Values.Look(ref playNeedDecayDays, "playNeedDecayDays", 6f);
            Scribe_Values.Look(ref playWeightInGrowth, "playWeightInGrowth", 0.5f);
            Scribe_Values.Look(ref ageGatedPlayDesires, "ageGatedPlayDesires", true);
            Scribe_Values.Look(ref keepPlayMoodForPsychopaths, "keepPlayMoodForPsychopaths", false);
            Scribe_Values.Look(ref playWhenIdle, "playWhenIdle", false);
            Scribe_Values.Look(ref guaranteeActivePlayDesire, "guaranteeActivePlayDesire", false);
            Scribe_Values.Look(ref showDirtCastles, "showDirtCastles", true);
            Scribe_Values.Look(ref disableChildPlayAnimations, "disableChildPlayAnimations", false);

            // Debug
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);

            base.ExposeData();
        }
    }

    /// <summary>
    /// Mod entry point that registers the settings page in RimWorld's
    /// Options → Mod Settings menu. The UI is organized into four tabbed
    /// categories so that no single view grows tall enough to push options
    /// off-screen.
    /// </summary>
    public class TPE_Mod : Mod
    {
        public static TPE_Settings Settings = null!;

        /// <summary>
        /// Height reserved for the row of category-tab buttons at the top of
        /// the settings panel.
        /// </summary>
        private const float TabRowHeight = 36f;

        /// <summary>
        /// Horizontal gap between adjacent tab buttons.
        /// </summary>
        private const float TabButtonGap = 6f;

        /// <summary>
        /// Vertical gap between the tab row and the content area below it.
        /// </summary>
        private const float TabContentGap = 8f;

        /// <summary>
        /// Number of settings tabs — sizes <see cref="tabContentHeights"/>. Computed once
        /// from the enum so adding a tab can never index out of range.
        /// </summary>
        private static readonly int TabCount = System.Enum.GetValues(typeof(TPE_SettingsTab)).Length;

        /// <summary>
        /// Scroll offset of the tab content area. UI-only (never saved); reset to the top
        /// whenever the player switches tabs so a new tab never opens mid-scroll.
        /// </summary>
        private Vector2 settingsScrollPosition;

        /// <summary>
        /// Content height each tab actually used on its previous draw pass
        /// (<see cref="Listing.CurHeight"/>), indexed by <c>(int)TPE_SettingsTab</c>. Used as
        /// the scroll view's <c>viewRect</c> height on the next pass. Measuring instead of
        /// hard-coding means sub-options that appear/disappear (e.g. the random-mood
        /// sliders) and longer translations size the scroll area correctly by themselves.
        /// Starts at 0 = "not measured yet" (the view then just fills the visible area for
        /// one pass — see the clamp in <see cref="DoSettingsWindowContents"/>).
        /// </summary>
        private readonly float[] tabContentHeights = new float[TabCount];

        public TPE_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TPE_Settings>();

            // Stale-DLL guard (dev mode only, once): logs one clear error if this DLL was not
            // built from the Source/ folder next to it. Returns immediately when dev mode is
            // off. See BuildFingerprintCheck / Architecture.md §3a.
            BuildFingerprintCheck.RunIfDevMode(content);
        }

        /// <summary>
        /// Renders the entire settings window. The top portion is a row of
        /// category buttons; the remainder shows only the settings belonging
        /// to the currently selected category.
        /// </summary>
        /// <param name="inRect">The full rectangle allocated by RimWorld for this mod's settings page.</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // ── Tab button row ──
            Rect tabRow = new Rect(inRect.x, inRect.y, inRect.width, TabRowHeight);
            DrawTabButtons(tabRow);

            // ── Content area (everything below the tabs) ──
            float contentY = inRect.y + TabRowHeight + TabContentGap;
            Rect contentRect = new Rect(inRect.x, contentY, inRect.width, inRect.height - TabRowHeight - TabContentGap);

            // ── Scrollable content (fixes tabs taller than the 900×700 settings window) ──
            // The Toddlers / Newborns tab already needed ~580px against ~540px available
            // before the animation toggle was added, so its last slider was clipped.
            // viewRect height = what this tab measured last pass, but never less than the
            // visible area, so short tabs show no scrollbar. Width ALWAYS reserves the
            // scrollbar: if it only did so when scrolling, label wrapping (and therefore
            // the measured height) could flip-flop right at the threshold every frame.
            int tabIndex = (int)Settings.currentTab;
            if ((uint)tabIndex >= (uint)TabCount)
                tabIndex = 0; // defensive; currentTab is never persisted, so this can't really happen
            float viewHeight = Mathf.Max(tabContentHeights[tabIndex], contentRect.height);
            Rect viewRect = new Rect(0f, 0f, contentRect.width - GenUI.ScrollBarWidth, viewHeight);

            Widgets.BeginScrollView(contentRect, ref settingsScrollPosition, viewRect);

            // maxOneColumn: when content grows this pass (a sub-option was just revealed),
            // the listing must NOT wrap the overflow into a second column — it is simply
            // clipped for this one pass, and next pass viewHeight already fits it.
            Listing_Standard listing = new Listing_Standard { maxOneColumn = true };
            listing.Begin(viewRect);

            // try/finally: the scroll view and the listing each push a GUI clip. If any
            // tab's draw code ever threw, skipping End()/EndScrollView() would leave the
            // clip stack unbalanced and cascade into per-frame GUI errors in every window.
            try
            {
                switch (Settings.currentTab)
                {
                    case TPE_SettingsTab.ToddlersNewborns:
                        DrawToddlersNewbornsTab(listing);
                        break;
                    case TPE_SettingsTab.Children:
                        DrawChildrenTab(listing);
                        break;
                    case TPE_SettingsTab.ChildrensPlayNeed:
                        DrawChildrensPlayNeedTab(listing);
                        break;
                    case TPE_SettingsTab.Adults:
                        DrawAdultsTab(listing);
                        break;
                }

                // Remember how tall this tab really is for the next pass. Idempotent
                // across the multiple OnGUI passes per frame (Layout/Repaint/input) —
                // a plain overwrite of a measured value, never an accumulation.
                tabContentHeights[tabIndex] = listing.CurHeight;
            }
            finally
            {
                listing.End();
                Widgets.EndScrollView();
            }
        }

        /// <summary>
        /// Draws the category buttons side-by-side. The active category
        /// receives a highlight so the player knows which view is open.
        /// </summary>
        private void DrawTabButtons(Rect tabRow)
        {
            // Pair each tab enum with its display label.
            (TPE_SettingsTab tab, string label)[] tabs = new (TPE_SettingsTab, string)[]
            {
                (TPE_SettingsTab.ToddlersNewborns, "TPE_Settings_Tab_ToddlersNewborns".Translate()),
                (TPE_SettingsTab.Children,           "TPE_Settings_Tab_Children".Translate()),
                (TPE_SettingsTab.ChildrensPlayNeed,  "TPE_Settings_Tab_ChildrensPlayNeed".Translate()),
                (TPE_SettingsTab.Adults,              "TPE_Settings_Tab_Adults".Translate())
            };

            float buttonWidth = (tabRow.width - (tabs.Length - 1) * TabButtonGap) / tabs.Length;

            for (int i = 0; i < tabs.Length; i++)
            {
                Rect buttonRect = new Rect(
                    tabRow.x + i * (buttonWidth + TabButtonGap),
                    tabRow.y,
                    buttonWidth,
                    tabRow.height);

                bool isSelected = Settings.currentTab == tabs[i].tab;

                if (Widgets.ButtonText(buttonRect, tabs[i].label))
                {
                    // Switching to a different tab starts it scrolled to the top.
                    if (Settings.currentTab != tabs[i].tab)
                        settingsScrollPosition = Vector2.zero;
                    Settings.currentTab = tabs[i].tab;
                }

                // Highlight the selected tab so it stands out from the other two.
                if (isSelected)
                {
                    Widgets.DrawHighlight(buttonRect);
                }
            }
        }

        /// <summary>
        /// Toddlers / Newborns category.
        /// Contains: random moods, event moods, self soothing, heater snuggling,
        /// mute baby/toddler crying sounds, disable toddler play animations,
        /// plush-sleep rendering/seeking, crib confinement scaling, bubble limits.
        /// </summary>
        private void DrawToddlersNewbornsTab(Listing_Standard listing)
        {
            listing.CheckboxLabeled(
                "Enable random toddler moods",
                ref Settings.enableRandomToddlerMoods,
                "Awake toddlers occasionally gain spontaneous mood buffs or debuffs about small, everyday things.");

            if (Settings.enableRandomToddlerMoods)
            {
                listing.Gap(8f);
                listing.Label($"Max simultaneous random moods: {Settings.randomMoodMaxActive}");
                Settings.randomMoodMaxActive = Mathf.RoundToInt(listing.Slider(Settings.randomMoodMaxActive, 1f, 5f));
                listing.Label("How many random mood thoughts a toddler can have at once.");

                listing.Gap(8f);
                listing.Label($"Mood event frequency: {Settings.randomMoodFrequency:P0}");
                Settings.randomMoodFrequency = listing.Slider(Settings.randomMoodFrequency, 0.05f, 0.50f);
                listing.Label("Hourly chance for each awake toddler to gain a random mood.");
            }

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Enable event-triggered toddler moods",
                ref Settings.enableEventTriggeredMoods,
                "Toddlers gain mood buffs/debuffs when colony events happen (raids, weather, death, weddings, etc.).");

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Enable toddler self soothing",
                ref Settings.enableSelfSoothing,
                "Upset toddlers in cribs may attempt to calm themselves down through activities like thumb sucking, blanket clutching, and thinking happy thoughts. More common when mood is low.");

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Enable heater snuggling",
                ref Settings.enableHeaterSnuggling,
                "When outdoor temperature drops below a toddler's comfortable minimum, toddlers will seek indoor heaters and idle next to them for warmth. They may bring a plush toy if one is available.");

            if (Settings.enableHeaterSnuggling)
            {
                listing.Gap(4f);
                listing.CheckboxLabeled(
                    "Allow geothermal snuggling",
                    ref Settings.allowGeothermalSnuggling,
                    "When enabled, toddlers may also snuggle next to geothermal generators and steam geysers. Default OFF because these are industrial/natural heat sources rather than safe indoor heaters.");
            }

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Mute baby / toddler crying sounds",
                ref Settings.muteBabyCryingSound,
                "When enabled, babies and toddlers in crying mental states will NOT play crying audio. Visual tears and mood effects still apply. Accessibility option.");

            // Cosmetic toggle (default OFF = animations play). Sits next to the other
            // presentation/accessibility option (mute crying) on this tab.
            listing.Gap(12f);
            listing.CheckboxLabeled(
                "TPE_Settings_DisableToddlerPlayAnimations_Label".Translate(),
                ref Settings.disableToddlerPlayAnimations,
                "TPE_Settings_DisableToddlerPlayAnimations_Tooltip".Translate());

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "TPE_Settings_RenderPlushWhileSleeping_Label".Translate(),
                ref Settings.renderPlushWhileSleeping,
                "TPE_Settings_RenderPlushWhileSleeping_Tooltip".Translate());

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "TPE_Settings_AlwaysSeekPlushRegardlessOfRest_Label".Translate(),
                ref Settings.alwaysSeekPlushRegardlessOfRest,
                "TPE_Settings_AlwaysSeekPlushRegardlessOfRest_Tooltip".Translate());

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Enable crib confinement priority scaling",
                ref Settings.enableConfinementPriorityScaling,
                "When enabled, crawler toddlers' escape-crib priority increases as confinement builds: Tier 1 (Restless)=2.0f, Tier 2 (Frustrated)=3.0f, Tier 3 (Desperate)=4.5f. When mental break triggers, priority resets to 2.0f (panic mode, forgets the gate exists).");

            listing.Gap(12f);
            listing.Label(
                $"Global bubble rendering limit: {Settings.maxBubblesPerMap}",
                -1f,
                (TipSignal)"Maximum bubbles that can render at once across the entire map. Prevents performance issues with many bubble machines active. Range: 1–100.");
            Settings.maxBubblesPerMap = Mathf.RoundToInt(listing.Slider(Settings.maxBubblesPerMap, 1f, 100f));

            listing.Gap(8f);
            listing.Label(
                $"Bubble opacity: {Settings.bubbleOpacity:P0}",
                -1f,
                (TipSignal)"Controls how transparent or opaque bubbles appear when rendered. 100% = fully opaque (bubble texture as authored). Lower values make bubbles more see-through. Applies instantly to bubbles already on screen.");
            Settings.bubbleOpacity = listing.Slider(Settings.bubbleOpacity, 0f, 1f);
        }

        /// <summary>
        /// Children category (non-play-need).
        /// Contains: plush-to-bed system, child crying system,
        /// and mute child crying sounds.
        /// </summary>
        private void DrawChildrenTab(Listing_Standard listing)
        {
            listing.CheckboxLabeled(
                "Enable children (3–6) plush-to-bed system",
                ref Settings.enableChildPlushSleep,
                "Children aged 3–6 will autonomously seek and equip plush toys before sleeping, receiving comfort thoughts when bedding down with one. Mirrors the toddler system but for older children.");

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Enable older children (7–12) plush-to-bed system",
                ref Settings.enableOlderChildPlushSleep,
                "Independent of the 3–6 toggle above. Children aged 7–12 will also autonomously seek and equip plush toys before sleeping, receiving the same comfort thoughts as younger children when bedding down with one.");

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Enable child crying system (3–6)",
                ref Settings.enableChildCrying,
                "Children aged 3–6 who become very unhappy may cry, seek comfort from adults, and disturb nearby colonists. When disabled, children behave like vanilla pawns with no crying mechanics.");

            listing.Gap(12f);
            listing.CheckboxLabeled(
                "Mute child crying sounds (3–6)",
                ref Settings.muteChildCryingSound,
                "When enabled, children in crying or tantrum mental states will NOT play crying audio. Visual tears and mood effects still apply. Accessibility option.");
        }

        /// <summary>
        /// Adults (13+) category. Contains the master toggle for the adult
        /// plush-to-bed system and, when enabled, the 4-way mood-mode dropdown
        /// controlling which (if any) of the two adult plush-sleep thoughts can apply.
        /// </summary>
        private void DrawAdultsTab(Listing_Standard listing)
        {
            listing.CheckboxLabeled(
                "TPE_Settings_EnableAdultPlushSleep_Label".Translate(),
                ref Settings.enableAdultPlushSleep,
                "TPE_Settings_EnableAdultPlushSleep_Tooltip".Translate());

            if (!Settings.enableAdultPlushSleep)
            {
                return;
            }

            listing.Gap(12f);
            listing.Label("TPE_Settings_AdultMoodMode_Label".Translate());

            // Dropdown button showing the currently selected mode. Clicking opens
            // a FloatMenu with all four options — matches the exact wording the
            // user specified: "No moodlets", "Only negative moodlet for not
            // sleeping with plushie", "Only positive moodlet for sleeping with
            // plushie", "Moodlets for both options".
            Rect dropdownRect = listing.GetRect(30f);
            if (Widgets.ButtonText(dropdownRect, AdultMoodModeLabel(Settings.adultPlushieMoodMode)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("TPE_Settings_AdultMoodMode_None".Translate(),
                        () => Settings.adultPlushieMoodMode = AdultPlushieMoodMode.None),
                    new FloatMenuOption("TPE_Settings_AdultMoodMode_NegativeOnly".Translate(),
                        () => Settings.adultPlushieMoodMode = AdultPlushieMoodMode.NegativeOnly),
                    new FloatMenuOption("TPE_Settings_AdultMoodMode_PositiveOnly".Translate(),
                        () => Settings.adultPlushieMoodMode = AdultPlushieMoodMode.PositiveOnly),
                    new FloatMenuOption("TPE_Settings_AdultMoodMode_Both".Translate(),
                        () => Settings.adultPlushieMoodMode = AdultPlushieMoodMode.Both),
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(6f);
            listing.Label(AdultMoodModeDescription(Settings.adultPlushieMoodMode));
        }

        /// <summary>Short button-face label for the currently selected <see cref="AdultPlushieMoodMode"/>.</summary>
        private static string AdultMoodModeLabel(AdultPlushieMoodMode mode)
        {
            switch (mode)
            {
                case AdultPlushieMoodMode.None: return "TPE_Settings_AdultMoodMode_None".Translate();
                case AdultPlushieMoodMode.NegativeOnly: return "TPE_Settings_AdultMoodMode_NegativeOnly".Translate();
                case AdultPlushieMoodMode.PositiveOnly: return "TPE_Settings_AdultMoodMode_PositiveOnly".Translate();
                case AdultPlushieMoodMode.Both: return "TPE_Settings_AdultMoodMode_Both".Translate();
                default: return mode.ToString();
            }
        }

        /// <summary>Longer explanatory line shown under the dropdown for the currently selected mode.</summary>
        private static string AdultMoodModeDescription(AdultPlushieMoodMode mode)
        {
            switch (mode)
            {
                case AdultPlushieMoodMode.None: return "TPE_Settings_AdultMoodMode_None_Desc".Translate();
                case AdultPlushieMoodMode.NegativeOnly: return "TPE_Settings_AdultMoodMode_NegativeOnly_Desc".Translate();
                case AdultPlushieMoodMode.PositiveOnly: return "TPE_Settings_AdultMoodMode_PositiveOnly_Desc".Translate();
                case AdultPlushieMoodMode.Both: return "TPE_Settings_AdultMoodMode_Both_Desc".Translate();
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Children's Play Need category.
        /// Contains: debug logging, psychopath play-mood toggle, master toggle,
        /// and (while the master toggle is ON) age-gated desires, decay slider,
        /// growth-weight slider, play when idle, always include Active Play,
        /// disable child play animations, and show dirt castles.
        /// </summary>
        private void DrawChildrensPlayNeedTab(Listing_Standard listing)
        {
            // Debug options at the very top
            listing.CheckboxLabeled(
                "[DEBUG] Debug Logging",
                ref Settings.debugLogging,
                "Enable detailed debug logging across the entire mod (crib gate, heater snuggling, play activities, AI allocation, etc.). Useful for troubleshooting. Logs do not affect gameplay—purely diagnostic.");

            listing.GapLine(12f);

            // Cross-cutting toggle: affects BOTH toddler Self Soothing thoughts
            // (Toddlers/Newborns tab feature) and the child play-need thoughts
            // below, so it's drawn unconditionally at the top of this tab rather
            // than gated behind "enableChildrenPlayNeed".
            listing.CheckboxLabeled(
                "TPE_Settings_KeepPlayMoodForPsychopaths_Label".Translate(),
                ref Settings.keepPlayMoodForPsychopaths,
                "TPE_Settings_KeepPlayMoodForPsychopaths_Tooltip".Translate());

            listing.GapLine(12f);

            listing.CheckboxLabeled(
                "Enable children play-need system (3–13)",
                ref Settings.enableChildrenPlayNeed,
                "Children gain a dedicated Play need alongside Learning. They'll develop Play Desires, run around, jump, spin, and chase each other — just like real kids! Growth points are blended from both needs so they must balance fun and study.");

            if (Settings.enableChildrenPlayNeed)
            {
                listing.Gap(4f);
                listing.CheckboxLabeled(
                    "Age-gated play desires",
                    ref Settings.ageGatedPlayDesires,
                    "When ON, each play desire is restricted to specific age groups (e.g. plush toys only for ages 3–6). When OFF, any child (3–13) can receive any play desire.");

                listing.Gap(8f);
                listing.Label($"Play need decay time: {Settings.playNeedDecayDays:F1} days");
                Settings.playNeedDecayDays = listing.Slider(Settings.playNeedDecayDays, 3f, 14f);
                listing.Label("How long it takes for a child's play need to drain from full to empty.");

                listing.Gap(8f);
                listing.Label($"Play weight in growth: {Settings.playWeightInGrowth:F2}");
                Settings.playWeightInGrowth = listing.Slider(Settings.playWeightInGrowth, 0f, 1f);
                listing.Label("How much play need contributes to growth-point gain. 0.5 = average of learning and play.");

                listing.Gap(12f);
                listing.CheckboxLabeled(
                    "TPE_Settings_PlayWhenIdle_Label".Translate(),
                    ref Settings.playWhenIdle,
                    "TPE_Settings_PlayWhenIdle_Tooltip".Translate());

                listing.Gap(12f);
                listing.CheckboxLabeled(
                    "TPE_Settings_GuaranteeActivePlay_Label".Translate(),
                    ref Settings.guaranteeActivePlayDesire,
                    "TPE_Settings_GuaranteeActivePlay_Tooltip".Translate());

                // Cosmetic toggle (default OFF = animations play). Placed right under
                // "Always include Active Play" because Active Play is the only child play
                // desire that animates; hidden with the rest of this block when the
                // play-need system is off (Active Play can't be rolled then anyway).
                listing.Gap(12f);
                listing.CheckboxLabeled(
                    "TPE_Settings_DisableChildPlayAnimations_Label".Translate(),
                    ref Settings.disableChildPlayAnimations,
                    "TPE_Settings_DisableChildPlayAnimations_Tooltip".Translate());

                // Dirt-castle visual toggle (default ON). Sits with the other
                // play-desire options; hidden along with them when the whole
                // play-need system is disabled.
                listing.Gap(12f);
                listing.CheckboxLabeled(
                    "TPE_Settings_ShowDirtCastles_Label".Translate(),
                    ref Settings.showDirtCastles,
                    "TPE_Settings_ShowDirtCastles_Tooltip".Translate());
            }
        }

        public override string SettingsCategory()
        {
            return "Toddlers Play Expanded";
        }
    }
}
