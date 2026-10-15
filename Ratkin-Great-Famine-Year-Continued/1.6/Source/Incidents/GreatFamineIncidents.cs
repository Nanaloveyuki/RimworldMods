using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{

    public abstract class IncidentWorker_MouseDisasterGreatFamineBase : IncidentWorker
    {
        protected abstract bool InfectsWithPlague { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map &&
                   base.CanFireNowSub(parms) &&
                   Find.Storyteller.difficulty.ChildrenAllowed &&
                   MouseDisasterUtility.TryFindEntryCell(map, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 entryCell))
            {
                return false;
            }

            Faction faction = MouseDisasterPhase3Utility.ResolveVisitorFaction();
            if (faction != null)
            {
                MouseDisasterUtility.MakeFactionHostileToPlayer(faction, explicitDriveAway: true);
            }

            int count = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 8, 28, 55f);
            return GameComponent_MouseDisasterPawnGeneration.TryStartGreatFamine(def, parms, map, entryCell, faction, count, InfectsWithPlague);
        }
    }

    public class IncidentWorker_MouseDisasterGreatFamine : IncidentWorker_MouseDisasterGreatFamineBase
    {
        protected override bool InfectsWithPlague => false;
    }

    public class IncidentWorker_MouseDisasterPlagueGreatFamine : IncidentWorker_MouseDisasterGreatFamineBase
    {
        protected override bool InfectsWithPlague => true;
    }
}
