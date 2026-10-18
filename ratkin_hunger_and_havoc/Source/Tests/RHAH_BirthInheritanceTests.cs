using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Generation;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_BirthInheritanceTests : IDisposable
    {
        readonly bool previousBiotechActive;
        readonly bool previousBiotechInstalled;
        readonly bool previousBinding;
        readonly XenotypeDef previousBaseliner;
        readonly LoadedLanguage previousLanguage;
        readonly string temporary;
        readonly string previousSavePath;
        readonly string previousModsPath;
        readonly string previousOfficialModsPath;
        readonly object previousPrefs;
        readonly bool previousProfiler;
        readonly Dictionary<FieldInfo, object> previousRelations = new Dictionary<FieldInfo, object>();

        public RHAH_BirthInheritanceTests()
        {
            temporary = Path.Combine(Path.GetTempPath(), "rhah-inheritance-" + Guid.NewGuid());
            Directory.CreateDirectory(Path.Combine(temporary, "Config"));
            Directory.CreateDirectory(Path.Combine(temporary, "Mods", "Core", "About"));
            File.WriteAllText(Path.Combine(temporary, "Mods", "Core", "About", "About.xml"),
                "<ModMetaData><name>Core</name><packageId>ludeon.rimworld</packageId><author>Ludeon</author><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>");
            File.WriteAllText(Path.Combine(temporary, "Config", "ModsConfig.xml"),
                "<ModsConfigData><activeMods /><knownExpansions /></ModsConfigData>");
            previousSavePath = (string)Field(typeof(GenFilePaths), "saveDataPath").GetValue(null);
            previousModsPath = (string)Field(typeof(GenFilePaths), "modsFolderPath").GetValue(null);
            previousOfficialModsPath = (string)Field(typeof(GenFilePaths), "officialModsFolderPath").GetValue(null);
            previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
            previousProfiler = DeepProfiler.enabled;
            Field(typeof(GenFilePaths), "saveDataPath").SetValue(null, temporary);
            Field(typeof(GenFilePaths), "modsFolderPath").SetValue(null, Path.Combine(temporary, "Mods"));
            Field(typeof(GenFilePaths), "officialModsFolderPath").SetValue(null, Path.Combine(temporary, "Mods"));
            Field(typeof(Prefs), "data").SetValue(null, new PrefsData());
            DeepProfiler.enabled = false;
            previousBiotechActive = Flag(typeof(ModsConfig), "biotechActive");
            previousBiotechInstalled = Flag(typeof(ModLister), "biotechInstalled");
            previousBinding = Flag(typeof(DefOfHelper), "bindingNow");
            SetFlag(typeof(ModsConfig), "biotechActive", true);
            SetFlag(typeof(ModLister), "biotechInstalled", true);
            SetFlag(typeof(DefOfHelper), "bindingNow", true);
            foreach (string name in new[] { "Spouse", "Lover", "ExSpouse", "ExLover" })
            {
                FieldInfo field = typeof(PawnRelationDefOf).GetField(name);
                object previous = field.GetValue(null);
                previousRelations.Add(field, previous);
                if (previous == null)
                {
                    field.SetValue(null, new PawnRelationDef { defName = name });
                }
            }
            previousBaseliner = XenotypeDefOf.Baseliner;
            XenotypeDefOf.Baseliner = Xenotype("Baseliner", false);
            previousLanguage = LanguageDatabase.activeLanguage;
            LoadedLanguage language = (LoadedLanguage)FormatterServices.GetUninitializedObject(typeof(LoadedLanguage));
            language.keyedReplacements = new Dictionary<string, LoadedLanguage.KeyedReplacement>
            {
                ["Hybrid"] = new LoadedLanguage.KeyedReplacement { key = "Hybrid", value = "Mixed offspring" }
            };
            typeof(LoadedLanguage).GetField("dataIsLoaded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(language, true);
            LanguageDatabase.activeLanguage = language;
        }

        public void Dispose()
        {
            LanguageDatabase.activeLanguage = previousLanguage;
            XenotypeDefOf.Baseliner = previousBaseliner;
            foreach (KeyValuePair<FieldInfo, object> entry in previousRelations)
            {
                entry.Key.SetValue(null, entry.Value);
            }
            SetFlag(typeof(DefOfHelper), "bindingNow", previousBinding);
            SetFlag(typeof(ModLister), "biotechInstalled", previousBiotechInstalled);
            SetFlag(typeof(ModsConfig), "biotechActive", previousBiotechActive);
            DeepProfiler.enabled = previousProfiler;
            Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
            Field(typeof(GenFilePaths), "saveDataPath").SetValue(null, previousSavePath);
            Field(typeof(GenFilePaths), "modsFolderPath").SetValue(null, previousModsPath);
            Field(typeof(GenFilePaths), "officialModsFolderPath").SetValue(null, previousOfficialModsPath);
            Directory.Delete(temporary, true);
        }

        [Fact]
        public void DifferentHeritableParentsProduceMixedIdentityWithoutChangingGenes()
        {
            GeneDef inherited = new GeneDef { defName = "Inherited" };
            GeneDef implant = new GeneDef { defName = "Implant" };
            Verse.Pawn mother = Parent(Xenotype("Ratkin", true));
            Verse.Pawn father = Parent(Xenotype("Other", true));
            Verse.Pawn baby = Parent(XenotypeDefOf.Baseliner, inherited);
            baby.genes.Xenogenes.Add(GeneOf(implant));

            RHAH_Fertility.SetBirthXenotype(baby, mother, father);

            Assert.True(baby.genes.hybrid);
            Assert.Equal("Mixed offspring", baby.genes.XenotypeLabel);
            Assert.Same(inherited, Assert.Single(baby.genes.Endogenes).def);
            Assert.Same(implant, Assert.Single(baby.genes.Xenogenes).def);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void KnownHeritableParentKeepsIdentityWithoutInstallingMissingGenes(bool bothParents)
        {
            GeneDef omitted = new GeneDef { defName = "NotInherited" };
            GeneDef inherited = new GeneDef { defName = "Inherited" };
            GeneDef implant = new GeneDef { defName = "Implant" };
            XenotypeDef ratkin = Xenotype("Ratkin", true);
            ratkin.genes.Add(omitted);
            Verse.Pawn mother = Parent(ratkin);
            Verse.Pawn baby = Parent(XenotypeDefOf.Baseliner, inherited);
            baby.genes.Xenogenes.Add(GeneOf(implant));

            RHAH_Fertility.SetBirthXenotype(baby, mother, bothParents ? Parent(ratkin) : null);

            Assert.Same(ratkin, baby.genes.Xenotype);
            Assert.False(baby.genes.hybrid);
            Assert.Same(inherited, Assert.Single(baby.genes.Endogenes).def);
            Assert.Same(implant, Assert.Single(baby.genes.Xenogenes).def);
        }

        [Fact]
        public void NonheritableParentDoesNotCountAsAnUnknownParent()
        {
            Verse.Pawn mother = Parent(Xenotype("Ratkin", true));
            Verse.Pawn father = Parent(Xenotype("Implanted", false));
            Verse.Pawn baby = Parent(XenotypeDefOf.Baseliner);

            RHAH_Fertility.SetBirthXenotype(baby, mother, father);

            Assert.Same(XenotypeDefOf.Baseliner, baby.genes.Xenotype);
            Assert.True(baby.genes.hybrid);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void SingleNonheritableParentPreservesOnlyExistingHybridStatus(bool parentHybrid, bool expectedHybrid)
        {
            Verse.Pawn mother = Parent(XenotypeDefOf.Baseliner);
            mother.genes.hybrid = parentHybrid;
            Verse.Pawn baby = Parent(XenotypeDefOf.Baseliner);

            RHAH_Fertility.SetBirthXenotype(baby, mother, null);

            Assert.Equal(expectedHybrid, baby.genes.hybrid);
            Assert.Same(XenotypeDefOf.Baseliner, baby.genes.Xenotype);
        }

        [Fact]
        public void IdenticalCustomGenomesRetainTheirNameAndIcon()
        {
            Verse.Pawn mother = Parent(XenotypeDefOf.Baseliner);
            Verse.Pawn father = Parent(XenotypeDefOf.Baseliner);
            mother.genes.xenotypeName = father.genes.xenotypeName = "Custom ratkin";
            XenotypeIconDef icon = new XenotypeIconDef { defName = "CustomIcon" };
            mother.genes.iconDef = icon;
            Verse.Pawn baby = Parent(XenotypeDefOf.Baseliner);

            RHAH_Fertility.SetBirthXenotype(baby, mother, father);

            Assert.Equal("Custom ratkin", baby.genes.XenotypeLabel);
            Assert.Same(icon, baby.genes.iconDef);
            Assert.False(baby.genes.hybrid);
        }

        [Fact]
        public void LitterUsesVanillaExclusionsAndOwnsItsSample()
        {
            GeneDef common = new GeneDef { defName = "Common" };
            GeneDef hair = new GeneDef { defName = "Hair", endogeneCategory = EndogeneCategory.HairColor };
            GeneDef prerequisite = new GeneDef { defName = "AbsentPrerequisite" };
            GeneDef dependent = new GeneDef { defName = "Dependent", prerequisite = prerequisite };
            GeneDef archite = new GeneDef { defName = "Archite", biostatArc = 1 };
            GeneDef implant = new GeneDef { defName = "Implant" };
            Verse.Pawn mother = Parent(Xenotype("Ratkin", true), common, hair, dependent, archite);
            Verse.Pawn father = Parent(Xenotype("Other", true), common, hair, dependent, archite);
            mother.genes.Xenogenes.Add(GeneOf(implant));
            Rand.PushState(170112);
            try
            {
                PawnGenerationRequest request = RHAH_Fertility.LitterRequest(mother, father);
                Assert.Contains(common, request.ForcedEndogenes);
                Assert.Contains(hair, request.ForcedEndogenes);
                Assert.DoesNotContain(dependent, request.ForcedEndogenes);
                Assert.DoesNotContain(archite, request.ForcedEndogenes);
                Assert.DoesNotContain(implant, request.ForcedEndogenes);
                Assert.Same(XenotypeDefOf.Baseliner, request.ForcedXenotype);
                List<GeneDef> saved = new List<GeneDef>(request.ForcedEndogenes);

                mother.genes.Endogenes.Clear();
                father.genes.Endogenes.Clear();
                mother.genes.Endogenes.Add(GeneOf(hair));
                father.genes.Endogenes.Add(GeneOf(hair));
                RHAH_Fertility.LitterRequest(mother, father);

                Assert.Equal(saved, request.ForcedEndogenes);
            }
            finally
            {
                Rand.PopState();
            }
        }

        static XenotypeDef Xenotype(string name, bool inheritable)
        {
            return new XenotypeDef { defName = name, label = name, inheritable = inheritable };
        }

        static Verse.Pawn Parent(XenotypeDef xenotype, params GeneDef[] endogenes)
        {
            Verse.Pawn pawn = (Verse.Pawn)FormatterServices.GetUninitializedObject(typeof(Verse.Pawn));
            Pawn_GeneTracker tracker = new Pawn_GeneTracker(pawn);
            typeof(Pawn_GeneTracker).GetField("xenotype", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tracker, xenotype);
            foreach (GeneDef def in endogenes)
            {
                tracker.Endogenes.Add(GeneOf(def));
            }
            pawn.genes = tracker;
            pawn.kindDef = new PawnKindDef
            {
                race = new ThingDef { race = new RaceProperties { lifeStageAges = new List<LifeStageAge>() } }
            };
            pawn.def = pawn.kindDef.race;
            typeof(RaceProperties).GetField("fleshType", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(pawn.RaceProps, new FleshTypeDef { isOrganic = true });
            pawn.relations = new Pawn_RelationsTracker(pawn);
            return pawn;
        }

        static Gene GeneOf(GeneDef def)
        {
            Gene gene = (Gene)FormatterServices.GetUninitializedObject(typeof(Gene));
            gene.def = def;
            return gene;
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        static bool Flag(Type type, string name)
        {
            return (bool)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        }

        static void SetFlag(Type type, string name, bool value)
        {
            type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
        }
    }
}
