using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{

    [HarmonyPatch(typeof(TransportersArrivalAction_GiveGift), nameof(TransportersArrivalAction_GiveGift.Arrived))]
    public static class MouseDisasterGiftAirdropRetaliationPatch
    {
        private static readonly AccessTools.FieldRef<TransportersArrivalAction_GiveGift, Settlement> SettlementField =
            AccessTools.FieldRefAccess<TransportersArrivalAction_GiveGift, Settlement>("settlement");

        public static void Prefix(TransportersArrivalAction_GiveGift __instance, List<ActiveTransporterInfo> transporters)
        {
            Record(SettlementField(__instance)?.Faction, transporters);
        }

        private static void Record(Faction faction, List<ActiveTransporterInfo> transporters)
        {
            int total = CountEggs(transporters, out bool plague);
            if (total > 0)
            {
                Current.Game.GetComponent<GameComponent_MouseDisasterPhase3>()?.RecordAirdroppedEggs(faction, total, plague);
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
