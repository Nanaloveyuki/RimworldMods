using HungerAndHavoc.Incidents;

namespace HungerAndHavoc.Core
{
    internal static class RHAH_ContentPreset
    {
        internal static void Apply(RHAH_Settings settings, RHAH_ContentRating rating)
        {
            settings.contentPreset = (int)rating;
            settings.contentPreset132Chosen = true;
            bool gentle = rating >= RHAH_ContentRating.General;
            bool allAges = rating == RHAH_ContentRating.AllAges;
            settings.plagueProgressionFactor = allAges ? 0.25f : gentle ? 0.75f : 1f;
            settings.negativeEffectFactor = settings.plagueProgressionFactor;
            settings.foodRequestFactor = gentle ? 0.5f : 1f;
            settings.peacefulRefusals = gentle;
            settings.plagueSafeMode = allAges;
            settings.plagueContagious = !allAges;
            settings.SetAllowVisitorDoors(false);
            settings.refugeeCampEnabled = !gentle;
            for (int i = 0; i < RHAH_IncidentGroups.Count; i++)
            {
                RHAH_IncidentEntry entry = RHAH_IncidentGroups.Scenarios(i)[0];
                settings.SetIncidentEnabled(entry.DisplayId, Allows(rating, entry.DisplayId));
            }
            Identity.RHAH_NegativeHealthEffects.RefreshPawns();
        }

        internal static bool Allows(RHAH_ContentRating preset, string displayId)
        {
            return RHAH_IncidentRating.For(displayId) >= preset ||
                preset == RHAH_ContentRating.AllAges && Pawn.RHAH_ArmedDemand.IsSiege(displayId);
        }

        internal static bool PeacefulRefusal(string displayId)
        {
            return RHAH_Mod.Settings?.peacefulRefusals == true && !Pawn.RHAH_ArmedDemand.IsSiege(displayId);
        }

        internal static float FoodFactor => RHAH_Mod.Settings?.foodRequestFactor ?? 1f;

        internal static float NegativeEffect(float value)
        {
            return value < 0f ? value * (RHAH_Mod.Settings?.negativeEffectFactor ?? 1f) : value;
        }
    }
}
