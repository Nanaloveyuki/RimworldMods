using System;

namespace HungerAndHavoc.Incidents
{
    internal enum RHAH_ContentRating
    {
        Cruel = 0,
        Realistic = 1,
        General = 2,
        AllAges = 3
    }

    internal static class RHAH_IncidentRating
    {
        internal static string LabelKey(RHAH_ContentRating rating) => "RHAH_ContentRating_" + rating;

        internal static RHAH_ContentRating For(string displayId)
        {
            switch (displayId)
            {
                case "I-055":
                    return RHAH_ContentRating.Cruel;
                case "I-003":
                case "I-013":
                case "I-019":
                case "I-049":
                case "I-051":
                case "I-056":
                    return RHAH_ContentRating.Realistic;
                case "I-015":
                case "I-016":
                case "I-017":
                case "I-018":
                case "I-020":
                case "I-021":
                case "I-022":
                case "I-023":
                case "I-024":
                case "I-025":
                case "I-026":
                case "I-027":
                case "I-028":
                case "I-031":
                case "I-033":
                case "I-039":
                case "I-047":
                case "I-053":
                case "I-057":
                case "I-058":
                    return RHAH_ContentRating.AllAges;
                case "I-001":
                case "I-002":
                case "I-004":
                case "I-005":
                case "I-006":
                case "I-007":
                case "I-008":
                case "I-009":
                case "I-010":
                case "I-008-01":
                case "I-009-01":
                case "I-010-01":
                case "I-011":
                case "I-012":
                case "I-014":
                case "I-029":
                case "I-030":
                case "I-032":
                case "I-034":
                case "I-035":
                case "I-036":
                case "I-037":
                case "I-038":
                case "I-040":
                case "I-041":
                case "I-042":
                case "I-043":
                case "I-044":
                case "I-045":
                case "I-046":
                case "I-048":
                case "I-050":
                case "I-052":
                case "I-054":
                case "I-059":
                case "I-060":
                case "I-061":
                case "I-059-01":
                case "I-060-01":
                    return RHAH_ContentRating.General;
                default:
                    throw new ArgumentException("Incident has no content rating: " + displayId, nameof(displayId));
            }
        }
    }
}
