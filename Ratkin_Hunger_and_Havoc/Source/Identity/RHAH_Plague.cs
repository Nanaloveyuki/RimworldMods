using HungerAndHavoc.Api;
using RimWorld;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Identity
{
    internal static class RHAH_Plague
    {
        internal const float SeverityMin = 0f;
        internal const float SeverityMax = 0.1f;
        internal const float SpreadChancePerCarrier = 0.005f;
        internal const float SpreadChanceCap = 0.30f;
        internal const float BloodPumpingSkipPercent = 120f;
        internal const int SpreadHour = 6;
        internal const int SpreadDayInterval = 3;

        internal static HediffDef Def =>
            DefDatabase<HediffDef>.GetNamedSilentFail("RHAH_Plague");

        internal static bool HasActive(Verse.Pawn pawn)
        {
            HediffDef def = Def;
            return def != null &&
                   pawn?.health?.hediffSet?.HasHediff(def) == true;
        }

        // 已有同病时不新增，也不把严重度往下压
        internal static bool TryInfect(Verse.Pawn pawn, float? severity = null)
        {
            HediffDef def = Def;
            if (pawn?.health == null || def == null)
            {
                return false;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                if (severity.HasValue)
                {
                    existing.Severity = Mathf.Max(existing.Severity, severity.Value);
                }

                return false;
            }

            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            float cap = Core.RHAH_Mod.Settings == null ? SeverityMax : Core.RHAH_Mod.Settings.plagueSeverityMax;
            hediff.Severity = severity ?? Rand.Range(SeverityMin, cap);
            pawn.health.AddHediff(hediff);
            return true;
        }

        internal static void InfectCarrier(Verse.Pawn pawn, bool carriesPlague)
        {
            if (carriesPlague)
            {
                TryInfect(pawn, null);
            }
        }

        internal static bool IsSpreadDay(int absoluteDay, int lastSpreadDay, int intervalDays)
        {
            int interval = intervalDays < 1 ? 1 : intervalDays;
            return absoluteDay % interval == 0 && absoluteDay != lastSpreadDay;
        }

        internal static bool IsSpreadDay(int absoluteDay, int lastSpreadDay)
        {
            return IsSpreadDay(absoluteDay, lastSpreadDay, SpreadDayInterval);
        }

        internal static float SpreadChance(int carrierCount, float perCarrier, float cap)
        {
            if (carrierCount <= 0 || perCarrier <= 0f || cap <= 0f)
            {
                return 0f;
            }

            return Mathf.Min(perCarrier * carrierCount, cap);
        }

        internal static float SpreadChance(int carrierCount)
        {
            return SpreadChance(carrierCount, SpreadChancePerCarrier, SpreadChanceCap);
        }

        internal static bool SkipsBloodPumping(float bloodPumpingPercent, float skipPercent)
        {
            return bloodPumpingPercent >= skipPercent;
        }

        internal static bool SkipsBloodPumping(float bloodPumpingPercent)
        {
            return SkipsBloodPumping(bloodPumpingPercent, BloodPumpingSkipPercent);
        }


        internal static bool IsCarrier(Verse.Pawn pawn, bool isRatkin)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   pawn.Spawned &&
                   isRatkin &&
                   HasActive(pawn);
        }

        internal static bool CanReceive(Verse.Pawn pawn, float bloodPumpingPercent, float skipPercent)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   pawn.IsFreeColonist &&
                   pawn.health?.capacities != null &&
                   !HasActive(pawn) &&
                   !SkipsBloodPumping(bloodPumpingPercent, skipPercent);
        }

        internal static bool CanReceive(Verse.Pawn pawn, float bloodPumpingPercent)
        {
            return CanReceive(pawn, bloodPumpingPercent, BloodPumpingSkipPercent);
        }
    }
}

