namespace MouseDisaster
{
    public enum MouseDisasterGeneRestoreDecision
    {
        None = 0,
        RestoreMissingGenes = 1,
        SuppressRestore = 2
    }

    public static class MouseDisasterGeneRestorePolicy
    {
        public static MouseDisasterGeneRestoreDecision DecideMissingGeneRestore(bool isIncidentVisitor, bool hasMouseDisasterGenes, bool hasManualRemovalMarker)
        {
            if (!isIncidentVisitor || hasMouseDisasterGenes)
            {
                return MouseDisasterGeneRestoreDecision.None;
            }

            if (hasManualRemovalMarker)
            {
                return MouseDisasterGeneRestoreDecision.SuppressRestore;
            }

            return MouseDisasterGeneRestoreDecision.RestoreMissingGenes;
        }

        public static bool ShouldTrackManualRemovalOnGeneRemoved(bool isManagedPawn, bool removedMouseDisasterGene)
        {
            return isManagedPawn && removedMouseDisasterGene;
        }

        public static bool ShouldClearManualRemovalOnGeneAdded(bool hasManualRemovalMarker, bool isManagedPawn, bool addedMouseDisasterGene)
        {
            return hasManualRemovalMarker && isManagedPawn && addedMouseDisasterGene;
        }

        public static bool ShouldInheritManualRemovalToNewborn(bool parentHasManualRemovalMarker)
        {
            return false;
        }

        public static bool ShouldPruneExpiredRecord(int nowTick, int recordTick, int windowTicks)
        {
            return nowTick - recordTick > windowTicks;
        }

        public static bool ShouldApplyRimTalkToddlerCompatibility(bool isMouseDisasterPawn, bool isIncidentVisitor, bool isPlayerAffiliatedRatkin)
        {
            return isMouseDisasterPawn || isIncidentVisitor || isPlayerAffiliatedRatkin;
        }

        public static bool ShouldPreserveTraderKindForIncidentVisitor(bool isIncidentVisitor, bool isTraderAdult)
        {
            return isIncidentVisitor && isTraderAdult;
        }

        public static int ResolveTraderCaravanChildrenToLinkCount(int childCount)
        {
            return childCount;
        }

        public static bool ShouldSkipVisitorNormalizationWhileTurningHostile(bool isIncidentVisitor, bool factionHostileToPlayer)
        {
            return isIncidentVisitor && factionHostileToPlayer;
        }

        public static bool ShouldRunIdentityRestrictionScan(bool explicitRefreshRequested)
        {
            return explicitRefreshRequested;
        }
    }
}
