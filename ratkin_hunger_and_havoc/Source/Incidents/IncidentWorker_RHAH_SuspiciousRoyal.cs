using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_RHAH_SuspiciousRoyal : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
            return map?.IsPlayerHome == true && game != null && RHAH_Runtime.AllowsNewContent &&
                RHAH_Mod.Settings?.IsIncidentEnabled(RHAH_SuspiciousRoyal.DisplayId) != false &&
                RHAH_SuspiciousRoyal.Available(game.LastRoyalArrivalTick, Find.TickManager.TicksGame,
                    RHAH_RoyalSilver.Stored(map)) &&
                Pawn.Compat.RHAH_SkylandCompat.AllowsIncidents(map) &&
                RHAH_IncidentArrival.TryFindSpawnCell(map, out _, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            if (!RHAH_IncidentArrival.TryFindSpawnCell(map, out IntVec3 cell, out bool dropPod)) return false;
            int batchId = RHAH_Runtime.NextBatchId(map);
            bool arrived = RHAH_IncidentFacts.Submit(new RHAH_IncidentContext
            {
                DisplayId = RHAH_SuspiciousRoyal.DisplayId,
                SpawnBatchId = batchId,
                RelationshipGroupId = batchId,
                Role = RHAH_PawnRole.Refugee,
                Attitude = RHAH_Attitude.Neutral,
                Map = map,
                SpawnCell = cell,
                DropPod = dropPod,
                PawnCount = Rand.Chance(0.5f) ? 3 : 1,
                Points = parms.points
            });
            if (arrived)
                Current.Game.GetComponent<GameComponent_RHAH_Game>().LastRoyalArrivalTick = Find.TickManager.TicksGame;
            return arrived;
        }
    }
}
