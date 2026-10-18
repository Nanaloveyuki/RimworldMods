using UnityEngine;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_FertilityRules
    {
        internal const string LargeLitter = "RHAH_Gene_LargeLitter";
        internal const string EarlyFertility = "RHAH_Gene_EarlyFertility";
        internal const string HighFertility = "RHAH_Gene_HighFertility";
        internal const string RoomFertility = "RHAH_Gene_RoomFertility";
        internal const string FastBirth = "RHAH_Gene_FastBirth";

        internal const int MinLitter = 1;
        internal const int MaxLitter = 12;
        internal const int DefaultLitterMin = 2;
        internal const int DefaultLitterPeak = 4;
        internal const int DefaultLitterMax = 6;

        internal const float MinFertileAge = 1f;
        internal const float MaxFertileAge = 14f;
        internal const float DefaultFertileAge = 1f;

        internal const float MinFertilityPercent = 100f;
        internal const float MaxFertilityPercent = 1000f;
        internal const float DefaultFertilityPercent = 300f;

        internal const float MinGestationDays = 3f;
        internal const float VanillaGestationFloorDays = 5.661f;
        internal const float DefaultGestationDays = 5.661f;

        internal const float MinLovinPregnancyPercent = 0f;
        internal const float MaxLovinPregnancyPercent = 100f;
        internal const float DefaultLovinPregnancyPercent = 50f;

        internal static int ClampLitter(int value)
        {
            return Mathf.Clamp(value, MinLitter, MaxLitter);
        }

        internal static void ClampLitter(ref int minimum, ref int peak, ref int maximum)
        {
            minimum = ClampLitter(minimum);
            maximum = ClampLitter(maximum);
            if (maximum < minimum)
            {
                maximum = minimum;
            }

            peak = Mathf.Clamp(peak, minimum, maximum);
        }

        internal static float LitterChance(int count, int minimum, int peak, int maximum)
        {
            ClampLitter(ref minimum, ref peak, ref maximum);
            if (count < minimum || count > maximum)
            {
                return 0f;
            }

            if (minimum == maximum || count == peak)
            {
                return 1f;
            }

            if (count < peak)
            {
                return (count - minimum + 1f) / (peak - minimum + 1f);
            }

            return (maximum - count + 1f) / (maximum - peak + 1f);
        }

        internal static int RollLitter(int minimum, int peak, int maximum, System.Func<float> chance)
        {
            ClampLitter(ref minimum, ref peak, ref maximum);
            if (chance == null || minimum == maximum)
            {
                return minimum;
            }

            float total = 0f;
            for (int count = minimum; count <= maximum; count++)
            {
                total += LitterChance(count, minimum, peak, maximum);
            }

            if (total <= 0f)
            {
                return peak;
            }

            float roll = Mathf.Clamp01(chance()) * total;
            float walked = 0f;
            for (int count = minimum; count <= maximum; count++)
            {
                walked += LitterChance(count, minimum, peak, maximum);
                if (roll <= walked)
                {
                    return count;
                }
            }

            return maximum;
        }

        internal static float ClampFertileAge(float years)
        {
            if (float.IsNaN(years) || float.IsInfinity(years))
            {
                return DefaultFertileAge;
            }

            return Mathf.Clamp(years, MinFertileAge, MaxFertileAge);
        }

        internal static bool AgeAllows(float years, float minimumYears)
        {
            return years >= ClampFertileAge(minimumYears);
        }

        internal static float ClampFertilityPercent(float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent))
            {
                return DefaultFertilityPercent;
            }

            return Mathf.Clamp(percent, MinFertilityPercent, MaxFertilityPercent);
        }

        internal static float FertilityFactor(float percent)
        {
            return ClampFertilityPercent(percent) / 100f;
        }

        internal static float ClampGestationDays(float days)
        {
            if (float.IsNaN(days) || float.IsInfinity(days))
            {
                return DefaultGestationDays;
            }

            return Mathf.Clamp(days, MinGestationDays, VanillaGestationFloorDays);
        }

        internal static float GestationDays(float requestedDays, float raceDays)
        {
            float requested = ClampGestationDays(requestedDays);
            if (raceDays <= 0f || float.IsNaN(raceDays) || float.IsInfinity(raceDays))
            {
                return requested;
            }

            return Mathf.Min(requested, raceDays);
        }

        internal static float ClampLovinPregnancyPercent(float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent))
            {
                return DefaultLovinPregnancyPercent;
            }

            return Mathf.Clamp(percent, MinLovinPregnancyPercent, MaxLovinPregnancyPercent);
        }

        internal static float LovinPregnancyChance(float percent)
        {
            return ClampLovinPregnancyPercent(percent) / 100f;
        }

        // 不看婚恋 只看同一室内
        internal static bool RoomPartner(bool sameMap, bool sameRoom, bool outdoors, bool oppositeGender, bool dead, bool self)
        {
            return sameMap && sameRoom && !outdoors && oppositeGender && !dead && !self;
        }

        internal const int MinRoomLovinCooldownTicks = 1250;

        // 人必须已经躺在非医疗床上 冷却没过就不再发
        internal static bool RoomLovinReady(int now, int canLovinTick, bool drafted, bool awake, bool alreadyLovin)
        {
            return now >= canLovinTick && !drafted && awake && !alreadyLovin;
        }

        internal static bool RoomLovinBed(bool hasBed, bool medical, bool partnerInBed, int partnerCanLovinTick, int now)
        {
            return hasBed && !medical && partnerInBed && now >= partnerCanLovinTick;
        }
    }
}
