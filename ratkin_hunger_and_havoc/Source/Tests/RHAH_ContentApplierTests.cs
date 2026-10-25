using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HungerAndHavoc.Core;
using HungerAndHavoc.Data;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ContentApplierTests : IDisposable
    {
        readonly RHAH_Settings previousSettings;
        readonly List<BackstoryDef> previousHistories;
        readonly List<TraitDef> previousTraits;

        static RHAH_ContentApplierTests()
        {
            RimWorldAssemblies.EnsureResolved();
            string temporary = Path.Combine(Path.GetTempPath(), "rhah-content-" + Guid.NewGuid());
            FieldInfo savePath = typeof(GenFilePaths).GetField("saveDataPath", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo modsPath = typeof(GenFilePaths).GetField("modsFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo officialPath = typeof(GenFilePaths).GetField("officialModsFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            object previousSave = savePath.GetValue(null);
            object previousMods = modsPath.GetValue(null);
            object previousOfficial = officialPath.GetValue(null);
            FieldInfo prefs = typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
            object previousPrefs = prefs.GetValue(null);
            bool previousProfiler = DeepProfiler.enabled;
            try
            {
                Directory.CreateDirectory(Path.Combine(temporary, "Config"));
                Directory.CreateDirectory(Path.Combine(temporary, "Mods", "Core", "About"));
                File.WriteAllText(Path.Combine(temporary, "Mods", "Core", "About", "About.xml"),
                    "<ModMetaData><name>Core</name><packageId>ludeon.rimworld</packageId><author>Ludeon</author><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>");
                File.WriteAllText(Path.Combine(temporary, "Config", "ModsConfig.xml"),
                    "<ModsConfigData><activeMods /><knownExpansions /></ModsConfigData>");
                savePath.SetValue(null, temporary);
                modsPath.SetValue(null, Path.Combine(temporary, "Mods"));
                officialPath.SetValue(null, Path.Combine(temporary, "Mods"));
                prefs.SetValue(null, new PrefsData { devMode = false });
                DeepProfiler.enabled = false;
                RuntimeHelpers.RunClassConstructor(typeof(ModsConfig).TypeHandle);
            }
            finally
            {
                savePath.SetValue(null, previousSave);
                modsPath.SetValue(null, previousMods);
                officialPath.SetValue(null, previousOfficial);
                prefs.SetValue(null, previousPrefs);
                DeepProfiler.enabled = previousProfiler;
                Directory.Delete(temporary, true);
            }
        }

        public RHAH_ContentApplierTests()
        {
            previousSettings = RHAH_Mod.Settings;
            previousHistories = new List<BackstoryDef>(DefDatabase<BackstoryDef>.AllDefsListForReading);
            previousTraits = new List<TraitDef>(DefDatabase<TraitDef>.AllDefsListForReading);
            DefDatabase<BackstoryDef>.Clear();
            DefDatabase<TraitDef>.Clear();
            foreach (RHAH_HistoryRecord record in RHAH_ContentCatalog.Histories)
            {
                DefDatabase<BackstoryDef>.Add(History(record.BackstoryDefName,
                    record.Slot == RHAH_HistorySlot.Adulthood ? BackstorySlot.Adulthood : BackstorySlot.Childhood));
            }
            DefDatabase<BackstoryDef>.Add(History(RHAH_ContentCatalog.FallbackChildhood, BackstorySlot.Childhood));
            foreach (RHAH_TraitRecord record in RHAH_ContentCatalog.Traits)
            {
                DefDatabase<TraitDef>.Add(new TraitDef
                {
                    defName = record.TraitDefName,
                    degreeDatas = new List<TraitDegreeData> { new TraitDegreeData { degree = 0 } }
                });
            }
            RHAH_Mod.Settings = new RHAH_Settings();
            Rand.PushState(741);
        }

        public void Dispose()
        {
            Rand.PopState();
            RHAH_Mod.Settings = previousSettings;
            DefDatabase<BackstoryDef>.Clear();
            DefDatabase<TraitDef>.Clear();
            foreach (BackstoryDef def in previousHistories) DefDatabase<BackstoryDef>.Add(def);
            foreach (TraitDef def in previousTraits) DefDatabase<TraitDef>.Add(def);
        }

        [Theory]
        [InlineData(0f, DevelopmentalStage.Baby)]
        [InlineData(6f, DevelopmentalStage.Child)]
        [InlineData(12f, DevelopmentalStage.Adult)]
        [InlineData(13f, DevelopmentalStage.Adult)]
        [InlineData(17.999f, DevelopmentalStage.Adult)]
        public void DefaultApplicationKeepsJuvenileContentEvenInAdultDevelopmentalStage(float age, DevelopmentalStage stage)
        {
            foreach (bool missingSettings in new[] { false, true })
            {
                RHAH_Mod.Settings = missingSettings ? null : new RHAH_Settings();
                for (int seed = 0; seed < 20; seed++)
                {
                    Rand.Seed = seed;
                    Verse.Pawn pawn = Pawn(age, stage);
                    RHAH_ContentApplier.Apply(pawn, Request("I-001"), true);
                    Assert.NotNull(pawn.story.Childhood);
                    Assert.Equal(BackstorySlot.Childhood, pawn.story.Childhood.slot);
                    Assert.Null(pawn.story.Adulthood);
                    Assert.All(pawn.story.traits.allTraits, trait => Assert.NotEqual(
                        RHAH_TraitSlot.Adulthood, RHAH_ContentCatalog.FindTraitByDef(trait.def.defName).Slot));
                }
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void MissingHistoriesCannotLetAdultOnlyTraitsWinTheIncidentOrFallbackPool(bool filterAge, bool matchingIncident)
        {
            RHAH_Mod.Settings.traitAgeFilter = filterAge;
            RHAH_Mod.Settings.pawnHistoriesEnabled = false;
            OnlyTrait("T23");
            Verse.Pawn pawn = Pawn(17.999f, DevelopmentalStage.Adult);
            RHAH_ContentApplier.Apply(pawn, Request(matchingIncident ? "I-001" : null), true);
            Assert.Empty(pawn.story.traits.allTraits);
            Assert.Null(pawn.story.Adulthood);
        }

        [Theory]
        [InlineData("T37")]
        [InlineData("T01")]
        public void JuvenilesKeepAllowedChildAndAnyTraitsWhenAgeFilterIsOff(string traitId)
        {
            RHAH_Mod.Settings.traitAgeFilter = false;
            RHAH_Mod.Settings.pawnHistoriesEnabled = false;
            OnlyTrait(traitId);
            Verse.Pawn pawn = Pawn(17f, DevelopmentalStage.Adult);
            RHAH_ContentApplier.Apply(pawn, Request(null), true);
            Assert.Equal(RHAH_ContentCatalog.FindTrait(traitId).TraitDefName,
                Assert.Single(pawn.story.traits.allTraits).def.defName);
        }

        [Theory]
        [InlineData(18f)]
        [InlineData(30f)]
        public void AdultsKeepOwnedHistoryAndAdultTraits(float age)
        {
            foreach (RHAH_HistoryRecord record in RHAH_ContentCatalog.Histories)
            {
                RHAH_Mod.Settings.SetHistoryEnabled(record.DisplayId, record.DisplayId == "A023");
            }
            OnlyTrait("T23");
            Verse.Pawn pawn = Pawn(age, DevelopmentalStage.Adult);
            RHAH_ContentApplier.Apply(pawn, Request("I-001"), true);
            Assert.Equal(RHAH_ContentCatalog.FindHistory("A023").BackstoryDefName, pawn.story.Adulthood?.defName);
            Assert.Equal(RHAH_ContentCatalog.FindTrait("T23").TraitDefName,
                Assert.Single(pawn.story.traits.allTraits).def.defName);
        }

        [Theory]
        [InlineData(17f, DevelopmentalStage.Adult, false)]
        [InlineData(18f, DevelopmentalStage.Adult, false)]
        [InlineData(19.999f, DevelopmentalStage.Adult, false)]
        [InlineData(20f, DevelopmentalStage.Adult, true)]
        [InlineData(30f, DevelopmentalStage.Child, false)]
        public void ExplicitProfileRetainsTwentyYearBoundaryAndRequiresAdultDevelopment(float age, DevelopmentalStage stage, bool adulthood)
        {
            BackstoryDef child = History("ExplicitChild", BackstorySlot.Childhood);
            BackstoryDef adult = History("ExplicitAdult", BackstorySlot.Adulthood);
            RHAH_PawnProfile profile = new RHAH_PawnProfile
            {
                UseExplicitBackstory = true,
                Childhood = child,
                Adulthood = adult
            };
            Verse.Pawn pawn = Pawn(age, stage);
            typeof(RHAH_PawnFactory).GetMethod("ApplyProfile", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { pawn, profile });
            RHAH_Mod.Settings.pawnTraitsEnabled = false;
            RHAH_ContentApplier.Apply(pawn, Request(null), false);
            Assert.Same(child, pawn.story.Childhood);
            Assert.Same(adulthood ? adult : null, pawn.story.Adulthood);
        }

        [Fact]
        public void ExplicitAdultHistoryCannotBeSmuggledThroughJuvenileChildhoodSlot()
        {
            Verse.Pawn pawn = Pawn(16f, DevelopmentalStage.Adult);
            RHAH_PawnProfile profile = new RHAH_PawnProfile
            {
                UseExplicitBackstory = true,
                Childhood = History("MisplacedAdulthood", BackstorySlot.Adulthood),
                Adulthood = History("ExplicitAdult", BackstorySlot.Adulthood)
            };
            typeof(RHAH_PawnFactory).GetMethod("ApplyProfile", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { pawn, profile });
            Assert.Null(pawn.story.Childhood);
            Assert.Null(pawn.story.Adulthood);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void JuvenileLosesUpstreamAdulthoodEvenWhenNoHistoryCanBeApplied(bool settingsEnabled)
        {
            RHAH_Mod.Settings.pawnHistoriesEnabled = settingsEnabled;
            RHAH_Mod.Settings.pawnTraitsEnabled = false;
            foreach (RHAH_HistoryRecord record in RHAH_ContentCatalog.Histories)
            {
                RHAH_Mod.Settings.SetHistoryEnabled(record.DisplayId, false);
            }
            Verse.Pawn pawn = Pawn(16f, DevelopmentalStage.Adult);
            pawn.story.Adulthood = History("UpstreamAdulthood", BackstorySlot.Adulthood);
            RHAH_ContentApplier.Apply(pawn, Request(null), true);
            Assert.Null(pawn.story.Adulthood);
        }

        static Verse.Pawn Pawn(float age, DevelopmentalStage stage)
        {
            LifeStageAge lifeStage = (LifeStageAge)FormatterServices.GetUninitializedObject(typeof(LifeStageAge));
            lifeStage.def = new LifeStageDef { developmentalStage = stage };
            Verse.Pawn pawn = new Verse.Pawn { gender = Gender.Female };
            pawn.def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            pawn.def.race = new RaceProperties
            {
                intelligence = Intelligence.Humanlike,
                lifeStageAges = new List<LifeStageAge> { lifeStage }
            };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "ageBiologicalTicksInt").SetValue(pawn.ageTracker, (long)(age * 3600000f));
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            pawn.story = new Pawn_StoryTracker(pawn);
            pawn.health = new Pawn_HealthTracker(pawn);
            pawn.needs = new Pawn_NeedsTracker();
            return pawn;
        }

        static BackstoryDef History(string name, BackstorySlot slot)
        {
            return new BackstoryDef { defName = name, slot = slot, spawnCategories = new List<string>() };
        }

        static RHAH_PawnRequest Request(string incident)
        {
            return new RHAH_PawnRequest { SourceIncidentDisplayId = incident };
        }

        static void OnlyTrait(string id)
        {
            foreach (RHAH_TraitRecord record in RHAH_ContentCatalog.Traits)
            {
                RHAH_Mod.Settings.SetTraitEnabled(record.DisplayId, record.DisplayId == id);
            }
            RHAH_Mod.Settings.SetTraitWeight(id, 100f);
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
