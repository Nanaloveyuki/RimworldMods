using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    internal sealed class LordToil_RHAH_ArmedAssault : LordToil
    {
        public override bool ForceHighStoryDanger => true;
        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                bool swarm = RHAH_MouseSwarm.IsProvoked(pawn);
                bool assaults = !swarm && RHAH_ArmedDemand.Enabled && JobGiver_RHAH_Visitor.CanFight(pawn);
                // 不参战的人沿用 VisitorSeek 只为复用它的 think tree 站位就是原地
                pawn.mindState.duty = assaults ? new PawnDuty(DutyDefOf.AssaultColony) :
                    new PawnDuty(RHAH_DefOf.RHAH_VisitorSeek, pawn.Position, 3f);
            }
        }
    }
}
