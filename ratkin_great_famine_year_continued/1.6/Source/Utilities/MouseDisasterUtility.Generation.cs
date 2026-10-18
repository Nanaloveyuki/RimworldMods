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

        private static Pawn GenerateRatkinPawn(
            PawnKindDef kindDef,
            Faction formerFaction,
            DevelopmentalStage stage,
            bool allowViolenceDisabledTraits = false,
            Gender? fixedGender = null,
            float? fixedAgeYears = null)
        {
            if (kindDef == null)
            {
                return null;
            }

            ThingDef expectedRace = ResolveRatkinRaceDef(kindDef);
            PawnKindDef generationKindDef = ResolveRatkinKindDef(kindDef, stage, expectedRace);
            expectedRace ??= ResolveRatkinRaceDef(generationKindDef);
            if (generationKindDef == null || expectedRace == null)
            {
                return null;
            }

            Pawn pawn = null;
            var attemptedXenotypeDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Faction requestFaction = attempt == 0 ? formerFaction : null;
                XenotypeDef selectedXenotype = MouseDisasterAdaptiveXenotypeUtility.Choose(attemptedXenotypeDefNames);
                if (selectedXenotype != null)
                {
                    attemptedXenotypeDefNames.Add(selectedXenotype.defName);
                }

                pawn = TryGenerateRatkinPawnCandidate(
                    generationKindDef,
                    requestFaction,
                    stage,
                    allowViolenceDisabledTraits,
                    fixedGender,
                    fixedAgeYears,
                    expectedRace,
                    selectedXenotype,
                    safeXenotypeFallback: false);
                if (pawn != null)
                {
                    break;
                }
            }

            if (pawn == null)
            {
                pawn = TryGenerateRatkinPawnCandidate(
                    generationKindDef,
                    formerFaction,
                    stage,
                    allowViolenceDisabledTraits,
                    fixedGender,
                    fixedAgeYears,
                    expectedRace,
                    selectedXenotype: null,
                    safeXenotypeFallback: true);
                if (pawn != null)
                {
                    Log.Warning("[MouseDisaster] Ratkin pawn generation used the safe Ratkin xenotype fallback for " +
                        generationKindDef.defName + " (" + stage + ").");
                }
            }

            return pawn;
        }

        private static Pawn TryGenerateRatkinPawnCandidate(
            PawnKindDef generationKindDef,
            Faction requestFaction,
            DevelopmentalStage stage,
            bool allowViolenceDisabledTraits,
            Gender? fixedGender,
            float? fixedAgeYears,
            ThingDef expectedRace,
            XenotypeDef selectedXenotype,
            bool safeXenotypeFallback)
        {
            Pawn generated = null;
            XenotypeDef requestedXenotype = selectedXenotype;
            if (requestedXenotype == null && safeXenotypeFallback)
            {
                requestedXenotype = ResolveRatkinBaseXenotype();
            }

            try
            {
                generated = PawnGenerator.GeneratePawn(CreateRatkinGenerationRequest(
                    generationKindDef,
                    requestFaction,
                    stage,
                    allowViolenceDisabledTraits,
                    fixedGender,
                    selectedXenotype,
                    safeXenotypeFallback));
                if (generated == null || (expectedRace != null && generated.def != expectedRace))
                {
                    generated?.Destroy(DestroyMode.Vanish);
                    return null;
                }

                if (!TryPrepareRatkinPawn(generated, generationKindDef, stage, fixedAgeYears, requestedXenotype, fixedGender.HasValue))
                {
                    generated.Destroy(DestroyMode.Vanish);
                    return null;
                }

                return generated;
            }
            catch (Exception exception)
            {
                generated?.Destroy(DestroyMode.Vanish);
                Log.Warning("[MouseDisaster] Ratkin pawn generation attempt failed for " +
                    generationKindDef.defName + " (" + stage + ", xenotype=" +
                    (requestedXenotype?.defName ?? (safeXenotypeFallback ? "SafeRatkinFallback" : "default")) +
                    "): " + exception);
                return null;
            }
        }

        private static bool TryPrepareRatkinPawn(
            Pawn pawn,
            PawnKindDef generationKindDef,
            DevelopmentalStage stage,
            float? fixedAgeYears,
            XenotypeDef requestedXenotype,
            bool preserveRoleAge)
        {
            if (!IsRatkin(pawn))
            {
                return false;
            }

            if (ModsConfig.BiotechActive && pawn.genes != null && requestedXenotype != null &&
                pawn.genes.Xenotype != requestedXenotype)
            {
                pawn.genes.SetXenotype(requestedXenotype);
            }

            EnsureRatkinIdentity(pawn, generationKindDef, stage);
            EnsureMouseDisasterBackstories(pawn, stage);
            bool hasFixedAge = fixedAgeYears.HasValue &&
                               !float.IsNaN(fixedAgeYears.Value) &&
                               !float.IsInfinity(fixedAgeYears.Value);
            if (hasFixedAge)
            {
                SetBiologicalAgeYears(pawn, fixedAgeYears.Value);
            }
            else
            {
                NormalizeMouseEggAge(pawn, stage);
            }
            EnsureRatEggMobility(pawn);
            EnsureStageInventoryClear(pawn, stage);
            StripRatEggEquipmentIfNeeded(pawn, stage);
            TryApplyRandomRatEggMissingParts(pawn);
            ApplyMouseDisasterGenes(pawn);
            TryApplyRatEggExtendedTrait(pawn, stage);
            NormalizeGeneratedTraits(pawn, stage);
            AssignDisasterApparel(pawn, stage);

            if (!hasFixedAge && stage.Adult() && (pawn.ageTracker.AgeBiologicalYearsFloat < RatkinAdultMinAgeYears || pawn.ageTracker.AgeBiologicalYearsFloat > RatkinAdultMaxAgeYears))
            {
                SetBiologicalAgeYears(pawn, Rand.Range(RatkinAdultMinAgeYears, RatkinAdultMaxAgeYears));
            }

            if (!hasFixedAge && stage == DevelopmentalStage.Child && (pawn.ageTracker.AgeBiologicalYearsFloat < RatkinYoungChildMinAgeYears || pawn.ageTracker.AgeBiologicalYearsFloat > RatkinYoungChildMaxAgeYears))
            {
                SetBiologicalAgeYears(pawn, Rand.Range(RatkinYoungChildMinAgeYears, RatkinYoungChildMaxAgeYears));
            }

            MouseDisasterPawnHistoryDefinition history = MouseDisasterPawnHistoryCatalog.TryApply(pawn, stage);
            MouseDisasterTraitCatalog.TryApply(pawn, stage, history);
            if (!hasFixedAge && !preserveRoleAge && stage != DevelopmentalStage.Baby)
            {
                float lower = Mathf.Max(stage.Adult() ? RatkinAdultMinAgeYears : RatkinYoungChildMinAgeYears,
                    MouseDisasterMod.Settings?.minGeneratedAge ?? 0f);
                float upper = Mathf.Min(stage.Adult() ? 100f : RatkinYoungChildMaxAgeYears,
                    MouseDisasterMod.Settings?.maxGeneratedAge ?? 50f);
                if (lower <= upper && (pawn.ageTracker.AgeBiologicalYearsFloat < lower || pawn.ageTracker.AgeBiologicalYearsFloat > upper))
                    SetBiologicalAgeYears(pawn, Rand.Range(lower, upper));
            }
            RefreshRatkinDevelopmentalPresentation(pawn);

            return true;
        }

        private static PawnGenerationRequest CreateRatkinGenerationRequest(
            PawnKindDef kindDef,
            Faction faction,
            DevelopmentalStage stage,
            bool allowViolenceDisabledTraits,
            Gender? fixedGender,
            XenotypeDef selectedXenotype,
            bool safeXenotypeFallback)
        {
            List<XenotypeDef> allowedXenotypes = null;
            if (ModsConfig.BiotechActive)
            {
                if (safeXenotypeFallback)
                {
                    XenotypeDef safeRatkinXenotype = ResolveRatkinBaseXenotype();
                    if (safeRatkinXenotype != null)
                    {
                        allowedXenotypes = new List<XenotypeDef> { safeRatkinXenotype };
                    }
                    else if (XenotypeDefOf.Baseliner != null)
                    {
                        allowedXenotypes = new List<XenotypeDef> { XenotypeDefOf.Baseliner };
                    }
                }
                else if (selectedXenotype != null)
                {
                    allowedXenotypes = new List<XenotypeDef> { selectedXenotype };
                }
                else
                {
                    allowedXenotypes = ResolveAllowedRatkinXenotypes();
                }
            }

            return new PawnGenerationRequest(
                kindDef,
                faction,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: MouseDisasterPawnGenerationPolicy.AllowRelationsForDirectMouseDisasterGeneration(),
                prohibitedTraits: MouseDisasterGenerationPolicy.ProhibitedTraits,
                allowDowned: stage == DevelopmentalStage.Baby,
                mustBeCapableOfViolence: !allowViolenceDisabledTraits && stage != DevelopmentalStage.Baby,
                developmentalStages: stage,
                fixedGender: fixedGender,
                forceBaselinerChance: 0f,
                allowedXenotypes: allowedXenotypes);
        }

        private static void EnsureRatkinIdentity(Pawn pawn, PawnKindDef generatedKindDef, DevelopmentalStage stage)
        {
            if (pawn == null)
            {
                return;
            }

            if (generatedKindDef != null && generatedKindDef.race == pawn.def)
            {
                pawn.kindDef = generatedKindDef;
            }

            if (!ModsConfig.BiotechActive || pawn.genes == null)
            {
                return;
            }
        }

        private static void EnsureMouseDisasterBackstories(Pawn pawn, DevelopmentalStage stage)
        {
            if (pawn?.story == null)
            {
                return;
            }

            pawn.story.Childhood = MouseDisasterDefOf.MouseDisaster_Newborn;
            pawn.story.Adulthood = stage.Adult() ? MouseDisasterDefOf.MouseDisaster_Refugee : null;
        }

        private static void NormalizeMouseEggAge(Pawn pawn, DevelopmentalStage stage)
        {
            if (pawn?.ageTracker == null)
            {
                return;
            }

            float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
            if (stage == DevelopmentalStage.Baby)
            {
                if (ageYears < RatEggMinAgeYears || ageYears >= 3f)
                {
                    SetBiologicalAgeYears(pawn, RandomRatEggAgeYears());
                }
                return;
            }

            if (stage == DevelopmentalStage.Child && (ageYears < RatkinYoungChildMinAgeYears || ageYears > RatkinYoungChildMaxAgeYears))
            {
                SetBiologicalAgeYears(pawn, Rand.Range(RatkinYoungChildMinAgeYears, RatkinYoungChildMaxAgeYears));
                return;
            }

            if (stage.Adult() && (ageYears < RatkinAdultMinAgeYears || ageYears > RatkinAdultMaxAgeYears))
            {
                SetBiologicalAgeYears(pawn, Rand.Range(RatkinAdultMinAgeYears, RatkinAdultMaxAgeYears));
            }
        }

        private static void AssignBodyTypeForCurrentStage(Pawn pawn)
        {
            if (pawn?.story == null)
            {
                return;
            }

            DevelopmentalStage stage = pawn.DevelopmentalStage;
            if (ModsConfig.BiotechActive)
            {
                if (stage == DevelopmentalStage.Baby && BodyTypeDefOf.Baby != null)
                {
                    pawn.story.bodyType = BodyTypeDefOf.Baby;
                    return;
                }

                if (stage == DevelopmentalStage.Child && BodyTypeDefOf.Child != null)
                {
                    pawn.story.bodyType = BodyTypeDefOf.Child;
                    return;
                }
            }

            if (stage.Adult())
            {
                pawn.story.bodyType = BodyTypeDefOf.Thin ?? (pawn.gender == Gender.Female ? BodyTypeDefOf.Female : BodyTypeDefOf.Male);
            }
        }

        public static Pawn GenerateWildPawn(PawnKindDef kindDef, Faction formerFaction, DevelopmentalStage stage, bool allowViolenceDisabledTraits = false)
        {
            return GenerateWildPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits, null);
        }

        private static Pawn GenerateWildPawn(PawnKindDef kindDef, Faction formerFaction, DevelopmentalStage stage, bool allowViolenceDisabledTraits, Gender? fixedGender)
        {
            Pawn pawn = GenerateRatkinPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits, fixedGender);
            if (pawn == null)
            {
                return null;
            }

            if (formerFaction != null && pawn.Faction != formerFaction)
            {
                pawn.SetFaction(formerFaction);
            }

            SetMouseDisasterFoodLevel(pawn);
            return pawn;
        }

        public static Pawn GenerateThiefPawn(PawnKindDef kindDef, Faction formerFaction, DevelopmentalStage stage, bool allowViolenceDisabledTraits = false)
        {
            return GenerateThiefPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits, null);
        }

        private static Pawn GenerateThiefPawn(PawnKindDef kindDef, Faction formerFaction, DevelopmentalStage stage, bool allowViolenceDisabledTraits, Gender? fixedGender)
        {
            Pawn pawn = GenerateWildPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits, fixedGender);
            if (pawn == null)
            {
                return null;
            }

            pawn.mindState?.mentalStateHandler?.TryStartMentalState(MouseDisasterDefOf.MouseDisaster_ThievingState, null, forced: true, forceWake: true, causedByMood: false, otherPawn: null, transitionSilently: true);
            if (pawn.inventory != null)
            {
                pawn.inventory.innerContainer.ClearAndDestroyContents();
            }

            SetMouseDisasterFoodLevel(pawn);
            return pawn;
        }

        public static Pawn GenerateBeggarPawn(PawnKindDef kindDef, Faction formerFaction, DevelopmentalStage stage, bool allowViolenceDisabledTraits = false)
        {
            return GenerateBeggarPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits, null);
        }

        internal static Pawn GenerateMotherPawn(PawnKindDef kindDef, Faction formerFaction, DevelopmentalStage stage, bool allowViolenceDisabledTraits = false)
        {
            return GenerateBeggarPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits, Gender.Female);
        }

        private static Pawn GenerateBeggarPawn(PawnKindDef kindDef, Faction formerFaction, DevelopmentalStage stage, bool allowViolenceDisabledTraits, Gender? fixedGender)
        {
            Pawn pawn = GenerateThiefPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits, fixedGender);
            if (pawn == null)
            {
                return null;
            }

            pawn.mindState?.mentalStateHandler?.TryStartMentalState(MouseDisasterDefOf.MouseDisaster_BeggingState, null, forced: true, forceWake: true, causedByMood: false, otherPawn: null, transitionSilently: true);
            ResetBeggarState(pawn);
            return pawn;
        }

        public static Pawn GenerateFactionRatkinPawn(PawnKindDef kindDef, Faction faction, DevelopmentalStage stage, float foodPercentage = 0.35f, bool allowViolenceDisabledTraits = false)
        {
            return GenerateFactionRatkinPawn(kindDef, faction, stage, foodPercentage, allowViolenceDisabledTraits, null, null);
        }

        public static Pawn GenerateFactionRatkinPawn(
            PawnKindDef kindDef,
            Faction faction,
            DevelopmentalStage stage,
            float foodPercentage,
            bool allowViolenceDisabledTraits,
            Gender? fixedGender = null,
            float? fixedAgeYears = null)
        {
            Pawn pawn = GenerateRatkinPawn(kindDef, faction, stage, allowViolenceDisabledTraits, fixedGender, fixedAgeYears);
            if (pawn == null)
            {
                return null;
            }

            if (faction != null && pawn.Faction != faction)
            {
                pawn.SetFaction(faction);
            }

            SetMouseDisasterFoodLevel(pawn, foodPercentage);

            return pawn;
        }

        public static IEnumerable<Pawn> SpawnTravelerGroup(Map map, IntVec3 center, int adults, int children, Faction faction = null, float foodPercentage = 0.35f)
        {
            List<Pawn> pawns = new List<Pawn>();
            if (map == null)
            {
                return pawns;
            }

            if (faction == null)
            {
                TryFindFormerFaction(out faction);
            }

            for (int index = 0; index < adults; index++)
            {
                Pawn pawn = GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult, faction, DevelopmentalStage.Adult, foodPercentage, allowViolenceDisabledTraits: true);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(center, map, 6), map);
                pawns.Add(pawn);
            }

            for (int index = 0; index < children; index++)
            {
                Pawn pawn = GenerateFactionRatkinPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, faction, DevelopmentalStage.Child, foodPercentage, allowViolenceDisabledTraits: true);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(center, map, 6), map);
                StripRatEggInventory(pawn);
                pawns.Add(pawn);
            }

            return pawns;
        }

        private static void SetMouseDisasterFoodLevel(Pawn pawn, float? preferredPercent = null)
        {
            if (pawn?.needs?.food == null)
            {
                return;
            }

            float maxLevel = Mathf.Max(0.001f, pawn.needs.food.MaxLevel);
            float normalizedPercent = preferredPercent.HasValue
                ? Mathf.Clamp(preferredPercent.Value, MouseDisasterFoodMinPercent, MouseDisasterFoodMaxPercent)
                : Rand.Range(MouseDisasterFoodMinPercent, MouseDisasterFoodMaxPercent);

            if (maxLevel >= MouseDisasterFoodMaxAbsolute)
            {
                float absoluteLevel = Mathf.Lerp(MouseDisasterFoodMinAbsolute, MouseDisasterFoodMaxAbsolute, Mathf.InverseLerp(MouseDisasterFoodMinPercent, MouseDisasterFoodMaxPercent, normalizedPercent));
                pawn.needs.food.CurLevel = Mathf.Clamp(absoluteLevel, 0f, maxLevel);
                return;
            }

            pawn.needs.food.CurLevel = Mathf.Clamp(maxLevel * normalizedPercent, 0f, maxLevel);
        }

        public static void SetBiologicalAgeYears(Pawn pawn, float years)
        {
            if (pawn?.ageTracker == null)
            {
                return;
            }

            long ticks = (long)(years * GenDate.TicksPerYear);
            pawn.ageTracker.AgeBiologicalTicks = ticks;
            pawn.ageTracker.AgeChronologicalTicks = ticks;
            RefreshRatkinDevelopmentalPresentation(pawn);
        }

        public static float RandomRatEggAgeYears()
        {
            return RandomRatEggAgeYears(RatEggMaxAgeYears);
        }

        public static float RandomRatEggAgeYears(float maximumAgeYears)
        {
            return Rand.Range(RatEggMinAgeYears, Mathf.Max(RatEggMinAgeYears, maximumAgeYears));
        }

        public static int CalculateGroupCount(float points)
        {
            int maxCount = CalculateEscalatingFixedCount(points, 2, 10, 120f);
            return Rand.RangeInclusive(2, maxCount);
        }

        public static int CalculateEscalatingGroupCount(float points, int min, int max, float pointsPerStep)
        {
            int maxCount = CalculateEscalatingFixedCount(points, min, max, pointsPerStep);
            return Rand.RangeInclusive(min, maxCount);
        }

        public static int CalculateEscalatingFixedCount(float points, int min, int max, float pointsPerStep)
        {
            return LimitEventPawnCount(Mathf.Clamp(Mathf.RoundToInt(points / pointsPerStep) + min, min, max), min);
        }

        public static int LimitEventPawnCount(int requested, int minimum = 1)
        {
            int limit = Mathf.Clamp(MouseDisasterMod.Settings?.maxEventPawns ?? 30, 1, 100);
            return limit < minimum ? requested : Mathf.Min(requested, limit);
        }

        public static PawnKindDef RandomWildKind(bool allowChildren)
        {
            if (allowChildren && Rand.Chance(0.5f))
            {
                return MouseDisasterDefOf.MouseDisaster_WildRatkinChild;
            }

            return MouseDisasterDefOf.MouseDisaster_WildRatkinAdult;
        }

        public static IEnumerable<Pawn> SpawnWildGroup(Map map, IntVec3 center, int count, Faction formerFaction)
        {
            List<Pawn> pawns = new List<Pawn>();
            bool allowChildren = Find.Storyteller.difficulty.ChildrenAllowed;

            for (int index = 0; index < count; index++)
            {
                PawnKindDef kindDef = RandomWildKind(allowChildren);
                DevelopmentalStage stage = kindDef == MouseDisasterDefOf.MouseDisaster_WildRatkinChild ? DevelopmentalStage.Child : DevelopmentalStage.Adult;
                Pawn pawn = GenerateWildPawn(kindDef, formerFaction, stage);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(center, map, 6), map);
                StripRatEggInventory(pawn);
                pawns.Add(pawn);
            }

            return pawns;
        }

        public static IEnumerable<Pawn> SpawnThiefGroup(Map map, IntVec3 center, int count, bool childOnly)
        {
            List<Pawn> pawns = new List<Pawn>();
            TryFindFormerFaction(out Faction formerFaction);
            if (formerFaction != null)
            {
                MakeFactionNeutralToPlayer(formerFaction, force: true);
                EnsureMouseDisasterFactionNeutralOnMap(map, formerFaction);
            }

            bool allowChildren = childOnly || Find.Storyteller.difficulty.ChildrenAllowed;

            for (int index = 0; index < count; index++)
            {
                bool useChild = childOnly || (allowChildren && Rand.Chance(0.45f));
                PawnKindDef kindDef = useChild ? MouseDisasterDefOf.MouseDisaster_ThiefRatkinChild : MouseDisasterDefOf.MouseDisaster_ThiefRatkinAdult;
                DevelopmentalStage stage = useChild ? DevelopmentalStage.Child : DevelopmentalStage.Adult;
                Pawn pawn = GenerateThiefPawn(kindDef, formerFaction, stage, allowViolenceDisabledTraits: true);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(center, map, 6), map);
                StripRatEggInventory(pawn);
                pawn.jobs?.StartJob(CreateGotoJob(map.Center), JobCondition.InterruptForced);
                pawns.Add(pawn);
            }

            return pawns;
        }

        public static IEnumerable<Pawn> SpawnBeggarGroup(Map map, IntVec3 center, int adults, int children)
        {
            List<Pawn> pawns = new List<Pawn>();
            TryFindFormerFaction(out Faction formerFaction);
            if (formerFaction != null)
            {
                MakeFactionNeutralToPlayer(formerFaction, force: true);
                EnsureMouseDisasterFactionNeutralOnMap(map, formerFaction);
            }

            for (int index = 0; index < adults; index++)
            {
                Pawn pawn = GenerateBeggarPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult, formerFaction, DevelopmentalStage.Adult, allowViolenceDisabledTraits: true);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(center, map, 6), map);
                StripRatEggInventory(pawn);
                pawn.jobs?.StartJob(CreateGotoJob(map.Center), JobCondition.InterruptForced);
                pawns.Add(pawn);
            }

            for (int index = 0; index < children; index++)
            {
                Pawn pawn = GenerateBeggarPawn(MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild, formerFaction, DevelopmentalStage.Child, allowViolenceDisabledTraits: true);
                if (pawn == null)
                {
                    continue;
                }

                GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(center, map, 6), map);
                StripRatEggInventory(pawn);
                pawn.jobs?.StartJob(CreateGotoJob(map.Center), JobCondition.InterruptForced);
                pawns.Add(pawn);
            }

            return pawns;
        }

        private static PawnKindDef ResolveRatkinKindDef(PawnKindDef preferredKindDef, DevelopmentalStage stage, ThingDef expectedRace)
        {
            if (stage == DevelopmentalStage.Baby)
            {
                PawnKindDef mappedBabyKind = ResolveMappedBabyRatkinKindDef(preferredKindDef, expectedRace);
                if (mappedBabyKind != null)
                {
                    return mappedBabyKind;
                }
            }

            if (preferredKindDef != null &&
                (expectedRace == null || preferredKindDef.race == expectedRace) &&
                CanGenerateRequestedStage(preferredKindDef, stage))
            {
                return preferredKindDef;
            }

            string defName = preferredKindDef?.defName ?? string.Empty;
            bool isAdult = stage.Adult();
            PawnKindDef categorized = null;
            if (defName.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                categorized = MouseDisasterDefOf.MouseDisaster_TraderRatkinAdult;
            }
            else if (defName.IndexOf("Thief", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                categorized = isAdult ? MouseDisasterDefOf.MouseDisaster_ThiefRatkinAdult : MouseDisasterDefOf.MouseDisaster_ThiefRatkinChild;
            }
            else if (defName.IndexOf("Beggar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                categorized = isAdult ? MouseDisasterDefOf.MouseDisaster_BeggarRatkinAdult : MouseDisasterDefOf.MouseDisaster_BeggarRatkinChild;
            }

            if (categorized != null &&
                (expectedRace == null || categorized.race == expectedRace) &&
                CanGenerateRequestedStage(categorized, stage))
            {
                return categorized;
            }

            PawnKindDef fallback = isAdult ? MouseDisasterDefOf.MouseDisaster_WildRatkinAdult : MouseDisasterDefOf.MouseDisaster_WildRatkinChild;
            if (!CanGenerateRequestedStage(fallback, stage))
            {
                fallback = null;
            }

            if (expectedRace != null && (fallback == null || fallback.race != expectedRace))
            {
                fallback = DefDatabase<PawnKindDef>.AllDefsListForReading
                    .FirstOrDefault(kind => kind != null &&
                                            kind.race == expectedRace &&
                                            CanGenerateRequestedStage(kind, stage));
            }

            return fallback;
        }

        private static PawnKindDef ResolveMappedBabyRatkinKindDef(PawnKindDef preferredKindDef, ThingDef expectedRace)
        {
            string preferredDefName = preferredKindDef?.defName;
            if (preferredDefName.NullOrEmpty())
            {
                return null;
            }

            string candidateDefName = preferredDefName.Replace("Child", "Baby");
            if (candidateDefName == preferredDefName)
            {
                return null;
            }

            PawnKindDef mapped = DefDatabase<PawnKindDef>.GetNamedSilentFail(candidateDefName);
            if (mapped == null)
            {
                return null;
            }

            if (expectedRace != null && mapped.race != expectedRace)
            {
                return null;
            }

            return mapped;
        }

        private static bool CanGenerateRequestedStage(PawnKindDef kindDef, DevelopmentalStage stage)
        {
            if (kindDef == null)
            {
                return false;
            }

            if (stage == DevelopmentalStage.Baby)
            {
                return kindDef.minGenerationAge < RatkinYoungChildMinAgeYears;
            }

            if (stage == DevelopmentalStage.Child)
            {
                return kindDef.maxGenerationAge > RatkinYoungChildMinAgeYears &&
                       kindDef.minGenerationAge < RatkinAdultMinAgeYears;
            }

            return kindDef.maxGenerationAge >= RatkinAdultMinAgeYears;
        }

        private static void EnsureStageInventoryClear(Pawn pawn, DevelopmentalStage stage)
        {
            if (pawn?.inventory?.innerContainer == null)
            {
                return;
            }

            if (stage == DevelopmentalStage.Baby || stage == DevelopmentalStage.Child)
            {
                pawn.inventory.innerContainer.ClearAndDestroyContents();
            }
        }

        public static void StripRatEggInventory(Pawn pawn)
        {
            if (pawn == null || !IsMouseEggBaby(pawn) || pawn.inventory?.innerContainer == null)
            {
                return;
            }

            pawn.inventory.innerContainer.ClearAndDestroyContents();
        }

        public static void RemoveAllIncidentVisitorHediffs(Pawn pawn)
        {
            if (pawn?.health == null)
            {
                return;
            }

            for (int i = 0; i < IncidentVisitorHediffs.Length; i++)
            {
                RemoveHediffByDef(pawn, IncidentVisitorHediffs[i]);
            }
        }

        private static void StripRatEggEquipmentIfNeeded(Pawn pawn, DevelopmentalStage stage)
        {
            if (pawn == null || stage != DevelopmentalStage.Baby)
            {
                return;
            }

            pawn.equipment?.DestroyAllEquipment();
        }

        public static void RefreshRatkinDevelopmentalPresentation(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            AssignBodyTypeForCurrentStage(pawn);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            pawn.ageTracker?.PostResolveLifeStageChange();
            TryEnsureToddlerCompatibilityHediffs(pawn);
        }

        public static void EnsureRatEggMobility(Pawn pawn)
        {
            if (pawn == null || !IsMouseEggBaby(pawn) || pawn.ageTracker == null)
            {
                return;
            }

            if (pawn.ageTracker.AgeBiologicalYearsFloat < RatEggMinAgeYears)
            {
                SetBiologicalAgeYears(pawn, RandomRatEggAgeYears());
            }
            if (MouseDisasterInfantMobilityPatch.UsesFallback(pawn) && pawn.Downed)
            {
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.Notify_HediffChanged(null);
            }
        }
    }
}
