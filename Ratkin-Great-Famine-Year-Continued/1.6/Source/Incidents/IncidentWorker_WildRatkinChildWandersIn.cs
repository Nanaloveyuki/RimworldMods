using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_WildRatkinChildWandersIn : IncidentWorker
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

            Map map = (Map)parms.target;
            IntVec3 cell;
            return MouseDisasterUtility.TryFindEntryCell(map, out cell);
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

            Pawn pawn = MouseDisasterUtility.GenerateWildPawn(MouseDisasterDefOf.MouseDisaster_WildRatkinChild, null, DevelopmentalStage.Child);
            if (pawn == null)
            {
                return false;
            }

            GenSpawn.Spawn(pawn, cell, map);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_WildChildWanderer);
            if (MouseDisasterVisitorChoicePolicy.ShouldUpgradeToVisitorChoiceControl(def.defName) &&
                MouseDisasterVisitorUtility.RegisterAndSendVisitorChoiceLetter(def, parms, map, Gen.YieldSingle(pawn)))
            {
                return true;
            }

            if (!MouseDisasterUtility.SendFoodGiveLetter(def, parms, map, Gen.YieldSingle(pawn)))
            {
                MouseDisasterUtility.SendIncidentLetter(def, parms, pawn);
            }
            return true;
        }
    }
}
