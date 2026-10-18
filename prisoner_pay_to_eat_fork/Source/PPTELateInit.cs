using HarmonyLib;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Applies the compatibility patches that have to scan or patch other mods' types.
    ///
    /// These deliberately do NOT run from the Mod constructor. <see cref="LaborAreaColonistBlock.Apply"/>
    /// walks <see cref="GenTypes.AllTypes"/> and patches methods declared in every loaded assembly, and
    /// touching a foreign type's method runs that type's static initialiser. At mod-constructor time the
    /// def database is still empty, so a mod that resolves defs from static field initialisers caches
    /// every lookup as null and throws later — RJW resolves ~110 defs that way (see its rjw.xxx class),
    /// which is what breaks world/map generation and pawn rendering when it is loaded alongside us.
    /// [StaticConstructorOnStartup] runs from StaticConstructorOnStartupUtility.CallAll(), i.e. after
    /// defs are loaded and still well before any map or pawn exists, so patching there is harmless.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PPTELateInit
    {
        static PPTELateInit()
        {
            var harmony = new Harmony(PrisonersPayToEat2Mod.HarmonyId + ".lateinit");
            LaborAreaColonistBlock.Apply(harmony);
            HygieneBridge.Apply(harmony);
            Log.Message("[PrisonersPayToEat2] deferred compat patches applied (post-defs).");
        }
    }
}
