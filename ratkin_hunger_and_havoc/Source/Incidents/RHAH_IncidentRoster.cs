using Verse;
using HungerAndHavoc.Api;
using RimWorld;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_IncidentRoster
    {
        internal static RHAH_PawnRole RoleAt(string displayId, RHAH_PawnRole fallback, int index)
        {
            switch (displayId)
            {
                case "I-002":
                case "I-037":
                    return RHAH_PawnRole.BeggarChild;
                case "I-003":
                    return index == 0 ? RHAH_PawnRole.Mother : RHAH_PawnRole.RatkinYoung;
                case "I-004":
                    return index == 0 ? RHAH_PawnRole.BeggarMother : RHAH_PawnRole.BeggarChild;
                case "I-007":
                case "I-043":
                    return RHAH_PawnRole.ThiefChild;
                case "I-009":
                case "I-009-01":
                case "I-041":
                    return RHAH_PawnRole.WildChild;
                case "I-013":
                case "I-032":
                case "I-033":
                case "I-046":
                case "I-047":
                    return RHAH_PawnRole.RatkinYoung;
                case "I-029":
                case "I-044":
                    return RHAH_PawnRole.BeggarMother;
                case "I-012":
                case "I-038":
                    return index == 0 ? RHAH_PawnRole.Trader : RHAH_PawnRole.RatkinYoung;
                default:
                    return fallback;
            }
        }

        internal static Gender? GenderAt(string displayId, int index)
        {
            if (displayId == "I-029" || displayId == "I-044")
            {
                return Gender.Female;
            }

            if ((displayId == "I-003" || displayId == "I-004") && index == 0)
            {
                return Gender.Female;
            }

            return null;
        }

        internal static bool StartsLabor(string displayId)
        {
            return displayId == "I-029" || displayId == "I-044";
        }

        internal static bool Shatters(string displayId, int index)
        {
            return displayId == "I-003" && index == 0;
        }
    }
}
