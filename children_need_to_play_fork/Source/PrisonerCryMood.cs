using RimWorld;
using Verse;

namespace ToddlersPlayExpanded
{
    internal static class PrisonerCryMood
    {
        internal static bool AllowsMemory(Pawn hearer, Pawn? cryingPawn, ThoughtDef thought)
        {
            if (cryingPawn?.IsPrisoner != true)
                return true;

            TPE_Settings? settings = TPE_Mod.Settings;
            if (settings == null || (hearer.IsFreeNonSlaveColonist
                ? settings.prisonerCryAffectsColonists : settings.prisonerCryAffectsOthers))
                return true;

            return thought != ThoughtDefOf.CryingBaby && thought != ThoughtDefOf.MyCryingBaby
                && thought != TPE_ThoughtDefOf.TPE_ChildCrying && thought != TPE_ThoughtDefOf.TPE_MyChildCrying
                && thought != TPE_ThoughtDefOf.TPE_OlderChildCrying && thought != TPE_ThoughtDefOf.TPE_MyOlderChildCrying;
        }
    }
}
