using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Core
{
    internal static class RHAH_MapResolver
    {
        internal static Map Resolve(Map selectedMap = null, int? selectedMapId = null)
        {
            if (selectedMap != null && IsUsable(selectedMap))
            {
                return selectedMap;
            }

            if (selectedMapId.HasValue)
            {
                List<Map> mapsById = Find.Maps;
                for (int i = 0; i < mapsById.Count; i++)
                {
                    if (mapsById[i].uniqueID == selectedMapId.Value && IsUsable(mapsById[i]))
                    {
                        return mapsById[i];
                    }
                }

                return null;
            }

            List<Map> maps = new List<Map>();
            List<Map> allMaps = Find.Maps;
            for (int i = 0; i < allMaps.Count; i++)
            {
                if (IsUsable(allMaps[i]))
                {
                    maps.Add(allMaps[i]);
                }
            }

            return maps.Count == 0 ? null : maps.RandomElement();
        }

        internal static Map MapById(int mapId)
        {
            if (mapId < 0 || Find.Maps == null)
            {
                return null;
            }

            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i] != null && maps[i].uniqueID == mapId)
                {
                    return maps[i];
                }
            }

            return null;
        }

        static bool IsUsable(Map map)
        {
            return map != null && map.mapPawns != null && !map.Parent.Destroyed;
        }
    }

    internal readonly struct RHAH_QueuedTarget
    {
        internal readonly IIncidentTarget Target;
        internal readonly bool Unavailable;
        internal readonly bool Terminal;
        internal readonly string Reason;

        RHAH_QueuedTarget(IIncidentTarget target, bool unavailable, bool terminal, string reason)
        {
            Target = target;
            Unavailable = unavailable;
            Terminal = terminal;
            Reason = reason;
        }

        internal static RHAH_QueuedTarget Resolve(HungerAndHavoc.Incidents.RHAH_IncidentTarget kind, int savedId)
        {
            int rawId = HungerAndHavoc.Incidents.RHAH_IncidentSchedule.DecodeTargetId(savedId, kind);
            if (kind == HungerAndHavoc.Incidents.RHAH_IncidentTarget.Caravan)
            {
                if (rawId == HungerAndHavoc.Incidents.RHAH_IncidentSchedule.UnspecifiedTargetId)
                {
                    RimWorld.Planet.Caravan selected = Caravan.CaravanTargetResolver.ResolvePlayerCaravan();
                    return selected == null
                        ? Waiting("No player caravan.")
                        : Ready(selected);
                }

                RimWorld.Planet.Caravan caravan = Caravan.CaravanTargetResolver.CaravanById(rawId);
                if (caravan == null)
                {
                    return Gone("Saved caravan is gone.");
                }

                return Caravan.CaravanTargetResolver.IsPlayerCaravan(caravan)
                    ? Ready(caravan)
                    : Waiting("Saved caravan is not ready.");
            }

            if (rawId == HungerAndHavoc.Incidents.RHAH_IncidentSchedule.UnspecifiedTargetId &&
                savedId != HungerAndHavoc.Incidents.RHAH_IncidentSchedule.MapZeroTargetId)
            {
                Map selected = RHAH_MapResolver.Resolve();
                return selected == null ? Waiting("No usable map.") : Ready(selected);
            }

            Map map = RHAH_MapResolver.MapById(rawId);
            if (map == null)
            {
                return Gone("Saved map is gone.");
            }

            return map.mapPawns != null && !map.Parent.Destroyed
                ? Ready(map)
                : Waiting("Saved map is not usable.");
        }

        static RHAH_QueuedTarget Ready(IIncidentTarget target)
        {
            return new RHAH_QueuedTarget(target, false, false, null);
        }

        static RHAH_QueuedTarget Waiting(string reason)
        {
            return new RHAH_QueuedTarget(null, true, false, reason);
        }

        static RHAH_QueuedTarget Gone(string reason)
        {
            return new RHAH_QueuedTarget(null, false, true, reason);
        }
}
}
