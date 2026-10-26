using System;
using System.Collections.Generic;
using IrisMenus;
using Verse;

internal static class ThingRegistryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var category = new ThingCategoryDef { defName = "Resources" };
        DefDatabase<ThingCategoryDef>.AllDefsListForReading.Add(category);
        var silver = new ThingDef { defName = "Silver", category = ThingCategory.Item, thingCategories = new List<ThingCategoryDef> { category, category } };
        var building = new ThingDef { defName = "Building", category = ThingCategory.Building };
        var pawn = new ThingDef { defName = "Pawn", category = ThingCategory.Pawn };
        var blueprint = new ThingDef { defName = "Blueprint", category = ThingCategory.Building };
        var nearMatch = new ThingDef { defName = "Mote", category = ThingCategory.Item };
        var effects = new[]
        {
            new ThingDef { defName = "Mote_Glow", category = ThingCategory.Item, thingCategories = new List<ThingCategoryDef> { category } },
            new ThingDef { defName = "Mod_MoTe_Spark", category = ThingCategory.Item },
            new ThingDef { defName = "EffectWithoutPrefix", category = ThingCategory.Mote, thingCategories = new List<ThingCategoryDef> { category } }
        };
        var source = DefDatabase<ThingDef>.AllDefsListForReading;
        source.Add(silver);
        source.AddRange(effects);
        source.Add(building);
        source.Add(pawn);
        source.Add(blueprint);
        // More than one indexing batch proves progress still describes the scanned source.
        for (int i = 0; i < 260; i++) source.Add(new ThingDef { defName = "Item_" + i, category = ThingCategory.Item });
        source.Add(nearMatch);
        ThingRegistry.Begin();
        check(ThingRegistry.ProcessedCount == 256 && ThingRegistry.TotalCount == source.Count && !ThingRegistry.IsReady,
            "Thing registry progress counts all scanned definitions, including excluded effects");
        while (!ThingRegistry.IsReady) ThingRegistry.Advance();
        check(ThingRegistry.ProcessedCount == source.Count && ThingRegistry.Progress == 1f,
            "Thing registry filtering reaches complete source progress");
        check(ThingRegistry.Things.Count == source.Count - effects.Length,
            "Thing registry exposes only registered definitions");
        foreach (ThingDef effect in effects)
        {
            check(!ThingRegistry.TryGet(effect.defName, out _), "Effect absent from name lookup: " + effect.defName);
            check(!Contains(ThingRegistry.Things, effect) && !Contains(ThingRegistry.Uncategorized, effect) && !Contains(ThingRegistry.InCategory(category), effect),
                "Effect absent from every registry collection: " + effect.defName);
        }
        check(ThingRegistry.TryGet("Silver", out ThingDef found) && ReferenceEquals(found, silver) && Contains(ThingRegistry.Things, silver),
            "Silver remains registered and discoverable");
        check(ThingRegistry.InCategory(category).Count == 1 && ReferenceEquals(ThingRegistry.InCategory(category)[0], silver),
            "Filtering retains direct category membership without duplicate entries");
        check(Contains(ThingRegistry.Uncategorized, building) && Contains(ThingRegistry.Uncategorized, pawn) && Contains(ThingRegistry.Uncategorized, blueprint),
            "Effect exclusion does not broaden to buildings, pawns or blueprints");
        check(ThingRegistry.Errors.Count == 0, "Excluded effects do not create registry diagnostics");
        check(ThingRegistry.TryGet("Mote", out ThingDef nearFound) && ReferenceEquals(nearFound, nearMatch),
            "Name filtering requires the underscore in Mote_");
        check(Log.Messages.Count > 0 && Log.Messages[Log.Messages.Count - 1].Contains(
                ThingRegistry.Things.Count + " registered of " + source.Count + " scanned definitions", StringComparison.Ordinal),
            "Registry completion log distinguishes registered and scanned counts");
    }

    private static bool Contains(IReadOnlyList<ThingDef> source, ThingDef item)
    {
        for (int i = 0; i < source.Count; i++) if (ReferenceEquals(source[i], item)) return true;
        return false;
    }
}

namespace Verse
{
    public enum ThingCategory { Item, Building, Pawn, Mote }
    public sealed class ThingCategoryDef { public string defName; }
    public sealed class ThingDef
    {
        public string defName;
        public ThingCategory category;
        public List<ThingCategoryDef> thingCategories;
    }
    public static class DefDatabase<T>
    {
        public static readonly List<T> AllDefsListForReading = new List<T>();
    }
    public sealed class StaticConstructorOnStartup : Attribute { }
    public static class LongEventHandler { public static void ExecuteWhenFinished(Action action) => action(); }
    public class Root { public void Update() { } }
}
