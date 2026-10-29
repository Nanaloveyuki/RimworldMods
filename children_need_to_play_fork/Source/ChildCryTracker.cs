using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Static tracker for the First-Cry 1-Hour Cap and Mood-Debuff Tantrum systems.
    ///
    /// FIRST-CRY CAP:
    ///   When a child's mood drops below 30% for the first time in an episode,
    ///   a 1-hour timer starts. If the child hasn't cried naturally via MTB by
    ///   the time the timer expires, crying is forced. Once crying starts (for
    ///   any reason), the cap is satisfied and normal MTB resumes.
    ///
    /// TANTRUM SYSTEM:
    ///   When a child gains a new mood thought with severity ≤ -12, there's a
    ///   chance they immediately enter a short tantrum cry. Chance scales with
    ///   severity. 1-hour cooldown per child prevents spam.
    ///   Need-based thoughts (e.g. Starving) are caught by the periodic scan in
    ///   TickFirstCryCap because they don't go through MemoryThoughtHandler.
    ///
    /// NOTE: Timer data is NOT saved across loads. On reload, a child below 30%
    /// mood will get a fresh 1-hour timer. This is a minor soft-timer desync.
    /// </summary>
    public static class ChildCryTracker
    {
        // ═══════════════════════════════════════════════════════════════════
        //  FIRST-CRY EPISODE TRACKING
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Per-pawn data for a single "below 30% mood" episode.
        /// Reset when mood recovers to 30% or higher.
        /// </summary>
        private class EpisodeData
        {
            /// <summary>
            /// Game tick when mood first dropped below 30% this episode.
            /// -1 means the episode is not currently being tracked.
            /// </summary>
            public int firstEligibleTick = -1;

            /// <summary>
            /// True once the child has entered a crying mental state this episode.
            /// Prevents the 1-hour cap from firing again until mood recovers.
            /// </summary>
            public bool hasCriedThisEpisode = false;
        }

        /// <summary>
        /// Maps pawn ThingID → current episode data.
        /// Using string keys avoids holding references to dead/destroyed pawns.
        /// </summary>
        private static Dictionary<string, EpisodeData> episodeData = new Dictionary<string, EpisodeData>();

        /// <summary>1 in-game hour = 2500 ticks (60 seconds at 1× speed).</summary>
        private const int FirstCryTimeoutTicks = 2500;

        // ═══════════════════════════════════════════════════════════════════
        //  TANTRUM COOLDOWN TRACKING
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Maps pawn ThingID → tick of their last tantrum cry.
        /// </summary>
        private static Dictionary<string, int> lastTantrumTick = new Dictionary<string, int>();

        /// <summary>Tantrum cooldown duration: 1 in-game hour.</summary>
        private const int TantrumCooldownTicks = 2500;

        /// <summary>
        /// Maps pawn ThingID → set of severe thought defNames they've already
        /// thrown a tantrum for. Cleared when the thought is no longer active.
        /// Prevents re-tantruming every 150 ticks while the same severe thought
        /// (e.g. Starving) remains active.
        /// </summary>
        private static Dictionary<string, HashSet<string>> tantrumedThoughtDefs = new Dictionary<string, HashSet<string>>();

        // ═══════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Tick of the last global cleanup of dead-pawn entries.</summary>
        private static int lastCleanupTick = -1;

        /// <summary>Run cleanup every 6 in-game hours (15000 ticks).</summary>
        private const int CleanupIntervalTicks = 15000;

        /// <summary>
        /// Removes dictionary entries for pawns that are dead, destroyed, or no longer
        /// on any map. Prevents unbounded growth over long colonies.
        /// </summary>
        private static void CleanupDeadPawns()
        {
            int now = Find.TickManager.TicksGame;
            if (now - lastCleanupTick < CleanupIntervalTicks)
                return;
            lastCleanupTick = now;

            HashSet<string> toRemove = new HashSet<string>();

            // Check episode data
            foreach (var kvp in episodeData)
            {
                if (IsPawnDeadOrGone(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (string key in toRemove)
                episodeData.Remove(key);
            toRemove.Clear();

            // Check tantrum cooldowns
            foreach (var kvp in lastTantrumTick)
            {
                if (IsPawnDeadOrGone(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (string key in toRemove)
                lastTantrumTick.Remove(key);
            toRemove.Clear();

            // Check tantrumed thought sets
            foreach (var kvp in tantrumedThoughtDefs)
            {
                if (IsPawnDeadOrGone(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (string key in toRemove)
                tantrumedThoughtDefs.Remove(key);
        }

        /// <summary>
        /// Returns true if the pawn with the given ThingID is dead or destroyed.
        /// Pawns in caravans or on other maps are NOT considered gone — their
        /// timer data should persist so the cap works when they return.
        /// </summary>
        private static bool IsPawnDeadOrGone(string thingID)
        {
            // Search all maps (including the current one)
            foreach (Map m in Find.Maps)
            {
                Pawn p = m.mapPawns.AllPawns.FirstOrDefault(x => x.ThingID == thingID);
                if (p != null)
                    return p.Dead || p.Destroyed;
            }

            // Search world pawns (caravans, traveling, etc.)
            if (Find.WorldPawns != null)
            {
                Pawn wp = Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(x => x.ThingID == thingID);
                if (wp != null)
                    return wp.Dead || wp.Destroyed;
            }

            // Pawn not found anywhere — likely despawned/destroyed without a corpse.
            return true;
        }

        /// <summary>
        /// Called every time MentalFitGenerator ticks for a pawn (every 150 ticks).
        /// Manages the first-cry timer and forces crying if the 1-hour cap is reached.
        /// Also scans for severe need-based thoughts (e.g. Starving) that don't go
        /// through MemoryThoughtHandler.TryGainMemory and triggers tantrums for them.
        /// </summary>
        public static void TickFirstCryCap(Pawn pawn)
        {
            CleanupDeadPawns();
            // Only process eligible children.
            if (!IsCryingEligibleChild(pawn))
            {
                ClearEpisodeData(pawn.ThingID);
                return;
            }

            // ── Severe-thought tantrum scan (ALWAYS, regardless of mood threshold) ──
            // Need-based thoughts (Starving, etc.) are situational and never go through
            // MemoryThoughtHandler.TryGainMemory. We scan all active mood thoughts here
            // to catch any severe mood debuff, even if the child is in a good mood.
            CheckSevereThoughtTantrum(pawn);

            // ── First-Cry Cap logic ──
            float moodPct = pawn.needs.mood.CurLevelPercentage;

            // Mood recovered above 30% — clear tracker for a fresh episode next time.
            if (moodPct >= 0.30f)
            {
                ClearEpisodeData(pawn.ThingID);
                return;
            }

            // Mood is below 30%.
            EpisodeData data = GetOrCreateEpisodeData(pawn.ThingID);

            // First time dropping below 30% this episode — start the timer.
            if (data.firstEligibleTick < 0)
            {
                data.firstEligibleTick = Find.TickManager.TicksGame;
            }

            // Already cried this episode — nothing more to do.
            if (data.hasCriedThisEpisode)
                return;

            // Already in a crying mental state (naturally triggered) — mark as cried.
            if (IsInCryingState(pawn))
            {
                data.hasCriedThisEpisode = true;
                return;
            }

            // Check if 1 in-game hour has elapsed since first dropping below 30%.
            int elapsed = Find.TickManager.TicksGame - data.firstEligibleTick;
            if (elapsed >= FirstCryTimeoutTicks)
            {
                // Force crying. TryStartMentalState returns false if the pawn is sleeping
                // (recoverFromSleep=false in BaseBabyMentalFit) — that's fine, let them sleep.
                bool started = false;
                if (pawn.mindState?.mentalStateHandler != null)
                {
                    started = pawn.mindState.mentalStateHandler.TryStartMentalState(
                        TPE_MentalStateDefOf.TPE_ChildCrying,
                        reason: "Crying from prolonged low mood",
                        causedByMood: true);
                }

                if (started)
                {
                    data.hasCriedThisEpisode = true;
                }
            }
        }

        /// <summary>
        /// Called from the ThoughtHandler patch when a severe mood debuff is gained.
        /// Rolls for a tantrum cry if the pawn is eligible, not already crying,
        /// and not on cooldown.
        /// </summary>
        /// <param name="thoughtDefName">Optional defName of the triggering thought.
        /// When provided, the defName is recorded so the periodic scan won't re-trigger.</param>
        public static void TryTriggerTantrum(Pawn pawn, float moodEffect, string thoughtDefName = null)
        {
            if (!IsCryingEligibleChild(pawn))
                return;

            if (IsInCryingState(pawn))
                return;

            if (IsTantrumOnCooldown(pawn))
                return;

            float chance = GetTantrumChance(moodEffect);
            if (Rand.Chance(chance))
            {
                bool started = false;
                if (pawn.mindState?.mentalStateHandler != null)
                {
                    started = pawn.mindState.mentalStateHandler.TryStartMentalState(
                        TPE_MentalStateDefOf.TPE_ChildTantrum,
                        reason: "Crying from a severe mood debuff",
                        causedByMood: true);
                }

                if (started)
                {
                    RecordTantrum(pawn);
                    // Also mark the episode as cried so the 1-hour cap doesn't fire later.
                    GetOrCreateEpisodeData(pawn.ThingID).hasCriedThisEpisode = true;
                    // Record the thought def so the periodic scan doesn't re-trigger.
                    if (!string.IsNullOrEmpty(thoughtDefName))
                    {
                        GetOrCreateTantrumedSet(pawn.ThingID).Add(thoughtDefName);
                    }
                }
            }
        }

        /// <summary>
        /// Called from MentalState_ChildCry.PostStart when ANY crying state begins.
        /// Marks the episode as cried so the 1-hour cap is disabled.
        /// </summary>
        public static void OnCryStarted(Pawn pawn)
        {
            if (pawn == null)
                return;
            GetOrCreateEpisodeData(pawn.ThingID).hasCriedThisEpisode = true;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scans all currently active mood thoughts (memory + situational + need-based)
        /// for any with severity ≤ -12. If a severe thought is found that we haven't
        /// already reacted to, rolls for a tantrum. Clears old reacted defNames when
        /// the corresponding thought is no longer active.
        /// </summary>
        private static void CheckSevereThoughtTantrum(Pawn pawn)
        {
            if (IsInCryingState(pawn)) return;
            if (IsTantrumOnCooldown(pawn)) return;

            // Collect all current severe thought defNames.
            HashSet<string> currentSevere = new HashSet<string>();
            var thoughtHandler = pawn.needs?.mood?.thoughts;
            if (thoughtHandler != null)
            {
                // GetAllMoodThoughts includes memories AND situational thoughts.
                List<Thought> allThoughts = new List<Thought>();
                thoughtHandler.GetAllMoodThoughts(allThoughts);
                foreach (Thought t in allThoughts)
                {
                    if (t == null || t.def == null) continue;
                    float offset = t.MoodOffset();
                    if (offset <= -12f)
                    {
                        currentSevere.Add(t.def.defName);
                    }
                }
            }

            HashSet<string> reacted = GetOrCreateTantrumedSet(pawn.ThingID);

            // Trigger tantrum for the first new severe thought we find.
            foreach (string defName in currentSevere)
            {
                if (!reacted.Contains(defName))
                {
                    // Use -12 as the baseline severity; actual chance is capped by GetTantrumChance.
                    TryTriggerTantrum(pawn, -12f, defName);
                    break; // Only attempt one tantrum per scan tick.
                }
            }

            // Clean up: remove defNames that are no longer active so they can trigger again later.
            reacted.RemoveWhere(d => !currentSevere.Contains(d));
        }

        /// <summary>
        /// Returns the set of thought defNames this pawn has already tantrumed for.
        /// </summary>
        private static HashSet<string> GetOrCreateTantrumedSet(string key)
        {
            if (!tantrumedThoughtDefs.TryGetValue(key, out HashSet<string> set))
            {
                set = new HashSet<string>();
                tantrumedThoughtDefs[key] = set;
            }
            return set;
        }

        /// <summary>
        /// Returns true if the pawn is a living child aged 3–6 with a mood need
        /// AND the child crying system is enabled in mod settings.
        /// This is the universal eligibility gate for ALL crying systems.
        /// </summary>
        public static bool IsCryingEligibleChild(Pawn pawn)
        {
            // System disabled — no child may enter crying states.
            if (TPE_Mod.Settings?.enableChildCrying != true)
                return false;

            if (pawn == null || pawn.Dead)
                return false;

            if (pawn.needs?.mood == null)
                return false;

            if (pawn.DevelopmentalStage != DevelopmentalStage.Child)
                return false;

            int age = pawn.ageTracker?.AgeBiologicalYears ?? 999;
            return age >= 3 && age <= 6;
        }

        /// <summary>
        /// Returns true if the pawn is currently in any child-crying mental state.
        /// </summary>
        private static bool IsInCryingState(Pawn pawn)
        {
            if (pawn.MentalState == null)
                return false;

            return pawn.MentalState.def == TPE_MentalStateDefOf.TPE_ChildCrying
                || pawn.MentalState.def == TPE_MentalStateDefOf.TPE_ChildTantrum;
        }

        /// <summary>
        /// Returns true if the pawn threw a tantrum within the last hour.
        /// </summary>
        private static bool IsTantrumOnCooldown(Pawn pawn)
        {
            string key = pawn.ThingID;
            if (!lastTantrumTick.TryGetValue(key, out int lastTick))
                return false;

            return Find.TickManager.TicksGame - lastTick < TantrumCooldownTicks;
        }

        /// <summary>
        /// Records that the pawn just threw a tantrum, starting the 1-hour cooldown.
        /// </summary>
        private static void RecordTantrum(Pawn pawn)
        {
            lastTantrumTick[pawn.ThingID] = Find.TickManager.TicksGame;
        }

        /// <summary>
        /// Returns the episode data for a pawn, creating it if necessary.
        /// </summary>
        private static EpisodeData GetOrCreateEpisodeData(string key)
        {
            if (!episodeData.TryGetValue(key, out EpisodeData data))
            {
                data = new EpisodeData();
                episodeData[key] = data;
            }
            return data;
        }

        /// <summary>
        /// Clears episode data for a pawn (called when mood recovers or pawn is invalid).
        /// </summary>
        private static void ClearEpisodeData(string key)
        {
            episodeData.Remove(key);
        }

        /// <summary>
        /// Maps a mood-debuff severity to the tantrum trigger chance.
        /// More negative = worse debuff = higher chance.
        /// </summary>
        private static float GetTantrumChance(float moodEffect)
        {
            // moodEffect is negative. -14 is worse than -12.
            if (moodEffect >= -14f)      // Range: -14, -13, -12
                return 0.20f;
            if (moodEffect >= -19f)      // Range: -19 to -15
                return 0.35f;
            if (moodEffect >= -24f)      // Range: -24 to -20
                return 0.50f;
                                         // -25 or worse
            return 0.70f;
        }
    }
}
