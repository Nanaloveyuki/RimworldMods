using System.Collections.Generic;
using HungerAndHavoc.Api;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal interface RHAH_PawnCompatHook
    {
        bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate);

        bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason);
    }

    // 后注册优先 null 表示弃权
    internal static class RHAH_PawnCompat
    {
        static readonly List<Entry> hooks = new List<Entry>();
        static readonly RHAH_PawnCompatAdapter adapter = new RHAH_PawnCompatAdapter();

        internal static void Register(string packageId, RHAH_PawnCompatHook hook)
        {
            if (string.IsNullOrEmpty(packageId) || hook == null)
            {
                return;
            }

            hooks.Add(new Entry(packageId, hook));
        }

        internal static bool? TryQuery(Verse.Pawn pawn, RHAH_BehaviorGate gate)
        {
            IRHAH_Pawn snapshot = pawn == null ? null : RHAH_Api.Get(pawn);
            return QueryAllows(pawn, snapshot, gate);
        }

        internal static void EnsureAdapterRegistered()
        {
            RHAH_PawnBehaviors.Register(adapter);
        }

        internal static void ResetForTests()
        {
            hooks.Clear();
        }

        static bool? QueryAllows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
        {
            for (int i = hooks.Count - 1; i >= 0; i--)
            {
                bool? result = hooks[i].hook.Allows(pawn, snapshot, gate);
                if (result.HasValue)
                {
                    return result;
                }
            }

            return null;
        }

        static bool? QueryRelease(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
        {
            for (int i = hooks.Count - 1; i >= 0; i--)
            {
                bool? result = hooks[i].hook.ShouldReleaseToColony(pawn, snapshot, reason);
                if (result.HasValue)
                {
                    return result;
                }
            }

            return null;
        }

        sealed class Entry
        {
            internal readonly string packageId;
            internal readonly RHAH_PawnCompatHook hook;

            internal Entry(string packageId, RHAH_PawnCompatHook hook)
            {
                this.packageId = packageId;
                this.hook = hook;
            }
        }

        sealed class RHAH_PawnCompatAdapter : IRHAH_PawnBehavior
        {
            public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
            {
                return QueryAllows(pawn, snapshot, gate);
            }

            public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                return QueryRelease(pawn, snapshot, reason);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class RHAH_PawnCompatStartup
    {
        static RHAH_PawnCompatStartup()
        {
            // Verse 扫描公开静态构造 接到闸门查询链
            RHAH_PawnCompat.EnsureAdapterRegistered();
        }
    }
}
