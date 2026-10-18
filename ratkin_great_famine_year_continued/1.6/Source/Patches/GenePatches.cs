using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.RemoveGene))]
    public static class MouseDisasterManualGeneRemovalPatch
    {
        public static void Postfix(Pawn_GeneTracker __instance, Gene gene)
        {
            MouseDisasterUtility.NotifyMouseDisasterGeneRemoved(__instance?.pawn, gene?.def);
        }
    }

    [HarmonyPatch(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.AddGene), new[] { typeof(GeneDef), typeof(bool) })]
    public static class MouseDisasterManualGeneRestorePatch
    {
        public static void Postfix(Pawn_GeneTracker __instance, Gene __result)
        {
            MouseDisasterUtility.NotifyMouseDisasterGeneAdded(__instance?.pawn, __result?.def);
        }
    }

    internal static class MouseDisasterBirthUtility
    {
        public static PawnKindDef ResolveBirthPawnKindDef(Pawn mother)
        {
            if (mother == null)
            {
                return null;
            }

            if (mother.kindDef?.race == mother.def)
            {
                return mother.kindDef;
            }

            ThingDef motherRace = mother.def;
            if (motherRace == null)
            {
                return mother.kindDef;
            }

            return DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(def => def != null && def.race == motherRace)
                .OrderByDescending(def => def.canMeleeAttack)
                .ThenBy(def => (def.defName ?? string.Empty).Length)
                .FirstOrDefault() ?? mother.kindDef;
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.CanEverProduceChild))]
    public static class MouseDisasterPregnancyEligibilityPatch
    {
        public static void Postfix(Pawn first, Pawn second, ref AcceptanceReport __result)
        {
            if (__result.Accepted)
            {
                return;
            }

            bool hasFertilityOverrideGene = MouseDisasterUtility.HasActiveGene(first, MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility) ||
                                            MouseDisasterUtility.HasActiveGene(second, MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility) ||
                                            MouseDisasterUtility.HasActiveGene(first, MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility) ||
                                            MouseDisasterUtility.HasActiveGene(second, MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility);
            if (!hasFertilityOverrideGene || first == null || second == null || first.Dead || second.Dead || first.gender == second.gender)
            {
                return;
            }

            Pawn male = first.gender == Gender.Male ? first : second;
            Pawn female = first.gender == Gender.Female ? first : second;
            if (!MouseDisasterUtility.CanParticipateInMouseDisasterPregnancy(male) ||
                !MouseDisasterUtility.CanParticipateInMouseDisasterPregnancy(female))
            {
                return;
            }

            bool femaleHasChaosFertility = MouseDisasterUtility.HasActiveGene(female, MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility);
            bool femaleCanCarry = femaleHasChaosFertility || !female.Sterile() || PregnancyUtility.GetPregnancyHediff(female) != null;
            if (!male.Sterile() && femaleCanCarry)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.PregnancyChanceForPawn))]
    public static class MouseDisasterPrimalFertilityChancePatch
    {
        public static void Postfix(Pawn pawn, ref float __result)
        {
            bool hasPrimalFertility = MouseDisasterUtility.HasActiveGene(pawn, MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility);
            bool hasChaosFertility = MouseDisasterUtility.HasActiveGene(pawn, MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility);
            if ((!hasPrimalFertility && !hasChaosFertility) || !Find.Storyteller.difficulty.ChildrenAllowed)
            {
                return;
            }

            if (!MouseDisasterUtility.CanParticipateInMouseDisasterPregnancy(pawn))
            {
                __result = 0f;
                return;
            }

            bool bypassSterility = hasChaosFertility && pawn.gender == Gender.Female;
            if (pawn.Sterile() && !bypassSterility)
            {
                return;
            }

            float minChance = hasChaosFertility ? 0.45f : 0.35f;
            __result = Mathf.Max(__result, minChance);
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.PregnancyChanceForPartners))]
    public static class MouseDisasterHyperBreedingChancePatch
    {
        public static void Postfix(Pawn woman, Pawn man, ref float __result)
        {
            bool hasHyperBreeding = MouseDisasterUtility.HasActiveGene(woman, MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding) ||
                                    MouseDisasterUtility.HasActiveGene(man, MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding);
            if (hasHyperBreeding)
            {
                __result *= 3f;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class MouseDisasterPregnancyMoodPatch
    {
        public static void Postfix(Hediff hediff)
        {
            if (hediff is Hediff_Pregnant && hediff.pawn != null)
            {
                MouseDisasterUtility.TryGainBloodlinePregnancyThought(hediff.pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.TickInterval))]
    public static class MouseDisasterFastGestationPatch
    {
        private const float FixedTotalGestationDays = 12f;

        public static void Prefix(Hediff_Pregnant __instance, ref float __state)
        {
            __state = __instance?.GestationProgress ?? 0f;
        }

        public static void Postfix(Hediff_Pregnant __instance, float __state, int delta)
        {
            if (__instance?.pawn == null || !MouseDisasterUtility.HasActiveGene(__instance.pawn, MouseDisasterDefOf.MouseDisaster_Gene_MoreComing))
            {
                return;
            }

            if (__instance.pawn.health?.hediffSet == null || !__instance.pawn.health.hediffSet.hediffs.Contains(__instance))
            {
                return;
            }

            if (delta <= 0)
            {
                return;
            }

            float fixedGain = delta / (GenDate.TicksPerDay * FixedTotalGestationDays);
            __instance.Severity = Mathf.Clamp01(__state + fixedGain);
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.DoBirthSpawn))]
    public static class MouseDisasterBirthKindPatch
    {
        public static void Prefix(Pawn mother, ref PawnKindDef __state)
        {
            if (mother == null || !MouseDisasterUtility.IsRatkin(mother))
            {
                return;
            }

            PawnKindDef resolvedKind = MouseDisasterBirthUtility.ResolveBirthPawnKindDef(mother);
            if (resolvedKind != null && mother.kindDef != resolvedKind)
            {
                __state = mother.kindDef;
                mother.kindDef = resolvedKind;
            }
        }

        public static void Postfix(Pawn mother, PawnKindDef __state)
        {
            if (mother != null && __state != null)
            {
                mother.kindDef = __state;
            }
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.DoBirthSpawn))]
    public static class MouseDisasterMoreLitterPatch
    {
        private const int MinimumBirthCount = 4;
        private const float ExtraBirthContinueChance = 0.5f;

        public static bool Prefix(Pawn mother, Pawn father)
        {
            if (!MouseDisasterUtility.HasActiveGene(mother, MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter))
            {
                return true;
            }

            DoBirthSpawnFixedRange(mother, father);
            return false;
        }

        private static void DoBirthSpawnFixedRange(Pawn mother, Pawn father)
        {
            if (mother.RaceProps.Humanlike && !ModsConfig.BiotechActive)
            {
                return;
            }

            int birthCount = ResolveMoreLitterBirthCount();

            PawnGenerationRequest request = new PawnGenerationRequest(
                MouseDisasterBirthUtility.ResolveBirthPawnKindDef(mother) ?? mother.kindDef,
                mother.Faction,
                PawnGenerationContext.NonPlayer,
                null,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: true,
                canGeneratePawnRelations: true,
                mustBeCapableOfViolence: false,
                1f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: false,
                allowFood: true,
                allowAddictions: true,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                0f,
                0f,
                null,
                1f,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                forceNoIdeo: false,
                forceNoBackstory: false,
                forbidAnyTitle: false,
                forceDead: false,
                null,
                null,
                null,
                null,
                null,
                0f,
                DevelopmentalStage.Newborn);

            Pawn lastBorn = null;
            for (int i = 0; i < birthCount; i++)
            {
                Pawn newborn = PawnGenerator.GeneratePawn(request);

                lastBorn = newborn;
                if (PawnUtility.TrySpawnHatchedOrBornPawn(newborn, mother))
                {
                    MouseDisasterUtility.RefreshRatkinDevelopmentalPresentation(newborn);
                    if (newborn.playerSettings != null && mother.playerSettings != null)
                    {
                        newborn.playerSettings.AreaRestrictionInPawnCurrentMap = mother.playerSettings.AreaRestrictionInPawnCurrentMap;
                    }

                    if (newborn.RaceProps.IsFlesh)
                    {
                        newborn.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                        if (father != null)
                        {
                            newborn.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);
                        }
                    }

                    if (mother.Spawned)
                    {
                        mother.GetLord()?.AddPawn(newborn);
                    }
                }
                else
                {
                    Find.WorldPawns.PassToWorld(newborn, PawnDiscardDecideMode.Discard);
                }

                TaleRecorder.RecordTale(TaleDefOf.GaveBirth, mother, newborn);
            }

            if (!mother.Spawned)
            {
                return;
            }

            FilthMaker.TryMakeFilth(mother.Position, mother.Map, ThingDefOf.Filth_AmnioticFluid, mother.LabelIndefinite(), 5);
            mother.caller?.DoCall();
            lastBorn?.caller?.DoCall();
        }

        public static int ResolveMoreLitterBirthCount()
        {
            int birthCount = MinimumBirthCount;
            while (Rand.Chance(ExtraBirthContinueChance))
            {
                birthCount++;
            }

            return birthCount;
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    public static class MouseDisasterBirthOutcomeMotherPatch
    {
        public static void Prefix(ref Pawn geneticMother, Thing birtherThing, ref PawnKindDef __state)
        {
            if (!(birtherThing is Pawn birtherPawn) || !MouseDisasterUtility.IsRatkin(birtherPawn))
            {
                return;
            }

            if (geneticMother == null || !MouseDisasterUtility.IsRatkin(geneticMother))
            {
                geneticMother = birtherPawn;
            }

            if (geneticMother == null)
            {
                return;
            }

            PawnKindDef resolvedKind = MouseDisasterBirthUtility.ResolveBirthPawnKindDef(geneticMother);
            if (resolvedKind != null && geneticMother.kindDef != resolvedKind)
            {
                __state = geneticMother.kindDef;
                geneticMother.kindDef = resolvedKind;
            }
        }

        public static void Postfix(Pawn geneticMother, PawnKindDef __state)
        {
            if (geneticMother != null && __state != null)
            {
                geneticMother.kindDef = __state;
            }
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    public static class MouseDisasterMoreLitterBirthOutcomePatch
    {
        public static void Postfix(RitualOutcomePossibility outcome, Pawn geneticMother, Thing birtherThing, Pawn father)
        {
            if (outcome == null || outcome.positivityIndex < 0)
            {
                return;
            }

            Pawn birtherPawn = birtherThing as Pawn;
            Pawn mother = geneticMother ?? birtherPawn;
            if (mother == null || !MouseDisasterUtility.HasActiveGene(mother, MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter))
            {
                return;
            }

            int targetBirthCount = MouseDisasterMoreLitterPatch.ResolveMoreLitterBirthCount();

            int additionalCount = Mathf.Max(0, targetBirthCount - 1);
            for (int i = 0; i < additionalCount; i++)
            {
                SpawnAdditionalNewborn(birtherThing, mother, father);
            }
        }

        private static void SpawnAdditionalNewborn(Thing birtherThing, Pawn mother, Pawn father)
        {
            if (birtherThing == null || mother == null)
            {
                return;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                MouseDisasterBirthUtility.ResolveBirthPawnKindDef(mother) ?? mother.kindDef,
                mother.Faction,
                PawnGenerationContext.NonPlayer,
                null,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: true,
                canGeneratePawnRelations: true,
                mustBeCapableOfViolence: false,
                1f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: false,
                allowFood: true,
                allowAddictions: true,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                0f,
                0f,
                null,
                1f,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                forceNoIdeo: false,
                forceNoBackstory: false,
                forbidAnyTitle: false,
                forceDead: false,
                null,
                null,
                null,
                null,
                null,
                0f,
                DevelopmentalStage.Newborn);

            Pawn newborn = PawnGenerator.GeneratePawn(request);
            if (PawnUtility.TrySpawnHatchedOrBornPawn(newborn, birtherThing))
            {
                MouseDisasterUtility.RefreshRatkinDevelopmentalPresentation(newborn);
                if (newborn.playerSettings != null && mother.playerSettings != null)
                {
                    newborn.playerSettings.AreaRestrictionInPawnCurrentMap = mother.playerSettings.AreaRestrictionInPawnCurrentMap;
                }

                if (newborn.RaceProps.IsFlesh)
                {
                    newborn.relations.AddDirectRelation(PawnRelationDefOf.Parent, mother);
                    if (father != null)
                    {
                        newborn.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);
                    }
                }

                if (mother.Spawned)
                {
                    mother.GetLord()?.AddPawn(newborn);
                }
            }
            else
            {
                Find.WorldPawns.PassToWorld(newborn, PawnDiscardDecideMode.Discard);
            }

            TaleRecorder.RecordTale(TaleDefOf.GaveBirth, mother, newborn);
        }
    }
}
