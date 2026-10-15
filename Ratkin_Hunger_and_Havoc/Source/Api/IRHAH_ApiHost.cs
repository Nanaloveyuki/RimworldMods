using System;
using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Api
{
    // 实现程序集启动时绑定 其它模组不得实现此接口
    internal interface IRHAH_ApiHost
    {
        IRHAH_Pawn Get(Pawn pawn);

        bool IsRatkin(Pawn pawn);

        IRHAH_Pawn TryMarkOrigin(Pawn pawn, RHAH_PawnSeed seed);

        bool ReleaseToColony(Pawn pawn, RHAH_ReleaseReason reason);

        void SetLifecycle(Pawn pawn, RHAH_Lifecycle lifecycle);

        bool Allows(Pawn pawn, RHAH_BehaviorGate gate);

        void SetGate(Pawn pawn, RHAH_BehaviorGate gate, bool? allowed);

        void SetExtra(Pawn pawn, string key, string value);

        bool TryGetExtra(Pawn pawn, string key, out string value);

        void RegisterRatkinMatcher(Func<ThingDef, bool> matcher);
        bool IsOwnedHistory(string backstoryDefName);

        bool IsOwnedTrait(string traitDefName);

        bool TryGetHistory(string displayId, out string backstoryDefName);

        bool TryGetTrait(string displayId, out string traitDefName);

        void CopyHistoryIds(List<string> destination);

        void CopyTraitIds(List<string> destination);
    }
}
