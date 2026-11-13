using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.EventMgr;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using Oxygendsl.Core.Events;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_WarlordVictimsTests : IDisposable
    {
        readonly HashSet<string> activeMods = (HashSet<string>)typeof(ModsConfig)
            .GetField("activeModsHashSet", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        readonly HashSet<string> previousMods;
        readonly Game previousGame = Current.Game;
        readonly Harmony harmony = new Harmony("rhah.tests.warlord");
        static int goodwill;
        static int goodwillCalls;

        public RHAH_WarlordVictimsTests()
        {
            previousMods = new HashSet<string>(activeMods);
            Rand.PushState(913);
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            activeMods.Clear();
            activeMods.UnionWith(previousMods);
            Current.Game = previousGame;
            Rand.PopState();
        }

        [Theory]
        [InlineData(false, 0f)]
        [InlineData(false, 1f)]
        [InlineData(true, 0f)]
        [InlineData(true, 1f)]
        public void VictimsStayYoungAndAboveTheirMovementAgeFloor(bool toddlers, float roll)
        {
            float age = RHAH_WarlordCompat.YoungAge(toddlers, roll);
            Assert.True(age > (toddlers ? 1f + 47f / 60f : 3f));
            Assert.True(age >= (toddlers ? 1f + 48f / 60f : 3f + 1f / 60f));
            Assert.True(age < 7f);
        }

        [Theory]
        [InlineData("I-055", 2.802863f, false)]
        [InlineData("I-055", 3.99f, false)]
        [InlineData("I-055", 4f, true)]
        [InlineData("I-054", 20f, true)]
        [InlineData("I-056", 2.802863f, false)]
        public void BabyVictimsDoNotRequireTheImpossibleViolenceGenerationCondition(string id, float age, bool requiresViolence)
        {
            RHAH_Settings previousSettings = RHAH_Mod.Settings;
            try
            {
                RHAH_Mod.Settings = new RHAH_Settings { allowImmobileBabies = true };
                PawnKindDef kind = (PawnKindDef)FormatterServices.GetUninitializedObject(typeof(PawnKindDef));
                kind.race = new ThingDef { race = new RaceProperties { lifeStageAges = new List<LifeStageAge>() } };
                PawnGenerationRequest request = RHAH_GenerationOptimizer.BuildRequest(new RHAH_PawnRequest
                {
                    SourceIncidentDisplayId = id,
                    PawnKind = kind,
                    BiologicalAge = age
                }, null, null);

                Assert.Equal(age, request.FixedBiologicalAge);
                Assert.Equal(requiresViolence, request.MustBeCapableOfViolence);
                Assert.Equal(RHAH_GenerationOptimizer.StageFor(age), request.AllowedDevelopmentalStages);
            }
            finally
            {
                RHAH_Mod.Settings = previousSettings;
            }
        }

        [Fact]
        public void OptionalEventsRequireWarlordsAndKeepTheirAttitudes()
        {
            activeMods.Remove(RHAH_WarlordCompat.WarlordPackage);
            Assert.False(RHAH_WarlordCompat.Available("I-055"));
            Assert.False(RHAH_WarlordCompat.Available("I-056"));
            Assert.True(RHAH_WarlordCompat.Available("I-054"));
            activeMods.Add(RHAH_WarlordCompat.WarlordPackage);
            Assert.True(RHAH_WarlordCompat.Available("I-055"));
            Assert.True(RHAH_WarlordCompat.Available("I-056"));
            Assert.Equal(RHAH_Attitude.Hostile, RHAH_IncidentArrival.For("I-055"));
            Assert.Equal(RHAH_Attitude.LeaningHostile, RHAH_IncidentArrival.For("I-056"));
        }

        [Theory]
        [InlineData("I-055", 10)]
        [InlineData("I-056", 12)]
        public void FamineYearsDoNotMultiplyTheRequestedVictimCount(string id, int maximum)
        {
            for (int i = 0; i < 100; i++)
            {
                int count = RHAH_IncidentScale.Count(id, 10000f, 1000, false);
                Assert.InRange(count, 1, maximum);
                Assert.Equal(count, RHAH_IncidentScale.MultiplyCount(id, count, true, 4f));
            }
        }

        [Theory]
        [InlineData(true, true, false, false, true)]
        [InlineData(false, true, false, false, false)]
        [InlineData(true, false, false, false, false)]
        [InlineData(true, true, true, false, false)]
        [InlineData(true, true, false, true, false)]
        public void BombardmentExcludesEachProtectedOrAttackedChild(bool alive, bool onMap, bool protectedStatus, bool attacked, bool expected)
        {
            Assert.Equal(expected, RHAH_WarlordBombardment.Eligible(alive, onMap, protectedStatus, attacked));
        }

        [Fact]
        public void MutilatedChildrenCanOnlyBeTransferredToTheUnderground()
        {
            Assert.False(RHAH_WarlordCompat.AllowsDestination("I-056", false));
            Assert.True(RHAH_WarlordCompat.AllowsDestination("I-056", true));
            Assert.False(RHAH_WarlordCompat.AllowsDestination("I-055", true));
            Assert.True(RHAH_WarlordCompat.AllowsDestination("I-005", false));
            Faction underground = Underground();
            activeMods.Remove(RHAH_WarlordCompat.UndergroundPackage);
            Assert.False(RHAH_WarlordCompat.AcceptsTransfer(underground, "I-056"));
            activeMods.Add(RHAH_WarlordCompat.UndergroundPackage);
            Assert.True(RHAH_WarlordCompat.AcceptsTransfer(underground, "I-056"));
        }

        [Theory]
        [InlineData(-100, 0, false)]
        [InlineData(-26, 0, false)]
        [InlineData(-25, 1, false)]
        [InlineData(-1, 1, false)]
        [InlineData(0, 0, false)]
        [InlineData(74, 0, false)]
        [InlineData(75, 0, true)]
        [InlineData(100, 0, true)]
        public void ManyActualArrivalsSettleUndergroundGoodwillAndGiftOnlyOnce(int initial, int gain, bool gift)
        {
            activeMods.Add(RHAH_WarlordCompat.UndergroundPackage);
            goodwill = initial;
            goodwillCalls = 0;
            Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
            World world = (World)FormatterServices.GetUninitializedObject(typeof(World));
            world.factionManager = new FactionManager();
            Current.Game.World = world;
            Faction player = new Faction { def = new FactionDef { isPlayer = true } };
            typeof(FactionManager).GetField("ofPlayer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world.factionManager, player);
            harmony.Patch(AccessTools.Method(typeof(Faction), nameof(Faction.GoodwillWith)),
                prefix: new HarmonyMethod(typeof(RHAH_WarlordVictimsTests), nameof(ReadGoodwill)));
            harmony.Patch(AccessTools.Method(typeof(Faction), nameof(Faction.TryAffectGoodwillWith)),
                prefix: new HarmonyMethod(typeof(RHAH_WarlordVictimsTests), nameof(AffectGoodwill)));
            Faction underground = Underground();
            Verse.Pawn child = new Verse.Pawn { def = new ThingDef { race = new RaceProperties() } };
            child.health = new Pawn_HealthTracker(child);
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord { DisplayId = "I-056" };

            for (int i = 0; i < 12; i++) RHAH_AllyTransfer.OnArrival(record, underground, child, 500);

            Assert.Equal(initial + gain, goodwill);
            Assert.Equal(gain, goodwillCalls);
            Assert.Equal(gain, record.AllyGoodwillGranted);
            Assert.True(record.AllyGiftChecked);
            Assert.Equal(gift, record.AllyGiftDueTick >= 0);
            Assert.Equal(0f, record.AllyTransferredValue);
            int savedDue = record.AllyGiftDueTick;
            goodwill = 100;
            RHAH_AllyTransfer.OnArrival(record, underground, child, 100000);
            Assert.Equal(savedDue, record.AllyGiftDueTick);
            Assert.Equal(gain, goodwillCalls);
        }

        [Theory]
        [InlineData(9999, false)]
        [InlineData(10000, false)]
        [InlineData(10001, true)]
        public void TeleportFallbackWaitsStrictlyMoreThanFourHours(int elapsed, bool expected)
        {
            Assert.Equal(expected, RHAH_AllyDeparture.Stalled(500 + elapsed, 500));
        }

        [Fact]
        public void SavedBombardmentKeepsItsOriginalTwelveHourDeadline()
        {
            Current.Game = null;
            int due = RHAH_WarlordBombardment.NextTick(500, RHAH_WarlordBombardment.Delay);
            EventChainInstance saved = new EventChainInstance
            {
                Id = 7, DefinitionId = "I-055", SiteId = 0, StartedTick = 500, NextTick = due,
                Payload = "I-055|500|0"
            };
            EventChainProviderState partition = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 8,
                Instances = new List<EventChainInstance> { saved }
            };
            EventChainRuntime restored = new EventChainRuntime(new List<EventChainProviderState> { partition });
            restored.Register(new RHAH_EventChainProvider());
            restored.Activate(20000);
            restored.Tick(due - 1);
            Assert.Equal(30500, saved.NextTick);
            Assert.Empty(saved.Receipts);
            Assert.Equal(0L, restored.CallbackCount);
            RHAH_WarlordBombardment definition = new RHAH_WarlordBombardment();
            EventChainDecision decision = definition.Evaluate(saved, due);
            Assert.Equal("bombard_children", decision.ActionId);
            Assert.Equal(1, decision.Stage);
            saved.Stage = 1;
            Assert.Equal("finish_bombardment", definition.Evaluate(saved, due + 120).ActionId);
            Assert.Equal(int.MaxValue, RHAH_WarlordBombardment.NextTick(int.MaxValue - 1, 30000));
        }

        static Faction Underground()
        {
            return new Faction
            {
                def = new FactionDef
                {
                    defName = RHAH_WarlordCompat.UndergroundFaction,
                    humanlikeFaction = true,
                    dropPodIncoming = new ThingDef { skyfaller = new SkyfallerProperties { ticksToImpactRange = new IntRange(200, 200) } }
                }
            };
        }

        static bool ReadGoodwill(ref int __result)
        {
            __result = goodwill;
            return false;
        }

        static bool AffectGoodwill(int goodwillChange, ref bool __result)
        {
            goodwill += goodwillChange;
            goodwillCalls++;
            __result = true;
            return false;
        }
    }
}
