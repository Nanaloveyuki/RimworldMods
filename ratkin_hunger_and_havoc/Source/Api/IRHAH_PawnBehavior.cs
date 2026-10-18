using Verse;

namespace HungerAndHavoc.Api
{
    public interface IRHAH_PawnBehavior
    {
        bool? Allows(Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate);

        bool? ShouldReleaseToColony(Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason);
    }
}
