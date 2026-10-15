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
        internal const float MinimumGeneratedComfortTemperature = MouseDisasterSettings.DefaultMouseDisasterMinimumEnvironmentTemperature;
        internal const float MaximumGeneratedComfortTemperature = MouseDisasterSettings.DefaultMouseDisasterMaximumEnvironmentTemperature;

        internal static readonly TemperatureApparelOption[] ColdTemperatureApparelOptions =
        {
            new TemperatureApparelOption("MouseDisaster_Cold_ThinHempLayer", 8f, true),
            new TemperatureApparelOption("MouseDisaster_Cold_LayeredHempClothes", 12f, true),
            new TemperatureApparelOption("MouseDisaster_Cold_StrawBarkQuilt", 20f, true),
            new TemperatureApparelOption("MouseDisaster_Cold_PatchedFurCloak", 28f, true),
            new TemperatureApparelOption("MouseDisaster_Cold_SmokeStiffenedBlanket", 40f, true),
            new TemperatureApparelOption("MouseDisaster_Cold_ThickHideHempWrap", 56f, true)
        };

        internal static readonly TemperatureApparelOption[] HeatTemperatureApparelOptions =
        {
            new TemperatureApparelOption("MouseDisaster_Heat_StaleWetCloth", 8f, false),
            new TemperatureApparelOption("MouseDisaster_Heat_DryMudCoating", 12f, false),
            new TemperatureApparelOption("MouseDisaster_Heat_ReedShadeWrap", 20f, false),
            new TemperatureApparelOption("MouseDisaster_Heat_SoakedBarkWrap", 28f, false),
            new TemperatureApparelOption("MouseDisaster_Heat_MudReedMantle", 36f, false),
            new TemperatureApparelOption("MouseDisaster_Heat_HeavyCoolingMud", 44f, false)
        };

        internal static readonly TemperatureApparelOption[] AllTemperatureApparelOptions =
            ColdTemperatureApparelOptions.Concat(HeatTemperatureApparelOptions).ToArray();

        internal sealed class TemperatureApparelOption
        {
            public readonly string DefName;
            public readonly float Insulation;
            public readonly bool IsCold;

            public TemperatureApparelOption(string defName, float insulation, bool isCold)
            {
                DefName = defName;
                Insulation = insulation;
                IsCold = isCold;
            }
        }

        internal static void ApplyTemperatureProtectionApparel(Pawn pawn, Map map, IntVec3 cell)
        {
            // 该辅助服饰只允许在 Pawn.SpawnSetup 的 Prefix 中加入。
            // 已入图 Pawn 的 Wear 会使 PawnRenderer 缓存失效，可能与 DynamicDrawManager 的绘制迭代交错。
            if (pawn == null || pawn.Spawned || map?.mapTemperature == null || pawn.Dead || pawn.apparel == null)
            {
                return;
            }

            MouseDisasterSettings settings = MouseDisasterMod.Settings;
            if (settings?.enableTemperatureProtectionApparel == false)
            {
                MouseDisasterTrace.Log("temperature apparel skipped; " + MouseDisasterTrace.DescribePawn(pawn) +
                    "; reason=feature-disabled");
                return;
            }

            // Keep generated protection apparel across map transfers and normal respawns.
            if (HasTemperatureProtectionApparel(pawn))
            {
                MouseDisasterTrace.Log("temperature apparel skipped; " + MouseDisasterTrace.DescribePawn(pawn) +
                    "; reason=existing-temperature-apparel");
                return;
            }

            float environmentTemperature = 0f;
            bool usedCellTemperature = false;
            if (cell.InBounds(map))
            {
                usedCellTemperature = GenTemperature.TryGetTemperatureForCell(
                    cell, map, out environmentTemperature) &&
                    !float.IsNaN(environmentTemperature) &&
                    !float.IsInfinity(environmentTemperature);
            }

            if (!usedCellTemperature)
            {
                environmentTemperature = map.mapTemperature.OutdoorTemp;
            }

            if (float.IsNaN(environmentTemperature) || float.IsInfinity(environmentTemperature))
            {
                MouseDisasterTrace.Log("temperature apparel skipped; " + MouseDisasterTrace.DescribePawn(pawn) +
                    "; " + MouseDisasterTrace.DescribeMap(map) + "; reason=invalid-environment-temperature");
                return;
            }

            FloatRange currentRange = pawn.ComfortableTemperatureRange();
            float targetTemperature = Mathf.Clamp(
                environmentTemperature,
                settings?.mouseDisasterMinimumEnvironmentTemperature ?? MinimumGeneratedComfortTemperature,
                settings?.mouseDisasterMaximumEnvironmentTemperature ?? MaximumGeneratedComfortTemperature);
            bool needsColdProtection = environmentTemperature < currentRange.min;
            bool needsHeatProtection = environmentTemperature > currentRange.max;
            float requiredInsulation = needsColdProtection
                ? currentRange.min - targetTemperature
                : needsHeatProtection ? targetTemperature - currentRange.max : 0f;
            TemperatureApparelOption selected = null;
            if (needsColdProtection)
            {
                selected = FindTemperatureApparelOption(
                    ColdTemperatureApparelOptions,
                    requiredInsulation);
            }
            else if (needsHeatProtection)
            {
                selected = FindTemperatureApparelOption(
                    HeatTemperatureApparelOptions,
                    requiredInsulation);
            }

            if (selected == null)
            {
                MouseDisasterTrace.Log("temperature apparel decision; " + MouseDisasterTrace.DescribePawn(pawn) +
                    "; " + MouseDisasterTrace.DescribeMap(map) + "; comfort=" + currentRange.min.ToString("0.0") +
                    ".." + currentRange.max.ToString("0.0") + "; target=" + targetTemperature.ToString("0.0") +
                    "; required=" + requiredInsulation.ToString("0.0") + "; temperatureSource=" +
                    (usedCellTemperature ? "cell" : "outdoor") + "; selected=none");
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(selected.DefName);
            bool equipped = TryWearTemperatureApparel(pawn, apparelDef);
            float configuredInsulation = settings?.GetTemperatureApparelInsulation(
                selected.DefName, selected.Insulation) ?? selected.Insulation;
            MouseDisasterTrace.Log("temperature apparel decision; " + MouseDisasterTrace.DescribePawn(pawn) +
                "; " + MouseDisasterTrace.DescribeMap(map) + "; comfort=" + currentRange.min.ToString("0.0") +
                ".." + currentRange.max.ToString("0.0") + "; target=" + targetTemperature.ToString("0.0") +
                "; required=" + requiredInsulation.ToString("0.0") + "; selected=" + selected.DefName +
                "; insulation=" + configuredInsulation.ToString("0.0") + "; equipped=" + equipped +
                "; temperatureSource=" + (usedCellTemperature ? "cell" : "outdoor"));
        }

        private static TemperatureApparelOption FindTemperatureApparelOption(
            TemperatureApparelOption[] options,
            float requiredInsulation)
        {
            TemperatureApparelOption fallback = null;
            for (int i = 0; i < options.Length; i++)
            {
                TemperatureApparelOption option = options[i];
                if (MouseDisasterMod.Settings != null &&
                    !MouseDisasterMod.Settings.IsTemperatureApparelEnabled(option.DefName))
                {
                    continue;
                }

                ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(option.DefName);
                if (apparelDef == null || !apparelDef.IsApparel)
                {
                    continue;
                }

                fallback = option;
                float insulation = MouseDisasterMod.Settings?.GetTemperatureApparelInsulation(
                    option.DefName, option.Insulation) ?? option.Insulation;
                if (insulation >= requiredInsulation - 0.001f)
                {
                    return option;
                }
            }

            return fallback;
        }

        private static bool HasTemperatureProtectionApparel(Pawn pawn)
        {
            return pawn?.apparel?.WornApparel.Any(worn =>
                IsTemperatureProtectionApparel(worn?.def)) == true;
        }

        private static bool IsTemperatureProtectionApparel(ThingDef apparelDef)
        {
            if (apparelDef == null)
            {
                return false;
            }

            return ColdTemperatureApparelOptions.Any(option => option.DefName == apparelDef.defName) ||
                   HeatTemperatureApparelOptions.Any(option => option.DefName == apparelDef.defName);
        }

        private static bool TryWearTemperatureApparel(Pawn pawn, ThingDef apparelDef)
        {
            if (pawn?.apparel == null || apparelDef == null || !apparelDef.IsApparel)
            {
                return false;
            }

            Apparel apparel = null;
            try
            {
                apparel = ThingMaker.MakeThing(apparelDef) as Apparel;
                if (apparel == null ||
                    !apparel.PawnCanWear(pawn, ignoreGender: true) ||
                    !ApparelUtility.HasPartsToWear(pawn, apparel.def) ||
                    !pawn.apparel.CanWearWithoutDroppingAnything(apparel.def))
                {
                    apparel?.Destroy();
                    return false;
                }

                pawn.apparel.Wear(apparel, dropReplacedApparel: false);
                return pawn.apparel.WornApparel.Contains(apparel);
            }
            catch (Exception exception)
            {
                // Wear 可能在加入 wornApparel 后、通知链完成前抛错；先清掉半成品，避免留下无效穿戴项。
                if (apparel != null && pawn.apparel.WornApparel.Contains(apparel))
                {
                    pawn.apparel.Remove(apparel);
                }
                apparel?.Destroy();
                Log.Warning("[MouseDisaster] Could not equip temperature protection apparel on " + pawn + ": " + exception);
                return false;
            }
        }

        private static void AssignDisasterApparel(Pawn pawn, DevelopmentalStage stage)
        {
            if (pawn?.apparel == null)
            {
                return;
            }

            if (stage.Adult())
            {
                EnsureAdultDisasterApparel(pawn);
                return;
            }

            for (int index = pawn.apparel.WornApparel.Count - 1; index >= 0; index--)
            {
                Apparel current = pawn.apparel.WornApparel[index];
                pawn.apparel.Remove(current);
                current.Destroy();
            }

            if (stage == DevelopmentalStage.Baby && TryAssignSeasonalBabyDisasterApparel(pawn))
            {
                return;
            }

            List<ThingDef> apparelPool;
            if (stage == DevelopmentalStage.Baby)
            {
                apparelPool = GetRestrictedApparelDefs(BabyRestrictedApparelDefNames);
            }
            else
            {
                apparelPool = GetRestrictedApparelDefs(ChildRestrictedApparelDefNames);
            }

            if (apparelPool.Count == 0)
            {
                return;
            }

            int targetCount = Mathf.Min(GetDesiredApparelCount(stage), apparelPool.Count);
            if (targetCount <= 0)
            {
                return;
            }

            int equipped = 0;
            List<ThingDef> randomizedPool = apparelPool.InRandomOrder().ToList();
            for (int attempt = 0; attempt < randomizedPool.Count && equipped < targetCount; attempt++)
            {
                if (TryWearRandomizedApparel(pawn, randomizedPool[attempt]))
                {
                    equipped++;
                }
            }
        }

        private static bool TryAssignSeasonalBabyDisasterApparel(Pawn pawn)
        {
            if (pawn == null || pawn.DevelopmentalStage != DevelopmentalStage.Baby)
            {
                return false;
            }

            switch (ResolveCurrentSeasonForDisasterPawn(pawn))
            {
                case Season.Summer:
                case Season.PermanentSummer:
                    return TryWearSpecificDisasterApparel(pawn, KidTribalApparelDefName) |
                           TryWearSpecificDisasterApparel(pawn, SunHatApparelDefName);
                case Season.Winter:
                case Season.PermanentWinter:
                    return TryWearSpecificDisasterApparel(pawn, BabyOnesieApparelDefName) |
                           TryWearSpecificDisasterApparel(pawn, WarmerHatApparelDefName);
                case Season.Spring:
                case Season.Fall:
                default:
                    return TryWearSpecificDisasterApparel(pawn, BabyOnesieApparelDefName);
            }
        }

        private static Season ResolveCurrentSeasonForDisasterPawn(Pawn pawn)
        {
            Map map = pawn?.MapHeld ?? Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (map == null || !map.Tile.Valid)
            {
                return Season.Undefined;
            }

            return GenLocalDate.Season(map.Tile);
        }

        private static bool TryWearSpecificDisasterApparel(Pawn pawn, string apparelDefName)
        {
            if (pawn?.apparel == null || apparelDefName.NullOrEmpty())
            {
                return false;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(apparelDefName);
            if (apparelDef == null || !apparelDef.IsApparel || !apparelDef.MadeFromStuff)
            {
                return false;
            }

            return TryWearRandomizedApparel(pawn, apparelDef);
        }

        private static void EnsureAdultDisasterApparel(Pawn pawn)
        {
            if (pawn?.apparel == null)
            {
                return;
            }

            for (int i = pawn.apparel.WornApparel.Count - 1; i >= 0; i--)
            {
                Apparel worn = pawn.apparel.WornApparel[i];
                pawn.apparel.Remove(worn);
                worn.Destroy();
            }

            List<ThingDef> adultPool = GetAdultApparelDefs(pawn);
            if (adultPool.Count == 0)
            {
                return;
            }

            int targetCount = Mathf.Clamp(RollWeightedCount(0.02f, 0.16f, 0.34f, 0.30f, 0.18f), 1, 4);
            int equipped = 0;
            List<ThingDef> randomizedPool = adultPool.InRandomOrder().ToList();
            for (int i = 0; i < randomizedPool.Count && equipped < targetCount; i++)
            {
                if (TryWearRandomizedApparel(pawn, randomizedPool[i]))
                {
                    equipped++;
                }
            }
        }

        private static bool TryWearRandomizedApparel(Pawn pawn, ThingDef apparelDef)
        {
            if (pawn?.apparel == null || apparelDef == null)
            {
                return false;
            }

            ThingDef stuff = TryResolvePreferredApparelStuff(apparelDef);
            if (stuff == null)
            {
                return false;
            }

            Apparel apparel = ThingMaker.MakeThing(apparelDef, stuff) as Apparel;
            if (apparel == null ||
                !apparel.PawnCanWear(pawn, ignoreGender: true) ||
                !ApparelUtility.HasPartsToWear(pawn, apparel.def) ||
                !pawn.apparel.CanWearWithoutDroppingAnything(apparel.def))
            {
                apparel?.Destroy();
                return false;
            }

            ApplyRandomizedApparelQuality(apparel);
            apparel.WornByCorpse = Rand.Chance(0.25f);
            pawn.apparel.Wear(apparel, dropReplacedApparel: false);
            ApplyRandomizedApparelDurability(apparel);
            return true;
        }

        private static int GetDesiredApparelCount(DevelopmentalStage stage)
        {
            if (stage == DevelopmentalStage.Baby)
            {
                return RollWeightedCount(0.30f, 0.50f, 0.18f, 0.02f);
            }

            if (stage == DevelopmentalStage.Child)
            {
                return RollWeightedCount(0.16f, 0.34f, 0.30f, 0.16f, 0.04f);
            }

            return 0;
        }

        private static int RollWeightedCount(params float[] weights)
        {
            if (weights == null || weights.Length == 0)
            {
                return 0;
            }

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                total += Mathf.Max(0f, weights[i]);
            }

            if (total <= 0f)
            {
                return 0;
            }

            float roll = Rand.Value * total;
            float cumulative = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += Mathf.Max(0f, weights[i]);
                if (roll <= cumulative)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        private static void ResolvePreferredApparelStuffDefs()
        {
            if (apparelStuffResolved)
            {
                return;
            }

            apparelStuffResolved = true;
            clothStuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(ClothStuffDefName);
            humanleatherStuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(HumanleatherStuffDefName);
        }

        private static ThingDef TryResolvePreferredApparelStuff(ThingDef apparelDef)
        {
            if (apparelDef == null)
            {
                return null;
            }

            ResolvePreferredApparelStuffDefs();
            if (!ApparelPreferredStuffCache.TryGetValue(apparelDef, out List<ThingDef> options))
            {
                options = GenStuff.AllowedStuffsFor(apparelDef)
                    .Where(stuff => stuff == clothStuffDef || stuff == humanleatherStuffDef)
                    .Distinct()
                    .ToList();
                ApparelPreferredStuffCache[apparelDef] = options;
            }

            if (options == null || options.Count == 0)
            {
                return null;
            }

            return options.RandomElementByWeight(stuff => stuff == humanleatherStuffDef ? 0.35f : 0.65f);
        }

        private static List<ThingDef> GetRestrictedApparelDefs(IEnumerable<string> defNames)
        {
            if (defNames == null)
            {
                return new List<ThingDef>();
            }

            return defNames
                .Select(DefDatabase<ThingDef>.GetNamedSilentFail)
                .Where(def => def != null && def.IsApparel && def.MadeFromStuff && TryResolvePreferredApparelStuff(def) != null)
                .Distinct()
                .ToList();
        }

        private static List<ThingDef> GetAdultApparelDefs(Pawn pawn)
        {
            if (pawn?.kindDef == null)
            {
                return new List<ThingDef>();
            }

            if (!adultApparelDefsResolved)
            {
                adultApparelDefsResolved = true;
                HashSet<string> restricted = new HashSet<string>(BabyRestrictedApparelDefNames.Concat(ChildRestrictedApparelDefNames));
                adultApparelDefsCache = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(def =>
                        MouseDisasterGenerationPolicy.AllowsApparel(def) &&
                        def.IsApparel &&
                        def.MadeFromStuff &&
                        def.apparel != null &&
                        def.apparel.countsAsClothingForNudity &&
                        !restricted.Contains(def.defName) &&
                        TryResolvePreferredApparelStuff(def) != null)
                    .Distinct()
                    .ToList();
            }

            if (adultApparelDefsCache == null || adultApparelDefsCache.Count == 0)
            {
                return new List<ThingDef>();
            }

            return adultApparelDefsCache
                .Where(def => ApparelUtility.HasPartsToWear(pawn, def))
                .ToList();
        }

        private static void ApplyRandomizedApparelQuality(Apparel apparel)
        {
            CompQuality qualityComp = apparel?.TryGetComp<CompQuality>();
            if (qualityComp == null)
            {
                return;
            }

            float roll = Rand.Value;
            QualityCategory quality;
            if (roll < 0.35f)
            {
                quality = QualityCategory.Awful;
            }
            else if (roll < 0.85f)
            {
                quality = QualityCategory.Poor;
            }
            else
            {
                quality = QualityCategory.Normal;
            }
            qualityComp.SetQuality(quality, ArtGenerationContext.Outsider);
        }

        private static void ApplyRandomizedApparelDurability(Apparel apparel)
        {
            if (apparel == null || apparel.MaxHitPoints <= 1)
            {
                return;
            }

            int randomHitPoints = Mathf.RoundToInt(apparel.MaxHitPoints * Rand.Range(DisasterApparelMinDurability, DisasterApparelMaxDurability));
            apparel.HitPoints = Mathf.Clamp(randomHitPoints, 1, apparel.MaxHitPoints);
        }
    }
}
