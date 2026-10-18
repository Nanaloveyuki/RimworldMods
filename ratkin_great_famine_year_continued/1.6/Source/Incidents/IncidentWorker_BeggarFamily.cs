using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public class IncidentWorker_BeggarFamily : IncidentWorker
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

            if (MouseDisasterUtility.TryFindFormerFaction(out Faction faction))
            {
                MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
            }

            Pawn mother = MouseDisasterUtility.GenerateMotherPawn(
                MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult,
                faction,
                DevelopmentalStage.Adult);
            if (mother == null)
            {
                return false;
            }

            GenSpawn.Spawn(mother, cell, map);
            MouseDisasterUtility.StripRatEggInventory(mother);
            mother.health.AddHediff(MouseDisasterDefOf.MouseDisaster_BeggarMother);
            IntVec3 gatherCell = map.Center;
            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(cell, map, mother, out gatherCell))
            {
                gatherCell = map.Center;
            }
            mother.jobs?.StartJob(MouseDisasterUtility.CreateGotoJob(gatherCell), JobCondition.InterruptForced);

            List<Pawn> pawns = new List<Pawn> { mother };
            List<Pawn> babies = new List<Pawn>();
            for (int i = 0; i < 4; i++)
            {
                Pawn baby = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, faction, DevelopmentalStage.Baby, 0.22f);
                if (baby == null)
                {
                    continue;
                }

                MouseDisasterUtility.SetBiologicalAgeYears(baby, MouseDisasterUtility.RandomRatEggAgeYears());
                GenSpawn.Spawn(baby, CellFinder.RandomClosewalkCellNear(cell, map, 4), map);
                MouseDisasterUtility.StripRatEggInventory(baby);
                MouseDisasterUtility.PrepareNonCaravanBabyPawn(baby, gatherCell);
                baby.health.AddHediff(MouseDisasterDefOf.MouseDisaster_BeggarChild);
                babies.Add(baby);
                pawns.Add(baby);
            }

            if (babies.Count == 0)
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(pawns);
                return false;
            }

            MouseDisasterUtility.LinkIncidentParentToChildren(mother, babies);
            MouseDisasterUtility.TryStartLeadYourPetMotherLeashes(mother, babies);
            LordJob_MouseDisasterBegForItems.MakeOrReplaceLord(
                faction,
                gatherCell,
                mother,
                ThingDefOf.MealSimple,
                1,
                map,
                pawns);
            MouseDisasterVisitorUtility.RegisterVisitors(pawns);
            if (!MouseDisasterVisitorUtility.SendVisitorChoiceLetter(def, parms, map, pawns))
            {
                SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, pawns);
            }
            return true;
        }
    }
}
