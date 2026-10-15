using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_Interception
    {
        internal static bool TrySpawn(Map map, string displayId, float points)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            WorldObject_RHAH_Interception parent = map?.Parent as WorldObject_RHAH_Interception;
            Faction faction = Trade.TradeEventRouter.RequireAmbushFaction();
            if (entry == null || parent == null || faction == null || !TryCrossing(map, out IntVec3 start, out IntVec3 exit))
            {
                return false;
            }

            int batch = RHAH_Runtime.NextBatchId(map);
            Storyteller.Suiyin.SuiyinBook book = Current.Game?.GetComponent<NarrativeState>()?.Book;
            if (book != null)
            {
                for (int i = 0; i < book.Journals.Count; i++)
                {
                    if (book.Journals[i].BatchId >= batch)
                    {
                        batch = book.Journals[i].BatchId + 1;
                    }
                }
            }
            while (RHAH_Runtime.IsBatchActive(map, batch))
            {
                batch++;
            }
            RHAH_Settings settings = RHAH_Mod.Settings;
            int count = RHAH_IncidentScale.Count(displayId, points,
                settings == null ? RHAH_IncidentScale.DefaultEventPawns : settings.maxEventPawns, false);
            List<Verse.Pawn> pawns = new List<Verse.Pawn>(count);
            bool committed = false;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    RHAH_PawnRole role = RHAH_IncidentRoster.RoleAt(displayId, Role(entry.Family), i);
                    RHAH_PawnCreationResult result = RHAH_PawnFactory.Create(new RHAH_PawnRequest
                    {
                        SourceIncidentDisplayId = displayId,
                        SpawnBatchId = batch,
                        RelationshipGroupId = batch,
                        Role = role,
                        AttitudeAtArrival = RHAH_Attitude.Hostile,
                        CarriesPlague = entry.Category == RHAH_IncidentCategory.Plague,
                        Map = map,
                        PawnKind = RHAH_DefOf.RHAH_PawnKind_Ratkin,
                        Faction = faction,
                        SpawnCell = start,
                        Gender = RHAH_IncidentRoster.GenderAt(displayId, i),
                        // 拦截队必须能够自行穿图 不触发原事件伤残与分娩
                        BiologicalAge = RHAH_PawnDefaults.IsYoungRole(role) ? Rand.Range(4f, 13.99f) : Rand.Range(18f, 50f)
                    }, registerBatch: false);
                    if (!result.Succeeded)
                    {
                        return false;
                    }

                    pawns.AddRange(result.Pawns);
                }

                Lord lord = LordMaker.MakeNewLord(faction, new LordJob_RHAH_Intercept(exit), map, pawns);
                if (lord == null)
                {
                    return false;
                }

                List<int> ids = new List<int>(pawns.Count);
                for (int i = 0; i < pawns.Count; i++)
                {
                    ids.Add(pawns[i].thingIDNumber);
                }

                Current.Game?.GetComponent<NarrativeState>()?.Commit(ledger =>
                    ledger.OpenInterception(map.uniqueID, batch, Find.TickManager.TicksGame, ids));
                parent.Configure(displayId, batch);
                RHAH_Runtime.RegisterBatch(map, batch);
                committed = true;
                return true;
            }
            finally
            {
                if (!committed)
                {
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        pawns[i].Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }

        internal static bool IsInterception(int batchId)
        {
            Storyteller.Suiyin.SuiyinBook book = Current.Game?.GetComponent<NarrativeState>()?.Book;
            if (book == null || batchId <= 0)
            {
                return false;
            }

            for (int i = 0; i < book.Journals.Count; i++)
            {
                if (book.Journals[i].BatchId == batchId && book.Journals[i].Id == 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryCrossing(Map map, out IntVec3 start, out IntVec3 exit)
        {
            start = IntVec3.Invalid;
            exit = IntVec3.Invalid;
            int first = Rand.Range(0, 4);
            for (int i = 0; i < 4; i++)
            {
                Rot4 side = new Rot4((first + i) % 4);
                IntVec3 candidateExit = IntVec3.Invalid;
                if (CellFinder.TryFindRandomEdgeCellWith(cell => cell.Standable(map) &&
                    CellFinder.TryFindRandomEdgeCellWith(other => other.Standable(map) &&
                        map.reachability.CanReach(cell, other, PathEndMode.OnCell,
                            TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly)),
                        map, side.Opposite, 0f, out candidateExit), map, side, 0f, out start))
                {
                    exit = candidateExit;
                    return true;
                }
            }

            return false;
        }

        static RHAH_PawnRole Role(RHAH_IncidentFamily family)
        {
            switch (family)
            {
                case RHAH_IncidentFamily.Beggar: return RHAH_PawnRole.Beggar;
                case RHAH_IncidentFamily.Thief: return RHAH_PawnRole.Thief;
                case RHAH_IncidentFamily.Wild: return RHAH_PawnRole.Wild;
                case RHAH_IncidentFamily.Siege: return RHAH_PawnRole.Siege;
                case RHAH_IncidentFamily.Trade: return RHAH_PawnRole.Trader;
                case RHAH_IncidentFamily.Intel: return RHAH_PawnRole.Envoy;
                default: return RHAH_PawnRole.Refugee;
            }
        }
    }
}
