using Verse;

namespace HungerAndHavoc.Pawn
{
    public class CompProperties_RHAH_Clay : CompProperties
    {
        public CompProperties_RHAH_Clay()
        {
            compClass = typeof(Comp_RHAH_Clay);
        }
    }

    public class Comp_RHAH_Clay : ThingComp
    {
        public override void PostIngested(Verse.Pawn ingester)
        {
            Hediff_RHAH_ClaySatiety.NotifyEaten(ingester);
        }
    }
}
