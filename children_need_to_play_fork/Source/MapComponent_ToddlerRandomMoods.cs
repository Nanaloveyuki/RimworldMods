using System.Collections.Generic;
using System.Linq;
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

        public MapComponent_ToddlerRandomMoods(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (!TPE_Mod.Settings.enableRandomToddlerMoods) return;
            if (!map.IsHashIntervalTick(CheckInterval)) return;

            // Copy the list — AllPawnsSpawned is a cached mutable list.
            List<Pawn> allSpawned = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            foreach (Pawn pawn in allSpawned)
            {
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

            // Gather existing random moods and find the oldest one.
            List<Thought_Memory> existingRandom = new List<Thought_Memory>();
            Thought_Memory oldest = null;
            int oldestAge = -1;

            foreach (Thought_Memory mem in memories.Memories)
            {
                string defName = mem.def.defName;
                if (defName.StartsWith("TPE_RandomMoodPos_") || defName.StartsWith("TPE_RandomMoodNeg_"))
                {
                    existingRandom.Add(mem);
                    if (mem.age > oldestAge)
                    {
                        oldestAge = mem.age;
                        oldest = mem;
                    }
                }
            }

            int maxActive = TPE_Mod.Settings.randomMoodMaxActive;
            if (existingRandom.Count >= maxActive && oldest != null)
            {
                memories.RemoveMemory(oldest);
            }

            // 50 / 50 split between positive and negative.
            bool positive = Rand.Value < 0.5f;
            int magnitude = PickWeightedMagnitude();

            // Find all matching thoughts for this magnitude and sign.
            string prefix = positive
                ? $"TPE_RandomMoodPos_{magnitude}_"
                : $"TPE_RandomMoodNeg_{magnitude}_";

            List<ThoughtDef> candidates = new List<ThoughtDef>();
            foreach (ThoughtDef def in DefDatabase<ThoughtDef>.AllDefsListForReading)
            {
                if (def.defName.StartsWith(prefix))
                    candidates.Add(def);
            }

            if (candidates.Count == 0) return;

            ThoughtDef selected = candidates.RandomElement();
            memories.TryGainMemory(selected);
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
