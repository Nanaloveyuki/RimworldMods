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

        private static void ApplyMouseDisasterGenes(Pawn pawn)
        {
            if (!IsEligibleForMouseDisasterGenes(pawn))
            {
                StripMouseDisasterGenes(pawn);
                CleanupOrphanedChemicalDependencies(pawn);
                return;
            }

            if (HasManualMouseDisasterGeneRemovalMarker(pawn))
            {
                StripMouseDisasterGenes(pawn);
                CleanupOrphanedChemicalDependencies(pawn);
                return;
            }

            StripNonMouseDisasterGenes(pawn);
            StripMouseDisasterGenes(pawn);
            MouseDisasterSubtypeUtility.ApplySubtypeGenes(pawn);
            CleanupOrphanedChemicalDependencies(pawn);
        }

        public static bool HasActiveGene(Pawn pawn, GeneDef geneDef)
        {
            return ModsConfig.BiotechActive && geneDef != null && pawn?.genes != null && pawn.genes.HasActiveGene(geneDef);
        }

        public static bool HasAnyMouseDisasterGene(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null)
            {
                return false;
            }

            List<GeneDef> pool = GetMouseDisasterGenePool();
            for (int i = 0; i < pool.Count; i++)
                if (pawn.genes.HasActiveGene(pool[i])) return true;
            return false;
        }

        public static void TryAssignBirthMouseDisasterGenes(Pawn newborn, Pawn parent)
        {
            if (!ModsConfig.BiotechActive || newborn?.genes == null || parent == null || !IsRatkin(newborn) || !IsRatkin(parent))
            {
                return;
            }

            if (HasManualMouseDisasterGeneRemovalMarker(newborn))
            {
                StripMouseDisasterGenes(newborn);
                CleanupOrphanedChemicalDependencies(newborn);
                return;
            }

            if (!IsMouseDisasterManagedGenePawn(parent) || !HasAnyMouseDisasterGene(parent))
            {
                StripMouseDisasterGenes(newborn);
                CleanupOrphanedChemicalDependencies(newborn);
                return;
            }

            StripNonMouseDisasterGenes(newborn);
            StripMouseDisasterGenes(newborn);
            MouseDisasterSubtypeUtility.ApplySubtypeGenes(newborn, parent);
            CleanupOrphanedChemicalDependencies(newborn);
        }

        public static void TryForceRatkinBirthXenotype(Pawn newborn, Pawn mother)
        {
            if (!ModsConfig.BiotechActive || newborn?.genes == null || mother == null)
            {
                return;
            }

            bool useMouseDisasterBirthIdentity = ShouldUseMouseDisasterBirthIdentity(newborn, mother);
            if (!MouseDisasterBirthPolicy.ShouldForceMouseDisasterBirthXenotype(
                    shouldUseMouseDisasterBirthIdentity: useMouseDisasterBirthIdentity,
                    motherIsRatkin: IsRatkin(mother),
                    newbornIsRatkin: IsRatkin(newborn),
                    newbornAlreadyRatkinXenotype: !useMouseDisasterBirthIdentity && IsRatkinXenotypeDef(newborn.genes.Xenotype)))
            {
                return;
            }

            XenotypeDef ratkinXenotype = MouseDisasterAdaptiveXenotypeUtility.Choose();
            if (ratkinXenotype != null)
            {
                newborn.genes.SetXenotype(ratkinXenotype);
            }
        }

        private static List<GeneDef> GetMouseDisasterGenePool()
        {
            if (cachedMouseDisasterGenePoolResolved)
            {
                return cachedMouseDisasterGenePool;
            }

            cachedMouseDisasterGenePoolResolved = true;
            cachedMouseDisasterGenePool = new List<GeneDef>
            {
                MouseDisasterDefOf.MouseDisaster_Gene_MoreLitter,
                MouseDisasterDefOf.MouseDisaster_Gene_PrimalFertility,
                MouseDisasterDefOf.MouseDisaster_Gene_HyperBreeding,
                MouseDisasterDefOf.MouseDisaster_Gene_ChaosFertility,
                MouseDisasterDefOf.MouseDisaster_Gene_MoreComing
            }.Where(def => def != null).Distinct().ToList();
            return cachedMouseDisasterGenePool;
        }

        public static void StripMouseDisasterGenes(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null)
            {
                return;
            }

            List<GeneDef> pool = GetMouseDisasterGenePool();
            if (pool.Count == 0)
            {
                return;
            }

            List<Gene> removable = pawn.genes.GenesListForReading
                .Where(gene => gene != null && gene.def != null && pool.Contains(gene.def))
                .ToList();

            using (new MouseDisasterGeneRemovalScope(pawn))
            {
                for (int i = 0; i < removable.Count; i++)
                {
                    pawn.genes.RemoveGene(removable[i]);
                }
            }
        }

        public static bool HasManualMouseDisasterGeneRemovalMarker(Pawn pawn)
        {
            return GeneRestoreState?.HasManualRemovalMarker(pawn) ?? false;
        }

        public static void MarkManualMouseDisasterGeneRemoval(Pawn pawn)
        {
            GeneRestoreState?.MarkManualRemoval(pawn);
        }

        public static void ClearManualMouseDisasterGeneRemovalMarker(Pawn pawn)
        {
            GeneRestoreState?.ClearManualRemoval(pawn);
        }

        public static void BeginInternalMouseDisasterGeneRemoval(Pawn pawn)
        {
            GeneRestoreState?.BeginInternalGeneRemoval(pawn);
        }

        public static void EndInternalMouseDisasterGeneRemoval(Pawn pawn)
        {
            GeneRestoreState?.EndInternalGeneRemoval(pawn);
        }

        public static bool IsInternalMouseDisasterGeneRemovalInProgress(Pawn pawn)
        {
            return GeneRestoreState?.IsInternalGeneRemovalInProgress(pawn) ?? false;
        }

        public static bool IsMouseDisasterManagedGenePawn(Pawn pawn)
        {
            return pawn != null && ModsConfig.BiotechActive && pawn.genes != null && (IsMouseDisasterPawn(pawn) || IsMouseDisasterIncidentVisitor(pawn));
        }

        public static bool IsMouseDisasterGeneDef(GeneDef geneDef)
        {
            return geneDef != null && GetMouseDisasterGenePool().Contains(geneDef);
        }

        public static void NotifyMouseDisasterGeneRemoved(Pawn pawn, GeneDef geneDef)
        {
            if (!MouseDisasterGeneRestorePolicy.ShouldTrackManualRemovalOnGeneRemoved(
                    isManagedPawn: IsMouseDisasterManagedGenePawn(pawn),
                    removedMouseDisasterGene: IsMouseDisasterGeneDef(geneDef)) ||
                IsInternalMouseDisasterGeneRemovalInProgress(pawn))
            {
                return;
            }

            MarkManualMouseDisasterGeneRemoval(pawn);
        }

        public static void NotifyMouseDisasterGeneAdded(Pawn pawn, GeneDef geneDef)
        {
            if (!MouseDisasterGeneRestorePolicy.ShouldClearManualRemovalOnGeneAdded(
                    hasManualRemovalMarker: HasManualMouseDisasterGeneRemovalMarker(pawn),
                    isManagedPawn: IsMouseDisasterManagedGenePawn(pawn),
                    addedMouseDisasterGene: IsMouseDisasterGeneDef(geneDef)))
            {
                return;
            }

            ClearManualMouseDisasterGeneRemovalMarker(pawn);
        }

        public static void CleanupOrphanedChemicalDependencies(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.health?.hediffSet?.hediffs == null)
            {
                return;
            }

            List<Hediff> removable = pawn.health.hediffSet.hediffs
                .Where(hediff => hediff is Hediff_ChemicalDependency dependency &&
                                 (dependency.chemical == null || dependency.LinkedGene == null))
                .ToList();

            for (int i = 0; i < removable.Count; i++)
            {
                pawn.health.RemoveHediff(removable[i]);
            }
        }

        private static void StripNonMouseDisasterGenes(Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null || IsPlayerAffiliatedRatkin(pawn))
            {
                return;
            }

            List<GeneDef> pool = GetMouseDisasterGenePool();
            if (pool.Count == 0)
            {
                return;
            }

            List<Gene> removable = pawn.genes.GenesListForReading
                .Where(gene => gene != null &&
                               gene.def != null &&
                               pawn.genes.IsXenogene(gene) &&
                               pawn.genes.Xenotype?.genes?.Contains(gene.def) != true &&
                               !pool.Contains(gene.def))
                .ToList();

            using (new MouseDisasterGeneRemovalScope(pawn))
            {
                for (int i = 0; i < removable.Count; i++)
                {
                    pawn.genes.RemoveGene(removable[i]);
                }
            }
        }

    }
}
