using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Identity
{
    internal static class RHAH_PlagueRuntime
    {
        internal static void TickMap(MapComponent_RHAH_Map component, Map map)
        {
            if (component == null || map == null || !map.IsPlayerHome || Find.TickManager == null)
            {
                return;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            bool plagueOn = settings == null || settings.plagueEnabled;
            if (!plagueOn)
            {
                return;
            }

            int spreadHour = settings == null ? RHAH_Plague.SpreadHour : settings.plagueSpreadHour;
            if (GenLocalDate.HourInteger(map) != spreadHour)
            {
                return;
            }


            int absoluteDay = GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);
            int interval = settings == null ? RHAH_Plague.SpreadDayInterval : settings.plagueSpreadDayInterval;
            if (!RHAH_Plague.IsSpreadDay(absoluteDay, component.PlagueLastSpreadDay, interval))
            {
                return;
            }

            component.PlagueLastSpreadDay = absoluteDay;
            Spread(map, settings);
        }

        static void Spread(Map map, RHAH_Settings settings)
        {
            HediffDef def = RHAH_Plague.Def;
            if (def == null || map.mapPawns == null)
            {
                return;
            }

            IReadOnlyList<Verse.Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            int carriers = 0;
            for (int i = 0; i < spawned.Count; i++)
            {
                Verse.Pawn pawn = spawned[i];
                if (RHAH_Plague.IsCarrier(pawn, RHAH_Race.IsRatkin(pawn?.def)))
                {
                    carriers++;
                }
            }

            float perCarrier = settings == null ? RHAH_Plague.SpreadChancePerCarrier : settings.plagueSpreadChancePerCarrier;
            float cap = settings == null ? RHAH_Plague.SpreadChanceCap : settings.plagueSpreadChanceCap;
            float chance = RHAH_Plague.SpreadChance(carriers, perCarrier, cap);
            if (chance <= 0f)
            {
                return;
            }

            List<Verse.Pawn> infected = new List<Verse.Pawn>();
            List<Verse.Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Verse.Pawn pawn = colonists[i];
                float pumping = pawn?.health?.capacities == null
                    ? 0f
                    : pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping) * 100f;
                float skip = settings == null ? RHAH_Plague.BloodPumpingSkipPercent : settings.plagueBloodPumpingSkipPercent;
                if (!RHAH_Plague.CanReceive(pawn, pumping, skip) || !Rand.Chance(chance))
                {
                    continue;
                }

                if (RHAH_Plague.TryInfect(pawn, null))
                {
                    infected.Add(pawn);
                    HungerAndHavoc.EventMgr.RHAH_EventFollowMood.NoteTraderSpread(map);
                }
            }

            if (infected.Count > 0)
            {
                Messages.Message(
                    "RHAH_PlagueSpreading".Translate(),
                    infected,
                    MessageTypeDefOf.NegativeHealthEvent,
                    historical: true);
            }
        }

    }
}
