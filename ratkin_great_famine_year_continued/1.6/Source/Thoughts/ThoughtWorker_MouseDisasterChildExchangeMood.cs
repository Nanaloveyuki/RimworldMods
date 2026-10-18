using System.Linq;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ThoughtWorker_MouseDisasterChildExchangeMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!ModsConfig.BiotechActive ||
                !MouseDisasterUtility.IsBabyExpansionEnabled ||
                p == null ||
                p.Suspended ||
                !MouseDisasterUtility.IsMouseEggOrChild(p))
            {
                return ThoughtState.Inactive;
            }

            if (!MouseDisasterUtility.HasChildExchangeMoodMarker(p))
            {
                return ThoughtState.Inactive;
            }

            if (p.IsPrisonerOfColony)
            {
                return ThoughtState.ActiveAtStage(MouseDisasterUtility.IsMouseDisasterIncidentChild(p) ? 1 : 2);
            }

            if (!MouseDisasterUtility.IsPlayerAffiliatedRatkin(p))
            {
                return ThoughtState.ActiveAtStage(MouseDisasterUtility.IsMouseDisasterIncidentChild(p) ? 0 : 3);
            }

            return ThoughtState.Inactive;
        }
    }
}
