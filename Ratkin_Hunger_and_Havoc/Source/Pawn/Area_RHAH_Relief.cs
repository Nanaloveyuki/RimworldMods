using HungerAndHavoc.Core;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // Verse 按 AreaManager 全名创建并写入 .rws
    public class Area_RHAH_Relief : Area
    {
        static readonly Color ReliefColor = new Color(0.85f, 0.62f, 0.22f);

        public Area_RHAH_Relief()
        {
        }

        public Area_RHAH_Relief(AreaManager areaManager) : base(areaManager)
        {
        }

        public override string Label => "RHAH_ReliefArea_Label".Translate();

        public override Color Color => ReliefColor;

        // 高于家区 低于禁止区
        public override int ListPriority => 9500;

        public override string GetUniqueLoadID()
        {
            return "Area_" + ID + "_RHAH_Relief";
        }

        protected override void Set(IntVec3 cell, bool value)
        {
            if (this[cell] == value)
            {
                return;
            }

            base.Set(cell, value);
            MapComponent_RHAH_Map component = Map != null
                ? Map.GetComponent<MapComponent_RHAH_Map>()
                : null;
            if (component != null)
            {
                component.InvalidateFoodSearch();
            }
        }
    }
}
