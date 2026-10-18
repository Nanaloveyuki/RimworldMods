using System;
using System.Collections;
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
        private static bool leadYourPetInvocationFailureLogged;

        public static void LinkIncidentParentToChildren(Pawn adult, IEnumerable<Pawn> children)
        {
            if (adult == null || children == null)
            {
                return;
            }

            foreach (Pawn child in children)
            {
                if (child == null || child.Dead)
                {
                    continue;
                }

                bool alreadyLinked = child.relations?.DirectRelations != null &&
                                     child.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Parent && r.otherPawn == adult);
                if (!alreadyLinked)
                {
                    child.relations?.AddDirectRelation(PawnRelationDefOf.Parent, adult);
                }

                MarkMapPawnCacheDirty(child);
            }

            MarkMapPawnCacheDirty(adult);
        }

        public static void TryStartLeadYourPetMotherLeashes(Pawn mother, IEnumerable<Pawn> babies)
        {
            if (mother == null || babies == null)
            {
                return;
            }

            List<Pawn> pawns = new List<Pawn> { mother };
            pawns.AddRange(babies.Where(baby => baby != null));
            TryStartLeadYourPetRelatedAdultLeashes(pawns);
        }

        public static void TryStartLeadYourPetRelatedAdultLeashes(IEnumerable<Pawn> pawns)
        {
            if (pawns == null || !IsLeadYourPetEnabled || Current.Game == null)
            {
                return;
            }

            EnsureLeadYourPetReflection();
            if (leadYourPetComponentType == null ||
                leadYourPetTryStartRatkinMotherLeashMethod == null ||
                leadYourPetStartLeashMethod == null ||
                gameGetComponentMethod == null)
            {
                return;
            }

            object component = TryGetLeadYourPetComponent();
            if (component == null)
            {
                return;
            }

            List<Pawn> pawnList = pawns
                .Where(pawn => pawn != null && !pawn.Dead && pawn.Spawned && !IsPlayerAffiliatedRatkin(pawn))
                .Distinct()
                .ToList();
            List<Pawn> adults = pawnList
                .Where(pawn => pawn.DevelopmentalStage == DevelopmentalStage.Adult)
                .ToList();
            List<Pawn> babies = pawnList
                .Where(pawn => pawn.DevelopmentalStage == DevelopmentalStage.Baby)
                .ToList();
            for (int babyIndex = 0; babyIndex < babies.Count; babyIndex++)
            {
                Pawn baby = babies[babyIndex];
                IEnumerable<Pawn> relatedAdults = adults
                    .Where(adult => MouseDisasterBabyLeashPolicy.ShouldTryRelatedAdultBabyLeash(
                        IsLeadYourPetEnabled,
                        adult.DevelopmentalStage == DevelopmentalStage.Adult,
                        HasIncidentAdultSocialRelation(baby, adult)))
                    .OrderBy(adult => adult.Position.DistanceToSquared(baby.Position));
                foreach (Pawn adult in relatedAdults)
                {
                    object result = StartLeadYourPetBabyLeash(component, adult, baby);
                    if (result is bool started && started)
                    {
                        break;
                    }
                }
            }
        }

        private static object StartLeadYourPetBabyLeash(object component, Pawn adult, Pawn baby)
        {
            MouseDisasterBabyLeashMode leashMode = MouseDisasterBabyLeashPolicy.ResolveRelatedAdultLeashMode(
                hasParentRelation: HasIncidentParentRelation(baby, adult));
            switch (leashMode)
            {
                case MouseDisasterBabyLeashMode.RatkinMother:
                    return InvokeLeadYourPet(leadYourPetTryStartRatkinMotherLeashMethod, component, new object[] { adult, baby, false });
                case MouseDisasterBabyLeashMode.GenericMouseEgg:
                    return InvokeLeadYourPet(leadYourPetStartLeashMethod, component, new object[] { adult, baby, true, false });
                default:
                    return false;
            }
        }

        private static bool HasIncidentAdultSocialRelation(Pawn baby, Pawn adult)
        {
            return baby?.relations?.DirectRelations != null &&
                   adult != null &&
                   baby.relations.DirectRelations.Any(relation => relation.otherPawn == adult && relation.otherPawn.DevelopmentalStage == DevelopmentalStage.Adult);
        }

        private static bool HasIncidentParentRelation(Pawn baby, Pawn adult)
        {
            return baby?.relations?.DirectRelations != null &&
                   adult != null &&
                   baby.relations.DirectRelations.Any(relation => relation.def == PawnRelationDefOf.Parent && relation.otherPawn == adult);
        }

        public static void TryAssignLeadYourPetTravelMouseEggs(Lord lord)
        {
            if (lord == null || !IsLeadYourPetEnabled || Current.Game == null)
            {
                return;
            }

            EnsureLeadYourPetReflection();
            if (leadYourPetComponentType == null || leadYourPetTryAssignTravelMouseEggsMethod == null || gameGetComponentMethod == null)
            {
                return;
            }

            object component = TryGetLeadYourPetComponent();
            if (component == null)
            {
                return;
            }

            InvokeLeadYourPet(leadYourPetTryAssignTravelMouseEggsMethod, component, new object[] { lord });
        }

        internal static List<Pawn> GetLeadYourPetLinkedPawns(Pawn master)
        {
            // Lead Your Pet is optional, so inspect its public link list only at departure time.
            List<Pawn> linkedPawns = new List<Pawn>();
            if (master == null || !IsLeadYourPetEnabled || Current.Game == null)
            {
                return linkedPawns;
            }

            try
            {
                EnsureLeadYourPetReflection();
                if (leadYourPetComponentType == null || leadYourPetGetLinksForMasterMethod == null || gameGetComponentMethod == null)
                {
                    return linkedPawns;
                }

                object component = TryGetLeadYourPetComponent();
                if (component == null)
                {
                    return linkedPawns;
                }

                object links = InvokeLeadYourPet(leadYourPetGetLinksForMasterMethod, component, new object[] { master });
                if (!(links is IEnumerable enumerable))
                {
                    return linkedPawns;
                }

                foreach (object link in enumerable)
                {
                    try
                    {
                        FieldInfo petField = link == null ? null : AccessTools.Field(link.GetType(), "Pet");
                        Pawn pet = petField?.GetValue(link) as Pawn;
                        if (pet != null && !linkedPawns.Contains(pet))
                        {
                            linkedPawns.Add(pet);
                        }
                    }
                    catch (Exception exception)
                    {
                        LogLeadYourPetInvocationFailure(exception);
                    }
                }
            }
            catch (Exception exception)
            {
                LogLeadYourPetInvocationFailure(exception);
            }

            return linkedPawns;
        }

        public static void TryStartLeadYourPetAbandonedDropoff(Pawn mother, IEnumerable<Pawn> babies, IntVec3 dropoffCell)
        {
            if (!MouseDisasterAbandonedDeliveryPolicy.ShouldUseLeadYourPetDropoff(IsLeadYourPetEnabled, babies?.Count(pawn => pawn != null && !pawn.Dead) ?? 0))
            {
                return;
            }

            TryStartLeadYourPetMotherLeashes(mother, babies);
            TryAnchorLeadYourPetLeashesToCell(mother, dropoffCell);
        }

        private static void TryAnchorLeadYourPetLeashesToCell(Pawn master, IntVec3 cell)
        {
            if (master == null || !cell.IsValid || !IsLeadYourPetEnabled || Current.Game == null)
            {
                return;
            }

            EnsureLeadYourPetReflection();
            if (leadYourPetComponentType == null || leadYourPetAnchorLeashedPetsToCellMethod == null || gameGetComponentMethod == null)
            {
                return;
            }

            object component = TryGetLeadYourPetComponent();
            if (component == null)
            {
                return;
            }

            InvokeLeadYourPet(leadYourPetAnchorLeashedPetsToCellMethod, component, new object[] { master, cell });
        }

        internal static void TryEndLeadYourPetLeashForPet(Pawn pet)
        {
            if (pet == null || !IsLeadYourPetEnabled || Current.Game == null)
            {
                return;
            }

            EnsureLeadYourPetReflection();
            if (leadYourPetComponentType == null || leadYourPetEndLeashForPetMethod == null || gameGetComponentMethod == null)
            {
                return;
            }

            object component = TryGetLeadYourPetComponent();
            if (component == null)
            {
                return;
            }

            InvokeLeadYourPet(leadYourPetEndLeashForPetMethod, component, new object[] { pet, false });
        }

        public static void TryReleaseLeadYourPetTradePawn(Pawn pawn)
        {
            if (pawn == null || !IsLeadYourPetEnabled || Current.Game == null) return;
            List<Pawn> linked = GetLeadYourPetLinkedPawns(pawn);
            ClearLeadYourPetOwnershipState(pawn);
            foreach (Pawn pet in linked) ClearLeadYourPetOwnershipState(pet);
        }

        private static void ClearLeadYourPetOwnershipState(Pawn pawn)
        {
            EnsureLeadYourPetReflection();
            if (leadYourPetComponentType == null || gameGetComponentMethod == null) return;
            object component = TryGetLeadYourPetComponent();
            if (component == null) return;
            if (leadYourPetEndLeashForPetMethod != null)
                InvokeLeadYourPet(leadYourPetEndLeashForPetMethod, component, new object[] { pawn, false });
            if (leadYourPetClearMouseEggPetStateMethod != null)
                InvokeLeadYourPet(leadYourPetClearMouseEggPetStateMethod, component, new object[] { pawn });
            if (leadYourPetClearTravelStockMethod != null)
                InvokeLeadYourPet(leadYourPetClearTravelStockMethod, component, new object[] { pawn });
        }

        private static object TryGetLeadYourPetComponent()
        {
            try
            {
                return gameGetComponentMethod.MakeGenericMethod(leadYourPetComponentType).Invoke(Current.Game, null);
            }
            catch (Exception exception)
            {
                LogLeadYourPetInvocationFailure(exception);
                return null;
            }
        }

        private static object InvokeLeadYourPet(MethodInfo method, object component, object[] arguments)
        {
            try
            {
                return method.Invoke(component, arguments);
            }
            catch (Exception exception)
            {
                LogLeadYourPetInvocationFailure(exception);
                return null;
            }
        }

        private static void LogLeadYourPetInvocationFailure(Exception exception)
        {
            if (leadYourPetInvocationFailureLogged)
            {
                return;
            }

            leadYourPetInvocationFailureLogged = true;
            Log.Warning("[MouseDisaster] Lead Your Pet integration was skipped after a reflection call failed: " + exception);
        }

        private static void EnsureLeadYourPetReflection()
        {
            if (leadYourPetComponentType == null)
            {
                leadYourPetComponentType = AccessTools.TypeByName("LeadYourPet.LeadYourPetGameComponent");
            }

            if (leadYourPetTryStartRatkinMotherLeashMethod == null && leadYourPetComponentType != null)
            {
                leadYourPetTryStartRatkinMotherLeashMethod = AccessTools.Method(leadYourPetComponentType, "TryStartRatkinMotherLeash", new[] { typeof(Pawn), typeof(Pawn), typeof(bool) });
            }

            if (leadYourPetStartLeashMethod == null && leadYourPetComponentType != null)
            {
                leadYourPetStartLeashMethod = AccessTools.Method(leadYourPetComponentType, "StartLeash", new[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool) });
            }

            if (leadYourPetTryAssignTravelMouseEggsMethod == null && leadYourPetComponentType != null)
            {
                leadYourPetTryAssignTravelMouseEggsMethod = AccessTools.Method(leadYourPetComponentType, "TryAssignTravelMouseEggs", new[] { typeof(Lord) });
            }

            if (leadYourPetGetLinksForMasterMethod == null && leadYourPetComponentType != null)
            {
                leadYourPetGetLinksForMasterMethod = AccessTools.Method(leadYourPetComponentType, "GetLinksForMaster", new[] { typeof(Pawn) });
            }

            if (leadYourPetAnchorLeashedPetsToCellMethod == null && leadYourPetComponentType != null)
            {
                leadYourPetAnchorLeashedPetsToCellMethod = AccessTools.Method(leadYourPetComponentType, "AnchorLeashedPetsToCell", new[] { typeof(Pawn), typeof(IntVec3) });
            }

            if (leadYourPetEndLeashForPetMethod == null && leadYourPetComponentType != null)
            {
                leadYourPetEndLeashForPetMethod = AccessTools.Method(leadYourPetComponentType, "EndLeashForPet", new[] { typeof(Pawn), typeof(bool) });
            }

            if (gameGetComponentMethod == null)
            {
                gameGetComponentMethod = typeof(Game).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method => method.Name == "GetComponent" && method.IsGenericMethod && method.GetParameters().Length == 0);
            }
            if (leadYourPetComponentType != null)
            {
                leadYourPetClearMouseEggPetStateMethod ??= AccessTools.Method(leadYourPetComponentType, "ClearMouseEggPetState", new[] { typeof(Pawn) });
                leadYourPetClearTravelStockMethod ??= AccessTools.Method(leadYourPetComponentType, "ClearTravelStock", new[] { typeof(Pawn) });
            }
        }
    }
}
