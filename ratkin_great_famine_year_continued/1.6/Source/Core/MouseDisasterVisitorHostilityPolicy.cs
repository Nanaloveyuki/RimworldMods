namespace MouseDisaster
{
    public enum MouseDisasterVisitorHostilityLordKind
    {
        AssaultColony = 0,
        TravelAndExit = 1
    }

    public static class MouseDisasterVisitorHostilityPolicy
    {
        public static bool ShouldForceHiddenFactionHostility(bool explicitDriveAway, bool hiddenFaction)
        {
            return explicitDriveAway && hiddenFaction;
        }

        public static MouseDisasterVisitorHostilityLordKind ResolveLordKind(bool explicitDriveAway, double randomValue)
        {
            if (!explicitDriveAway)
            {
                return MouseDisasterVisitorHostilityLordKind.AssaultColony;
            }

            return randomValue < 0.5d
                ? MouseDisasterVisitorHostilityLordKind.TravelAndExit
                : MouseDisasterVisitorHostilityLordKind.AssaultColony;
        }

        public static bool ShouldKeepRequestedRelationKind(bool hiddenFaction, bool explicitDriveAway)
        {
            return !hiddenFaction || explicitDriveAway;
        }

        public static bool ShouldSkipHiddenFactionNeutralMaintenance(bool explicitHostilityActive)
        {
            return explicitHostilityActive;
        }

        public static bool ShouldDeferNeutralReset(bool hiddenFaction, bool explicitHostilityActive)
        {
            return hiddenFaction && explicitHostilityActive;
        }
    }
}
