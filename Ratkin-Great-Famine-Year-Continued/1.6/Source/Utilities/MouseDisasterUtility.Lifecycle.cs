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

        public static void ProcessMouseDisasterIdentityRestrictions(Map map, bool explicitRefreshRequested = false)
        {
            if (map == null || !MouseDisasterGeneRestorePolicy.ShouldRunIdentityRestrictionScan(explicitRefreshRequested))
            {
                return;
            }

            List<Pawn> pawns = GetCachedRatkinPawns(map);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                {
                    continue;
                }

                NormalizeMouseDisasterPawnIdentityOrLifeStageChanged(pawn, markMapCacheDirty: false);
            }
        }

        public static void ProcessOpenDoorStuckJobs(Map map)
        {
            if (map == null)
            {
                return;
            }

            List<Pawn> pawns = GetCachedDoorStuckCandidates(map);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!IsDoorStuckCandidate(pawn))
                {
                    continue;
                }

                Job curJob = pawn.jobs.curJob;
                if (curJob?.def != JobDefOf.Open || !curJob.targetA.IsValid)
                {
                    continue;
                }

                Building_Door targetDoor = curJob.targetA.Thing as Building_Door;
                if (targetDoor == null || !targetDoor.Open)
                {
                    continue;
                }

                pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
        }

        public static void ProcessBabyExpansionMoodReplacement(Map map)
        {
            if (!IsBabyExpansionEnabled || map == null)
            {
                return;
            }

            ThoughtDef thoughtDef = ResolveBabyExpansionVisitorMoodThoughtDef();
            if (thoughtDef == null)
            {
                return;
            }

            List<Pawn> pawns = GetCachedIncidentMoodChildren(map);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.needs?.mood?.thoughts?.memories == null)
                {
                    continue;
                }

                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thoughtDef);
            }
        }

        public static void NotifyMouseDisasterPawnIdentityOrLifeStageChanged(Pawn pawn)
        {
            MouseDisasterVisitorUtility.NotifyPawnIdentityChanged(pawn);
            NormalizeMouseDisasterPawnIdentityOrLifeStageChanged(pawn, markMapCacheDirty: true);
        }

        private static void NormalizeMouseDisasterPawnIdentityOrLifeStageChanged(Pawn pawn, bool markMapCacheDirty)
        {
            if (markMapCacheDirty)
            {
                MarkMapPawnCacheDirty(pawn);
            }

            if (pawn == null || pawn.Dead || !IsRatkin(pawn))
            {
                return;
            }

            if (MouseDisasterInfantMobilityPatch.UsesFallback(pawn) && pawn.Downed)
            {
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.Notify_HediffChanged(null);
            }

            bool hostileIncidentVisitor = MouseDisasterGeneRestorePolicy.ShouldSkipVisitorNormalizationWhileTurningHostile(
                isIncidentVisitor: IsMouseDisasterIncidentVisitor(pawn),
                factionHostileToPlayer: IsHostileTo(pawn.Faction, Faction.OfPlayer));
            if (hostileIncidentVisitor)
            {
                TryEnsureToddlerCompatibilityHediffs(pawn);
                return;
            }

            if (IsMouseDisasterPawn(pawn) || IsMouseDisasterIncidentVisitor(pawn))
            {
                NormalizeMouseDisasterPawnGenes(pawn);
            }

            if (IsMouseDisasterIncidentVisitor(pawn))
            {
                ConfigureNoRescueJoinForIncidentVisitor(pawn);
            }

            if (IsPlayerAffiliatedRatkin(pawn))
            {
                TryNormalizePlayerAffiliatedMouseDisasterState(pawn);
            }

            TryEnsureToddlerCompatibilityHediffs(pawn);
        }

        public static bool IsMouseDisasterIncidentChild(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !IsMouseEggOrChild(pawn))
            {
                return false;
            }

            return pawn.relations?.DirectRelations != null &&
                   pawn.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Parent &&
                                                          r.otherPawn != null &&
                                                          IsMouseDisasterIncidentParentAdult(r.otherPawn));
        }

        private static ThoughtDef ResolveBabyExpansionVisitorMoodThoughtDef()
        {
            if (babyExpansionVisitorMoodThoughtResolved)
            {
                return babyExpansionVisitorMoodThoughtDef;
            }

            babyExpansionVisitorMoodThoughtResolved = true;
            babyExpansionVisitorMoodThoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(BabyExpansionVisitorMoodThoughtDefName);
            return babyExpansionVisitorMoodThoughtDef;
        }

        private static bool IsDoorStuckCandidate(Pawn pawn)
        {
            if (pawn?.jobs == null || pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            PawnKindDef kindDef = pawn.kindDef;
            string kindDefName = kindDef?.defName;
            if (kindDefName.NullOrEmpty())
            {
                return false;
            }

            return kindDefName.StartsWith("MouseDisaster_", StringComparison.OrdinalIgnoreCase) && !IsPlayerAffiliatedRatkin(pawn);
        }

        private static void NormalizeMouseDisasterPawnGenes(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null || IsPlayerAffiliatedRatkin(pawn))
            {
                return;
            }

            StripNonMouseDisasterGenes(pawn);
            bool isManagedPawn = IsMouseDisasterPawn(pawn) || IsMouseDisasterIncidentVisitor(pawn);
            bool hasVirtualDefaultRatkinXenotype =
                MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(pawn.genes.Xenotype);
            if (hasVirtualDefaultRatkinXenotype)
            {
                MouseDisasterSubtypeUtility.MigrateVirtualDefaultRatkinXenotype(pawn);
            }

            bool hasRatkinLikeXenotype = pawn.genes.Xenotype != null &&
                                         MouseDisasterBirthPolicy.IsRatkinLikeXenotype(
                                             pawn.genes.Xenotype.defName,
                                             pawn.genes.Xenotype.label) &&
                                         !MouseDisasterAdaptiveXenotypeUtility.IsVirtualDefaultRatkinXenotype(
                                             pawn.genes.Xenotype);
            bool hasManualRemovalMarker = HasManualMouseDisasterGeneRemovalMarker(pawn);
            if (isManagedPawn && !hasRatkinLikeXenotype && !hasManualRemovalMarker)
            {
                MouseDisasterSubtypeUtility.ApplyDefaultSubtypeGenes(pawn);
            }

            MouseDisasterGeneRestoreDecision decision = MouseDisasterGeneRestorePolicy.DecideMissingGeneRestore(
                isIncidentVisitor: IsMouseDisasterIncidentVisitor(pawn),
                hasMouseDisasterGenes: HasAnyMouseDisasterGene(pawn),
                hasManualRemovalMarker: hasManualRemovalMarker);

            if (decision == MouseDisasterGeneRestoreDecision.SuppressRestore)
            {
                StripMouseDisasterGenes(pawn);
                CleanupOrphanedChemicalDependencies(pawn);
                return;
            }

            if (decision == MouseDisasterGeneRestoreDecision.RestoreMissingGenes)
            {
                MouseDisasterSubtypeUtility.ApplySubtypeGenes(pawn);
            }

            CleanupOrphanedChemicalDependencies(pawn);
        }

        private static void TryNormalizePlayerAffiliatedMouseDisasterState(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (MouseDisasterGeneRestorePolicy.ShouldPreserveTraderKindForIncidentVisitor(
                    isIncidentVisitor: IsMouseDisasterIncidentVisitor(pawn),
                    isTraderAdult: IsMouseDisasterTraderAdult(pawn)))
            {
                return;
            }

            if (IsThiefPawn(pawn) ||
                IsBeggarPawn(pawn) ||
                IsWildMouseDisasterKind(pawn) ||
                IsMouseDisasterTraderAdult(pawn) ||
                IsMouseDisasterTraderEscort(pawn))
            {
                PawnKindDef fallbackKind = ResolvePlayerAffiliatedRatkinKindDef(pawn);
                if (fallbackKind != null && fallbackKind != pawn.kindDef)
                {
                    pawn.ChangeKind(fallbackKind);
                }
            }

            if (IsInThiefMentalState(pawn) || IsInBeggarMentalState(pawn))
            {
                pawn.mindState?.mentalStateHandler?.Reset();
            }

            pawn.mindState?.duty = null;
        }

        public static void ConfigureNoRescueJoinForIncidentVisitor(Pawn pawn)
        {
            if (pawn == null || !IsMouseDisasterIncidentVisitor(pawn) || MouseDisasterVisitorUtility.IsShelteredVisitor(pawn))
            {
                return;
            }

            if (pawn.mindState != null)
            {
                pawn.mindState.WillJoinColonyIfRescued = false;
            }

            if (pawn.guest != null)
            {
                pawn.guest.leftAfterRescue = true;
                pawn.guest.getRescuedThoughtOnUndownedBecauseOfPlayer = false;
            }
        }

        public static void TryRecoverIncidentVisitorFromPlayerGuest(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !IsMouseDisasterIncidentVisitor(pawn) || IsPlayerAffiliatedRatkin(pawn) || MouseDisasterVisitorUtility.IsShelteredVisitor(pawn))
            {
                return;
            }

            if (pawn.guest == null || pawn.guest.HostFaction != Faction.OfPlayer)
            {
                return;
            }

            ConfigureNoRescueJoinForIncidentVisitor(pawn);
            if (pawn.guest.HostFaction != null)
            {
                pawn.guest.SetGuestStatus(null, GuestStatus.Guest);
            }

            if (IsHospitalityEnabled)
            {
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
                pawn.GetLord()?.RemovePawn(pawn);
            }

            if (pawn.Map != null && pawn.Spawned)
            {
                Job exitJob = ExitMapJob(pawn, force: true);
                if (exitJob != null)
                {
                    pawn.jobs?.TryTakeOrderedJob(exitJob);
                }
            }
        }

        private static PawnKindDef ResolvePlayerAffiliatedRatkinKindDef(Pawn pawn)
        {
            bool juvenile = pawn != null && !pawn.DevelopmentalStage.Adult();
            if (!juvenile && playerAffiliatedRatkinKindResolved)
            {
                return playerAffiliatedRatkinKindDef;
            }

            ThingDef raceDef = ResolveRatkinRaceDef(null);
            if (raceDef == null)
            {
                return null;
            }

            List<PawnKindDef> candidates = DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(def => def != null &&
                              def.race == raceDef &&
                              !def.defName.NullOrEmpty() &&
                              !def.defName.StartsWith("MouseDisaster_", StringComparison.OrdinalIgnoreCase) &&
                              !def.defName.StartsWith("WildMan", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            IEnumerable<PawnKindDef> stageFiltered = juvenile
                ? candidates.Where(def => def.maxGenerationAge <= RatkinYoungChildMaxAgeYears + 0.1f)
                : candidates.Where(def => def.maxGenerationAge >= RatkinAdultMinAgeYears);
            List<PawnKindDef> filteredCandidates = stageFiltered.ToList();
            if (filteredCandidates.Count == 0)
            {
                filteredCandidates = candidates;
            }

            PawnKindDef resolved = filteredCandidates
                .OrderByDescending(def => def.canMeleeAttack)
                .ThenByDescending(def => def.isFighter)
                .ThenByDescending(def => (def.defName ?? string.Empty).IndexOf("colonist", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(def => (def.label ?? string.Empty).IndexOf("殖民", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenBy(def => (def.defName ?? string.Empty).Length)
                .FirstOrDefault();

            if (!juvenile)
            {
                playerAffiliatedRatkinKindResolved = true;
                playerAffiliatedRatkinKindDef = resolved;
            }

            return resolved;
        }

        private static void TryEnsureToddlerCompatibilityHediffs(Pawn pawn)
        {
            // Run at identity/age transitions, not from RemoveHediff: providers may
            // intentionally remove completed learning or unsupported infant states.
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            bool shouldApplyCompatibility = MouseDisasterGeneRestorePolicy.ShouldApplyRimTalkToddlerCompatibility(
                isMouseDisasterPawn: IsMouseDisasterPawn(pawn),
                isIncidentVisitor: IsMouseDisasterIncidentVisitor(pawn),
                isPlayerAffiliatedRatkin: IsPlayerAffiliatedRatkin(pawn));
            if (!shouldApplyCompatibility)
            {
                return;
            }

            if (syncingToddlerCompat)
            {
                return;
            }

            syncingToddlerCompat = true;
            try
            {
                ResolveToddlerCompatibilityDefsIfNeeded();
                float ageYears = pawn.ageTracker?.AgeBiologicalYearsFloat ?? -1f;
                bool toddlerAge = ageYears >= 1f && ageYears < 3f;
                bool infantAge = ageYears >= 0f && ageYears < 1f;

                if (toddlerAge)
                {
                    EnsureHediffPresentIfMissing(pawn, toddlersLearningToWalkDef, 0.001f);
                    EnsureHediffPresentIfMissing(pawn, toddlersLearningManipulationDef, 0.001f);
                    EnsureHediffPresentIfMissing(pawn, toddlersLonelyDef, 0.001f);
                    EnsureHediffPresentIfMissing(pawn, rimTalkToddlerLanguageLearningDef, 0.001f);
                    RemoveHediffIfPresent(pawn, rimTalkBabyBabblingDef);
                    return;
                }

                if (infantAge)
                {
                    RemoveHediffIfPresent(pawn, toddlersLearningToWalkDef);
                    RemoveHediffIfPresent(pawn, toddlersLearningManipulationDef);
                    RemoveHediffIfPresent(pawn, toddlersLonelyDef);
                    RemoveHediffIfPresent(pawn, rimTalkToddlerLanguageLearningDef);
                    EnsureHediffPresentIfMissing(pawn, rimTalkBabyBabblingDef, 1f);
                    return;
                }

                RemoveHediffIfPresent(pawn, toddlersLearningToWalkDef);
                RemoveHediffIfPresent(pawn, toddlersLearningManipulationDef);
                RemoveHediffIfPresent(pawn, toddlersLonelyDef);
                RemoveHediffIfPresent(pawn, rimTalkBabyBabblingDef);
                RemoveHediffIfPresent(pawn, rimTalkToddlerLanguageLearningDef);
            }
            finally
            {
                syncingToddlerCompat = false;
            }
        }

        public static bool IsToddlerCompatibilityHediffDef(HediffDef hediffDef)
        {
            if (hediffDef == null)
            {
                return false;
            }

            string defName = hediffDef.defName ?? string.Empty;
            return defName.Equals("LearningToWalk", StringComparison.OrdinalIgnoreCase) ||
                   defName.Equals("LearningManipulation", StringComparison.OrdinalIgnoreCase) ||
                   defName.Equals("ToddlerLonely", StringComparison.OrdinalIgnoreCase) ||
                   defName.Equals("RimTalk_BabyBabbling", StringComparison.OrdinalIgnoreCase) ||
                   defName.Equals("RimTalk_ToddlerLanguageLearning", StringComparison.OrdinalIgnoreCase);
        }

        private static void ResolveToddlerCompatibilityDefsIfNeeded()
        {
            if (toddlerCompatDefsResolved)
            {
                return;
            }

            toddlerCompatDefsResolved = true;
            toddlersLearningToWalkDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningToWalk");
            toddlersLearningManipulationDef = DefDatabase<HediffDef>.GetNamedSilentFail("LearningManipulation");
            toddlersLonelyDef = DefDatabase<HediffDef>.GetNamedSilentFail("ToddlerLonely");
            rimTalkBabyBabblingDef = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_BabyBabbling");
            rimTalkToddlerLanguageLearningDef = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_ToddlerLanguageLearning");
        }

        private static void EnsureHediffPresentIfMissing(Pawn pawn, HediffDef hediffDef, float minimumSeverity)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null || pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) != null)
            {
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            if (hediff != null && hediff.Severity < minimumSeverity)
            {
                hediff.Severity = minimumSeverity;
            }

            if (hediff != null)
            {
                pawn.health.AddHediff(hediff);
            }
        }

        private static void RemoveHediffIfPresent(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private enum ChaosPregnancyIdentity
        {
            Colonist,
            Prisoner,
            Slave,
            Enemy
        }

        private static bool TryGetChaosPregnancyIdentity(Pawn pawn, out ChaosPregnancyIdentity identity)
        {
            identity = ChaosPregnancyIdentity.Colonist;
            if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike || pawn.RaceProps.Animal || pawn.RaceProps.IsMechanoid)
            {
                return false;
            }

            if (pawn.IsPrisonerOfColony)
            {
                identity = ChaosPregnancyIdentity.Prisoner;
                return true;
            }

            if (pawn.IsSlaveOfColony)
            {
                identity = ChaosPregnancyIdentity.Slave;
                return true;
            }

            if (pawn.Faction == Faction.OfPlayer)
            {
                identity = ChaosPregnancyIdentity.Colonist;
                return true;
            }

            if (IsHostileTo(pawn.Faction, Faction.OfPlayer))
            {
                identity = ChaosPregnancyIdentity.Enemy;
                return true;
            }

            return false;
        }
    }
}
