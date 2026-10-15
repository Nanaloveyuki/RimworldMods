using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MouseDisaster
{

    public abstract class IncidentWorker_MouseDisasterStrongSiegeBase : IncidentWorker
    {
        protected abstract bool InfectsWithPlague { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map &&
                   base.CanFireNowSub(parms) &&
                   MouseDisasterUtility.TryFindEntryCell(map, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell))
            {
                return false;
            }

            Faction faction = MouseDisasterPhase3Utility.ResolveVisitorFaction();
            int count = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 4, 16, 80f);
            return GameComponent_MouseDisasterPawnGeneration.TryStartStrongSiegeGroup(def, parms, map, cell, faction, count, InfectsWithPlague);
        }
    }

    public class IncidentWorker_MouseDisasterStrongSiege : IncidentWorker_MouseDisasterStrongSiegeBase
    {
        protected override bool InfectsWithPlague => false;
    }

    public class IncidentWorker_MouseDisasterPlagueStrongSiege : IncidentWorker_MouseDisasterStrongSiegeBase
    {
        protected override bool InfectsWithPlague => true;
    }
}
