using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class IncidentWorker_ShatteredMother : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) && Find.Storyteller.difficulty.ChildrenAllowed && MouseDisasterUtility.TryFindEntryCell((Map)parms.target, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 entryCell))
            {
                return false;
            }

            MouseDisasterUtility.TryFindFormerFaction(out Faction formerFaction);
            if (formerFaction != null)
            {
                MouseDisasterUtility.MakeFactionNeutralToPlayer(formerFaction, force: true);
            }

            int childCount = Mathf.Clamp(Mathf.RoundToInt(parms.points / 150f) + 1, 1, 10);
            if (!MouseDisasterUtility.TryFindFarEdgeCell(map, entryCell, out IntVec3 exitCell))
            {
                exitCell = map.Center;
            }

            Pawn mother = MouseDisasterUtility.GenerateMotherPawn(
                MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult,
                formerFaction,
                DevelopmentalStage.Adult);
            if (mother == null)
            {
                return false;
            }

            GenSpawn.Spawn(mother, entryCell, map);
            MouseDisasterUtility.RemoveBodyPartByName(mother, "Ear", "left");
            MouseDisasterUtility.RemoveBodyPartByName(mother, "Leg", "right");
            MouseDisasterUtility.RemoveBodyPartByName(mother, "RK_RatTail");
            HealthUtility.DamageUntilDowned(mother, false);
            Hediff bloodLoss = mother.health.GetOrAddHediff(HediffDefOf.BloodLoss);
            bloodLoss.Severity = Mathf.Max(bloodLoss.Severity, 0.7f);
            mother.health.AddHediff(MouseDisasterDefOf.MouseDisaster_ShatteredMother_Dying);

            List<Pawn> children = new List<Pawn>();
            for (int index = 0; index < childCount; index++)
            {
                Pawn child = MouseDisasterUtility.GenerateWildPawn(MouseDisasterDefOf.MouseDisaster_WildRatkinChild, null, DevelopmentalStage.Child);
                if (child == null)
                {
                    continue;
                }

                GenSpawn.Spawn(child, CellFinder.RandomClosewalkCellNear(entryCell, map, 5), map);
                MouseDisasterUtility.StripRatEggInventory(child);
                child.health.AddHediff(MouseDisasterDefOf.MouseDisaster_ShatteredMother_Children);
                children.Add(child);
            }

            if (children.Count == 0)
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(new[] { mother });
                return false;
            }

            MouseDisasterUtility.LinkIncidentParentToChildren(mother, children);
            MouseDisasterUtility.TryStartLeadYourPetMotherLeashes(mother, children);
            MouseDisasterUtility.MakeTravelAndExitLord(map, children, exitCell, includeBabiesInExit: false);

            List<Thing> lookTargets = new List<Thing> { mother };
            lookTargets.AddRange(children);
            List<Pawn> pawns = new List<Pawn> { mother };
            pawns.AddRange(children);
            MouseDisasterVisitorUtility.RegisterVisitors(pawns);
            if (!MouseDisasterVisitorUtility.SendVisitorChoiceLetter(def, parms, map, pawns))
            {
                SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, lookTargets);
            }
            return true;
        }
    }
}
