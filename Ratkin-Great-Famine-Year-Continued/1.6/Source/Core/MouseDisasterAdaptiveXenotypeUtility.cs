using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    internal static class MouseDisasterAdaptiveXenotypeUtility
    {
        private const string MouseDisasterXenotypePrefix = "MouseDisasterSubtype_";
        private const string VirtualDefaultRatkinXenotypeDefName = "MouseDisasterSubtype_ratkin";

        public static List<XenotypeDef> GetCandidates()
        {
            return DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(IsCandidate)
                .OrderByDescending(IsMouseDisasterXenotype)
                .ThenBy(def => def.defName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<XenotypeDef> GetEnabledCandidates()
        {
            return GetCandidates()
                .Where(def => GetSpawnWeight(def) > 0f)
                .ToList();
        }

        public static bool IsCandidate(XenotypeDef xenotype)
        {
            return xenotype != null &&
                   !IsVirtualDefaultRatkinXenotype(xenotype) &&
                   MouseDisasterBirthPolicy.IsRatkinLikeXenotype(xenotype.defName, xenotype.label);
        }

        public static bool IsMouseDisasterXenotype(XenotypeDef xenotype)
        {
            return xenotype != null &&
                   !string.IsNullOrEmpty(xenotype.defName) &&
                   xenotype.defName.StartsWith(MouseDisasterXenotypePrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsVirtualDefaultRatkinXenotype(XenotypeDef xenotype)
        {
            return xenotype != null &&
                   string.Equals(xenotype.defName, VirtualDefaultRatkinXenotypeDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static float GetDefaultSpawnWeight(XenotypeDef xenotype)
        {
            if (IsOriginalRatkinXenotype(xenotype))
            {
                return MouseDisasterSettings.DefaultRatkinXenotypeSpawnWeight;
            }

            return MouseDisasterSettings.DefaultExternalRatkinXenotypeSpawnWeight;
        }

        public static bool IsOriginalRatkinXenotype(XenotypeDef xenotype)
        {
            return xenotype != null &&
                   (string.Equals(xenotype.defName, "Ratkin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(xenotype.defName, "RK_XenoType_Ratkin", StringComparison.OrdinalIgnoreCase));
        }

        public static float GetSpawnWeight(XenotypeDef xenotype)
        {
            if (xenotype == null)
            {
                return 0f;
            }

            float fallback = GetDefaultSpawnWeight(xenotype);
            return MouseDisasterMod.Settings?.GetRatkinXenotypeSpawnWeight(xenotype.defName, fallback) ?? fallback;
        }

        public static XenotypeDef Choose(ISet<string> excludedDefNames = null)
        {
            List<XenotypeDef> candidates = GetCandidates();
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                XenotypeDef candidate = candidates[i];
                XenotypeDef resolvedCandidate = ResolveGeneratedXenotype(candidate);
                if (resolvedCandidate == null || IsExcluded(candidate, resolvedCandidate, excludedDefNames))
                {
                    continue;
                }

                totalWeight += GetSpawnWeight(candidate);
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = Rand.Range(0f, totalWeight);
            XenotypeDef lastCandidate = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                XenotypeDef candidate = candidates[i];
                XenotypeDef resolvedCandidate = ResolveGeneratedXenotype(candidate);
                if (resolvedCandidate == null || IsExcluded(candidate, resolvedCandidate, excludedDefNames))
                {
                    continue;
                }

                float weight = GetSpawnWeight(candidate);
                if (weight <= 0f)
                {
                    continue;
                }

                lastCandidate = candidate;
                roll -= weight;
                if (roll <= 0f)
                {
                    return resolvedCandidate;
                }
            }

            return ResolveGeneratedXenotype(lastCandidate);
        }

        private static bool IsExcluded(
            XenotypeDef candidate,
            XenotypeDef resolvedCandidate,
            ISet<string> excludedDefNames)
        {
            if (excludedDefNames == null)
            {
                return false;
            }

            if (candidate != null && excludedDefNames.Contains(candidate.defName))
            {
                return true;
            }

            return resolvedCandidate != null && excludedDefNames.Contains(resolvedCandidate.defName);
        }

        private static XenotypeDef ResolveGeneratedXenotype(XenotypeDef candidate)
        {
            if (!IsVirtualDefaultRatkinXenotype(candidate))
            {
                return candidate;
            }

            return MouseDisasterUtility.ResolveRatkinBaseXenotype();
        }
    }
}
