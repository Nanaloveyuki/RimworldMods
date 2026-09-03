using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace OhYouAreSoFast
{
    [HarmonyPatch(
        typeof(ThinkNode_PrioritySorter),
        nameof(ThinkNode_PrioritySorter.TryIssueJobPackage))]
    internal static class Patch_ThinkNode_PrioritySorter
    {
        private sealed class Scratch
        {
            public readonly List<ThinkNode> Nodes = new List<ThinkNode>();
            public readonly List<bool> Active = new List<bool>();
        }

        private sealed class ScratchPool
        {
            public readonly Stack<Scratch> Available = new Stack<Scratch>();
        }

        private static readonly ConditionalWeakTable<ThinkNode_PrioritySorter, ScratchPool> Pools =
            new ConditionalWeakTable<ThinkNode_PrioritySorter, ScratchPool>();

        private static bool Prefix(
            ThinkNode_PrioritySorter __instance,
            Pawn pawn,
            JobIssueParams jobParams,
            ref ThinkResult __result)
        {
            ScratchPool pool = Pools.GetOrCreateValue(__instance);
            Scratch scratch = pool.Available.Count > 0
                ? pool.Available.Pop()
                : new Scratch();
            try
            {
                __result = Evaluate(__instance, pawn, jobParams, scratch);
                return false;
            }
            finally
            {
                scratch.Nodes.Clear();
                scratch.Active.Clear();
                pool.Available.Push(scratch);
            }
        }

        private static ThinkResult Evaluate(
            ThinkNode_PrioritySorter sorter,
            Pawn pawn,
            JobIssueParams jobParams,
            Scratch scratch)
        {
            scratch.Nodes.Clear();
            scratch.Active.Clear();
            int count = sorter.subNodes.Count;
            if (scratch.Nodes.Capacity < count)
                scratch.Nodes.Capacity = count;
            if (scratch.Active.Capacity < count)
                scratch.Active.Capacity = count;
            for (int i = 0; i < count; i++)
            {
                int insertIndex = Rand.Range(0, scratch.Nodes.Count - 1);
                scratch.Nodes.Insert(insertIndex, sorter.subNodes[i]);
                scratch.Active.Insert(insertIndex, true);
            }

            int activeCount = count;
            while (activeCount > 0)
            {
                float bestPriority = 0f;
                int bestIndex = -1;
                for (int i = 0; i < scratch.Nodes.Count; i++)
                {
                    if (!scratch.Active[i])
                        continue;

                    float priority = 0f;
                    try
                    {
                        priority = scratch.Nodes[i].GetPriority(pawn);
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error(
                            "Exception in " + sorter.GetType() + " GetPriority: " + ex);
                    }

                    if (priority > 0f
                        && priority >= sorter.minPriority
                        && priority > bestPriority)
                    {
                        bestPriority = priority;
                        bestIndex = i;
                    }
                }

                if (bestIndex == -1)
                    break;

                ThinkResult result = ThinkResult.NoJob;
                try
                {
                    result = scratch.Nodes[bestIndex].TryIssueJobPackage(pawn, jobParams);
                }
                catch (System.Exception ex)
                {
                    Log.Error(
                        "Exception in " + sorter.GetType() + " TryIssueJobPackage: " + ex);
                }

                if (result.IsValid)
                    return result;

                // Keep the randomized order intact so equal priorities retain
                // the same first-node tie break as the vanilla RemoveAt path.
                scratch.Active[bestIndex] = false;
                activeCount--;
            }

            return ThinkResult.NoJob;
        }
    }
}
