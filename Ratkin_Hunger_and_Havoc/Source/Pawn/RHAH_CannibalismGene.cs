using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_CannibalismGene
    {
        internal const string DefName = "RHAH_Gene_Cannibalism";

        internal static Gene ActiveGene(Verse.Pawn pawn)
        {
            if (!ModsConfig.BiotechActive || pawn?.genes == null)
            {
                return null;
            }
            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                Gene gene = genes[i];
                if (gene.def.defName == DefName && gene.Active)
                {
                    return gene;
                }
            }
            return null;
        }

        internal static bool ExemptsHistoryEvent(HistoryEventDef eventDef)
        {
            if (eventDef == null)
            {
                return false;
            }
            switch (eventDef.defName)
            {
                case "AteHumanMeat":
                case "AteHumanMeatDirect":
                case "AteHumanMeatAsIngredient":
                case "ObservedLayingCorpse":
                case "ObservedLayingRottingCorpse":
                    return true;
                default:
                    return false;
            }
        }
    }

    // 原版 nullifyingGenes 未检查 Active 这里只纠正本基因并保留其它豁免来源
    [HarmonyPatch(typeof(ThoughtUtility), nameof(ThoughtUtility.NullifyingGene))]
    internal static class RHAH_CannibalismNullifyingGenePatch
    {
        internal static void Postfix(ThoughtDef def, Verse.Pawn pawn, ref Gene __result)
        {
            if (__result == null || __result.def.defName != RHAH_CannibalismGene.DefName || __result.Active)
            {
                return;
            }
            __result = null;
            for (int i = 0; i < def.nullifyingGenes.Count; i++)
            {
                GeneDef geneDef = def.nullifyingGenes[i];
                Gene gene = geneDef.defName == RHAH_CannibalismGene.DefName
                    ? RHAH_CannibalismGene.ActiveGene(pawn)
                    : pawn.genes.GetGene(geneDef);
                if (gene != null)
                {
                    __result = gene;
                    return;
                }
            }
        }
    }

    // 生食厌恶按人肉来源豁免 不扩及生菜或普通动物肉
    [HarmonyPatch(typeof(FoodUtility), "TryAddIngestThought")]
    internal static class RHAH_CannibalismRawFoodPatch
    {
        internal static bool Prefix(Verse.Pawn ingester, ThoughtDef def, MeatSourceCategory meatSourceCategory)
        {
            return def.defName != "AteRawFood" || meatSourceCategory != MeatSourceCategory.Humanlike ||
                RHAH_CannibalismGene.ActiveGene(ingester) == null;
        }
    }

    [HarmonyPatch(typeof(Ideo), nameof(Ideo.MemberWillingToDo))]
    internal static class RHAH_CannibalismWillingnessPatch
    {
        internal static void Postfix(HistoryEvent ev, ref bool __result)
        {
            if (!__result && RHAH_CannibalismGene.ExemptsHistoryEvent(ev.def) &&
                ev.args.TryGetArg(HistoryEventArgsNames.Doer, out Verse.Pawn pawn) &&
                RHAH_CannibalismGene.ActiveGene(pawn) != null)
            {
                __result = true;
            }
        }
    }
}
