using RimWorld;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // Zone 分类按 XML 全名创建
    public abstract class Designator_AreaRHAH_Relief : Designator_Cells
    {
        readonly DesignateMode mode;

        public override bool DragDrawMeasurements => true;

        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        protected Designator_AreaRHAH_Relief(DesignateMode mode)
        {
            this.mode = mode;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 cell)
        {
            if (!cell.InBounds(Map))
            {
                return false;
            }

            Area_RHAH_Relief area = RHAH_ReliefArea.Get(Map);
            if (area == null)
            {
                return false;
            }

            bool contains = area[cell];
            return mode == DesignateMode.Add ? !contains : contains;
        }

        public override void DesignateSingleCell(IntVec3 cell)
        {
            Area_RHAH_Relief area = RHAH_ReliefArea.Get(Map);
            if (area == null)
            {
                return;
            }

            area[cell] = mode == DesignateMode.Add;
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            Area_RHAH_Relief area = RHAH_ReliefArea.Get(Map);
            if (area != null)
            {
                area.MarkForDraw();
            }
        }
    }

    public class Designator_AreaRHAH_ReliefExpand : Designator_AreaRHAH_Relief
    {
        public Designator_AreaRHAH_ReliefExpand() : base(DesignateMode.Add)
        {
            defaultLabel = "RHAH_ReliefArea_Expand".Translate();
            defaultDesc = "RHAH_ReliefArea_Expand_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/HomeAreaOn", true);
            soundDragSustain = SoundDefOf.Designate_DragAreaAdd;
            soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
            soundSucceeded = SoundDefOf.Designate_ZoneAdd;
            hotKey = KeyBindingDefOf.Misc4;
        }
    }

    public class Designator_AreaRHAH_ReliefClear : Designator_AreaRHAH_Relief
    {
        public Designator_AreaRHAH_ReliefClear() : base(DesignateMode.Remove)
        {
            defaultLabel = "RHAH_ReliefArea_Clear".Translate();
            defaultDesc = "RHAH_ReliefArea_Clear_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/HomeAreaOff", true);
            soundDragSustain = SoundDefOf.Designate_DragAreaDelete;
            soundDragChanged = null;
            soundSucceeded = SoundDefOf.Designate_ZoneDelete;
            hotKey = KeyBindingDefOf.Misc5;
        }
    }
}
