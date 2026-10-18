using System.IO;
using System.Reflection;
using System.Xml;
using HungerAndHavoc.Api;
using Verse;
using RimWorld;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_IncidentWeightTests
    {
        [Fact]
        public void FamilyWeightsStayOrdered()
        {
            Assert.True(RHAH_IncidentWeight.FamilyBase(RHAH_IncidentFamily.Wild) >
                RHAH_IncidentWeight.FamilyBase(RHAH_IncidentFamily.Beggar));
            Assert.Equal(0.12f, RHAH_IncidentWeight.FamilyBase(RHAH_IncidentFamily.Intel));
            Assert.Equal(0.25f, RHAH_IncidentWeight.FamilyBase(RHAH_IncidentFamily.Aid));
            Assert.Equal(0.2f, RHAH_IncidentWeight.FamilyBase(RHAH_IncidentFamily.Special));
        }

        [Fact]
        public void PlagueAndWinterStackOnNegativePool()
        {
            float weight = RHAH_IncidentWeight.Evaluate(Input(
                RHAH_IncidentFamily.Wild,
                RHAH_IncidentCategory.Plague,
                RHAH_AttitudePool.Negative,
                RHAH_IncidentSeason.Winter,
                trust: -100));

            Assert.Equal(1.4f * 1.1f * 0.5f * 1.25f, weight);
        }

        [Fact]
        public void PositivePoolIgnoresTrust()
        {
            float low = RHAH_IncidentWeight.Evaluate(Input(
                RHAH_IncidentFamily.Aid,
                RHAH_IncidentCategory.Hunger,
                RHAH_AttitudePool.Positive,
                RHAH_IncidentSeason.Summer,
                trust: -100));
            float high = RHAH_IncidentWeight.Evaluate(Input(
                RHAH_IncidentFamily.Aid,
                RHAH_IncidentCategory.Hunger,
                RHAH_AttitudePool.Positive,
                RHAH_IncidentSeason.Summer,
                trust: 100));

            Assert.Equal(0.25f, low);
            Assert.Equal(low, high);
        }

        [Fact]
        public void TrustClampsAtTheNarrativeLimits()
        {
            Assert.Equal(1.25f, RHAH_IncidentWeight.TrustFactor(-400));
            Assert.Equal(0.75f, RHAH_IncidentWeight.TrustFactor(400));
            Assert.Equal(1f, RHAH_IncidentWeight.TrustFactor(0));
        }

        [Fact]
        public void TargetMismatchZeroesTheWeight()
        {
            RHAH_IncidentWeightInput caravanOnMap = new RHAH_IncidentWeightInput(
                RHAH_IncidentFamily.Thief,
                RHAH_IncidentCategory.Hunger,
                RHAH_IncidentTarget.Caravan,
                RHAH_AttitudePool.Negative,
                RHAH_IncidentSeason.Spring,
                0,
                mapHome: true,
                playerCaravan: false);
            RHAH_IncidentWeightInput mapOnCaravan = new RHAH_IncidentWeightInput(
                RHAH_IncidentFamily.Beggar,
                RHAH_IncidentCategory.Hunger,
                RHAH_IncidentTarget.Map,
                RHAH_AttitudePool.Negative,
                RHAH_IncidentSeason.Spring,
                0,
                mapHome: false,
                playerCaravan: true);

            Assert.Equal(0f, RHAH_IncidentWeight.Evaluate(caravanOnMap));
            Assert.Equal(0f, RHAH_IncidentWeight.Evaluate(mapOnCaravan));
        }

        [Fact]
        public void SelectionUsesOnlyPositiveWeights()
        {
            float[] weights = { 0f, 1.4f, 0f, 0.7f };

            Assert.Equal(1, RHAH_IncidentWeight.Select(weights, 0f));
            Assert.Equal(1, RHAH_IncidentWeight.Select(weights, 1.399f));
            Assert.Equal(3, RHAH_IncidentWeight.Select(weights, 1.4f));
            Assert.Equal(-1, RHAH_IncidentWeight.Select(weights, 2.1f));
            Assert.Equal(-1, RHAH_IncidentWeight.Select(new[] { 0f, 0f }, 0f));
        }
        [Fact]
        public void DisabledIncidentDropsOutOfTheDraw()
        {
            Assert.Equal(0f, RHAH_IncidentTuning.Scale(1.4f, false, 100f));
            Assert.Equal(0f, RHAH_IncidentTuning.Scale(1.4f, true, 0f));
        }

        [Fact]
        public void PlayerWeightScalesTheCatalogWeight()
        {
            Assert.Equal(0.7f, RHAH_IncidentTuning.Scale(1.4f, true, 50f));
            Assert.Equal(1.4f, RHAH_IncidentTuning.Scale(1.4f, true, 100f));
            Assert.Equal(0f, RHAH_IncidentTuning.Scale(0f, true, 100f));
        }

        [Fact]
        public void DebugPointsAndWeightsClampToTheirRanges()
        {
            RHAH_Settings settings = new RHAH_Settings();
            Assert.Equal(700f, settings.IncidentDebugPoints("I-001", 700f));
            Assert.Equal(100f, settings.IncidentWeight("I-001"));

            settings.SetIncidentDebugPoints("I-001", 20000f);
            settings.SetIncidentWeight("I-001", -4f);
            Assert.Equal(10000f, settings.IncidentDebugPoints("I-001", 700f));
            Assert.Equal(0f, settings.IncidentWeight("I-001"));

            settings.SetIncidentEnabled("I-001", false);
            Assert.False(settings.IsIncidentEnabled("I-001"));
            settings.SetIncidentEnabled("I-001", true);
            Assert.True(settings.IsIncidentEnabled("I-001"));
        }

        [Theory]
        [InlineData("I-001", "I-042")]
        [InlineData("I-002", "I-037")]
        [InlineData("I-011", "I-040")]
        [InlineData("I-012", "I-038")]
        [InlineData("I-020", "I-028")]
        public void ClassSettingsApplyToEveryScenario(string primary, string variant)
        {
            RHAH_Settings settings = new RHAH_Settings();
            settings.SetIncidentEnabled(variant, false);
            settings.SetIncidentDebugPoints(variant, 450f);
            settings.SetIncidentWeight(variant, 30f);
            settings.SetIncidentAttitude(variant, RHAH_Attitude.Friendly);
            settings.SetIncidentLongChain(variant, true);
            Assert.False(settings.IsIncidentEnabled(primary));
            Assert.Equal(450f, settings.IncidentDebugPoints(primary, 700f));
            Assert.Equal(30f, settings.IncidentWeight(primary));
            Assert.Equal(RHAH_Attitude.Friendly, settings.IncidentAttitude(primary, RHAH_Attitude.Neutral));
            Assert.True(settings.IncidentUsesLongChain(primary));
            settings.SetIncidentEnabled(primary, true);
            settings.SetIncidentLongChain(primary, false);
            Assert.True(settings.IsIncidentEnabled(variant));
            Assert.False(settings.IncidentUsesLongChain(variant));
            Assert.True(settings.IsIncidentEnabled("I-004"));
        }

        [Theory]
        [InlineData(null, true, true, false)]
        [InlineData("<incidentGroupLongChains />", false, false, false)]
        [InlineData("<incidentGroupLongChains IsNull=\"True\" />", false, false, false)]
        [InlineData("<incidentGroupLongChains><li>I-002</li></incidentGroupLongChains>", true, false, false)]
        [InlineData("<incidentGroupLongChains><li>I-001</li></incidentGroupLongChains>", false, false, true)]
        public void LongChainLoadingDistinguishesMissingFieldFromPlayerOverrides(
            string savedField, bool childLongChain, bool traderLongChain, bool refugeeLongChain)
        {
            ScribeSaver previousSaver = Scribe.saver;
            ScribeLoader previousLoader = Scribe.loader;
            LoadSaveMode previousMode = Scribe.mode;
            bool previousProfiling = DeepProfiler.enabled;
            FieldInfo prefsField = typeof(Prefs).GetField("data", BindingFlags.NonPublic | BindingFlags.Static);
            object previousPrefs = prefsField.GetValue(null);
            string path = Path.GetTempFileName();
            try
            {
                Scribe.saver = new ScribeSaver();
                Scribe.loader = new ScribeLoader();
                Scribe.mode = LoadSaveMode.Inactive;
                DeepProfiler.enabled = false;
                prefsField.SetValue(null, new PrefsData { devMode = false });

                XmlDocument fixture = new XmlDocument();
                fixture.LoadXml("<settings>" + savedField + "</settings>");
                fixture.Save(path);

                RHAH_Settings loaded = LoadSettings(path);
                AssertLongChains(loaded, childLongChain, traderLongChain, refugeeLongChain);

                Scribe.saver.InitSaving(path, "settings");
                loaded.ExposeData();
                Scribe.saver.FinalizeSaving();
                AssertLongChains(LoadSettings(path), childLongChain, traderLongChain, refugeeLongChain);
            }
            finally
            {
                Scribe.ForceStop();
                Scribe.saver = previousSaver;
                Scribe.loader = previousLoader;
                Scribe.mode = previousMode;
                DeepProfiler.enabled = previousProfiling;
                prefsField.SetValue(null, previousPrefs);
                File.Delete(path);
            }
        }

        static RHAH_Settings LoadSettings(string path)
        {
            Scribe.loader.InitLoading(path);
            RHAH_Settings settings = new RHAH_Settings();
            Scribe.loader.curParent = settings;
            settings.ExposeData();
            Scribe.loader.initer.RegisterForPostLoadInit(settings);
            Scribe.loader.FinalizeLoading();
            return settings;
        }

        static void AssertLongChains(RHAH_Settings settings, bool childLongChain, bool traderLongChain, bool refugeeLongChain)
        {
            Assert.Equal(childLongChain, settings.IncidentUsesLongChain("I-002"));
            Assert.Equal(childLongChain, settings.IncidentUsesLongChain("I-037"));
            Assert.Equal(traderLongChain, settings.IncidentUsesLongChain("I-012"));
            Assert.Equal(traderLongChain, settings.IncidentUsesLongChain("I-038"));
            Assert.Equal(refugeeLongChain, settings.IncidentUsesLongChain("I-001"));
            Assert.False(settings.IncidentUsesLongChain("I-999"));
        }

        [Fact]
        public void ExactFieldKeepsAnEquivalentTypedBuffer()
        {
            Assert.True(HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.SameNumber("50", "50.0"));
            Assert.False(HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.SameNumber("50.5", "50"));
            Assert.False(HungerAndHavoc.Pawn.Compat.RHAH_IrisMenusWidgets.SameNumber("soon", "50"));
        }
        [Fact]
        public void CatalogPointsKeepTheCurrentGroupSize()
        {
            Assert.Equal(24, RHAH_IncidentScale.Count("I-001", 700f));
            Assert.Equal(4, RHAH_IncidentScale.Count("I-002", 400f));
            Assert.Equal(8, RHAH_IncidentScale.Count("I-010", 700f));
            Assert.Equal(3, RHAH_IncidentScale.Count("I-011", 350f));
            Assert.Equal(1, RHAH_IncidentScale.Count("I-008", 10000f));
            Assert.Equal(10, RHAH_IncidentScale.Count("I-001", 1f));
            Assert.Equal(50, RHAH_IncidentScale.Count("I-001", 10000f));
        }

        [Fact]
        public void EggEventsStayYoungAndLaboringMothersStartLabor()
        {
            Assert.Equal(RHAH_PawnRole.BeggarChild, RHAH_IncidentRoster.RoleAt("I-002", RHAH_PawnRole.Beggar, 0));
            Assert.Equal(RHAH_PawnRole.Mother, RHAH_IncidentRoster.RoleAt("I-003", RHAH_PawnRole.Mother, 0));
            Assert.Equal(RHAH_PawnRole.RatkinYoung, RHAH_IncidentRoster.RoleAt("I-003", RHAH_PawnRole.Mother, 1));
            Assert.Equal(RHAH_PawnRole.BeggarMother, RHAH_IncidentRoster.RoleAt("I-004", RHAH_PawnRole.Refugee, 0));
            Assert.Equal(RHAH_PawnRole.BeggarChild, RHAH_IncidentRoster.RoleAt("I-004", RHAH_PawnRole.Refugee, 4));
            Assert.Equal(RHAH_PawnRole.ThiefChild, RHAH_IncidentRoster.RoleAt("I-007", RHAH_PawnRole.Thief, 2));
            Assert.Equal(RHAH_PawnRole.WildChild, RHAH_IncidentRoster.RoleAt("I-009", RHAH_PawnRole.Wild, 0));
            Assert.Equal(RHAH_PawnRole.RatkinYoung, RHAH_IncidentRoster.RoleAt("I-013", RHAH_PawnRole.RatkinYoung, 3));
            Assert.Equal(RHAH_PawnRole.RatkinYoung, RHAH_IncidentRoster.RoleAt("I-032", RHAH_PawnRole.Refugee, 0));
            Assert.Equal(RHAH_PawnRole.RatkinYoung, RHAH_IncidentRoster.RoleAt("I-033", RHAH_PawnRole.Refugee, 0));
            Assert.Equal(RHAH_PawnRole.BeggarMother, RHAH_IncidentRoster.RoleAt("I-029", RHAH_PawnRole.Beggar, 0));
            Assert.Equal(RHAH_PawnRole.BeggarMother, RHAH_IncidentRoster.RoleAt("I-044", RHAH_PawnRole.Beggar, 1));
            Assert.Equal(RHAH_PawnRole.Trader, RHAH_IncidentRoster.RoleAt("I-012", RHAH_PawnRole.Trader, 0));
            Assert.Equal(RHAH_PawnRole.RatkinYoung, RHAH_IncidentRoster.RoleAt("I-012", RHAH_PawnRole.Trader, 1));
            Assert.Equal(RHAH_PawnRole.RatkinYoung, RHAH_IncidentRoster.RoleAt("I-038", RHAH_PawnRole.Trader, 2));
            Assert.Equal(RHAH_PawnRole.Refugee, RHAH_IncidentRoster.RoleAt("I-001", RHAH_PawnRole.Refugee, 8));
            Assert.True(RHAH_IncidentRoster.StartsLabor("I-029"));
            Assert.True(RHAH_IncidentRoster.StartsLabor("I-044"));
            Assert.False(RHAH_IncidentRoster.StartsLabor("I-004"));
            Assert.True(RHAH_IncidentRoster.Shatters("I-003", 0));
            Assert.False(RHAH_IncidentRoster.Shatters("I-003", 1));
            Assert.Equal(Gender.Female, RHAH_IncidentRoster.GenderAt("I-003", 0));
            Assert.Null(RHAH_IncidentRoster.GenderAt("I-003", 1));
        }

        [Fact]
        public void RequestAmountScalesAroundTheCatalogPoints()
        {
            Assert.Equal(12, RHAH_IncidentScale.ScaleAmount(12, 300f, 6, 28));
            Assert.Equal(6, RHAH_IncidentScale.ScaleAmount(12, 1f, 6, 28));
            Assert.Equal(28, RHAH_IncidentScale.ScaleAmount(12, 10000f, 6, 28));
            Assert.Equal(1, RHAH_RequestRules.Amount(RHAH_RequestKind.Baby, 0f, 0, 10000f));
        }

        static RHAH_IncidentWeightInput Input(
            RHAH_IncidentFamily family,
            RHAH_IncidentCategory category,
            RHAH_AttitudePool pool,
            RHAH_IncidentSeason season,
            int trust)
        {
            return new RHAH_IncidentWeightInput(
                family,
                category,
                RHAH_IncidentTarget.Map,
                pool,
                season,
                trust,
                mapHome: true,
                playerCaravan: false);
        }
    }
}
