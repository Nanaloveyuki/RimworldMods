using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{

    public abstract class IncidentWorker_MouseDisasterIntelBase : IncidentWorker
    {
        protected abstract MouseDisasterRequestKind RequestKind { get; }
        protected abstract MouseDisasterIntelSiteKind SiteKind { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            if (map == null)
            {
                return false;
            }

            int amount = MouseDisasterPhase2Utility.CalculateAidAmount(map, RequestKind);
            if (MouseDisasterPhase2Utility.SupportsVisitorDelivery(RequestKind))
            {
                if (!MouseDisasterPhase2Utility.TrySpawnAidRequestVisitors(map, RequestKind, amount, createsIntelSite: true, SiteKind, out Pawn targetPawn, out List<Pawn> pawns, out _))
                {
                    return false;
                }

                Find.LetterStack.ReceiveLetter(
                    def.letterLabel,
                    MouseDisasterPhase2Utility.BuildVisitorDeliveryLetterText(def, targetPawn, RequestKind, amount, createsIntelSite: true),
                    def.letterDef,
                    pawns);
                return true;
            }

            ChoiceLetter_MouseDisasterRequest letter = LetterMaker.MakeLetter(def.letterLabel, def.letterText, MouseDisasterDefOf.MouseDisaster_RequestLetter, new TargetInfo(map.Center, map)) as ChoiceLetter_MouseDisasterRequest;
            if (letter == null)
            {
                return false;
            }

            letter.map = map;
            letter.requestKind = RequestKind;
            letter.amount = amount;
            letter.createsIntelSite = true;
            letter.intelSiteKind = SiteKind;
            Find.LetterStack.ReceiveLetter(letter, null);
            return true;
        }
    }

    public class IncidentWorker_MouseDisasterIntelTreasureSimpleMeal : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.SimpleMeal; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.Treasure; }
    public class IncidentWorker_MouseDisasterIntelTreasureHerbal : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.HerbalMedicine; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.Treasure; }
    public class IncidentWorker_MouseDisasterIntelTreasureSilver : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.Silver; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.Treasure; }
    public class IncidentWorker_MouseDisasterIntelStructureSimpleMeal : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.SimpleMeal; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.StructureCluster; }
    public class IncidentWorker_MouseDisasterIntelStructureHerbal : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.HerbalMedicine; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.StructureCluster; }
    public class IncidentWorker_MouseDisasterIntelStructureSilver : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.Silver; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.StructureCluster; }
    public class IncidentWorker_MouseDisasterIntelSettlementSimpleMeal : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.SimpleMeal; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.SmallSettlement; }
    public class IncidentWorker_MouseDisasterIntelSettlementHerbal : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.HerbalMedicine; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.SmallSettlement; }
    public class IncidentWorker_MouseDisasterIntelSettlementSilver : IncidentWorker_MouseDisasterIntelBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.Silver; protected override MouseDisasterIntelSiteKind SiteKind => MouseDisasterIntelSiteKind.SmallSettlement; }
}
