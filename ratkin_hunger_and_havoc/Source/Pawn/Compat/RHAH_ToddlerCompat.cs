using HungerAndHavoc.Api;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    // Toddlers 幼崽闸门预留 不反射对方类型
    internal sealed class RHAH_ToddlerCompat : RHAH_PawnCompatHook
    {
        public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
        {
            return null;
        }

        public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
        {
            return null;
        }
    }
}
