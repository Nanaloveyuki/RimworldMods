using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Toddlers;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Periodically rolls for random mood swings on awake toddlers.
    /// 
    /// Every ~1 in-game hour each toddler has a configurable chance to gain one
    /// of 80 situation-agnostic mood memories across 4 sets (+1 through +10
    /// positive, -1 through -10 negative per set).  Higher magnitudes are
    /// weighted to be rarer.
    /// 
    /// A toddler may have up to N random moods active at once (configurable,
    /// default 3).  When the cap is reached the oldest random mood is replaced.
    /// 
    /// The entire system can be disabled in Mod Settings.
    /// </summary>
    public class MapComponent_ToddlerRandomMoods : MapComponent
    {
        /// <summary>Tick interval between mood-roll checks (~1 in-game hour).</summary>
        private const int CheckInterval = 2500;

        /// <summary>
        /// Weights for magnitude selection. Index 0 corresponds to magnitude 1.
        /// Higher magnitudes receive lower weights, making them rarer.
        /// </summary>
        private static readonly int[] MagnitudeWeights = new int[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };

        /// <summary>Pre-computed sum of all magnitude weights.</summary>
        private static readonly int TotalWeight = ComputeTotalWeight();

        private static readonly List<ThoughtDef>?[] MoodCandidates = new List<ThoughtDef>?[20];

        public MapComponent_ToddlerRandomMoods(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (!TPE_Mod.Settings.enableRandomToddlerMoods) return;
            if (!map.IsHashIntervalTick(CheckInterval)) return;

            // Applying a memory does not change the map's spawned-pawn registry.
            IReadOnlyList<Pawn> allSpawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allSpawned.Count; i++)
            {
                Pawn pawn = allSpawned[i];
                if (!ToddlerUtility.IsToddler(pawn)) continue;
                if (pawn.Dead || pawn.Downed) continue;
                if (!pawn.Awake()) continue; // toddlers only react while awake

                if (Rand.Value > TPE_Mod.Settings.randomMoodFrequency) continue;

                TryApplyRandomMood(pawn);
            }
        }

        /// <summary>
        /// Attempts to apply a single random mood memory to the toddler.
        /// Respects the configured cap and evicts the oldest mood if needed.
        /// </summary>
        private void TryApplyRandomMood(Pawn pawn)
        {
            MemoryThoughtHandler memories = pawn.needs?.mood?.thoughts?.memories;
            if (memories == null) return;

            int existingRandomCount = 0;
            Thought_Memory oldest = null;
            int oldestAge = -1;

            foreach (Thought_Memory mem in memories.Memories)
            {
                string defName = mem.def.defName;
                if (defName.StartsWith("TPE_RandomMoodPos_", StringComparison.Ordinal) ||
                    defName.StartsWith("TPE_RandomMoodNeg_", StringComparison.Ordinal))
                {
                    existingRandomCount++;
                    if (mem.age > oldestAge)
                    {
                        oldestAge = mem.age;
                        oldest = mem;
                    }
                }
            }

            int maxActive = TPE_Mod.Settings.randomMoodMaxActive;
            if (existingRandomCount >= maxActive && oldest != null)
            {
                memories.RemoveMemory(oldest);
            }

            // 50 / 50 split between positive and negative.
            bool positive = Rand.Value < 0.5f;
            int magnitude = PickWeightedMagnitude();

            List<ThoughtDef> candidates = GetMoodCandidates(positive, magnitude);

            if (candidates.Count == 0) return;

            ThoughtDef selected = candidates.RandomElement();
            memories.TryGainMemory(selected);
        }

        private static List<ThoughtDef> GetMoodCandidates(bool positive, int magnitude)
        {
            int index = (positive ? 0 : 10) + magnitude - 1;
            List<ThoughtDef>? candidates = MoodCandidates[index];
            if (candidates != null) return candidates;

            string prefix = positive
                ? $"TPE_RandomMoodPos_{magnitude}_"
                : $"TPE_RandomMoodNeg_{magnitude}_";
            candidates = new List<ThoughtDef>();
            List<ThoughtDef> defs = DefDatabase<ThoughtDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ThoughtDef def = defs[i];
                if (def.defName.StartsWith(prefix, StringComparison.Ordinal))
                    candidates.Add(def);
            }
            MoodCandidates[index] = candidates;
            return candidates;
        }

        /// <summary>
        /// Selects a magnitude (1–10) using the static weight table.
        /// Higher magnitudes are less likely.
        /// </summary>
        private int PickWeightedMagnitude()
        {
            int roll = Rand.Range(0, TotalWeight);
            int cumulative = 0;

            for (int i = 0; i < MagnitudeWeights.Length; i++)
            {
                cumulative += MagnitudeWeights[i];
                if (roll < cumulative)
                    return i + 1; // magnitudes are 1-based
            }

            return 1; // fallback
        }

        private static int ComputeTotalWeight()
        {
            int sum = 0;
            foreach (int w in MagnitudeWeights) sum += w;
            return sum;
        }
    }
}
