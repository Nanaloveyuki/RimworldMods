using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_FamineRefugees : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) && MouseDisasterUtility.TryFindEntryCell((Map)parms.target, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell))
            {
                return false;
            }

            int count = Mathf.Clamp(Mathf.RoundToInt(parms.points / 200f) + 1, 1, 4);
            return GameComponent_MouseDisasterPawnGeneration.TryStartFamineRefugees(def, parms, map, cell, count);
        }
    }
}
