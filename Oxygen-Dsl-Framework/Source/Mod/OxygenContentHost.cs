using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using RimWorld;
using HarmonyLib;
using Oxygendsl.Core;
using Oxygendsl.Language;
using Oxygendsl.Mod.Content;
using Oxygendsl.Mod.Runtime;
using Verse;

namespace Oxygendsl.Mod
{
    public sealed class OxygenContentEntry
    {
        public ModContentPack Provider { get; internal set; }
        public string PackageId { get; internal set; }
        public string LayoutSignature { get; internal set; }
        public ContentPlan Content { get; internal set; }
        public ContentPlan Rules { get; internal set; }
        public IReadOnlyList<NativeContentChange> Changes { get; internal set; } = Array.Empty<NativeContentChange>();
        public bool Enabled { get; internal set; } = true;
        public string Error { get; internal set; } = string.Empty;
        public string EffectMode { get; internal set; } = "Startup content; rules reloadable";
    }

    public static class OxygenContentHost
    {
        static readonly List<OxygenContentEntry> entries = new List<OxygenContentEntry>();
        static bool installed;
        static int revision;
        public static IReadOnlyList<OxygenContentEntry> Entries { get { return entries; } }
        public static int Revision { get { return revision; } }

        internal static void Install()
        {
            if (installed) return;
            new Harmony("nanaloveyuki.oxygendsl.content").Patch(
                AccessTools.Method(typeof(LoadedModManager), nameof(LoadedModManager.ParseAndProcessXML)),
                prefix: new HarmonyMethod(typeof(OxygenContentHost), nameof(BeforeDefParsing)));
            installed = true;
        }

        // One framework-owned bridge, before XML inheritance, cross refs and native caches.
        static void BeforeDefParsing(XmlDocument xmlDoc, Dictionary<XmlNode, LoadableXmlAsset> assetlookup)
        {
            for (int i = 0; i < entries.Count; i++) NativeRuleRuntime.RemoveProvider(entries[i].PackageId);
            entries.Clear();
            OxygenModFiles.Reset();
            List<string> packages = new List<string>();
            foreach (ModContentPack active in LoadedModManager.RunningModsListForReading) packages.Add(active.PackageIdPlayerFacing);
            NativeContentSchema.Shared.SetActivePackages(packages);
            NativeContentSchema.Shared.MarkComplete();
            foreach (ModContentPack provider in LoadedModManager.RunningModsListForReading)
            {
                bool hasFolder = false;
                foreach (string folder in provider.foldersToLoadDescendingOrder)
                    if (Directory.Exists(Path.Combine(folder, "Oxygen"))) { hasFolder = true; break; }
                if (!hasFolder) continue;
                OxygenContentEntry entry = new OxygenContentEntry
                {
                    Provider = provider,
                    PackageId = provider.PackageIdPlayerFacing.ToLowerInvariant()
                };
                entries.Add(entry);
                try
                {
                    OxygenModPlan plan = OxygenModFiles.Compile(provider, true);
                    if (plan.Content.Definitions.Count != 0 || plan.Content.Patches.Count != 0)
                    {
                        var structural = new ContentPlan(plan.Content.Definitions, plan.Content.Patches, null, null);
                        NativeContentPipeline.Apply(structural, xmlDoc, assetlookup, provider);
                        entry.Changes = NativeContentPipeline.LastChanges;
                    }
                    entry.Content = plan.Content;
                }
                catch (Exception exception)
                {
                    entry.Error = exception.Message;
                    entry.EffectMode = "Rejected before native registration";
                    Log.Error("[Oxygendsl] " + entry.Error);
                }
            }
            revision++;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                foreach (OxygenContentEntry entry in entries)
                    if (entry.Content != null) entry.LayoutSignature = LoadedLayoutSignature(entry.Content);
                OxygenModFiles.Ready = true;
                ReloadRules(null);
            });
        }

        public static bool ReloadRules(Pawn previewPawn)
        {
            return ReloadRules(previewPawn, null);
        }

        public static bool ReloadRulesFor(string packageId, Pawn previewPawn)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("A provider package is required.", nameof(packageId));
            return ReloadRules(previewPawn, packageId);
        }

        static bool ReloadRules(Pawn previewPawn, string packageId)
        {
            bool success = true;
            bool found = false;
            for (int i = 0; i < entries.Count; i++)
            {
                OxygenContentEntry entry = entries[i];
                if (packageId != null && !string.Equals(entry.PackageId, packageId, StringComparison.OrdinalIgnoreCase)) continue;
                found = true;
                if (entry.Content == null) { success = false; continue; }
                try
                {
                    OxygenModPlan plan = OxygenModFiles.Compile(entry.Provider, false, OxygenModFiles.Ready);
                    NativeRuleRuntime.Validate(plan.Content);
                    if (previewPawn != null) NativeRuleRuntime.ValidateForPawn(plan.Content, previewPawn);
                    NativeRuleRuntime.SetProvider(entry.PackageId, plan.Content, entry.Enabled);
                    try { OxygenSettingsHost.Apply(entry.Provider, plan.Settings); }
                    catch
                    {
                        if (entry.Rules == null) NativeRuleRuntime.RemoveProvider(entry.PackageId);
                        else NativeRuleRuntime.SetProvider(entry.PackageId, entry.Rules, entry.Enabled);
                        throw;
                    }
                    OxygenModFiles.Commit(entry.Provider, plan);
                    entry.Rules = plan.Content;
                    entry.Error = string.Empty;
                    entry.EffectMode = "Startup content active; current runtime rules active";
                }
                catch (Exception exception)
                {
                    entry.Error = exception.Message;
                    success = false;
                }
            }
            revision++;
            return success && (packageId == null || found);
        }

        public static bool SetProviderEnabled(string packageId, bool enabled)
        {
            OxygenContentEntry entry = Find(packageId);
            if (entry == null || entry.Rules == null) return false;
            try { NativeRuleRuntime.SetEnabled(entry.PackageId, enabled); }
            catch (Exception exception)
            {
                entry.Error = exception.Message;
                revision++;
                return false;
            }
            entry.Enabled = enabled;
            revision++;
            return true;
        }

        internal static void RestoreGameState(IReadOnlyList<string> disabled, IReadOnlyList<ContentLayoutRecord> layouts)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                OxygenContentEntry entry = entries[i];
                bool desired = !Contains(disabled, entry.PackageId);
                if (layouts != null)
                    for (int j = 0; j < layouts.Count; j++)
                    {
                        ContentLayoutRecord saved = layouts[j];
                        if (saved == null || saved.PackageId != entry.PackageId || saved.Signature == entry.LayoutSignature) continue;
                        desired = false;
                        entry.Error = "Saved native content layout differs from installed content. Rules disabled; no stage/degree migration was performed.";
                        break;
                    }
                if (entry.Rules != null) SetProviderEnabled(entry.PackageId, desired);
            }
            NativeRuleRuntime.InvalidateSituationalThoughts();
            revision++;
        }

        public static string Inspect()
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                OxygenContentEntry entry = entries[i];
                text.Append(entry.PackageId).Append(entry.Enabled ? " [enabled]" : " [disabled]").AppendLine();
                text.AppendLine(entry.EffectMode);
                if (entry.Content != null)
                {
                    text.Append("Native definitions: ").Append(entry.Content.Definitions.Count)
                        .Append("; patches: ").Append(entry.Content.Patches.Count).AppendLine();
                    for (int j = 0; j < entry.Content.Definitions.Count; j++)
                    {
                        ContentDefinition definition = entry.Content.Definitions[j];
                        text.Append("  ").Append(definition.Type).Append('/').Append(definition.Id)
                            .AppendLine(" — referenced native content is not deleted when rules are disabled");
                    }
                    foreach (NativeContentChange change in entry.Changes)
                        text.Append("  ").Append(change.Order).Append(' ').Append(change.Kind).Append(' ').Append(change.Type)
                            .Append('/').Append(change.Id).Append('.').Append(change.Field).Append(": ")
                            .Append(change.Before).Append(" -> ").Append(change.After).AppendLine();
                }
                if (!string.IsNullOrEmpty(entry.Error)) text.AppendLine(entry.Error);
                text.AppendLine();
            }
            text.Append(NativeRuleRuntime.Inspection);
            return text.ToString();
        }

        public static string Preview(Pawn pawn)
        {
            if (pawn == null) return "Select a pawn to preview native thoughts and gates.";
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Rules != null) text.AppendLine(NativeRuleRuntime.PreviewThoughts(entries[i].PackageId, pawn));
            return text.ToString();
        }

        static OxygenContentEntry Find(string packageId)
        {
            for (int i = 0; i < entries.Count; i++)
                if (string.Equals(entries[i].PackageId, packageId, StringComparison.OrdinalIgnoreCase)) return entries[i];
            return null;
        }

        static bool Contains(IReadOnlyList<string> values, string value)
        {
            if (values != null)
                for (int i = 0; i < values.Count; i++)
                    if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string LoadedLayoutSignature(ContentPlan plan)
        {
            StringBuilder text = new StringBuilder();
            foreach (ContentDefinition definition in plan.Definitions)
            {
                text.Append(definition.Type).Append('/').Append(definition.Id).Append(':');
                if (definition.Type == "TraitDef")
                {
                    TraitDef trait = DefDatabase<TraitDef>.GetNamedSilentFail(definition.Id);
                    if (trait == null) text.Append("missing");
                    else foreach (TraitDegreeData degree in trait.degreeDatas) text.Append('[').Append(degree.degree).Append(':').Append(degree.label).Append(']');
                }
                else if (definition.Type == "ThoughtDef")
                {
                    ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(definition.Id);
                    if (thought == null) text.Append("missing");
                    else
                    {
                        text.Append(thought.workerClass == null ? string.Empty : thought.workerClass.FullName);
                        text.Append(thought.thoughtClass == null ? string.Empty : thought.thoughtClass.FullName);
                        foreach (ThoughtStage stage in thought.stages) text.Append('[').Append(stage.label).Append(']');
                    }
                }
                text.AppendLine();
            }
            return Hash(text.ToString());
        }

        static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }

    }

    public sealed class ContentLayoutRecord : IExposable
    {
        public string PackageId;
        public string Signature;
        public void ExposeData()
        {
            Scribe_Values.Look(ref PackageId, "packageId");
            Scribe_Values.Look(ref Signature, "layoutSignature");
        }
    }
}
