using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Pawn.Compat;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_XenotypeGeneration
    {
        static readonly AccessTools.FieldRef<XenotypeSet, List<XenotypeChance>> chances =
            AccessTools.FieldRefAccess<XenotypeSet, List<XenotypeChance>>("xenotypeChances");

        internal static bool Owns(PawnKindDef kind)
        {
            return kind?.defName == "RHAH_PawnKind_Ratkin" && kind.race?.defName == "Ratkin";
        }

        internal static bool Owns(FactionDef faction)
        {
            switch (faction?.defName)
            {
                case "RHAH_Faction_Hostile":
                case "RHAH_Faction_LeaningHostile":
                case "RHAH_Faction_Neutral":
                case "RHAH_Faction_LeaningFriendly":
                case "RHAH_Faction_Friendly":
                    return Owns(faction.basicMemberKind);
                default:
                    return false;
            }
        }

        internal static void ReplacePool(PawnKindDef kind, Dictionary<XenotypeDef, float> result)
        {
            if (!ModsConfig.BiotechActive || !Owns(kind))
            {
                return;
            }

            List<XenotypeChance> pool = RHAH_XenotypeResolver.GenerationPool(kind);
            result.Clear();
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                total += pool[i].chance;
            }
            for (int i = 0; i < pool.Count; i++)
            {
                XenotypeChance candidate = pool[i];
                result.TryGetValue(candidate.xenotype, out float previous);
                result[candidate.xenotype] = previous + candidate.chance / total;
            }
        }

        internal static void RefreshFaction(FactionDef faction, ref string cachedDescription)
        {
            if (!ModsConfig.BiotechActive || !Owns(faction))
            {
                return;
            }

            List<XenotypeChance> pool = RHAH_XenotypeResolver.GenerationPool(faction.basicMemberKind);
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                total += pool[i].chance;
            }
            bool changed = faction.xenotypeSet == null || faction.xenotypeSet.Count != pool.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                XenotypeChance candidate = pool[i];
                if (!changed && (faction.xenotypeSet[i].xenotype != candidate.xenotype ||
                    faction.xenotypeSet[i].chance != candidate.chance / total))
                {
                    changed = true;
                }
            }
            if (!changed)
            {
                return;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                XenotypeChance candidate = pool[i];
                pool[i] = new XenotypeChance(candidate.xenotype, candidate.chance / total);
            }
            XenotypeSet updated = new XenotypeSet();
            chances(updated) = pool;
            faction.xenotypeSet = updated;
            cachedDescription = null;
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.XenotypesAvailableFor))]
    internal static class RHAH_AvailableXenotypesPatch
    {
        static void Postfix(PawnKindDef kind, Dictionary<XenotypeDef, float> __result)
        {
            RHAH_XenotypeGeneration.ReplacePool(kind, __result);
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GetXenotypeForGeneratedPawn))]
    internal static class RHAH_GeneratedXenotypePatch
    {
        internal static void Postfix(PawnGenerationRequest request, ref XenotypeDef __result)
        {
            if (!ModsConfig.BiotechActive || !RHAH_XenotypeGeneration.Owns(request.KindDef))
            {
                return;
            }

            // 出生模板：调用方在生成后设置真实异种。原版出生和 RJW 都强制基准人新生，只有原版另外设置 ForceNoIdeo
            if (request.AllowedDevelopmentalStages.Newborn() &&
                request.ForcedXenotype == XenotypeDefOf.Baseliner && __result == XenotypeDefOf.Baseliner)
            {
                return;
            }

            // 调用方显式选择的异种由它自己和 HAR 裁决 这里不改选
            bool explicitSelection = request.ForcedXenotype != null || request.ForcedCustomXenotype != null ||
                request.ForceBaselinerChance > 0f || !request.AllowedXenotypes.NullOrEmpty();
            if (explicitSelection || __result != XenotypeDefOf.Baseliner)
            {
                return;
            }

            // 隐式基准人是原版空池回退 鼠族不能用智人异种补位
            List<XenotypeChance> pool = RHAH_XenotypeResolver.GenerationPool(request.KindDef);
            XenotypeDef replacement = RHAH_XenotypeResolver.ChooseAllowed(pool, request.KindDef.race, Rand.Value,
                (xenotype, race) => RHAH_HarXenotypeBridge.Allows(xenotype, race) &&
                    (!request.MustBeCapableOfViolence || xenotype.canGenerateAsCombatant));
            if (replacement == null)
            {
                Log.ErrorOnce("[RHAH] No permitted xenotype is available for " + request.KindDef.defName + ".",
                    104831703);
                return;
            }

            __result = replacement;
        }
    }

    [HarmonyPatch(typeof(FactionDef), nameof(FactionDef.Description), MethodType.Getter)]
    internal static class RHAH_FactionXenotypeDescriptionPatch
    {
        static void Prefix(FactionDef __instance, ref string ___cachedDescription)
        {
            RHAH_XenotypeGeneration.RefreshFaction(__instance, ref ___cachedDescription);
        }
    }

    [HarmonyPatch(typeof(FactionDef), nameof(FactionDef.BaselinerChance), MethodType.Getter)]
    internal static class RHAH_FactionBaselinerChancePatch
    {
        static bool Prefix(FactionDef __instance, ref float __result)
        {
            if (!ModsConfig.BiotechActive || !RHAH_XenotypeGeneration.Owns(__instance))
            {
                return true;
            }
            __result = 0f;
            XenotypeSet pool = __instance.xenotypeSet;
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].xenotype == XenotypeDefOf.Baseliner)
                    {
                        __result += pool[i].chance;
                    }
                }
            }
            return false;
        }
    }
}
