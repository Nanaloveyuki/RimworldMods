using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Pawn;
using Oxygendsl.Core.Events;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ContentPresetTests : IDisposable
    {
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;

        public void Dispose() => RHAH_Mod.Settings = previousSettings;

        [Fact]
        public void EveryScenarioHasAConsistentGroupRating()
        {
            foreach (RHAH_IncidentEntry entry in RHAH_IncidentCatalog.All)
            {
                int group = RHAH_IncidentGroups.GroupOf(entry.DisplayId);
                if (entry.DisplayId == "I-019")
                {
                    Assert.Equal(RHAH_ContentRating.Realistic, entry.ContentRating);
                    continue;
                }
                Assert.True(group >= 0);
                Assert.Equal(RHAH_IncidentGroups.Scenarios(group)[0].ContentRating, entry.ContentRating);
            }
            Assert.Equal(RHAH_ContentRating.Cruel, RHAH_IncidentRating.For("I-055"));
            Assert.Equal(RHAH_ContentRating.Realistic, RHAH_IncidentRating.For("I-013"));
            Assert.Equal(RHAH_ContentRating.General, RHAH_IncidentRating.For("I-029"));
            Assert.Equal(RHAH_ContentRating.General, RHAH_IncidentRating.For("I-032"));
            Assert.Equal(RHAH_ContentRating.AllAges, RHAH_IncidentRating.For("I-020"));
        }

        [Fact]
        public void SwitchingPresetsRestoresIntensityWithoutChangingTrustOrUnrelatedSettings()
        {
            RHAH_Settings settings = RHAH_Mod.Settings = new RHAH_Settings { maxEventPawns = 13 };
            List<FieldInfo> trustFields = new List<FieldInfo>();
            foreach (FieldInfo field in typeof(RHAH_Settings).GetFields())
                if (field.Name.StartsWith("trust", StringComparison.Ordinal))
                {
                    field.SetValue(settings, -37);
                    trustFields.Add(field);
                }
            RHAH_ContentPreset.Apply(settings, RHAH_ContentRating.AllAges);
            Assert.Equal(0.25f, settings.plagueProgressionFactor);
            Assert.Equal(0.25f, settings.negativeEffectFactor);
            Assert.Equal(0.5f, settings.foodRequestFactor);
            Assert.True(settings.plagueSafeMode);
            Assert.False(settings.plagueContagious);
            Assert.True(settings.peacefulRefusals);
            Assert.False(settings.allowVisitorDoors);
            foreach (string id in new[] { "I-012", "I-038", "I-013", "I-051", "I-055", "I-056", "I-054" })
                Assert.False(settings.IsIncidentEnabled(id));
            foreach (string id in new[] { "I-014", "I-030", "I-045", "I-020", "I-028", "I-053" })
                Assert.True(settings.IsIncidentEnabled(id));

            RHAH_ContentPreset.Apply(settings, RHAH_ContentRating.General);
            Assert.Equal(0.75f, settings.plagueProgressionFactor);
            Assert.Equal(0.75f, settings.negativeEffectFactor);
            Assert.True(settings.IsIncidentEnabled("I-029"));
            Assert.True(settings.IsIncidentEnabled("I-032"));
            Assert.True(settings.IsIncidentEnabled("I-012"));
            Assert.False(settings.IsIncidentEnabled("I-013"));
            Assert.False(settings.refugeeCampEnabled);
            Assert.False(settings.plagueSafeMode);
            Assert.True(settings.plagueContagious);

            RHAH_ContentPreset.Apply(settings, RHAH_ContentRating.Realistic);
            Assert.True(settings.IsIncidentEnabled("I-013"));
            Assert.True(settings.IsIncidentEnabled("I-056"));
            Assert.False(settings.IsIncidentEnabled("I-055"));
            Assert.Equal(1f, settings.plagueProgressionFactor);
            Assert.False(settings.peacefulRefusals);

            RHAH_ContentPreset.Apply(settings, RHAH_ContentRating.Cruel);
            foreach (RHAH_IncidentEntry entry in RHAH_IncidentCatalog.All) Assert.True(settings.IsIncidentEnabled(entry.DisplayId));
            Assert.Equal(1f, settings.negativeEffectFactor);
            Assert.Equal(1f, settings.foodRequestFactor);
            Assert.True(settings.refugeeCampEnabled);
            Assert.Equal(13, settings.maxEventPawns);
            foreach (FieldInfo field in trustFields) Assert.Equal(-37, field.GetValue(settings));
        }

        [Fact]
        public void PeacefulRefusalsKeepOnlyTheThreeSiegeExceptions()
        {
            RHAH_Mod.Settings = new RHAH_Settings();
            RHAH_ContentPreset.Apply(RHAH_Mod.Settings, RHAH_ContentRating.General);
            foreach (RHAH_IncidentEntry entry in RHAH_IncidentCatalog.All)
                Assert.Equal(!RHAH_ArmedDemand.IsSiege(entry.DisplayId), RHAH_ContentPreset.PeacefulRefusal(entry.DisplayId));
        }

        [Fact]
        public void SwitchingAwayFromCruelContentCancelsAlreadyScheduledBombardment()
        {
            RHAH_Mod.Settings = new RHAH_Settings();
            RHAH_WarlordBombardment definition = new RHAH_WarlordBombardment();
            EventChainInstance instance = new EventChainInstance { Stage = 0 };
            Assert.Equal("bombard_children", definition.Evaluate(instance, 0).ActionId);
            RHAH_ContentPreset.Apply(RHAH_Mod.Settings, RHAH_ContentRating.Realistic);
            Assert.Equal(EventChainEnd.Cancelled, definition.Evaluate(instance, 0).End);
            Assert.Equal(EventChainEnd.Cancelled, definition.Apply(instance, 0, "bombard_children").End);
        }

        [Fact]
        public void PresetFoodBudgetHalvesAnOddMealWithoutRoundingItBackUp()
        {
            RHAH_Mod.Settings = new RHAH_Settings();
            RHAH_ContentPreset.Apply(RHAH_Mod.Settings, RHAH_ContentRating.General);
            RHAH_ChoiceRecord request = new RHAH_ChoiceRecord
            {
                Amount = 1, RequiredFoodNutrition = RHAH_DemandFood.MealNutrition * RHAH_ContentPreset.FoodFactor
            };
            Assert.Equal(0.45, RHAH_DemandFood.Remaining(request), 6);
            Assert.Equal(9, RHAH_DemandFood.Units(RHAH_DemandFood.Remaining(request), 0.05));
            request.ArmedFoodNutritionDelivered = 0.2;
            Assert.Equal(0.25, RHAH_DemandFood.Remaining(request), 6);
            request.RequiredFoodNutrition = 0;
            Assert.Equal(0.7, RHAH_DemandFood.Remaining(request), 6);
        }

        [Fact]
        public void NegativeEffectsDoNotReducePositiveMoodOrMutateSharedDiseaseStages()
        {
            RHAH_Mod.Settings = new RHAH_Settings { negativeEffectFactor = 0.25f };
            Assert.Equal(-2f, RHAH_ContentPreset.NegativeEffect(-8f));
            Assert.Equal(8f, RHAH_ContentPreset.NegativeEffect(8f));
            HediffStage stage = new HediffStage
            {
                painOffset = 0.6f,
                capMods = new List<PawnCapacityModifier>
                {
                    new PawnCapacityModifier { offset = -0.4f, setMax = 0.1f }
                }
            };
            HediffStage scaled = RHAH_NegativeHealthEffects.ScaleStage(stage, 0.25f);
            Assert.Equal(0.15f, scaled.painOffset);
            Assert.Equal(-0.1f, scaled.capMods[0].offset);
            Assert.Equal(0.775f, scaled.capMods[0].setMax);
            Assert.Equal(0.6f, stage.painOffset);
            Assert.Equal(-0.4f, stage.capMods[0].offset);
            Assert.Same(scaled, RHAH_NegativeHealthEffects.ScaleStage(stage, 0.25f));
            Assert.Same(stage, RHAH_NegativeHealthEffects.ScaleStage(stage, 1f));
        }

        [Fact]
        public void MoodPatchScalesMemoryTotalsOnceAndLeavesOtherModsAlone()
        {
            ModContentPack pack = (ModContentPack)RuntimeHelpers.GetUninitializedObject(typeof(ModContentPack));
            RHAH_Settings settings = RHAH_Mod.Settings = new RHAH_Settings { negativeEffectFactor = 0.25f };
            typeof(ModSettings).GetProperty(nameof(ModSettings.Mod)).SetValue(settings, new TestMod(pack));
            ThoughtDef def = new ThoughtDef
            {
                defName = "RHAH_TestMood", modContentPack = pack, workerClass = null,
                stages = new List<ThoughtStage> { new ThoughtStage { baseMoodEffect = -8f } }
            };
            Harmony harmony = new Harmony("rhah.content.preset.mood.tests");
            try
            {
                harmony.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.ThoughtNullified)),
                    prefix: new HarmonyMethod(typeof(RHAH_ContentPresetTests), nameof(NeverNullified)));
                harmony.CreateClassProcessor(typeof(RHAH_NegativeMoodPatch)).Patch();
                Thought_Memory memory = new Thought_Memory { def = def, moodOffset = -4 };
                Assert.Equal(-3f, memory.MoodOffset());
                Assert.Equal(-2f, new TestThought { def = def }.MoodOffset());
                def.modContentPack = null;
                Assert.Equal(-12f, memory.MoodOffset());
                def.modContentPack = pack;
                def.stages[0].baseMoodEffect = 8f;
                memory.moodOffset = 0;
                Assert.Equal(8f, memory.MoodOffset());
            }
            finally
            {
                harmony.UnpatchAll(harmony.Id);
            }
        }

        static bool NeverNullified(ref bool __result)
        {
            __result = false;
            return false;
        }

        sealed class TestMod : Mod
        {
            internal TestMod(ModContentPack content) : base(content) { }
        }

        sealed class TestThought : Thought
        {
            public override int CurStageIndex => 0;
        }
    }
}
