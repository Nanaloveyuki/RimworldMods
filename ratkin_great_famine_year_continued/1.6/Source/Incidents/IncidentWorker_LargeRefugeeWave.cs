using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_LargeRefugeeWave : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            return MouseDisasterUtility.TryFindEntryCell((Map)parms.target, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell))
            {
                return false;
            }

            int count = MouseDisasterUtility.CalculateEscalatingFixedCount(parms.points, 10, 50, 50f);
            int children = Find.Storyteller.difficulty.ChildrenAllowed ? Mathf.Clamp(Mathf.RoundToInt(count * 0.35f), 0, count - 1) : 0;
            int adults = Mathf.Max(1, count - children);
            return GameComponent_MouseDisasterPawnGeneration.TryStartLargeRefugeeWave(def, parms, map, cell, adults, children);
        }
    }
}
