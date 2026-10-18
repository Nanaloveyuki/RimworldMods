$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')
$entry = Get-Content (Join-Path $root '1.6/Source/ModEntry.cs') -Raw
$draw = Get-CSharpMethod $entry 'DrawSettings'
$draw += Get-CSharpMethod $entry 'DrawDeveloperSettings'
$bridge = Get-Content (Join-Path $root '1.6/Source/ModEntry.IrisMenus.cs') -Raw
$stub = @'
namespace Verse {
    public class Mod { }
    public class Listing_Standard {
        public static bool ClickButtons;
        public static System.Collections.Generic.List<string> Buttons = new System.Collections.Generic.List<string>();
        public void Label(string text) { }
        public bool ButtonText(string text) { Buttons.Add(text); return ClickButtons; }
    }
    public static class Prefs { public static bool DevMode; }
    public enum ProgramState { Entry, Playing }
    public static class Current { public static object Game; public static ProgramState ProgramState; }
    public static class ModsConfig {
        public static bool Active;
        public static bool IsActive(string id) { return Active && id == "Nanaloveyuki.IrisMenus"; }
    }
    public static class GenTypes {
        public static System.Type Registry;
        public static int Lookups;
        public static System.Type GetTypeInAnyAssembly(string name) {
            Lookups++;
            if (name != "IrisMenus.MenuRegistry") throw new System.Exception(name);
            return Registry;
        }
    }
    public static class Log {
        public static int Warnings, Errors;
        public static void Warning(string text) { Warnings++; }
        public static void Error(string text) { Errors++; }
    }
    public static class Translator { public static string Translate(this string text) { return text; } }
}
namespace IrisMenus {
    public static class MenuRegistry {
        public static readonly System.Collections.Generic.List<string> Ids = new System.Collections.Generic.List<string>();
        public static bool Fail;
        public static void RegisterSubItemListing(Verse.Mod owner, string id, System.Func<string> title,
            System.Action<Verse.Listing_Standard> draw, System.Action save) {
            if (Fail) throw new System.InvalidOperationException("test registration failure");
            if (Ids.Contains(id)) throw new System.Exception("duplicate page");
            if (title() != "MouseDisaster_IrisMenus_" + id.Substring(9)) throw new System.Exception("title mismatch");
            Ids.Add(id);
            draw(new Verse.Listing_Standard());
            save();
        }
    }
}
namespace MouseDisaster {
    public class Entry { public string DefName, DisplayLabel; public bool IsOriginal; }
    public static class MouseDisasterIncidentCatalog {
        public static Entry[] AllEntries = { new Entry { DefName="Original", DisplayLabel="Original title", IsOriginal=true }, new Entry { DefName="Continued", DisplayLabel="Continued title" } };
    }
    public static class GameComponent_MouseDisasterNarrative {
        public static string[] NarrativeDebugIds = { "N001", "Check", "Inspect" };
        public static System.Collections.Generic.List<string> Actions = new System.Collections.Generic.List<string>();
        public static void RunNarrativeDebug(string id) { Actions.Add(id); }
    }
    public partial class MouseDisasterMod : Verse.Mod {
        private class FakeSettings {
            public bool enablePrisonerScavengeDebugLog = false;
            public bool enableDetailedTraceLog = false;
            public bool forceDismissMobileNonInfantEventPawns = false;
            public void ClampValues() { }
        }
        private static FakeSettings Settings = new FakeSettings();
        private static readonly System.Collections.Generic.List<string> Calls = new System.Collections.Generic.List<string>();
        private static int Saves;
        private static void SaveSettings() { Saves++; }
        private static void DrawSafetySettings(Verse.Listing_Standard list) { Calls.Add("Safety"); }
        private static void DrawGeneralSettings(Verse.Listing_Standard list) { Calls.Add("General"); }
        private static void DrawEnvironmentSettings(Verse.Listing_Standard list) { Calls.Add("Environment"); }
        private static void DrawPawnSettings(Verse.Listing_Standard list) { Calls.Add("Pawns"); }
        private static void DrawEventFoodWeightSettings(Verse.Listing_Standard list) { Calls.Add("FoodWeights"); }
        private static void DrawPawnHistorySettings(Verse.Listing_Standard list) { Calls.Add("PawnHistory"); }
        private static void DrawPawnTraitSettings(Verse.Listing_Standard list) { Calls.Add("PawnTrait"); }
        private static void DrawPredationSettings(Verse.Listing_Standard list) { Calls.Add("Predation"); }
        private static void DrawIncidentSection(Verse.Listing_Standard list, bool? original = null) { Calls.Add("Incidents:" + original); }
        private static void DrawNarrativeControls(Verse.Listing_Standard list, bool hosted = false) { Calls.Add("Narrative:" + hosted); }
        private static void DrawEndingSettings(Verse.Listing_Standard list, bool hosted) { Calls.Add("Endings:" + hosted); }
        private static void DrawBiologySettings(Verse.Listing_Standard list, bool legacy) { Calls.Add("Biology:" + legacy); }
        private static void DrawSettingsFooter(Verse.Listing_Standard list) { Calls.Add("Footer"); }
        private static void DrawSectionTitle(Verse.Listing_Standard list, string key) { Calls.Add(key); }
        private static void DrawPercentSlider(Verse.Listing_Standard list, string key, ref float value, float min, float max) { Calls.Add(key); }
        private static void DrawCheckbox(Verse.Listing_Standard list, string key, ref bool value, string tip) { Calls.Add(key); }
        private static void Check(bool value, string message) { if (!value) throw new System.Exception(message); }
        public static void Verify() {
            var mod = new MouseDisasterMod();
            mod.RegisterIrisMenus();
            Check(Verse.GenTypes.Lookups == 0, "inactive provider must not be resolved");
            Verse.ModsConfig.Active = true;
            mod.RegisterIrisMenus();
            Check(Verse.Log.Warnings == 1, "missing provider warning");
            Verse.GenTypes.Registry = typeof(string);
            mod.RegisterIrisMenus();
            Check(Verse.Log.Warnings == 2, "incompatible API warning");
            Verse.GenTypes.Registry = typeof(IrisMenus.MenuRegistry);
            mod.RegisterIrisMenus();
            Check(string.Join(",", IrisMenus.MenuRegistry.Ids) == "settings.Safety,settings.General,settings.Environment,settings.PawnBehavior,settings.FoodWeights,settings.PawnHistory,settings.PawnTrait,settings.Predation,settings.OriginalEvents,settings.ContinuedEvents,settings.Endings,settings.Developer", "twelve settings pages");
            Check(Saves == 12, "save callbacks");
            Check(string.Join(",", Calls) == "Safety,Footer,General,Environment,Pawns,FoodWeights,PawnHistory,PawnTrait,Predation,Incidents:True,Incidents:False,Narrative:True,Endings:True,MouseDisaster_IrisMenus_Developer", "hosted category dispatch");
            Calls.Clear();
            DrawSettings(new Verse.Listing_Standard());
            Check(string.Join(",", Calls) == "Safety,General,Environment,Pawns,FoodWeights,PawnHistory,PawnTrait,Predation,Incidents:,Narrative:False,Endings:False,Footer", "legacy drawing order");
            Check(Verse.Listing_Standard.Buttons.Count == 0, "developer actions shown without dev mode");
            Verse.Prefs.DevMode=true;
            DrawDeveloperSettings(new Verse.Listing_Standard());
            Check(Verse.Listing_Standard.Buttons.Count == 0, "developer actions shown before loading a game");
            Verse.Current.Game=new object(); Verse.Current.ProgramState=Verse.ProgramState.Playing;
            Verse.Listing_Standard.ClickButtons=true;
            DrawDeveloperSettings(new Verse.Listing_Standard());
            Check(string.Join(",", GameComponent_MouseDisasterNarrative.Actions)=="Original,Continued,N001,Check,Inspect", "flat buttons failed to execute all actions");
            IrisMenus.MenuRegistry.Fail = true;
            mod.RegisterIrisMenus();
            Check(Verse.Log.Errors == 1, "registration failure must be reported");
        }
__DRAW__
    }
}
'@
Add-Type -TypeDefinition ($bridge + "`n" + $stub.Replace('__DRAW__', $draw))
[MouseDisaster.MouseDisasterMod]::Verify()
foreach ($language in @('ChineseSimplified', 'English')) {
    [xml]$xml = Get-Content (Join-Path $root "Languages/$language/Keyed/MouseDisasterIrisMenus.xml") -Raw
    foreach ($page in @('Safety', 'OriginalEvents', 'ContinuedEvents', 'Endings', 'General', 'Environment', 'PawnBehavior', 'FoodWeights', 'PawnHistory', 'PawnTrait', 'Predation', 'Developer')) {
        if (!$xml.LanguageData.("MouseDisaster_IrisMenus_" + $page)) { throw "Missing title: $language/$page" }
    }
}
Write-Host 'IrisMenus integration checks passed (engine doubles, category dispatch, legacy order, localization).'
