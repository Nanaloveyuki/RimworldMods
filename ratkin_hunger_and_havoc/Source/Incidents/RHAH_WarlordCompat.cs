using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_WarlordCompat
    {
        internal const string WarlordPackage = "fxz.ratkinfaction";
        internal const string UndergroundPackage = "rku.ratkinunderground";
        internal const string UndergroundFaction = "RKU_Faction";
        internal const string SuicideSquad = "I-055";
        internal const string MutilatedChildren = "I-056";

        internal static bool IsVictimEvent(string displayId)
        {
            return displayId == SuicideSquad || displayId == MutilatedChildren;
        }

        internal static bool Available(string displayId)
        {
            return !IsVictimEvent(displayId) || ModsConfig.IsActive(WarlordPackage);
        }

        internal static bool IsUnderground(Faction faction)
        {
            return ModsConfig.IsActive(UndergroundPackage) && faction?.def?.defName == UndergroundFaction;
        }

        internal static bool AcceptsTransfer(Faction faction, string displayId)
        {
            return IsUnderground(faction) && !faction.IsPlayer && !faction.defeated && !faction.temporary &&
                faction.def.humanlikeFaction && displayId != SuicideSquad;
        }

        internal static bool AllowsDestination(string displayId, bool underground)
        {
            return displayId != SuicideSquad && (displayId != MutilatedChildren || underground);
        }

        internal static float YoungAge(bool toddlers, float roll)
        {
            float minimum = toddlers ? 1f + 48f / 60f : 3f + 1f / 60f;
            return minimum + (6.99f - minimum) * UnityEngine.Mathf.Clamp01(roll);
        }

        internal static int UndergroundGoodwill(int goodwill)
        {
            return goodwill >= -25 && goodwill <= -1 ? 1 : 0;
        }

        internal static bool UndergroundGift(int goodwill)
        {
            return goodwill >= 75;
        }
    }
}
