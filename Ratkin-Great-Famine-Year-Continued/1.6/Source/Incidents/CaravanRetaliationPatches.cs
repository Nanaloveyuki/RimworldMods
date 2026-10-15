using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{

    [HarmonyPatch(typeof(TransportersArrivalAction_AttackSettlement), nameof(TransportersArrivalAction_AttackSettlement.Arrived))]
    public static class MouseDisasterAttackSettlementRetaliationPatch
    {
        private static readonly AccessTools.FieldRef<TransportersArrivalAction_AttackSettlement, Settlement> SettlementField =
            AccessTools.FieldRefAccess<TransportersArrivalAction_AttackSettlement, Settlement>("settlement");

        public static void Prefix(TransportersArrivalAction_AttackSettlement __instance, List<ActiveTransporterInfo> transporters)
        {
            int total = CountEggs(transporters, out bool plague);
            if (total > 0)
            {
                Current.Game.GetComponent<GameComponent_MouseDisasterPhase3>()?.RecordAirdroppedEggs(SettlementField(__instance)?.Faction, total, plague);
            }
        }

        private static int CountEggs(IEnumerable<ActiveTransporterInfo> transporters, out bool plague)
        {
            plague = false;
            int total = 0;
            foreach (ActiveTransporterInfo transporter in transporters)
            {
                if (transporter?.innerContainer == null)
                {
                    continue;
                }

                for (int i = 0; i < transporter.innerContainer.Count; i++)
                {
                    if (!(transporter.innerContainer[i] is Pawn pawn) || !MouseDisasterUtility.IsMouseEggBaby(pawn))
                    {
                        continue;
                    }

                    total++;
                    plague |= MouseDisasterPhase3Utility.IsPlagueCarrier(pawn);
                }
            }

            return total;
        }
    }

    [HarmonyPatch(typeof(TransportersArrivalAction_FormCaravan), nameof(TransportersArrivalAction_FormCaravan.Arrived))]
    public static class MouseDisasterVisitSettlementRetaliationPatch
    {
        private static readonly AccessTools.FieldRef<TransportersArrivalAction_VisitSettlement, Settlement> SettlementField =
            AccessTools.FieldRefAccess<TransportersArrivalAction_VisitSettlement, Settlement>("settlement");

        public static void Prefix(TransportersArrivalAction_FormCaravan __instance, List<ActiveTransporterInfo> transporters)
        {
            if (!(__instance is TransportersArrivalAction_VisitSettlement visitSettlement))
            {
                return;
            }

            int total = CountEggs(transporters, out bool plague);
            if (total > 0)
            {
                Current.Game.GetComponent<GameComponent_MouseDisasterPhase3>()?.RecordAirdroppedEggs(SettlementField(visitSettlement)?.Faction, total, plague);
            }
        }

        private static int CountEggs(IEnumerable<ActiveTransporterInfo> transporters, out bool plague)
        {
            plague = false;
            int total = 0;
            foreach (ActiveTransporterInfo transporter in transporters)
            {
                if (transporter?.innerContainer == null)
                {
                    continue;
                }

                for (int i = 0; i < transporter.innerContainer.Count; i++)
                {
                    if (!(transporter.innerContainer[i] is Pawn pawn) || !MouseDisasterUtility.IsMouseEggBaby(pawn))
                    {
                        continue;
                    }

                    total++;
                    plague |= MouseDisasterPhase3Utility.IsPlagueCarrier(pawn);
                }
            }

            return total;
        }
    }
}
