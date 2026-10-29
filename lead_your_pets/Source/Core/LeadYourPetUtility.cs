using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    public static class LeadYourPetUtility
    {
        public const string HarmonyId = "lezhizhong.leadyourpet";

        private const string RatkinDisasterPackageId = "lezhizhong.mouse.disaster.famine";
        private const string RatkinDisasterChildPawnKindDefName = "MouseDisaster_BeggarRatkinChild";
        private const string RatkinDisasterTraderPawnKindDefName = "MouseDisaster_TraderRatkinAdult";
        private static readonly Type RatkinDisasterUtilityType = AccessTools.TypeByName("MouseDisaster.MouseDisasterUtility");
        private static readonly MethodInfo RatkinDisasterGenerateFactionRatkinPawnMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "GenerateFactionRatkinPawn", typeof(PawnKindDef), typeof(Faction), typeof(DevelopmentalStage), typeof(float));
        private static readonly MethodInfo RatkinDisasterSetBiologicalAgeYearsMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "SetBiologicalAgeYears", typeof(Pawn), typeof(float));
        private static readonly MethodInfo RatkinDisasterPrepareTradablePrisonerMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "PrepareTradablePrisoner", typeof(Pawn), typeof(Faction));
        private static readonly MethodInfo RatkinDisasterClearTradeLeaderStateMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "ClearTradeLeaderState", typeof(Pawn));
        private static readonly MethodInfo RatkinDisasterResetBeggarStateMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "ResetBeggarState", typeof(Pawn));
        private static readonly MethodInfo RatkinDisasterMakeFactionHostileToPlayerMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "MakeFactionHostileToPlayer", typeof(Faction), typeof(bool));
        private static readonly MethodInfo RatkinDisasterTryRefreshHiddenFactionRelationsMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "TryRefreshHiddenFactionRelations");
        private static readonly MethodInfo RatkinDisasterNotifyIdentityChangedMethod = OptionalModApi.Resolve(RatkinDisasterUtilityType, "NotifyMouseDisasterPawnIdentityOrLifeStageChanged", typeof(Pawn));
        private static readonly Type RatkinDisasterVisitorControlType = AccessTools.TypeByName("MouseDisaster.MouseDisasterVisitorUtility") ?? AccessTools.TypeByName("MouseDisaster.GameComponent_MouseDisasterVisitorControl");
        private static readonly MethodInfo RatkinDisasterTryMakeHostileVisitorsMethod = OptionalModApi.Resolve(RatkinDisasterVisitorControlType, "TryMakeHostile", typeof(IEnumerable<Pawn>), typeof(int).MakeByRefType());
        private static readonly Type RimTalkPawnUtilType = AccessTools.TypeByName("RimTalk.Util.PawnUtil");
        private static readonly Type RimTalkCacheType = AccessTools.TypeByName("RimTalk.Data.Cache");
        private static readonly MethodInfo RimTalkIsTalkEligibleMethod = RimTalkPawnUtilType == null ? null : AccessTools.Method(RimTalkPawnUtilType, "IsTalkEligible", new[] { typeof(Pawn) });
        private static readonly MethodInfo RimTalkGetPlayerMethod = RimTalkCacheType == null ? null : AccessTools.Method(RimTalkCacheType, "GetPlayer");
        private static readonly Type RimTalkToddlerCarryingUtilityType = AccessTools.TypeByName("RimTalk_ToddlersExpansion.Integration.Toddlers.ToddlerCarryingUtility");
        private static readonly MethodInfo RimTalkDismountToddlerMethod = OptionalModApi.Resolve(RimTalkToddlerCarryingUtilityType, "DismountToddler", typeof(Pawn));

        public static readonly List<LeadYourPetInteractionKind> AutomaticPositiveInteractions = new List<LeadYourPetInteractionKind>
        {
            LeadYourPetInteractionKind.WatchWork,
            LeadYourPetInteractionKind.Fed,
            LeadYourPetInteractionKind.RestAtFeet,
            LeadYourPetInteractionKind.IdleNearOwner,
            LeadYourPetInteractionKind.StudyFloor,
            LeadYourPetInteractionKind.SillySmile,
            LeadYourPetInteractionKind.TailPetting,
            LeadYourPetInteractionKind.EarPetting,
            LeadYourPetInteractionKind.OwnerWaited,
            LeadYourPetInteractionKind.Snack
        };

        public static readonly List<LeadYourPetInteractionKind> BidirectionalInteractions = new List<LeadYourPetInteractionKind>
        {
            LeadYourPetInteractionKind.Kick,
            LeadYourPetInteractionKind.TapHead,
            LeadYourPetInteractionKind.PullTail,
            LeadYourPetInteractionKind.PullEar,
            LeadYourPetInteractionKind.Whirl,
            LeadYourPetInteractionKind.Slap,
            LeadYourPetInteractionKind.Pat,
            LeadYourPetInteractionKind.KickButt
        };

        public static readonly List<LeadYourPetInteractionKind> UnidirectionalInteractions = new List<LeadYourPetInteractionKind>
        {
            LeadYourPetInteractionKind.Observe,
            LeadYourPetInteractionKind.WatchWork,
            LeadYourPetInteractionKind.Fed,
            LeadYourPetInteractionKind.RestAtFeet,
            LeadYourPetInteractionKind.IdleNearOwner,
            LeadYourPetInteractionKind.StudyFloor,
            LeadYourPetInteractionKind.SillySmile,
            LeadYourPetInteractionKind.TailPetting,
            LeadYourPetInteractionKind.EarPetting,
            LeadYourPetInteractionKind.OwnerWaited,
            LeadYourPetInteractionKind.Snack
        };

        public static LeadYourPetGameComponent Component => Current.Game?.GetComponent<LeadYourPetGameComponent>();
        public static int MaxLeashLength => LeadYourPetMod.Settings == null ? 10 : (LeadYourPetMod.Settings.infiniteLeash ? 999 : LeadYourPetMod.Settings.maxLeashLength);
        public static float HardLeashLength => MaxLeashLength >= 999 ? 999f : MaxLeashLength * 1.5f;
        public static float FollowThreshold => Mathf.Max(2f, MaxLeashLength - 2f);
        public static int MaxYoungRatkinPetLeashStartDistance => LeadYourPetMod.Settings == null ? 10 : Mathf.Max(1, LeadYourPetMod.Settings.maxYoungRatkinPetLeashStartDistance);
        public static int MinPetAgeYears => LeadYourPetMod.Settings == null ? LeadYourPetSettings.DefaultMinPetAgeYears : LeadYourPetMod.Settings.minPetAgeYears;
        public static int MaxPetAgeYears => LeadYourPetMod.Settings == null ? LeadYourPetSettings.DefaultMaxPetAgeYears : LeadYourPetMod.Settings.maxPetAgeYears;
        public static bool AllowAnimalPets => LeadYourPetMod.Settings == null || LeadYourPetMod.Settings.allowAnimalPets;
        public static bool AllowRatkinYoungPets => LeadYourPetMod.Settings == null || LeadYourPetMod.Settings.allowRatkinYoungPets;
        public static bool VisitorsLeadRatkinYoung => LeadYourPetMod.Settings == null || LeadYourPetMod.Settings.visitorsLeadRatkinYoung;
        public static int OrdinaryTravelRatkinYoungCount => LeadYourPetMod.Settings == null ? LeadYourPetSettings.DefaultOrdinaryTravelRatkinYoungCount : LeadYourPetMod.Settings.ordinaryTravelRatkinYoungCount;

        public static bool IsPlayerCaravanMaster(Pawn pawn)
        {
            return pawn != null && (pawn.IsColonist || pawn.IsColonySubhumanPlayerControlled);
        }

        public static bool CanPlayerCommand(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && pawn.Faction == Faction.OfPlayer
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Awake();
        }

        public static bool IsEligibleAnimalPet(Pawn pawn)
        {
            return AllowAnimalPets
                && pawn != null
                && pawn.Spawned
                && pawn.Faction == Faction.OfPlayer
                && pawn.RaceProps.Animal
                && !pawn.Dead
                && !pawn.Downed
                && IsWithinConfiguredPetAge(pawn);
        }

        public static bool IsRatkinHumanlike(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            bool isRatkin = string.Equals(pawn.def.defName, "Ratkin", StringComparison.OrdinalIgnoreCase);
            if (!isRatkin && pawn.kindDef != null && pawn.kindDef.race != null)
            {
                isRatkin = string.Equals(pawn.kindDef.race.defName, "Ratkin", StringComparison.OrdinalIgnoreCase);
            }

            return isRatkin;
        }

        public static bool IsHumanlikeJuvenile(Pawn pawn)
        {
            return IsWithinConfiguredPetAge(pawn) && pawn.RaceProps.Humanlike;
        }

        public static bool IsWithinConfiguredPetAge(Pawn pawn)
        {
            return pawn != null
                && pawn.ageTracker != null
                && (LeadYourPetMod.Settings != null && LeadYourPetMod.Settings.ignorePetAge
                    || LeadYourPetRules.IsWithinPetAgeRange(pawn.ageTracker.AgeBiologicalYearsFloat, MinPetAgeYears, MaxPetAgeYears));
        }

        public static bool IsYoungRatkin(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            return LeadYourPetRules.ShouldTreatAsYoungRatkin(
                IsRatkinHumanlike(pawn),
                pawn.DevelopmentalStage.Baby(),
                pawn.DevelopmentalStage.Child(),
                HasMeaningfulYoungRatkinState(pawn),
                IsRatkinDisasterTravelPawn(pawn),
                IsHumanlikeJuvenile(pawn));
        }

        public static bool IsColonistYoungRatkin(Pawn pawn)
        {
            return pawn != null && pawn.IsColonist && IsYoungRatkin(pawn);
        }

        public static bool IsProtectedLeashedYoungRatkin(Pawn pawn)
        {
            if (pawn == null || Component == null)
            {
                return false;
            }

            bool isYoungRatkin = IsYoungRatkin(pawn);
            bool isBaby = pawn.DevelopmentalStage.Baby();
            bool isChild = pawn.DevelopmentalStage.Child();
            bool isHumanlikeJuvenile = IsHumanlikeJuvenile(pawn);
            if (!isYoungRatkin || (!isBaby && !isChild && !isHumanlikeJuvenile))
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            YoungRatkinState state = Component.GetYoungRatkinState(pawn);
            if (IsUnprotectedTravelStock(pawn) && IsNonPlayerTravelDeparture(pawn))
            {
                return false;
            }

            return LeadYourPetRules.ShouldTreatAsProtectedLeashedYoungRatkin(
                isYoungRatkin,
                isBaby,
                isChild,
                link != null && (link.Kind == LeashLinkKind.YoungRatkinPet || link.Kind == LeashLinkKind.RatkinMotherBaby),
                state != null && (state.IsPet || state.CurrentMaster != null),
                isHumanlikeJuvenile);
        }

        public static bool HasMeaningfulYoungRatkinState(Pawn pawn)
        {
            if (pawn == null || Component == null)
            {
                return false;
            }

            YoungRatkinState state = Component.GetYoungRatkinState(pawn);
            return state != null && (state.IsPet || state.CurrentMaster != null || state.IsTravelStock);
        }

        public static bool CanBePetYoungRatkin(Pawn pawn, out string reasonKey)
        {
            reasonKey = null;
            if (!AllowRatkinYoungPets)
            {
                reasonKey = "LeadYourPet_Reason_RatkinYoungPetsDisabled";
                return false;
            }

            if (pawn == null || pawn.ageTracker == null || !pawn.RaceProps.Humanlike)
            {
                reasonKey = "LeadYourPet_Reason_NotHumanlikeJuvenile";
                return false;
            }

            if (!IsWithinConfiguredPetAge(pawn))
            {
                reasonKey = pawn.ageTracker.AgeBiologicalYearsFloat < MinPetAgeYears
                    ? "LeadYourPet_Reason_TooYoung"
                    : "LeadYourPet_Reason_TooOld";
                return false;
            }

            if (pawn.Dead)
            {
                reasonKey = "LeadYourPet_Reason_Dead";
                return false;
            }

            return true;
        }

        public static bool CanStartPlayerPetLeash(Pawn master, Pawn pet, bool markAsYoungRatkinPet, out string reasonKey)
        {
            reasonKey = null;
            if (master == null || pet == null)
            {
                reasonKey = "LeadYourPet_Reason_InvalidTarget";
                return false;
            }

            if (markAsYoungRatkinPet && !CanBePetYoungRatkin(pet, out reasonKey))
            {
                return false;
            }

            if (markAsYoungRatkinPet && !CanStartYoungRatkinPetLeashAtDistance(master, pet))
            {
                reasonKey = "LeadYourPet_Reason_MouseEggTooFarToLeash";
                return false;
            }

            if (Component != null)
            {
                LeashLink existing = Component.GetLinkForPet(pet);
                if (LeadYourPetRules.ShouldRejectPlayerTakeoverOfNonPlayerLeash(
                    hasExistingLink: existing != null && existing.Master != null,
                    existingMasterIsDifferent: existing != null && existing.Master != master,
                    existingMasterIsPlayer: existing?.Master?.Faction == Faction.OfPlayer,
                    newMasterIsPlayer: master.Faction == Faction.OfPlayer))
                {
                    reasonKey = "LeadYourPet_Reason_LeashedByNonPlayer";
                    return false;
                }
            }

            return true;
        }

        public static bool CanStartYoungRatkinPetLeashAtDistance(Pawn master, Pawn pet)
        {
            if (master == null || pet == null || master.Map == null || pet.Map == null)
            {
                return false;
            }

            return LeadYourPetRules.CanStartYoungRatkinPetLeashAtDistance(
                sameMap: master.Map == pet.Map,
                distance: master.Position.DistanceTo(pet.Position),
                maxDistance: MaxYoungRatkinPetLeashStartDistance);
        }

        public static bool CanStartRatkinMotherLeash(Pawn mother, Pawn baby, out string reasonKey)
        {
            reasonKey = null;
            bool motherIsRatkinHumanlike = IsRatkinHumanlike(mother);
            bool babyIsRatkinHumanlike = IsRatkinHumanlike(baby);
            bool motherIsDead = mother?.Dead ?? true;
            bool babyIsDead = baby?.Dead ?? true;
            bool babyIsBabyStage = baby != null && baby.DevelopmentalStage.Baby();
            bool motherIsBabyStage = mother != null && mother.DevelopmentalStage.Baby();
            bool hasParentRelation = HasParentRelation(baby, mother);

            if (!LeadYourPetRules.CanFormOrMaintainRatkinMotherLeash(
                motherIsRatkinHumanlike,
                babyIsRatkinHumanlike,
                motherIsDead,
                babyIsDead,
                babyIsBabyStage,
                motherIsBabyStage,
                hasParentRelation))
            {
                if (!motherIsRatkinHumanlike || !babyIsRatkinHumanlike)
                {
                    reasonKey = "LeadYourPet_Reason_NotRatkinMouseEgg";
                    return false;
                }

                if (babyIsDead || motherIsDead)
                {
                    reasonKey = "LeadYourPet_Reason_Dead";
                    return false;
                }

                if (!babyIsBabyStage)
                {
                    reasonKey = "LeadYourPet_Reason_NotBaby";
                    return false;
                }

                if (motherIsBabyStage)
                {
                    reasonKey = "LeadYourPet_Reason_InvalidPetStatus";
                    return false;
                }

                if (!hasParentRelation)
                {
                    reasonKey = "LeadYourPet_Reason_NotMotherChild";
                    return false;
                }
            }

            if (Component != null)
            {
                LeashLink existing = Component.GetLinkForPet(baby);
                if (LeadYourPetRules.ShouldRejectNonPlayerLeashReplacement(
                    hasExistingLink: existing != null && existing.Master != null,
                    existingMasterIsDifferent: existing != null && existing.Master != mother,
                    existingMasterIsPlayer: existing?.Master?.Faction == Faction.OfPlayer))
                {
                    reasonKey = "LeadYourPet_Reason_LeashedByNonPlayer";
                    return false;
                }
            }

            return true;
        }

        public static bool ShouldBlockCarryOfProtectedYoungRatkin(Pawn carrier, Pawn target)
        {
            return !CanCarryForNonPlayerDeparture(carrier, target)
                && LeadYourPetRules.ShouldBlockProtectedCarry(IsProtectedLeashedYoungRatkin(target), allowColonistCarry: false);
        }

        public static bool ShouldAllowProtectedYoungRatkinPrisonerTransfer(Pawn carrier, Pawn target)
        {
            Job job = carrier?.CurJob;
            return LeadYourPetRules.ShouldAllowProtectedPawnPrisonerTransfer(
                IsProtectedLeashedYoungRatkin(target),
                jobMakesTargetPrisoner: job?.def?.makeTargetPrisoner == true,
                jobTargetMatchesPawn: job?.targetA.Thing == target);
        }

        public static bool ShouldBlockExternalToddlerPickup(Pawn target, string jobDefName)
        {
            return target != null && LeadYourPetRules.ShouldBlockExternalToddlerPickup(
                IsProtectedLeashedYoungRatkin(target),
                jobDefName);
        }

        public static bool ShouldEndExternalToddlerHoldJob(Pawn jobPawn, Pawn protectedPawn)
        {
            if (jobPawn == null || protectedPawn == null)
            {
                return false;
            }

            Job curJob = jobPawn.CurJob;
            if (jobPawn != protectedPawn
                && curJob?.targetA.Thing != protectedPawn
                && curJob?.targetB.Thing != protectedPawn
                && curJob?.targetC.Thing != protectedPawn)
            {
                return false;
            }

            return LeadYourPetRules.ShouldEndExternalToddlerHoldJob(
                IsProtectedLeashedYoungRatkin(protectedPawn),
                jobPawn.jobs?.curDriver?.GetType().FullName);
        }

        public static bool IsExternalToddlerHold(string jobDefName, string driverTypeName)
        {
            return LeadYourPetRules.IsExternalToddlerHoldJob(jobDefName)
                || LeadYourPetRules.IsExternalToddlerHoldDriver(driverTypeName);
        }

        private static int cachedDepartureLordId = int.MinValue;
        private static LordToil cachedDepartureToilRef;
        private static bool cachedDepartureResult;

        internal static void ClearDepartureToilCache()
        {
            cachedDepartureLordId = int.MinValue;
            cachedDepartureToilRef = null;
        }

        public static bool ShouldPreserveMapExitMovement(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            Lord lord = pawn.GetLord();
            string dutyName = pawn.mindState?.duty?.def?.defName;
            return LeadYourPetRules.ShouldPreserveMapExitJob(
                currentJobExitsMap: pawn.CurJob != null && pawn.CurJob.exitMapOnArrival,
                currentDutyExitsMap: !string.IsNullOrEmpty(dutyName) && dutyName.StartsWith("ExitMap", StringComparison.Ordinal),
                lordToilExitsMap: IsDepartureToil(lord, lord?.CurLordToil));
        }

        internal static bool IsDepartureToil(Lord lord, LordToil toil)
        {
            if (toil == null)
            {
                return false;
            }

            int lordId = lord?.loadID ?? 0;
            if (cachedDepartureLordId == lordId && ReferenceEquals(cachedDepartureToilRef, toil))
            {
                return cachedDepartureResult;
            }

            bool departing = toil is LordToil_ExitMap || toil is LordToil_TakeWoundedGuest
                || toil.GetType().Name.StartsWith("LordToil_ExitMap", StringComparison.Ordinal)
                || (lord?.LordJob is LordJob_TravelAndExit && toil is LordToil_Travel)
                || (lord?.LordJob is LordJob_VisitColony visit
                    && visit.exitSubgraph != null && visit.exitSubgraph.lordToils.Contains(toil));
            cachedDepartureLordId = lordId;
            cachedDepartureToilRef = toil;
            cachedDepartureResult = departing;
            return departing;
        }

        internal static bool IsNonPlayerTravelDeparture(Pawn pawn)
        {
            Lord lord = pawn?.GetLord();
            if (lord?.faction == null || lord.faction.IsPlayer || pawn.Faction?.IsPlayer == true || pawn.IsPrisonerOfColony)
            {
                return false;
            }

            return IsDepartureToil(lord, lord.CurLordToil);
        }

        internal static bool IsUnprotectedTravelStock(Pawn pawn)
        {
            YoungRatkinState state = Component?.GetYoungRatkinState(pawn);
            return state?.IsTravelStock == true && pawn.Faction?.IsPlayer != true
                && !pawn.IsPrisonerOfColony && state.CurrentMaster?.Faction?.IsPlayer != true
                && Component.GetLinkForPet(pawn)?.Master?.Faction?.IsPlayer != true;
        }

        internal static bool CanCarryForNonPlayerDeparture(Pawn carrier, Pawn target)
        {
            return carrier != null && target != null && carrier != target
                && IsNonPlayerTravelDeparture(carrier) && IsNonPlayerTravelDeparture(target)
                && carrier.GetLord() == target.GetLord()
                && IsUnprotectedTravelStock(target);
        }

        internal static bool CanPerformDepartureHold(Pawn actor, Pawn target, string jobName)
        {
            return CanCarryForNonPlayerDeparture(actor, target)
                || (jobName != null && jobName.StartsWith("RimTalk_BeingCarried", StringComparison.Ordinal)
                    && CanCarryForNonPlayerDeparture(target, actor));
        }

        public static bool TryDismountExternalToddlerHold(Pawn pawn)
        {
            if (pawn == null || RimTalkDismountToddlerMethod == null)
            {
                return false;
            }

            try
            {
                return OptionalModApi.Invoke(RimTalkDismountToddlerMethod, pawn) as bool? == true;
            }
            catch (System.Exception exception)
            {
                Log.Warning("[LeadYourPet] Failed to dismount RimTalk toddler hold.\n" + exception);
                return false;
            }
        }

        public static bool ShouldBlockExternalDialogue(Pawn target, string integrationName)
        {
            return target != null && LeadYourPetRules.ShouldBlockExternalDialogue(
                IsProtectedLeashedYoungRatkin(target),
                integrationName);
        }

        public static bool ShouldTreatYoungRatkinAsRimTalkEligible(Pawn pawn, bool originalEligible)
        {
            return LeadYourPetRules.ShouldTreatYoungRatkinAsRimTalkEligible(
                originalEligible,
                IsYoungRatkin(pawn),
                IsRimTalkConversationPawnValid(pawn));
        }

        public static bool ShouldAllowRimTalkYoungRatkinConversation(Pawn initiator, Pawn target, bool originalAllowed)
        {
            return LeadYourPetRules.ShouldAllowRimTalkYoungRatkinConversation(
                originalAllowed,
                IsYoungRatkin(target),
                IsRimTalkConversationPawnValid(initiator),
                IsRimTalkConversationPawnValid(target),
                IsRimTalkTalkEligible(initiator),
                IsRimTalkPlayerPawn(initiator),
                CanReachForRimTalkConversation(initiator, target));
        }

        public static bool ShouldOverrideBabyCryMoodForYoungRatkin(Pawn source, Pawn hearer)
        {
            return LeadYourPetRules.ShouldOverrideBabyCryMoodForYoungRatkin(
                IsYoungRatkin(source),
                IsYoungRatkinPetState(source),
                IsYoungRatkin(hearer),
                hearer?.needs?.mood?.thoughts?.memories != null);
        }

        public static bool ShouldApplyYoungRatkinCryingNearbyMood(Pawn source, Pawn hearer)
        {
            return LeadYourPetRules.ShouldOverrideBabyCryMoodForYoungRatkin(
                IsYoungRatkin(source),
                IsYoungRatkinPetState(source),
                IsYoungRatkin(hearer),
                hearer?.needs?.mood?.thoughts?.memories != null);
        }

        public static bool IsYoungRatkinPetState(Pawn pawn)
        {
            YoungRatkinState state = Component?.GetYoungRatkinState(pawn);
            return state != null && state.IsPet;
        }

        private static bool IsRimTalkConversationPawnValid(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead;
        }

        private static bool IsRimTalkTalkEligible(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (RimTalkIsTalkEligibleMethod == null)
            {
                return pawn.RaceProps?.Humanlike == true && IsRimTalkConversationPawnValid(pawn);
            }

            try
            {
                return RimTalkIsTalkEligibleMethod.Invoke(null, new object[] { pawn }) is bool result && result;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsRimTalkPlayerPawn(Pawn pawn)
        {
            if (pawn == null || RimTalkGetPlayerMethod == null)
            {
                return false;
            }

            try
            {
                return RimTalkGetPlayerMethod.Invoke(null, null) is Pawn player && player == pawn;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanReachForRimTalkConversation(Pawn initiator, Pawn target)
        {
            if (initiator == null || target == null)
            {
                return false;
            }

            return initiator.CanReach(target, PathEndMode.Touch, Danger.Some);
        }

        public static bool HasParentRelation(Pawn child, Pawn parent)
        {
            if (child == null || parent == null || child.relations == null)
            {
                return false;
            }

            if (child.GetMother() == parent || child.GetFather() == parent)
            {
                return true;
            }

            return child.relations.DirectRelations != null &&
                   child.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Parent && r.otherPawn == parent);
        }

        private static bool IsFriendlyVisitorOrTraveler(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Faction == null || pawn.Faction == Faction.OfPlayer || pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            Lord lord = pawn.GetLord();
            return lord != null && (lord.LordJob is LordJob_TradeWithColony || lord.LordJob is LordJob_VisitColony);
        }

        public static bool IsNonHostileAdultVisitor(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            if (!pawn.DevelopmentalStage.Adult())
            {
                return false;
            }

            return IsFriendlyVisitorOrTraveler(pawn);
        }

        public static bool IsFriendlyVisitorYoungRatkin(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !IsYoungRatkin(pawn))
            {
                return false;
            }

            return IsFriendlyVisitorOrTraveler(pawn) || (pawn.GetLord()?.LordJob is LordJob_TradeWithColony || pawn.GetLord()?.LordJob is LordJob_VisitColony);
        }

        internal static bool AllowsLeashedHostileAttack(LeashLink link)
        {
            if (LeadYourPetMod.Settings?.leashedHostilesCanAttack != true || link?.Pet == null || link.Master == null)
            {
                return false;
            }

            return link.Pet.HostileTo(link.Master);
        }

        internal static bool HasFixedAnchor(LeashLink link)
        {
            return link != null && (link.AnchorMode == LeashAnchorMode.Cell
                || (link.AnchorMode == LeashAnchorMode.Thing && !(link.AnchorThing is Pawn)));
        }

        internal static bool HasLeashCareJob(LeashLink link)
        {
            Pawn pet = link?.Pet;
            return pet?.CurJob != null && (pet.CurJobDef == JobDefOf.Ingest
                || pet.CurJobDef == JobDefOf.LayDown || pet.CurJobDef == JobDefOf.LayDownResting
                || pet.CurJobDef == JobDefOf.LayDownAwake || pet.CurJobDef == JobDefOf.Deathrest
                || pet.jobs?.curDriver?.asleep == true);
        }

        public static bool IsSuppressedAgainstMasterFaction(Pawn pet, Faction faction)
        {
            if (LeadYourPetMod.Settings?.leashedHostilesCanAttack == true || pet == null || faction == null || Component == null)
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pet);
            return link != null && link.Master != null && link.Master.Faction == faction;
        }

        public static bool IsSuppressedAgainstThing(Thing a, Thing b)
        {
            if (LeadYourPetMod.Settings?.leashedHostilesCanAttack == true || a == null || b == null || Component == null)
            {
                return false;
            }

            Pawn pawnA = a as Pawn;
            Pawn pawnB = b as Pawn;

            if (pawnA != null)
            {
                LeashLink linkA = Component.GetLinkForPet(pawnA);
                if (linkA != null && linkA.Master != null && (b == linkA.Master || b.Faction == linkA.Master.Faction))
                {
                    return true;
                }
            }

            if (pawnB != null)
            {
                LeashLink linkB = Component.GetLinkForPet(pawnB);
                if (linkB != null && linkB.Master != null && (a == linkB.Master || a.Faction == linkB.Master.Faction))
                {
                    return true;
                }
            }

            return false;
        }

        // Keep the legacy overload for callers compiled against earlier Continued builds.
        public static Pawn GenerateYoungRatkinPawn(Faction ignoredFaction, Map map)
        {
            return GenerateYoungRatkinPawn(map);
        }

        public static Pawn GenerateYoungRatkinPawn(Map map)
        {
            PawnKindDef kind = ResolveTravelYoungRatkinPawnKind();
            if (kind == null)
            {
                return null;
            }

            // Caravan membership is tracked by the Lord and leash state; the generated egg itself is factionless.
            Pawn ratkinDisasterPawn = TryGenerateRatkinDisasterTravelPawn(kind);
            if (ratkinDisasterPawn != null)
            {
                ClearGeneratedTravelYoungRatkinFaction(ratkinDisasterPawn);
                return ratkinDisasterPawn;
            }

            float randomAge = ResolveGeneratedTravelAge(kind.defName == RatkinDisasterChildPawnKindDefName);
            // 这里不能把非鼠族来访派系直接传给 PawnGenerator，否则会被派系的人种/Kind 规则污染，生成成普通智人。
            PawnGenerationRequest request = new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true, allowDead: false, allowDowned: true, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, fixedBiologicalAge: randomAge, developmentalStages: DevelopmentalStage.Baby | DevelopmentalStage.Child, forceNoBackstory: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);
            ClearGeneratedTravelYoungRatkinFaction(pawn);

            return pawn;
        }

        private static void ClearGeneratedTravelYoungRatkinFaction(Pawn pawn)
        {
            if (LeadYourPetRules.ShouldClearGeneratedTravelYoungRatkinFaction(
                hasGeneratedPawn: pawn != null,
                hasFaction: pawn?.Faction != null))
            {
                pawn.SetFaction(null);
            }
        }

        internal static float ResolveGeneratedTravelAge(bool ratkinDisasterChildKind)
        {
            float min = MinPetAgeYears;
            float maxExclusive = Mathf.Max(min + 0.01f, MaxPetAgeYears);
            if (ratkinDisasterChildKind)
            {
                float lower = Mathf.Max(1f, min);
                float upper = Mathf.Min(6.9f, maxExclusive - 0.01f);
                if (upper < lower)
                {
                    lower = min;
                    upper = maxExclusive - 0.01f;
                }

                return Rand.Range(lower, upper);
            }

            return Rand.Range(min, maxExclusive - 0.01f);
        }

        private static Pawn TryGenerateRatkinDisasterTravelPawn(PawnKindDef kind)
        {
            if (!ShouldUseRatkinDisasterTravelPawn() || RatkinDisasterGenerateFactionRatkinPawnMethod == null)
            {
                return null;
            }

            try
            {
                Pawn pawn = OptionalModApi.Invoke(RatkinDisasterGenerateFactionRatkinPawnMethod, kind, null, DevelopmentalStage.Baby, 0.35f) as Pawn;
                if (pawn == null)
                {
                    return null;
                }

                if (kind.defName == RatkinDisasterChildPawnKindDefName && RatkinDisasterSetBiologicalAgeYearsMethod != null)
                {
                    RatkinDisasterSetBiologicalAgeYearsMethod.Invoke(null, new object[] { pawn, Rand.Range(1f, 2.9f) });
                }

                PawnComponentsUtility.AddAndRemoveDynamicComponents(pawn, true);
                return pawn;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] MouseDisaster generation failed: " + exception, 19462001);
                return null;
            }
        }

        public static bool TryPrepareRatkinDisasterTradablePrisoner(Pawn pawn, Faction ownerFaction)
        {
            if (RatkinDisasterPrepareTradablePrisonerMethod == null || pawn == null || ownerFaction == null)
            {
                return false;
            }

            try
            {
                object result = RatkinDisasterPrepareTradablePrisonerMethod.Invoke(null, new object[] { pawn, ownerFaction });
                return result is bool ok && ok;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Prisoner integration failed: " + exception, 19462002);
                return false;
            }
        }

        private static PawnKindDef ResolveTravelYoungRatkinPawnKind()
        {
            if (ShouldUseRatkinDisasterTravelPawn())
            {
                PawnKindDef ratkinDisasterKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(RatkinDisasterChildPawnKindDefName);
                if (ratkinDisasterKind != null)
                {
                    return ratkinDisasterKind;
                }
            }

            return DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(x => x.race != null && string.Equals(x.race.defName, "Ratkin", StringComparison.OrdinalIgnoreCase) && !x.trader);
        }

        public static bool IsRatkinDisasterTravelPawn(Pawn pawn)
        {
            return pawn?.kindDef != null && string.Equals(pawn.kindDef.defName, RatkinDisasterChildPawnKindDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRatkinDisasterTravelTrader(Pawn pawn)
        {
            return pawn?.kindDef != null && string.Equals(pawn.kindDef.defName, RatkinDisasterTraderPawnKindDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsRatkinDisasterBeggarAdult(Pawn pawn)
        {
            return pawn?.kindDef != null && string.Equals(pawn.kindDef.defName, "MouseDisaster_BeggarRatkinAdult", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldUseRatkinDisasterTravelPawn()
        {
            if (!ModsConfig.IsActive(RatkinDisasterPackageId) && !ModsConfig.IsActive("nanaloveyuki.mouse.disaster.famine.continued"))
            {
                return false;
            }

            bool? beggarEnabled = TryGetRatkinDisasterBoolProperty("AreBeggarIncidentsEnabled");
            bool? wildEnabled = TryGetRatkinDisasterBoolProperty("AreWildIncidentsEnabled");
            bool? thiefEnabled = TryGetRatkinDisasterBoolProperty("AreThiefIncidentsEnabled");

            if (beggarEnabled.HasValue || wildEnabled.HasValue || thiefEnabled.HasValue)
            {
                return beggarEnabled.GetValueOrDefault() || wildEnabled.GetValueOrDefault() || thiefEnabled.GetValueOrDefault();
            }

            return true;
        }

        private static bool? TryGetRatkinDisasterBoolProperty(string propertyName)
        {
            Type utilityType = AccessTools.TypeByName("MouseDisaster.MouseDisasterUtility");
            PropertyInfo property = utilityType?.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool))
            {
                return null;
            }

            try
            {
                return (bool)property.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        public static bool ShouldDrag(LeashLink link)
        {
            if (link == null || link.Master == null || link.Pet == null || HasFixedAnchor(link)
                || AllowsLeashedHostileAttack(link) || !link.Master.Spawned || !link.Pet.Spawned || link.Master.Map != link.Pet.Map)
            {
                return false;
            }

            Thing targetThing = GetCurrentFollowThing(link);
            IntVec3 targetCell = targetThing != null ? targetThing.Position : GetAnchorCell(link);
            if (!targetCell.IsValid)
            {
                targetCell = link.Master.Position;
            }

            float distance = targetCell.DistanceTo(link.Pet.Position);
            if ((link.Kind == LeashLinkKind.YoungRatkinPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !CanYoungRatkinMove(link.Pet))
            {
                return MasterControlsMovement(link) && distance >= MaxLeashLength - 0.5f;
            }

            if (distance < MaxLeashLength - 2f || !MasterControlsMovement(link))
            {
                return false;
            }

            float masterSpeed = Mathf.Max(1f, link.Master.TicksPerMoveCardinal);
            float petSpeed = Mathf.Max(1f, link.Pet.TicksPerMoveCardinal);
            return masterSpeed < petSpeed * 0.75f;
        }

        private static bool MasterControlsMovement(LeashLink link)
        {
            if (link.AnchorMode != LeashAnchorMode.None)
            {
                return true;
            }

            return link.Master != null && link.Master.pather != null && link.Master.pather.Moving;
        }

        public static bool CanYoungRatkinMove(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || pawn.health?.capacities == null)
            {
                return false;
            }

            if (LifeStageUtility.AlwaysDowned(pawn))
            {
                return false;
            }

            return pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        }

        public static bool MasterSleeping(Pawn master)
        {
            return master != null && (!master.Awake() || master.CurJobDef == JobDefOf.LayDown || master.CurJobDef == JobDefOf.LayDownResting || master.CurJobDef == JobDefOf.LayDownAwake);
        }

        public static bool MasterEating(Pawn master)
        {
            if (master == null)
            {
                return false;
            }

            if (master.CurJobDef == JobDefOf.Ingest)
            {
                return true;
            }

            return master.jobs?.curDriver is JobDriver_Ingest;
        }

        public static Thing GetCurrentFollowThing(LeashLink link)
        {
            if (link == null)
            {
                return null;
            }

            if (link.AnchorMode == LeashAnchorMode.Thing)
            {
                return link.AnchorThing;
            }

            return link.AnchorMode == LeashAnchorMode.None ? link.Master : null;
        }

        public static IntVec3 GetAnchorCell(LeashLink link)
        {
            if (link == null)
            {
                return IntVec3.Invalid;
            }

            if (link.AnchorMode == LeashAnchorMode.Thing && link.AnchorThing != null)
            {
                return link.AnchorThing.Position;
            }

            if (link.AnchorMode == LeashAnchorMode.Cell)
            {
                return link.AnchorCell;
            }

            return IntVec3.Invalid;
        }

        public static bool TryGetSpecialRenderData(Pawn pawn, out Vector3 drawPos, out float angle, out bool laying, out Rot4 facing)
        {
            drawPos = default(Vector3);
            angle = 0f;
            laying = false;
            facing = Rot4.South;

            if (pawn == null || Component == null)
            {
                return false;
            }

            if (Component.TryGetInteractionAnimationRenderData(pawn, out drawPos, out angle, out laying, out facing))
            {
                return true;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            if (link == null || !link.IsDragging || link.Master == null || !link.Master.Spawned)
            {
                return false;
            }

            Rot4 rot = link.Master.Rotation;
            drawPos = pawn.DrawPos;
            drawPos.y = AltitudeLayer.LayingPawn.AltitudeFor();
            angle = (rot == Rot4.West || rot == Rot4.North) ? 290f : 70f;
            laying = true;
            facing = Rot4.South;
            return true;
        }

        public static bool TryGetCustomRopeTarget(Pawn pawn, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (pawn == null || !pawn.Spawned || Component == null)
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            if (link == null || GodHandsCompat.IsGrabbed(pawn) || GodHandsCompat.IsGrabbed(link.Master))
            {
                return false;
            }

            Thing followThing = GetCurrentFollowThing(link);
            if (followThing is Pawn followedPawn && GodHandsCompat.IsGrabbed(followedPawn))
            {
                return false;
            }
            if (followThing != null && followThing != pawn && followThing.Spawned && followThing.Map == pawn.Map
                && pawn.Position.DistanceTo(followThing.Position) <= HardLeashLength)
            {
                target = followThing;
                return true;
            }

            IntVec3 anchorCell = GetAnchorCell(link);
            if (anchorCell.IsValid && anchorCell.InBounds(pawn.Map)
                && pawn.Position.DistanceTo(anchorCell) <= HardLeashLength)
            {
                target = anchorCell;
                return true;
            }

            return false;
        }

        public static Vector3 GetVisualDrawPos(Thing thing)
        {
            if (thing is Pawn pawn)
            {
                if (TryGetSpecialRenderData(pawn, out Vector3 drawPos, out float _, out bool _, out Rot4 _))
                {
                    return drawPos;
                }

                return pawn.DrawPos;
            }

            return thing?.DrawPos ?? Vector3.zero;
        }

        public static void MaintainLeashMood(Pawn master, Pawn pet)
        {
            LeashLink link = Component?.GetLinkForPet(pet);
            LeashLinkKind kind = link?.Kind ?? LeashLinkKind.NormalPet;
            bool caretakerMood = kind == LeashLinkKind.YoungRatkinPet && ShouldUseCaretakerMood(pet);

            if (master?.needs?.mood?.thoughts?.memories != null)
            {
                if (!caretakerMood)
                {
                    master.needs.mood.thoughts.memories.TryGainMemoryFast(LeadYourPetDefOf.LeadYourPet_LeadingPetMood);
                }
                else
                {
                    master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_LeadingPetMood);
                }

                if (caretakerMood)
                {
                    master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_YoungRatkinOwnerMood);
                }
                else if (kind == LeashLinkKind.YoungRatkinPet)
                {
                    master.needs.mood.thoughts.memories.TryGainMemoryFast(LeadYourPetDefOf.LeadYourPet_YoungRatkinOwnerMood);
                }
                else
                {
                    master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_YoungRatkinOwnerMood);
                }
            }

            if (pet?.needs?.mood?.thoughts?.memories != null)
            {
                if (caretakerMood)
                {
                    pet.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_YoungRatkinFearMood);
                }
                else if (kind == LeashLinkKind.YoungRatkinPet)
                {
                    pet.needs.mood.thoughts.memories.TryGainMemoryFast(LeadYourPetDefOf.LeadYourPet_YoungRatkinFearMood);
                }
                else
                {
                    pet.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_YoungRatkinFearMood);
                }
            }
        }

        public static void ClearLeashMood(Pawn master, Pawn pet)
        {
            if (master?.needs?.mood?.thoughts?.memories != null)
            {
                master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_LeadingPetMood);
                master.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_YoungRatkinOwnerMood);
            }

            if (pet?.needs?.mood?.thoughts?.memories != null)
            {
                pet.needs.mood.thoughts.memories.RemoveMemoriesOfDef(LeadYourPetDefOf.LeadYourPet_YoungRatkinFearMood);
            }
        }

        public static string InteractionLabel(LeadYourPetInteractionKind kind)
        {
            return InteractionLabel(null, kind);
        }

        public static string InteractionLabel(Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (IsColonistYoungRatkin(pet))
            {
                switch (kind)
                {
                    case LeadYourPetInteractionKind.Kick: return "LeadYourPet_Interaction_Colonist_Kick".Translate().Resolve();
                    case LeadYourPetInteractionKind.Observe: return "LeadYourPet_Interaction_Colonist_Observe".Translate().Resolve();
                    case LeadYourPetInteractionKind.Drag: return "LeadYourPet_Interaction_Colonist_Drag".Translate().Resolve();
                    case LeadYourPetInteractionKind.TapHead: return "LeadYourPet_Interaction_Colonist_TapHead".Translate().Resolve();
                    case LeadYourPetInteractionKind.PullTail: return "LeadYourPet_Interaction_Colonist_PullTail".Translate().Resolve();
                    case LeadYourPetInteractionKind.PullEar: return "LeadYourPet_Interaction_Colonist_PullEar".Translate().Resolve();
                    case LeadYourPetInteractionKind.Whirl: return "LeadYourPet_Interaction_Colonist_Whirl".Translate().Resolve();
                    case LeadYourPetInteractionKind.Slap: return "LeadYourPet_Interaction_Colonist_Slap".Translate().Resolve();
                    case LeadYourPetInteractionKind.Pat: return "LeadYourPet_Interaction_Colonist_Pat".Translate().Resolve();
                    case LeadYourPetInteractionKind.KickButt: return "LeadYourPet_Interaction_Colonist_KickButt".Translate().Resolve();
                    case LeadYourPetInteractionKind.WatchWork: return "LeadYourPet_Interaction_Colonist_WatchWork".Translate().Resolve();
                    case LeadYourPetInteractionKind.Fed: return "LeadYourPet_Interaction_Colonist_Fed".Translate().Resolve();
                    case LeadYourPetInteractionKind.RestAtFeet: return "LeadYourPet_Interaction_Colonist_RestAtFeet".Translate().Resolve();
                    case LeadYourPetInteractionKind.IdleNearOwner: return "LeadYourPet_Interaction_Colonist_IdleNearOwner".Translate().Resolve();
                    case LeadYourPetInteractionKind.StudyFloor: return "LeadYourPet_Interaction_Colonist_StudyFloor".Translate().Resolve();
                    case LeadYourPetInteractionKind.SillySmile: return "LeadYourPet_Interaction_Colonist_SillySmile".Translate().Resolve();
                    case LeadYourPetInteractionKind.TailPetting: return "LeadYourPet_Interaction_Colonist_TailPetting".Translate().Resolve();
                    case LeadYourPetInteractionKind.EarPetting: return "LeadYourPet_Interaction_Colonist_EarPetting".Translate().Resolve();
                    case LeadYourPetInteractionKind.OwnerWaited: return "LeadYourPet_Interaction_Colonist_OwnerWaited".Translate().Resolve();
                    case LeadYourPetInteractionKind.Snack: return "LeadYourPet_Interaction_Colonist_Snack".Translate().Resolve();
                }
            }
            switch (kind)
            {
                case LeadYourPetInteractionKind.Kick: return "LeadYourPet_Interaction_Kick".Translate().Resolve();
                case LeadYourPetInteractionKind.Observe: return "LeadYourPet_Interaction_Observe".Translate().Resolve();
                case LeadYourPetInteractionKind.Drag: return "LeadYourPet_Interaction_Drag".Translate().Resolve();
                case LeadYourPetInteractionKind.TapHead: return "LeadYourPet_Interaction_TapHead".Translate().Resolve();
                case LeadYourPetInteractionKind.PullTail: return "LeadYourPet_Interaction_PullTail".Translate().Resolve();
                case LeadYourPetInteractionKind.PullEar: return "LeadYourPet_Interaction_PullEar".Translate().Resolve();
                case LeadYourPetInteractionKind.Whirl: return "LeadYourPet_Interaction_Whirl".Translate().Resolve();
                case LeadYourPetInteractionKind.Slap: return "LeadYourPet_Interaction_Slap".Translate().Resolve();
                case LeadYourPetInteractionKind.Pat: return "LeadYourPet_Interaction_Pat".Translate().Resolve();
                case LeadYourPetInteractionKind.KickButt: return "LeadYourPet_Interaction_KickButt".Translate().Resolve();
                case LeadYourPetInteractionKind.WatchWork: return "LeadYourPet_Interaction_WatchWork".Translate().Resolve();
                case LeadYourPetInteractionKind.Fed: return "LeadYourPet_Interaction_Fed".Translate().Resolve();
                case LeadYourPetInteractionKind.RestAtFeet: return "LeadYourPet_Interaction_RestAtFeet".Translate().Resolve();
                case LeadYourPetInteractionKind.IdleNearOwner: return "LeadYourPet_Interaction_IdleNearOwner".Translate().Resolve();
                case LeadYourPetInteractionKind.StudyFloor: return "LeadYourPet_Interaction_StudyFloor".Translate().Resolve();
                case LeadYourPetInteractionKind.SillySmile: return "LeadYourPet_Interaction_SillySmile".Translate().Resolve();
                case LeadYourPetInteractionKind.TailPetting: return "LeadYourPet_Interaction_TailPetting".Translate().Resolve();
                case LeadYourPetInteractionKind.EarPetting: return "LeadYourPet_Interaction_EarPetting".Translate().Resolve();
                case LeadYourPetInteractionKind.OwnerWaited: return "LeadYourPet_Interaction_OwnerWaited".Translate().Resolve();
                case LeadYourPetInteractionKind.Snack: return "LeadYourPet_Interaction_Snack".Translate().Resolve();
                default: return kind.ToString();
            }
        }

        public static string GetYoungRatkinDutyReport(LeashedYoungRatkinDutyState state)
        {
            switch (state)
            {
                case LeashedYoungRatkinDutyState.SleepAtLeash:
                    return "LeadYourPet_Duty_SleepAtLeash".Translate().Resolve();
                case LeashedYoungRatkinDutyState.WaitForFeeding:
                    return "LeadYourPet_Duty_WaitForFeeding".Translate().Resolve();
                default:
                    return "LeadYourPet_Duty_FollowMaster".Translate().Resolve();
            }
        }

        public static bool TryGetYoungRatkinDutyReport(Pawn pawn, out string report)
        {
            report = null;
            if (pawn == null || Component == null)
            {
                return false;
            }

            LeashLink link = Component.GetLinkForPet(pawn);
            if (link == null || link.Kind != LeashLinkKind.YoungRatkinPet || link.AnchorMode != LeashAnchorMode.None)
            {
                return false;
            }

            report = GetYoungRatkinDutyReport(link.YoungRatkinDutyState);
            return !report.NullOrEmpty();
        }

        public static bool IsPositiveInteraction(LeadYourPetInteractionKind kind)
        {
            return (int)kind >= 10;
        }

        public static bool IsBidirectionalInteraction(LeadYourPetInteractionKind kind)
        {
            return BidirectionalInteractions.Contains(kind);
        }

        public static int OpinionOf(Pawn source, Pawn other)
        {
            return source?.relations?.OpinionOf(other) ?? 0;
        }

        public static bool CanUseSingleDirectionInteractions(Pawn master, Pawn pet)
        {
            return OpinionOf(master, pet) > 20 && OpinionOf(pet, master) > 20;
        }

        public static bool CanUseAsTraderChattel(Pawn pawn)
        {
            YoungRatkinState state = Component?.GetYoungRatkinState(pawn);
            return state != null && state.IsTravelStock && state.SellAsPrisoner;
        }

        private static readonly Dictionary<int, bool> tradeableYoungRatkinCache = new Dictionary<int, bool>();

        internal static void ClearTradeableYoungRatkinCache()
        {
            tradeableYoungRatkinCache.Clear();
        }

        internal static bool TryGetCachedTradeableYoungRatkin(int thingId, out bool tradeable)
        {
            return tradeableYoungRatkinCache.TryGetValue(thingId, out tradeable);
        }

        internal static void CacheTradeableYoungRatkin(int thingId, bool tradeable)
        {
            tradeableYoungRatkinCache[thingId] = tradeable;
        }

        public static bool IsTradeableYoungRatkin(Pawn pawn)
        {
            if (pawn == null || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            if (CanUseAsTraderChattel(pawn))
            {
                return true;
            }

            return IsYoungRatkin(pawn)
                && pawn.guest != null
                && ((pawn.IsPrisonerOfColony && pawn.guest.PrisonerIsSecure)
                    || (pawn.IsSlave && pawn.guest.SlaveIsSecure));
        }

        public static BoughtYoungRatkinStatus BoughtYoungRatkinStatus
        {
            get
            {
                return LeadYourPetRules.ResolveBoughtYoungRatkinStatus(
                    LeadYourPetMod.Settings == null
                        ? BoughtYoungRatkinStatus.Prisoner
                        : LeadYourPetMod.Settings.boughtYoungRatkinStatus,
                    ModsConfig.IdeologyActive);
            }
        }

        public static bool ShouldBreakNonPlayerTravelLeashAfterAcquisition(LeashLink link)
        {
            if (link?.Pet == null || link.Master == null)
            {
                return false;
            }

            YoungRatkinState state = Component?.GetYoungRatkinState(link.Pet);
            return YoungRatkinOwnershipStateMachine.ShouldDetachNonPlayerTravelLeashAfterAcquisition(
                masterIsPlayer: link.Master.Faction == Faction.OfPlayer,
                petIsPlayerOwned: link.Pet.Faction == Faction.OfPlayer,
                petIsColonyPrisoner: link.Pet.IsPrisonerOfColony,
                isTravelStock: state != null && state.IsTravelStock);
        }

        public static bool ShouldUseCaretakerMood(Pawn pet)
        {
            return IsColonistYoungRatkin(pet);
        }

        public static bool ShouldBlockColonistBidirectionalInteraction(Pawn master, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (!IsBidirectionalInteraction(kind) || !IsColonistYoungRatkin(pet))
            {
                return false;
            }

            return JoyIsFull(master) && RecreationNeedIsFull(pet);
        }

        public static bool JoyIsFull(Pawn pawn)
        {
            return pawn?.needs?.joy != null && pawn.needs.joy.CurLevelPercentage >= 0.999f;
        }

        public static bool RecreationNeedIsFull(Pawn pawn)
        {
            if (pawn?.needs == null)
            {
                return false;
            }

            if (pawn.needs.play != null)
            {
                return pawn.needs.play.CurLevelPercentage >= 0.999f;
            }

            return JoyIsFull(pawn);
        }

        public static IEnumerable<LeadYourPetInteractionKind> AvailableInteractionsFor(Pawn master, Pawn pet, IEnumerable<LeadYourPetInteractionKind> source)
        {
            foreach (LeadYourPetInteractionKind kind in source)
            {
                if (CanTriggerInteraction(master, pet, kind, out _))
                {
                    yield return kind;
                }
            }
        }

        public static bool CanTriggerInteraction(Pawn master, Pawn pet, LeadYourPetInteractionKind kind, out string reasonKey, bool manual = false)
        {
            if (!CanPerformInteraction(pet, kind, out reasonKey, manual))
            {
                return false;
            }

            if (ShouldBlockColonistBidirectionalInteraction(master, pet, kind))
            {
                reasonKey = null;
                return false;
            }

            return true;
        }

        public static bool CanPerformInteraction(Pawn pet, LeadYourPetInteractionKind kind, out string reasonKey, bool manual = false)
        {
            reasonKey = null;
            if (pet == null || !pet.Spawned || pet.Dead)
            {
                reasonKey = "LeadYourPet_Reason_InvalidTarget";
                return false;
            }

            if (!manual && kind != LeadYourPetInteractionKind.Fed && kind != LeadYourPetInteractionKind.Snack
                && kind != LeadYourPetInteractionKind.Drag
                && (pet.Downed || !pet.Awake() || HasLeashCareJob(Component?.GetLinkForPet(pet))))
            {
                return false;
            }

            switch (kind)
            {
                case LeadYourPetInteractionKind.PullTail:
                case LeadYourPetInteractionKind.TailPetting:
                    if (!HasRelevantBodyPart(pet, "tail"))
                    {
                        reasonKey = "LeadYourPet_Reason_MissingTail";
                        return false;
                    }

                    break;
                case LeadYourPetInteractionKind.PullEar:
                case LeadYourPetInteractionKind.EarPetting:
                    if (!HasRelevantBodyPart(pet, "ear"))
                    {
                        reasonKey = "LeadYourPet_Reason_MissingEar";
                        return false;
                    }

                    break;
                case LeadYourPetInteractionKind.TapHead:
                case LeadYourPetInteractionKind.Slap:
                    if (!HasRelevantBodyPart(pet, "head"))
                    {
                        reasonKey = "LeadYourPet_Reason_MissingHead";
                        return false;
                    }

                    break;
            }

            return true;
        }

        private static bool HasRelevantBodyPart(Pawn pawn, string token)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            return pawn.health.hediffSet.GetNotMissingParts().Any(x =>
                x.def.defName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Label.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool TryFindManualFeedFood(Pawn feeder, Pawn pet, out Thing foodSource, out string failReason)
        {
            foodSource = null;
            failReason = null;

            if (feeder == null || pet == null || !feeder.Spawned || !pet.Spawned || feeder.Map != pet.Map)
            {
                failReason = "LeadYourPet_Reason_NotOnCurrentMap";
                return false;
            }

            if (pet.needs?.food == null)
            {
                failReason = "LeadYourPet_Reason_NoFoodNeed";
                return false;
            }

            Thing inventoryFood = feeder.inventory?.innerContainer?.InnerListForReading?.FirstOrDefault(x => CanUseManualFeedFood(feeder, pet, x));
            if (inventoryFood != null)
            {
                foodSource = inventoryFood;
                return true;
            }

            Thing mapFood = FindAllowedFeedFoodOnMap(feeder, pet);
            if (CanUseManualFeedFood(feeder, pet, mapFood))
            {
                foodSource = mapFood;
                return true;
            }

            failReason = "LeadYourPet_Reason_NoSuitableFood";
            return false;
        }

        private static Thing FindAllowedFeedFoodOnMap(Pawn feeder, Pawn pet)
        {
            Thing best = null;
            float bestScore = float.MinValue;
            List<Thing> candidates = feeder.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSource);
            for (int i = 0; i < candidates.Count; i++)
            {
                Thing food = candidates[i];
                if (!food.Spawned || !CanUseManualFeedFood(feeder, pet, food)
                    || food.IsForbidden(feeder) || food.IsNotFresh() || food.IsDessicated()
                    || (!food.IsSociallyProper(feeder) && !food.IsSociallyProper(pet, pet.IsPrisonerOfColony, !feeder.IsAnimal))
                    || (feeder.roping.IsRoped && !food.Position.InHorDistOf(feeder.roping.RopedTo.Cell, 8f))) continue;
                float score = FoodUtility.FoodOptimality(pet, food, food.def, (feeder.Position - food.Position).LengthHorizontal);
                if (score < bestScore || !feeder.CanReach(food, PathEndMode.ClosestTouch, Danger.Some)) continue;
                best = food;
                bestScore = score;
            }
            return best;
        }

        public static bool TryFeedTarget(Pawn feeder, Pawn pet, out string failReason, out LeadYourPetInteractionKind interactionKind)
        {
            interactionKind = IsYoungRatkin(pet) ? LeadYourPetInteractionKind.Fed : LeadYourPetInteractionKind.Snack;
            if (!TryFindManualFeedFood(feeder, pet, out Thing foodSource, out failReason))
            {
                return false;
            }

            float nutrition = FoodUtility.NutritionForEater(pet, foodSource);
            if (nutrition <= 0f)
            {
                failReason = "LeadYourPet_Reason_NoUsableNutrition";
                return false;
            }

            Thing consumed = foodSource.SplitOff(1);
            consumed?.Destroy();

            if (pet.needs?.food != null)
            {
                pet.needs.food.CurLevel = Mathf.Min(pet.needs.food.MaxLevel, pet.needs.food.CurLevel + nutrition);
            }

            Hediff malnutrition = pet.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition != null)
            {
                malnutrition.Severity = Mathf.Max(0f, malnutrition.Severity - nutrition * 0.15f);
            }

            failReason = null;
            return true;
        }

        public static bool TryShareMasterMealNutrition(Pawn master, Pawn pet, float nutritionIngested)
        {
            if (master == null || pet == null || nutritionIngested <= 0f || pet.needs?.food == null)
            {
                return false;
            }

            float sharedNutrition = LeadYourPetRules.ResolveSharedNutritionFromMasterMeal(nutritionIngested);
            if (sharedNutrition <= 0f)
            {
                return false;
            }

            pet.needs.food.CurLevel = LeadYourPetRules.ResolveFedNutritionLevel(
                pet.needs.food.CurLevel,
                pet.needs.food.MaxLevel,
                sharedNutrition);

            Hediff malnutrition = pet.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition != null)
            {
                malnutrition.Severity = Mathf.Max(0f, malnutrition.Severity - sharedNutrition * 0.15f);
            }

            return true;
        }

        public static bool ShouldTreatVisitorYoungRatkinPetLeashAsAggressive(Pawn master, Pawn pet)
        {
            return LeadYourPetRules.ShouldTreatVisitorYoungRatkinPetLeashAsAggressive(
                masterIsPlayer: master?.Faction == Faction.OfPlayer,
                petIsRatkinHumanlike: IsRatkinHumanlike(pet),
                petIsBabyOrChild: pet != null && (pet.DevelopmentalStage.Baby() || pet.DevelopmentalStage.Child()),
                petFactionExists: pet?.Faction != null,
                petFactionIsPlayer: pet?.Faction == Faction.OfPlayer,
                petFactionHostileToPlayer: pet?.Faction != null && Faction.OfPlayer != null && pet.Faction.HostileTo(Faction.OfPlayer),
                petInFriendlyVisitorLord: IsFriendlyVisitorYoungRatkin(pet));
        }

        public static void ClearRatkinDisasterVisitorCover(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            ClearRatkinDisasterTradeAndVisitorFlags(pawn);
            TryRefreshRatkinDisasterHiddenFactionRelations();
        }

        public static void ClearRatkinDisasterTradeAndVisitorFlags(Pawn pawn)
        {
            TryInvokeVoid(RatkinDisasterClearTradeLeaderStateMethod, pawn);
            TryInvokeVoid(RatkinDisasterResetBeggarStateMethod, pawn);
        }

        public static bool TryMakeRatkinDisasterVisitorsHostile(IEnumerable<Pawn> pawns, out int hostileCount)
        {
            hostileCount = 0;
            if (RatkinDisasterTryMakeHostileVisitorsMethod == null)
            {
                return false;
            }

            try
            {
                object[] args = { pawns, 0 };
                bool handled = RatkinDisasterTryMakeHostileVisitorsMethod.Invoke(null, args) is bool result && result;
                if (args.Length > 1 && args[1] is int count)
                {
                    hostileCount = count;
                }

                return handled;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Visitor integration failed: " + exception, 19462003);
                hostileCount = 0;
                return false;
            }
        }

        public static void MakeRatkinDisasterFactionHostileToPlayer(Faction faction, bool explicitDriveAway)
        {
            if (RatkinDisasterMakeFactionHostileToPlayerMethod == null)
            {
                return;
            }

            try
            {
                RatkinDisasterMakeFactionHostileToPlayerMethod.Invoke(null, new object[] { faction, explicitDriveAway });
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Faction integration failed: " + exception, 19462004);
            }
        }

        public static void TryRefreshRatkinDisasterHiddenFactionRelations()
        {
            TryInvokeVoid(RatkinDisasterTryRefreshHiddenFactionRelationsMethod);
        }

        public static void NotifyRatkinDisasterPawnIdentityChanged(Pawn pawn)
        {
            TryInvokeVoid(RatkinDisasterNotifyIdentityChangedMethod, pawn);
        }

        public static void TryInvokeVoid(MethodInfo method, params object[] args)
        {
            if (method == null)
            {
                return;
            }

            try
            {
                OptionalModApi.Invoke(method, args);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[LeadYourPet Continued] Optional integration failed (" + method.Name + "): " + exception, 19462005);
            }
        }

        public static void ApplyInteractionMemories(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (pet?.needs?.mood?.thoughts?.memories == null)
            {
                return;
            }

            ThoughtDef ownerThought = OwnerThoughtFor(owner, pet, kind);
            if (ownerThought != null && owner?.needs?.mood?.thoughts?.memories != null)
            {
                owner.needs.mood.thoughts.memories.TryGainMemory(ownerThought, pet);
            }

            ThoughtDef petThought = PetThoughtFor(owner, pet, kind);
            if (petThought != null)
            {
                pet.needs.mood.thoughts.memories.TryGainMemory(petThought, owner);
            }
        }

        public static void ApplyInteractionJoy(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (!IsBidirectionalInteraction(kind))
            {
                return;
            }

            owner?.needs?.joy?.GainJoy(0.10f, JoyKindDefOf.Social);
            if (IsColonistYoungRatkin(pet))
            {
                if (pet.needs?.play != null)
                {
                    pet.needs.play.Play(0.10f);
                }
                else
                {
                    pet?.needs?.joy?.GainJoy(0.10f, JoyKindDefOf.Social);
                }
            }
        }

        public static void ApplyInteractionPhysicalEffects(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if ((!IsBidirectionalInteraction(kind) && kind != LeadYourPetInteractionKind.Drag) || pet?.health == null)
            {
                return;
            }

            if (IsColonistYoungRatkin(pet) && IsBidirectionalInteraction(kind))
            {
                return;
            }

            pet.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 0f, 0f, -1f, owner));
            if (Rand.Chance(0.35f))
            {
                BodyPartRecord part = FindBruiseBodyPart(pet, kind);
                Hediff hediff = HediffMaker.MakeHediff(LeadYourPetDefOf.LeadYourPet_DragBruise, pet, part);
                pet.health.AddHediff(hediff);
            }
        }

        private static BodyPartRecord FindBruiseBodyPart(Pawn pawn, LeadYourPetInteractionKind kind)
        {
            string token = null;
            switch (kind)
            {
                case LeadYourPetInteractionKind.TapHead:
                case LeadYourPetInteractionKind.Slap:
                    token = "head";
                    break;
                case LeadYourPetInteractionKind.PullEar:
                    token = "ear";
                    break;
                case LeadYourPetInteractionKind.PullTail:
                    token = "tail";
                    break;
                case LeadYourPetInteractionKind.KickButt:
                    token = "leg";
                    break;
            }

            if (!token.NullOrEmpty())
            {
                BodyPartRecord match = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def.defName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 || x.Label.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
                if (match != null)
                {
                    return match;
                }
            }

            return pawn.health.hediffSet.GetNotMissingParts().RandomElementWithFallback();
        }

        private static bool CanUseManualFeedFood(Pawn feeder, Pawn pet, Thing food)
        {
            return food != null
                && LeadYourPetFeedFood.Allows(food.def)
                && food.IngestibleNow
                && !food.def.IsDrug
                && pet.WillEat(food, feeder)
                && FoodUtility.NutritionForEater(pet, food) > 0f;
        }

        public static ThoughtDef OwnerThoughtFor(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (IsColonistYoungRatkin(pet) && IsBidirectionalInteraction(kind))
            {
                return LeadYourPetDefOf.LeadYourPet_OwnerPlayWithChild;
            }

            switch (kind)
            {
                case LeadYourPetInteractionKind.Kick: return LeadYourPetDefOf.LeadYourPet_OwnerKick;
                case LeadYourPetInteractionKind.Observe: return LeadYourPetDefOf.LeadYourPet_OwnerObserve;
                case LeadYourPetInteractionKind.Drag: return LeadYourPetDefOf.LeadYourPet_OwnerDrag;
                case LeadYourPetInteractionKind.TapHead: return LeadYourPetDefOf.LeadYourPet_OwnerTapHead;
                case LeadYourPetInteractionKind.PullTail: return LeadYourPetDefOf.LeadYourPet_OwnerPullTail;
                case LeadYourPetInteractionKind.PullEar: return LeadYourPetDefOf.LeadYourPet_OwnerPullEar;
                case LeadYourPetInteractionKind.Whirl: return LeadYourPetDefOf.LeadYourPet_OwnerWhirl;
                case LeadYourPetInteractionKind.Slap: return LeadYourPetDefOf.LeadYourPet_OwnerSlap;
                case LeadYourPetInteractionKind.Pat: return LeadYourPetDefOf.LeadYourPet_OwnerPat;
                case LeadYourPetInteractionKind.KickButt: return LeadYourPetDefOf.LeadYourPet_OwnerKickButt;
                default: return null;
            }
        }

        public static ThoughtDef PetThoughtFor(Pawn owner, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (IsColonistYoungRatkin(pet) && IsBidirectionalInteraction(kind))
            {
                return LeadYourPetDefOf.LeadYourPet_ChildPlayedWithCaretaker;
            }

            switch (kind)
            {
                case LeadYourPetInteractionKind.Kick: return LeadYourPetDefOf.LeadYourPet_PetKick;
                case LeadYourPetInteractionKind.Observe: return LeadYourPetDefOf.LeadYourPet_PetObserve;
                case LeadYourPetInteractionKind.Drag: return LeadYourPetDefOf.LeadYourPet_PetDrag;
                case LeadYourPetInteractionKind.TapHead: return LeadYourPetDefOf.LeadYourPet_PetTapHead;
                case LeadYourPetInteractionKind.PullTail: return LeadYourPetDefOf.LeadYourPet_PetPullTail;
                case LeadYourPetInteractionKind.PullEar: return LeadYourPetDefOf.LeadYourPet_PetPullEar;
                case LeadYourPetInteractionKind.Whirl: return LeadYourPetDefOf.LeadYourPet_PetWhirl;
                case LeadYourPetInteractionKind.Slap: return LeadYourPetDefOf.LeadYourPet_PetSlap;
                case LeadYourPetInteractionKind.Pat: return LeadYourPetDefOf.LeadYourPet_PetPat;
                case LeadYourPetInteractionKind.KickButt: return LeadYourPetDefOf.LeadYourPet_PetKickButt;
                case LeadYourPetInteractionKind.WatchWork: return LeadYourPetDefOf.LeadYourPet_PetWatchWork;
                case LeadYourPetInteractionKind.Fed: return LeadYourPetDefOf.LeadYourPet_PetFed;
                case LeadYourPetInteractionKind.RestAtFeet: return LeadYourPetDefOf.LeadYourPet_PetRestAtFeet;
                case LeadYourPetInteractionKind.IdleNearOwner: return LeadYourPetDefOf.LeadYourPet_PetIdleNearOwner;
                case LeadYourPetInteractionKind.StudyFloor: return LeadYourPetDefOf.LeadYourPet_PetStudyFloor;
                case LeadYourPetInteractionKind.SillySmile: return LeadYourPetDefOf.LeadYourPet_PetSillySmile;
                case LeadYourPetInteractionKind.TailPetting: return LeadYourPetDefOf.LeadYourPet_PetTailPetting;
                case LeadYourPetInteractionKind.EarPetting: return LeadYourPetDefOf.LeadYourPet_PetEarPetting;
                case LeadYourPetInteractionKind.OwnerWaited: return LeadYourPetDefOf.LeadYourPet_PetOwnerWaited;
                case LeadYourPetInteractionKind.Snack: return LeadYourPetDefOf.LeadYourPet_PetSnack;
                default: return null;
            }
        }
    }
}
