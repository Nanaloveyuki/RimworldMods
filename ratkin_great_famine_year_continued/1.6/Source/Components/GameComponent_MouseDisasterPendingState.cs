using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    public class GameComponent_MouseDisasterPendingState : GameComponent
    {
        private List<int> forcePrisonerOnPurchasePawnIds = new List<int>();
        private List<int> tradableChattelPawnIds = new List<int>();
        private List<int> childExchangeMoodPawnIds = new List<int>();
        private List<int> siegeBeggarPawnIds = new List<int>();
        private List<int> siegeBeggarStoleFoodSuccessPawnIds = new List<int>();
        private List<int> strongSiegePawnIds = new List<int>();
        private Dictionary<int, int> airDropStayUntilTickByPawnId = new Dictionary<int, int>();
        private Dictionary<int, int> wallGnawCounts = new Dictionary<int, int>();

        public GameComponent_MouseDisasterPendingState(Game game)
        {
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                foreach (int id in MouseDisasterUtility.NextBegTickByPawnId.Where(pair => pair.Value <= Find.TickManager.TicksGame).Select(pair => pair.Key).ToList())
                    MouseDisasterUtility.NextBegTickByPawnId.Remove(id);
            Scribe_Collections.Look(ref MouseDisasterUtility.NextBegTickByPawnId, "mouseDisaster_nextBegTicks", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                MouseDisasterUtility.NextBegTickByPawnId ??= new Dictionary<int, int>();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                forcePrisonerOnPurchasePawnIds = MouseDisasterUtility.CopyForcePrisonerOnPurchasePawnIds();
                tradableChattelPawnIds = MouseDisasterUtility.CopyTradableChattelPawnIds();
                childExchangeMoodPawnIds = MouseDisasterUtility.CopyChildExchangeMoodPawnIds();
                siegeBeggarPawnIds = MouseDisasterUtility.CopySiegeBeggarPawnIds();
                siegeBeggarStoleFoodSuccessPawnIds = MouseDisasterUtility.CopySiegeBeggarStoleFoodSuccessPawnIds();
                strongSiegePawnIds = MouseDisasterUtility.CopyStrongSiegePawnIds();
                airDropStayUntilTickByPawnId = MouseDisasterUtility.CopyAirDropStayUntilTickByPawnId();
                wallGnawCounts = MouseDisasterUtility.CopyWallGnawCounts();
            }

            Scribe_Collections.Look(ref forcePrisonerOnPurchasePawnIds, "mouseDisaster_forcePrisonerOnPurchasePawnIds", LookMode.Value);
            Scribe_Collections.Look(ref tradableChattelPawnIds, "mouseDisaster_tradableChattelPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref childExchangeMoodPawnIds, "mouseDisaster_childExchangeMoodPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref siegeBeggarPawnIds, "mouseDisaster_siegeBeggarPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref siegeBeggarStoleFoodSuccessPawnIds, "mouseDisaster_siegeBeggarStoleFoodSuccessPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref strongSiegePawnIds, "mouseDisaster_strongSiegePawnIds", LookMode.Value);
            Scribe_Collections.Look(ref airDropStayUntilTickByPawnId, "mouseDisaster_airDropStayUntilTickByPawnId", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref wallGnawCounts, "mouseDisaster_wallGnawCounts", LookMode.Value, LookMode.Value);

            MouseDisasterPhase2Utility.ExposePendingStateData();
            MouseDisasterUtility.ExposePendingStateData();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                forcePrisonerOnPurchasePawnIds ??= new List<int>();
                tradableChattelPawnIds ??= new List<int>();
                childExchangeMoodPawnIds ??= new List<int>();
                siegeBeggarPawnIds ??= new List<int>();
                siegeBeggarStoleFoodSuccessPawnIds ??= new List<int>();
                strongSiegePawnIds ??= new List<int>();
                airDropStayUntilTickByPawnId ??= new Dictionary<int, int>();
                wallGnawCounts ??= new Dictionary<int, int>();
                MouseDisasterUtility.RestorePendingMarkerState(
                    forcePrisonerOnPurchasePawnIds,
                    tradableChattelPawnIds,
                    childExchangeMoodPawnIds,
                    siegeBeggarPawnIds,
                    siegeBeggarStoleFoodSuccessPawnIds,
                    strongSiegePawnIds,
                    airDropStayUntilTickByPawnId,
                    wallGnawCounts);
            }
        }

        public override void StartedNewGame()
        {
            MouseDisasterPhase2Utility.ResetPendingState();
            MouseDisasterUtility.ResetPendingState();
        }

        public override void LoadedGame()
        {
            MouseDisasterUtility.ResetLoadedGameTransientState();
            MouseDisasterPhase2Utility.CleanupLoadedPendingState();
            MouseDisasterUtility.CleanupLoadedPendingState();
            MouseDisasterUtility.RepairLoadedTradeLeaders();
            MouseDisasterUtility.RestoreAbandonedDeliveryDuties();
            // Earlier versions cleared duties after creating the child-exchange defend Lord.
            foreach (Map map in Find.Maps)
                foreach (Lord lord in map.lordManager.lords.ToList())
                {
                    // Upgrade only a still-present exchange family; never recreate a departed trader.
                    Pawn trader = lord.ownedPawns.FirstOrDefault(p => p?.health?.hediffSet.HasHediff(
                        MouseDisasterDefOf.MouseDisaster_ChildExchangeTrader) == true && !MouseDisasterUtility.IsPlayerAffiliatedRatkin(p));
                    var children = lord.ownedPawns.Where(p => p != null && p.DevelopmentalStage != DevelopmentalStage.Adult &&
                        !MouseDisasterUtility.IsPlayerAffiliatedRatkin(p)).ToList();
                    if (lord.LordJob is LordJob_TravelAndExit && trader != null && children.Count > 0)
                    {
                        MouseDisasterPawnGroupUtility.SendFamilyAway(map, trader, lord.ownedPawns.ToList(), children);
                        continue;
                    }
                    if (lord.LordJob is LordJob_DefendPoint && lord.CurLordToil != null &&
                        lord.ownedPawns.Any(pawn => pawn?.mindState != null && pawn.mindState.duty == null &&
                            MouseDisasterUtility.IsMouseDisasterIncidentVisitor(pawn)))
                        lord.CurLordToil.UpdateAllDuties();
                }
        }
    }
}
