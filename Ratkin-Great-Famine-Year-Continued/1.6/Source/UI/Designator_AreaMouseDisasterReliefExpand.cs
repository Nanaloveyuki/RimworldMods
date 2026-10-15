using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class Designator_AreaMouseDisasterReliefExpand : Designator_AreaMouseDisasterRelief
    {
        public Designator_AreaMouseDisasterReliefExpand()
            : base(DesignateMode.Add)
        {
            defaultLabel = "MouseDisaster_ReliefArea_Expand".Translate();
            defaultDesc = "MouseDisaster_ReliefArea_Expand_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/HomeAreaOn");
            soundDragSustain = SoundDefOf.Designate_DragAreaAdd;
            soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd;
            hotKey = KeyBindingDefOf.Misc4;
        }
    }
}
