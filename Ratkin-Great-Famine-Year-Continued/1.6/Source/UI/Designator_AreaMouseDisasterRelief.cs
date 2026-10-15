using RimWorld;
using Verse;

namespace MouseDisaster
{
    public abstract class Designator_AreaMouseDisasterRelief : Designator_Cells
    {
        private readonly DesignateMode mode;

        public override bool DragDrawMeasurements => true;

        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Areas;

        protected Designator_AreaMouseDisasterRelief(DesignateMode mode)
        {
            this.mode = mode;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map))
            {
                return false;
            }

            Area_MouseDisasterRelief reliefArea = MouseDisasterUtility.GetReliefArea(Map);
            if (reliefArea == null)
            {
                return false;
            }

            bool contains = reliefArea[c];
            return mode == DesignateMode.Add ? !contains : contains;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            Area_MouseDisasterRelief reliefArea = MouseDisasterUtility.GetReliefArea(Map);
            if (reliefArea == null)
            {
                return;
            }

            reliefArea[c] = mode == DesignateMode.Add;
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            MouseDisasterUtility.GetReliefArea(Map)?.MarkForDraw();
        }
    }
}
