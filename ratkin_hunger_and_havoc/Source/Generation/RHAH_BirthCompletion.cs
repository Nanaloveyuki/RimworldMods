using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace HungerAndHavoc.Generation
{
    // 原版先释放父母登记再分娩 额外生产必须等最外层产程移除提交
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
    internal static class RHAH_BirthCompletion
    {
        [ThreadStatic] static Scope active;

        internal sealed class Scope
        {
            internal readonly Hediff Labor;
            internal readonly Scope Previous;
            internal readonly List<Action> Captured = new List<Action>();
            internal readonly List<Action> Committed = new List<Action>();

            internal Scope(Hediff labor, Scope previous)
            {
                Labor = labor;
                Previous = previous;
            }
        }

        static void Prefix(Hediff hediff, out Scope __state)
        {
            __state = hediff is Hediff_LaborPushing ? new Scope(hediff, active) : null;
            if (__state != null) active = __state;
        }

        internal static void CompleteAfterRemoval(Thing birther, Action completion)
        {
            Scope scope = active;
            if (scope == null)
            {
                completion();
                return;
            }
            while (scope.Previous != null && scope.Labor.pawn != birther) scope = scope.Previous;
            if (scope.Labor.pawn == birther) scope.Captured.Add(completion);
            else active.Committed.Add(completion);
        }

        static Exception Finalizer(Pawn_HealthTracker __instance, Scope __state, Exception __exception)
        {
            if (__state == null) return __exception;
            if (ReferenceEquals(active, __state)) active = __state.Previous;
            if (!__instance.hediffSet.hediffs.Contains(__state.Labor))
                __state.Committed.AddRange(__state.Captured);
            if (__state.Previous != null)
            {
                __state.Previous.Committed.AddRange(__state.Committed);
                return __exception;
            }
            for (int i = 0; i < __state.Committed.Count; i++)
            {
                try
                {
                    __state.Committed[i]();
                }
                catch (Exception exception)
                {
                    Log.Error("[RHAH] Additional birth failed after labor removal; this birth will not be retried: " + exception);
                    __exception = __exception ?? exception;
                }
            }
            return __exception;
        }
    }
}
