using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{

    public class IncidentWorker_MouseDisasterPlagueWanderers : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && base.CanFireNowSub(parms) && MouseDisasterUtility.TryFindEntryCell(map, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell))
            {
                return false;
            }

            int count = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 2, 8, 90f);
            return GameComponent_MouseDisasterPawnGeneration.TryStartWildGroup(def, parms, map, cell, count, infectsWithPlague: true);
        }
    }

    public class IncidentWorker_MouseDisasterPlagueAbandoned : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map &&
                   base.CanFireNowSub(parms) &&
                   Find.Storyteller.difficulty.ChildrenAllowed &&
                   MouseDisasterUtility.TryFindEntryCell(map, out _) &&
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

            Pawn mother = MouseDisasterUtility.GenerateFactionRatkinPawn(
                MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult,
                faction,
                DevelopmentalStage.Adult,
                0.35f,
                allowViolenceDisabledTraits: false,
                fixedGender: Gender.Female);
            if (mother == null)
            {
                return false;
            }

            GenSpawn.Spawn(mother, entryCell, map);
            mother.jobs.StartJob(MouseDisasterUtility.CreateGotoJob(foodCell), JobCondition.InterruptForced);
            MouseDisasterPlagueUtility.InfectWithPlague(mother, Rand.Chance(0.9f) ? 0.82f : 0.42f);

            List<Pawn> babies = new List<Pawn>();
            for (int i = 0; i < 3; i++)
            {
                Pawn baby = MouseDisasterUtility.GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, faction, DevelopmentalStage.Baby, 0.22f);
                if (baby == null)
                {
                    continue;
                }

                MouseDisasterUtility.SetBiologicalAgeYears(baby, 15f / 60f);
                GenSpawn.Spawn(baby, CellFinder.RandomClosewalkCellNear(entryCell, map, 4), map);
                MouseDisasterUtility.PrepareNonCaravanBabyPawn(baby, foodCell);
                if (MouseDisasterUtility.IsLeadYourPetEnabled && !baby.Downed &&
                    !baby.Position.InHorDistOf(foodCell, MouseDisasterAbandonedDeliveryPolicy.ChildDropoffRadius))
                {
                    baby.jobs.StartJob(MouseDisasterUtility.CreateGotoJob(foodCell), JobCondition.InterruptForced);
                }
                MouseDisasterPlagueUtility.InfectWithPlague(baby, 0.25f);
                babies.Add(baby);
            }

            if (babies.Count == 0)
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(new[] { mother });
                return false;
            }

            MouseDisasterUtility.LinkIncidentParentToChildren(mother, babies);
            MouseDisasterUtility.TryStartLeadYourPetAbandonedDropoff(mother, babies, foodCell);
            MouseDisasterGuanyinTuUtility.ApplyN004StartingState(mother, babies);
            List<Thing> targets = new List<Thing> { mother };
            targets.AddRange(babies);
            List<Pawn> pawns = new List<Pawn> { mother };
            pawns.AddRange(babies);

            GameComponent_MouseDisasterNarrative narrative = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
            MouseDisasterN004Record record = narrative?.RegisterN004(mother, babies, map, foodCell);
            ChoiceLetter_MouseDisasterAbandonedChildren choiceLetter = record == null
                ? null
                : LetterMaker.MakeLetter(
                    "MouseDisaster_N004_Label".Translate(),
                    "MouseDisaster_N004_EntryText".Translate(),
                    MouseDisasterDefOf.MouseDisaster_AbandonedChildrenLetter,
                    pawns) as ChoiceLetter_MouseDisasterAbandonedChildren;
            if (choiceLetter != null)
            {
                choiceLetter.mother = mother;
                choiceLetter.children = babies;
                choiceLetter.map = map;
                choiceLetter.foodCell = foodCell;
                Find.LetterStack.ReceiveLetter(choiceLetter, null);
                return true;
            }

            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, targets);
            return true;
        }
    }

    public class IncidentWorker_MouseDisasterPlagueTraderCaravan : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map &&
                   base.CanFireNowSub(parms) &&
                   Find.Storyteller.difficulty.ChildrenAllowed &&
                   MouseDisasterUtility.TryFindEntryCell(map, out _) &&
                   MouseDisasterUtility.TryFindFormerFaction(out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell) || !MouseDisasterUtility.TryFindFormerFaction(out Faction faction))
            {
                return false;
            }

            MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
            MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(map, faction);
            int saleChildren = Rand.RangeInclusive(5, 30);
            return GameComponent_MouseDisasterPawnGeneration.TryStartTraderCaravan(def, parms, map, cell, faction, saleChildren, infectsWithPlague: true);
        }
    }

    public class IncidentWorker_MouseDisasterPlaguePassersby : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map && base.CanFireNowSub(parms) && MouseDisasterUtility.TryFindEntryCell(map, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 entryCell))
            {
                return false;
            }

            if (!MouseDisasterUtility.TryFindFarEdgeCell(map, entryCell, out IntVec3 exitCell))
            {
                exitCell = map.Center;
            }

            int total = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 4, 12, 85f);
            int children = Find.Storyteller.difficulty.ChildrenAllowed ? Mathf.Clamp(Mathf.RoundToInt(total * 0.3f), 0, total - 1) : 0;
            int adults = Mathf.Max(1, total - children);
            Faction faction = MouseDisasterPhase3Utility.ResolveVisitorFaction();
            if (faction != null)
            {
                MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
            }

            return GameComponent_MouseDisasterPawnGeneration.TryStartTravelerGroup(
                def,
                parms,
                map,
                entryCell,
                exitCell,
                faction,
                adults,
                children,
                infectsWithPlague: true);
        }
    }

    public class IncidentWorker_MouseDisasterPlagueRefugees : IncidentWorker
    {
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

            int count = Mathf.Clamp(Mathf.RoundToInt(parms.points / 200f) + 1, 1, 4);
            return GameComponent_MouseDisasterPawnGeneration.TryStartFamineRefugees(def, parms, map, cell, count, infectsWithPlague: true);
        }
    }

    public class IncidentWorker_MouseDisasterPlagueOrphan : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return parms.target is Map map && base.CanFireNowSub(parms) && MouseDisasterUtility.TryFindEntryCell(map, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!MouseDisasterUtility.TryFindEntryCell(map, out IntVec3 cell))
            {
                return false;
            }

            Pawn pawn = MouseDisasterUtility.GenerateWildPawn(MouseDisasterDefOf.MouseDisaster_WildRatkinChild, null, DevelopmentalStage.Child);
            if (pawn == null)
            {
                return false;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(cell, map, 4), map);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            MouseDisasterPlagueUtility.InfectWithPlague(pawn, 0.5f);
            if (MouseDisasterVisitorChoicePolicy.ShouldUpgradeToVisitorChoiceControl(def.defName) &&
                MouseDisasterVisitorUtility.RegisterAndSendVisitorChoiceLetter(def, parms, map, Gen.YieldSingle(pawn)))
            {
                return true;
            }

            if (!MouseDisasterUtility.SendFoodGiveLetter(def, parms, map, Gen.YieldSingle(pawn)))
            {
                SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, pawn);
            }
            return true;
        }
    }

    public class IncidentWorker_MouseDisasterPlagueBeggarGroup : IncidentWorker
    {
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

            int total = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 3, 20, 85f);
            int children = Find.Storyteller.difficulty.ChildrenAllowed ? Mathf.Clamp(Mathf.RoundToInt(total * 0.4f), 0, total - 1) : 0;
            int adults = Mathf.Max(1, total - children);
            return GameComponent_MouseDisasterPawnGeneration.TryStartBeggarGroup(def, parms, map, cell, null, adults, children, infectsWithPlague: true);
        }
    }

    public class IncidentWorker_MouseDisasterPlagueThiefGroup : IncidentWorker
    {
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

            int count = MouseDisasterUtility.CalculateEscalatingGroupCount(parms.points, 3, 18, 90f);
            return GameComponent_MouseDisasterPawnGeneration.TryStartThiefGroup(def, parms, map, cell, null, count, childOnly: false, infectsWithPlague: true);
        }
    }
}
