using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_Approach
    {
        internal static bool SpaceHome(Map map)
        {
            return map != null && RHAH_ApproachRules.IsSpaceHome(SpaceLayer(map.Tile), map.Biome?.defName);
        }

        internal static bool SpaceLayer(PlanetTile tile)
        {
            return tile.Valid && tile.Layer != null && tile.Layer.Def != null && tile.Layer.Def.isSpace;
        }

        internal static bool SpaceApproachEnabled()
        {
            return Core.RHAH_Mod.Settings != null && Core.RHAH_Mod.Settings.spaceApproachEnabled;
        }

        internal static bool TrySend(string displayId, float points, int mapId)
        {
            if (!Core.RHAH_Runtime.AllowsNewContent || string.IsNullOrEmpty(displayId) || Find.WorldObjects == null)
            {
                return false;
            }

            Map home = Home(mapId);
            WorldObjectDef def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("RHAH_Approach");
            if (home == null || !home.Tile.Valid || def == null)
            {
                return false;
            }

            PlanetTile start;
            bool crossing = false;
            if (!TileFinder.TryFindPassableTileWithTraversalDistance(
                home.Tile,
                RHAH_ApproachRules.MinTiles,
                RHAH_ApproachRules.MaxTiles,
                out start,
                tile => tile.Valid && tile != home.Tile && !Find.World.Impassable(tile)))
            {
                crossing = true;
                start = OppositeShore(home.Tile);
                if (!start.Valid)
                {
                    return false;
                }
            }

            WorldObject_RHAH_Approach approach = (WorldObject_RHAH_Approach)WorldObjectMaker.MakeWorldObject(def);
            approach.Tile = start;
            approach.SetFaction(Faction.OfPlayer);
            approach.Configure(displayId, points, home.uniqueID, crossing);

            Find.WorldObjects.Add(approach);
            Find.LetterStack.ReceiveLetter(
                "RHAH_Approach_LetterLabel".Translate(),
                "RHAH_Approach_LetterText".Translate(IncidentLabel(displayId), home.Parent.LabelCap),
                LetterDefOf.NeutralEvent,
                approach);
            return true;
        }


        static Map Home(int mapId)
        {
            Map home = Core.RHAH_MapResolver.MapById(mapId);
            return home != null && home.IsPlayerHome &&
                RHAH_ApproachRules.AllowsHome(SpaceHome(home), SpaceApproachEnabled()) ? home : null;
        }

        static PlanetTile OppositeShore(PlanetTile home)
        {
            if (!home.Valid || home.Layer == null)
            {
                return PlanetTile.Invalid;
            }

            List<PlanetTile> open = new List<PlanetTile> { home };
            HashSet<int> seen = new HashSet<int> { home.tileId };
            List<PlanetTile> neighbors = new List<PlanetTile>();
            int bestId = -1;
            int bestDistance = int.MaxValue;
            for (int distance = 0; open.Count > 0 && distance < 80; distance++)
            {
                List<PlanetTile> next = new List<PlanetTile>();
                for (int i = 0; i < open.Count; i++)
                {
                    neighbors.Clear();
                    home.Layer.GetTileNeighbors(open[i], neighbors);
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        PlanetTile tile = neighbors[n];
                        if (!tile.Valid || !seen.Add(tile.tileId))
                        {
                            continue;
                        }

                        next.Add(tile);
                        if (!Find.World.Impassable(tile))
                        {
                            bestId = RHAH_ApproachRules.PickCrossing(bestId, tile.tileId, bestDistance, distance + 1);
                            if (bestId == tile.tileId)
                            {
                                bestDistance = distance + 1;
                            }
                        }
                    }
                }

                if (bestId >= 0 && distance + 1 >= RHAH_ApproachRules.MinTiles)
                {
                    break;
                }

                open = next;
            }

            return bestId < 0 ? PlanetTile.Invalid : new PlanetTile(bestId, home.Layer);
        }

        static string IncidentLabel(string displayId)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            return entry == null ? displayId : entry.LabelKey.Translate();
        }
    }
}
