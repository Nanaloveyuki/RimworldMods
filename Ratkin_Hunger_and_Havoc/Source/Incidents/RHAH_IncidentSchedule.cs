using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_IncidentSchedule
    {
        internal const float DefaultDays = 15f;
        internal const float MinDays = 0f;
        internal const float MaxDays = 60f;
        internal const float GraceDays = 1f;
        internal const float MinWindowDays = 5f;
        internal const float MaxWindowDays = 360f;
        internal const int ChecksPerDay = 60;

        internal static float OccurrenceChance(float days)
        {
            days = ClampDays(days);
            if (days <= 0f)
            {
                return 0f;
            }

            return RimWorld.Storyteller.CheckInterval / (days * GenDate.TicksPerDay);
        }

        internal static float DailyOccurrenceChance(float averageDays)
        {
            float perCheck = OccurrenceChance(averageDays);
            if (perCheck <= 0f)
            {
                return 0f;
            }

            return 1f - (float)Math.Pow(1d - perCheck, ChecksPerDay);
        }

        internal static float ClampWindowDays(float days)
        {
            if (float.IsNaN(days) || days < MinWindowDays)
            {
                return MinWindowDays;
            }

            if (days > MaxWindowDays)
            {
                return MaxWindowDays;
            }

            return days;
        }

        internal static float ClampDays(float days)
        {
            if (float.IsNaN(days) || days < MinDays)
            {
                return DefaultDays;
            }

            if (days > MaxDays)
            {
                return MaxDays;
            }

            return days;
        }

        internal static float ClampPaceResult(float days)
        {
            if (float.IsNaN(days) || float.IsInfinity(days))
            {
                return DefaultDays;
            }

            if (days < MinDays)
            {
                return MinDays;
            }

            if (days > MaxDays)
            {
                return MaxDays;
            }

            return days;
        }


        internal static void QueueDueIncidents(float daysPassed, int trust, RHAH_IncidentSeason season, int checkIntervalTicks)
        {
            if (!RHAH_Runtime.AllowsNewContent || daysPassed <= GraceDays || Current.Game == null)
            {
                return;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                return;
            }

            GameComponent_RHAH_Game game = Current.Game.GetComponent<GameComponent_RHAH_Game>();
            if (game == null)
            {
                return;
            }

            int seasonIndex = SeasonIndex(season);
            float positiveDays = PoolDays(settings.positiveIncidentPace, settings.positiveIncidentDays, daysPassed, trust, seasonIndex);
            float negativeDays = PoolDays(settings.negativeIncidentPace, settings.negativeIncidentDays, daysPassed, trust, seasonIndex);
            List<IIncidentTarget> targets = Find.Storyteller?.AllIncidentTargets;
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                IIncidentTarget target = targets[i];
                if (target == null)
                {
                    continue;
                }

                bool mapHome = target is Map map && map.IsPlayerHome &&
                    RHAH_ApproachRules.AllowsHome(RHAH_Approach.SpaceHome(map), RHAH_Approach.SpaceApproachEnabled());
                bool playerCaravan = target is RimWorld.Planet.Caravan;
                if (!mapHome && !playerCaravan)
                {
                    continue;
                }

                TryQueue(game, target, RHAH_AttitudePool.Positive, positiveDays, trust, season, mapHome, playerCaravan, checkIntervalTicks);
                TryQueue(game, target, RHAH_AttitudePool.Negative, negativeDays, trust, season, mapHome, playerCaravan, checkIntervalTicks);
            }
        }

        static void TryQueue(
            GameComponent_RHAH_Game game,
            IIncidentTarget target,
            RHAH_AttitudePool pool,
            float days,
            int trust,
            RHAH_IncidentSeason season,
            bool mapHome,
            bool playerCaravan,
            int checkIntervalTicks)
        {
            if (days <= 0f || !Rand.MTBEventOccurs(days, GenDate.TicksPerDay, checkIntervalTicks))
            {
                return;
            }

            float total = TotalWeight(pool, trust, season, mapHome, playerCaravan);
            if (total <= 0f)
            {
                return;
            }

            string displayId = Select(pool, trust, season, mapHome, playerCaravan, Rand.Range(0f, total));
            if (displayId != null)
            {
                game.QueueIncident(displayId, PointsFor(displayId), TargetId(playerCaravan, RawId(target)));
            }
        }

        internal static float TotalWeight(
            RHAH_AttitudePool pool,
            int trust,
            RHAH_IncidentSeason season,
            bool mapHome,
            bool playerCaravan)
        {
            float total = 0f;
            for (int i = 0; i < RHAH_IncidentGroups.Count; i++)
            {
                total += GroupWeight(i, pool, trust, season, mapHome, playerCaravan, out _);
            }

            return total;
        }

        internal static string Select(
            RHAH_AttitudePool pool,
            int trust,
            RHAH_IncidentSeason season,
            bool mapHome,
            bool playerCaravan,
            float roll)
        {
            if (float.IsNaN(roll) || float.IsInfinity(roll) || roll < 0f)
            {
                return null;
            }
            float cursor = 0f;
            for (int i = 0; i < RHAH_IncidentGroups.Count; i++)
            {
                float weight = GroupWeight(i, pool, trust, season, mapHome, playerCaravan, out int count);
                if (weight <= 0f)
                {
                    continue;
                }
                float next = cursor + weight;
                if (roll < next)
                {
                    float scenarioRoll = (roll - cursor) * count;
                    RHAH_IncidentEntry last = null;
                    IReadOnlyList<RHAH_IncidentEntry> scenarios = RHAH_IncidentGroups.Scenarios(i);
                    for (int j = 0; j < scenarios.Count; j++)
                    {
                        float scenarioWeight = WeightOf(scenarios[j], pool, trust, season, mapHome, playerCaravan);
                        if (scenarioWeight <= 0f)
                        {
                            continue;
                        }
                        last = scenarios[j];
                        if (scenarioRoll < scenarioWeight)
                        {
                            return scenarios[j].DisplayId;
                        }
                        scenarioRoll -= scenarioWeight;
                    }
                    return last?.DisplayId;
                }
                cursor = next;
            }
            return null;
        }

        static float GroupWeight(
            int group,
            RHAH_AttitudePool pool,
            int trust,
            RHAH_IncidentSeason season,
            bool mapHome,
            bool playerCaravan,
            out int count)
        {
            IReadOnlyList<RHAH_IncidentEntry> scenarios = RHAH_IncidentGroups.Scenarios(group);
            float total = 0f;
            count = 0;
            if (RHAH_IncidentGroups.PoolOf(group) != pool)
            {
                return 0f;
            }
            for (int i = 0; i < scenarios.Count; i++)
            {
                RHAH_IncidentEntry entry = scenarios[i];
                if (!RHAH_IncidentWeight.TargetMatches(entry.Target, mapHome, playerCaravan))
                {
                    continue;
                }
                count++;
                total += WeightOf(entry, pool, trust, season, mapHome, playerCaravan);
            }
            // 场景数量不增加同类事件的抽取份额 关闭场景仍保留零权重
            return count == 0 ? 0f : total / count;
        }

        static float WeightOf(
            RHAH_IncidentEntry entry,
            RHAH_AttitudePool pool,
            int trust,
            RHAH_IncidentSeason season,
            bool mapHome,
            bool playerCaravan)
        {
            if (RHAH_IncidentGroups.PoolOf(entry.DisplayId) != pool ||
                !GameComponent_RHAH_FamineYear.Available(entry.DisplayId))
            {
                return 0f;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            RHAH_WeightFactors factors = settings == null ? RHAH_WeightFactors.Defaults() : settings.WeightFactors();
            float formula = RHAH_IncidentWeight.Evaluate(new RHAH_IncidentWeightInput(
                entry.Family,
                entry.Category,
                entry.Target,
                pool,
                season,
                trust,
                mapHome,
                playerCaravan), factors);
            bool enabled = settings == null || settings.IsIncidentEnabled(entry.DisplayId);
            float playerWeight = settings == null
                ? RHAH_IncidentTuning.DefaultWeight
                : settings.IncidentWeight(entry.DisplayId);
            return RHAH_IncidentTuning.Scale(formula, enabled, playerWeight);
        }

        internal static RHAH_IncidentSeason SeasonOf(IIncidentTarget target)
        {
            if (target == null || !target.Tile.Valid)
            {
                return RHAH_IncidentSeason.Undefined;
            }

            switch (GenLocalDate.Season(target.Tile))
            {
                case Season.Spring: return RHAH_IncidentSeason.Spring;
                case Season.Summer: return RHAH_IncidentSeason.Summer;
                case Season.Fall: return RHAH_IncidentSeason.Fall;
                case Season.Winter: return RHAH_IncidentSeason.Winter;
                default: return RHAH_IncidentSeason.Undefined;
            }
        }

        internal static int SeasonIndex(RHAH_IncidentSeason season)
        {
            switch (season)
            {
                case RHAH_IncidentSeason.Spring: return 0;
                case RHAH_IncidentSeason.Summer: return 1;
                case RHAH_IncidentSeason.Fall: return 2;
                case RHAH_IncidentSeason.Winter: return 3;
                default: return 0;
            }
        }

        internal static float PoolDays(string formula, float averageDays, float day, int trust, int season)
        {
            float baseline = ClampDays(averageDays);
            return RHAH_IncidentPace.Resolve(
                formula,
                baseline,
                new RHAH_IncidentPaceContext(day, baseline, trust, season));
        }


        internal const int UnspecifiedTargetId = 0;
        internal const int MapZeroTargetId = int.MinValue;

        internal static int RawId(IIncidentTarget target)
        {
            if (target is RimWorld.Planet.Caravan caravan)
            {
                return caravan.ID;
            }

            if (target is Map map)
            {
                return map.uniqueID;
            }

            return UnspecifiedTargetId;
        }

        static float PointsFor(string displayId)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            float catalog = entry == null ? RHAH_IncidentTuning.MinDebugPoints : entry.DebugPoints;
            return RHAH_Mod.Settings == null
                ? catalog
                : RHAH_Mod.Settings.IncidentDebugPoints(displayId, catalog);
        }

        internal static int TargetId(bool caravan, int rawId)
        {
            if (!caravan && rawId == 0)
            {
                return MapZeroTargetId;
            }

            if (rawId <= 0)
            {
                return UnspecifiedTargetId;
            }

            return caravan ? -rawId : rawId;
        }

        internal static int DecodeTargetId(int savedId, RHAH_IncidentTarget kind)
        {
            if (savedId == UnspecifiedTargetId)
            {
                return UnspecifiedTargetId;
            }

            if (savedId == MapZeroTargetId)
            {
                return kind == RHAH_IncidentTarget.Map ? 0 : UnspecifiedTargetId;
            }

            if (kind == RHAH_IncidentTarget.Caravan)
            {
                return savedId < 0 ? -savedId : UnspecifiedTargetId;
            }

            return savedId > 0 ? savedId : UnspecifiedTargetId;
        }

        internal static int NextExecutable(bool[] terminal, bool[] ready)
        {
            if (terminal == null || ready == null)
            {
                return -1;
            }

            int count = terminal.Length < ready.Length ? terminal.Length : ready.Length;
            for (int i = 0; i < count; i++)
            {
                if (terminal[i] || ready[i])
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(RimWorld.Storyteller), nameof(RimWorld.Storyteller.StorytellerTick))]
    internal static class RHAH_IncidentSchedulePatch
    {
        internal static void Postfix()
        {
            if (Find.TickManager == null || Find.TickManager.TicksGame % RimWorld.Storyteller.CheckInterval != 0)
            {
                return;
            }

            int trust = Current.Game?.GetComponent<Narrative.NarrativeState>()?.Snapshot().Trust ?? 0;
            RHAH_IncidentSeason season = RHAH_IncidentSchedule.SeasonOf(Find.AnyPlayerHomeMap);
            RHAH_IncidentSchedule.QueueDueIncidents(
                GenDate.DaysPassedSinceSettleFloat,
                trust,
                season,
                RimWorld.Storyteller.CheckInterval);
        }
    }
}
