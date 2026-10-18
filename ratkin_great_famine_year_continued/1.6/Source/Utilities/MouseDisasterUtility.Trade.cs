using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static void SendIncidentLetter(IncidentDef def, IncidentParms parms, LookTargets lookTargets)
        {
            Find.LetterStack.ReceiveLetter(def.letterLabel, def.letterText, def.letterDef, lookTargets, parms.faction);
        }

        public static bool SendFoodGiveLetter(IncidentDef def, IncidentParms parms, Map map, IEnumerable<Pawn> recipients)
        {
            List<Pawn> validRecipients = recipients?
                .Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map == map)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (def == null || map == null || validRecipients.Count == 0)
            {
                return false;
            }

            ChoiceLetter_MouseDisasterFoodGive letter =
                LetterMaker.MakeLetter(def.letterLabel, def.letterText, MouseDisasterDefOf.MouseDisaster_FoodGiveLetter, validRecipients) as ChoiceLetter_MouseDisasterFoodGive;
            if (letter == null)
            {
                return false;
            }

            letter.map = map;
            letter.recipients = validRecipients;
            Find.LetterStack.ReceiveLetter(letter, null);
            return true;
        }

        public static void EnsureTradeLeader(Pawn pawn, TraderKindDef preferredTraderKind)
        {
            if (pawn == null || pawn.mindState == null)
            {
                return;
            }

            if (pawn.mindState != null)
            {
                pawn.mindState.wantsToTradeWithColony = true;
            }
            if (pawn.kindDef != null && !pawn.kindDef.trader)
            {
                pawn.kindDef.trader = true;
            }

            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, actAsIfSpawned: true);
            if (pawn.trader != null)
            {
                pawn.trader.traderKind = preferredTraderKind ?? pawn.trader.traderKind;
            }
        }

        public static void RepairLoadedTradeLeaders()
        {
            TraderKindDef preferredTraderKind = ResolveSlaveTraderKind();
            if (preferredTraderKind == null)
            {
                return;
            }

            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
            {
                IEnumerable<Pawn> pawns = map?.mapPawns?.AllPawnsSpawned;
                if (pawns == null)
                {
                    continue;
                }

                foreach (Pawn pawn in pawns)
                {
                    if (!IsLoadedTradeLeaderCandidate(pawn))
                    {
                        continue;
                    }

                    EnsureTradeLeader(pawn, preferredTraderKind);
                }
            }
        }

        private static bool IsLoadedTradeLeaderCandidate(Pawn pawn)
        {
            return IsMouseDisasterTraderAdult(pawn) && pawn.Spawned && !pawn.Dead && !pawn.Destroyed &&
                   !IsPlayerAffiliatedRatkin(pawn) &&
                   (pawn.GetLord()?.LordJob is LordJob_TradeWithColony || HasActiveChildExchangeForTrader(pawn));
        }

        public static void ClearTradeLeaderState(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (pawn.mindState != null)
            {
                pawn.mindState.wantsToTradeWithColony = false;
            }

            if (pawn.trader != null)
            {
                pawn.trader.traderKind = null;
            }

            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, actAsIfSpawned: pawn.Spawned);
        }

        public static bool IsMouseDisasterTraderAdult(Pawn pawn)
        {
            return pawn != null && pawn.kindDef == MouseDisasterDefOf.MouseDisaster_TraderRatkinAdult;
        }

        public static bool IsMouseDisasterTraderEscort(Pawn pawn)
        {
            return pawn != null && pawn.kindDef == MouseDisasterDefOf.MouseDisaster_TraderRatkinEscort;
        }

        internal static bool IsMouseDisasterRatkinTradeThingDef(ThingDef thingDef)
        {
            return thingDef != null && thingDef.category == ThingCategory.Pawn && IsRatkinRaceDef(thingDef);
        }

        public static bool IsMouseDisasterIncidentParentAdult(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.DevelopmentalStage != DevelopmentalStage.Adult)
            {
                return false;
            }

            return IsMouseDisasterTraderAdult(pawn) ||
                   pawn.kindDef == MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult;
        }

        public static bool PrepareTradablePrisoner(Pawn pawn, Faction ownerFaction)
        {
            if (pawn == null || ownerFaction == null)
            {
                return false;
            }

            if (pawn.Faction != ownerFaction)
            {
                pawn.SetFaction(ownerFaction);
            }

            MarkTradableChattel(pawn);
            MarkForcedPrisonerOnPurchase(pawn);

            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, actAsIfSpawned: true);
            if (pawn.guest != null)
            {
                pawn.guest.joinStatus = JoinStatus.JoinAsColonist;
                pawn.guest.SetGuestStatus(ownerFaction, GuestStatus.Prisoner);
            }

            return true;
        }

        public static bool TryOrderFoodDelivery(Map map, IEnumerable<Pawn> recipients, out string message)
        {
            message = "MouseDisaster_FoodGive_Fail".Translate();
            if (map == null)
            {
                return false;
            }

            List<Pawn> validRecipients = recipients?
                .Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map == map)
                .Distinct()
                .ToList() ?? new List<Pawn>();
            if (validRecipients.Count == 0)
            {
                return false;
            }

            ThingDef requestedFoodDef = null;
            Pawn worker = null;
            Pawn requester = validRecipients
                .Where(pawn => !pawn.Downed)
                .OrderByDescending(pawn => pawn.DevelopmentalStage == DevelopmentalStage.Adult)
                .ThenBy(pawn => pawn.Position.DistanceToSquared(map.Center))
                .FirstOrDefault() ?? validRecipients.First();

            Thing foodThing = null;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned
                .Where(colonist => colonist != null && !colonist.Downed && !colonist.Drafted && colonist.jobs != null)
                .OrderBy(colonist => colonist.Position.DistanceToSquared(requester.Position))
                .ToList();
            ThingDef[] preferredFoodDefs =
            {
                ThingDefOf.MealSimple,
                ThingDefOf.MealFine,
                ThingDefOf.MealSurvivalPack
            };

            for (int colonistIndex = 0; colonistIndex < colonists.Count && foodThing == null; colonistIndex++)
            {
                Pawn colonist = colonists[colonistIndex];
                if (!colonist.CanReach(requester, PathEndMode.Touch, Danger.Some))
                {
                    continue;
                }

                for (int defIndex = 0; defIndex < preferredFoodDefs.Length; defIndex++)
                {
                    ThingDef foodDef = preferredFoodDefs[defIndex];
                    Thing candidate = GiveItemsToPawnUtility.FindItemToGive(colonist, foodDef);
                    if (candidate == null || !colonist.CanReserve(candidate))
                    {
                        continue;
                    }

                    worker = colonist;
                    requestedFoodDef = foodDef;
                    foodThing = candidate;
                    break;
                }
            }

            if (worker == null || requestedFoodDef == null || foodThing == null)
            {
                message = "MouseDisaster_FoodGive_NoFood".Translate();
                return false;
            }

            Faction lordFaction = requester.Faction;
            if (lordFaction == null)
            {
                TryFindFormerFaction(out lordFaction);
            }

            if (lordFaction == null)
            {
                message = "MouseDisaster_FoodGive_Fail".Translate();
                return false;
            }

            if (!RCellFinder.TryFindRandomSpotJustOutsideColony(requester.Position, map, requester, out IntVec3 idleSpot))
            {
                idleSpot = requester.Position;
            }

            int requestedCount = Mathf.Clamp(validRecipients.Count, 1, 12);
            Lord lord = LordJob_MouseDisasterBegForItems.MakeOrReplaceLord(
                lordFaction,
                idleSpot,
                requester,
                requestedFoodDef,
                requestedCount,
                map,
                validRecipients);
            if (lord == null)
            {
                message = "MouseDisaster_FoodGive_Fail".Translate();
                return false;
            }

            Job job = JobMaker.MakeJob(JobDefOf.GiveToPawn, foodThing, requester);
            job.haulMode = HaulMode.ToContainer;
            job.lord = lord;
            worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);

            message = "MouseDisaster_FoodGive_Success".Translate(worker.Named("PAWN"), requester.Named("TARGET"), requestedFoodDef.LabelCap);
            return true;
        }

        public static TraderKindDef ResolveSlaveTraderKind()
        {
            if (slaveTraderKindResolved)
            {
                return slaveTraderKind;
            }

            slaveTraderKindResolved = true;
            for (int i = 0; i < SlaveTraderKindDefNames.Length; i++)
            {
                TraderKindDef candidate = DefDatabase<TraderKindDef>.GetNamedSilentFail(SlaveTraderKindDefNames[i]);
                if (candidate != null)
                {
                    slaveTraderKind = candidate;
                    return slaveTraderKind;
                }
            }

            slaveTraderKind = DefDatabase<TraderKindDef>.AllDefsListForReading
                .FirstOrDefault(def => def != null &&
                                        ((!def.defName.NullOrEmpty() && def.defName.IndexOf("Slaver", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                         (!def.label.NullOrEmpty() && (def.label.IndexOf("slaver", StringComparison.OrdinalIgnoreCase) >= 0 || def.label.IndexOf("奴隶", StringComparison.OrdinalIgnoreCase) >= 0))));

            if (slaveTraderKind == null)
            {
                slaveTraderKind = DefDatabase<TraderKindDef>.GetNamedSilentFail("Caravan_Outlander_BulkGoods");
            }

            return slaveTraderKind;
        }
    }
}
