using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_SoupKitchenTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.soup.kitchen.tests");
        readonly ThingDef previousMeal = ThingDefOf.MealSurvivalPack;
        readonly LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        readonly LoadedLanguage previousDefaultLanguage = LanguageDatabase.defaultLanguage;
        readonly Dictionary<Thing, float> prices = new Dictionary<Thing, float>();
        static RHAH_SoupKitchenTests active;
        int goodwillRequested;

        public RHAH_SoupKitchenTests()
        {
            active = this;
            Find.Maps.Clear();
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            LoadedLanguage language = (LoadedLanguage)RuntimeHelpers.GetUninitializedObject(typeof(LoadedLanguage));
            AccessTools.Field(typeof(LoadedLanguage), "dataIsLoaded").SetValue(language, true);
            AccessTools.Field(typeof(LoadedLanguage), "workerInt").SetValue(language, new LanguageWorker_English());
            language.info = new LanguageInfo { languageWorkerClass = typeof(LanguageWorker_English) };
            language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>();
            LanguageDatabase.activeLanguage = language;
            LanguageDatabase.defaultLanguage = language;
            ThingDefOf.MealSurvivalPack = new ThingDef
            {
                defName = "MealSurvivalPack", label = "packaged survival meal", category = ThingCategory.Item,
                thingClass = typeof(Thing), stackLimit = 10, useHitPoints = false
            };
            harmony.Patch(AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue),
                new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) }),
                prefix: new HarmonyMethod(typeof(RHAH_SoupKitchenTests), nameof(Price)));
            harmony.Patch(AccessTools.Method(typeof(WorldObject), nameof(WorldObject.Destroy)),
                prefix: new HarmonyMethod(typeof(RHAH_SoupKitchenTests), nameof(DestroySite)));
            harmony.Patch(AccessTools.Method(typeof(Faction), nameof(Faction.TryAffectGoodwillWith)),
                prefix: new HarmonyMethod(typeof(RHAH_SoupKitchenTests), nameof(ChangeGoodwill)));
            foreach (System.Reflection.MethodInfo method in typeof(LetterStack).GetMethods())
            {
                System.Reflection.ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == nameof(LetterStack.ReceiveLetter) && parameters.Length >= 3 &&
                    parameters[0].ParameterType == typeof(TaggedString) && parameters[1].ParameterType == typeof(TaggedString))
                    harmony.Patch(method, prefix: new HarmonyMethod(typeof(RHAH_SoupKitchenTests), nameof(SuppressLetter)));
            }
            Current.Game.letterStack = new LetterStack();
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            ThingDefOf.MealSurvivalPack = previousMeal;
            LanguageDatabase.activeLanguage = previousLanguage;
            LanguageDatabase.defaultLanguage = previousDefaultLanguage;
            active = null;
            fixture.Dispose();
        }

        [Theory]
        [InlineData(4999f, 0)]
        [InlineData(5000f, 0)]
        [InlineData(5000.5f, 5)]
        [InlineData(5999f, 5)]
        [InlineData(6000f, 6)]
        [InlineData(12500f, 12)]
        public void GoodwillUsesStrictThresholdAndFullThousands(float wealth, int loss)
        {
            Assert.Equal(loss, RHAH_SoupKitchen.GoodwillLoss(wealth));
        }

        [Fact]
        public void FactionPoolExcludesPlayerHiddenDefeatedAndHostileFactions()
        {
            Assert.False(RHAH_SoupKitchen.EligibleFaction(RimWorld.Faction.OfPlayer));
            Faction faction = FriendlyFaction();
            Assert.True(RHAH_SoupKitchen.EligibleFaction(faction));
            faction.def.hidden = true;
            Assert.False(RHAH_SoupKitchen.EligibleFaction(faction));
            faction.def.hidden = false;
            faction.defeated = true;
            Assert.False(RHAH_SoupKitchen.EligibleFaction(faction));
            faction.defeated = false;
            faction.RelationWith(RimWorld.Faction.OfPlayer).kind = FactionRelationKind.Hostile;
            Assert.False(RHAH_SoupKitchen.EligibleFaction(faction));
        }

        [Fact]
        public void WealthCountsPeopleAnimalsPrisonersClothingEquipmentAndCargoAtFullValue()
        {
            RimWorld.Planet.Caravan caravan = Caravan();
            Verse.Pawn colonist = Carrier(2000f);
            colonist.SetFactionDirect(RimWorld.Faction.OfPlayer);
            colonist.apparel = new Pawn_ApparelTracker(colonist);
            Apparel coat = new Apparel { def = new ThingDef { category = ThingCategory.Item } };
            prices[coat] = 100f;
            colonist.apparel.WornApparel.Add(coat);
            colonist.equipment = new Pawn_EquipmentTracker(colonist);
            ThingWithComps weapon = new ThingWithComps { def = new ThingDef { category = ThingCategory.Item } };
            prices[weapon] = 200f;
            colonist.equipment.AllEquipmentListForReading.Add(weapon);
            Thing cargo = ThingMaker.MakeThing(ThingDefOf.MealSurvivalPack);
            cargo.stackCount = 10;
            prices[cargo] = 25f;
            colonist.inventory.innerContainer.TryAdd(cargo);
            caravan.PawnsListForReading.Add(colonist);
            caravan.PawnsListForReading.Add(Carrier(500f));
            caravan.PawnsListForReading.Add(Carrier(1200f));
            Assert.Equal(4250f, RHAH_SoupKitchen.Wealth(caravan));
        }

        [Theory]
        [InlineData(5)]
        [InlineData(20)]
        public void SuccessfulVisitDeliversRealMealsAndCannotBeRepeated(int meals)
        {
            RimWorld.Planet.Caravan caravan = Caravan();
            Verse.Pawn carrier = Carrier(5000f);
            caravan.PawnsListForReading.Add(carrier);
            WorldObject_RHAH_SoupKitchen site = Site(meals);
            Assert.True(site.Visit(caravan));
            Assert.True(site.Destroyed);
            Assert.Equal(meals, carrier.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.MealSurvivalPack));
            Assert.All(carrier.inventory.innerContainer, item => Assert.InRange(item.stackCount, 1, 10));
            Assert.False(site.Visit(caravan));
            Assert.Equal(meals, carrier.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.MealSurvivalPack));
            Assert.Equal(0, goodwillRequested);
        }

        [Fact]
        public void WealthyCaravanGetsNoFoodAndLosesScaledGoodwill()
        {
            RimWorld.Planet.Caravan caravan = Caravan();
            Verse.Pawn carrier = Carrier(7000f);
            caravan.PawnsListForReading.Add(carrier);
            WorldObject_RHAH_SoupKitchen site = Site(20);
            Assert.False(site.Visit(caravan));
            Assert.Empty(carrier.inventory.innerContainer);
            Assert.Equal(-7, goodwillRequested);
            Assert.False(site.Destroyed);
        }

        [Fact]
        public void RewardCountSurvivesSaveLoadAndMissingSiteInvalidatesArrival()
        {
            WorldObjectDef definition = new WorldObjectDef { defName = "RHAH_SoupKitchen", comps = new List<WorldObjectCompProperties>() };
            List<WorldObjectDef> previous = new List<WorldObjectDef>(DefDatabase<WorldObjectDef>.AllDefsListForReading);
            DefDatabase<WorldObjectDef>.Add(definition);
            WorldObject_RHAH_SoupKitchen site = Site(17);
            site.def = definition;
            ScribeSaver previousSaver = Scribe.saver;
            ScribeLoader previousLoader = Scribe.loader;
            LoadSaveMode previousMode = Scribe.mode;
            bool previousProfiling = DeepProfiler.enabled;
            System.Reflection.FieldInfo prefs = AccessTools.Field(typeof(Prefs), "data");
            object previousPrefs = prefs.GetValue(null);
            string path = Path.GetTempFileName();
            try
            {
                Scribe.saver = new ScribeSaver();
                Scribe.loader = new ScribeLoader();
                Scribe.mode = LoadSaveMode.Inactive;
                DeepProfiler.enabled = false;
                prefs.SetValue(null, new PrefsData());
                Scribe.saver.InitSaving(path, "site");
                site.ExposeData();
                Scribe.saver.FinalizeSaving();
                WorldObject_RHAH_SoupKitchen loaded = new WorldObject_RHAH_SoupKitchen { def = definition };
                Scribe.loader.InitLoading(path);
                Scribe.loader.curParent = loaded;
                loaded.ExposeData();
                Scribe.loader.crossRefs.RegisterForCrossRefResolve(loaded);
                Scribe.loader.initer.RegisterForPostLoadInit(loaded);
                Scribe.loader.FinalizeLoading();
                Assert.Equal(17, AccessTools.Field(typeof(WorldObject_RHAH_SoupKitchen), "mealCount").GetValue(loaded));
            }
            finally
            {
                Scribe.ForceStop();
                Scribe.saver = previousSaver;
                Scribe.loader = previousLoader;
                Scribe.mode = previousMode;
                DeepProfiler.enabled = previousProfiling;
                prefs.SetValue(null, previousPrefs);
                DefDatabase<WorldObjectDef>.Clear();
                foreach (WorldObjectDef def in previous) DefDatabase<WorldObjectDef>.Add(def);
                File.Delete(path);
            }
            RimWorld.Planet.Caravan caravan = Caravan();
            caravan.PawnsListForReading.Add(Carrier(100f));
            CaravanArrivalAction_RHAH_SoupKitchen action = new CaravanArrivalAction_RHAH_SoupKitchen(site);
            Assert.True(action.StillValid(caravan, site.Tile).Accepted);
            site.Destroy();
            Assert.False(action.StillValid(caravan, site.Tile).Accepted);
            Assert.False(new CaravanArrivalAction_RHAH_SoupKitchen().StillValid(caravan, caravan.Tile).Accepted);
        }

        [Fact]
        public void SoupKitchenParticipatesInAllAgesPositivePool()
        {
            RHAH_IncidentEntry entry = RHAH_IncidentCatalog.GetByDisplayId("I-058");
            Assert.Equal(RHAH_ContentRating.AllAges, entry.ContentRating);
            Assert.Equal(RHAH_AttitudePool.Positive, RHAH_IncidentGroups.PoolOf(entry.DisplayId));
            Assert.False(RHAH_ApproachRules.UsesApproach(entry.Target, entry.DisplayId));
            RHAH_ContentPreset.Apply(RHAH_Mod.Settings, RHAH_ContentRating.AllAges);
            Assert.True(RHAH_Mod.Settings.IsIncidentEnabled(entry.DisplayId));
        }

        Faction FriendlyFaction()
        {
            Faction faction = new Faction { def = new FactionDef(), loadID = 80, Name = "Host" };
            faction.SetRelation(new FactionRelation { other = RimWorld.Faction.OfPlayer, kind = FactionRelationKind.Neutral });
            Find.FactionManager.AllFactionsListForReading.Add(faction);
            return faction;
        }

        Verse.Pawn Carrier(float value)
        {
            Verse.Pawn pawn = new Verse.Pawn
            {
                def = new ThingDef { category = ThingCategory.Pawn, race = new RaceProperties() }
            };
            pawn.inventory = new Pawn_InventoryTracker(pawn);
            prices[pawn] = value;
            return pawn;
        }

        static RimWorld.Planet.Caravan Caravan()
        {
            RimWorld.Planet.Caravan caravan = new RimWorld.Planet.Caravan { ID = 101, def = new WorldObjectDef { canHaveFaction = true } };
            caravan.SetFaction(RimWorld.Faction.OfPlayer);
            Objects().Add(caravan);
            return caravan;
        }

        WorldObject_RHAH_SoupKitchen Site(int meals)
        {
            WorldObject_RHAH_SoupKitchen site = new WorldObject_RHAH_SoupKitchen { ID = 102, def = new WorldObjectDef { canHaveFaction = true } };
            site.SetFaction(FriendlyFaction());
            site.Configure(meals);
            Objects().Add(site);
            return site;
        }

        static HashSet<WorldObject> Objects() => (HashSet<WorldObject>)AccessTools.Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(Find.WorldObjects);
        static bool Price(Thing __0, ref float __result) { active.prices.TryGetValue(__0, out __result); return false; }
        static bool SuppressLetter() => false;
        static bool ChangeGoodwill(int goodwillChange, ref bool __result) { active.goodwillRequested += goodwillChange; __result = true; return false; }
        static bool DestroySite(WorldObject __instance)
        {
            if (!(__instance is WorldObject_RHAH_SoupKitchen)) return true;
            Objects().Remove(__instance);
            AccessTools.Field(typeof(WorldObject), "destroyed").SetValue(__instance, true);
            return false;
        }
    }
}
