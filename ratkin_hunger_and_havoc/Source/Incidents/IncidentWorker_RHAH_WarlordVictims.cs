using HungerAndHavoc.Api;
using RimWorld;

namespace HungerAndHavoc.Incidents
{
    internal sealed class IncidentWorker_RHAH_WarlordVictims : IncidentWorker_RHAH_OriginalGroup
    {
        protected override string DisplayId => RHAH_IncidentCatalog.GetByDefName(def.defName).DisplayId;
        protected override RHAH_PawnRole Role => RHAH_PawnRole.RatkinYoung;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return RHAH_WarlordCompat.Available(DisplayId) && base.CanFireNowSub(parms);
        }
    }
}
