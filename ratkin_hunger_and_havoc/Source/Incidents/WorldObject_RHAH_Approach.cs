using System;
using System.Text;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Incidents
{
    public class WorldObject_RHAH_Approach : WorldObject
    {
        string displayId = "";
        float points;
        int mapId;
        int nextTileId = -1;
        int costLeft;
        bool crossing;
        bool arrivalPending;
        bool arriving;

        public override string Label => "RHAH_Approach_Label".Translate();

        public override Vector3 DrawPos
        {
            get
            {
                if (!Spawned || Find.WorldGrid == null || !Tile.Valid)
                {
                    return base.DrawPos;
                }

                Vector3 start = Find.WorldGrid.GetTileCenter(Tile);
                if (nextTileId < 0 || costLeft <= 0)
                {
                    return start;
                }

                PlanetTile next = new PlanetTile(nextTileId, Tile.Layer);
                if (!next.Valid)
                {
                    return start;
                }

                int total = RHAH_ApproachRules.MoveTicks(1f, 1f);
                float traveled = total <= 0 ? 1f : 1f - Mathf.Clamp01(costLeft / (float)total);
                return Vector3.Slerp(start, Find.WorldGrid.GetTileCenter(next), traveled);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref displayId, "displayId", "");
            Scribe_Values.Look(ref points, "points", 0f);
            Scribe_Values.Look(ref mapId, "mapId", 0);
            Scribe_Values.Look(ref nextTileId, "nextTileId", -1);
            Scribe_Values.Look(ref costLeft, "costLeft", 0);
            Scribe_Values.Look(ref crossing, "crossing", false);
            Scribe_Values.Look(ref arrivalPending, "arrivalPending", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && displayId == null)
            {
                displayId = "";
            }
        }

        public override string GetInspectString()
        {
            StringBuilder text = new StringBuilder();
            text.Append("RHAH_Approach_Inspect".Translate(IncidentLabel()));
            if (mapId >= 0)
            {
                text.AppendLine();
                text.Append("RHAH_Approach_Target".Translate(MapLabel()));
            }

            return text.ToString();
        }

        public void Configure(string incidentId, float incidentPoints, int targetMapId, bool crossSea)
        {
            displayId = incidentId ?? "";
            points = incidentPoints;
            mapId = targetMapId;
            crossing = crossSea;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(RimWorld.Planet.Caravan caravan)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(caravan))
            {
                yield return option;
            }

            if (!CanIntercept(caravan))
            {
                yield break;
            }

            bool canGenerate = CanGenerateInterceptionMap();
            yield return new FloatMenuOption(canGenerate
                ? "RHAH_Approach_Intercept".Translate()
                : "RHAH_Approach_InterceptBlocked".Translate(),
                canGenerate ? () => Intercept(caravan) : (Action)null);
        }

        internal bool CanIntercept(RimWorld.Planet.Caravan caravan)
        {
            return RHAH_Runtime.AllowsNewContent && !arriving && Spawned && !Destroyed &&
                caravan != null && caravan.Spawned && !caravan.Destroyed && caravan.IsPlayerControlled &&
                caravan.PawnsListForReading.Count > 0 && Tile.Valid && caravan.Tile == Tile && !SpaceLayer();
        }

        bool CanGenerateInterceptionMap()
        {
            if (Find.WorldObjects == null || Find.WorldGrid == null ||
                !Find.WorldGrid[Tile].PrimaryBiome.implemented)
            {
                return false;
            }

            MapParent existing = Find.WorldObjects.MapParentAt(Tile);
            if (existing != null && (!(existing is WorldObject_RHAH_Interception interception) || !interception.CanReuse))
            {
                return false;
            }

            foreach (WorldObject candidate in Find.WorldObjects.ObjectsAt(Tile))
            {
                if (candidate != this && candidate != existing && candidate.def != null && !candidate.def.allowCaravanIncidentsWhichGenerateMap)
                {
                    return false;
                }
            }

            return true;
        }

        void Intercept(RimWorld.Planet.Caravan caravan)
        {
            if (!CanIntercept(caravan) || !CanGenerateInterceptionMap())
            {
                return;
            }

            arriving = true;
            LongEventHandler.QueueLongEvent(() => EnterInterceptionMap(caravan), "GeneratingMap", false, null);
        }

        void EnterInterceptionMap(RimWorld.Planet.Caravan caravan)
        {
            Map map = null;
            bool created = Current.Game.FindMap(Tile) == null;
            bool completionQueued = false;
            try
            {
                // 生成器的绘制初始化排在长事件结束后
                arriving = false;
                if (!CanIntercept(caravan) || !CanGenerateInterceptionMap())
                {
                    return;
                }

                arriving = true;
                WorldObjectDef parentDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("RHAH_Interception");
                if (parentDef == null)
                {
                    return;
                }

                map = CaravanIncidentUtility.GetOrGenerateMapForIncident(caravan,
                    new IntVec3(100, 1, 100), parentDef);
                if (map == null)
                {
                    return;
                }

                LongEventHandler.ExecuteWhenFinished(() => CompleteInterception(caravan, map, created));
                completionQueued = true;
            }
            finally
            {
                if (!completionQueued)
                {
                    arriving = false;
                    if (created && map != null)
                    {
                        LongEventHandler.ExecuteWhenFinished(() => CleanupInterceptionMap(map));
                    }
                }
            }
        }

        void CompleteInterception(RimWorld.Planet.Caravan caravan, Map map, bool created)
        {
            bool committed = false;
            try
            {
                if (!RHAH_Interception.TrySpawn(map, displayId, points))
                {
                    Messages.Message("RHAH_Approach_InterceptFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                Verse.Pawn colonist = caravan.PawnsListForReading[0];
                CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge,
                    CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
                committed = true;
                Destroy();
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                CameraJumper.TryJumpAndSelect(colonist);
            }
            finally
            {
                arriving = false;
                if (!committed && map != null && (created || map.mapPawns.AnyPawnBlockingMapRemoval))
                {
                    CleanupInterceptionMap(map);
                }
            }
        }

        void CleanupInterceptionMap(Map map)
        {
            if (map.mapPawns.AnyPawnBlockingMapRemoval)
            {
                // 原版进入中途异常时保留已经放下的玩家成员
                Destroy();
            }
            else
            {
                map.Parent.Destroy();
            }
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (arriving || !Spawned || delta <= 0)
            {
                return;
            }

            int left = delta;
            while (left > 0 && !arriving)
            {
                if (costLeft > 0)
                {
                    int spent = costLeft < left ? costLeft : left;
                    costLeft = RHAH_ApproachRules.Spend(costLeft, spent);
                    left -= spent;
                    if (costLeft > 0)
                    {
                        return;
                    }
                }

                if (arrivalPending)
                {
                    Arrive();
                    return;
                }

                if (nextTileId >= 0)
                {
                    PlanetTile entered = new PlanetTile(nextTileId, Tile.Layer);
                    nextTileId = -1;
                    if (entered.Valid)
                    {
                        Tile = entered;
                    }
                }

                if (OnDestination())
                {
                    Arrive();
                    return;
                }

                if (!Step())
                {
                    Arrive();
                    return;
                }
            }
        }

        bool Step()
        {
            Map map = Home();
            if (map?.Tile == null || !map.Tile.Valid || !Tile.Valid || map.Tile.Layer != Tile.Layer || Find.World == null)
            {
                return false;
            }

            if (!RHAH_ApproachRules.AllowsHome(RHAH_Approach.SpaceHome(map), SpaceApproachEnabled()))
            {
                return false;
            }

            WorldPath path;
            try
            {
                path = map.Tile.Layer.Pather.FindPath(Tile, map.Tile, null);
            }
            catch (Exception exception)
            {
                Log.Error("[RHAH] Approach pathing failed for " + displayId + ". " + exception);
                return false;
            }

            if (!RHAH_ApproachRules.TryConsumeNext(path, out PlanetTile next))
            {
                if (RHAH_ApproachRules.ReleasePath(path == null, path == WorldPath.NotFound))
                {
                    path.ReleaseToPool();
                }

                return false;
            }

            path.ReleaseToPool();
            if (!next.Valid || !RHAH_ApproachRules.CanWalk(Find.World.Impassable(next), crossing))
            {
                return false;
            }

            nextTileId = next.tileId;
            float difficulty = WorldPathGrid.CalculatedMovementDifficultyAt(next, false, null, null);
            float road = Find.WorldGrid.GetRoadMovementDifficultyMultiplier(Tile, next, null);
            costLeft = RHAH_ApproachRules.MoveTicks(difficulty, road);
            return true;
        }

        bool SpaceLayer()
        {
            return RHAH_Approach.SpaceLayer(Tile);
        }

        bool SpaceApproachEnabled()
        {
            return RHAH_Approach.SpaceApproachEnabled();
        }

        bool OnDestination()
        {
            Map map = Home();
            return map != null && map.Tile.Valid && Tile == map.Tile;
        }

        void Arrive()
        {
            if (arriving)
            {
                return;
            }

            arrivalPending = true;
            arriving = true;
            try
            {
                GameComponent_RHAH_Game game = Current.Game?.GetComponent<GameComponent_RHAH_Game>();
                RHAH_ArrivalResult result = game == null
                    ? RHAH_ArrivalResult.Waiting
                    : game.SpawnArrived(displayId, points, mapId);
                if (result == RHAH_ArrivalResult.Waiting)
                {
                    costLeft = GenDate.TicksPerHour;
                    return;
                }

                Destroy();
            }
            finally
            {
                arriving = false;
            }
        }

        Map Home()
        {
            if (Find.Maps == null)
            {
                return null;
            }

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (map != null && map.IsPlayerHome && map.uniqueID == mapId)
                {
                    return map;
                }
            }

            return null;
        }

        string IncidentLabel()
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            return entry == null ? displayId : entry.LabelKey.Translate();
        }

        string MapLabel()
        {
            Map map = Home();
            return map == null ? mapId.ToString() : map.Parent.LabelCap;
        }
    }
}
