using RimWorld;

using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Generation
{
    public class RHAH_GenerationExtension : DefModExtension
    {
        // 保留旧 XML 字段 不参与默认衣物准入
        public bool allowRefugeeApparel;
    }

    internal static class RHAH_ApparelPolicy
    {
        internal const float AwfulShare = 0.35f;
        internal const float PoorShare = 0.50f;
        internal const float MinDurability = 0.10f;
        internal const float MaxDurability = 0.60f;
        internal const float CorpseChance = 0.25f;
        internal const float ClothWeight = 0.65f;
        internal const float LeatherWeight = 0.35f;
        internal const int MaxPieces = 4;

        const string CorePackageId = "ludeon.rimworld";
        const string RatkinPackageId = "solaris.ratkinracemod";
        const string TribalDefName = "Apparel_TribalA";
        const string ClothDefName = "Cloth";
        const string LeatherDefName = "Humanleather";

        static readonly string[] YoungNames =
        {
            "Apparel_BabyOnesie",
            "Apparel_WarmerHat",
            "Apparel_SunHat",
            "Apparel_KidTribal"
        };

        static readonly string[] BabyNames =
        {
            "Apparel_BabyOnesie",
            "Apparel_WarmerHat",
            "Apparel_SunHat"
        };

        static readonly string[] ChildNames =
        {
            "Apparel_WarmerHat",
            "Apparel_SunHat",
            "Apparel_KidTribal"
        };

        // NewRatkinPlus 1.6 原始指定清单 不读取第三方扩充后的 HAR 可穿池
        static readonly HashSet<string> RatkinNames = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "RK_Apparel_Banner", "RK_Apparel_SpaceArmor", "RK_Apparel_SpaceArmorHelmet",
            "RK_Apparel_Vacsuit", "RK_Apparel_VacsuitChildren", "RK_Apparel_VacsuitHelmet",
            "RK_ApronSkirt", "RK_ApronSkirtChildren", "RK_Backpack", "RK_BattleSuit",
            "RK_BulletProofHelmet", "RK_Cardigan", "RK_Apparel_ChildrenCardigan",
            "RK_ChefHat", "RK_ChefSuit", "RK_Coif", "RK_CrossBack", "RK_ExplorerHat",
            "RK_ExplorerWear", "RK_FlatColorCoat", "RK_FrillOnepiece", "RK_GaurdenUniform",
            "RK_HairCorsage", "RK_HeadBand", "RK_HeavyShield", "RK_Mask", "RK_MaskB",
            "RK_Muffler", "RK_OrderUniform", "RK_OutdoorBackpack", "RK_Plate",
            "RK_PlateHelmA", "RK_PlateHelmB", "RK_PlateHelmC", "RK_ResearchGlasses",
            "RK_ResearchGown", "RK_RibbonHairBand", "RK_RoyalCrown", "RK_RoyalRobe",
            "RK_Sack", "RK_SantaHat", "RK_SantaRobe", "RK_SantaSack", "RK_SistersDerss",
            "RK_SistersVeil", "RK_StrawHat", "RK_SummerDress", "RK_Apparel_ChildrenSummerDress",
            "RK_TowerShield", "RK_WhiteCoat", "RK_WinterRobe", "RK_Apparel_ChildrenWinterRobe",
            "RK_WoodenShield", "RK_WoolenHat", "RK_WorkerWear"
        };

        internal static bool AllowsApparel(string defName, bool isApparel, bool madeFromStuff, bool countsAsClothing, string packageId, bool official, int techLevel, bool enabled)
        {
            if (!enabled)
            {
                return false;
            }

            if (string.IsNullOrEmpty(defName) || !isApparel || !madeFromStuff || !countsAsClothing)
            {
                return false;
            }

            if (techLevel > (int)TechLevel.Medieval)
            {
                return false;
            }

            if (IsYoung(defName))
            {
                return IsOfficialSource(packageId, official);
            }

            if (string.Equals(packageId, RatkinPackageId, System.StringComparison.OrdinalIgnoreCase) && RatkinNames.Contains(defName))
            {
                return true;
            }

            return defName == TribalDefName && IsOfficialSource(packageId, official);
        }

        static bool IsOfficialSource(string packageId, bool official)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return false;
            }

            return official && (string.Equals(packageId, CorePackageId, System.StringComparison.OrdinalIgnoreCase) ||
                packageId.StartsWith(CorePackageId + ".", System.StringComparison.OrdinalIgnoreCase));
        }
        internal static bool IsYoung(string defName)
        {
            return IndexOf(YoungNames, defName) >= 0;
        }

        internal static bool FitsStage(string defName, int stage)
        {
            if (stage == 0)
            {
                return IndexOf(BabyNames, defName) >= 0;
            }

            if (stage == 1)
            {
                return IndexOf(ChildNames, defName) >= 0;
            }

            return !IsYoung(defName);
        }

        internal static int Quality(float roll)
        {
            return Quality(roll, AwfulShare, PoorShare);
        }

        internal static int Quality(float roll, float awfulShare, float poorShare)
        {
            float safe = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
            float awful = awfulShare < 0f ? 0f : awfulShare > 1f ? 1f : awfulShare;
            float poor = poorShare < 0f ? 0f : poorShare;
            if (awful + poor > 1f)
            {
                poor = 1f - awful;
            }

            if (safe < awful)
            {
                return 0;
            }

            return safe < awful + poor ? 1 : 2;
        }

        internal static int HitPoints(int maxHitPoints, float fraction)
        {
            if (maxHitPoints <= 1)
            {
                return maxHitPoints;
            }

            float low = MinDurability;
            float high = MaxDurability;
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (settings != null)
            {
                low = settings.apparelMinDurabilityPercent / 100f;
                high = settings.apparelMaxDurabilityPercent / 100f;
            }

            float safe = fraction < low || float.IsNaN(fraction) ? low : fraction > high ? high : fraction;
            int points = (int)System.Math.Round(maxHitPoints * safe);
            if (points < 1)
            {
                return 1;
            }

            return points > maxHitPoints ? maxHitPoints : points;
        }

        internal static int PieceCount(int stage, float roll)
        {
            if (stage == 0)
            {
                return Weighted(roll, 0.30f, 0.50f, 0.18f, 0.02f);
            }

            if (stage == 1)
            {
                return Weighted(roll, 0.16f, 0.34f, 0.30f, 0.16f, 0.04f);
            }

            int adult = Weighted(roll, 0.02f, 0.16f, 0.34f, 0.30f, 0.18f);
            int cap = MaxPieces;
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (settings != null && settings.apparelMaxPieces > 0)
            {
                cap = settings.apparelMaxPieces;
            }

            return adult < 1 ? 1 : adult > cap ? cap : adult;
        }

        internal static string PreferredStuff(IList<string> allowed, float roll)
        {
            bool cloth = false;
            bool leather = false;
            if (allowed != null)
            {
                for (int i = 0; i < allowed.Count; i++)
                {
                    cloth |= allowed[i] == ClothDefName;
                    leather |= allowed[i] == LeatherDefName;
                }
            }

            if (cloth && leather)
            {
                float safe = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
                float clothShare = ClothWeight;
                Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
                if (settings != null)
                {
                    clothShare = settings.apparelClothPercent / 100f;
                }

                return safe < clothShare ? ClothDefName : LeatherDefName;
            }

            if (cloth)
            {
                return ClothDefName;
            }

            return leather ? LeatherDefName : null;
        }

        static int Weighted(float roll, params float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i] < 0f ? 0f : weights[i];
            }

            if (total <= 0f)
            {
                return 0;
            }

            float safe = roll < 0f || float.IsNaN(roll) ? 0f : roll > 1f ? 1f : roll;
            float cursor = safe * total;
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i] < 0f ? 0f : weights[i];
                if (cursor <= sum)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        static int IndexOf(string[] names, string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return -1;
            }

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == defName)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
