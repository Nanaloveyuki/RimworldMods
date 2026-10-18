using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ThoughtWorker_MouseDisasterN004FamilySurvived : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || p.Dead || p.Suspended || p.needs?.mood == null)
            {
                return ThoughtState.Inactive;
            }

            int stage = Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.GetN004FamilySurvivedThoughtStage(p) ?? -1;
            return stage < 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(stage);
        }
    }

    public class ThoughtWorker_MouseDisasterN004MotherRegret : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || p.Dead || p.Suspended || p.needs?.mood == null)
            {
                return ThoughtState.Inactive;
            }

            return Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>()?.HasN004MotherRegret(p) == true
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }
}
