using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_ReliefArea
    {
        internal static Area_RHAH_Relief Get(Map map)
        {
            if (map == null || map.areaManager == null)
            {
                return null;
            }

            return map.areaManager.Get<Area_RHAH_Relief>();
        }

        internal static bool IsEmpty(Map map)
        {
            Area_RHAH_Relief area = Get(map);
            return area == null || area.TrueCount == 0;
        }

        internal static bool Contains(Map map, IntVec3 cell)
        {
            Area_RHAH_Relief area = Get(map);
            if (area == null || !cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            return area[cell];
        }

        internal static void Ensure(Map map)
        {
            if (map == null || map.areaManager == null || Get(map) != null)
            {
                return;
            }

            map.areaManager.AllAreas.Add(new Area_RHAH_Relief(map.areaManager));
        }
    }
}
