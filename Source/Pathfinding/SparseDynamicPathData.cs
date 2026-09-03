using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Verse;

namespace OhYouAreSoFast
{
    internal sealed class TouchedNativeIndices
    {
        public Map Owner;
        public bool RequiresFullClear;
        public readonly List<int> Indices = new List<int>();
    }

    internal static class DynamicPathDataTracker
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<IntPtr, TouchedNativeIndices> Buffers =
            new Dictionary<IntPtr, TouchedNativeIndices>();
        private static readonly Dictionary<Map, List<IntPtr>> KeysByMap =
            new Dictionary<Map, List<IntPtr>>();

        public static TouchedNativeIndices Get<T>(Map map, NativeArray<T> array) where T : struct
        {
            IntPtr key = GetKey(array);
            lock (Sync)
            {
                TouchedNativeIndices buffer;
                if (!Buffers.TryGetValue(key, out buffer) || buffer.Owner != map)
                {
                    if (buffer != null && buffer.Owner != null
                        && KeysByMap.TryGetValue(buffer.Owner, out var oldKeys))
                    {
                        oldKeys.Remove(key);
                    }

                    buffer = new TouchedNativeIndices { Owner = map };
                    Buffers[key] = buffer;
                }

                if (!KeysByMap.TryGetValue(map, out var keys))
                {
                    keys = new List<IntPtr>();
                    KeysByMap[map] = keys;
                }
                if (!keys.Contains(key))
                    keys.Add(key);
                return buffer;
            }
        }

        public static void ReleaseMap(Map map)
        {
            if (map == null)
                return;

            lock (Sync)
            {
                if (!KeysByMap.TryGetValue(map, out var keys))
                    return;

                for (int i = 0; i < keys.Count; i++)
                {
                    if (Buffers.TryGetValue(keys[i], out var buffer) && buffer.Owner == map)
                        Buffers.Remove(keys[i]);
                }
                KeysByMap.Remove(map);
            }
        }

        public static void Clear<T>(TouchedNativeIndices buffer, NativeArray<T> array) where T : struct
        {
            if (buffer.RequiresFullClear)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = default(T);
                buffer.RequiresFullClear = false;
            }
            else
            {
                for (int i = 0; i < buffer.Indices.Count; i++)
                    array[buffer.Indices[i]] = default(T);
            }
            buffer.Indices.Clear();
        }

        private static unsafe IntPtr GetKey<T>(NativeArray<T> array) where T : struct
        {
            return (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(array);
        }
    }

    [HarmonyPatch(typeof(PathGridDoorsBlockedJob), nameof(PathGridDoorsBlockedJob.Execute))]
    internal static class Patch_PathGridDoorsBlockedJob
    {
        private static bool Prefix(PathGridDoorsBlockedJob __instance)
        {
            TouchedNativeIndices providerTouched = null;
            TouchedNativeIndices blockedTouched = null;
            if (__instance.map == null
                || !__instance.providerCost.IsCreated
                || !__instance.blocked.IsCreated)
            {
                return true;
            }

            try
            {
                providerTouched = DynamicPathDataTracker.Get(__instance.map, __instance.providerCost);
                blockedTouched = DynamicPathDataTracker.Get(__instance.map, __instance.blocked);
                if (!(OhYouAreSoFastMod.Settings?.sparseDynamicPathData ?? true))
                {
                    // Vanilla clears the complete arrays while the option is
                    // disabled. Force one complete clear if it is re-enabled.
                    providerTouched.RequiresFullClear = true;
                    blockedTouched.RequiresFullClear = true;
                    return true;
                }

                // A PathUniqueState is not reused until its previous job has
                // completed, so one lock is enough to protect the two buffers.
                lock (providerTouched)
                {
                    DynamicPathDataTracker.Clear(providerTouched, __instance.providerCost);
                    DynamicPathDataTracker.Clear(blockedTouched, __instance.blocked);
                    ExecuteWithoutFullClear(__instance, providerTouched, blockedTouched);
                }
                return false;
            }
            catch (Exception ex)
            {
                if (providerTouched != null)
                    providerTouched.RequiresFullClear = true;
                if (blockedTouched != null)
                    blockedTouched.RequiresFullClear = true;
                // Let vanilla execute after a failed optimization pass. It
                // performs the full clear and preserves the original fallback.
                Log.Error("[OhYouAreSoFast] Sparse path data pass failed: " + ex);
                return true;
            }
        }

        private static void ExecuteWithoutFullClear(
            PathGridDoorsBlockedJob job,
            TouchedNativeIndices providerTouched,
            TouchedNativeIndices blockedTouched)
        {
            foreach (Thing thing in job.doors)
            {
                var door = thing as Building_Door;
                if (door == null)
                    continue;

                foreach (IntVec3 cell in door.OccupiedRect())
                {
                    int index = job.map.cellIndices.CellToIndex(cell);
                    providerTouched.Indices.Add(index);
                    job.providerCost[index] =
                        PathUtility.GetDoorCost(door, job.traverseParams, job.pawn,
                            new PathFinderCostTuning?(job.tuning));
                }
            }

            if (job.pawn != null)
            {
                foreach (IPathFindCostProvider provider in job.providers)
                {
                    foreach (IntVec3 cell in provider.GetOccupiedRect())
                    {
                        int index = job.map.cellIndices.CellToIndex(cell);
                        providerTouched.Indices.Add(index);
                        job.providerCost[index] += provider.PathFindCostFor(job.pawn);
                    }
                }
            }

            if (!PawnUtility.ShouldCollideWithPawns(job.pawn))
                return;

            foreach (Pawn other in job.pawns)
            {
                if (other == job.pawn || !CanBlockEver(job, other))
                    continue;

                bool collideWithNonHostile = job.pawn.CurJob != null
                    && (job.pawn.CurJob.collideWithPawns
                        || job.pawn.CurJob.def.collideWithPawns
                        || job.pawn.jobs.curDriver.collideWithPawns);
                if (PawnUtility.PawnBlockedBy(job.pawn, other, false, collideWithNonHostile, true, false))
                {
                    int index = job.map.cellIndices.CellToIndex(other.Position);
                    blockedTouched.Indices.Add(index);
                    job.blocked[index] = true;
                }
            }
        }

        private static bool CanBlockEver(PathGridDoorsBlockedJob job, Pawn other)
        {
            if (other.Destroyed || other.Discarded || !other.Spawned)
                return false;

            int index = job.map.cellIndices.CellToIndex(other.Position);
            if (job.blocked[index])
                return false;

            if (!job.pawn.pather.BestPathHadPawnsInTheWayRecently())
            {
                int distanceFromStart = (other.Position - job.start).LengthManhattan;
                int distanceFromDestination = (other.Position - job.dest).LengthManhattan;
                if (UnityEngine.Mathf.Min(distanceFromStart, distanceFromDestination) > 32)
                    return false;
            }

            return PawnUtility.ShouldCollideWithPawns(other);
        }
    }

    [HarmonyPatch(typeof(PathFinder), "Dispose")]
    internal static class Patch_PathFinder_Dispose
    {
        private static void Postfix(Map ___map)
        {
            DynamicPathDataTracker.ReleaseMap(___map);
        }
    }
}
