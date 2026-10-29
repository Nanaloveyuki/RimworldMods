using System;
using System.Collections.Generic;
using System.Linq;
using IrisMenus;
using UnityEngine;
using Verse;

internal static class Program
{
    private static int assertions;
    private static void Main()
    {
        Registry();
        LoaderConstruction();
        Session();
        Scrolling();
        SettingsRoundTrip();
        MainButtonVisibility();
        Routing();
        AdapterChecks.Run(Check);
        MarkdownChecks.Run(Check);
        CatalogChecks.Run(Check);
        ModNameChecks.Run(Check);
        ItemValueChecks.Run(Check);
        ThingRegistryChecks.Run(Check);
        Console.WriteLine("PASS: " + assertions + " runtime assertions (engine doubles; no game rendering).");
    }

    private static void Registry()
    {
        var owner = new Mod();
        Throws<ArgumentNullException>(() => MenuRegistry.Register(null, "x", () => "X", _ => { }));
        Throws<ArgumentException>(() => MenuRegistry.Register(owner, " ", () => "X", _ => { }));
        Throws<ArgumentNullException>(() => MenuRegistry.Register(owner, "x", null, _ => { }));
        Throws<ArgumentNullException>(() => MenuRegistry.Register(owner, "x", () => "X", null));
        string title = "First";
        MenuRegistry.Register(owner, "first", () => title, owner.DoSettingsWindowContents);
        MenuPage page = MenuRegistry.Registered.Single();
        Check(page.DisplayTitle == "First", "Registered title");
        title = "Translated";
        Check(page.DisplayTitle == title, "Dynamic title");
        page.Draw(default); page.Save();
        Check(owner.Draws == 1 && owner.Saves == 1, "Owner callbacks");
        Throws<ArgumentException>(() => MenuRegistry.Register(owner, "first", () => "Duplicate", _ => { }));
        Check(MenuRegistry.Registered.Count() == 1, "Duplicate registration is atomic");
        MenuRegistry.Register(owner, "second", () => throw new Exception("title"), _ => { });
        MenuPage brokenTitle = MenuRegistry.Registered.Last();
        int errors = Log.Errors.Count;
        Check(brokenTitle.DisplayTitle == owner.Content.Name, "Title fallback");
        Check(brokenTitle.DisplayTitle == owner.Content.Name && Log.Errors.Count == errors + 1, "Title failure logged once");
        var adapter = new OtherMod();
        var noSettings = new NoSettings();
        var badCategory = new BadCategory();
        LoadedModManager.ModHandles.AddRange(new Mod[] { owner, adapter, noSettings, badCategory, new IrisMenusMod(new ModContentPack()) });
        var adapters = MenuRegistry.Adaptable();
        Check(adapters.Count == 1 && ReferenceEquals(adapters[0].Owner, adapter), "Adapters exclude registered, empty, failed and self");
        adapters[0].Draw(default); adapters[0].Save();
        Check(adapter.Draws == 1 && adapter.Saves == 1, "Adapter invokes original draw/save");
        Check(MenuRegistry.OwnerId(owner) != MenuRegistry.OwnerId(adapter), "Different mod types retain distinct identity");
        MenuRegistry.RegisterListing(adapter, "listing", () => "Listing", listing => listing.CurHeight = 100);
        Check(MenuRegistry.Adaptable().Count == 0, "Native registration supersedes manual host");
        MenuRegistry.Open(); MenuRegistry.Open();
        Check(Find.WindowStack.Windows.Count == 1, "Open is idempotent");
        int saves = 0;
        var customOwner = new Mod { Content = new ModContentPack { PackageIdPlayerFacing = "test.custom" } };
        MenuRegistry.Register(customOwner, "custom", () => "Custom", _ => { }, () => saves++);
        MenuRegistry.Registered.Last().Save();
        Check(saves == 1 && customOwner.Saves == 0, "Custom save replaces default");
    }

    private static void LoaderConstruction()
    {
        var content = new ModContentPack { PackageIdPlayerFacing = "loader.iris", Name = "Loader" };
        foreach (Type type in typeof(MenuRegistry).Assembly.GetTypes())
        {
            if (type.Namespace != typeof(MenuRegistry).Namespace || !type.IsSubclassOf(typeof(Mod)) || type.IsAbstract) continue;
            var instance = (Mod)Activator.CreateInstance(type, content);
            Check(ReferenceEquals(instance.Content, content), "Game loader constructs " + type.FullName + " with its content pack");
        }
    }

    private static void Session()
    {
        int errors = 0, saves = 0, draws = 0;
        bool failSave = true, failDraw = true;
        var owner = new Mod();
        var first = new MenuPage("a", owner, () => "A", _ =>
        {
            draws++;
            if (failDraw) throw new Exception("draw");
        }, () => { saves++; if (failSave) throw new Exception("save"); }, false);
        var second = new MenuPage("b", owner, () => "B", _ => { }, () => { }, true);
        var session = new MenuSession((_, exception) => errors++);
        Check(session.Save(), "Empty session save");
        Check(session.Select(first), "Initial selection");
        Check(session.Select(first) && saves == 0, "Reselect does not save");
        session.Draw(default); session.Draw(default);
        Check(draws == 1 && session.DrawFailed && errors == 1, "Failed draw does not repeat every event");
        Check(!session.Select(second) && ReferenceEquals(session.Selected, first), "Save failure retains selection");
        Check(!session.Save(), "Close can be refused on save failure");
        failDraw = false;
        session.Retry(); session.Draw(default);
        Check(draws == 2 && !session.DrawFailed, "Retry redraws");
        failSave = false;
        Check(session.Select(second) && ReferenceEquals(session.Selected, second), "Successful save permits switch");
        Check(session.Select(null) && session.Selected == null, "Manage page saves and clears selection");
        var exit = new MenuPage("exit", owner, () => "Exit", _ => throw new ExitGUIException(), () => { }, false);
        session.Select(exit);
        Throws<ExitGUIException>(() => session.Draw(default));
        Check(!session.DrawFailed, "ExitGUI is not a page failure");
    }

    private static void Scrolling()
    {
        var scroll = new MenuScrollView();
        Rect viewport = new Rect(0, 0, 400, 300);
        Throws<ArgumentNullException>(() => scroll.Draw(viewport, null));
        scroll.Draw(viewport, listing => { Check(listing.maxOneColumn, "Listing is single column"); listing.CurHeight = 1200; });
        Check(Widgets.Groups == 0, "Scroll groups balanced");
        scroll.Draw(viewport, listing => listing.CurHeight = 1200);
        Check(Widgets.LastContent.height == 1204 && Widgets.LastContent.width == 380, "Measured scroll extent");
        Throws<InvalidOperationException>(() => scroll.Draw(viewport, _ => throw new InvalidOperationException()));
        Check(Widgets.Groups == 0, "Exception restores groups");
        scroll.Draw(viewport, listing => listing.CurHeight = 10);
        scroll.Draw(viewport, listing => listing.CurHeight = 10);
        Check(Widgets.LastContent.height == 300, "Shrinking content fits viewport");
        scroll.Reset();
        scroll.Draw(viewport, _ => { });
        Check(Widgets.LastPosition.y == 0, "Reset clears scroll");
    }

    private static void SettingsRoundTrip()
    {
        SettingsLegacyMigration();
        SettingsNewKeyPrecedence();
        SettingsSerializationPhases();
        SettingsNormalization();
    }

    private static void SettingsLegacyMigration()
    {
        var original = new MenuSettings
        {
            Theme = IrisMenus.UI.UiTheme.Dark, Scale = 1.2f, Density = 2, PanelOpacity = 0.72f,
            ReducedMotion = true, RememberState = false, ShowMainButton = false,
            ShowTips = false, TipDelay = 0.6f, SearchCurrentMod = true,
            MarqueeText = "CUSTOM MARQUEE", ToastDuration = 6f, ShowToasts = false,
            LastPage = "test:page"
        };
        original.EnabledAdapters.Add("unloaded.mod:Settings");
        original.HiddenMainButtons.Add("World");
        original.SidebarPins.Add("test.owner");
        original.SidebarOrder.Add("unloaded.mod:Settings");
        original.SidebarOrder.Add("test.owner");
        original.SidebarAliases["test.owner"] = "My settings";
        original.LastSubItems["test.owner"] = "test:page";
        original.HostView["mode"] = "search";
        original.HostView["query"] = "custom query";
        original.HostView["scope"] = "test.owner";
        original.HostView["resultPage"] = "3";
        original.HostView["resultScroll"] = "140";
        original.HostView["navigationScroll"] = "80";
        original.HostView["adapterScroll"] = "260";
        original.HostView["showNavigation"] = "1";
        original.HostView["listing:owner:page=a;b"] = "240";
        original.ItemValueBands = new List<ItemValueBand>
        {
            new ItemValueBand(0f, new Color(0.1f, 0.2f, 0.3f)),
            new ItemValueBand(123f, new Color(0.4f, 0.5f, 0.6f))
        };
        Dictionary<string, object> legacy = SaveSettings(original);
        foreach (string key in new[] { "theme", "scale", "density", "panelOpacity" })
        {
            legacy["native" + char.ToUpperInvariant(key[0]) + key.Substring(1)] = legacy[key];
            legacy.Remove(key);
        }
        legacy["nativeMode"] = false;
        legacy["width"] = 920f;
        legacy["height"] = 640f;
        legacy["x"] = 55f;
        legacy["y"] = 75f;
        legacy["frostEnabled"] = false;
        legacy["blur"] = 0.8f;
        legacy["transparency"] = 0.4f;
        original.ItemValueBands[1].Minimum = 999f;

        MenuSettings restored = LoadSettings(legacy);
        Check(restored.Theme == IrisMenus.UI.UiTheme.Dark && restored.Scale == 1.2f && restored.Density == 2
            && restored.PanelOpacity == 0.72f, "Legacy appearance values migrate even when legacy mode was disabled");
        Check(restored.ReducedMotion && !restored.ShowTips && restored.TipDelay == 0.6f && restored.SearchCurrentMod
            && restored.MarqueeText == "CUSTOM MARQUEE" && restored.ToastDuration == 6f && !restored.ShowToasts,
            "Migration preserves motion, tips, search, marquee and toast preferences");
        Check(!restored.RememberState && !restored.ShowMainButton && restored.HiddenMainButtons.Single() == "World",
            "Migration preserves memory and main-button preferences");
        Check(restored.LastPage == "test:page" && restored.LastSubItems["test.owner"] == "test:page",
            "Migration preserves selected pages and subitems");
        Check(restored.HostView["railScroll"] == "80" && restored.HostView["managementScroll"] == "260"
            && !restored.HostView.ContainsKey("navigationScroll") && !restored.HostView.ContainsKey("adapterScroll")
            && !restored.HostView.ContainsKey("showNavigation"), "Legacy host offsets move to new rails and management without obsolete visibility");
        Check(restored.HostView["resultScroll"] == "140" && restored.HostView["listing:owner:page=a;b"] == "240",
            "Migration preserves result offsets and listing IDs containing separators");
        var browse = new MenuBrowseState();
        browse.Restore(restored.HostView, null, true);
        Check(browse.Scope == "test.owner" && browse.Query == "custom query" && browse.ResultPage == 3 && browse.ShowResults,
            "Migrated host state still restores the current-mod search and results page");
        Check(restored.EnabledAdapters.Single() == "unloaded.mod:Settings" && restored.SidebarPins.Single() == "test.owner"
            && restored.SidebarOrder.SequenceEqual(new[] { "unloaded.mod:Settings", "test.owner" })
            && restored.SidebarAliases["test.owner"] == "My settings", "Migration preserves adapters and sidebar customization");
        Check(restored.ItemValueBands.Count == 2 && restored.ItemValueBands[1].Minimum == 123f
            && restored.ItemValueBands[1].Color.r == 0.4f && restored.ItemValueBands[1].Color.g == 0.5f
            && restored.ItemValueBands[1].Color.b == 0.6f, "Deep-scribed item value thresholds and colors survive migration independently");
        string[] retiredKeys = { "nativeMode", "width", "height", "x", "y", "frostEnabled", "blur", "transparency" };
        Check(!Scribe_Values.LookCalls.Any(call => retiredKeys.Contains(call.Key)), "Retired mode, geometry and frost fields are ignored during loading");
        legacy["nativeMode"] = true;
        MenuSettings enabledLegacyMode = LoadSettings(legacy);
        Check(enabledLegacyMode.Theme == restored.Theme && enabledLegacyMode.Scale == restored.Scale
            && enabledLegacyMode.Density == restored.Density && enabledLegacyMode.PanelOpacity == restored.PanelOpacity
            && enabledLegacyMode.HostView.OrderBy(pair => pair.Key).SequenceEqual(restored.HostView.OrderBy(pair => pair.Key)),
            "Either obsolete mode flag produces the same settings and navigation");

        Dictionary<string, object> saved = SaveSettings(restored);
        Check(new[] { "theme", "scale", "density", "panelOpacity" }.All(saved.ContainsKey)
            && !saved.Keys.Any(key => key.StartsWith("native", StringComparison.Ordinal) || retiredKeys.Contains(key)),
            "Resaving migrated settings writes only current appearance keys and no retired fields");
        var host = (Dictionary<string, string>)saved["hostView"];
        Check(!host.ContainsKey("navigationScroll") && !host.ContainsKey("adapterScroll") && !host.ContainsKey("showNavigation")
            && host["railScroll"] == "80" && host["managementScroll"] == "260" && host["listing:owner:page=a;b"] == "240",
            "Resaving host state emits only current offsets while retaining listing positions");
        MenuSettings reloaded = LoadSettings(saved);
        Check(reloaded.Theme == restored.Theme && reloaded.Scale == restored.Scale && reloaded.Density == restored.Density
            && reloaded.PanelOpacity == restored.PanelOpacity && reloaded.LastSubItems["test.owner"] == "test:page"
            && reloaded.SidebarAliases["test.owner"] == "My settings" && reloaded.ItemValueBands[1].Minimum == 123f,
            "Migrated settings survive a second save and load through the current schema");
        reloaded.ClearRememberedView();
        Check(reloaded.LastPage == null && reloaded.LastSubItems.Count == 0 && reloaded.HostView.Count == 0,
            "Disabling memory clears all remembered host navigation");
        Check(reloaded.EnabledAdapters.Single() == "unloaded.mod:Settings" && reloaded.SidebarPins.Single() == "test.owner"
            && reloaded.ItemValueBands[1].Minimum == 123f && reloaded.PanelOpacity == 0.72f,
            "Disabling memory leaves adapters, sidebar, colors and appearance intact");
    }

    private static void SettingsNewKeyPrecedence()
    {
        var current = new MenuSettings();
        Dictionary<string, object> mixed = SaveSettings(current);
        mixed["theme"] = current.Theme;
        mixed["scale"] = current.Scale;
        mixed["density"] = current.Density;
        mixed["panelOpacity"] = current.PanelOpacity;
        mixed["nativeTheme"] = current.Theme == IrisMenus.UI.UiTheme.Light ? IrisMenus.UI.UiTheme.Dark : IrisMenus.UI.UiTheme.Light;
        mixed["nativeScale"] = current.Scale <= 1.15f ? 1.2f : 1f;
        mixed["nativeDensity"] = (current.Density + 1) % 3;
        mixed["nativePanelOpacity"] = current.PanelOpacity > 0.8f ? 0.72f : 0.94f;
        var host = (Dictionary<string, string>)mixed["hostView"];
        host["railScroll"] = "0";
        host["managementScroll"] = "0";
        host["navigationScroll"] = "80";
        host["adapterScroll"] = "260";
        host["showNavigation"] = "1";
        MenuSettings restored = LoadSettings(mixed);
        Check(restored.Theme == current.Theme && restored.Scale == current.Scale && restored.Density == current.Density
            && restored.PanelOpacity == current.PanelOpacity, "Present current keys override legacy values even when equal to current defaults");
        Check(restored.HostView["railScroll"] == "0" && restored.HostView["managementScroll"] == "0"
            && !restored.HostView.ContainsKey("navigationScroll") && !restored.HostView.ContainsKey("adapterScroll")
            && !restored.HostView.ContainsKey("showNavigation"), "Present zero host offsets win over nonzero legacy positions");
    }

    private static void SettingsSerializationPhases()
    {
        var settings = new MenuSettings { Scale = 1.1f, PanelOpacity = 0.8f };
        Dictionary<string, object> data = SaveSettings(settings);
        data["scale"] = 1.2f;
        data["nativeScale"] = 1.3f;
        Scribe_Values.Data.Clear();
        foreach (var pair in data) Scribe_Values.Data[pair.Key] = pair.Value;
        Scribe_Values.LookCalls.Clear();
        foreach (LoadSaveMode mode in new[] { LoadSaveMode.Inactive, LoadSaveMode.ResolvingCrossRefs, LoadSaveMode.PostLoadInit })
        {
            Scribe.mode = mode;
            settings.ExposeData();
        }
        Scribe.mode = LoadSaveMode.Inactive;
        Check(settings.Scale == 1.1f && settings.PanelOpacity == 0.8f && (float)Scribe_Values.Data["scale"] == 1.2f,
            "Inactive and post-load scribing neither reload nor overwrite persisted values");
        Check(!Scribe_Values.LookCalls.Any(call => call.Key.StartsWith("native", StringComparison.Ordinal)),
            "Legacy keys are not read outside LoadingVars");
        LoadSettings(data);
        string[] legacyKeys = { "nativeTheme", "nativeScale", "nativeDensity", "nativePanelOpacity" };
        Check(legacyKeys.All(key => Scribe_Values.LookCalls.Count(call => call.Key == key && call.Mode == LoadSaveMode.LoadingVars) == 1)
            && Scribe_Values.LookCalls.Where(call => legacyKeys.Contains(call.Key)).All(call => call.Mode == LoadSaveMode.LoadingVars),
            "Each legacy key is read once and only in LoadingVars");
    }

    private static void SettingsNormalization()
    {
        Dictionary<string, object> data = SaveSettings(new MenuSettings());
        data["scale"] = float.NaN;
        data["density"] = 8;
        data["panelOpacity"] = float.PositiveInfinity;
        data["tipDelay"] = float.NegativeInfinity;
        data["toastDuration"] = float.NaN;
        MenuSettings restored = LoadSettings(data);
        Check(!float.IsNaN(restored.Scale) && !float.IsInfinity(restored.Scale) && restored.Scale >= 0.8f && restored.Scale <= 1.3f
            && restored.Density == 2 && !float.IsNaN(restored.PanelOpacity) && !float.IsInfinity(restored.PanelOpacity)
            && restored.PanelOpacity >= 0f && restored.PanelOpacity <= 1f && restored.TipDelay >= 0.1f
            && restored.TipDelay <= 0.8f && restored.ToastDuration >= 1f && restored.ToastDuration <= 10f,
            "Settings replace nonfinite values and normalize density into supported ranges");
        data["scale"] = 8f;
        data["density"] = -8;
        data["panelOpacity"] = -2f;
        data["tipDelay"] = 8f;
        data["toastDuration"] = -8f;
        restored = LoadSettings(data);
        Check(restored.Scale == 1.3f && restored.Density == 0 && restored.PanelOpacity == 0f
            && restored.TipDelay == 0.8f && restored.ToastDuration == 1f, "Finite out-of-range settings clamp at supported limits");
        restored = LoadSettings(new Dictionary<string, object>());
        Check(restored.EnabledAdapters != null && restored.HiddenMainButtons != null && restored.SidebarPins != null
            && restored.SidebarOrder != null && restored.SidebarAliases != null && restored.LastSubItems != null
            && restored.HostView != null && restored.ItemValueBands != null && restored.ItemValueBands.Count > 0,
            "Missing persisted collections leave settings and color lookup ready for use");
    }

    private static Dictionary<string, object> SaveSettings(MenuSettings settings)
    {
        Scribe_Values.Data.Clear();
        Scribe_Values.LookCalls.Clear();
        Scribe.mode = LoadSaveMode.Saving;
        try
        {
            settings.ExposeData();
            return new Dictionary<string, object>(Scribe_Values.Data);
        }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
    }

    private static MenuSettings LoadSettings(Dictionary<string, object> data)
    {
        Scribe_Values.Data.Clear();
        Scribe_Values.LookCalls.Clear();
        foreach (var pair in data) Scribe_Values.Data[pair.Key] = pair.Value;
        var settings = new MenuSettings();
        try
        {
            foreach (LoadSaveMode mode in new[] { LoadSaveMode.LoadingVars, LoadSaveMode.ResolvingCrossRefs, LoadSaveMode.PostLoadInit })
            {
                Scribe.mode = mode;
                settings.ExposeData();
            }
            return settings;
        }
        finally { Scribe.mode = LoadSaveMode.Inactive; }
    }

    private static void MainButtonVisibility()
    {
        var settings = IrisMenusMod.Instance.Settings;
        bool previous = settings.ShowMainButton;
        var worker = new MainButtonWorker_IrisMenus();
        try
        {
            settings.ShowMainButton = false;
            Check(!worker.Visible, "Main button hides immediately when disabled");
            settings.ShowMainButton = true;
            Check(worker.Visible, "Same main button reappears immediately when enabled");
            worker.EngineVisible = false;
            Check(!worker.Visible, "User preference preserves engine visibility restrictions");
        }
        finally { settings.ShowMainButton = previous; }
    }


    private static void Routing()
    {
        var mod = new OtherMod();
        Find.WindowStack.Windows.Clear();
        var original = new RimWorld.Dialog_ModSettings(mod);
        Find.WindowStack.Add(original);
        Check(ReferenceEquals(Find.WindowStack.Windows.Single(), original), "Unselected mod keeps original entry");
        Find.WindowStack.Windows.Clear();
        IrisMenusMod.Instance.Settings.EnabledAdapters.Add(MenuRegistry.OwnerId(mod));
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(mod));
        var host = Find.WindowStack.WindowOfType<MenuWindow>();
        Check(host != null && ReferenceEquals(host.Page.Owner, mod), "Actual Harmony patch routes selected mod");
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(mod));
        Check(Find.WindowStack.Windows.Count == 1 && Find.WindowStack.Focused == host, "Existing host reused and focused");
        host.AcceptPage = false;
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(mod));
        Check(Find.WindowStack.Windows.Count == 1, "Failed selection does not leak an original dialog");
        var independentStack = new WindowStack();
        original = new RimWorld.Dialog_ModSettings(mod);
        independentStack.Add(original);
        Check(ReferenceEquals(independentStack.Windows.Single(), original), "Unrelated window stacks are untouched");
        var custom = new CustomDialog(mod);
        Find.WindowStack.Add(custom);
        Check(Find.WindowStack.Windows.Contains(custom), "Custom dialog subclasses remain untouched");
        IrisMenusMod.Instance.Settings.EnabledAdapters.Clear();
        Find.WindowStack.Windows.Clear();
        original = new RimWorld.Dialog_ModSettings(mod);
        Find.WindowStack.Add(original);
        Check(ReferenceEquals(Find.WindowStack.Windows.Single(), original), "Unselect restores original entry");
        var settings = IrisMenusMod.Instance.Settings;
        settings.EnabledAdapters.Add("unloaded.mod:Settings");
        settings.HostAllModSettings = true;
        Find.WindowStack.Windows.Clear();
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(mod));
        Check(Find.WindowStack.WindowOfType<MenuWindow>()?.Page.Owner == mod,
            "Global hosting routes a mod without adding a manual entry");
        Check(settings.EnabledAdapters.SequenceEqual(new[] { "unloaded.mod:Settings" }),
            "Global hosting never populates or rewrites manual selections");
        MenuSettings persisted = LoadSettings(SaveSettings(settings));
        Check(persisted.HostAllModSettings && persisted.EnabledAdapters.Single() == "unloaded.mod:Settings",
            "Global hosting persists independently of unloaded manual selections");
        settings.HostAllModSettings = false;
        Find.WindowStack.Windows.Clear();
        original = new RimWorld.Dialog_ModSettings(mod);
        Find.WindowStack.Add(original);
        Check(Find.WindowStack.Windows.Single() == original && settings.EnabledAdapters.Single() == "unloaded.mod:Settings",
            "Disabling global hosting restores original routing without losing manual selections");
        settings.EnabledAdapters.Add(MenuRegistry.OwnerId(mod));
        Check(MenuRegistry.ResolveEntry(mod)?.Owner == mod, "Manual selections still route with global hosting disabled");
        settings.EnabledAdapters.Clear();
        Find.WindowStack.Windows.Clear();
        original = new RimWorld.Dialog_ModSettings(IrisMenusMod.Instance);
        Find.WindowStack.Add(original);
        Check(ReferenceEquals(Find.WindowStack.Windows.Single(), original), "Own settings entry does not recurse");
        Find.WindowStack.Windows.Clear();
        var native = MenuRegistry.Registered.First();
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(native.Owner));
        Check(Find.WindowStack.WindowOfType<MenuWindow>()?.Page.Id == native.Id, "Registered owner routes to its native page");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        assertions++;
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { assertions++; return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private sealed class OtherMod : Mod { }
    private sealed class CustomDialog : RimWorld.Dialog_ModSettings { public CustomDialog(Mod mod) : base(mod) { } }
    private sealed class NoSettings : Mod { public override string SettingsCategory() => ""; }
    private sealed class BadCategory : Mod { public override string SettingsCategory() => throw new Exception("category"); }
}
