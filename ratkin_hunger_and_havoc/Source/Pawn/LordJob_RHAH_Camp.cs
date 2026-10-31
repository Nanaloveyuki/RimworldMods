using HungerAndHavoc.Api;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    // Verse 深存档需要无参构造和可见类型
    public sealed class LordJob_RHAH_Camp : LordJob_DefendPoint
    {
        public LordJob_RHAH_Camp()
        {
        }

        public LordJob_RHAH_Camp(IntVec3 center) : base(center, addFleeToil: false)
        {
        }

        public override bool AddFleeToil => false;

        public override bool ShouldRemovePawn(Verse.Pawn pawn, PawnLostCondition reason)
        {
            return reason != PawnLostCondition.Incapped;
        }

        public override StateGraph CreateGraph()
        {
            if (lord?.faction == null || lord.faction != RHAH_AttitudeFactions.Resolve(RHAH_Attitude.Hostile))
            {
                return base.CreateGraph();
            }

            StateGraph graph = new StateGraph();
            graph.AddToil(new LordToil_AssaultColony());
            return graph;
        }
    }
}
