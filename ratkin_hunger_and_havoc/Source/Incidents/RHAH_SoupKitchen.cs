using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_SoupKitchen
    {
        internal const int DurationDays = 10;

        internal static bool EligibleFaction(Faction faction)
        {
            return faction != null && !faction.IsPlayer && !faction.def.hidden && !faction.defeated &&
                !faction.HostileTo(Faction.OfPlayer);
        }

        internal static int GoodwillLoss(float wealth)
        {
            return wealth <= 5000f ? 0 : 5 + (int)Math.Floor((wealth - 5000f) / 1000f);
        }

        internal static float Wealth(RimWorld.Planet.Caravan caravan)
        {
            float wealth = 0f;
            List<Verse.Pawn> pawns = caravan.PawnsListForReading;
            for (int i = 0; i < pawns.Count; i++)
            {
                wealth += pawns[i].MarketValue + WealthWatcher.GetEquipmentApparelAndInventoryWealth(pawns[i]);
            }
            return wealth;
        }
    }
}
