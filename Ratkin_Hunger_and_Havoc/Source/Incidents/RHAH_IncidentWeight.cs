using Verse;
using System;

namespace HungerAndHavoc.Incidents
{
    internal enum RHAH_IncidentSeason
    {
        Spring = 0,
        Summer = 1,
        Fall = 2,
        Winter = 3,
        Undefined = 4
    }

    internal readonly struct RHAH_IncidentWeightInput
    {
        public RHAH_IncidentFamily Family { get; }
        public RHAH_IncidentCategory Category { get; }
        public RHAH_IncidentTarget Target { get; }
        public RHAH_AttitudePool Pool { get; }
        public RHAH_IncidentSeason Season { get; }
        public int Trust { get; }
        public bool MapHome { get; }
        public bool PlayerCaravan { get; }

        public RHAH_IncidentWeightInput(
            RHAH_IncidentFamily family,
            RHAH_IncidentCategory category,
            RHAH_IncidentTarget target,
            RHAH_AttitudePool pool,
            RHAH_IncidentSeason season,
            int trust,
            bool mapHome,
            bool playerCaravan)
        {
            Family = family;
            Category = category;
            Target = target;
            Pool = pool;
            Season = season;
            Trust = trust;
            MapHome = mapHome;
            PlayerCaravan = playerCaravan;
        }
    }

    internal static class RHAH_IncidentTuning
    {
        internal const float MinDebugPoints = 1f;
        internal const float MaxDebugPoints = 10000f;
        internal const float DefaultWeight = 100f;
        internal const float MinWeight = 0f;
        internal const float MaxWeight = 100f;

        internal static float ClampPoints(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < MinDebugPoints)
            {
                return MinDebugPoints;
            }

            return value > MaxDebugPoints ? MaxDebugPoints : value;
        }

        internal static float ClampWeight(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < MinWeight)
            {
                return MinWeight;
            }

            return value > MaxWeight ? MaxWeight : value;
        }

        internal static float Scale(float formulaWeight, bool enabled, float playerWeight)
        {
            if (!enabled || formulaWeight <= 0f)
            {
                return 0f;
            }

            return formulaWeight * (ClampWeight(playerWeight) / DefaultWeight);
        }
    }

    internal static class RHAH_IncidentScale
    {
        internal const float ReferencePoints = 300f;

        internal static int Count(string displayId, float points)
        {
            int raw = RawCount(displayId, points);
            int minimum = Minimum(displayId);
            return FamineCount(displayId, raw < minimum ? minimum : raw);
        }

        internal static int Count(string displayId, float points, int cap, bool keepTogether)
        {
            int raw = RawCount(displayId, points);
            int safeMinimum = Minimum(displayId);
            int requested = raw < safeMinimum ? safeMinimum : raw;
            if (!keepTogether && !FixedGroup(displayId) && displayId != "I-052" && cap >= safeMinimum)
            {
                requested = requested < cap ? requested : cap;
            }

            return FamineCount(displayId, requested);
        }

        static int FamineCount(string displayId, int count)
        {
            return MultiplyCount(displayId, count, Core.GameComponent_RHAH_FamineYear.Active,
                Core.RHAH_Mod.Settings?.famineYearPawnMultiplier ?? 3f);
        }

        internal static int MultiplyCount(string displayId, int count, bool famine, float multiplier)
        {
            return !famine || count <= 1 || FixedGroup(displayId) || displayId == "I-052"
                ? count
                : (int)Math.Ceiling(count * Core.GameComponent_RHAH_FamineYear.ClampMultiplier(multiplier));
        }

        static int RawCount(string displayId, float points)
        {
            float safe = RHAH_IncidentTuning.ClampPoints(points);
            switch (displayId)
            {
                case "I-001": return Fixed(safe, 10, 50, 50f);
                case "I-002": return Linear(safe, 120f, 1, 10);
                case "I-003": return Linear(safe, 150f, 1, 10) + 1;
                case "I-004": return 3;
                case "I-005":
                case "I-042": return Spread(safe, 3, 20, 85f);
                case "I-006":
                case "I-054": return Spread(safe, 3, 20, 75f);
                case "I-007": return Spread(safe, 3, 20, 75f);
                case "I-008":
                case "I-009":
                case "I-008-01":
                case "I-009-01":
                case "I-013":
                case "I-019":
                case "I-033":
                case "I-037":
                case "I-041":
                case "I-047":
                case "I-049": return 1;
                case "I-010":
                case "I-010-01": return Fixed(safe, 2, 10, 120f);
                case "I-011":
                case "I-040": return Linear(safe, 200f, 1, 4);
                case "I-012":
                case "I-038": return 3;
                case "I-014": return 5;
                case "I-015":
                case "I-016":
                case "I-017":
                case "I-018":
                case "I-020":
                case "I-021":
                case "I-022":
                case "I-023":
                case "I-024":
                case "I-025":
                case "I-026":
                case "I-027":
                case "I-028": return 1;
                case "I-029":
                case "I-044": return 2;
                case "I-030":
                case "I-045": return Spread(safe, 4, 16, 80f);
                case "I-031":
                case "I-039": return Spread(safe, 4, 12, 85f);
                case "I-032":
                case "I-046": return Spread(safe, 4, 14, 80f);
                case "I-034":
                case "I-048": return Spread(safe, 8, 28, 55f);
                case "I-035":
                case "I-050": return Spread(safe, 3, 10, 85f);
                case "I-036": return Spread(safe, 2, 8, 90f);
                case "I-043": return Spread(safe, 3, 18, 90f);
                case "I-052": return Rand.RangeInclusive(15, 50);
                case "I-053": return 1;
                default: return 1;
            }
        }

        internal static int ScaleAmount(int amount, float points, int min, int max)
        {
            if (amount <= 0)
            {
                return 0;
            }

            float safe = RHAH_IncidentTuning.ClampPoints(points);
            float reference = Core.RHAH_Mod.Settings == null || Core.RHAH_Mod.Settings.requestPointScale <= 0f
                ? ReferencePoints
                : Core.RHAH_Mod.Settings.requestPointScale;
            int scaled = Round(amount * (safe / reference));
            if (scaled < min)
            {
                return min;
            }

            return scaled > max ? max : scaled;
        }

        static int Linear(float points, float perStep, int min, int max)
        {
            return Clamp(Round(points / perStep) + 1, min, max);
        }

        static int Fixed(float points, int min, int max, float perStep)
        {
            return Clamp(Round(points / perStep) + min, min, max);
        }

        static int Spread(float points, int min, int max, float perStep)
        {
            int ceiling = Fixed(points, min, max, perStep);
            if (ceiling <= min)
            {
                return min;
            }

            return min + Rand.Range(0, ceiling - min + 1);
        }

        static int Round(float value)
        {
            return (int)(value >= 0f ? value + 0.5f : value - 0.5f);
        }

        static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        internal const int DefaultEventPawns = 30;

        internal static bool FixedGroup(string displayId)
        {
            return displayId == "I-003" || displayId == "I-004" || displayId == "I-029" || displayId == "I-044";
        }

        internal static int Minimum(string displayId)
        {
            switch (displayId)
            {
                case "I-001": return 10;
                case "I-052": return 15;
                case "I-054": return 3;
                case "I-004":
                case "I-005":
                case "I-006":
                case "I-012":
                case "I-038":
                case "I-042": return 3;
                case "I-003":
                case "I-007":
                case "I-010":
                case "I-010-01":
                case "I-014":
                case "I-029":
                case "I-035":
                case "I-036":
                case "I-043":
                case "I-044":
                case "I-050": return 2;
                case "I-030":
                case "I-031":
                case "I-032":
                case "I-045":
                case "I-046":
                case "I-039": return 4;
                case "I-034":
                case "I-048": return 8;
                default: return 1;
            }
        }
    }

    internal readonly struct RHAH_WeightFactors
    {
        internal RHAH_WeightFactors(
            float wild,
            float beggar,
            float thief,
            float trade,
            float siege,
            float aid,
            float special,
            float intel,
            float season,
            float plague)
        {
            Wild = wild;
            Beggar = beggar;
            Thief = thief;
            Trade = trade;
            Siege = siege;
            Aid = aid;
            Special = special;
            Intel = intel;
            Season = season;
            Plague = plague;
        }

        internal float Wild { get; }
        internal float Beggar { get; }
        internal float Thief { get; }
        internal float Trade { get; }
        internal float Siege { get; }
        internal float Aid { get; }
        internal float Special { get; }
        internal float Intel { get; }
        internal float Season { get; }
        internal float Plague { get; }

        internal static RHAH_WeightFactors Defaults()
        {
            return new RHAH_WeightFactors(1.4f, 1f, 0.7f, 0.5f, 0.35f, 0.25f, 0.2f, 0.12f, 1.1f, 0.5f);
        }

        internal float Family(RHAH_IncidentFamily family)
        {
            switch (family)
            {
                case RHAH_IncidentFamily.Wild: return Wild;
                case RHAH_IncidentFamily.Beggar: return Beggar;
                case RHAH_IncidentFamily.Thief: return Thief;
                case RHAH_IncidentFamily.Trade: return Trade;
                case RHAH_IncidentFamily.Siege: return Siege;
                case RHAH_IncidentFamily.Aid: return Aid;
                case RHAH_IncidentFamily.Special: return Special;
                case RHAH_IncidentFamily.Intel: return Intel;
                default: return 0f;
            }
        }
    }

    internal static class RHAH_IncidentWeight
    {
        internal const float PlagueFactor = 0.5f;
        internal const float SeasonFactor = 1.1f;
        internal const int TrustLimit = 100;

        internal static float FamilyBase(RHAH_IncidentFamily family)
        {
            switch (family)
            {
                case RHAH_IncidentFamily.Wild: return 1.4f;
                case RHAH_IncidentFamily.Beggar: return 1f;
                case RHAH_IncidentFamily.Thief: return 0.7f;
                case RHAH_IncidentFamily.Trade: return 0.5f;
                case RHAH_IncidentFamily.Siege: return 0.35f;
                case RHAH_IncidentFamily.Aid: return 0.25f;
                case RHAH_IncidentFamily.Special: return 0.2f;
                case RHAH_IncidentFamily.Intel: return 0.12f;
                default: return 0f;
            }
        }

        internal static float Evaluate(RHAH_IncidentWeightInput input, RHAH_WeightFactors factors)
        {
            if (!TargetMatches(input.Target, input.MapHome, input.PlayerCaravan))
            {
                return 0f;
            }

            float weight = factors.Family(input.Family);
            if (weight <= 0f)
            {
                return 0f;
            }

            if (input.Season == RHAH_IncidentSeason.Spring || input.Season == RHAH_IncidentSeason.Winter)
            {
                weight *= factors.Season;
            }

            if (input.Category == RHAH_IncidentCategory.Plague)
            {
                weight *= factors.Plague;
            }

            if (input.Pool == RHAH_AttitudePool.Negative)
            {
                weight *= TrustFactor(input.Trust);
            }

            return weight;
        }

        internal static float Evaluate(RHAH_IncidentWeightInput input)
        {
            return Evaluate(input, RHAH_WeightFactors.Defaults());
        }

        internal static float TrustFactor(int trust)
        {
            int clamped = trust;
            if (clamped < -TrustLimit)
            {
                clamped = -TrustLimit;
            }
            else if (clamped > TrustLimit)
            {
                clamped = TrustLimit;
            }

            return 1f - clamped / 400f;
        }

        internal static bool TargetMatches(RHAH_IncidentTarget target, bool mapHome, bool playerCaravan)
        {
            if (target == RHAH_IncidentTarget.Caravan)
            {
                return playerCaravan;
            }

            return target == RHAH_IncidentTarget.Map && mapHome;
        }

        internal static int Select(float[] weights, float roll)
        {
            if (weights == null || weights.Length == 0 || roll < 0f)
            {
                return -1;
            }

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0f)
                {
                    total += weights[i];
                }
            }

            if (total <= 0f || roll >= total)
            {
                return -1;
            }

            float cursor = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f)
                {
                    continue;
                }

                cursor += weights[i];
                if (roll < cursor)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
