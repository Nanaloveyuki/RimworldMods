using System.Collections.Generic;
using HarmonyLib;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Identity
{
    internal static class RHAH_NegativeHealthEffects
    {
        static readonly Dictionary<HediffStage, HediffStage> stages = new Dictionary<HediffStage, HediffStage>();
        static float cachedFactor = 1f;

        internal static void RefreshPawns()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            List<Map> maps = Current.Game?.Maps;
            for (int i = 0; maps != null && i < maps.Count; i++)
            {
                IReadOnlyList<Verse.Pawn> pawns = maps[i].mapPawns.AllPawnsSpawned;
                for (int j = 0; j < pawns.Count; j++) pawns[j].health.hediffSet.DirtyCache();
            }
        }

        internal static HediffStage ScaleStage(HediffStage stage, float factor)
        {
            if (stage == null || factor == 1f) return stage;
            if (cachedFactor != factor)
            {
                stages.Clear();
                cachedFactor = factor;
            }
            if (stages.TryGetValue(stage, out HediffStage scaled)) return scaled;
            scaled = new HediffStage
            {
                minSeverity = stage.minSeverity,
                label = stage.label,
                untranslatedLabel = stage.untranslatedLabel,
                lifeThreatening = stage.lifeThreatening,
                painOffset = stage.painOffset * factor,
                painFactor = stage.painFactor,
                hungerRateFactor = stage.hungerRateFactor,
                statFactors = new List<StatModifier>(),
                capMods = new List<PawnCapacityModifier>()
            };
            for (int i = 0; stage.statFactors != null && i < stage.statFactors.Count; i++)
            {
                StatModifier modifier = stage.statFactors[i];
                scaled.statFactors.Add(new StatModifier
                {
                    stat = modifier.stat,
                    value = modifier.value < 1f ? 1f - (1f - modifier.value) * factor : modifier.value
                });
            }
            for (int i = 0; i < stage.capMods.Count; i++)
            {
                PawnCapacityModifier modifier = stage.capMods[i];
                scaled.capMods.Add(new PawnCapacityModifier
                {
                    capacity = modifier.capacity,
                    offset = modifier.offset < 0f ? modifier.offset * factor : modifier.offset,
                    postFactor = modifier.postFactor < 1f ? 1f - (1f - modifier.postFactor) * factor : modifier.postFactor,
                    setMax = modifier.setMax < 1f ? 1f - (1f - modifier.setMax) * factor : modifier.setMax,
                    statFactorMod = modifier.statFactorMod,
                    setMaxCurveOverride = modifier.setMaxCurveOverride,
                    setMaxCurveEvaluateStat = modifier.setMaxCurveEvaluateStat
                });
            }
            stages.Add(stage, scaled);
            return scaled;
        }
    }

    [HarmonyPatch(typeof(Hediff), nameof(Hediff.CurStage), MethodType.Getter)]
    internal static class RHAH_NegativeHealthPatch
    {
        static void Postfix(Hediff __instance, ref HediffStage __result)
        {
            switch (__instance.def?.defName)
            {
                case "RHAH_Plague":
                case "RHAH_GnawedBark":
                case "RHAH_GnawedWall":
                case "RHAH_OvergnawedWall":
                case "RHAH_RefeedingSyndrome":
                    __result = RHAH_NegativeHealthEffects.ScaleStage(__result, RHAH_Mod.Settings?.negativeEffectFactor ?? 1f);
                    break;
            }
        }
    }
}
