using HungerAndHavoc.Api;
using RimWorld;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_RHAH_MouseSwarm : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => "I-052";
        protected override RHAH_PawnRole Role => RHAH_PawnRole.Thief;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return Core.GameComponent_RHAH_FamineYear.Active && base.CanFireNowSub(parms);
        }
    }
}
