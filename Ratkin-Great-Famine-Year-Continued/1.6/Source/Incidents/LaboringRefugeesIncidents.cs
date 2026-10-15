using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MouseDisaster
{

    public abstract class IncidentWorker_MouseDisasterLaboringRefugeesBase : IncidentWorker
    {
        protected abstract bool InfectsWithPlague { get; }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map &&
                   base.CanFireNowSub(parms) &&
                   ModsConfig.BiotechActive &&
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
                MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
            }

            int count = MouseDisasterUtility.LimitEventPawnCount(Rand.RangeInclusive(1, 3));
            List<Pawn> pawns = new List<Pawn>();
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = MouseDisasterPhase3Utility.CreatePregnantVisitor(faction, InfectsWithPlague);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entryCell, map, 5), map);
                MouseDisasterPhase3Utility.StartImmediateLabor(pawn);
                pawn.jobs?.StartJob(MouseDisasterUtility.CreateGotoJob(map.Center), JobCondition.InterruptForced);
                pawns.Add(pawn);
            }

            if (pawns.Count == 0)
            {
                return false;
            }

            MouseDisasterVisitorUtility.RegisterVisitors(pawns);
            if (!MouseDisasterVisitorUtility.SendVisitorChoiceLetter(def, parms, map, pawns))
            {
                SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, pawns);
            }
            return true;
        }
    }

    public class IncidentWorker_MouseDisasterLaboringRefugees : IncidentWorker_MouseDisasterLaboringRefugeesBase
    {
        protected override bool InfectsWithPlague => false;
    }

    public class IncidentWorker_MouseDisasterPlagueLaboringRefugees : IncidentWorker_MouseDisasterLaboringRefugeesBase
    {
        protected override bool InfectsWithPlague => true;
    }
}
