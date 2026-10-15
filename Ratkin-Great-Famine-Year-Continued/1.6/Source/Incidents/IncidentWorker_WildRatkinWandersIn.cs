using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_WildRatkinWandersIn : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
            {
                return false;
            }

            Map map = (Map)parms.target;
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout))
            {
                return false;
            }

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

            Pawn pawn = MouseDisasterUtility.GenerateWildPawn(MouseDisasterDefOf.MouseDisaster_WildRatkinAdult, null, DevelopmentalStage.Adult);
            if (pawn == null)
            {
                return false;
            }

            GenSpawn.Spawn(pawn, cell, map);
            pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_WildWanderer);
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
