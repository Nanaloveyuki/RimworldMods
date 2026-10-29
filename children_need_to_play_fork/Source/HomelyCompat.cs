using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Optional compatibility layer for the Workshop mod "Intimacy - Homely"
    /// (packageId <see cref="HomelyPackageId"/>), which adds a "walk to the
    /// wardrobe and get undressed" ritual before colonists go to sleep.
    ///
    /// Without this layer, two things silently go wrong when both mods are
    /// active and a child (age 3-6) is affected by both systems:
    ///
    ///   1. Homely's own "undress at the wardrobe" job is implemented as a
    ///      subclass of vanilla <c>JobDriver_LayDown</c>
    ///      (<c>PillowTalkWithEuterpe.JobDriver_UndressBeforeBed</c>). Our
    ///      plush-before-sleep redirect
    ///      (<see cref="Patch_JobDriver_LayDown_SetupToils_Child"/>) treats
    ///      ANY LayDown-family job as "the child is about to sleep" and,
    ///      if they don't have a plush yet, cancels it to send them to
    ///      fetch one. That means every time Homely tries to walk a
    ///      plush-less child to the wardrobe, our redirect cancels the trip
    ///      before it can happen — the undressing ritual silently never
    ///      completes.
    ///   2. Homely doesn't know our plush toys aren't "clothing" — by
    ///      default it will pack a worn plush into the wardrobe right along
    ///      with the child's actual apparel, stripping it before the child
    ///      is actually asleep and defeating our plush-sleep mood/thought
    ///      detection (they'd wake up flagged as having slept with no
    ///      plush, even though they own one).
    ///
    /// <see cref="Initialize"/> resolves everything purely by packageId
    /// string and reflection (<c>AccessTools.TypeByName</c> /
    /// <c>FieldInfo</c>) — there is never a compile-time reference to
    /// Homely's assembly, so this class is 100% safe to load whether or not
    /// Homely is installed. See the "harmony" lore entry on optional-mod
    /// compat: never <c>typeof()</c> a type that might not exist.
    /// </summary>
    public static class HomelyCompat
    {
        /// <summary>Homely - PillowTalk's packageId, exactly as declared in its own About.xml.</summary>
        public const string HomelyPackageId = "Homely.PillowTalk.WithEuterpe";

        /// <summary>
        /// Full type name of Homely's "walk to wardrobe and undress" JobDriver.
        /// Compared by string in <see cref="Patch_JobDriver_LayDown_SetupToils_Child"/> —
        /// never resolved to an actual <see cref="Type"/> unless <see cref="Active"/> is true.
        /// </summary>
        public const string UndressBeforeBedTypeName = "PillowTalkWithEuterpe.JobDriver_UndressBeforeBed";

        /// <summary>Full type name of Homely's per-apparel DefModExtension.</summary>
        const string ModExtensionTypeName = "PillowTalkWithEuterpe.HomelyModExtension";

        /// <summary>
        /// True only when Homely is active in the current mod list AND its
        /// expected internals were successfully resolved via reflection.
        /// Every other member of this mod that cares about Homely compat
        /// treats a false <see cref="Active"/> as "do nothing, ever" — this
        /// is the single choke point that gates the whole feature.
        /// </summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// Detects Homely and, if present, tags all of our plush toy
        /// ThingDefs with Homely's own "never take this off while
        /// undressing" DefModExtension so it treats a worn plush as
        /// sleep-safe rather than something to pack into the wardrobe.
        ///
        /// Safe to call unconditionally at startup — fully no-ops (and
        /// leaves <see cref="Active"/> false) when Homely isn't installed,
        /// and fails closed (also leaves <see cref="Active"/> false) if
        /// Homely's internals ever change shape in a way reflection can't
        /// follow, rather than risk a half-applied compat state.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (!ModsConfig.IsActive(HomelyPackageId))
                {
                    Active = false;
                    return;
                }

                Type modExtType = AccessTools.TypeByName(ModExtensionTypeName);
                if (modExtType == null)
                {
                    Log.Warning("[TPE] Homely compat: \"Intimacy - Homely\" is active but " +
                        ModExtensionTypeName + " could not be found via reflection " +
                        "(possible Homely update changed its internals). Skipping Homely compatibility layer.");
                    Active = false;
                    return;
                }

                FieldInfo isSleepwearField = AccessTools.Field(modExtType, "isSleepwear");
                FieldInfo neverTakeOffField = AccessTools.Field(modExtType, "neverEverTakeOffWhileUndressing");
                if (isSleepwearField == null || neverTakeOffField == null)
                {
                    Log.Warning("[TPE] Homely compat: HomelyModExtension fields could not be resolved " +
                        "via reflection. Skipping Homely compatibility layer.");
                    Active = false;
                    return;
                }

                // From this point on, both required gates (Homely active,
                // internals resolved) are satisfied — flip Active now, BEFORE
                // the tagging loop below. Fix 1 (the SetupToils job-type skip
                // in Patch_JobDriver_LayDown_SetupToils_Child) only reads this
                // flag and doesn't depend on tagging succeeding at all, so it
                // must not get disabled by a failure in the tagging loop.
                Active = true;

                int tagged = 0;
                try
                {
                    foreach (ThingDef plush in AllPlushDefs())
                    {
                        // Skip if this def was already tagged (e.g. re-entrant call,
                        // or a future version of this mod already handled it).
                        bool alreadyTagged = plush.modExtensions != null
                            && plush.modExtensions.Exists(e => e != null && e.GetType() == modExtType);
                        if (alreadyTagged) continue;

                        object ext = Activator.CreateInstance(modExtType);
                        // isSleepwear: Homely treats this as "already dressed for bed,
                        // don't remove it during the undress ritual" (unless the pawn
                        // is a Nudist, who strips everything regardless).
                        isSleepwearField.SetValue(ext, true);
                        // neverEverTakeOffWhileUndressing: the stronger guarantee —
                        // Homely will never remove this item during the undress
                        // ritual for ANY reason, including Nudist pawns, and it also
                        // won't count against "had to sleep dressed" unhappiness.
                        // A comfort toy should never be treated as removable clothing.
                        neverTakeOffField.SetValue(ext, true);

                        if (plush.modExtensions == null)
                            plush.modExtensions = new List<DefModExtension>();
                        plush.modExtensions.Add((DefModExtension)ext);
                        tagged++;
                    }
                }
                catch (Exception tagEx)
                {
                    // Tagging is best-effort and independent of Fix 1 (Active
                    // stays true above). Whatever plush defs were tagged before
                    // the failure keep their protection; log and move on rather
                    // than tearing down the whole compat layer over one def.
                    Log.Warning("[TPE] Homely compat: error while tagging plush toy defs (tagged " +
                        tagged + " before failure). Fix for the sleep-job redirect is still active. " + tagEx);
                }

                if (TPE_Mod.Settings?.debugLogging ?? false)
                {
                    Log.Message($"[TPE] Homely compat: \"Intimacy - Homely\" detected. Tagged {tagged} plush toy " +
                        "def(s) as sleep-safe apparel so Homely won't undress children out of them at bedtime, " +
                        "and the plush-fetch redirect will now step aside for Homely's own undress-before-bed job.");
                }
            }
            catch (Exception ex)
            {
                // Never let an optional third-party compat layer take down our
                // own mod's load. Fail closed: compat off, everything else in
                // this mod keeps working exactly as it did before Homely existed.
                Log.Warning("[TPE] Homely compat: unexpected error while initializing, disabling compat layer. " + ex);
                Active = false;
            }
        }

        /// <summary>
        /// Enumerates all of our plush toy ThingDefs by defName prefix — the
        /// same filter the developmentalStageFilter startup fixup already
        /// uses in <see cref="HarmonyPatches"/>. Deliberately NOT using
        /// <c>TPE_ThingDefOf</c> DefOf fields here: this runs very early in
        /// the same static-constructor pass as that fixup, and defName
        /// filtering over <c>DefDatabase&lt;ThingDef&gt;.AllDefs</c> sidesteps
        /// any question of whether DefOf resolution has completed yet.
        /// Also future-proof: any new "TPE_Plush*" apparel def is picked up
        /// automatically without editing this list.
        /// </summary>
        static IEnumerable<ThingDef> AllPlushDefs()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.defName != null && def.defName.StartsWith("TPE_Plush") && def.apparel != null)
                    yield return def;
            }
        }
    }
}
