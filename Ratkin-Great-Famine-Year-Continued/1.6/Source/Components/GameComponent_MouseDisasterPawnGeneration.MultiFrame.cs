using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public partial class GameComponent_MouseDisasterPawnGeneration
    {
        internal static bool TryStartBeggarGroup(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, Faction faction, int adults, int children, bool infectsWithPlague = false)
        {
            if (adults <= 0 || children < 0 || adults > MaxGeneratedSlots || children > MaxGeneratedSlots - adults)
            {
                return false;
            }

            faction = ResolveNeutralFaction(map, faction);
            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.BeggarGroup,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                adultsRemaining = adults,
                childrenRemaining = children,
                infectsWithPlague = infectsWithPlague
            }, adults + children);
        }

        internal static bool TryStartThiefGroup(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, Faction faction, int count, bool childOnly, bool infectsWithPlague = false)
        {
            if (count <= 0 || count > MaxGeneratedSlots)
            {
                return false;
            }

            faction = ResolveNeutralFaction(map, faction);
            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = childOnly ? MouseDisasterPawnBatchKind.ThiefChildGroup : MouseDisasterPawnBatchKind.ThiefGroup,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                remainingCount = count,
                infectsWithPlague = infectsWithPlague
            }, count);
        }

        internal static bool TryStartWildGroup(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, int count, bool infectsWithPlague = false)
        {
            if (count <= 0 || count > MaxGeneratedSlots)
            {
                return false;
            }

            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.WildGroup,
                map = map,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                remainingCount = count,
                infectsWithPlague = infectsWithPlague
            }, count);
        }

        internal static bool TryStartTravelerGroup(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, IntVec3 exitCell, Faction faction, int adults, int children, bool infectsWithPlague)
        {
            if (adults <= 0 || children < 0 || adults > MaxGeneratedSlots || children > MaxGeneratedSlots - adults)
            {
                return false;
            }

            faction = ResolveNeutralFaction(map, faction);
            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.TravelerGroup,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                exitCell = exitCell.IsValid ? exitCell : map.Center,
                adultsRemaining = adults,
                childrenRemaining = children,
                infectsWithPlague = infectsWithPlague,
                foodPercentage = 0.35f
            }, adults + children);
        }

        internal static bool TryStartFamineRefugees(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, int count, bool infectsWithPlague = false)
        {
            if (count <= 0 || count > MaxGeneratedSlots)
            {
                return false;
            }

            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.FamineRefugees,
                map = map,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                remainingCount = count,
                infectsWithPlague = infectsWithPlague
            }, count);
        }

        internal static bool TryStartSiegeBeggarGroup(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, Faction faction, int adults, int children)
        {
            if (adults <= 0 || children < 0 || adults > MaxGeneratedSlots || children > MaxGeneratedSlots - adults)
            {
                return false;
            }

            faction = ResolveNeutralFaction(map, faction);
            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.SiegeBeggarGroup,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                adultsRemaining = adults,
                childrenRemaining = children
            }, adults + children);
        }

        internal static bool TryStartStrongSiegeGroup(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, Faction faction, int count, bool infectsWithPlague)
        {
            if (count <= 0 || count > MaxGeneratedSlots)
            {
                return false;
            }

            faction = ResolveNeutralFaction(map, faction);
            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.StrongSiegeGroup,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                remainingCount = count,
                infectsWithPlague = infectsWithPlague
            }, count);
        }

        internal static bool TryStartAirdropMistake(IncidentDef incidentDef, IncidentParms parms, Map map, IntVec3 entryCell, Faction faction, int count, bool infectsWithPlague)
        {
            if (count <= 0 || count > MaxGeneratedSlots)
            {
                return false;
            }

            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.AirdropMistake,
                map = map,
                faction = faction,
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = entryCell,
                remainingCount = count,
                infectsWithPlague = infectsWithPlague
            }, count);
        }

        internal static bool TryStartEggBombRetaliation(Map map, int requestedCount, bool infectsWithPlague, Faction sourceFaction)
        {
            if (map == null)
            {
                return false;
            }

            int count = Mathf.Clamp(requestedCount, 4, 96);
            Faction faction = MouseDisasterPhase3Utility.ResolveVisitorFaction();
            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.EggBombRetaliation,
                map = map,
                faction = faction,
                sourceFaction = sourceFaction,
                entryCell = map.Center,
                remainingCount = count,
                infectsWithPlague = infectsWithPlague,
                thiefLike = true,
                pureNegative = true,
                stayHours = 24,
                specialLetterDef = infectsWithPlague ? LetterDefOf.ThreatBig : LetterDefOf.ThreatSmall
            }, count);
        }

        internal static bool TryStartPlagueRevengeWave(IncidentDef incidentDef, IncidentParms parms, Map map, int count)
        {
            if (map == null || count <= 0 || count > MaxGeneratedSlots)
            {
                return false;
            }

            return TryStartBatch(new MouseDisasterPawnBatch
            {
                kind = MouseDisasterPawnBatchKind.PlagueRevengeWave,
                map = map,
                faction = MouseDisasterPhase3Utility.ResolveVisitorFaction(),
                incidentDef = incidentDef,
                parms = parms?.ShallowCopy(),
                entryCell = map.Center,
                remainingCount = count
            }, count);
        }

        private static Faction ResolveNeutralFaction(Map map, Faction faction)
        {
            if (faction == null)
            {
                MouseDisasterUtility.TryFindFormerFaction(out faction);
            }

            if (faction != null)
            {
                MouseDisasterUtility.MakeFactionNeutralToPlayer(faction, force: true);
                MouseDisasterUtility.EnsureMouseDisasterFactionNeutralOnMap(map, faction);
            }

            return faction;
        }

        private static bool IsPayloadBatch(MouseDisasterPawnBatch batch)
        {
            return batch != null && (batch.kind == MouseDisasterPawnBatchKind.AirdropMistake ||
                batch.kind == MouseDisasterPawnBatchKind.EggBombRetaliation ||
                batch.kind == MouseDisasterPawnBatchKind.PlagueRevengeWave);
        }

        private static bool IsStandalonePayloadBatch(MouseDisasterPawnBatch batch)
        {
            return batch?.kind == MouseDisasterPawnBatchKind.EggBombRetaliation;
        }

        private static bool IsMultiFrameVisitorBatch(MouseDisasterPawnBatch batch)
        {
            if (batch == null)
            {
                return false;
            }

            switch (batch.kind)
            {
                case MouseDisasterPawnBatchKind.BeggarGroup:
                case MouseDisasterPawnBatchKind.ThiefGroup:
                case MouseDisasterPawnBatchKind.ThiefChildGroup:
                case MouseDisasterPawnBatchKind.WildGroup:
                case MouseDisasterPawnBatchKind.TravelerGroup:
                case MouseDisasterPawnBatchKind.FamineRefugees:
                case MouseDisasterPawnBatchKind.SiegeBeggarGroup:
                case MouseDisasterPawnBatchKind.StrongSiegeGroup:
                    return true;
                default:
                    return false;
            }
        }

        private static void GenerateBeggarGroupPawn(MouseDisasterPawnBatch batch)
        {
            bool adult = batch.adultsRemaining > 0;
            if (adult)
            {
                batch.adultsRemaining--;
            }
            else
            {
                batch.childrenRemaining--;
            }

            DevelopmentalStage stage = adult ? DevelopmentalStage.Adult : DevelopmentalStage.Child;
            PawnKindDef kindDef = adult
                ? MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult
                : MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild;
            Pawn pawn = MouseDisasterUtility.GenerateBeggarPawn(kindDef, batch.faction, stage, allowViolenceDisabledTraits: true);
            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 6), batch.map);
            batch.pawns.Add(pawn);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            pawn.jobs?.StartJob(MouseDisasterUtility.CreateGotoJob(batch.map.Center), JobCondition.InterruptForced);

            if (batch.kind == MouseDisasterPawnBatchKind.SiegeBeggarGroup)
            {
                MouseDisasterUtility.RegisterSiegeBeggar(pawn);
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_SiegeBeggar);
            }
            else if (!batch.infectsWithPlague)
            {
                pawn.health.AddHediff(adult
                    ? MouseDisasterDefOf.MouseDisaster_BeggarGroupAdult
                    : MouseDisasterDefOf.MouseDisaster_BeggarGroupChild);
            }

            if (batch.infectsWithPlague)
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }
        }

        private static void GenerateThiefGroupPawn(MouseDisasterPawnBatch batch)
        {
            batch.remainingCount--;
            bool childOnly = batch.kind == MouseDisasterPawnBatchKind.ThiefChildGroup;
            bool useChild = childOnly || (Find.Storyteller.difficulty.ChildrenAllowed && Rand.Chance(0.45f));
            PawnKindDef kindDef = useChild
                ? MouseDisasterDefOf.MouseDisaster_ThiefRatkinChild
                : MouseDisasterDefOf.MouseDisaster_ThiefRatkinAdult;
            DevelopmentalStage stage = useChild ? DevelopmentalStage.Child : DevelopmentalStage.Adult;
            Pawn pawn = MouseDisasterUtility.GenerateThiefPawn(kindDef, batch.faction, stage, allowViolenceDisabledTraits: true);
            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 6), batch.map);
            batch.pawns.Add(pawn);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            pawn.jobs?.StartJob(MouseDisasterUtility.CreateGotoJob(batch.map.Center), JobCondition.InterruptForced);

            if (batch.kind == MouseDisasterPawnBatchKind.StrongSiegeGroup)
            {
                MouseDisasterUtility.RegisterStrongSiegePawn(pawn);
            }
            else if (childOnly)
            {
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_ThiefChildOnly);
            }
            else if (!batch.infectsWithPlague)
            {
                pawn.health.AddHediff(useChild
                    ? MouseDisasterDefOf.MouseDisaster_ThiefChild
                    : MouseDisasterDefOf.MouseDisaster_ThiefAdult);
            }

            if (batch.infectsWithPlague)
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }
        }

        private static void GenerateWildGroupPawn(MouseDisasterPawnBatch batch)
        {
            batch.remainingCount--;
            PawnKindDef kindDef = MouseDisasterUtility.RandomWildKind(Find.Storyteller.difficulty.ChildrenAllowed);
            DevelopmentalStage stage = kindDef == MouseDisasterDefOf.MouseDisaster_WildRatkinChild
                ? DevelopmentalStage.Child
                : DevelopmentalStage.Adult;
            Pawn pawn = MouseDisasterUtility.GenerateWildPawn(kindDef, batch.faction, stage);
            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 6), batch.map);
            batch.pawns.Add(pawn);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            if (!batch.infectsWithPlague)
            {
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_WildGroupWanderer);
            }
            else
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }
        }

        private static void GenerateTravelerGroupPawn(MouseDisasterPawnBatch batch)
        {
            bool adult = batch.adultsRemaining > 0;
            if (adult)
            {
                batch.adultsRemaining--;
            }
            else
            {
                batch.childrenRemaining--;
            }

            DevelopmentalStage stage = adult ? DevelopmentalStage.Adult : DevelopmentalStage.Child;
            PawnKindDef kindDef = adult
                ? MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult
                : MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild;
            Pawn pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(
                kindDef,
                batch.faction,
                stage,
                batch.foodPercentage,
                allowViolenceDisabledTraits: true);
            if (pawn == null)
            {
                return;
            }

            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 6), batch.map);
            batch.pawns.Add(pawn);
            if (!adult)
            {
                MouseDisasterUtility.StripRatEggInventory(pawn);
            }

            if (batch.infectsWithPlague)
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }
        }

        private static void GenerateFamineRefugeePawn(MouseDisasterPawnBatch batch)
        {
            batch.remainingCount--;
            bool child = Find.Storyteller.difficulty.ChildrenAllowed && Rand.Chance(0.5f);
            DevelopmentalStage stage = child ? DevelopmentalStage.Child : DevelopmentalStage.Adult;
            PawnKindDef kindDef = child
                ? MouseDisasterDefOf.MouseDisaster_WildRatkinChild
                : MouseDisasterDefOf.MouseDisaster_WildRatkinAdult;
            Pawn pawn = MouseDisasterUtility.GenerateFactionRatkinPawn(kindDef, null, stage, 0.28f);
            if (pawn == null)
            {
                return;
            }

            pawn.SetFaction(null);
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(batch.entryCell, batch.map, 4), batch.map);
            batch.pawns.Add(pawn);
            MouseDisasterUtility.StripRatEggInventory(pawn);
            if (!batch.infectsWithPlague)
            {
                pawn.health.AddHediff(MouseDisasterDefOf.MouseDisaster_FamineRefugee);
            }
            else
            {
                MouseDisasterPlagueUtility.InfectWithPlague(pawn);
            }
        }

        private static void GenerateAirdropEggPawn(MouseDisasterPawnBatch batch)
        {
            batch.remainingCount--;
            Pawn pawn = MouseDisasterPhase3Utility.CreateRatEggPawn(
                batch.faction,
                babyStage: true,
                thiefLike: false,
                pureNegative: false,
                infect: batch.infectsWithPlague,
                foodLevel: 0.14f);
            if (pawn == null)
            {
                return;
            }

            MouseDisasterPhase3Utility.PrepareStrandedAirdroppedEgg(pawn);
            batch.pawns.Add(pawn);
        }

        private static void GenerateSpecialPayloadPawn(MouseDisasterPawnBatch batch)
        {
            batch.remainingCount--;
            Pawn pawn = batch.kind == MouseDisasterPawnBatchKind.EggBombRetaliation
                ? MouseDisasterPhase3Utility.CreateRatEggPawn(
                    batch.faction,
                    babyStage: true,
                    thiefLike: batch.thiefLike,
                    pureNegative: batch.pureNegative,
                    infect: batch.infectsWithPlague,
                    foodLevel: 0.08f)
                : MouseDisasterPhase3Utility.CreatePlagueRevengePawn(batch.faction);
            if (pawn == null)
            {
                return;
            }

            if (batch.kind == MouseDisasterPawnBatchKind.EggBombRetaliation)
            {
                MouseDisasterPhase3Utility.PrepareAirdroppedEgg(pawn, batch.stayHours);
            }

            batch.pawns.Add(pawn);
        }

        private static void FinalizeMultiFrameGroup(MouseDisasterPawnBatch batch, List<Pawn> pawns)
        {
            if (batch.kind == MouseDisasterPawnBatchKind.WildGroup)
            {
                if (MouseDisasterVisitorChoicePolicy.ShouldUpgradeToVisitorChoiceControl(batch.incidentDef.defName) &&
                    MouseDisasterVisitorUtility.RegisterAndSendVisitorChoiceLetter(batch.incidentDef, batch.parms, batch.map, pawns))
                {
                    return;
                }

                if (!MouseDisasterUtility.SendFoodGiveLetter(batch.incidentDef, batch.parms, batch.map, pawns))
                {
                    MouseDisasterUtility.SendIncidentLetter(batch.incidentDef, batch.parms, pawns);
                }
                return;
            }

            MouseDisasterVisitorUtility.RegisterVisitors(pawns);
            if (batch.kind == MouseDisasterPawnBatchKind.TravelerGroup)
            {
                SendVisitorOrStandardLetter(batch, pawns);
                return;
            }

            if (batch.kind == MouseDisasterPawnBatchKind.FamineRefugees)
            {
                ChoiceLetter_FamineRefugees letter = LetterMaker.MakeLetter(
                    batch.incidentDef.letterLabel,
                    batch.incidentDef.letterText,
                    MouseDisasterDefOf.MouseDisaster_AcceptFamineRefugees,
                    pawns) as ChoiceLetter_FamineRefugees;
                if (letter == null)
                {
                    MouseDisasterUtility.DestroyFailedIncidentPawns(pawns);
                    return;
                }

                letter.refugees = pawns;
                letter.map = batch.map;
                Find.LetterStack.ReceiveLetter(letter, null);
                return;
            }

            if (batch.kind == MouseDisasterPawnBatchKind.ThiefGroup || batch.kind == MouseDisasterPawnBatchKind.ThiefChildGroup)
            {
                if (!MouseDisasterVisitorUtility.SendVisitorChoiceLetter(batch.incidentDef, batch.parms, batch.map, pawns))
                {
                    if (batch.kind == MouseDisasterPawnBatchKind.ThiefChildGroup)
                    {
                        Messages.Message("MouseDisaster_UI_ChildThievesArrived".Translate().Resolve(), pawns, MessageTypeDefOf.NeutralEvent, false);
                    }
                    else
                    {
                        Messages.Message("MouseDisaster_UI_ThievesArrived".Translate().Resolve(), pawns, MessageTypeDefOf.NeutralEvent, false);
                    }
                }
                Find.TickManager.slower.SignalForceNormalSpeedShort();
                return;
            }

            SendVisitorOrStandardLetter(batch, pawns);
        }

        private static void SendVisitorOrStandardLetter(MouseDisasterPawnBatch batch, List<Pawn> pawns)
        {
            if (!MouseDisasterVisitorUtility.SendVisitorChoiceLetter(batch.incidentDef, batch.parms, batch.map, pawns))
            {
                SendBatchLetter(batch, pawns);
            }
        }

        private static void FinalizePayloadBatch(MouseDisasterPawnBatch batch)
        {
            List<Pawn> payloadPawns = batch?.pawns?
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Spawned)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (payloadPawns.Count == 0)
            {
                return;
            }

            if (!IsBatchMapAvailable(batch))
            {
                MouseDisasterUtility.DestroyFailedIncidentPawns(payloadPawns);
                return;
            }

            List<Thing> payload = payloadPawns.Cast<Thing>().ToList();
            DropPodUtility.DropThingsNear(
                DropCellFinder.TradeDropSpot(batch.map),
                batch.map,
                payload,
                110,
                canInstaDropDuringInit: false,
                leaveSlag: false,
                canRoofPunch: true,
                forbid: true,
                allowFogged: true,
                batch.faction);
            GameComponent_MouseDisasterEventBehavior.Component?.Register(batch.behaviorGroupId, payloadPawns);
            if (batch.kind == MouseDisasterPawnBatchKind.EggBombRetaliation)
            {
                Find.LetterStack.ReceiveLetter(
                    "MouseDisaster_UI_AirdropRetaliationLabel".Translate().Resolve(),
                    batch.sourceFaction == null
                        ? "MouseDisaster_UI_AirdropOverloadRetaliation".Translate(payload.Count).Resolve()
                        : "MouseDisaster_UI_AirdropFactionRetaliation".Translate(batch.sourceFaction.Name, payload.Count).Resolve(),
                    batch.specialLetterDef,
                    payload);
            }
            else if (batch.kind == MouseDisasterPawnBatchKind.PlagueRevengeWave)
            {
                IncidentWorker.SendIncidentLetter(
                    batch.incidentDef.letterLabel,
                    batch.incidentDef.letterText,
                    batch.incidentDef.letterDef,
                    batch.parms,
                    new TargetInfo(batch.map.Center, batch.map),
                    batch.incidentDef);
            }
            else
            {
                IncidentWorker.SendIncidentLetter(
                    batch.incidentDef.letterLabel,
                    batch.incidentDef.letterText,
                    batch.incidentDef.letterDef,
                    batch.parms,
                    payload,
                    batch.incidentDef);
            }
        }
    }
}
