using System;

namespace MouseDisaster
{
    public static class MouseDisasterBirthPolicy
    {
        private const string LocalSubtypeXenotypePrefix = "MouseDisasterSubtype_";

        private static readonly string[] RatkinXenotypeKeywords =
        {
            "ratkin",
            "鼠族",
            "鼠系",
            "仓鼠",
            "鼹",
            "白鼠",
            "沙鼠",
            "鼩",
            "实验体"
        };

        public static bool ShouldForceRatkinBirthXenotype(bool motherIsRatkin, bool newbornIsRatkin, bool newbornAlreadyRatkinXenotype)
        {
            return motherIsRatkin && newbornIsRatkin && !newbornAlreadyRatkinXenotype;
        }

        public static bool ShouldForceMouseDisasterBirthXenotype(
            bool shouldUseMouseDisasterBirthIdentity,
            bool motherIsRatkin,
            bool newbornIsRatkin,
            bool newbornAlreadyRatkinXenotype)
        {
            return shouldUseMouseDisasterBirthIdentity &&
                   ShouldForceRatkinBirthXenotype(motherIsRatkin, newbornIsRatkin, newbornAlreadyRatkinXenotype);
        }

        public static bool IsRatkinLikeXenotype(string defName, string label)
        {
            if (string.IsNullOrWhiteSpace(defName) && string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            if (string.Equals(defName, "Baseliner", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(defName) &&
                (defName.StartsWith(LocalSubtypeXenotypePrefix, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(defName, "Ratkin", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            string source = ((defName ?? string.Empty) + " " + (label ?? string.Empty)).ToLowerInvariant();
            for (int i = 0; i < RatkinXenotypeKeywords.Length; i++)
            {
                if (source.Contains(RatkinXenotypeKeywords[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
