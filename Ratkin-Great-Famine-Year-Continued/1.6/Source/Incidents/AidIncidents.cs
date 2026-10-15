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
    public abstract class IncidentWorker_MouseDisasterAidBase : IncidentWorker
    {
        protected abstract MouseDisasterRequestKind RequestKind { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            if (map == null || !base.CanFireNowSub(parms))
            {
                return false;
            }

            if (RequestKind == MouseDisasterRequestKind.PrisonerOrSlaveBaby)
            {
                return MouseDisasterUtility.FindExchangeOfferBaby(map, MouseDisasterUtility.ChildExchangeModePrisoner) != null ||
                       MouseDisasterUtility.FindExchangeOfferBaby(map, MouseDisasterUtility.ChildExchangeModeSlave) != null;
            }

            return true;
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
                if (!MouseDisasterPhase2Utility.TrySpawnAidRequestVisitors(map, RequestKind, amount, createsIntelSite: false, MouseDisasterIntelSiteKind.Treasure, out Pawn targetPawn, out List<Pawn> pawns, out _))
                {
                    return false;
                }

                Find.LetterStack.ReceiveLetter(
                    def.letterLabel,
                    MouseDisasterPhase2Utility.BuildVisitorDeliveryLetterText(def, targetPawn, RequestKind, amount, createsIntelSite: false),
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
            letter.createsIntelSite = false;
            Find.LetterStack.ReceiveLetter(letter, null);
            return true;
        }
    }

    public class IncidentWorker_MouseDisasterAidSimpleMeal : IncidentWorker_MouseDisasterAidBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.SimpleMeal; }
    public class IncidentWorker_MouseDisasterAidFineMeal : IncidentWorker_MouseDisasterAidBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.FineMeal; }
    public class IncidentWorker_MouseDisasterAidMedicine : IncidentWorker_MouseDisasterAidBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.Medicine; }
    public class IncidentWorker_MouseDisasterAidSilver : IncidentWorker_MouseDisasterAidBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.Silver; }
    public class IncidentWorker_MouseDisasterAidBaby : IncidentWorker_MouseDisasterAidBase { protected override MouseDisasterRequestKind RequestKind => MouseDisasterRequestKind.PrisonerOrSlaveBaby; }
}
