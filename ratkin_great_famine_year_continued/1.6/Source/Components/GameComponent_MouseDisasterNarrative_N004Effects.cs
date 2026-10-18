using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public partial class GameComponent_MouseDisasterNarrative
    {
        private const int N004RescueSilverCost = 250;
        private const int N004RescueRewardSilverCount = 2500;
        private const float N004RevisitBaseChance = 0.5f;
        private const float N004FamilyMoodEndAgeYears = 18f;

        private void ApplyN004OutcomeEffects(MouseDisasterN004Record record)
        {
            if (record == null || record.outcomeEffectsApplied)
            {
                return;
            }

            switch (record.outcome)
            {
                case MouseDisasterN004Outcome.ChildSurvived:
                    record.missingMotherBreakWindowUntilTick = CurrentNarrativeTick + GenDate.TicksPerDay;
                    record.missingMotherBreakTraitApplied = false;
                    if (record.children != null)
                    {
                        for (int i = 0; i < record.children.Count; i++)
                        {
                            Pawn child = record.children[i];
                            if (IsPlayerCare(child))
                            {
                                TryGainN004Memory(child, MouseDisasterDefOf.MouseDisaster_N004_MissingMother);
                            }
                        }
                    }
                    break;
                case MouseDisasterN004Outcome.ChildEnslaved:
                    if (record.children != null)
                    {
                        for (int i = 0; i < record.children.Count; i++)
                        {
                            Pawn child = record.children[i];
                            if (child != null && !child.Dead && (child.IsPrisonerOfColony || child.IsSlaveOfColony))
                            {
                                TryGainN004Memory(child, MouseDisasterDefOf.MouseDisaster_N004_SurvivedCaptivity);
                            }
                        }
                    }
                    break;
                case MouseDisasterN004Outcome.FamilySeparated:
                    if (record.revisitDeadlineTick < 0)
                    {
                        record.revisitDeadlineTick = CurrentNarrativeTick + N004RevisitDurationTicks;
                    }
                    RetainN004RevisitPawns(record);
                    break;
                case MouseDisasterN004Outcome.ChildDied:
                    TryAddN004RegretTrait(record.mother);
                    break;
            }

            record.outcomeEffectsApplied = true;
        }

        public int GetN004FamilySurvivedThoughtStage(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.ageTracker == null || !IsN004Present(pawn))
            {
                return -1;
            }

            for (int i = 0; i < n004Records.Count; i++)
            {
                MouseDisasterN004Record record = n004Records[i];
                Pawn mother = record?.mother;
                if (record == null || record.outcome != MouseDisasterN004Outcome.FamilySurvived || mother == null || mother.Dead)
                {
                    continue;
                }

                if (pawn == mother)
                {
                    if (!IsN004InPlayerDomain(mother) || !HasN004LivingChildWithMood(mother, record))
                    {
                        continue;
                    }

                    return mother.IsPrisonerOfColony || mother.IsSlaveOfColony ? 2 : 1;
                }

                if (record.children != null && record.children.Contains(pawn) && IsN004FamilyChildWithMood(pawn, mother))
                {
                    return 0;
                }
            }

            return -1;
        }

        public bool IsN004FamilySurvivedMother(Pawn pawn)
        {
            return GetN004FamilySurvivedThoughtStage(pawn) >= 1;
        }

        public bool HasN004MotherRegret(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.needs?.mood == null)
            {
                return false;
            }

            return n004Records.Any(record =>
                record?.outcome == MouseDisasterN004Outcome.ChildDied &&
                record.mother == pawn);
        }

        public void NotifyN004MentalBreak(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || n004Records == null)
            {
                return;
            }

            for (int i = 0; i < n004Records.Count; i++)
            {
                MouseDisasterN004Record record = n004Records[i];
                if (record?.outcome != MouseDisasterN004Outcome.ChildSurvived ||
                    record.missingMotherBreakTraitApplied ||
                    record.missingMotherBreakWindowUntilTick < 0 ||
                    CurrentNarrativeTick > record.missingMotherBreakWindowUntilTick ||
                    record.children == null ||
                    !record.children.Contains(pawn))
                {
                    continue;
                }

                record.missingMotherBreakTraitApplied = true;
                TryAddN004Trait(pawn, "NaturalMood", -2);
                return;
            }
        }

        private void ProcessN004RescueRewards()
        {
            if (n004Records == null)
            {
                return;
            }

            int now = CurrentNarrativeTick;
            ProcessN004RevisitExpiry(now);
            for (int i = 0; i < n004Records.Count; i++)
            {
                MouseDisasterN004Record record = n004Records[i];
                if (record?.revisitDecision != MouseDisasterN004RevisitDecision.Rescued ||
                    record.rescueRewardGranted ||
                    record.rescueRewardDueTick < 0 ||
                    now < record.rescueRewardDueTick)
                {
                    continue;
                }

                Map map = Find.AnyPlayerHomeMap;
                if (map == null)
                {
                    continue;
                }

                Thing reward = ThingMaker.MakeThing(ThingDefOf.Silver);
                reward.stackCount = N004RescueRewardSilverCount;
                DropPodUtility.DropThingsNear(
                    DropCellFinder.TradeDropSpot(map),
                    map,
                    Gen.YieldSingle(reward),
                    110,
                    canInstaDropDuringInit: false,
                    leaveSlag: false,
                    canRoofPunch: true,
                    forbid: false,
                    allowFogged: true,
                    faction: null);
                record.rescueRewardGranted = true;

                if (IsNarratorActive())
                {
                    ReceiveNarrativeLetter(
                        "MouseDisaster_N004_Revisit_RewardLabel",
                        "MouseDisaster_N004_Revisit_RewardText",
                        map,
                        N004RescueRewardSilverCount);
                }
            }
        }

        public void TryTriggerN004Revisit(Caravan caravan)
        {
            if (!NarrativeEnabled("N004Return")) return;
            if (caravan == null || !caravan.Spawned || !caravan.IsPlayerControlled || n004Records == null)
            {
                return;
            }

            int now = CurrentNarrativeTick;
            MouseDisasterN004Record record = n004Records.FirstOrDefault(candidate =>
                candidate?.outcome == MouseDisasterN004Outcome.FamilySeparated &&
                candidate.revisitDecision == MouseDisasterN004RevisitDecision.Pending &&
                !candidate.revisitTriggered &&
                candidate.revisitDeadlineTick > now &&
                GetN004RevisitPawns(candidate).Count > 0);
            if (record == null || (!narrativeDebugForce && !Rand.Chance(MouseDisasterNarrativePolicy.ScaledChance(
                Mathf.Clamp01(N004RevisitBaseChance * caravan.Visibility), MouseDisasterMod.Settings?.narrativeN004ReturnChancePercent ?? 100f))))
            {
                return;
            }

            List<Pawn> candidates = GetN004RevisitPawns(record);
            if (candidates.Count == 0)
            {
                return;
            }

            record.revisitTriggered = true;
            OpenN004RevisitDialog(caravan, record, candidates);
        }

        private void OpenN004RevisitDialog(Caravan caravan, MouseDisasterN004Record record, List<Pawn> candidates)
        {
            string names = string.Join("、", candidates.Select(pawn => pawn.LabelShort));
            DiaNode root = new DiaNode("MouseDisaster_N004_Revisit_Text".Translate(names));

            DiaOption ignore = new DiaOption("MouseDisaster_N004_Revisit_Ignore".Translate());
            ignore.action = delegate
            {
                record.revisitDecision = MouseDisasterN004RevisitDecision.Ignored;
                ReleaseN004RevisitPawns(record);
                Messages.Message(
                    "MouseDisaster_N004_Revisit_IgnoreMessage".Translate(),
                    caravan,
                    MessageTypeDefOf.NeutralEvent,
                    historical: false);
            };
            ignore.resolveTree = true;
            root.options.Add(ignore);

            DiaOption rescue = new DiaOption("MouseDisaster_N004_Revisit_Rescue".Translate());
            if (!CaravanHasSilver(caravan, N004RescueSilverCost))
            {
                rescue.Disable("MouseDisaster_N004_Revisit_RescueUnavailable".Translate());
            }
            else
            {
                rescue.action = delegate
                {
                    List<Pawn> currentCandidates = GetN004RevisitPawns(record);
                    if (currentCandidates.Count == 0)
                    {
                        record.revisitTriggered = false;
                        return;
                    }

                    if (!TryTakeCaravanSilver(caravan, N004RescueSilverCost))
                    {
                        record.revisitTriggered = false;
                        Messages.Message(
                            "MouseDisaster_N004_Revisit_RescueUnavailable".Translate(),
                            caravan,
                            MessageTypeDefOf.RejectInput,
                            historical: false);
                        return;
                    }

                    int rescuedCount = RescueN004Pawns(caravan, currentCandidates);
                    if (rescuedCount <= 0)
                    {
                        record.revisitTriggered = false;
                        return;
                    }

                    record.revisitDecision = MouseDisasterN004RevisitDecision.Rescued;
                    ReleaseN004RevisitPawns(record);
                    record.rescueRewardDueTick = CurrentNarrativeTick + N004RevisitDurationTicks;
                    Messages.Message(
                        "MouseDisaster_N004_Revisit_RescueMessage".Translate(),
                        caravan,
                        MessageTypeDefOf.PositiveEvent,
                        historical: false);
                };
            }
            rescue.resolveTree = true;
            root.options.Add(rescue);

            DiaOption kill = new DiaOption("MouseDisaster_N004_Revisit_Kill".Translate());
            kill.action = delegate
            {
                List<Pawn> currentCandidates = GetN004RevisitPawns(record);
                for (int i = 0; i < currentCandidates.Count; i++)
                {
                    if (!currentCandidates[i].Dead)
                    {
                        currentCandidates[i].Kill(null);
                    }
                }

                record.revisitDecision = MouseDisasterN004RevisitDecision.Killed;
                ReleaseN004RevisitPawns(record);
                ChangeNarratorTrust(-10);
                if (IsNarratorActive())
                {
                    Messages.Message(
                        "MouseDisaster_N004_Revisit_KillMessage".Translate(),
                        caravan,
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }
            };
            kill.resolveTree = true;
            root.options.Add(kill);

            string title = "MouseDisaster_N004_Revisit_Label".Translate();
            CameraJumper.TryJumpAndSelect(caravan);
            Find.WindowStack.Add(new Dialog_NodeTree(root, delayInteractivity: true, radioMode: false, title: title));
            Find.Archive.Add(new ArchivedDialog(root.text, title));
        }

        private static List<Pawn> GetN004RevisitPawns(MouseDisasterN004Record record)
        {
            List<Pawn> result = new List<Pawn>();
            if (record == null)
            {
                return result;
            }

            if (IsN004RevisitCandidate(record.mother))
            {
                result.Add(record.mother);
            }

            if (record.children != null)
            {
                result.AddRange(record.children.Where(IsN004RevisitCandidate));
            }

            return result.Distinct().ToList();
        }

        private static void RetainN004RevisitPawns(MouseDisasterN004Record record)
        {
            if (record == null || Find.WorldPawns == null)
            {
                return;
            }

            IEnumerable<Pawn> pawns = new[] { record.mother }
                .Concat(record.children ?? Enumerable.Empty<Pawn>());
            foreach (Pawn pawn in pawns.Distinct())
            {
                if (pawn != null && !pawn.Dead && Find.WorldPawns.Contains(pawn))
                {
                    Find.WorldPawns.ForcefullyKeptPawns.Add(pawn);
                }
            }
        }

        private void ProcessN004RevisitExpiry(int now)
        {
            for (int i = 0; i < n004Records.Count; i++)
            {
                MouseDisasterN004Record record = n004Records[i];
                if (record?.outcome != MouseDisasterN004Outcome.FamilySeparated ||
                    record.revisitDecision != MouseDisasterN004RevisitDecision.Pending ||
                    record.revisitTriggered ||
                    record.revisitDeadlineTick < 0 ||
                    now < record.revisitDeadlineTick)
                {
                    continue;
                }

                record.revisitTriggered = true;
                ReleaseN004RevisitPawns(record);
            }
        }

        private static void ReleaseN004RevisitPawns(MouseDisasterN004Record record)
        {
            if (record == null || Find.WorldPawns == null)
            {
                return;
            }

            IEnumerable<Pawn> pawns = new[] { record.mother }
                .Concat(record.children ?? Enumerable.Empty<Pawn>());
            foreach (Pawn pawn in pawns.Distinct())
            {
                if (pawn != null)
                {
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn);
                }
            }
        }

        private static bool IsN004RevisitCandidate(Pawn pawn)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   !pawn.Spawned &&
                   pawn.MapHeld == null &&
                   pawn.GetCaravan() == null &&
                   Find.WorldPawns != null &&
                   Find.WorldPawns.Contains(pawn);
        }

        private static int RescueN004Pawns(Caravan caravan, IEnumerable<Pawn> pawns)
        {
            int rescuedCount = 0;
            foreach (Pawn pawn in pawns.ToList())
            {
                if (!IsN004RevisitCandidate(pawn))
                {
                    continue;
                }

                Find.WorldPawns.RemovePawn(pawn);
                pawn.SetFaction(Faction.OfPlayer);
                pawn.guest?.SetGuestStatus(null, GuestStatus.Guest);
                caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: false);
                rescuedCount++;
            }

            caravan.RecacheInventory();
            return rescuedCount;
        }

        private static bool CaravanHasSilver(Caravan caravan, int amount)
        {
            if (caravan == null || amount <= 0)
            {
                return false;
            }

            int total = 0;
            List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i]?.def == ThingDefOf.Silver)
                {
                    total += items[i].stackCount;
                    if (total >= amount)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryTakeCaravanSilver(Caravan caravan, int amount)
        {
            if (!CaravanHasSilver(caravan, amount))
            {
                return false;
            }

            int remaining = amount;
            List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan).ToList();
            for (int i = 0; i < items.Count && remaining > 0; i++)
            {
                Thing silver = items[i];
                if (silver?.def != ThingDefOf.Silver)
                {
                    continue;
                }

                int taken = Mathf.Min(remaining, silver.stackCount);
                silver.SplitOff(taken).Destroy(DestroyMode.Vanish);
                remaining -= taken;
            }

            caravan.RecacheInventory();
            return remaining == 0;
        }

        private static bool HasN004LivingChildWithMood(Pawn mother, MouseDisasterN004Record record)
        {
            return record.children != null && record.children.Any(child =>
                child != null &&
                !child.Dead &&
                IsN004FamilyChildWithMood(child, mother));
        }

        private static bool IsN004FamilyChildWithMood(Pawn child, Pawn mother)
        {
            return child != null &&
                   child.ageTracker != null &&
                   child.ageTracker.AgeBiologicalYearsFloat < N004FamilyMoodEndAgeYears &&
                   IsChildAge(child) &&
                   IsN004InPlayerDomain(child) &&
                   IsN004InPlayerDomain(mother) &&
                   IsN004Together(mother, child);
        }

        private static bool IsN004Present(Pawn pawn)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   (pawn.Spawned || pawn.MapHeld != null || pawn.GetCaravan() != null);
        }

        private static bool IsN004InPlayerDomain(Pawn pawn)
        {
            return IsN004Present(pawn) &&
                   (pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony);
        }

        private static bool IsN004Together(Pawn first, Pawn second)
        {
            if (!IsN004Present(first) || !IsN004Present(second))
            {
                return false;
            }

            Map firstMap = first.MapHeld;
            if (firstMap != null && firstMap == second.MapHeld)
            {
                return true;
            }

            Caravan firstCaravan = first.GetCaravan();
            return firstCaravan != null && firstCaravan == second.GetCaravan();
        }

        private static void TryGainN004Memory(Pawn pawn, ThoughtDef thoughtDef)
        {
            pawn?.needs?.mood?.thoughts?.memories?.TryGainMemory(thoughtDef);
        }

        private static void TryAddN004RegretTrait(Pawn pawn)
        {
            if (pawn?.story?.traits == null)
            {
                return;
            }

            if (Rand.Bool)
            {
                if (!TryAddN004Trait(pawn, "TorturedArtist", 0))
                {
                    TryAddN004Trait(pawn, "NaturalMood", -2);
                }
            }
            else if (!TryAddN004Trait(pawn, "NaturalMood", -2))
            {
                TryAddN004Trait(pawn, "TorturedArtist", 0);
            }
        }

        private static bool TryAddN004Trait(Pawn pawn, string traitDefName, int degree)
        {
            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
            if (pawn?.story?.traits == null || traitDef == null || pawn.story.traits.HasTrait(traitDef))
            {
                return false;
            }

            pawn.story.traits.GainTrait(new Trait(traitDef, degree), suppressConflicts: false);
            return true;
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class MouseDisasterN004MentalBreakPatch
    {
        private static readonly AccessTools.FieldRef<MentalStateHandler, Pawn> PawnField =
            AccessTools.FieldRefAccess<MentalStateHandler, Pawn>("pawn");

        public static void Postfix(MentalStateHandler __instance, bool __result)
        {
            if (!__result)
            {
                return;
            }

            Pawn pawn = PawnField(__instance);
            Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.NotifyN004MentalBreak(pawn);
        }
    }

    [HarmonyPatch(typeof(PrisonBreakUtility), nameof(PrisonBreakUtility.InitiatePrisonBreakMtbDays))]
    public static class MouseDisasterN004PrisonBreakPatch
    {
        public static void Postfix(Pawn pawn, ref float __result)
        {
            if (Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.IsN004FamilySurvivedMother(pawn) == true)
            {
                __result = -1f;
            }
        }
    }

    [HarmonyPatch(typeof(SlaveRebellionUtility), nameof(SlaveRebellionUtility.InitiateSlaveRebellionMtbDays))]
    public static class MouseDisasterN004SlaveRebellionPatch
    {
        public static void Postfix(Pawn pawn, ref float __result)
        {
            if (Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.IsN004FamilySurvivedMother(pawn) == true)
            {
                __result = -1f;
            }
        }
    }

    [HarmonyPatch(typeof(Caravan_PathFollower), "PatherArrived")]
    public static class MouseDisasterN004CaravanArrivalPatch
    {
        private static readonly AccessTools.FieldRef<Caravan_PathFollower, Caravan> CaravanField =
            AccessTools.FieldRefAccess<Caravan_PathFollower, Caravan>("caravan");

        public static void Postfix(Caravan_PathFollower __instance)
        {
            Caravan caravan = CaravanField(__instance);
            if (caravan != null && caravan.Spawned && caravan.IsPlayerControlled)
            {
                Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.TryTriggerN004Revisit(caravan);
            }
        }
    }
}
