using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    internal static class MouseDisasterPlagueUtility
    {

        private const float PlagueStartSeverityMin = 0f;
        private const float PlagueStartSeverityMax = 0.1f;

        public static bool IsPlagueCarrierMouseDisasterPawn(Pawn pawn)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   pawn.Spawned &&
                   MouseDisasterUtility.IsRatkin(pawn) &&
                   pawn.health?.hediffSet?.HasHediff(MouseDisasterDefOf.MouseDisaster_Plague) == true;
        }

        public static void InfectWithPlague(Pawn pawn, float? severity = null)
        {
            if (pawn?.health == null || MouseDisasterDefOf.MouseDisaster_Plague == null)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_Plague);
            if (existing != null)
            {
                if (severity.HasValue)
                {
                    existing.Severity = Mathf.Max(existing.Severity, severity.Value);
                }
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(MouseDisasterDefOf.MouseDisaster_Plague, pawn);
            hediff.Severity = severity ?? Rand.Range(PlagueStartSeverityMin, PlagueStartSeverityMax);
            pawn.health.AddHediff(hediff);
        }

        public static void InfectMany(IEnumerable<Pawn> pawns, bool leaderOnly = false)
        {
            if (pawns == null)
            {
                return;
            }

            List<Pawn> list = pawns.Where(p => p != null).ToList();
            if (leaderOnly)
            {
                if (list.Count > 0)
                {
                    InfectWithPlague(list[0]);
                }
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                InfectWithPlague(list[i]);
            }
        }

        public static void DoPlagueSpreadCheck(Map map)
        {
            if (map == null || MouseDisasterDefOf.MouseDisaster_Plague == null)
            {
                return;
            }

            int carrierCount = map.mapPawns.AllPawnsSpawned.Count(IsPlagueCarrierMouseDisasterPawn);
            if (carrierCount <= 0)
            {
                return;
            }

            float chance = Mathf.Min(0.005f * carrierCount, 0.30f);
            List<Pawn> infected = new List<Pawn>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.Where(p => p != null && !p.Dead && p.health?.capacities != null).ToList();
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn.health.hediffSet.HasHediff(MouseDisasterDefOf.MouseDisaster_Plague))
                {
                    continue;
                }

                float bloodPumpingPercent = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping) * 100f;
                if (bloodPumpingPercent >= 120f)
                {
                    continue;
                }

                if (!Rand.Chance(chance))
                {
                    continue;
                }

                InfectWithPlague(pawn);
                infected.Add(pawn);
            }

            if (infected.Count > 0)
            {
                Messages.Message("MouseDisaster_UI_PlagueSpreading".Translate().Resolve(), infected, MessageTypeDefOf.NegativeHealthEvent, historical: true);
            }
        }
    }
}
