using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace IrisMenus
{
    /// <summary>Game-thread snapshot of loaded definitions, indexed incrementally without creating Things.</summary>
    public static class ThingRegistry
    {
        private static readonly ThingDef[] emptyThings = Array.Empty<ThingDef>();
        private static readonly List<ThingDef> registered = new List<ThingDef>();
        private static readonly List<ThingDef> uncategorized = new List<ThingDef>();
        private static readonly List<string> errors = new List<string>();
        private static readonly Dictionary<string, ThingDef> byName = new Dictionary<string, ThingDef>(StringComparer.Ordinal);
        private static readonly Dictionary<ThingCategoryDef, List<ThingDef>> members = new Dictionary<ThingCategoryDef, List<ThingDef>>();
        private static readonly Dictionary<ThingCategoryDef, IReadOnlyList<ThingDef>> categoryViews = new Dictionary<ThingCategoryDef, IReadOnlyList<ThingDef>>();
        private static ThingDef[] source = emptyThings;
        private static bool started;

        public static IReadOnlyList<ThingDef> Things { get; } = registered.AsReadOnly();
        public static IReadOnlyList<ThingCategoryDef> Categories { get; private set; } = Array.AsReadOnly(Array.Empty<ThingCategoryDef>());
        public static IReadOnlyList<ThingDef> Uncategorized { get; } = uncategorized.AsReadOnly();
        public static IReadOnlyList<string> Errors { get; } = errors.AsReadOnly();
        public static int ProcessedCount { get; private set; }
        public static int TotalCount => source.Length;
        public static bool IsReady { get; private set; }
        public static int Revision { get; private set; }
        public static float Progress => !started ? 0f : TotalCount == 0 ? 1f : (float)ProcessedCount / TotalCount;

        public static bool TryGet(string defName, out ThingDef definition)
        {
            definition = null;
            return defName != null && byName.TryGetValue(defName, out definition);
        }

        // Direct membership preserves multiple categories; callers traverse category.parent for ancestry.
        public static IReadOnlyList<ThingDef> InCategory(ThingCategoryDef category) =>
            category != null && categoryViews.TryGetValue(category, out IReadOnlyList<ThingDef> view) ? view : emptyThings;

        internal static void Begin()
        {
            if (started) return;
            var definitions = DefDatabase<ThingDef>.AllDefsListForReading;
            source = definitions.ToArray();
            var categories = DefDatabase<ThingCategoryDef>.AllDefsListForReading.ToArray();
            Categories = Array.AsReadOnly(categories);
            foreach (ThingCategoryDef category in categories)
            {
                if (category == null) { errors.Add("Null ThingCategoryDef in loaded definitions."); continue; }
                var list = new List<ThingDef>();
                members.Add(category, list);
                categoryViews.Add(category, list.AsReadOnly());
            }
            started = true;
            Revision++;
            Advance();
        }

        internal static void Advance()
        {
            if (!started || IsReady) return;
            int end = Math.Min(TotalCount, ProcessedCount + 256);
            while (ProcessedCount < end)
            {
                ThingDef definition = source[ProcessedCount++];
                if (definition == null) { errors.Add("Null ThingDef at index " + (ProcessedCount - 1)); continue; }
                if (definition.category == ThingCategory.Mote ||
                    definition.defName?.IndexOf("Mote_", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                registered.Add(definition);
                if (string.IsNullOrEmpty(definition.defName)) errors.Add("ThingDef without defName at index " + (ProcessedCount - 1));
                else if (byName.ContainsKey(definition.defName)) errors.Add("Duplicate ThingDef: " + definition.defName);
                else byName.Add(definition.defName, definition);
                bool categorized = false;
                var categories = definition.thingCategories;
                if (categories != null)
                {
                    for (int i = 0; i < categories.Count; i++)
                    {
                        ThingCategoryDef category = categories[i];
                        if (category == null || !members.TryGetValue(category, out List<ThingDef> list))
                        { errors.Add(definition.defName + ": unregistered category " + (category?.defName ?? "<null>")); continue; }
                        bool duplicate = false;
                        for (int j = 0; j < i; j++) if (ReferenceEquals(categories[j], category)) { duplicate = true; break; }
                        if (!duplicate) list.Add(definition);
                        categorized = true;
                    }
                }
                if (!categorized) uncategorized.Add(definition);
            }
            Revision++;
            if (ProcessedCount != TotalCount) return;
            IsReady = true;
            Log.Message("[IrisMenus] Thing registry ready: " + registered.Count + " registered of " + TotalCount + " scanned definitions, " + Categories.Count +
                " game categories, " + uncategorized.Count + " uncategorized, " + errors.Count + " diagnostics.");
        }
    }

    [StaticConstructorOnStartup]
    internal static class ThingRegistryStartup
    {
        static ThingRegistryStartup() => LongEventHandler.ExecuteWhenFinished(ThingRegistry.Begin);
    }

    [HarmonyPatch(typeof(Root), nameof(Root.Update))]
    internal static class ThingRegistryUpdate
    {
        private static void Postfix() => ThingRegistry.Advance();
    }
}
