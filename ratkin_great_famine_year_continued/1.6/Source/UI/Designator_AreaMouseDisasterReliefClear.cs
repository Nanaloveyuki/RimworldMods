using RimWorld;
using UnityEngine;
using Verse;

namespace MouseDisaster
{
    public class Designator_AreaMouseDisasterReliefClear : Designator_AreaMouseDisasterRelief
    {
        public Designator_AreaMouseDisasterReliefClear()
            : base(DesignateMode.Remove)
        {
            defaultLabel = "MouseDisaster_ReliefArea_Clear".Translate();
            defaultDesc = "MouseDisaster_ReliefArea_Clear_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/HomeAreaOff");
            soundDragSustain = SoundDefOf.Designate_DragAreaDelete;
            soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneDelete;
            hotKey = KeyBindingDefOf.Misc5;
        }
    }
}
