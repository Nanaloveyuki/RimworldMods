using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_WildRatkinGroupWandersIn : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            Map map = (Map)parms.target;
            IntVec3 cell;
            return MouseDisasterUtility.TryFindEntryCell(map, out cell);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            IntVec3 cell;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out cell))
            {
                return false;
            }

            int count = MouseDisasterUtility.CalculateGroupCount(parms.points);
            return GameComponent_MouseDisasterPawnGeneration.TryStartWildGroup(def, parms, map, cell, count);
        }
    }
}
