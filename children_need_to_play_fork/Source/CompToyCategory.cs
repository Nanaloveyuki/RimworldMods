using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Def-level properties for <see cref="CompToyCategory"/>.
    /// Set <c>categoryLabel</c> in XML to display a custom category name
    /// on the inspect panel (e.g. "Plush toys", "Sensory Toys").
    /// </summary>
    public class CompProperties_ToyCategory : CompProperties
    {
        public string categoryLabel = "";

        public CompProperties_ToyCategory()
        {
            compClass = typeof(CompToyCategory);
        }
    }

    /// <summary>
    /// Displays a "Category: X" line in the inspect panel for toys.
    /// Each toy ThingDef declares its category via <c>&lt;compClass&gt;CompToyCategory&lt;/compClass&gt;</c>
    /// and a <c>categoryLabel</c> in its XML.
    /// </summary>
    public class CompToyCategory : ThingComp
    {
        public CompProperties_ToyCategory Props => (CompProperties_ToyCategory)props;

        public override string CompInspectStringExtra()
        {
            if (!Props.categoryLabel.NullOrEmpty())
                return "Category: " + Props.categoryLabel;
            return null;
        }
    }
}
