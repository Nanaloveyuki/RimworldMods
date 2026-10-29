using System;
using System.IO;
using System.Reflection;
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// STALE-DLL GUARD, part 2 of 2 (part 1 = SourceFingerprint.cs; overview: Architecture.md §3a).
    /// <para>Once at startup, ONLY in dev mode, verifies that this DLL was compiled from the
    /// <c>Source/</c> folder that ships next to it. The build embeds the fingerprint of the
    /// source it compiled (AssemblyMetadata "TPE.SourceFingerprint"). This recomputes the
    /// fingerprint of the Source/ folder that is actually on disk. If they differ, the DLL is
    /// out of date: some code changes are missing from it, and errors like "Failed to find
    /// ThoughtDef named …" can appear (exactly what happened to the v1.1.x Workshop upload).
    /// One clear <see cref="Log.Error(string)"/> then names the real cause.</para>
    /// <para>Who sees it: the mod author during test runs (Modmixer launches with dev mode on).
    /// If a stale build is ever published again, dev-mode players see it too. Players without
    /// dev mode pay nothing: the method returns on its first check.</para>
    /// <para>Silent no-ops (by design): dev mode off; no Source/ folder shipped; a Source/ folder
    /// with no .cs files; a DLL built without the fingerprint (e.g. someone compiled it with a
    /// different project file).</para>
    /// </summary>
    internal static class BuildFingerprintCheck
    {
        /// <summary>
        /// Runs the check. Called once from the <see cref="TPE_Mod"/> constructor.
        /// <para>THREADING: mod constructors run on RimWorld's loading thread
        /// (LoadedModManager.CreateModClasses inside a LongEvent), not Unity's main thread.
        /// That is fine here: only file I/O, hashing, reflection over our own assembly, and
        /// Verse.Log (thread-safe; it locks internally). No Unity API is touched.</para>
        /// <para>Prefs are initialised in Root.Start, before any mod class is constructed, so
        /// <see cref="Prefs.DevMode"/> already holds the player's real setting here. A later
        /// toggle of dev mode in-game does NOT re-run the check (startup-only, by design).</para>
        /// </summary>
        /// <param name="content">This mod's content pack (the constructor's argument). Nullable on
        /// purpose: LoadedModManager.CreateModClasses finds it with FirstOrDefault(), so in theory it
        /// can pass null.</param>
        internal static void RunIfDevMode(ModContentPack? content)
        {
            // Cheapest gate first: non-dev players exit here, so the check costs them nothing.
            if (!Prefs.DevMode || content == null) return;

            try
            {
                // RootDir is the folder this mod was loaded from (Workshop, local Mods/ or
                // Modmixer's symlink). Use it instead of Assembly.Location: RimWorld loads mod
                // DLLs from raw bytes (ModAssemblyHandler), so Location is empty.
                string sourceDir = Path.Combine(content.RootDir, "Source");
                if (!Directory.Exists(sourceDir)) return; // Source/ not shipped → nothing to compare

                string? built = GetEmbeddedFingerprint();
                if (string.IsNullOrEmpty(built)) return;  // DLL built without the guard → can't judge

                string onDisk = SourceFingerprint.Compute(sourceDir, out int fileCount);
                if (fileCount == 0) return; // Source/ exists but holds no code (e.g. a repack): nothing to compare
                if (string.Equals(built, onDisk, StringComparison.Ordinal)) return; // in sync: stay silent

                // Mismatch → stale (or locally modified) build. Log text is developer-facing.
                // It stays English on purpose: vanilla never translates log lines, and Keyed
                // translations aren't loaded yet when mod constructors run.
                Log.Error("[TPE] Out-of-date DLL: Assemblies/ToddlersPlayExpanded.dll was NOT compiled from the Source/ "
                    + "folder shipped next to it (DLL built from source " + Short(built!) + ", Source/ on disk is "
                    + Short(onDisk) + "). Recent code changes are missing, and errors such as \"Failed to find "
                    + "ThoughtDef named …\" can follow. Mod author: rebuild the mod, then publish. Players: please "
                    + "report this on the mod's Workshop page. (This check only runs in dev mode.)");
            }
            catch (Exception ex)
            {
                // Unreadable folder, locked file, etc.: just say the check was skipped. Never
                // let a diagnostic break mod loading.
                Log.Warning("[TPE] Build check skipped (dev mode only): " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Reads the fingerprint the build embedded (AssemblyMetadata key
        /// <see cref="SourceFingerprint.MetadataKey"/>), or null if absent. Reflection over our
        /// own assembly, once, dev mode only.
        /// </summary>
        private static string? GetEmbeddedFingerprint()
        {
            object[] attrs = typeof(BuildFingerprintCheck).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false);
            for (int i = 0; i < attrs.Length; i++)
            {
                if (attrs[i] is AssemblyMetadataAttribute meta
                    && string.Equals(meta.Key, SourceFingerprint.MetadataKey, StringComparison.Ordinal))
                    return meta.Value;
            }
            return null;
        }

        /// <summary>First 12 hex chars: enough to tell two builds apart in a log line.</summary>
        private static string Short(string fingerprint)
            => fingerprint.Length > 12 ? fingerprint.Substring(0, 12) : fingerprint;
    }
}
