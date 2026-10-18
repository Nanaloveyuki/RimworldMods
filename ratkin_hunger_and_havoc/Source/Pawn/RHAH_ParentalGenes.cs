using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using VersePawn = Verse.Pawn;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_ParentalGenes
    {
        internal const string MaternalInstinct = "RHAH_Gene_MaternalInstinct";
        internal const string PaternalInstinct = "RHAH_Gene_PaternalInstinct";
        internal const float DefaultMaternalMilkPercent = 150f;
        internal const float DefaultMaternalHungerPercent = 115f;
        internal const float DefaultMaternalNutritionPercent = 125f;
        internal const float DefaultPaternalWorkPercent = 120f;
        internal const float DefaultPaternalHungerPercent = 110f;
        internal const float DefaultPaternalLearningPercent = 110f;
        internal const float DefaultPaternalDamagePercent = 90f;
        internal const float MinPercent = 1f;
        internal const float MaxPercent = 1000f;
        internal const float MaxDamagePercent = 100f;

        internal static float ClampPercent(float value, float fallback, float max = MaxPercent)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Min(max, Math.Max(MinPercent, value));
        }

        internal static Gene_RHAH_ParentalInstinct Find(VersePawn pawn, string defName, bool requireActive = true)
        {
            List<Gene> genes = pawn?.genes?.GenesListForReading;
            if (!ModsConfig.BiotechActive || genes == null)
            {
                return null;
            }

            for (int i = 0; i < genes.Count; i++)
            {
                if (genes[i].def.defName == defName && genes[i] is Gene_RHAH_ParentalInstinct gene && (!requireActive || gene.BaseActive))
                {
                    return gene;
                }
            }

            return null;
        }

        internal static bool MaternalEffect(VersePawn pawn)
        {
            Gene_RHAH_ParentalInstinct gene = Find(pawn, MaternalInstinct);
            return gene != null && gene.Active && pawn.health?.hediffSet?.HasHediff(HediffDefOf.Lactating) == true;
        }

        internal static bool PaternalEffect(VersePawn pawn)
        {
            Gene_RHAH_ParentalInstinct gene = Find(pawn, PaternalInstinct);
            return gene != null && gene.Active && gene.HasChildOnMap();
        }

        internal static float MilkFactor(VersePawn pawn)
        {
            return MaternalEffect(pawn) ? (RHAH_Mod.Settings?.maternalMilkPercent ?? DefaultMaternalMilkPercent) / 100f : 1f;
        }

        internal static float HungerFactor(VersePawn pawn)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            float factor = MaternalEffect(pawn) ? (settings?.maternalHungerPercent ?? DefaultMaternalHungerPercent) / 100f : 1f;
            if (PaternalEffect(pawn))
            {
                factor *= (settings?.paternalHungerPercent ?? DefaultPaternalHungerPercent) / 100f;
            }

            return factor;
        }

        internal static float StatFactor(VersePawn pawn, StatDef stat)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (stat == StatDefOf.MaxNutrition)
            {
                return MaternalEffect(pawn) ? (settings?.maternalNutritionPercent ?? DefaultMaternalNutritionPercent) / 100f : 1f;
            }

            if (!PaternalEffect(pawn))
            {
                return 1f;
            }

            if (stat == StatDefOf.WorkSpeedGlobal)
            {
                return (settings?.paternalWorkPercent ?? DefaultPaternalWorkPercent) / 100f;
            }

            if (stat == StatDefOf.GlobalLearningFactor)
            {
                return (settings?.paternalLearningPercent ?? DefaultPaternalLearningPercent) / 100f;
            }

            return stat == StatDefOf.IncomingDamageFactor ? (settings?.paternalDamagePercent ?? DefaultPaternalDamagePercent) / 100f : 1f;
        }
    }

    // XML 基因入口
    public sealed class Gene_RHAH_ParentalInstinct : Gene
    {
        List<VersePawn> children;
        bool childrenDirty = true;

        internal bool BaseActive => base.Active;

        public override bool Active
        {
            get
            {
                if (!base.Active)
                {
                    return false;
                }

                if (def.defName == RHAH_ParentalGenes.MaternalInstinct && pawn.gender == Gender.Male)
                {
                    return RHAH_ParentalGenes.Find(pawn, RHAH_ParentalGenes.PaternalInstinct) == null;
                }

                if (def.defName == RHAH_ParentalGenes.PaternalInstinct && pawn.gender == Gender.Female)
                {
                    return RHAH_ParentalGenes.Find(pawn, RHAH_ParentalGenes.MaternalInstinct) == null;
                }

                return true;
            }
        }

        internal void InvalidateChildren()
        {
            childrenDirty = true;
        }

        internal bool HasChildOnMap()
        {
            Map map = pawn.MapHeld;
            if (map == null || pawn.relations == null)
            {
                return false;
            }

            if (childrenDirty)
            {
                if (children == null)
                {
                    children = new List<VersePawn>();
                }

                children.Clear();
                foreach (VersePawn child in pawn.relations.Children)
                {
                    children.Add(child);
                }

                childrenDirty = false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                VersePawn child = children[i];
                if (!child.Dead && !child.Destroyed && child.MapHeld == map)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation))]
    internal static class RHAH_ParentalRelationAddedPatch
    {
        static void Postfix(PawnRelationDef def, VersePawn otherPawn)
        {
            if (def == PawnRelationDefOf.Parent)
            {
                RHAH_ParentalGenes.Find(otherPawn, RHAH_ParentalGenes.PaternalInstinct, false)?.InvalidateChildren();
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.TryRemoveDirectRelation))]
    internal static class RHAH_ParentalRelationRemovedPatch
    {
        static void Postfix(PawnRelationDef def, VersePawn otherPawn, bool __result)
        {
            if (__result && def == PawnRelationDefOf.Parent)
            {
                RHAH_ParentalGenes.Find(otherPawn, RHAH_ParentalGenes.PaternalInstinct, false)?.InvalidateChildren();
            }
        }
    }

    [HarmonyPatch(typeof(Need_Food), nameof(Need_Food.FoodFallPerTickAssumingCategory))]
    internal static class RHAH_ParentalHungerPatch
    {
        static void Postfix(VersePawn ___pawn, ref float __result)
        {
            __result *= RHAH_ParentalGenes.HungerFactor(___pawn);
        }
    }

    [HarmonyPatch(typeof(HediffComp_Chargeable), nameof(HediffComp_Chargeable.Props), MethodType.Getter)]
    internal static class RHAH_MaternalMilkPatch
    {
        static readonly ConditionalWeakTable<HediffComp_Lactating, HediffCompProperties_Lactating> properties =
            new ConditionalWeakTable<HediffComp_Lactating, HediffCompProperties_Lactating>();

        static void Postfix(HediffComp_Chargeable __instance, ref HediffCompProperties_Chargeable __result)
        {
            if (!(__instance is HediffComp_Lactating lactating))
            {
                return;
            }

            float factor = RHAH_ParentalGenes.MilkFactor(lactating.Pawn);
            if (factor == 1f)
            {
                return;
            }

            if (!properties.TryGetValue(lactating, out HediffCompProperties_Lactating adjusted))
            {
                adjusted = new HediffCompProperties_Lactating();
                properties.Add(lactating, adjusted);
            }

            // Def 的 comp 属性由所有角色共享，倍率只写入当前哺乳组件的副本
            adjusted.compClass = __result.compClass;
            adjusted.ticksToFullCharge = __result.ticksToFullCharge;
            adjusted.initialCharge = __result.initialCharge * factor;
            adjusted.fullChargeAmount = __result.fullChargeAmount * factor;
            adjusted.minChargeToActivate = __result.minChargeToActivate;
            adjusted.labelInBrackets = __result.labelInBrackets;
            __result = adjusted;
        }
    }
}
