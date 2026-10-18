using HungerAndHavoc.Api;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_RHAH_PleadingJoin : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-053";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Refugee;
    }
}
