using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{

    public abstract class IncidentWorker_MouseDisasterMisguidedKinshipBase : IncidentWorker
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

            List<Pawn> babies = new List<Pawn>();
            for (int i = 0; i < Rand.RangeInclusive(2, 5); i++)
            {
                Pawn pawn = MouseDisasterPhase3Utility.CreateMisguidedKinshipPawn(InfectsWithPlague);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(entryCell, map, 3), map);
                babies.Add(pawn);
            }

            if (babies.Count == 0)
            {
                return false;
            }

            ChoiceLetter_MouseDisasterMisguidedKinship letter =
                LetterMaker.MakeLetter(def.letterLabel, def.letterText, DefDatabase<LetterDef>.GetNamed("MouseDisaster_MisguidedKinshipLetter"), babies) as ChoiceLetter_MouseDisasterMisguidedKinship;
            if (letter == null)
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(babies);
                return false;
            }

            letter.babies = babies;
            letter.map = map;
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }
    }

    public class IncidentWorker_MouseDisasterMisguidedKinship : IncidentWorker_MouseDisasterMisguidedKinshipBase
    {
        protected override bool InfectsWithPlague => false;
    }

    public class IncidentWorker_MouseDisasterPlagueMisguidedKinship : IncidentWorker_MouseDisasterMisguidedKinshipBase
    {
        protected override bool InfectsWithPlague => true;
    }
}
