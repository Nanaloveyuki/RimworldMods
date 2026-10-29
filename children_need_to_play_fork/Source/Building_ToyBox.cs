using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Vanilla ToyBox patched to function as <see cref="Building_Storage"/>.
    /// Dynamically swaps between empty and full textures based on whether
    /// any items are currently stored inside.
    /// </summary>
    public class Building_ToyBox : Building_Storage
    {
        // Our own graphic cache, separate from Thing.graphicInt (which vanilla uses)
        Graphic toyBoxGraphicInt;
        bool wasFull;
        bool graphicDirty = true;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            graphicDirty = true;
        }

        public override void TickRare()
        {
            base.TickRare();
            CheckFullnessChanged();
        }

        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);
            CheckFullnessChanged();
        }

        public override void Notify_LostThing(Thing newItem)
        {
            base.Notify_LostThing(newItem);
            CheckFullnessChanged();
        }

        void CheckFullnessChanged()
        {
            bool isFull = HasStoredItems();
            if (isFull != wasFull)
            {
                wasFull = isFull;
                graphicDirty = true;
                Notify_ColorChanged(); // invalidates mesh and vanilla graphicInt
            }
        }

        public override Graphic Graphic
        {
            get
            {
                // Vanilla pattern: cache the computed graphic and only recompute when dirty
                if (graphicDirty || toyBoxGraphicInt == null)
                {
                    if (def.graphicData == null)
                    {
                        toyBoxGraphicInt = BaseContent.BadGraphic;
                    }
                    else
                    {
                        // Pick the appropriate texture path based on fullness
                        string texPath = HasStoredItems()
                            ? "Things/Building/Childcare/ToyBox"
                            : "Things/Building/ToyBox/ToyBox_Empty";

                        // Step 1: Create base graphic with Color.white
                        // This mirrors GraphicData.Init() exactly
                        Graphic baseGraphic = GraphicDatabase.Get<Graphic_Single>(
                            texPath,
                            def.graphicData.shaderType.Shader,
                            def.graphicData.drawSize,
                            Color.white,
                            Color.white,
                            def.graphicData);

                        // Step 2: Apply thing's color
                        // This mirrors GraphicData.GraphicColoredFor() exactly
                        if (DrawColor.IndistinguishableFrom(Color.white)
                            && DrawColorTwo.IndistinguishableFrom(Color.white))
                        {
                            toyBoxGraphicInt = baseGraphic;
                        }
                        else
                        {
                            toyBoxGraphicInt = baseGraphic.GetColoredVersion(
                                def.graphicData.shaderType.Shader,
                                DrawColor,
                                DrawColorTwo);
                        }
                    }
                    graphicDirty = false;
                }
                return toyBoxGraphicInt;
            }
        }

        public override void Notify_ColorChanged()
        {
            base.Notify_ColorChanged(); // nulls Thing.graphicInt + styleGraphicInt, dirties mesh
            graphicDirty = true;
            toyBoxGraphicInt = null;
        }

        bool HasStoredItems()
        {
            if (slotGroup == null || Map == null) return false;
            foreach (IntVec3 cell in slotGroup.CellsList)
            {
                List<Thing> things = Map.thingGrid.ThingsListAt(cell);
                foreach (Thing t in things)
                {
                    if (t.def.category == ThingCategory.Item)
                        return true;
                }
            }
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref wasFull, "wasFull", false);
        }
    }
}
