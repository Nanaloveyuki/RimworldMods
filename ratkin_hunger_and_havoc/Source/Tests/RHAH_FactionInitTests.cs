using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using Oxygendsl.Core.Events;
using HungerAndHavoc.Pawn;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_FactionInitTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousProgramState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly ScribeSaver previousSaver = Scribe.saver;
        readonly ScribeLoader previousLoader = Scribe.loader;
        readonly LoadSaveMode previousMode = Scribe.mode;
        readonly bool previousProfiling = DeepProfiler.enabled;
        readonly object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
        readonly List<FactionDef> previousFactionDefs = new List<FactionDef>(DefDatabase<FactionDef>.AllDefsListForReading);
        readonly Dictionary<FieldInfo, object> previousExpansionFlags = new Dictionary<FieldInfo, object>();
        readonly Harmony harmony = new Harmony("rhah.faction.init.tests");
        readonly string path = Path.GetTempFileName();
        readonly FactionDef[] previousDefs;
        readonly FactionDef[] defs = new FactionDef[5];
        readonly GameComponent_RHAH_Game component;
        readonly Faction player;
        readonly Faction enemy;
        readonly Faction ally;
        static int generated;
        readonly int previousMainThreadId;

        static RHAH_FactionInitTests()
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RHAH_ContentApplierTests).TypeHandle);
            bool initializing = UnityDataInitializer.initializing;
            UnityDataInitializer.initializing = true;
            try
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(UnityData).TypeHandle);
            }
            finally
            {
                UnityDataInitializer.initializing = initializing;
            }
        }

        public RHAH_FactionInitTests()
        {
            previousMainThreadId = (int)Field(typeof(UnityData), "mainThreadId").GetValue(null);
            Field(typeof(UnityData), "mainThreadId").SetValue(null, System.Threading.Thread.CurrentThread.ManagedThreadId);
            FieldInfo binding = Field(typeof(DefOfHelper), "bindingNow");
            bool previousBinding = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RHAH_DefOf).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(FactionDefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previousBinding);
            }

            previousDefs = new[]
            {
                RHAH_DefOf.RHAH_Faction_Hostile,
                RHAH_DefOf.RHAH_Faction_LeaningHostile,
                RHAH_DefOf.RHAH_Faction_Neutral,
                RHAH_DefOf.RHAH_Faction_LeaningFriendly,
                RHAH_DefOf.RHAH_Faction_Friendly
            };
            for (int i = 0; i < defs.Length; i++)
            {
                defs[i] = new FactionDef { defName = "RHAH_InitTest_" + i, hidden = true, naturalEnemy = i == 0 };
                DefDatabase<FactionDef>.Add(defs[i]);
            }
            SetDefs(defs);
            foreach (string name in new[] { "royaltyActive", "anomalyActive", "odysseyActive" })
            {
                FieldInfo flag = Field(typeof(ModsConfig), name);
                if (flag == null) continue;
                previousExpansionFlags.Add(flag, flag.GetValue(null));
                flag.SetValue(null, false);
            }
            Rand.PushState(1701);
            Current.Game = Uninitialized<Game>();
            Current.Game.components = new List<GameComponent>();
            Current.Game.tickManager = new TickManager();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            World world = Uninitialized<World>();
            world.factionManager = new FactionManager();
            world.worldPawns = new WorldPawns();
            world.worldObjects = new WorldObjectsHolder();
            Current.Game.World = world;
            Current.ProgramState = ProgramState.Entry;
            player = AddFaction(new FactionDef { defName = "RHAH_InitTest_Player", isPlayer = true }, 70);
            enemy = AddFaction(new FactionDef { defName = "RHAH_InitTest_Enemy", permanentEnemy = true }, 71);
            ally = AddFaction(new FactionDef { defName = "RHAH_InitTest_Ally" }, 72);
            DefDatabase<FactionDef>.Add(player.def);
            DefDatabase<FactionDef>.Add(enemy.def);
            DefDatabase<FactionDef>.Add(ally.def);
            Field(typeof(FactionManager), "ofPlayer").SetValue(world.factionManager, player);
            SetPair(player, enemy, FactionRelationKind.Hostile, -45);
            SetPair(player, ally, FactionRelationKind.Ally, 90);
            SetPair(enemy, ally, FactionRelationKind.Hostile, -65);
            component = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components.Add(component);
            RHAH_Mod.Settings = new RHAH_Settings();
            Scribe.saver = new ScribeSaver();
            Scribe.loader = new ScribeLoader();
            Scribe.mode = LoadSaveMode.Inactive;
            DeepProfiler.enabled = false;
            Field(typeof(Prefs), "data").SetValue(null, new PrefsData { devMode = false });
            generated = 0;
            harmony.Patch(typeof(FactionGenerator).GetMethod(nameof(FactionGenerator.CreateFactionAndAddToManager), new[] { typeof(FactionDef) }),
                prefix: new HarmonyMethod(typeof(RHAH_FactionInitTests), nameof(GenerateFaction)));
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            Field(typeof(UnityData), "mainThreadId").SetValue(null, previousMainThreadId);
            Scribe.ForceStop();
            Scribe.saver = previousSaver;
            Scribe.loader = previousLoader;
            Scribe.mode = previousMode;
            DeepProfiler.enabled = previousProfiling;
            Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
            DefDatabase<FactionDef>.Clear();
            foreach (FactionDef def in previousFactionDefs) DefDatabase<FactionDef>.Add(def);
            SetDefs(previousDefs);
            foreach (KeyValuePair<FieldInfo, object> flag in previousExpansionFlags) flag.Key.SetValue(null, flag.Value);
            Current.Game = previousGame;
            Current.ProgramState = previousProgramState;
            RHAH_Mod.Settings = previousSettings;
            Rand.PopState();
            File.Delete(path);
        }

        [Theory]
        [InlineData(0, false, false)]
        [InlineData(2, false, false)]
        [InlineData(5, false, false)]
        [InlineData(0, true, false)]
        [InlineData(2, false, true)]
        public void FinalizeInit_CompletesOnlyMissingFactionsAndPreservesSavedRelations(int savedCount, bool globalDisabled, bool saveDisabled)
        {
            Faction[] saved = new Faction[savedCount];
            FactionRelation[] savedRelations = new FactionRelation[savedCount];
            for (int i = 0; i < savedCount; i++)
            {
                saved[i] = AddFaction(defs[i], 80 + i);
                SetPair(saved[i], player, FactionRelationKind.Ally, 90);
                SetPair(saved[i], enemy, FactionRelationKind.Neutral, 35);
                savedRelations[i] = saved[i].RelationWith(enemy);
            }
            if (savedCount > 1) SetPair(saved[0], saved[1], FactionRelationKind.Ally, 87);
            FactionRelation playerEnemy = player.RelationWith(enemy);
            FactionRelation playerAlly = player.RelationWith(ally);
            FactionRelation outsidePair = enemy.RelationWith(ally);
            RHAH_Mod.Settings.enableNewContent = !globalDisabled;
            if (saveDisabled) component.DisableNewContent();

            component.FinalizeInit();
            component.FinalizeInit();
            RHAH_AttitudeFactions.EnsureAll();

            Assert.Equal(5 - savedCount, generated);
            Assert.Equal(8, Find.FactionManager.AllFactionsListForReading.Count);
            for (int i = 0; i < defs.Length; i++)
            {
                Faction faction = Find.FactionManager.FirstFactionOfDef(defs[i]);
                Assert.False(faction.HasGoodwill);
                AssertPair(player, faction, i == 0 ? FactionRelationKind.Hostile : FactionRelationKind.Neutral, i == 0 ? -100 : 0);
                if (i < savedCount)
                {
                    Assert.Same(saved[i], faction);
                    Assert.Equal(80 + i, faction.loadID);
                    Assert.Equal("Saved faction " + (80 + i), faction.Name);
                    Assert.Same(savedRelations[i], faction.RelationWith(enemy));
                    AssertPair(faction, enemy, FactionRelationKind.Neutral, 35);
                }
                else
                {
                    AssertPair(faction, enemy, i == 4 ? FactionRelationKind.Neutral : FactionRelationKind.Hostile, i == 4 ? 0 : -100);
                    AssertPair(faction, ally, i == 0 ? FactionRelationKind.Hostile : FactionRelationKind.Neutral, i == 0 ? -100 : 0);
                    for (int j = 0; j < defs.Length; j++)
                    {
                        if (i != j) AssertPair(faction, Find.FactionManager.FirstFactionOfDef(defs[j]), FactionRelationKind.Neutral, 0);
                    }
                }
            }
            if (savedCount > 1) AssertPair(saved[0], saved[1], FactionRelationKind.Ally, 87);
            Assert.Same(playerEnemy, player.RelationWith(enemy));
            Assert.Same(playerAlly, player.RelationWith(ally));
            Assert.Same(outsidePair, enemy.RelationWith(ally));
            AssertPair(player, enemy, FactionRelationKind.Hostile, -45);
            AssertPair(player, ally, FactionRelationKind.Ally, 90);
            AssertPair(enemy, ally, FactionRelationKind.Hostile, -65);
            Assert.Equal(saveDisabled, component.NewContentDisabled);
            Assert.Equal(!globalDisabled, RHAH_Mod.Settings.enableNewContent);
            Assert.Empty(component.PendingIncidentDisplayIds);
            Assert.Empty(component.OpenChoices);
            Assert.Empty(component.ActiveGenerationBatches);
            Assert.Empty(Find.WorldObjects.AllWorldObjects);
            Assert.Empty(Find.WorldPawns.AllPawnsAliveOrDead);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Require_InitializesNewFactionPairsInEitherOrderWithoutDuplicates(bool reverse)
        {
            for (int index = 0; index < defs.Length; index++)
            {
                int i = reverse ? defs.Length - 1 - index : index;
                Faction faction = RHAH_AttitudeFactions.Require((RHAH_Attitude)i);
                Assert.Same(faction, RHAH_AttitudeFactions.Require((RHAH_Attitude)i));
                AssertPair(player, faction, i == 0 ? FactionRelationKind.Hostile : FactionRelationKind.Neutral, i == 0 ? -100 : 0);
            }

            component.FinalizeInit();

            Assert.Equal(5, generated);
            Assert.Equal(8, Find.FactionManager.AllFactionsListForReading.Count);
            for (int i = 0; i < defs.Length; i++)
            {
                Faction faction = RHAH_AttitudeFactions.Resolve((RHAH_Attitude)i);
                AssertPair(faction, enemy, i == 4 ? FactionRelationKind.Neutral : FactionRelationKind.Hostile, i == 4 ? 0 : -100);
                AssertPair(faction, ally, i == 0 ? FactionRelationKind.Hostile : FactionRelationKind.Neutral, i == 0 ? -100 : 0);
                for (int j = i + 1; j < defs.Length; j++)
                {
                    AssertPair(faction, RHAH_AttitudeFactions.Resolve((RHAH_Attitude)j), FactionRelationKind.Neutral, 0);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AddedComponent_PreparesAnEmptyProviderWithoutStartingGameplay(bool disabled)
        {
            if (disabled) component.DisableNewContent();
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());

            component.FinalizeInit();
            RHAH_EventChainMigration.PrepareGame(runtime);
            EventChainProviderState state = runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey);
            RHAH_EventChainMigration.PrepareGame(runtime);
            runtime.Activate(0);
            runtime.Tick(GenDate.TicksPerDay);

            Assert.Same(state, runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey));
            Assert.Equal(disabled ? EventChainMode.Disabled : EventChainMode.Active, state.Mode);
            Assert.Equal("completed", component.EventChainMigrationStatus);
            Assert.Equal(1, state.NextId);
            Assert.Empty(state.Instances);
            Assert.Equal(0L, runtime.CallbackCount);
            Assert.Empty(component.LegacyEventChains);
            Assert.Empty(component.LongFollows);
            Assert.Empty(component.PendingIncidentDisplayIds);
            Assert.Empty(component.OpenChoices);
            Assert.Empty(Find.WorldObjects.AllWorldObjects);
            Assert.Empty(Find.WorldPawns.AllPawnsAliveOrDead);
        }

        [Fact]
        public void CompletedFactions_SaveAndReloadKeepIdsNamesAndRelationsWithoutGeneratingAgain()
        {
            component.FinalizeInit();
            List<Faction> original = new List<Faction>(Find.FactionManager.AllFactionsListForReading);
            Scribe.saver.InitSaving(path, "record");
            Find.FactionManager.ExposeData();
            Scribe.saver.FinalizeSaving();

            FactionManager loaded = new FactionManager();
            Find.World.factionManager = loaded;
            Scribe.loader.InitLoading(path);
            Scribe.loader.curParent = loaded;
            loaded.ExposeData();
            Scribe.loader.crossRefs.RegisterForCrossRefResolve(loaded);
            Scribe.loader.initer.RegisterForPostLoadInit(loaded);
            Scribe.loader.FinalizeLoading();

            component.FinalizeInit();
            component.FinalizeInit();

            Assert.Equal(5, generated);
            Assert.Equal(original.Count, loaded.AllFactionsListForReading.Count);
            for (int i = 0; i < original.Count; i++)
            {
                Faction faction = loaded.FirstFactionOfDef(original[i].def);
                Assert.NotSame(original[i], faction);
                Assert.Equal(original[i].loadID, faction.loadID);
                Assert.Equal(original[i].Name, faction.Name);
                for (int j = 0; j < original.Count; j++)
                {
                    if (i == j) continue;
                    FactionRelation relation = original[i].RelationWith(original[j]);
                    AssertPair(faction, loaded.FirstFactionOfDef(original[j].def), relation.kind, relation.baseGoodwill);
                }
            }
        }

        static bool GenerateFaction(FactionDef facDef)
        {
            Faction faction = new Faction { def = facDef, loadID = 100 + generated++, Name = facDef.defName };
            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++) faction.TryMakeInitialRelationsWith(factions[i]);
            factions.Add(faction);
            return false;
        }

        static Faction AddFaction(FactionDef def, int id)
        {
            Faction faction = new Faction { def = def, loadID = id, Name = "Saved faction " + id };
            Find.FactionManager.AllFactionsListForReading.Add(faction);
            return faction;
        }

        static void SetPair(Faction first, Faction second, FactionRelationKind kind, int goodwill)
        {
            first.SetRelation(new FactionRelation { other = second, kind = kind, baseGoodwill = goodwill });
            second.RelationWith(first).baseGoodwill = goodwill;
        }

        static void AssertPair(Faction first, Faction second, FactionRelationKind kind, int goodwill)
        {
            Assert.Equal(kind, first.RelationWith(second).kind);
            Assert.Equal(kind, second.RelationWith(first).kind);
            Assert.Equal(goodwill, first.RelationWith(second).baseGoodwill);
            Assert.Equal(goodwill, second.RelationWith(first).baseGoodwill);
        }

        static void SetDefs(FactionDef[] values)
        {
            RHAH_DefOf.RHAH_Faction_Hostile = values[0];
            RHAH_DefOf.RHAH_Faction_LeaningHostile = values[1];
            RHAH_DefOf.RHAH_Faction_Neutral = values[2];
            RHAH_DefOf.RHAH_Faction_LeaningFriendly = values[3];
            RHAH_DefOf.RHAH_Faction_Friendly = values[4];
        }

        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        }
    }
}
