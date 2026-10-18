using RimWorld.Planet;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_ApproachRules
    {
        internal const int MinTiles = 4;
        internal const int MaxTiles = 6;
        internal const float TilesPerDay = 3f;
        internal const int TicksPerMove = 20000;
        internal const int MinMoveTicks = 1;
        internal const int MaxMoveTicks = 30000;

        internal static bool UsesApproach(RHAH_IncidentTarget target)
        {
            return target == RHAH_IncidentTarget.Map;
        }

        internal static int MoveTicks(float tileDifficulty, float roadMultiplier)
        {
            if (float.IsNaN(tileDifficulty) || float.IsNaN(roadMultiplier) || tileDifficulty <= 0f || roadMultiplier <= 0f)
            {
                return TicksPerMove;
            }

            int ticks = (int)(TicksPerMove * tileDifficulty * roadMultiplier);
            if (ticks < MinMoveTicks)
            {
                return MinMoveTicks;
            }

            if (ticks > MaxMoveTicks)
            {
                return MaxMoveTicks;
            }

            return ticks;
        }

        internal static int Spend(int costLeft, int delta)
        {
            if (delta <= 0 || costLeft <= 0)
            {
                return costLeft;
            }

            int left = costLeft - delta;
            return left < 0 ? 0 : left;
        }

        internal static int PickDistance(int roll)
        {
            int span = MaxTiles - MinTiles + 1;
            int offset = roll % span;
            if (offset < 0)
            {
                offset += span;
            }

            return MinTiles + offset;
        }


        internal static bool CanWalk(bool impassable, bool crossSea)
        {
            return !impassable || crossSea;
        }

        internal static int PickCrossing(int best, int candidate, int bestDistance, int candidateDistance)
        {
            if (candidateDistance < MinTiles)
            {
                return best;
            }

            if (best < 0 || candidateDistance < bestDistance)
            {
                return candidate;
            }

            return best;
        }

        internal static bool IsSpaceHome(bool spaceLayer, string biomeDefName)
        {
            return spaceLayer || biomeDefName == "OuterSpaceBiome";
        }

        internal static bool AllowsHome(bool spaceHome, bool spaceEnabled)
        {
            return !spaceHome || spaceEnabled;
        }

        internal static bool BlocksLaunch(bool spaceLayer)
        {
            return !spaceLayer;
        }

        // 玩家队图标优先级更高 右键先命中玩家队 同格的鼠族队要另外问一次
        internal static bool IncludeApproachMenu(bool playerCaravanHit, bool approachHit, bool sameTile, bool canIntercept)
        {
            return playerCaravanHit && !approachHit && sameTile && canIntercept;
        }

        internal static bool TryConsumeNext(WorldPath path, out PlanetTile next)
        {
            next = default;
            if (path == null || path == WorldPath.NotFound || !path.Found || path.NodesLeftCount < 2 || path.NodeCount < path.NodesLeftCount)
            {
                return false;
            }

            next = path.ConsumeNextNode();
            return next.Valid;
        }

        internal static bool ReleasePath(bool missing, bool sharedFailure)
        {
            return !missing && !sharedFailure;
        }
    }
}
