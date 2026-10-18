using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    public class LordJob_RHAH_Intercept : LordJob
    {
        IntVec3 exitCell = IntVec3.Invalid;

        public LordJob_RHAH_Intercept()
        {
        }

        public LordJob_RHAH_Intercept(IntVec3 exitCell)
        {
            this.exitCell = exitCell;
        }

        public override bool ShouldRemovePawn(Verse.Pawn pawn, PawnLostCondition reason)
        {
            return reason != PawnLostCondition.Incapped;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            graph.AddToil(new LordToil_RHAH_InterceptCrossing(exitCell));
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref exitCell, "exitCell", IntVec3.Invalid);
        }
    }

    internal sealed class LordToil_RHAH_InterceptCrossing : LordToil
    {
        readonly IntVec3 exitCell;

        internal LordToil_RHAH_InterceptCrossing(IntVec3 exitCell)
        {
            this.exitCell = exitCell;
        }

        public override bool AllowSatisfyLongNeeds => false;

        public override void LordToilTick()
        {
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                UpdateAllDuties();
            }
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Verse.Pawn pawn = lord.ownedPawns[i];
                // 出生边缘不能接离图 Job 到达对边后才允许离场
                bool arrived = pawn.Position.InHorDistOf(exitCell, 10f);
                DutyDef def = arrived ? DutyDefOf.ExitMapNearDutyTarget : DutyDefOf.TravelOrWait;
                if (pawn.mindState.duty?.def == def && pawn.mindState.duty.focus.Cell == exitCell)
                {
                    continue;
                }

                PawnDuty duty = new PawnDuty(def, exitCell, arrived ? 1f : -1f);
                duty.locomotion = LocomotionUrgency.Jog;
                duty.maxDanger = Danger.Deadly;
                pawn.mindState.duty = duty;
            }
        }
    }
}
