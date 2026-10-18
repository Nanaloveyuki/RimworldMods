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

        private static void TryApplyRatEggExtendedTrait(Pawn pawn, DevelopmentalStage stage)
        {
            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (!MouseDisasterRuntime.AllowsNewContent || (settings != null && !settings.enableRatEggTraitsBridge) || pawn?.story?.traits == null || !IsRatkin(pawn))
            {
                return;
            }

            if (stage != DevelopmentalStage.Baby && stage != DevelopmentalStage.Child)
            {
                return;
            }

            float chance = Mathf.Clamp01(settings?.ratEggTraitGenerationChance ?? 0.5f);
            if (!Rand.Chance(chance))
            {
                return;
            }

            List<TraitDef> pool = GetRatEggTraitPool();
            if (pool.Count == 0)
            {
                return;
            }

            for (int attempt = 0; attempt < 32; attempt++)
            {
                TraitDef selected = pool.RandomElement();
                if (selected == null || !CanAssignTraitDef(pawn, selected))
                {
                    continue;
                }

                int degree = 0;
                if (selected.degreeDatas != null && selected.degreeDatas.Count > 0)
                {
                    degree = selected.degreeDatas[0].degree;
                }

                pawn.story.traits.GainTrait(new Trait(selected, degree, false), suppressConflicts: false);
                return;
            }
        }

        private static void NormalizeGeneratedTraits(Pawn pawn, DevelopmentalStage stage)
        {
            if (pawn?.story?.traits == null)
            {
                return;
            }

            TraitSet traitSet = pawn.story.traits;
            foreach (Trait trait in traitSet.allTraits.Where(t => t.sourceGene == null &&
                !MouseDisasterGenerationPolicy.AllowsTrait(t.def)).ToList())
                traitSet.RemoveTrait(trait);
            List<Trait> generatedTraits = traitSet.allTraits
                .Where(trait => trait != null && trait.sourceGene == null &&
                    !MouseDisasterGenerationPolicy.IsOwnedTrait(trait.def))
                .ToList();

            float randomShootingKeepChance = GetRandomShootingKeepChance(stage);
            for (int i = generatedTraits.Count - 1; i >= 0; i--)
            {
                Trait trait = generatedTraits[i];
                if (!IsRandomShootingTrait(trait) || Rand.Chance(randomShootingKeepChance))
                {
                    continue;
                }

                traitSet.RemoveTrait(trait);
                generatedTraits.RemoveAt(i);
            }

            int desiredCount = GetDesiredGeneratedTraitCount(stage);
            if (generatedTraits.Count > desiredCount)
            {
                int desiredNegativeCount = desiredCount > 0 ? Mathf.Clamp(Mathf.CeilToInt(desiredCount * NegativeTraitTargetRatio), 0, desiredCount) : 0;
                List<Trait> negatives = generatedTraits.Where(IsNegativeTrait).ToList();
                List<Trait> neutrals = generatedTraits.Where(trait => !IsNegativeTrait(trait)).ToList();
                List<Trait> kept = new List<Trait>();

                for (int i = 0; i < negatives.Count && kept.Count < desiredNegativeCount; i++)
                {
                    kept.Add(negatives[i]);
                }

                for (int i = 0; i < negatives.Count && kept.Count < desiredCount; i++)
                {
                    if (!kept.Contains(negatives[i]))
                    {
                        kept.Add(negatives[i]);
                    }
                }

                for (int i = 0; i < neutrals.Count && kept.Count < desiredCount; i++)
                {
                    kept.Add(neutrals[i]);
                }

                for (int i = generatedTraits.Count - 1; i >= 0; i--)
                {
                    Trait trait = generatedTraits[i];
                    if (!kept.Contains(trait))
                    {
                        traitSet.RemoveTrait(trait);
                    }
                }

                generatedTraits = kept.ToList();
            }

            int targetNegative = desiredCount > 0 ? Mathf.Clamp(Mathf.CeilToInt(desiredCount * NegativeTraitTargetRatio), 0, desiredCount) : 0;
            int currentNegative = generatedTraits.Count(IsNegativeTrait);
            int addAttempts = 0;
            while ((generatedTraits.Count < desiredCount || currentNegative < targetNegative) && addAttempts < 24)
            {
                addAttempts++;
                if (!TryAddRandomNegativeTrait(pawn, out Trait added))
                {
                    break;
                }

                generatedTraits.Add(added);
                currentNegative++;
            }

            if (generatedTraits.Count > desiredCount)
            {
                List<Trait> removable = generatedTraits.Where(trait => !IsNegativeTrait(trait)).ToList();
                for (int i = 0; i < removable.Count && generatedTraits.Count > desiredCount; i++)
                {
                    traitSet.RemoveTrait(removable[i]);
                    generatedTraits.Remove(removable[i]);
                }

                for (int i = generatedTraits.Count - 1; i >= 0 && generatedTraits.Count > desiredCount; i--)
                {
                    traitSet.RemoveTrait(generatedTraits[i]);
                    generatedTraits.RemoveAt(i);
                }
            }
        }

        private static int GetDesiredGeneratedTraitCount(DevelopmentalStage stage)
        {
            if (stage == DevelopmentalStage.Baby)
            {
                return RollWeightedCount(0.34f, 0.34f, 0.20f, 0.08f, 0.04f);
            }

            if (stage == DevelopmentalStage.Child)
            {
                return RollWeightedCount(0.26f, 0.28f, 0.24f, 0.14f, 0.08f);
            }

            return RollWeightedCount(0.20f, 0.26f, 0.26f, 0.16f, 0.12f);
        }

        private static bool TryAddRandomNegativeTrait(Pawn pawn, out Trait addedTrait)
        {
            addedTrait = null;
            if (pawn?.story?.traits == null)
            {
                return false;
            }

            List<TraitDef> pool = GetNegativeTraitPool();
            if (pool.Count == 0)
            {
                return false;
            }

            for (int attempt = 0; attempt < 32; attempt++)
            {
                TraitDef traitDef = pool.RandomElement();
                if (traitDef == null || !CanAssignTraitDef(pawn, traitDef))
                {
                    continue;
                }

                if (IsRandomShootingTrait(traitDef) && !Rand.Chance(RandomShootingAdultKeepChance))
                {
                    continue;
                }

                if (!TryPickNegativeDegree(traitDef, out int degree))
                {
                    continue;
                }

                Trait trait = new Trait(traitDef, degree, false);
                pawn.story.traits.GainTrait(trait, suppressConflicts: false);
                addedTrait = pawn.story.traits.allTraits
                    .LastOrDefault(existing => existing != null && existing.def == traitDef && existing.Degree == degree);
                return addedTrait != null;
            }

            return false;
        }

        private static List<TraitDef> GetNegativeTraitPool()
        {
            if (negativeTraitPoolResolved)
            {
                return negativeTraitPool ?? new List<TraitDef>();
            }

            negativeTraitPoolResolved = true;
            negativeTraitPool = DefDatabase<TraitDef>.AllDefsListForReading
                .Where(def => MouseDisasterGenerationPolicy.AllowsTrait(def) && IsNegativeTraitDef(def) &&
                              !MouseDisasterGenerationPolicy.IsOwnedTrait(def) &&
                              def != TraitDefOf.Gay &&
                              def != TraitDefOf.Bisexual &&
                              def != TraitDefOf.Asexual)
                .ToList();
            return negativeTraitPool;
        }

        private static bool CanAssignTraitDef(Pawn pawn, TraitDef candidate)
        {
            if (pawn?.story?.traits == null || !MouseDisasterGenerationPolicy.AllowsTrait(candidate) || pawn.story.traits.HasTrait(candidate))
            {
                return false;
            }

            List<Trait> allTraits = pawn.story.traits.allTraits;
            for (int i = 0; i < allTraits.Count; i++)
            {
                Trait existing = allTraits[i];
                if (existing == null)
                {
                    continue;
                }

                if (existing.def.ConflictsWith(candidate) || candidate.ConflictsWith(existing.def))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool CanAssignGeneratedTrait(Pawn pawn, TraitDef candidate)
        {
            return CanAssignTraitDef(pawn, candidate);
        }

        private static bool TryPickNegativeDegree(TraitDef traitDef, out int degree)
        {
            degree = 0;
            if (traitDef == null || traitDef.degreeDatas.NullOrEmpty())
            {
                return false;
            }

            List<TraitDegreeData> negativeDegrees = traitDef.degreeDatas
                .Where(data => data != null && IsNegativeTraitDef(traitDef, data.degree))
                .ToList();
            if (negativeDegrees.Count == 0)
            {
                return false;
            }

            TraitDegreeData picked = negativeDegrees.RandomElementByWeight(data => Mathf.Max(0.01f, data.commonality));
            degree = picked.degree;
            return true;
        }

        public static bool TryAddNegativeTraitByKeywords(Pawn pawn, params string[] keywords)
        {
            if (pawn?.story?.traits == null || keywords.NullOrEmpty())
            {
                return false;
            }

            List<TraitDef> matches = GetNegativeTraitPool()
                .Where(def => TraitMatchesAnyKeyword(def, keywords))
                .ToList();
            for (int i = 0; i < matches.Count; i++)
            {
                TraitDef match = matches[i];
                if (match == null || !CanAssignTraitDef(pawn, match) || !TryPickNegativeDegree(match, out int degree))
                {
                    continue;
                }

                pawn.story.traits.GainTrait(new Trait(match, degree, false), suppressConflicts: false);
                return true;
            }

            return false;
        }

        public static void MakeRatEggPureNegative(Pawn pawn, int minimumNegativeTraits, IEnumerable<string[]> preferredKeywordSets = null)
        {
            if (pawn?.story?.traits == null)
            {
                return;
            }

            TraitSet traitSet = pawn.story.traits;
            List<Trait> removable = traitSet.allTraits
                .Where(trait => trait != null && trait.sourceGene == null && !IsNegativeTrait(trait))
                .ToList();
            for (int i = 0; i < removable.Count; i++)
            {
                traitSet.RemoveTrait(removable[i]);
            }

            if (preferredKeywordSets != null)
            {
                foreach (string[] keywordSet in preferredKeywordSets)
                {
                    if (traitSet.allTraits.Count(IsNegativeTrait) >= minimumNegativeTraits)
                    {
                        break;
                    }

                    TryAddNegativeTraitByKeywords(pawn, keywordSet);
                }
            }

            int attempts = 0;
            while (traitSet.allTraits.Count(IsNegativeTrait) < minimumNegativeTraits && attempts < 24)
            {
                attempts++;
                if (!TryAddRandomNegativeTrait(pawn, out _))
                {
                    break;
                }
            }
        }

        private static bool TraitMatchesAnyKeyword(TraitDef traitDef, IEnumerable<string> keywords)
        {
            if (traitDef == null || keywords == null)
            {
                return false;
            }

            string source = ((traitDef.defName ?? string.Empty) + " " + (traitDef.label ?? string.Empty)).ToLowerInvariant();
            foreach (string keyword in keywords)
            {
                if (!keyword.NullOrEmpty() && source.Contains(keyword.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNegativeTrait(Trait trait)
        {
            return trait != null && IsNegativeTraitDef(trait.def, trait.Degree);
        }

        private static float GetRandomShootingKeepChance(DevelopmentalStage stage)
        {
            if (stage == DevelopmentalStage.Baby)
            {
                return RandomShootingBabyKeepChance;
            }

            if (stage == DevelopmentalStage.Child)
            {
                return RandomShootingChildKeepChance;
            }

            return RandomShootingAdultKeepChance;
        }

        private static bool IsRandomShootingTrait(Trait trait)
        {
            return trait != null && IsRandomShootingTrait(trait.def, trait.Degree);
        }

        private static bool IsRandomShootingTrait(TraitDef traitDef)
        {
            if (traitDef == null || traitDef.degreeDatas.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < traitDef.degreeDatas.Count; i++)
            {
                if (IsRandomShootingTrait(traitDef, traitDef.degreeDatas[i].degree))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRandomShootingTrait(TraitDef traitDef, int degree)
        {
            if (traitDef == null)
            {
                return false;
            }

            TraitDegreeData data = traitDef.DataAtDegree(degree);
            if (data == null)
            {
                return false;
            }

            string source = ((traitDef.defName ?? string.Empty) + " " + (traitDef.label ?? string.Empty) + " " + (data.label ?? string.Empty)).ToLowerInvariant();
            for (int i = 0; i < RandomShootingTraitKeywords.Length; i++)
            {
                if (source.Contains(RandomShootingTraitKeywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNegativeTraitDef(TraitDef traitDef)
        {
            if (traitDef == null || traitDef.degreeDatas.NullOrEmpty())
            {
                return false;
            }

            for (int i = 0; i < traitDef.degreeDatas.Count; i++)
            {
                TraitDegreeData data = traitDef.degreeDatas[i];
                if (data != null && IsNegativeTraitDef(traitDef, data.degree))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNegativeTraitDef(TraitDef traitDef, int degree)
        {
            if (traitDef == null)
            {
                return false;
            }

            if (IsRandomShootingTrait(traitDef, degree))
            {
                return false;
            }

            TraitDegreeData data = traitDef.DataAtDegree(degree);
            if (data == null)
            {
                return false;
            }

            if (!traitDef.disabledWorkTypes.NullOrEmpty() || traitDef.disabledWorkTags != WorkTags.None)
            {
                return true;
            }

            if (data.forcedMentalState != null || data.randomMentalState != null || data.socialFightChanceFactor > 1.05f)
            {
                return true;
            }

            if (data.hungerRateFactor > 1.05f || data.painOffset > 0.05f || data.painFactor > 1.02f)
            {
                return true;
            }

            if (data.randomDiseaseMtbDays > 0f && data.randomDiseaseMtbDays < 999f)
            {
                return true;
            }

            string source = ((traitDef.defName ?? string.Empty) + " " + (data.label ?? string.Empty) + " " + (traitDef.label ?? string.Empty)).ToLowerInvariant();
            return source.Contains("lazy") ||
                   source.Contains("wimp") ||
                   source.Contains("slow") ||
                   source.Contains("greedy") ||
                   source.Contains("jealous") ||
                   source.Contains("pess") ||
                   source.Contains("depress") ||
                   source.Contains("neuro") ||
                   source.Contains("abrasive") ||
                   source.Contains("annoy") ||
                   source.Contains("病弱") ||
                   source.Contains("\u61d2") ||
                   source.Contains("迟缓") ||
                   source.Contains("\u8d2a") ||
                   source.Contains("悲观") ||
                   source.Contains("抑郁") ||
                   source.Contains("神经质");
        }

        private static List<TraitDef> GetRatEggTraitPool()
        {
            if (ratEggTraitPoolResolved)
            {
                return ratEggTraitPool ?? new List<TraitDef>();
            }

            ratEggTraitPoolResolved = true;
            ratEggTraitPool = DefDatabase<TraitDef>.AllDefsListForReading
                .Where(def => MouseDisasterGenerationPolicy.AllowsTrait(def) &&
                              !MouseDisasterGenerationPolicy.IsOwnedTrait(def) &&
                              !def.degreeDatas.NullOrEmpty() &&
                              def != TraitDefOf.Gay &&
                              def != TraitDefOf.Bisexual &&
                              def != TraitDefOf.Asexual)
                .ToList();

            return ratEggTraitPool;
        }
    }
}
