using HungerAndHavoc.Api;
using HungerAndHavoc.Trade;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_Sequel : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            RHAH_IncidentEntry entry = Entry;
            if (entry == null || !Core.RHAH_Runtime.AllowsNewContent)
            {
                return false;
            }

            if (entry.DisplayId == "I-038")
            {
                return Core.RHAH_MapResolver.Resolve(parms?.target as Map) != null;
            }

            if (entry.Target == RHAH_IncidentTarget.Caravan)
            {
                if (parms != null && parms.forced && parms.target is Map debugMap)
                {
                    return Core.RHAH_MapResolver.Resolve(debugMap) != null && FindAnySpawnCell(debugMap);
                }

                return Caravan.CaravanTargetResolver.Resolve(parms == null ? null : parms.target as RimWorld.Planet.Caravan, parms == null || parms.target == null) != null;
            }

            Map map = Core.RHAH_MapResolver.Resolve(parms?.target as Map);
            return map != null && FindAnySpawnCell(map);
        }
        static bool FindAnySpawnCell(Map map)
        {
            IntVec3 cell;
            return RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal, false, null);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            RHAH_IncidentEntry entry = Entry;
            if (entry == null || !CanFireNowSub(parms))
            {
                return false;
            }

            if (entry.DisplayId == "I-051")
            {
                return parms != null && parms.forced && parms.target is Map debugMap
                    ? GenStep_RHAH_RefugeeCamp.TrySpawnOnMap(debugMap)
                    : RHAH_RefugeeCampQuest.TryOffer(parms);
            }
            if (entry.DisplayId == "I-038")
            {
                return TradeEventRouter.TrySpawnTraderCaravan(entry, parms);
            }

            if (entry.Target == RHAH_IncidentTarget.Caravan)
            {
                return TradeEventRouter.TrySpawnCaravanAmbush(entry, parms == null ? 0f : parms.points,
                    parms == null ? null : parms.target as RimWorld.Planet.Caravan,
                    parms == null || parms.target == null,
                    parms != null && parms.forced ? parms.target as Map : null);
            }

            Map map = Core.RHAH_MapResolver.Resolve(parms?.target as Map);
            if (map == null)
            {
                return false;
            }

            bool airdrop = entry.DisplayId == "I-032" || entry.DisplayId == "I-046";
            IntVec3 cell;
            if (airdrop)
            {
                cell = DropCellFinder.TradeDropSpot(map);
            }
            else if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return false;
            }

            if (!cell.IsValid)
            {
                return false;
            }

            int tick = Core.RHAH_Runtime.NextBatchId(map);
            return RHAH_IncidentFacts.Submit(new RHAH_IncidentContext
            {
                DisplayId = entry.DisplayId,
                SpawnBatchId = tick,
                RelationshipGroupId = tick,
                Role = RoleFor(entry),
                Attitude = RHAH_IncidentArrival.For(entry),
                CarriesPlague = entry.Category == RHAH_IncidentCategory.Plague,
                Map = map,
                SpawnCell = cell,
                DropPod = airdrop,
                PawnCount = RHAH_IncidentScale.Count(entry.DisplayId, parms == null ? 0f : parms.points, EventCap(), false),
                Points = parms == null ? 0f : parms.points
            });
        }
        static int EventCap()
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            return settings == null ? RHAH_IncidentScale.DefaultEventPawns : settings.maxEventPawns;
        }

        RHAH_IncidentEntry Entry => def == null ? null : RHAH_IncidentCatalog.GetByDefName(def.defName);

        static RHAH_PawnRole RoleFor(RHAH_IncidentEntry entry)
        {
            switch (entry.Family)
            {
                case RHAH_IncidentFamily.Thief: return RHAH_PawnRole.Thief;
                case RHAH_IncidentFamily.Wild: return RHAH_PawnRole.Wild;
                case RHAH_IncidentFamily.Siege: return RHAH_PawnRole.Siege;
                case RHAH_IncidentFamily.Trade: return RHAH_PawnRole.Trader;
                case RHAH_IncidentFamily.Aid: return RHAH_PawnRole.Refugee;
                case RHAH_IncidentFamily.Intel: return RHAH_PawnRole.Envoy;
                case RHAH_IncidentFamily.Special: return RHAH_PawnRole.Refugee;
                default: return RHAH_PawnRole.Beggar;
            }
        }


    }
}
