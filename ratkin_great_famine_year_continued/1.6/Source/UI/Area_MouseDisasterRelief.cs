using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class Area_MouseDisasterRelief : Area
    {
        public override string Label => "MouseDisaster_ReliefArea_Label".Translate();

        public override Color Color => new Color(0.85f, 0.62f, 0.22f);

        public override int ListPriority => 9500;

        public Area_MouseDisasterRelief()
        {
        }

        public Area_MouseDisasterRelief(AreaManager areaManager)
            : base(areaManager)
        {
        }

        public override string GetUniqueLoadID()
        {
            return "Area_" + ID + "_MouseDisasterRelief";
        }
    }
}
