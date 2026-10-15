using System;

namespace MouseDisaster
{
    public enum MouseDisasterBabyLeashMode
    {
        None = 0,
        RatkinMother = 1,
        GenericMouseEgg = 2
    }

    public static class MouseDisasterBabyLeashPolicy
    {
        private const string CurrentLeadYourPetPackageId = "lezhizhong.leadyourpet";
        private const string LegacyLeadYourPetPackageId = "codex.leadyourpet";

        public static bool IsKnownLeadYourPetPackageId(string packageId)
        {
            return !string.IsNullOrWhiteSpace(packageId) &&
                   (packageId.Equals("nanaloveyuki.leadyourpet.continued", StringComparison.OrdinalIgnoreCase) ||
                    packageId.Equals(CurrentLeadYourPetPackageId, StringComparison.OrdinalIgnoreCase) ||
                    packageId.Equals(LegacyLeadYourPetPackageId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool ShouldTryRelatedAdultBabyLeash(bool leadYourPetEnabled, bool adultIsAdult, bool hasAdultSocialRelation)
        {
            return leadYourPetEnabled && adultIsAdult && hasAdultSocialRelation;
        }

        public static MouseDisasterBabyLeashMode ResolveRelatedAdultLeashMode(bool hasParentRelation)
        {
            return hasParentRelation
                ? MouseDisasterBabyLeashMode.RatkinMother
                : MouseDisasterBabyLeashMode.GenericMouseEgg;
        }
    }
}
