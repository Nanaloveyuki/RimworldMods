using System.Collections.Generic;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_XenotypeWeightTable
    {
        internal const float MinWeight = 0f;
        internal const float MaxWeight = 100f;
        internal const float DefaultBuiltinWeight = 100f;
        internal const float DefaultExternalWeight = 0f;

        internal static float Clamp(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return MinWeight;
            }

            if (value < MinWeight)
            {
                return MinWeight;
            }

            if (value > MaxWeight)
            {
                return MaxWeight;
            }

            return value;
        }

        internal static float StoredOrDefault(IReadOnlyDictionary<string, float> weights, string defName, float fallback)
        {
            if (string.IsNullOrEmpty(defName) || weights == null || !weights.TryGetValue(defName, out float stored))
            {
                return Clamp(fallback);
            }

            return Clamp(stored);
        }
    }
}
