using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Storyteller.Suiyin;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_IncidentArrival
    {
        internal static bool TryFindSpawnCell(Map map, out IntVec3 cell, out bool dropPod, bool airdrop = false)
        {
            dropPod = airdrop;
            if (!airdrop && RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Animal, false, null))
            {
                return true;
            }

            // 大气层也可使用 OrbitLayer，但 isSpace=false；实体地面不一定连到地图边缘。
            if (map.Tile.Layer is OrbitLayer)
            {
                dropPod = true;
                if (CellFinder.TryFindRandomCell(map, c => c.Standable(map) && !c.Fogged(map) &&
                    !c.GetTerrain(map).dangerous && DropCellFinder.CanPhysicallyDropInto(c, map, true) &&
                    map.reachability.CanReachColony(c), out cell))
                {
                    return true;
                }
            }
            else if (airdrop)
            {
                cell = DropCellFinder.TradeDropSpot(map);
                return cell.IsValid;
            }

            cell = IntVec3.Invalid;
            return false;
        }

        internal static RHAH_Attitude For(string displayId)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            return For(entry);
        }

        internal static RHAH_Attitude For(RHAH_IncidentEntry entry)
        {
            if (entry == null)
            {
                return RHAH_Attitude.Neutral;
            }
            if (entry.DisplayId == "I-052")
            {
                return RHAH_Attitude.Neutral;
            }
            if (entry.DisplayId == "I-054")
            {
                return RHAH_Attitude.Hostile;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            RHAH_Attitude configured = settings == null
                ? entry.DefaultAttitude
                : settings.IncidentAttitude(entry.DisplayId, entry.DefaultAttitude);
            NarrativeState state = Verse.Current.Game?.GetComponent<NarrativeState>();
            int trust = state == null ? 0 : state.Snapshot().Trust;
#if RIMWORLD_1_7
            if (state?.CareTrustEnabled != true) return configured;
#endif
            return RHAH_AttitudePolicy.Arrival(configured, RHAH_EndingRuntime.CountsNow(), trust);
        }
    }
}
