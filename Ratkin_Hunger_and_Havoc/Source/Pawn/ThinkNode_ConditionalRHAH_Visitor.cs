using HungerAndHavoc.Api;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class ThinkNode_ConditionalRHAH_Visitor : ThinkNode_Conditional
    {
        protected override bool Satisfied(Verse.Pawn pawn)
        {
            return RHAH_BatchAttitude.CanOrderLeave(pawn);
        }
    }
}
