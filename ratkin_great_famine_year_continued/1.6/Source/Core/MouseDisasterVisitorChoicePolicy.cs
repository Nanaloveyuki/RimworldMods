using System;
using System.Collections.Generic;

namespace MouseDisaster
{
    public static class MouseDisasterVisitorChoicePolicy
    {
        private static readonly HashSet<string> VisitorChoiceUpgradeIncidents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MouseDisaster_WildRatkinWandersIn",
            "MouseDisaster_WildRatkinChildWandersIn",
            "MouseDisaster_WildRatkinGroupWandersIn",
            "MouseDisaster_PlagueWanderers",
            "MouseDisaster_PlagueOrphan"
        };

        private static readonly HashSet<string> BatchEmploymentIncidents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MouseDisaster_BeggarFamily",
            "MouseDisaster_BeggarGroup",
            "MouseDisaster_ThiefRatkinGroup",
            "MouseDisaster_ThiefRatkinChildGroup",
            "MouseDisaster_BeggarSiege",
            "MouseDisaster_PlaguePassersby",
            "MouseDisaster_PlagueRefugees",
            "MouseDisaster_PlagueBeggarGroup",
            "MouseDisaster_PlagueThiefGroup",
            "MouseDisaster_LaboringRefugees",
            "MouseDisaster_PlagueLaboringRefugees",
            "MouseDisaster_StrongSiege",
            "MouseDisaster_PlagueStrongSiege",
            "MouseDisaster_Passersby"
        };

        public static bool ShouldUpgradeToVisitorChoiceControl(string incidentDefName)
        {
            return !string.IsNullOrEmpty(incidentDefName) &&
                   VisitorChoiceUpgradeIncidents.Contains(incidentDefName);
        }

        public static bool ShouldOfferBatchHire(string incidentDefName, int recruitableVisitorCount)
        {
            return recruitableVisitorCount > 0 && AllowsBatchEmployment(incidentDefName);
        }

        public static bool ShouldOfferBatchJoin(string incidentDefName, int activeVisitorCount)
        {
            return activeVisitorCount > 0 && AllowsBatchEmployment(incidentDefName);
        }

        public static bool ShouldOfferPrisonTransfer(string incidentDefName, int activeVisitorCount, bool prisonIntegrationEnabled)
        {
            return prisonIntegrationEnabled &&
                   activeVisitorCount > 0 &&
                   AllowsBatchEmployment(incidentDefName);
        }

        private static bool AllowsBatchEmployment(string incidentDefName)
        {
            return !string.IsNullOrEmpty(incidentDefName) &&
                   (VisitorChoiceUpgradeIncidents.Contains(incidentDefName) ||
                    BatchEmploymentIncidents.Contains(incidentDefName));
        }
    }
}
