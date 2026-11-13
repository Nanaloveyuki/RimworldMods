#if RHAH_IRISMENUS
extern alias iris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn.Compat;
using iris::IrisMenus;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_IrisMenusTests : IDisposable
    {
        const string DeveloperOnly = "Developer mode is required.";
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
        readonly object previousReset = Field(typeof(RHAH_Mod), "SettingsReset").GetValue(null);
        readonly List<ThingDef> previousThings = new List<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading);
        readonly Harmony harmony = new Harmony("rhah.iris.menus.tests");
        readonly RHAH_IrisMenusPages pages;
        readonly GameComponent_RHAH_Game game;
        static Dictionary<string, RegisteredPage> registered;
        static int catalogDraws, quotes, schedulerCalls;

        sealed class RegisteredPage
        {
            internal Action<Listing_Standard> Draw;
            internal Func<IEnumerable<MenuSearchEntry>> Search;
            internal Action<string> BeforeFocus;
        }

        public RHAH_IrisMenusTests()
        {
            try
            {
                Current.Game = Empty<Game>();
                Current.Game.components = new List<GameComponent>();
                game = new GameComponent_RHAH_Game(Current.Game);
                Current.Game.components.Add(game);
                Current.ProgramState = ProgramState.Entry;
                RHAH_Mod.Settings = new RHAH_Settings();
                Field(typeof(Prefs), "data").SetValue(null, new PrefsData { devMode = false });
                LoadedLanguage language = Empty<LoadedLanguage>();
                Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
                Field(typeof(LoadedLanguage), "workerInt").SetValue(language, new LanguageWorker_English());
                language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
                language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
                AddText(language, "RHAH_Menu_DevEvents_RequiresDevMode", DeveloperOnly);
                AddText(language, "RHAH_Menu_DevEvents_Note", "Event catalog");
                AddText(language, "RHAH_Menu_Format_IdentifiedLabel", "{0}: {1}");
                foreach (RHAH_IncidentEntry entry in RHAH_IncidentCatalog.All)
                    AddText(language, entry.LabelKey, entry.DisplayId);
                LanguageDatabase.activeLanguage = language;
                DefDatabase<ThingDef>.Clear();
                ThingDef food = new ThingDef
                {
                    defName = "RHAH_TestFood", label = "zesty meal",
                    ingestible = new IngestibleProperties { preferability = FoodPreferability.MealSimple },
                    modContentPack = Empty<ModContentPack>(),
                    thingCategories = new List<ThingCategoryDef>
                    {
                        new ThingCategoryDef { defName = "TestMeals", parent = new ThingCategoryDef { defName = "Foods" } }
                    }
                };
                Field(typeof(IngestibleProperties), "cachedNutrition").SetValue(food.ingestible, 0.9f);
                Field(typeof(ModContentPack), "nameInt").SetValue(food.modContentPack, "Test kitchen");
                DefDatabase<ThingDef>.Add(food);
                registered = new Dictionary<string, RegisteredPage>();
                catalogDraws = quotes = schedulerCalls = 0;
                Patch(typeof(RHAH_IrisMenusPages), "RegisterPage", nameof(CapturePage));
                Patch(typeof(RHAH_IrisMenusPages), "DrawDevEvents", nameof(CaptureCatalogDraw));
                Patch(typeof(RHAH_IrisMenusWidgets), "Quote", nameof(SkipQuote));
                Patch(typeof(RHAH_Scheduler), "QueueDebugIncident", nameof(RejectScheduler));
                Patch(typeof(RHAH_Scheduler), "SpawnDebugIncidentOnMap", nameof(RejectScheduler));
                pages = new RHAH_IrisMenusPages("test");
                pages.Register(Empty<RHAH_Mod>());
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                harmony.UnpatchAll(harmony.Id);
            }
            finally
            {
                registered = null;
                catalogDraws = quotes = schedulerCalls = 0;
                Field(typeof(RHAH_Mod), "SettingsReset").SetValue(null, previousReset);
                Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
                LanguageDatabase.activeLanguage = previousLanguage;
                RHAH_Mod.Settings = previousSettings;
                Current.Game = previousGame;
                Current.ProgramState = previousState;
                DefDatabase<ThingDef>.Clear();
                foreach (ThingDef thing in previousThings) DefDatabase<ThingDef>.Add(thing);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EventPageDrawsAndSearchesTheCompleteCatalogInEitherMode(bool developerMode)
        {
            ((PrefsData)Field(typeof(Prefs), "data").GetValue(null)).devMode = developerMode;

            registered["dev-events"].Draw(null);
            List<MenuSearchEntry> entries = registered["dev-events"].Search().ToList();

            Assert.Equal(1, catalogDraws);
            Assert.Equal(developerMode ? 0 : 1, quotes);
            Assert.Equal(60, entries.Count);
            Assert.Equal(RHAH_IncidentCatalog.All.Select(entry => "dev-" + entry.DisplayId), entries.Select(entry => entry.Id));
            foreach (RHAH_IncidentEntry incident in RHAH_IncidentCatalog.All)
            {
                MenuSearchEntry entry = Assert.Single(entries, item => item.Id == "dev-" + incident.DisplayId);
                Assert.Contains(incident.DisplayId, entry.Title());
                Assert.Contains(incident.DefName, entry.Keywords());
            }
        }

        [Fact]
        public void RestoredSubpagesHaveTheirOwnSearchRoutes()
        {
            string[] expected = { "overview", "relief", "relief-foods", "give-foods", "beg-foods", "visitors", "stay", "plague",
                "events", "pawns", "suiyin-trust", "settlement-progress", "environment", "compat-diagnostics", "genes",
                "dev-events", "event-frequency", "pawn-history", "experimental", "arrival-options", "removal" };
            Assert.Equal(expected.OrderBy(id => id), registered.Keys.OrderBy(id => id));
            Assert.Equal(new[] { "shelter-days", "short-stay-lock", "hire-days" }, SearchIds("stay"));
            Assert.Contains("plague-enabled", SearchIds("plague"));
            Assert.Contains("pawns-list", SearchIds("pawns"));
            Assert.DoesNotContain("pawns-list", SearchIds("compat-diagnostics"));
            Assert.Equal(new[] { "optimize-generation", "stagger-generation" }, SearchIds("experimental"));
            Assert.Contains("setting-aidRequestsEnabled", SearchIds("arrival-options"));
            Assert.DoesNotContain("optimize-generation", SearchIds("arrival-options"));
            Assert.Contains("relief-enabled", SearchIds("relief"));
            Assert.DoesNotContain("relief-foods", SearchIds("relief"));
            foreach (string page in new[] { "relief-foods", "give-foods", "beg-foods" })
            {
                Assert.Contains(page, SearchIds(page));
                Assert.NotNull(registered[page].BeforeFocus);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EveryNonDeveloperTriggerStopsBeforeSchedulingOrContextChecks(bool gameLoaded)
        {
            Assert.True(game.QueueIncident("retained", 80f, 9));
            if (!gameLoaded) Current.Game = null;

            foreach (RHAH_IncidentEntry entry in RHAH_IncidentCatalog.All)
            {
                Assert.Equal(DeveloperOnly, RHAH_IrisMenusPages.Queue(entry));
                Assert.Equal(DeveloperOnly, RHAH_IrisMenusPages.SpawnInstant(entry));
            }
            Assert.Equal(DeveloperOnly, RHAH_IrisMenusPages.Queue(null));
            Assert.Equal(DeveloperOnly, RHAH_IrisMenusPages.SpawnInstant(null));
            Assert.Equal(0, schedulerCalls);
            Assert.Equal(new[] { "retained" }, game.PendingIncidentDisplayIds);
            Assert.Empty(game.ActiveGenerationBatches);
        }

        [Fact]
        public void ReadingFoodSearchMetadataDoesNotChangeFiltersOrPendingFocus()
        {
            SetFoodNavigation();

            foreach (string page in new[] { "relief-foods", "give-foods", "beg-foods" })
            {
                MenuSearchEntry food = Assert.Single(registered[page].Search(), entry => entry.Id.EndsWith("RHAH_TestFood", StringComparison.Ordinal));
                Assert.False(string.IsNullOrEmpty(food.Title()));
                Assert.Contains("RHAH_TestFood", food.Keywords());
                Assert.Equal("Test kitchen", food.Context());
            }

            AssertFoodNavigation("filter", "pending");
        }

        [Theory]
        [InlineData("relief-foods", "relief-food-", "foodQuery", "pendingFoodMod", 0, "Test kitchen")]
        [InlineData("give-foods", "give-food-", "giveFoodQuery", "pendingGiveFoodGroup", 1, "Z")]
        [InlineData("beg-foods", "beg-food-", "begFoodQuery", "pendingBegFoodGroup", 2, "TestMeals")]
        public void FoodFocusUsesCurrentSortAndChangesOnlyItsOwnPage(string page, string prefix, string query, string pending, int mode, string group)
        {
            registered[page].Search().ToList();
            RHAH_Mod.Settings.giveFoodListMode = mode;
            RHAH_Mod.Settings.begFoodListMode = mode;
            SetFoodNavigation();

            registered[page].BeforeFocus(prefix + "RHAH_TestFood");

            foreach (string name in new[] { "foodQuery", "giveFoodQuery", "begFoodQuery" })
                Assert.Equal(name == query ? "" : "filter", Field(typeof(RHAH_IrisMenusPages), name).GetValue(pages));
            foreach (string name in new[] { "pendingFoodMod", "pendingGiveFoodGroup", "pendingBegFoodGroup" })
                Assert.Equal(name == pending ? group : "pending", Field(typeof(RHAH_IrisMenusPages), name).GetValue(pages));
            SetFoodNavigation();
            foreach (string anchor in new[] { "chapter-" + page, page, prefix + "search", prefix + "sort" })
                registered[page].BeforeFocus(anchor);
            AssertFoodNavigation("filter", "pending");
        }

        void SetFoodNavigation()
        {
            foreach (string name in new[] { "foodQuery", "giveFoodQuery", "begFoodQuery" })
                Field(typeof(RHAH_IrisMenusPages), name).SetValue(pages, "filter");
            foreach (string name in new[] { "pendingFoodMod", "pendingGiveFoodGroup", "pendingBegFoodGroup" })
                Field(typeof(RHAH_IrisMenusPages), name).SetValue(pages, "pending");
        }

        void AssertFoodNavigation(string query, string pending)
        {
            foreach (string name in new[] { "foodQuery", "giveFoodQuery", "begFoodQuery" })
                Assert.Equal(query, Field(typeof(RHAH_IrisMenusPages), name).GetValue(pages));
            foreach (string name in new[] { "pendingFoodMod", "pendingGiveFoodGroup", "pendingBegFoodGroup" })
                Assert.Equal(pending, Field(typeof(RHAH_IrisMenusPages), name).GetValue(pages));
        }

        static IEnumerable<string> SearchIds(string page) => registered[page].Search().Select(entry => entry.Id);
        static bool CapturePage(string pageId, Action<Listing_Standard> draw, Func<IEnumerable<MenuSearchEntry>> search, Action<string> beforeFocus)
        {
            registered.Add(pageId, new RegisteredPage { Draw = draw, Search = search, BeforeFocus = beforeFocus });
            return false;
        }
        static bool CaptureCatalogDraw() { catalogDraws++; return false; }
        static bool SkipQuote() { quotes++; return false; }
        static bool RejectScheduler(ref bool __result) { schedulerCalls++; __result = false; return false; }
        void Patch(Type type, string method, string prefix) =>
            harmony.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(typeof(RHAH_IrisMenusTests), prefix));
        static void AddText(LoadedLanguage language, string key, string value) =>
            language.keyedReplacements[key] = new LoadedLanguage.KeyedReplacement { key = key, value = value };
        static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        static T Empty<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
#endif
