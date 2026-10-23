using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_DemandFoodTests
    {
        [Fact]
        public void SourceDefaultsAndExplicitChoicesSurviveSaveLoad()
        {
            ThingDef core = RHAH_ArmedFoodPaymentTests.Meal("TestCore", 0.9f, "ludeon.rimworld");
            ThingDef ratkin = RHAH_ArmedFoodPaymentTests.Meal("TestRatkin", 0.2f, "Solaris.RatkinRaceMod");
            ThingDef mod = RHAH_ArmedFoodPaymentTests.Meal("TestMod", 1.8f, "example.food");
            ThingDef laterMod = RHAH_ArmedFoodPaymentTests.Meal("LaterMod", 0.9f, "example.later");
            ThingDef laterCore = RHAH_ArmedFoodPaymentTests.Meal("LaterCore", 0.9f, "ludeon.rimworld");
            WithScribe(path =>
            {
                File.WriteAllText(path, "<settings />");
                RHAH_Settings settings = Load<RHAH_Settings>(path);
                Assert.True(settings.IsDemandFoodEnabled(core));
                Assert.True(settings.IsDemandFoodEnabled(ratkin));
                Assert.False(settings.IsDemandFoodEnabled(mod));
                settings.SetDemandFoodEnabled(core.defName, false);
                settings.SetDemandFoodEnabled(mod.defName, true);
                Save(path, settings);
                settings = Load<RHAH_Settings>(path);
                Assert.False(settings.IsDemandFoodEnabled(core));
                Assert.True(settings.IsDemandFoodEnabled(mod));
                Assert.True(settings.IsDemandFoodEnabled(laterCore));
                Assert.False(settings.IsDemandFoodEnabled(laterMod));
                settings.SetAllDemandFood(false, new List<ThingDef> { ratkin });
                Assert.False(settings.IsDemandFoodEnabled(ratkin));
                Assert.True(settings.IsDemandFoodEnabled(mod));
                settings.SetAllDemandFood(true, new List<ThingDef> { core, ratkin, mod });
                Assert.True(settings.IsDemandFoodEnabled(core));
                Assert.False(settings.IsDemandFoodEnabled(laterMod));
                settings.ResetDemandFoods();
                Assert.True(settings.IsDemandFoodEnabled(core));
                Assert.True(settings.IsDemandFoodEnabled(ratkin));
                Assert.False(settings.IsDemandFoodEnabled(mod));
                Assert.True(settings.IsGiveFoodEnabled(core.defName));
                Assert.True(settings.IsBegFoodEnabled(mod.defName));
            });
        }

        [Theory]
        [InlineData(0.05, 180)]
        [InlineData(0.2, 45)]
        [InlineData(1.8, 5)]
        [InlineData(0.7, 13)]
        public void WholeFoodUnitsCoverNutritionWithoutAnExtraExactBoundaryUnit(double nutrition, int expected)
        {
            Assert.Equal(expected, RHAH_DemandFood.Units(9, nutrition));
            Assert.Equal(0, RHAH_DemandFood.Units(0, nutrition));
        }

        [Fact]
        public void LegacyMealCreditAndFractionalNutritionSurviveSaveLoad()
        {
            WithScribe(path =>
            {
                File.WriteAllText(path, "<record><amount>10</amount><armedFoodDelivered>4</armedFoodDelivered></record>");
                RHAH_ChoiceRecord record = Load<RHAH_ChoiceRecord>(path);
                Assert.Equal(5.4, RHAH_DemandFood.Remaining(record), 5);
                record.ArmedFoodNutritionDelivered = 2.15;
                Save(path, record);
                record = Load<RHAH_ChoiceRecord>(path);
                Assert.Equal(3.25, RHAH_DemandFood.Remaining(record), 5);
                Assert.Equal(65, RHAH_DemandFood.Units(RHAH_DemandFood.Remaining(record), 0.05));
            });
        }

        [Fact]
        public void NonFoodsAndDrugsStayIneligibleEvenWhenExplicitlyEnabled()
        {
            RHAH_Settings settings = new RHAH_Settings();
            ThingDef raw = RHAH_ArmedFoodPaymentTests.Meal("TestRaw", 0.05f, "ludeon.rimworld");
            raw.ingestible.preferability = FoodPreferability.RawTasty;
            ThingDef drug = RHAH_ArmedFoodPaymentTests.Meal("TestDrug", 0.9f, "ludeon.rimworld");
            drug.ingestible.drugCategory = DrugCategory.Social;
            settings.SetDemandFoodEnabled(raw.defName, true);
            settings.SetDemandFoodEnabled(drug.defName, true);
            Assert.False(settings.IsDemandFoodEnabled(raw));
            Assert.False(settings.IsDemandFoodEnabled(drug));
        }

        static void Save(string path, IExposable value)
        {
            Scribe.saver.InitSaving(path, "record");
            value.ExposeData();
            Scribe.saver.FinalizeSaving();
        }

        static T Load<T>(string path) where T : IExposable, new()
        {
            Scribe.loader.InitLoading(path);
            T value = new T();
            Scribe.loader.curParent = value;
            value.ExposeData();
            Scribe.loader.initer.RegisterForPostLoadInit(value);
            Scribe.loader.FinalizeLoading();
            return value;
        }

        static void WithScribe(Action<string> action)
        {
            ScribeSaver previousSaver = Scribe.saver;
            ScribeLoader previousLoader = Scribe.loader;
            LoadSaveMode previousMode = Scribe.mode;
            bool previousProfiling = DeepProfiler.enabled;
            FieldInfo prefs = typeof(Prefs).GetField("data", BindingFlags.NonPublic | BindingFlags.Static);
            object previousPrefs = prefs.GetValue(null);
            string path = Path.GetTempFileName();
            try
            {
                Scribe.saver = new ScribeSaver();
                Scribe.loader = new ScribeLoader();
                Scribe.mode = LoadSaveMode.Inactive;
                DeepProfiler.enabled = false;
                prefs.SetValue(null, new PrefsData { devMode = false });
                action(path);
            }
            finally
            {
                Scribe.ForceStop();
                Scribe.saver = previousSaver;
                Scribe.loader = previousLoader;
                Scribe.mode = previousMode;
                DeepProfiler.enabled = previousProfiling;
                prefs.SetValue(null, previousPrefs);
                File.Delete(path);
            }
        }
    }
}
