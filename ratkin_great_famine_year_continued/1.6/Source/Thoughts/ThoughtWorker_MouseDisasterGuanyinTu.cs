using UnityEngine;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class ThoughtWorker_MouseDisasterGuanyinTu : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || p.Dead || p.Suspended || p.needs?.mood == null)
            {
                return ThoughtState.Inactive;
            }

            Hediff_GuanyinTuSatiety satiety = MouseDisasterGuanyinTuUtility.GetSatiety(p);
            if (satiety == null || satiety.Severity <= 0f)
            {
                return ThoughtState.Inactive;
            }

            int stage = Mathf.Clamp(Mathf.CeilToInt(satiety.Severity * 10f) - 1, 0, 9);
            return ThoughtState.ActiveAtStage(stage);
        }
    }
}
