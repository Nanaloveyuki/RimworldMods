using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class IncidentWorker_AbandonedRatkinChildren : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) &&
                   Find.Storyteller.difficulty.ChildrenAllowed &&
                   MouseDisasterUtility.TryFindEntryCell((Map)parms.target, out _) &&
                   MouseDisasterUtility.TryFindFormerFaction(out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 entryCell) || !MouseDisasterUtility.TryFindFormerFaction(out Faction faction))
            {
                return false;
            }

            MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);

            MouseDisasterUtility.TryFindAbandonedDeliveryFoodCell(map, entryCell, out IntVec3 foodCell);
            if (!foodCell.IsValid || !map.reachability.CanReach(entryCell, foodCell, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)))
            {
                foodCell = map.Center;
            }
            int childCount = Mathf.Clamp(Mathf.RoundToInt(parms.points / 120f) + 1, 1, 10);

            List<Pawn> children = new List<Pawn>();
            for (int index = 0; index < childCount; index++)
            {
                Pawn child = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, faction, DevelopmentalStage.Baby, 0.28f);
                if (child == null)
                {
                    continue;
                }

                GenSpawn.Spawn(child, CellFinder.RandomClosewalkCellNear(entryCell, map, 4), map);
                MouseDisasterUtility.StripRatEggInventory(child);
                MouseDisasterUtility.PrepareNonCaravanBabyPawn(child, foodCell);
                child.health.AddHediff(MouseDisasterDefOf.MouseDisaster_AbandonedEgg);
                if (MouseDisasterUtility.IsLeadYourPetEnabled && !child.Downed &&
                    !child.Position.InHorDistOf(foodCell, MouseDisasterAbandonedDeliveryPolicy.ChildDropoffRadius))
                {
                    child.jobs.StartJob(MouseDisasterUtility.CreateGotoJob(foodCell), JobCondition.InterruptForced);
                }
                children.Add(child);
            }

            Pawn adult = MouseDisasterUtility.GenerateFactionRatkinPawn(
                MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult,
                faction,
                DevelopmentalStage.Adult,
                0.35f,
                allowViolenceDisabledTraits: false,
                fixedGender: Gender.Female);
            if (adult == null || children.Count == 0)
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(children);
                MouseDisasterUtility.DestroyFailedIncidentPawns(new[] { adult });
                return false;
            }

            GenSpawn.Spawn(adult, entryCell, map);
            adult.health.AddHediff(MouseDisasterDefOf.MouseDisaster_AbandoningMother);
            MouseDisasterUtility.LinkIncidentParentToChildren(adult, children);
            MouseDisasterUtility.TryStartLeadYourPetAbandonedDropoff(adult, children, foodCell);
            MouseDisasterUtility.RegisterAbandonedDelivery(adult, children, foodCell);

            List<Thing> lookTargets = new List<Thing> { adult };
            lookTargets.AddRange(children);
            List<Pawn> pawns = new List<Pawn> { adult };
            pawns.AddRange(children);
            if (MouseDisasterAbandonedDeliveryPolicy.ShouldRegisterAsVisitorChoiceTargets())
            {
                MouseDisasterVisitorUtility.RegisterVisitors(pawns);
            }

            if (!MouseDisasterAbandonedDeliveryPolicy.ShouldRegisterAsVisitorChoiceTargets() ||
                !MouseDisasterVisitorUtility.SendVisitorChoiceLetter(def, parms, map, pawns))
            {
                SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, lookTargets);
            }
            return true;
        }
    }
}
