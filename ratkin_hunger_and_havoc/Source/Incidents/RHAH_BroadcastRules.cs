using System.Collections.Generic;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_BroadcastRules
    {
        internal const int MinCooldownDays = 0;
        internal const int MaxCooldownDays = 10;
        internal const int DefaultCooldownDays = 3;

        internal static int ClampDays(int days)
        {
            if (days < MinCooldownDays)
            {
                return MinCooldownDays;
            }

            return days > MaxCooldownDays ? MaxCooldownDays : days;
        }

        internal static bool Ready(int tick, int cooldownUntilTick)
        {
            return tick >= cooldownUntilTick;
        }

        internal static int NextCooldown(int tick, int days)
        {
            return tick + RHAH_RequestRules.TicksPerDay * ClampDays(days);
        }

        internal static int ResponseCount(bool famine, float multiplier)
        {
            return famine ? (int)System.Math.Ceiling(Core.GameComponent_RHAH_FamineYear.ClampMultiplier(multiplier)) : 1;
        }

        internal static List<string> Candidates(IReadOnlyList<RHAH_IncidentEntry> entries, ISet<string> disabled)
        {
            List<string> result = new List<string>();
            if (entries == null)
            {
                return result;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RHAH_IncidentEntry entry = entries[i];
                if (entry == null || !entry.BroadcastEligible)
                {
                    continue;
                }

                if (disabled != null && disabled.Contains(entry.DisplayId))
                {
                    continue;
                }

                result.Add(entry.DisplayId);
            }

            return result;
        }

        internal static int GroupCount(IReadOnlyList<string> candidates)
        {
            int count = 0;
            for (int i = 0; i < RHAH_IncidentGroups.Count; i++)
            {
                if (ScenarioCount(candidates, i) > 0)
                {
                    count++;
                }
            }
            return count;
        }

        internal static string Pick(IReadOnlyList<string> candidates, float roll)
        {
            int groupCount = GroupCount(candidates);
            if (float.IsNaN(roll) || float.IsInfinity(roll) || roll < 0f || roll >= groupCount)
            {
                return null;
            }
            for (int i = 0; i < RHAH_IncidentGroups.Count; i++)
            {
                int count = ScenarioCount(candidates, i);
                if (count == 0)
                {
                    continue;
                }
                if (roll >= 1f)
                {
                    roll -= 1f;
                    continue;
                }
                int scenarioRoll = System.Math.Min((int)(roll * count), count - 1);
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (RHAH_IncidentGroups.GroupOf(candidates[j]) == i && scenarioRoll-- == 0)
                    {
                        return candidates[j];
                    }
                }
            }
            return null;
        }

        internal static int ScenarioCount(IReadOnlyList<string> candidates, int group)
        {
            int count = 0;
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (RHAH_IncidentGroups.GroupOf(candidates[i]) == group)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }

    internal static class RHAH_GenerationQueue
    {
        internal const int DefaultBatchSize = 1;

        internal static int Take(int pending, int batchSize, bool enabled)
        {
            if (!enabled || pending <= 0)
            {
                return pending <= 0 ? 0 : pending;
            }

            int size = batchSize < 1 ? 1 : batchSize;
            return pending < size ? pending : size;
        }
    }
}
