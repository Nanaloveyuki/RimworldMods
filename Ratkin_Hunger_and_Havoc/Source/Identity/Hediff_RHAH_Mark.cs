using Verse;

namespace HungerAndHavoc.Identity
{
    public class Hediff_RHAH_Mark : HediffWithComps
    {
        public CompRHAH_Pawn Hunger
        {
            get
            {
                if (comps == null)
                {
                    return null;
                }

                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i] is CompRHAH_Pawn hunger)
                    {
                        return hunger;
                    }
                }

                return null;
            }
        }

        public override bool ShouldRemove => false;

        public override string LabelBase
        {
            get
            {
                CompRHAH_Pawn comp = Hunger;
                if (comp == null)
                {
                    return base.LabelBase;
                }

                string key = comp.RoleLabelKey;
                TaggedString translated = key.Translate();
                if (translated.RawText == key)
                {
                    return base.LabelBase;
                }

                return translated;
            }
        }

        public override string GetInspectString()
        {
            CompRHAH_Pawn comp = Hunger;
            if (comp == null)
            {
                return null;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            return Pawn.RHAH_VisitorRules.StayCountdown(
                comp.State.stayKind,
                now,
                comp.State.leaveAfterGameTick,
                Pawn.RHAH_VisitorStay.PausesStay(pawn),
                comp.State.stayRemainingTicks);
        }
    }
}
