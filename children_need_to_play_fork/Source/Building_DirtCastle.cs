using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// A dirt castle left behind by a child after playing.
    /// Shows the builder's name in its label: "[Name]'s Castle!"
    /// </summary>
    public class Building_DirtCastle : Building
    {
        /// <summary>The name of the child who built this castle.</summary>
        public string builderName = "Unknown";

        /// <summary>
        /// Dynamic label showing the builder's name.
        /// </summary>
        public override string Label => "TPE_DirtCastle_BuilderLabel".Translate(
            builderName == "Unknown" ? "TPE_DirtCastle_UnknownBuilder".Translate().ToString() : builderName);

        /// <summary>
        /// Save/load the builder name so it persists across saves.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref builderName, "builderName", "Unknown");
        }
    }
}
