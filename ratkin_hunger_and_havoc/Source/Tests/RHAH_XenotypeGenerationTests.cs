using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Pawn.Compat;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_XenotypeGenerationTests : IDisposable
    {
        readonly List<XenotypeDef> previousXenotypes = new List<XenotypeDef>(DefDatabase<XenotypeDef>.AllDefsListForReading);
        readonly List<PawnKindDef> previousKinds = new List<PawnKindDef>(DefDatabase<PawnKindDef>.AllDefsListForReading);
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly bool previousBiotech;
        readonly bool previousResolved;
        readonly object previousAllows;
        readonly XenotypeDef previousBaseliner;
        readonly bool previousBinding;
        readonly bool previousProfiler;
        readonly XenotypeDef primary = new XenotypeDef { defName = RHAH_GeneCatalog.DefaultXenotypeDefName };
        readonly XenotypeDef external = new XenotypeDef { defName = "Ratkin_RegressionExternal" };
        readonly ThingDef race = new ThingDef { defName = "Ratkin", race = new RaceProperties { lifeStageAges = new List<LifeStageAge>() } };
        readonly PawnKindDef kind;
        readonly Harmony harmony = new Harmony("rhah.xenotype.generation.tests");

        static RHAH_XenotypeGenerationTests()
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RHAH_ContentApplierTests).TypeHandle);
        }

        public RHAH_XenotypeGenerationTests()
        {
            previousProfiler = DeepProfiler.enabled;
            DeepProfiler.enabled = false;
            previousBiotech = (bool)Field(typeof(ModsConfig), "biotechActive").GetValue(null);
            Field(typeof(ModsConfig), "biotechActive").SetValue(null, true);
            previousBinding = (bool)Field(typeof(DefOfHelper), "bindingNow").GetValue(null);
            Field(typeof(DefOfHelper), "bindingNow").SetValue(null, true);
            previousBaseliner = XenotypeDefOf.Baseliner;
            XenotypeDefOf.Baseliner = new XenotypeDef { defName = "Baseliner" };
            previousResolved = (bool)Field(typeof(RHAH_HarXenotypeBridge), "resolved").GetValue(null);
            previousAllows = Field(typeof(RHAH_HarXenotypeBridge), "canUse").GetValue(null);
            Field(typeof(RHAH_HarXenotypeBridge), "resolved").SetValue(null, true);
            Allow((x, r) => x != null && r == race);
            DefDatabase<XenotypeDef>.Clear();
            DefDatabase<PawnKindDef>.Clear();
            DefDatabase<XenotypeDef>.Add(primary);
            DefDatabase<XenotypeDef>.Add(external);
            RHAH_Mod.Settings = new RHAH_Settings();
            kind = new PawnKindDef { defName = "RHAH_PawnKind_Ratkin", race = race, xenotypeSet = Pool(primary, 1f) };
            harmony.CreateClassProcessor(typeof(RHAH_AvailableXenotypesPatch)).Patch();
            harmony.CreateClassProcessor(typeof(RHAH_GeneratedXenotypePatch)).Patch();
            harmony.CreateClassProcessor(typeof(RHAH_FactionBaselinerChancePatch)).Patch();
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            DefDatabase<XenotypeDef>.Clear();
            DefDatabase<PawnKindDef>.Clear();
            foreach (XenotypeDef def in previousXenotypes) DefDatabase<XenotypeDef>.Add(def);
            foreach (PawnKindDef def in previousKinds) DefDatabase<PawnKindDef>.Add(def);
            RHAH_Mod.Settings = previousSettings;
            Field(typeof(ModsConfig), "biotechActive").SetValue(null, previousBiotech);
            Field(typeof(RHAH_HarXenotypeBridge), "resolved").SetValue(null, previousResolved);
            Field(typeof(RHAH_HarXenotypeBridge), "canUse").SetValue(null, previousAllows);
            XenotypeDefOf.Baseliner = previousBaseliner;
            Field(typeof(DefOfHelper), "bindingNow").SetValue(null, previousBinding);
            DeepProfiler.enabled = previousProfiler;
        }


        [Fact]
        public void GenericPoolUsesUserWeightsWithoutAnImplicitBaselinerRemainder()
        {
            RHAH_Mod.Settings.SetXenotypeEnabled(external.defName, true);
            RHAH_Mod.Settings.SetXenotypeWeight(primary.defName, 25f);
            RHAH_Mod.Settings.SetXenotypeWeight(external.defName, 75f);
            Dictionary<XenotypeDef, float> pool = new Dictionary<XenotypeDef, float>();
            RHAH_XenotypeGeneration.ReplacePool(kind, pool);
            Assert.Equal(2, pool.Count);
            Assert.Equal(0.25f, pool[primary]);
            Assert.Equal(0.75f, pool[external]);
            Assert.True(RHAH_XenotypeResolver.TryResolve(null, kind, out XenotypeDef actual));
            Assert.True(pool.ContainsKey(actual));
        }

        [Fact]
        public void HarRestrictedConfiguredXenotypeCannotEnterGenericPool()
        {
            RHAH_Mod.Settings.SetXenotypeEnabled(external.defName, true);
            RHAH_Mod.Settings.SetXenotypeWeight(external.defName, 100f);
            Allow((x, r) => x == external && r == race);
            Dictionary<XenotypeDef, float> pool = new Dictionary<XenotypeDef, float>();
            RHAH_XenotypeGeneration.ReplacePool(kind, pool);
            Assert.Same(external, Assert.Single(pool).Key);
            Assert.Equal(1f, pool[external]);
        }

        [Fact]
        public void DisabledOptimizationReadsUpstreamNativePoolNotOwnDefaultDeclaration()
        {
            RHAH_Mod.Settings.optimizeGeneration = false;
            DefDatabase<PawnKindDef>.Add(new PawnKindDef
            {
                defName = "RatkinColonist", race = race, xenotypeSet = Pool(external, 0.4f)
            });
            Dictionary<XenotypeDef, float> pool = new Dictionary<XenotypeDef, float>();
            RHAH_XenotypeGeneration.ReplacePool(kind, pool);
            Assert.Same(external, Assert.Single(pool).Key);
            Assert.Equal(1f, pool[external]);
            Assert.True(RHAH_XenotypeResolver.TryResolve(null, kind, out XenotypeDef actual));
            Assert.Same(external, actual);
        }

        [Fact]
        public void UnrelatedKindsAndFactionsAreUnchanged()
        {
            Dictionary<XenotypeDef, float> pool = new Dictionary<XenotypeDef, float> { [external] = 0.2f };
            RHAH_XenotypeGeneration.ReplacePool(new PawnKindDef { defName = "RatkinColonist", race = race }, pool);
            Assert.Equal(0.2f, Assert.Single(pool).Value);
            FactionDef foreign = new FactionDef { defName = "RatkinChurch", basicMemberKind = kind };
            string cached = "Unchanged foreign description";
            RHAH_XenotypeGeneration.RefreshFaction(foreign, ref cached);
            Assert.Equal("Unchanged foreign description", cached);
            Assert.Null(foreign.xenotypeSet);
        }

        [Fact]
        public void DisplayPoolRefreshesOnlyWhenEffectiveWeightsChange()
        {
            FactionDef faction = new FactionDef
            {
                defName = "RHAH_Faction_Neutral", basicMemberKind = kind, xenotypeSet = Pool(primary, 1f)
            };
            string cached = "Current";
            RHAH_XenotypeGeneration.RefreshFaction(faction, ref cached);
            Assert.Equal("Current", cached);
            RHAH_Mod.Settings.SetXenotypeEnabled(external.defName, true);
            RHAH_Mod.Settings.SetXenotypeWeight(primary.defName, 0f);
            RHAH_Mod.Settings.SetXenotypeWeight(external.defName, 100f);
            RHAH_XenotypeGeneration.RefreshFaction(faction, ref cached);
            Assert.Null(cached);
            Assert.Same(external, faction.xenotypeSet[0].xenotype);
            Assert.Equal(1f, faction.xenotypeSet[0].chance);
        }

        [Fact]
        public void PermissionChangeClearsDisplayWithoutShowingAnImplicitBaseliner()
        {
            FactionDef faction = new FactionDef
            {
                defName = "RHAH_Faction_Neutral", basicMemberKind = kind, xenotypeSet = Pool(primary, 1f)
            };
            string cached = "Previous";
            Allow((x, r) => false);
            RHAH_XenotypeGeneration.RefreshFaction(faction, ref cached);
            Assert.Null(cached);
            Assert.Equal(0, faction.xenotypeSet.Count);
            Assert.Equal(0f, faction.BaselinerChance);
        }

        [Fact]
        public void MissingHarPermissionDoesNotBecomeAnImplicitBaseliner()
        {
            Allow((x, r) => false);
            Dictionary<XenotypeDef, float> pool = new Dictionary<XenotypeDef, float>();
            RHAH_XenotypeGeneration.ReplacePool(kind, pool);
            Assert.Empty(pool);
            Assert.False(RHAH_XenotypeResolver.TryResolve(null, kind, out _));
            PawnGenerationRequest request = new PawnGenerationRequest(kind);
            Assert.Throws<InvalidOperationException>(() => PawnGenerator.GetXenotypeForGeneratedPawn(request));
        }

        [Fact]
        public void ExplicitGeneratorRequestsKeepVanillaPrecedenceButStillRespectHar()
        {
            PawnGenerationRequest forced = new PawnGenerationRequest(kind, forcedXenotype: external);
            Assert.Same(external, PawnGenerator.GetXenotypeForGeneratedPawn(forced));
            PawnGenerationRequest baseline = new PawnGenerationRequest(kind, forceBaselinerChance: 1f);
            Assert.Same(XenotypeDefOf.Baseliner, PawnGenerator.GetXenotypeForGeneratedPawn(baseline));
            PawnGenerationRequest allowed = new PawnGenerationRequest(kind)
            {
                AllowedXenotypes = new List<XenotypeDef> { external }
            };
            Assert.Same(external, PawnGenerator.GetXenotypeForGeneratedPawn(allowed));
            Allow((x, r) => x == primary && r == race);
            Assert.Throws<InvalidOperationException>(() => PawnGenerator.GetXenotypeForGeneratedPawn(forced));
        }

        [Fact]
        public void NoncombatantOnlyPoolCannotFallBackToBaselinerForCombatRequest()
        {
            primary.canGenerateAsCombatant = false;
            PawnGenerationRequest request = new PawnGenerationRequest(kind, mustBeCapableOfViolence: true);
            Assert.Throws<InvalidOperationException>(() => PawnGenerator.GetXenotypeForGeneratedPawn(request));
        }

        [Fact]
        public void InactiveBiotechLeavesVanillaPoolUntouched()
        {
            Field(typeof(ModsConfig), "biotechActive").SetValue(null, false);
            Dictionary<XenotypeDef, float> pool = new Dictionary<XenotypeDef, float> { [external] = 0.2f };
            RHAH_XenotypeGeneration.ReplacePool(kind, pool);
            Assert.Equal(0.2f, Assert.Single(pool).Value);
        }

        [Fact]
        public void VanillaGeneratorUsesConfiguredRatkinPoolWithoutFactory()
        {
            RHAH_Mod.Settings.SetXenotypeEnabled(external.defName, true);
            RHAH_Mod.Settings.SetXenotypeWeight(primary.defName, 0f);
            RHAH_Mod.Settings.SetXenotypeWeight(external.defName, 100f);
            Dictionary<XenotypeDef, float> available = PawnGenerator.XenotypesAvailableFor(kind);
            Assert.Same(external, Assert.Single(available).Key);
            Assert.Same(external, PawnGenerator.GetXenotypeForGeneratedPawn(new PawnGenerationRequest(kind)));
        }

        static void Allow(Func<XenotypeDef, ThingDef, bool> allows)
        {
            Field(typeof(RHAH_HarXenotypeBridge), "canUse").SetValue(null, allows);
        }

        static XenotypeSet Pool(XenotypeDef xenotype, float chance)
        {
            XenotypeSet set = new XenotypeSet();
            Field(typeof(XenotypeSet), "xenotypeChances").SetValue(set,
                new List<XenotypeChance> { new XenotypeChance(xenotype, chance) });
            return set;
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        }

    }
}
