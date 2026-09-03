using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld.Planet;
using Unity.Collections;
using Verse;

namespace OhYouAreSoFast
{
    [HarmonyPatch(typeof(WorldPathing), nameof(WorldPathing.FindPath))]
    internal static class Patch_WorldPathing
    {
        private static readonly ConditionalWeakTable<WorldPathing, SearchState> SearchStates =
            new ConditionalWeakTable<WorldPathing, SearchState>();

        private static bool Prefix(
            WorldPathing __instance,
            PlanetTile startTile,
            PlanetTile destTile,
            Caravan caravan,
            Func<float, bool> terminator,
            ref WorldPath __result)
        {
            if (!(OhYouAreSoFastMod.Settings?.worldPathCorrectness ?? true))
                return true;
            if (terminator != null)
                return true;

            __result = FindPathExact(__instance, startTile, destTile, caravan);
            return false;
        }

        internal static void Release(WorldPathing pathing)
        {
            if (pathing != null)
                SearchStates.Remove(pathing);
        }

        private static WorldPath FindPathExact(
            WorldPathing pathing,
            PlanetTile startTile,
            PlanetTile destTile,
            Caravan caravan)
        {
            PlanetLayer layer = pathing.Layer;
            if (!startTile.Valid)
            {
                Log.Error(string.Format(
                    "Tried to FindPath with invalid start tile {0}, caravan = {1}",
                    startTile,
                    caravan));
                return WorldPath.NotFound;
            }

            if (!destTile.Valid)
            {
                Log.Error(string.Format(
                    "Tried to FindPath with invalid dest tile {0}, caravan = {1}",
                    destTile,
                    caravan));
                return WorldPath.NotFound;
            }

            if (startTile.Layer != destTile.Layer)
            {
                Log.Error(string.Format(
                    "Tried to FindPath to a different layer {0} -> {1}, caravan = {2}",
                    startTile,
                    destTile,
                    caravan));
                return WorldPath.NotFound;
            }

            if (caravan != null)
            {
                if (!caravan.CanReach(destTile))
                    return WorldPath.NotFound;
            }
            else if (!Find.WorldReachability.CanReach(startTile, destTile))
            {
                return WorldPath.NotFound;
            }

            World world = Find.World;
            NativeArray<int> neighborOffsets = layer.UnsafeTileIDToNeighbors_offsets;
            NativeArray<PlanetTile> neighborValues = layer.UnsafeTileIDToNeighbors_values;
            int ticksPerMove = caravan != null ? caravan.TicksPerMove : 3300;
            SearchState state = SearchStates.GetOrCreateValue(pathing);
            state.Begin(layer.TilesCount);
            state.Touch(startTile.tileId);
            state.KnownCosts[startTile.tileId] = 0L;
            state.ParentTileIds[startTile.tileId] = startTile.tileId;
            state.Frontier.Enqueue(startTile.tileId, 0L);

            while (state.Frontier.TryDequeue(out var current))
            {
                if (!state.IsCurrent(current))
                    continue;

                PlanetTile currentTile = new PlanetTile(current.TileId, layer);
                if (currentTile == destTile)
                    return FinalizePath(layer, destTile.tileId, state);

                ValueTuple<int, int> listIndexes = PackedListOfLists.GetListIndexes<PlanetTile>(
                    neighborOffsets,
                    neighborValues,
                    currentTile.tileId);
                for (int i = listIndexes.Item1; i < listIndexes.Item2; i++)
                {
                    PlanetTile nextTile = neighborValues[i];
                    if (state.IsClosed(nextTile.tileId) || world.Impassable(nextTile))
                        continue;

                    state.Touch(nextTile.tileId);
                    // Caravan_PathFollower clamps this value to at least one
                    // tick; WorldPathing's original calculation does not.
                    int moveCost = Caravan_PathFollower.CostToMove(
                        ticksPerMove,
                        currentTile,
                        nextTile,
                        null,
                        true,
                        null,
                        null,
                        false);
                    long nextCost = current.Cost + moveCost;
                    if (nextCost < state.KnownCosts[nextTile.tileId])
                    {
                        state.KnownCosts[nextTile.tileId] = nextCost;
                        state.ParentTileIds[nextTile.tileId] = current.TileId;
                        state.Frontier.Enqueue(nextTile.tileId, nextCost);
                    }
                }

                state.Close(current.TileId);
            }

            Log.Warning(string.Format(
                "{0} pathing from {1} to {2} ran out of tiles to process.",
                caravan,
                startTile,
                destTile));
            return WorldPath.NotFound;
        }

        private static WorldPath FinalizePath(
            PlanetLayer layer,
            int destinationTileId,
            SearchState state)
        {
            WorldPath path = Find.WorldPathPool.GetEmptyWorldPath();
            int tileId = destinationTileId;
            while (true)
            {
                PlanetTile tile = new PlanetTile(tileId, layer);
                path.AddNodeAtStart(tile);
                int parentTileId = state.ParentTileIds[tileId];
                if (tileId == parentTileId)
                    break;
                tileId = parentTileId;
            }

            path.SetupFound((float)state.KnownCosts[destinationTileId], layer);
            return path;
        }

        private sealed class SearchState
        {
            public long[] KnownCosts;
            public int[] ParentTileIds;
            public MinHeap Frontier { get; } = new MinHeap(128);

            private int[] generations;
            private bool[] closed;
            private int generation;

            public void Begin(int tileCount)
            {
                EnsureCapacity(tileCount);
                if (generation == int.MaxValue)
                {
                    Array.Clear(generations, 0, generations.Length);
                    generation = 0;
                }
                generation++;
                Frontier.Clear();
            }

            public void Touch(int tileId)
            {
                if (generations[tileId] == generation)
                    return;

                generations[tileId] = generation;
                closed[tileId] = false;
                KnownCosts[tileId] = long.MaxValue;
                ParentTileIds[tileId] = -1;
            }

            public bool IsClosed(int tileId)
            {
                return generations[tileId] == generation && closed[tileId];
            }

            public bool IsCurrent(OpenNode node)
            {
                return generations[node.TileId] == generation
                    && !closed[node.TileId]
                    && KnownCosts[node.TileId] == node.Cost;
            }

            public void Close(int tileId)
            {
                closed[tileId] = true;
            }

            private void EnsureCapacity(int tileCount)
            {
                if (KnownCosts != null && KnownCosts.Length == tileCount)
                    return;

                KnownCosts = new long[tileCount];
                ParentTileIds = new int[tileCount];
                generations = new int[tileCount];
                closed = new bool[tileCount];
                generation = 0;
            }
        }

        private struct OpenNode
        {
            public int TileId;
            public long Cost;

            public OpenNode(int tileId, long cost)
            {
                TileId = tileId;
                Cost = cost;
            }
        }

        private sealed class MinHeap
        {
            private readonly List<OpenNode> nodes;

            public MinHeap(int capacity)
            {
                nodes = new List<OpenNode>(Math.Max(1, capacity));
            }

            public void Clear()
            {
                nodes.Clear();
            }

            public void Enqueue(int tileId, long cost)
            {
                nodes.Add(new OpenNode(tileId, cost));
                int index = nodes.Count - 1;
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!Before(nodes[index], nodes[parent]))
                        break;

                    OpenNode value = nodes[index];
                    nodes[index] = nodes[parent];
                    nodes[parent] = value;
                    index = parent;
                }
            }

            public bool TryDequeue(out OpenNode result)
            {
                if (nodes.Count == 0)
                {
                    result = default(OpenNode);
                    return false;
                }

                result = nodes[0];
                int lastIndex = nodes.Count - 1;
                OpenNode last = nodes[lastIndex];
                nodes.RemoveAt(lastIndex);
                if (nodes.Count == 0)
                    return true;

                nodes[0] = last;
                int index = 0;
                while (true)
                {
                    int left = index * 2 + 1;
                    if (left >= nodes.Count)
                        break;

                    int right = left + 1;
                    int smallest = left;
                    if (right < nodes.Count && Before(nodes[right], nodes[left]))
                        smallest = right;
                    if (!Before(nodes[smallest], nodes[index]))
                        break;

                    OpenNode value = nodes[index];
                    nodes[index] = nodes[smallest];
                    nodes[smallest] = value;
                    index = smallest;
                }
                return true;
            }

            private static bool Before(OpenNode left, OpenNode right)
            {
                if (left.Cost != right.Cost)
                    return left.Cost < right.Cost;
                return left.TileId < right.TileId;
            }
        }
    }

    [HarmonyPatch(typeof(WorldPathing), nameof(WorldPathing.Dispose))]
    internal static class Patch_WorldPathing_Dispose
    {
        private static void Postfix(WorldPathing __instance)
        {
            Patch_WorldPathing.Release(__instance);
        }
    }
}
