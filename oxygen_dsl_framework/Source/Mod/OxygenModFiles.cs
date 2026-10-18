using System;
using System.Collections.Generic;
using System.IO;
using Oxygendsl.Core;
using Oxygendsl.Language;
using Verse;

namespace Oxygendsl.Mod
{
    internal sealed class OxygenModPlan
    {
        internal ContentPlan Content;
        internal OxygenSettingsPlan Settings;
    }

    internal static class OxygenModFiles
    {
        sealed class PreparedFile
        {
            internal string Source;
            internal RecordingCatalog Modules;
            internal OxygenFileCompilation Compilation;
        }
        static readonly Dictionary<ModContentPack, Dictionary<string, PreparedFile>> native = new Dictionary<ModContentPack, Dictionary<string, PreparedFile>>();
        static readonly Dictionary<ModContentPack, OxygenModPlan> current = new Dictionary<ModContentPack, OxygenModPlan>();
        internal static bool Ready { get; set; }

        internal static void Reset() { native.Clear(); current.Clear(); Ready = false; }

        internal static bool IrisMenusAvailable
        {
            get
            {
                foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
                    if (string.Equals(mod.PackageIdPlayerFacing, "Nanaloveyuki.IrisMenus", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        internal static OxygenModPlan Compile(ModContentPack provider, bool startup, bool reload = false)
        {
            if (!startup && !reload && current.TryGetValue(provider, out OxygenModPlan cached)) return cached;
            if (!native.TryGetValue(provider, out Dictionary<string, PreparedFile> prepared))
                native.Add(provider, prepared = new Dictionary<string, PreparedFile>(StringComparer.OrdinalIgnoreCase));
            var definitions = new List<ContentDefinition>();
            var patches = new List<ContentPatch>();
            var thoughts = new List<ThoughtRuleDefinition>();
            var gates = new List<GateRuleDefinition>();
            var pages = new List<OxygenSettingsPage>();
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            var seenNative = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (OxygenSourceFile file in OxygenSourceFiles.Discover(provider.foldersToLoadDescendingOrder))
            {
                try
                {
                    string source = File.ReadAllText(file.Path);
                    ArtiParseResult parsed = new ArtiParser().Parse(source);
                    bool hasNative = OxygenFileCompiler.HasNativeDeclarations(parsed.Program);
                    if (startup && !hasNative) continue;
                    OxygenFileCompilation compilation;
                    if (!startup && prepared.TryGetValue(file.RelativePath, out PreparedFile previous))
                    {
                        // Check imported native sources without executing the file a second time.
                        if (source != previous.Source || !previous.Modules.Unchanged())
                            throw new InvalidOperationException("Native content or its imports changed. Restart required; previous rules and settings retained.");
                        compilation = previous.Compilation;
                        seenNative.Add(file.RelativePath);
                    }
                    else
                    {
                        if (!startup && hasNative) throw new InvalidOperationException("New native content requires a restart.");
                        var modules = new RecordingCatalog();
                        compilation = OxygenFileCompiler.Compile(parsed, IrisMenusAvailable, modules,
                            OxygenSettingsHost.Modules, OxygenSettingsHost.Modules, startup ? StartupLocalization.Instance : OxygenLocalization.Instance);
                        if (!compilation.Success) throw new InvalidOperationException(string.Join("; ", compilation.Diagnostics));
                        if (startup)
                        {
                            prepared.Add(file.RelativePath, new PreparedFile { Source = source, Modules = modules, Compilation = compilation });
                        }
                    }
                    foreach (ContentDefinition definition in compilation.Content.Definitions)
                    {
                        if (!definitionIds.Add(definition.Type + "/" + definition.Id)) throw new InvalidOperationException("Duplicate definition '" + definition.Id + "'.");
                        definitions.Add(definition);
                    }
                    patches.AddRange(compilation.Content.Patches);
                    foreach (ThoughtRuleDefinition thought in compilation.Content.Thoughts)
                    {
                        if (!ruleIds.Add("thought/" + thought.Id)) throw new InvalidOperationException("Duplicate thought rule '" + thought.Id + "'.");
                        thoughts.Add(thought);
                    }
                    foreach (GateRuleDefinition gate in compilation.Content.Gates)
                    {
                        if (!ruleIds.Add("gate/" + gate.Id)) throw new InvalidOperationException("Duplicate gate rule '" + gate.Id + "'.");
                        gates.Add(gate);
                    }
                    foreach (OxygenSettingsPage page in compilation.Settings.Pages)
                    {
                        if (!pageIds.Add(page.Id)) throw new InvalidOperationException("Duplicate settings page '" + page.Id + "'.");
                        pages.Add(page);
                    }
                }
                catch (Exception exception)
                { throw new InvalidOperationException(provider.PackageIdPlayerFacing + "/Oxygen/" + file.RelativePath + ": " + exception.Message, exception); }
            }
            if (!startup && seenNative.Count != prepared.Count)
                throw new InvalidOperationException("A native Oxygen file was removed. Restart required; previous rules and settings retained.");
            var plan = new OxygenModPlan
            {
                Content = new ContentPlan(definitions, patches, thoughts, gates),
                Settings = new OxygenSettingsPlan(pages)
            };
            return plan;
        }

        internal static void Commit(ModContentPack provider, OxygenModPlan plan) { current[provider] = plan; }

        sealed class StartupLocalization : IOxygenLocalization
        {
            internal static readonly StartupLocalization Instance = new StartupLocalization();
            public string Translate(string key, IDictionary<string, object> arguments)
            {
                if (!Ready) throw new InvalidOperationException("lang is unavailable during native content loading; use DefInjected for native labels.");
                return OxygenLocalization.Instance.Translate(key, arguments);
            }
        }

        sealed class RecordingCatalog : IOxygenModuleCatalog
        {
            readonly SortedDictionary<string, string> sources = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public bool TryGetSource(string packageId, string modulePath, out string source)
            {
                bool found = OxygenModModuleCatalog.Instance.TryGetSource(packageId, modulePath, out source);
                sources[packageId.ToLowerInvariant() + "/" + modulePath] = found ? source : null;
                return found;
            }
            internal bool Unchanged()
            {
                foreach (KeyValuePair<string, string> entry in sources)
                {
                    int slash = entry.Key.IndexOf('/');
                    OxygenModModuleCatalog.Instance.TryGetSource(entry.Key.Substring(0, slash), entry.Key.Substring(slash + 1), out string source);
                    if (source != entry.Value) return false;
                }
                return true;
            }
        }
    }
}
