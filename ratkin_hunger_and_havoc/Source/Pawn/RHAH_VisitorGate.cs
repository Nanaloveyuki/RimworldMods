using HungerAndHavoc.Api;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_VisitorGate
    {
        internal static bool IsVisitor(Verse.Pawn pawn)
        {
            return RHAH_Api.IsVisitor(pawn);
        }

        internal static bool Allows(Verse.Pawn pawn, RHAH_BehaviorGate gate)
        {
            if (!IsVisitor(pawn))
            {
                return false;
            }

            return RHAH_Api.Allows(pawn, gate);
        }
    }
}
