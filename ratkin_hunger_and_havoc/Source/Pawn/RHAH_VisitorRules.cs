using System.Collections.Generic;

using System;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;
using Verse.AI;
namespace HungerAndHavoc.Pawn
{
    internal enum RHAH_StayKind
    {
        None = 0,
        Shelter = 1,
        Hire = 2,
        Recruit = 3
    }

    // 人数、年龄、停留和雇佣期限 不碰存档
    internal static class RHAH_VisitorRules
    {
        internal const int MinEventPawns = 1;
        internal const int DefaultEventPawns = 30;
        internal const int MaxEventPawns = 100;
        internal const float MinGeneratedAge = 0f;
        internal const float DefaultMinGeneratedAge = 0f;
        internal const float DefaultMaxGeneratedAge = 50f;
        internal const float MaxGeneratedAge = 100f;
        internal const float MinMotherAge = 1f;
        internal const float DefaultMotherMinAge = 14f;
        internal const float MaxMotherAge = 14f;
        internal const int MinFedWanderHours = 1;
        internal const int DefaultFedWanderHours = 12;
        internal const int MaxFedWanderHours = 48;
        internal const float DefaultNoFoodWaitDays = 0.5f;
        internal const float MaxStayDays = 5f;
        internal const float MinStayFraction = 0.5f;
        internal const float MaxStayFraction = 1.5f;
        internal const int MinShelterDays = 5;
        internal const int DefaultShelterDays = 5;
        internal const int MaxShelterDays = 60 * 4;
        internal const int MinHireDays = 5;
        internal const int DefaultHireDays = 60 * 4;
        internal const int MaxHireDays = 60 * 4 * 10;
        internal const int TicksPerDay = 60000;
        internal const int TicksPerHour = 2500;
        internal const int MinBegFailCooldownHours = 3;
        internal const int DefaultBegFailCooldownHours = 3;
        internal const int MaxBegFailCooldownHours = 12;
        internal const float ToddlerBegAge = 1f + 47f / 60f;
        internal const int BegFailMood = -5;
        internal const int BegSuccessMood = 3;
        internal const int BegSuccessStack = 1;
        internal const int UnlimitedThoughtStack = -1;
        internal const int MinBegSuccessChancePercent = 0;
        internal const int DefaultBegSuccessChancePercent = 35;
        internal const int MaxBegSuccessChancePercent = 100;
        internal const int MinBegSocialBonusPercent = 0;
        internal const int DefaultBegSocialBonusPercent = 3;
        internal const int MaxBegSocialBonusPercent = 20;
        internal const int MinBegSlapChancePercent = 0;
        internal const int DefaultBegSlapChancePercent = 50;
        internal const int MaxBegSlapChancePercent = 100;
        internal const int BegSlapKnockoutHours = 0;
        internal const int BegSlapKnockoutTicks = TicksPerHour / 2;
        internal const int LegacyBegSlapKnockoutHours = 3;
        internal const int BegSlapMood = -10;
        internal const float MinorBruiseSeverity = 4f;
        internal const float BruiseSeverityStep = 4f;
        internal const float MaxBruiseSeverity = 16f;
        internal const float ModerateBleedSeverity = 8f;
        internal const float DefaultReliefScoreBonus = 0.1f;
        internal const float DefaultMinimumTemperature = -35f;
        internal const float DefaultMaximumTemperature = 70f;
        internal const float MinimumTemperatureBound = -35f;
        internal const float MaximumTemperatureBound = 70f;
        internal const float MinTemperatureInsulation = 0f;
        internal const float MaxTemperatureInsulation = 100f;

        internal static readonly string[] ColdApparel =
        {
            "RHAH_Cold_ThinHemp",
            "RHAH_Cold_LayeredHemp",
            "RHAH_Cold_StrawQuilt",
            "RHAH_Cold_FurCloak",
            "RHAH_Cold_SmokedBlanket",
            "RHAH_Cold_HideWrap"
        };

        internal static readonly string[] HeatApparel =
        {
            "RHAH_Heat_WetCloth",
            "RHAH_Heat_MudCoat",
            "RHAH_Heat_ReedWrap",
            "RHAH_Heat_BarkWrap",
            "RHAH_Heat_MudMantle",
            "RHAH_Heat_MudShell"
        };

        internal static readonly float[] ColdInsulation = { 8f, 12f, 20f, 28f, 40f, 56f };
        internal static readonly float[] HeatInsulation = { 8f, 12f, 20f, 28f, 36f, 44f };

        internal const int DefaultMaxOwnedTraits = 1;
        internal const int MaxOwnedTraits = 3;
        internal const int DefaultFemaleSharePercent = 50;

        internal static int LimitCount(int requested, int minimum, int cap, bool keepTogether)
        {
            int safeMinimum = minimum < 1 ? 1 : minimum;
            int safeRequested = requested < safeMinimum ? safeMinimum : requested;
            if (keepTogether)
            {
                return safeRequested;
            }

            int safeCap = ClampCount(cap);
            if (safeCap < safeMinimum)
            {
                return safeRequested;
            }

            return safeRequested < safeCap ? safeRequested : safeCap;
        }

        internal static int ClampCount(int cap)
        {
            if (cap < MinEventPawns)
            {
                return MinEventPawns;
            }

            return cap > MaxEventPawns ? MaxEventPawns : cap;
        }

        internal static bool KeepsAge(RHAH_PawnRole role, bool fixedAge, bool youngFollowsRange)
        {
            return fixedAge;
        }

        internal static bool IsYoungRole(RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.RatkinYoung ||
                   role == RHAH_PawnRole.BeggarChild ||
                   role == RHAH_PawnRole.ThiefChild ||
                   role == RHAH_PawnRole.WildChild;
        }

        internal static float? YoungAge(RHAH_PawnRole role, float roll)
        {
            float safeRoll = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
            switch (role)
            {
                case RHAH_PawnRole.BeggarChild:
                    return RatEggMinAge + (RatEggMaxAge - RatEggMinAge) * safeRoll;
                case RHAH_PawnRole.RatkinYoung:
                case RHAH_PawnRole.ThiefChild:
                case RHAH_PawnRole.WildChild:
                    return ChildMinAge + (ChildMaxAge - ChildMinAge) * safeRoll;
                default:
                    return null;
            }
        }

        internal static float AirdropAge(float roll)
        {
            float safeRoll = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
            if (safeRoll < 0.8f)
            {
                return RatEggMinAge + (RatEggMaxAge - RatEggMinAge) * (safeRoll / 0.8f);
            }

            return ChildMinAge + (ChildMaxAge - ChildMinAge) * ((safeRoll - 0.8f) / 0.2f);
        }

        internal static void DropPods(List<Verse.Pawn> pawns, Map map, IntVec3 center, Faction faction)
        {
            if (pawns == null || pawns.Count == 0 || map == null || !center.IsValid)
            {
                return;
            }

            List<Verse.Thing> payload = new List<Verse.Thing>(pawns.Count);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] != null && !pawns[i].Spawned && !pawns[i].Destroyed)
                {
                    payload.Add(pawns[i]);
                }
            }

            if (payload.Count == 0)
            {
                return;
            }

            DropPodUtility.DropThingsNear(center, map, payload, 110, false, false, true, false, true, faction);
        }

        internal static float? GenerationAge(
            RHAH_PawnRole role,
            float? fixedAge,
            float minAge,
            float maxAge,
            float roll,
            bool youngFollowsRange,
            float motherMinAge = DefaultMotherMinAge)
        {
            if (fixedAge.HasValue)
            {
                return fixedAge;
            }

            if (IsYoungRole(role) && !youngFollowsRange)
            {
                return YoungAge(role, roll);
            }

            float lower = ClampAge(float.IsNaN(minAge) ? DefaultMinGeneratedAge : minAge);
            float upper = ClampAge(float.IsNaN(maxAge) ? DefaultMaxGeneratedAge : maxAge);
            if (IsMotherRole(role))
            {
                float mother = ClampMotherAge(motherMinAge);
                if (lower < mother)
                {
                    lower = mother;
                }

                if (upper < lower)
                {
                    upper = lower;
                }
            }
            else if (upper < lower)
            {
                float swap = lower;
                lower = upper;
                upper = swap;
            }

            float span = upper - lower;
            float safeRoll = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
            return lower + span * safeRoll;
        }

        internal static bool IsMotherRole(RHAH_PawnRole role)
        {
            return role == RHAH_PawnRole.Mother || role == RHAH_PawnRole.BeggarMother;
        }

        internal static float ClampMotherAge(float years)
        {
            if (float.IsNaN(years) || float.IsInfinity(years))
            {
                return DefaultMotherMinAge;
            }

            if (years < MinMotherAge)
            {
                return MinMotherAge;
            }

            return years > MaxMotherAge ? MaxMotherAge : years;
        }

        internal static float? WalkingAgeFloor(float? age, bool toddlersActive, bool allowImmobileBabies)
        {
            if (toddlersActive || allowImmobileBabies || !age.HasValue || float.IsNaN(age.Value) || float.IsInfinity(age.Value))
            {
                return age;
            }

            return age.Value < WalkingAge ? WalkingAge : age.Value;
        }

        internal static int ClampOwnedTraits(int count)
        {
            if (count < 0)
            {
                return 0;
            }

            return count > MaxOwnedTraits ? MaxOwnedTraits : count;
        }

        internal static bool UsesPlayerIdeo(int percent, float roll)
        {
            int share = ClampPercent(percent, 0, 100);
            float safeRoll = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
            return safeRoll * 100f < share;
        }

        internal static void ApplyPlayerIdeo(Verse.Pawn pawn, int percent, float roll)
        {
            if (pawn?.ideo == null || !ModsConfig.IdeologyActive || Faction.OfPlayer?.ideos?.PrimaryIdeo == null)
            {
                return;
            }

            if (!UsesPlayerIdeo(percent, roll))
            {
                return;
            }

            pawn.ideo.SetIdeo(Faction.OfPlayer.ideos.PrimaryIdeo);
        }

        internal static int ClampFemaleShare(int percent)
        {
            if (percent < 0)
            {
                return 0;
            }

            return percent > 100 ? 100 : percent;
        }

        internal static int ClampBodyMode(int mode, int count)
        {
            if (mode < 0 || mode >= count)
            {
                return 0;
            }

            return mode;
        }

        internal static int? ResolveGender(int? requested, int mode, int femaleSharePercent, float roll)
        {
            const int male = 1;
            const int female = 2;
            if (requested.HasValue)
            {
                return requested.Value == female ? female : requested.Value == male ? male : male;
            }

            int safeMode = ClampBodyMode(mode, 4);
            if (safeMode == 2)
            {
                return female;
            }

            if (safeMode == 3)
            {
                return male;
            }

            if (safeMode != 1)
            {
                return null;
            }

            float safeRoll = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
            return safeRoll * 100f < ClampFemaleShare(femaleSharePercent) ? female : male;
        }

        internal static bool StripsRaceApparel(int apparelMode, bool explicitApparel)
        {
            return !explicitApparel && ClampBodyMode(apparelMode, 4) == 0;
        }

        internal static bool ClearsApparel(int apparelMode, bool explicitApparel)
        {
            return !explicitApparel && ClampBodyMode(apparelMode, 4) == 3;
        }

        internal static bool AddsColdClothes(int apparelMode, bool coldEnabled, bool explicitApparel, float temperature, float comfortableMinimum)
        {
            int mode = ClampBodyMode(apparelMode, 4);
            if (explicitApparel || mode == 3)
            {
                return false;
            }

            if (mode != 2 && !coldEnabled)
            {
                return false;
            }

            return WantsColdClothes(true, temperature, comfortableMinimum);
        }
        internal static bool IsTemperatureApparel(string defName)
        {
            return IndexOf(ColdApparel, defName) >= 0 || IndexOf(HeatApparel, defName) >= 0;
        }

        internal static float DefaultInsulation(string defName)
        {
            int cold = IndexOf(ColdApparel, defName);
            if (cold >= 0)
            {
                return ColdInsulation[cold];
            }

            int heat = IndexOf(HeatApparel, defName);
            return heat >= 0 ? HeatInsulation[heat] : 0f;
        }

        internal static float ClampInsulation(float value, float fallback)
        {
            float safe = float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
            if (safe < MinTemperatureInsulation)
            {
                return MinTemperatureInsulation;
            }

            return safe > MaxTemperatureInsulation ? MaxTemperatureInsulation : safe;
        }

        internal static int TemperatureDirection(float temperature, float comfortableMinimum, float comfortableMaximum)
        {
            if (float.IsNaN(temperature) || float.IsInfinity(temperature))
            {
                return 0;
            }

            if (temperature < comfortableMinimum)
            {
                return -1;
            }

            return temperature > comfortableMaximum ? 1 : 0;
        }

        internal static float RequiredInsulation(int direction, float temperature, float comfortableMinimum, float comfortableMaximum, float clampMinimum, float clampMaximum)
        {
            if (direction == 0 || float.IsNaN(temperature) || float.IsInfinity(temperature))
            {
                return 0f;
            }

            float low = float.IsNaN(clampMinimum) || float.IsInfinity(clampMinimum) ? DefaultMinimumTemperature : clampMinimum;
            float high = float.IsNaN(clampMaximum) || float.IsInfinity(clampMaximum) ? DefaultMaximumTemperature : clampMaximum;
            if (high < low)
            {
                high = low;
            }

            float target = temperature < low ? low : temperature > high ? high : temperature;
            float required = direction < 0 ? comfortableMinimum - target : target - comfortableMaximum;
            return required < 0f ? 0f : required;
        }

        internal static string SelectTemperatureApparel(int direction, float required, Func<string, bool> enabled, Func<string, float> insulation)
        {
            if (direction == 0 || enabled == null || insulation == null)
            {
                return null;
            }

            string[] names = direction < 0 ? ColdApparel : HeatApparel;
            string fallback = null;
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (!enabled(name))
                {
                    continue;
                }

                fallback = name;
                if (ClampInsulation(insulation(name), DefaultInsulation(name)) >= required - 0.001f)
                {
                    return name;
                }
            }

            return fallback;
        }

        static int IndexOf(string[] names, string defName)
        {
            if (names == null || string.IsNullOrEmpty(defName))
            {
                return -1;
            }

            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], defName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        internal const int HostileGoodwill = -100;
        internal const int NeutralGoodwill = 0;
        internal const float WalkingAge = 4f;
        internal const float RatEggMinAge = 1f / 60f;
        internal const float RatEggMaxAge = 2.9f;
        internal const float ChildMinAge = 3f;
        internal const float ChildMaxAge = 6.9f;

        internal static int LockedGoodwill(RHAH_Attitude attitude)
        {
            return attitude == RHAH_Attitude.Hostile ? HostileGoodwill : NeutralGoodwill;
        }

        internal static void OutsideRelation(
            bool ownerHostile,
            bool ownerFriendly,
            bool otherAttitudeFaction,
            bool playerHostileToOther,
            out FactionRelationKind kind,
            out int goodwill)
        {
            if (otherAttitudeFaction || ownerFriendly || (!ownerHostile && !playerHostileToOther))
            {
                kind = FactionRelationKind.Neutral;
                goodwill = NeutralGoodwill;
                return;
            }

            kind = FactionRelationKind.Hostile;
            goodwill = HostileGoodwill;
        }
        internal static int ClampBegFailCooldownHours(int hours)
        {
            if (hours < MinBegFailCooldownHours)
            {
                return MinBegFailCooldownHours;
            }

            return hours > MaxBegFailCooldownHours ? MaxBegFailCooldownHours : hours;
        }

        internal static int BegFailCooldownTicks(int hours)
        {
            return ClampBegFailCooldownHours(hours) * TicksPerHour;
        }

        internal static bool BegCooldownReady(int now, int untilTick)
        {
            return untilTick < 0 || now >= untilTick;
        }

        internal static int NextBegTick(int now, int hours)
        {
            if (now < 0)
            {
                now = 0;
            }

            return now + BegFailCooldownTicks(hours);
        }

        internal static float BegAgeFloor(bool toddlers)
        {
            return toddlers ? ToddlerBegAge : WalkingAge;
        }


        internal static int ClampPercent(int percent, int min, int max)
        {
            if (percent < min)
            {
                return min;
            }

            return percent > max ? max : percent;
        }

        internal static float BegSuccessChance(int basePercent, int socialLevel, int socialBonusPercent)
        {
            int level = socialLevel < 0 ? 0 : socialLevel;
            float chance = ClampPercent(basePercent, MinBegSuccessChancePercent, MaxBegSuccessChancePercent) / 100f;
            chance += level * ClampPercent(socialBonusPercent, MinBegSocialBonusPercent, MaxBegSocialBonusPercent) / 100f;
            if (chance < 0f)
            {
                return 0f;
            }

            return chance > 1f ? 1f : chance;
        }

        internal static bool BegFoodEligible(bool ingestible, bool nutrition, int preferability, int awful)
        {
            return ingestible && nutrition && preferability >= awful;
        }

        internal static int ClampBegSlapChance(int percent)
        {
            if (percent < MinBegSlapChancePercent)
            {
                return MinBegSlapChancePercent;
            }

            return percent > MaxBegSlapChancePercent ? MaxBegSlapChancePercent : percent;
        }

        internal static bool RollsSlap(int percent, float roll)
        {
            int safe = ClampBegSlapChance(percent);
            if (safe <= 0)
            {
                return false;
            }

            if (safe >= 100)
            {
                return true;
            }

            float safeRoll = float.IsNaN(roll) || float.IsInfinity(roll) || roll < 0f ? 1f : roll;
            return safeRoll < safe / 100f;
        }

        // 0 新增轻度瘀伤 1 加重已有瘀伤 2 瘀伤已满改中度流血
        internal static int SlapWoundKind(bool hasBruise, float bruiseSeverity)
        {
            if (!hasBruise)
            {
                return 0;
            }

            if (float.IsNaN(bruiseSeverity) || float.IsInfinity(bruiseSeverity))
            {
                return 0;
            }

            float cap = Core.RHAH_Mod.Settings == null ? MaxBruiseSeverity : Core.RHAH_Mod.Settings.begBruiseMax;
            return bruiseSeverity >= cap ? 2 : 1;
        }

        internal static float NextBruiseSeverity(float current)
        {
            float start = Core.RHAH_Mod.Settings == null ? MinorBruiseSeverity : Core.RHAH_Mod.Settings.begBruiseSeverity;
            float step = Core.RHAH_Mod.Settings == null ? BruiseSeverityStep : Core.RHAH_Mod.Settings.begBruiseStep;
            float cap = Core.RHAH_Mod.Settings == null ? MaxBruiseSeverity : Core.RHAH_Mod.Settings.begBruiseMax;
            if (float.IsNaN(current) || float.IsInfinity(current) || current < start)
            {
                return start;
            }

            float next = current + step;
            return next > cap ? cap : next;
        }
        // 睡着、医疗床、倒地和还不能行动的幼童不接受乞讨
        internal static bool CanReceiveBeg(
            bool dead,
            bool downed,
            bool forbidden,
            bool sleeping,
            bool medicalBed,
            bool reachable,
            bool reservable,
            float age,
            bool toddlers)
        {
            if (dead || downed || forbidden || sleeping || medicalBed || !reachable || !reservable)
            {
                return false;
            }

            if (float.IsNaN(age) || float.IsInfinity(age))
            {
                return false;
            }

            return age >= BegAgeFloor(toddlers);
        }



        internal static float ClampAge(float age)
        {
            if (float.IsNaN(age) || float.IsInfinity(age) || age < MinGeneratedAge)
            {
                return MinGeneratedAge;
            }

            return age > MaxGeneratedAge ? MaxGeneratedAge : age;
        }

        internal static float Score(float distanceScore, float suitability, bool inRelief, float bonus)
        {
            if (float.IsNaN(distanceScore) || float.IsInfinity(distanceScore))
            {
                return float.NegativeInfinity;
            }

            float fit = float.IsNaN(suitability) || float.IsInfinity(suitability) ? 0f : suitability;
            float total = distanceScore + fit;
            if (!inRelief)
            {
                return total;
            }

            float safeBonus = ClampBonus(bonus);
            return total + Abs(total) * safeBonus;
        }

        internal static float ClampBonus(float bonus)
        {
            if (float.IsNaN(bonus) || float.IsInfinity(bonus) || bonus < 0f)
            {
                return 0f;
            }

            return bonus > 1f ? 1f : bonus;
        }

        internal static int ClampFedWanderHours(int hours)
        {
            if (hours < MinFedWanderHours)
            {
                return MinFedWanderHours;
            }

            return hours > MaxFedWanderHours ? MaxFedWanderHours : hours;
        }

        internal static int FedWanderTicks(bool wander, int hours)
        {
            if (!wander)
            {
                return 0;
            }

            return ClampFedWanderHours(hours) * TicksPerHour;
        }

        internal static int WaitTicks(bool wait, float days)
        {
            if (!wait)
            {
                return 0;
            }

            return (int)(ClampDays(days, DefaultNoFoodWaitDays) * TicksPerDay);
        }

        internal static int NextWaitTick(int now, int previousDeadline, bool foundFood, bool wait, float days)
        {
            if (!wait)
            {
                return -1;
            }

            if (foundFood || previousDeadline < 0)
            {
                return now + WaitTicks(true, days);
            }

            return previousDeadline;
        }

        internal static bool NoFoodWaitExpired(bool waiting, bool hasBeenFed, int now, int deadline)
        {
            return waiting && !hasBeenFed && deadline >= 0 && now >= deadline;
        }

        internal static bool FedLeaveDue(bool leaveEnabled, bool hasBeenFed, int now, int leaveTick, bool downed)
        {
            return leaveEnabled && hasBeenFed && !downed && leaveTick >= 0 && now >= leaveTick;
        }



        internal static int BeginStay(int now, RHAH_StayKind kind, int shelterDays, int hireDays)
        {
            int days = kind == RHAH_StayKind.Hire ? ClampHireDays(hireDays) : ClampShelterDays(shelterDays);
            return now + days * TicksPerDay;
        }

        internal static int StayYears(int days)
        {
            int safe = days < 0 ? 0 : days;
            return safe / 60;
        }

        internal static int StayRestDays(int days)
        {
            int safe = days < 0 ? 0 : days;
            return safe - StayYears(safe) * 60;
        }

        internal static string StayLabel(int days)
        {
            int years = StayYears(days);
            int rest = StayRestDays(days);
            if (years > 0 && rest > 0)
            {
                return ((string)"RHAH_Stay_YearsDays").Translate(years, rest).ToString();
            }

            if (years == 1)
            {
                return ((string)"RHAH_Stay_Year").Translate().ToString();
            }

            if (years > 1)
            {
                return ((string)"RHAH_Stay_Years").Translate(years).ToString();
            }

            if (rest == 1)
            {
                return ((string)"RHAH_Stay_Day").Translate().ToString();
            }

            return ((string)"RHAH_Stay_Days").Translate(rest).ToString();
        }

        internal static int ResumeStay(int now, int savedDeadline, int savedRemaining, bool downed)
        {
            if (downed)
            {
                return savedDeadline;
            }

            if (savedRemaining > 0)
            {
                return now + savedRemaining;
            }

            return savedDeadline;
        }

        internal static bool StayExpired(int now, int deadline, bool downed)
        {
            return !downed && deadline >= 0 && now >= deadline;
        }

        internal static bool PausesStay(bool downed, bool ageDowned, bool forcedDowned, bool painShock, bool canBeAwake)
        {
            return downed && (!ageDowned || forcedDowned || painShock || !canBeAwake);
        }

        internal static bool RestoresStuckDeparture(bool playerFaction, RHAH_Lifecycle lifecycle, int stayKind, bool prisoner, bool slave)
        {
            return playerFaction && lifecycle == RHAH_Lifecycle.Leaving &&
                !IsColonyStay(stayKind) && !prisoner && !slave;
        }

        internal static int RemainingStay(int now, int deadline, bool downed, int savedRemaining)
        {
            if (downed)
            {
                return savedRemaining > 0 ? savedRemaining : deadline > now ? deadline - now : 0;
            }

            if (deadline < 0)
            {
                return 0;
            }

            return deadline > now ? deadline - now : 0;
        }

        // 选中时读已有期限 倒地用保存的剩余时间 旧档缺剩余时间时从期限回算
        internal static string StayCountdown(int stayKind, int now, int deadline, bool downed, int savedRemaining)
        {
            if (!IsColonyStay(stayKind) || deadline < 0)
            {
                return null;
            }

            int remaining = RemainingStay(now, deadline, downed, savedRemaining);
            if (remaining <= 0)
            {
                return null;
            }

            string period = remaining.ToStringTicksToPeriod(true, false, true, true, false);
            return downed
                ? ((string)"RHAH_Stay_InspectPaused").Translate(period).ToString()
                : ((string)"RHAH_Stay_Inspect").Translate(period).ToString();
        }

        // 招募、短工、长工期间不发本模组行为
        internal static bool IsColonyStay(int stayKind)
        {
            return stayKind == (int)RHAH_StayKind.Shelter ||
                   stayKind == (int)RHAH_StayKind.Hire ||
                   stayKind == (int)RHAH_StayKind.Recruit;
        }

        // 期限结束且不再倒地 只允许离场和被动反击
        internal static bool BlocksAssignedWork(int stayKind, int now, int deadline, bool downed)
        {
            return IsColonyStay(stayKind) && StayExpired(now, deadline, downed);
        }

        // 未到期的短工长工招募必须留在玩家派系 囚犯和奴隶除外
        internal static bool RestoresPlayerFaction(int stayKind, int now, int deadline, bool downed, bool dead, bool prisoner, bool slave, bool playerFaction)
        {
            return IsColonyStay(stayKind) && !dead && !prisoner && !slave && !playerFaction && !StayExpired(now, deadline, downed);
        }

        internal static bool AllowsModBehavior(int stayKind)
        {
            return !IsColonyStay(stayKind);
        }

        internal static bool IsPassiveDefense(JobDef def)
        {
            return def == JobDefOf.AttackMelee || def == JobDefOf.AttackStatic || def == JobDefOf.Flee;
        }

        internal static bool IsNeedFood(ThinkNode giver)
        {
            return giver is JobGiver_GetFood;
        }

        internal static bool AllowsGnaw(bool gate, bool visitor, bool modBehavior, bool needFood, float foodLevel, float starvation, bool busy, bool downed)
        {
            return gate && visitor && modBehavior && needFood &&
                   foodLevel < starvation && !busy && !downed;
        }

        internal static bool ClearsTrade(RHAH_ReleaseReason reason, bool letterBatch)
        {
            return letterBatch ||
                reason == RHAH_ReleaseReason.Recruited ||
                reason == RHAH_ReleaseReason.JoinedPlayerFaction ||
                reason == RHAH_ReleaseReason.Imprisoned ||
                reason == RHAH_ReleaseReason.Enslaved;
        }

        internal static bool LiftsAgeImmobility(bool toddlersActive, bool eventBaby, bool ageDowned, bool injuredDowned)
        {
            return !toddlersActive && eventBaby && ageDowned && !injuredDowned;
        }

        internal static bool WithinFoodRange(float manhattan, float limit)
        {
            return !float.IsNaN(manhattan) && !float.IsInfinity(manhattan) && manhattan <= limit;
        }

        internal static bool BlocksReliefFeed(bool downed, bool toddlersActive, int manipulationStage)
        {
            return downed && !(toddlersActive && manipulationStage >= 1);
        }

        // 倒地或还不能走的来客不接受别人塞来的离图 Goto
        internal static bool RejectsSelfExit(bool visitor, bool exitOnArrival, bool canWalkOut)
        {
            return visitor && exitOnArrival && !canWalkOut;
        }

        internal static bool AcceptsStone(bool stoneChunk, bool slag)
        {
            return stoneChunk && !slag;
        }

        internal static bool TemperatureAllows(float temperature, float minimum, float maximum, bool caravan)
        {
            if (caravan || float.IsNaN(temperature) || float.IsInfinity(temperature))
            {
                return caravan || float.IsNaN(temperature) || float.IsInfinity(temperature);
            }

            float lower = ClampTemperature(minimum, DefaultMinimumTemperature);
            float upper = ClampTemperature(maximum, DefaultMaximumTemperature);
            if (upper < lower)
            {
                float swap = lower;
                lower = upper;
                upper = swap;
            }

            return temperature >= lower && temperature <= upper;
        }

        internal static bool WantsColdClothes(bool enabled, float temperature, float comfortableMinimum)
        {
            return enabled && !float.IsNaN(temperature) && temperature < comfortableMinimum;
        }

        internal static int ClampShelterDays(int days)
        {
            if (days < MinShelterDays)
            {
                return MinShelterDays;
            }

            return days > MaxShelterDays ? MaxShelterDays : days;
        }

        internal static int ClampHireDays(int days)
        {
            if (days < MinHireDays)
            {
                return MinHireDays;
            }

            return days > MaxHireDays ? MaxHireDays : days;
        }

        static float ClampDays(float days, float fallback)
        {
            if (float.IsNaN(days) || float.IsInfinity(days))
            {
                return fallback;
            }

            if (days < 0f)
            {
                return 0f;
            }

            return days > MaxStayDays ? MaxStayDays : days;
        }

        static float ClampTemperature(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            if (value < MinimumTemperatureBound)
            {
                return MinimumTemperatureBound;
            }

            return value > MaximumTemperatureBound ? MaximumTemperatureBound : value;
        }

        static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }
    }
}
