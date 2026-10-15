using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    public abstract class IncidentWorker_MouseDisasterAirdropMistakeBase : IncidentWorker
    {
        protected abstract bool InfectsWithPlague { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map &&
                   base.CanFireNowSub(parms) &&
                   MouseDisasterUtility.TryFindEntryCell(map, out _) &&
                   Find.Storyteller.difficulty.ChildrenAllowed;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 entryCell))
            {
                return false;
            }

            Faction faction = MouseDisasterPhase3Utility.ResolveVisitorFaction();
            int count = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 4, 14, 80f);
            return GameComponent_MouseDisasterPawnGeneration.TryStartAirdropMistake(def, parms, map, entryCell, faction, count, InfectsWithPlague);
        }
    }

    public class IncidentWorker_MouseDisasterAirdropMistake : IncidentWorker_MouseDisasterAirdropMistakeBase
    {
        protected override bool InfectsWithPlague => false;
    }

    public class IncidentWorker_MouseDisasterPlagueAirdropMistake : IncidentWorker_MouseDisasterAirdropMistakeBase
    {
        protected override bool InfectsWithPlague => true;
    }
}
