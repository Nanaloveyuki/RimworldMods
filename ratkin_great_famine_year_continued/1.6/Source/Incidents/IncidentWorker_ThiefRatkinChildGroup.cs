using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_ThiefRatkinChildGroup : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            if (!Find.Storyteller.difficulty.ChildrenAllowed)
            {
                return false;
            }

            IntVec3 cell;
            return MouseDisasterUtility.TryFindEntryCell((Map)parms.target, out cell);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!Find.Storyteller.difficulty.ChildrenAllowed)
            {
                return false;
            }

            Map map = (Map)parms.target;
            IntVec3 cell;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out cell))
            {
                return false;
            }

            int count = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 3, 20, 75f);
            return GameComponent_MouseDisasterPawnGeneration.TryStartThiefGroup(def, parms, map, cell, null, count, childOnly: true);
        }
    }
}
