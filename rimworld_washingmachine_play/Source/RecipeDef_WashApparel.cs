using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WashingMachine;

public class RecipeDef_WashApparel : RecipeDef
{
    public override void ResolveReferences()
    {
        base.ResolveReferences();
        // Keep the selector independent of smelting, burning, biocoding and mod-added restrictions.
        forceHiddenSpecialFilters = new List<SpecialThingFilterDef>();
        foreach (SpecialThingFilterDef filter in DefDatabase<SpecialThingFilterDef>.AllDefsListForReading)
        {
            if (filter != SpecialThingFilterDefOf.AllowDeadmansApparel)
                forceHiddenSpecialFilters.Add(filter);
            fixedIngredientFilter.SetAllow(filter, true);
            defaultIngredientFilter.SetAllow(filter, true);
            ingredients[0].filter.SetAllow(filter, true);
        }
        foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (!thing.IsApparel)
                continue;
            fixedIngredientFilter.SetAllow(thing, true);
            defaultIngredientFilter.SetAllow(thing, true);
            ingredients[0].filter.SetAllow(thing, true);
        }
    }
}
