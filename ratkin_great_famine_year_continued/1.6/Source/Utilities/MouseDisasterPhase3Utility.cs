using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    internal static class MouseDisasterPhase3Utility
    {
        private static readonly string[][] PreferredRetaliationTraitKeywords =
        {
            new[] { "greed", "greedy", "贪" },
            new[] { "explosive", "blast", "爆" },
            new[] { "gas", "toxic", "毒" },
            new[] { "noble", "royal", "贵族" },
            new[] { "burn", "fire", "flame", "燃" }
        };

        public static Faction ResolveVisitorFaction()
        {
            MouseDisasterUtility.TryFindFormerFaction(out Faction faction);
            return faction;
        }

        public static Pawn CreatePregnantVisitor(Faction faction, bool infect)
        {
            Pawn pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(
                MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult,
                faction,
                DevelopmentalStage.Adult,
                0.24f,
                allowViolenceDisabledTraits: false,
                fixedGender: Gender.Female);
            if (pawn == null || !ModsConfig.BiotechActive || pawn.gender != Gender.Female)
            {
                return pawn;
            }

            Hediff_Pregnant pregnancy = (Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, pawn);
            pregnancy.Severity = Rand.Range(0.72f, 0.94f);
            pregnancy.SetParents(pawn, null, PregnancyUtility.GetInheritedGeneSet(null, pawn));
            pawn.health.AddHediff(pregnancy);
            if (infect)
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }

            return pawn;
        }

        public static void StartImmediateLabor(Pawn pawn)
        {
            Hediff_Pregnant pregnancy = PregnancyUtility.GetPregnancyHediff(pawn) as Hediff_Pregnant;
            if (pregnancy == null)
            {
                return;
            }

            pregnancy.StartLabor();
            pawn.health.RemoveHediff(pregnancy);
        }

        public static Pawn CreateRatEggPawn(Faction faction, bool babyStage, bool thiefLike, bool pureNegative, bool infect, float foodLevel = 0.12f)
        {
            Pawn pawn = thiefLike
                ? MouseDisasterUtility.GenerateThiefPawn(MouseDisasterDefOf.MouseDisaster_ThiefRatkinChild, faction, babyStage ? DevelopmentalStage.Baby : DevelopmentalStage.Child)
                : MouseDisasterUtility.GenerateBeggarPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, faction, babyStage ? DevelopmentalStage.Baby : DevelopmentalStage.Child);
            if (pawn == null)
            {
                return null;
            }

            MouseDisasterUtility.SetBiologicalAgeYears(pawn, babyStage
                ? MouseDisasterUtility.RandomRatEggAgeYears(2.8f)
                : Rand.Range(3.1f, 6.8f));
            MouseDisasterUtility.StripRatEggInventory(pawn);
            if (pureNegative)
            {
                MouseDisasterUtility.MakeRatEggPureNegative(pawn, babyStage ? 2 : 3, PreferredRetaliationTraitKeywords);
            }

            if (infect)
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }

            if (pawn.needs?.food != null)
            {
                pawn.needs.food.CurLevelPercentage = Mathf.Clamp01(foodLevel);
            }

            return pawn;
        }

        public static Pawn CreateMisguidedKinshipPawn(bool infect)
        {
            Pawn pawn = MouseDisasterUtility.GenerateWildPawn(MouseDisasterDefOf.MouseDisaster_WildRatkinChild, null, DevelopmentalStage.Child);
            if (pawn == null)
            {
                return null;
            }

            MouseDisasterUtility.MakeRatEggPureNegative(pawn, 3, PreferredRetaliationTraitKeywords);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            if (infect)
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }

            if (pawn.needs?.food != null)
            {
                pawn.needs.food.CurLevelPercentage = 0.18f;
            }

            return pawn;
        }

        public static Pawn CreateAnyAgeFaminePawn(Faction faction, bool infect)
        {
            float roll = Rand.Value;
            DevelopmentalStage stage = roll < 0.2f ? DevelopmentalStage.Baby : (roll < 0.65f ? DevelopmentalStage.Child : DevelopmentalStage.Adult);
            PawnKindDef kindDef = stage.Adult() ? MouseDisasterDefOf.MouseDisaster_ThiefRatkinAdult : MouseDisasterDefOf.MouseDisaster_ThiefRatkinChild;
            Pawn pawn = MouseDisasterUtility.GenerateThiefPawn(kindDef, faction, stage);
            if (pawn == null)
            {
                return null;
            }

            if (infect)
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }

            if (pawn.needs?.food != null)
            {
                pawn.needs.food.CurLevelPercentage = 0.04f;
            }

            return pawn;
        }

        public static Pawn CreatePlagueRevengePawn(Faction faction)
        {
            Pawn pawn = CreateAnyAgeFaminePawn(faction, infect: true);
            if (pawn == null)
            {
                return null;
            }

            Hediff plague = pawn.health?.hediffSet?.GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_Plague);
            if (plague != null)
            {
                plague.Severity = Mathf.Max(plague.Severity, 0.5f);
            }

            Hediff bloodLoss = pawn.health.GetOrAddHediff(HediffDefOf.BloodLoss);
            bloodLoss.Severity = Mathf.Max(bloodLoss.Severity, 0.72f);
            HealthUtility.DamageUntilDowned(pawn, false);
            return pawn;
        }

        public static void PrepareAirdroppedEgg(Pawn pawn, int stayHours)
        {
            if (pawn == null)
            {
                return;
            }

            MouseDisasterUtility.SetBiologicalAgeYears(pawn, 3f / GenDate.DaysPerYear);
            MouseDisasterUtility.RegisterAirDropStayPawn(pawn, GenDate.TicksPerHour * stayHours);
            MouseDisasterUtility.ConfigureNoRescueJoinForIncidentVisitor(pawn);
        }

        public static void PrepareStrandedAirdroppedEgg(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            MouseDisasterUtility.SetBiologicalAgeYears(pawn, 3f / GenDate.DaysPerYear);
            MouseDisasterUtility.ConfigureNoRescueJoinForIncidentVisitor(pawn);
        }

        public static bool IsPlagueCarrier(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(MouseDisasterDefOf.MouseDisaster_Plague) != null;
        }

        public static void TriggerEggBombRetaliation(Map map, int requestedCount, bool infect, Faction sourceFaction)
        {
            if (map == null)
            {
                return;
            }

            int actualCount = MouseDisasterUtility.LimitEventPawnCount(Mathf.Clamp(requestedCount, 4, 96), 4);
            if (GameComponent_MouseDisasterPawnGeneration.TryStartEggBombRetaliation(map, actualCount, infect, sourceFaction))
            {
                return;
            }

            Faction faction = ResolveVisitorFaction();
            List<Thing> payload = new List<Thing>();
            for (int i = 0; i < actualCount; i++)
            {
                Pawn pawn = CreateRatEggPawn(faction, babyStage: true, thiefLike: true, pureNegative: true, infect: infect, foodLevel: 0.08f);
                if (pawn == null)
                {
                    continue;
                }

                PrepareAirdroppedEgg(pawn, 24);
                payload.Add(pawn);
            }

            if (payload.Count == 0)
            {
                return;
            }

            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, payload, 110, canInstaDropDuringInit: false, leaveSlag: false, canRoofPunch: true, forbid: true, allowFogged: true, faction);
            Find.LetterStack.ReceiveLetter(
                "MouseDisaster_UI_AirdropRetaliationLabel".Translate().Resolve(),
                sourceFaction == null
                    ? "MouseDisaster_UI_AirdropOverloadRetaliation".Translate(payload.Count).Resolve()
                    : "MouseDisaster_UI_AirdropFactionRetaliation".Translate(sourceFaction.Name, payload.Count).Resolve(),
                infect ? LetterDefOf.ThreatBig : LetterDefOf.ThreatSmall,
                payload);
        }

        public static void SpawnPlagueRevengeWave(Map map, int count)
        {
            if (map == null)
            {
                return;
            }

            Faction faction = ResolveVisitorFaction();
            List<Thing> payload = new List<Thing>();
            for (int i = 0; i < MouseDisasterUtility.LimitEventPawnCount(Mathf.Max(1, count)); i++)
            {
                Pawn pawn = CreatePlagueRevengePawn(faction);
                if (pawn != null)
                {
                    payload.Add(pawn);
                }
            }

            if (payload.Count > 0)
            {
                DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, payload, 110, canInstaDropDuringInit: false, leaveSlag: false, canRoofPunch: true, forbid: true, allowFogged: true, faction);
            }
        }
    }
}
