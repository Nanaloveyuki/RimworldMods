using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ThoughtWorker_MouseDisasterVisitorShelterMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || p.Dead || p.Suspended || p.needs?.mood == null)
            {
                return ThoughtState.Inactive;
            }

            return MouseDisasterVisitorUtility.HasShelterMood(p)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }
}
