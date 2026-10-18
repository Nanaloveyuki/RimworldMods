using RimWorld;
using Verse;

namespace MouseDisaster
{
    public class HediffCompProperties_MouseDisasterEmploymentTimer : HediffCompProperties_DisappearsDisableable
    {
        public HediffCompProperties_MouseDisasterEmploymentTimer()
        {
            compClass = typeof(HediffComp_MouseDisasterEmploymentTimer);
        }
    }

    public class HediffComp_MouseDisasterEmploymentTimer : HediffComp_DisappearsDisableable
    {
        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (disabled || ticksToDisappear <= 0)
                {
                    return null;
                }

                return MouseDisasterVisitorUtility.FormatDurationLabel(ticksToDisappear);
            }
        }
    }
}
