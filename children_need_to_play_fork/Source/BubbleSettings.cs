using Verse;
using System.Collections.Generic;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Static utility class for managing global bubble settings and tracking active bubbles per map.
    /// </summary>
    public static class BubbleSettings
    {
        /// <summary>
        /// Dictionary tracking active bubble count per map ID.
        /// </summary>
        private static Dictionary<int, int> bubbleCountPerMap = new Dictionary<int, int>();

        /// <summary>
        /// Checks if a new bubble can be spawned on the given map.
        /// Returns false if the global hard limit has been reached.
        /// </summary>
        public static bool CanSpawnBubble(Map map)
        {
            if (map == null)
            {
                return false;
            }

            int mapId = map.uniqueID;
            if (!bubbleCountPerMap.TryGetValue(mapId, out int currentCount))
            {
                currentCount = 0;
            }

            // Check against the configured hard limit
            return currentCount < TPE_Mod.Settings.maxBubblesPerMap;
        }

        /// <summary>
        /// Registers a new bubble as active on the map.
        /// </summary>
        public static void AddBubbleToMap(Map map)
        {
            if (map == null)
            {
                return;
            }

            int mapId = map.uniqueID;
            if (!bubbleCountPerMap.TryGetValue(mapId, out int currentCount))
            {
                currentCount = 0;
            }

            bubbleCountPerMap[mapId] = currentCount + 1;
        }

        /// <summary>
        /// Unregisters a bubble when it despawns.
        /// </summary>
        public static void RemoveBubbleFromMap(Map map)
        {
            if (map == null)
            {
                return;
            }

            int mapId = map.uniqueID;
            if (bubbleCountPerMap.TryGetValue(mapId, out int currentCount))
            {
                currentCount = System.Math.Max(0, currentCount - 1);
                bubbleCountPerMap[mapId] = currentCount;
            }
        }

        /// <summary>
        /// Clears bubble tracking for a map when it's removed (e.g., abandoned).
        /// </summary>
        public static void ClearMapBubbles(Map map)
        {
            if (map == null)
            {
                return;
            }

            bubbleCountPerMap.Remove(map.uniqueID);
        }
    }
}
