using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Pawn;
using HungerAndHavoc.Storyteller.Suiyin;

namespace HungerAndHavoc.Incidents
{
    internal static class RHAH_IncidentArrival
    {
        internal static RHAH_Attitude For(string displayId)
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId(displayId);
            return For(entry);
        }

        internal static RHAH_Attitude For(RHAH_IncidentEntry entry)
        {
            if (entry == null)
            {
                return RHAH_Attitude.Neutral;
            }
            if (entry.DisplayId == "I-052")
            {
                return RHAH_Attitude.Neutral;
            }
            if (entry.DisplayId == "I-054")
            {
                return RHAH_Attitude.Hostile;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            RHAH_Attitude configured = settings == null
                ? entry.DefaultAttitude
                : settings.IncidentAttitude(entry.DisplayId, entry.DefaultAttitude);
            NarrativeState state = Verse.Current.Game?.GetComponent<NarrativeState>();
            int trust = state == null ? 0 : state.Snapshot().Trust;
            return RHAH_AttitudePolicy.Arrival(configured, RHAH_EndingRuntime.CountsNow(), trust);
        }
    }
}
