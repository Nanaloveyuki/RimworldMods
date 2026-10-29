#nullable enable
// ═════════════════════════════════════════════════════════════════════════════
//  STALE-DLL GUARD, part 1 of 2: the source fingerprint algorithm.
//  (Part 2 is BuildFingerprintCheck.cs. Overview: Architecture.md §3a.)
//
//  WHY: v1.1.x shipped to the Workshop with an Assemblies/ DLL that was older than
//  the Source/ + Defs/ next to it. It still declared the retired DefOf field
//  TPE_Event_SolarFlare, so every player got "Failed to find RimWorld.ThoughtDef
//  named TPE_Event_SolarFlare", and the newer code changes never shipped.
//  Neither RimWorld's Workshop uploader nor Modmixer's publish compiles anything,
//  so nothing caught it.
//
//  HOW: this ONE file is compiled TWICE, so both sides run byte-for-byte the same
//  algorithm:
//   1. At BUILD time, by MSBuild's RoslynCodeTaskFactory (ToddlersPlayExpanded.csproj,
//      target "TpeEmbedSourceFingerprint"). TPE_MOD_BUILD is NOT defined there, so
//      the TpeComputeSourceFingerprint MSBuild task at the bottom of this file is
//      included. Its result is embedded into the DLL as
//      [assembly: AssemblyMetadata("TPE.SourceFingerprint", "<hex>")].
//   2. Into the MOD itself (the csproj defines TPE_MOD_BUILD), where only
//      SourceFingerprint is compiled. At startup, in dev mode only,
//      BuildFingerprintCheck recomputes the hash over the shipped Source/ folder
//      and compares it with the embedded value.
//
//  Keep this file dependency-free (BCL only: no Verse/RimWorld/Unity/Harmony).
//  The MSBuild-task compile only references netstandard + MSBuild, so any game
//  type here would break the build.
// ═════════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Deterministic fingerprint of every C# source file under a <c>Source/</c> folder.
    /// It covers both file contents AND relative file names, so editing, adding, deleting
    /// or renaming any .cs file changes it. Used to detect a DLL that was not rebuilt
    /// after its source changed (see the file header).
    /// <para>internal: only this assembly uses it (BuildFingerprintCheck in the mod, and
    /// TpeComputeSourceFingerprint in the separately compiled MSBuild-task assembly). There's
    /// no reason to expose it to other mods.</para>
    /// </summary>
    internal static class SourceFingerprint
    {
        /// <summary>
        /// Key of the <see cref="System.Reflection.AssemblyMetadataAttribute"/> the csproj
        /// embeds. MUST match the literal <c>_Parameter1</c> in the csproj target
        /// "TpeEmbedSourceFingerprint" (MSBuild can't read this constant).
        /// </summary>
        public const string MetadataKey = "TPE.SourceFingerprint";

        /// <summary>
        /// Safety cap on folder recursion depth (guards against a junction/symlink loop).
        /// Source/ is currently flat, so this never matters in practice. Both compiles share
        /// this file, so the build and the runtime would truncate identically anyway.
        /// </summary>
        private const int MaxDepth = 16;

        /// <summary>
        /// Computes the fingerprint of all <c>*.cs</c> files under <paramref name="sourceDir"/>
        /// (recursively), skipping build-output folders (<c>obj</c>, <c>bin</c>) and hidden
        /// folders (names starting with '.', e.g. <c>.vs</c>). This is close to what the SDK
        /// compiles by default. One deliberate difference: the SDK only skips the TOP-LEVEL
        /// obj/bin, while this skips them at any depth. Modmixer's publish leaves obj out of the
        /// upload, so a nested obj/ hashed at build time would be missing at runtime and raise a
        /// false alarm. Source/ has no nested folders today. obj/ MUST stay excluded: it holds the
        /// generated AssemblyInfo.cs that contains the fingerprint itself (circular otherwise).
        /// <para>Algorithm (do NOT change one side only — both compiles share this code):
        /// for each file, take its key = path relative to <paramref name="sourceDir"/>,
        /// '/'-separated and lower-cased. Sort files by key (ordinal). Feed
        /// "key \0 sha256(file bytes with every CR removed) \n" for each file into an outer
        /// SHA-256 (UTF-8). The result is lower-case hex (64 chars).</para>
        /// <para>Why CR bytes (0x0D) are dropped: line-ending conversion (git text=auto, ZIP
        /// downloads, editors) turns CRLF into LF without changing a single line of code. Hashing
        /// raw bytes would make a correctly built copy look stale (a false alarm). With CRs
        /// dropped, CRLF and LF files hash the same, while every real code change still counts.
        /// In UTF-8, 0x0D only ever appears as the CR character itself, never inside another
        /// character, so nothing else is affected. Steam and Modmixer copy files byte-for-byte
        /// anyway; this is extra robustness.</para>
        /// <para>Cost: one sequential read of the source (≈1.3 MB today), with one file
        /// buffered at a time. It only runs at build time and, in-game, once at startup in dev
        /// mode (BuildFingerprintCheck).</para>
        /// </summary>
        /// <param name="sourceDir">The <c>Source/</c> folder (absolute or relative).</param>
        /// <returns>64-char lower-case hex SHA-256.</returns>
        public static string Compute(string sourceDir) => Compute(sourceDir, out _);

        /// <summary>
        /// Same as <see cref="Compute(string)"/>, and also reports how many .cs files were hashed.
        /// BuildFingerprintCheck uses the count to stay silent when a Source/ folder ships with no
        /// code in it, since there is nothing to compare. Without that, the empty-input hash would
        /// raise a misleading "out-of-date DLL" error.
        /// </summary>
        public static string Compute(string sourceDir, out int fileCount)
        {
            // (key, full path) pairs. Keys are built during the walk, NOT by stripping a
            // root prefix. The mod folder can be reached through a symlink (Modmixer links
            // the workspace into RimWorld/Mods), and a prefix strip could then mismatch.
            var files = new List<KeyValuePair<string, string>>(64);
            Collect(sourceDir, "", 0, files);

            // Ordinal sort on the lower-cased key: independent of the order the OS or runtime
            // enumerates files (it differs between .NET Framework, Mono and .NET Core) and of
            // the current culture.
            files.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            fileCount = files.Count;

            using (SHA256 perFile = SHA256.Create())
            using (SHA256 outer = SHA256.Create())
            {
                // ~90 bytes per file (key + 64 hex + 2 separators); presize to avoid regrowth.
                var manifest = new StringBuilder(files.Count * 96);
                for (int i = 0; i < files.Count; i++)
                {
                    byte[] content = File.ReadAllBytes(files[i].Value);
                    int length = StripCarriageReturns(content); // in place, no extra allocation
                    manifest.Append(files[i].Key)
                            .Append('\0')
                            .Append(ToHex(perFile.ComputeHash(content, 0, length)))
                            .Append('\n');
                }
                return ToHex(outer.ComputeHash(Encoding.UTF8.GetBytes(manifest.ToString())));
            }
        }

        /// <summary>
        /// Recursive walk. Adds every <c>.cs</c> file in <paramref name="dir"/> and descends into
        /// sub-folders except <c>obj</c>, <c>bin</c> and dot-folders.
        /// Enumerates with "*" and filters the extension in code. Each runtime matches a "*.cs"
        /// search pattern with its own engine: .NET Framework hands it to Windows FindFirstFile
        /// (8.3 short-name quirks), while Mono and .NET Core use their own globbing. Filtering
        /// here makes the build (.NET 10 MSBuild) and the game (Mono on 1.6, CoreCLR on 1.7) see
        /// exactly the same file set.
        /// </summary>
        private static void Collect(string dir, string keyPrefix, int depth, List<KeyValuePair<string, string>> files)
        {
            foreach (string file in Directory.GetFiles(dir, "*"))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(Path.GetExtension(name), ".cs", StringComparison.OrdinalIgnoreCase))
                    files.Add(new KeyValuePair<string, string>((keyPrefix + name).ToLowerInvariant(), file));
            }

            if (depth >= MaxDepth) return;

            foreach (string sub in Directory.GetDirectories(dir, "*"))
            {
                string name = Path.GetFileName(sub);
                if (name.Length == 0 || name[0] == '.'
                    || string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase))
                    continue;
                Collect(sub, keyPrefix + name + "/", depth + 1, files);
            }
        }

        /// <summary>
        /// Removes every CR byte (0x0D) from <paramref name="bytes"/> IN PLACE by compacting the
        /// remaining bytes to the front, and returns the new logical length. The array is a
        /// throw-away read buffer, so mutating it is safe. One linear pass, no allocation.
        /// </summary>
        private static int StripCarriageReturns(byte[] bytes)
        {
            int write = 0;
            for (int read = 0; read < bytes.Length; read++)
            {
                byte b = bytes[read];
                if (b != (byte)'\r') bytes[write++] = b;
            }
            return write;
        }

        /// <summary>Lower-case hex of <paramref name="bytes"/> (no separators). Culture-independent.</summary>
        private static string ToHex(byte[] bytes)
        {
            const string Digits = "0123456789abcdef";
            var chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = Digits[bytes[i] >> 4];
                chars[i * 2 + 1] = Digits[bytes[i] & 0xF];
            }
            return new string(chars);
        }
    }
}

#if !TPE_MOD_BUILD
// ─────────────────────────────────────────────────────────────────────────────
//  BUILD-TIME ONLY: the MSBuild task. It is compiled ONLY by RoslynCodeTaskFactory
//  (see the csproj UsingTask), never into the mod: the csproj defines TPE_MOD_BUILD,
//  and the mod doesn't reference MSBuild. It sits in the global namespace so
//  RoslynCodeTaskFactory finds it by its plain TaskName.
// ─────────────────────────────────────────────────────────────────────────────
/// <summary>MSBuild task: outputs <see cref="ToddlersPlayExpanded.SourceFingerprint.Compute"/> for <see cref="SourceDir"/>.</summary>
public class TpeComputeSourceFingerprint : Microsoft.Build.Utilities.Task
{
    /// <summary>The project's Source/ folder ($(MSBuildProjectDirectory)).</summary>
    [Microsoft.Build.Framework.Required]
    public string SourceDir { get; set; } = "";

    /// <summary>The computed fingerprint; the csproj embeds it as AssemblyMetadata.</summary>
    [Microsoft.Build.Framework.Output]
    public string Fingerprint { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            Fingerprint = ToddlersPlayExpanded.SourceFingerprint.Compute(SourceDir);
            Log.LogMessage(Microsoft.Build.Framework.MessageImportance.Normal,
                "TPE source fingerprint: " + Fingerprint);
            return true;
        }
        catch (Exception ex)
        {
            // Fail the build loudly. A DLL without a valid fingerprint would silently
            // disable the stale-DLL check it exists for.
            Log.LogError("TPE source fingerprint failed: " + ex.Message);
            return false;
        }
    }
}
#endif
