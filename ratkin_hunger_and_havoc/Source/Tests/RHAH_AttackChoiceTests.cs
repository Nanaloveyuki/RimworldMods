using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_AttackChoiceTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousProgramState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly IRHAH_ApiHost previousHost;
        readonly List<IRHAH_PawnBehavior> previousBehaviors;
        readonly FactionDef previousHostileDef;
        readonly Faction player;
        readonly FactionDef previousNeutralDef;
        readonly Faction hostile;
        readonly Map map;
        readonly GameComponent_RHAH_Game choices;


        static RHAH_AttackChoiceTests()
        {
            using (Log.LockMessages())
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Need).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Pawn_InventoryTracker).TypeHandle);
            }
        }
        public RHAH_AttackChoiceTests()
        {
            FieldInfo binding = Field(typeof(DefOfHelper), "bindingNow");
            bool previousBinding = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(RHAH_DefOf).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(JobDefOf).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(HediffDefOf).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ThingDefOf).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(DutyDefOf).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(MessageTypeDefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previousBinding);
            }
            previousHostileDef = RHAH_DefOf.RHAH_Faction_Hostile;
            previousNeutralDef = RHAH_DefOf.RHAH_Faction_Neutral;
            previousHost = (IRHAH_ApiHost)Field(typeof(RHAH_Api), "host").GetValue(null);
            previousBehaviors = new List<IRHAH_PawnBehavior>(
                (List<IRHAH_PawnBehavior>)Field(typeof(RHAH_PawnBehaviors), "handlers").GetValue(null));
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_Api.Bind(new RHAH_ApiHost());
            RHAH_Mod.Settings = new RHAH_Settings();
            Current.Game = Uninitialized<Game>();
            Current.ProgramState = ProgramState.Entry;
            Current.Game.components = new List<GameComponent>();
            Current.Game.tickManager = new TickManager();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            Current.Game.questManager = new QuestManager();
            World world = Uninitialized<World>();
            world.factionManager = new FactionManager();
            world.worldPawns = new WorldPawns();
            world.worldObjects = new WorldObjectsHolder();
            Current.Game.World = world;
            player = new Faction { def = new FactionDef { isPlayer = true }, loadID = 70 };
            hostile = new Faction { def = new FactionDef { hidden = true }, loadID = 71 };
            world.factionManager.AllFactionsListForReading.Add(player);
            world.factionManager.AllFactionsListForReading.Add(hostile);
            Field(typeof(FactionManager), "ofPlayer").SetValue(world.factionManager, player);
            RHAH_DefOf.RHAH_Faction_Hostile = hostile.def;
            SetPlayerRelation(FactionRelationKind.Hostile, -100);
            map = Uninitialized<Map>();
            map.uniqueID = 41;
            map.components = new List<MapComponent>();
            map.events = new MapEvents(map);
            map.attackTargetsCache = new AttackTargetsCache(map);
            Find.Maps.Add(map);
            choices = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components.Add(choices);
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            Current.ProgramState = previousProgramState;
            RHAH_Mod.Settings = previousSettings;
            RHAH_DefOf.RHAH_Faction_Hostile = previousHostileDef;
            RHAH_DefOf.RHAH_Faction_Neutral = previousNeutralDef;
            RHAH_Api.Bind(previousHost);
            RHAH_PawnBehaviors.ResetForTests();
            for (int i = 0; i < previousBehaviors.Count; i++)
            {
                RHAH_PawnBehaviors.Register(previousBehaviors[i]);
            }
        }

        [Fact]
        public void DisablingArmedDemandClosesOrphanRequestWithoutChangingSettledHistory()
        {
            RHAH_ChoiceRecord orphan = new RHAH_ChoiceRecord
            {
                Id = 1,
                DisplayId = "I-014",
                Kind = RHAH_RequestKind.SimpleMeal,
                ExpireTick = 500
            };
            RHAH_ChoiceRecord delivered = new RHAH_ChoiceRecord
            {
                Id = 2,
                DisplayId = "I-030",
                Kind = RHAH_RequestKind.SimpleMeal,
                Settled = RHAH_ChoiceAction.Deliver
            };
            List<RHAH_ChoiceRecord> records = (List<RHAH_ChoiceRecord>)choices.OpenChoices;
            records.Add(orphan);
            records.Add(delivered);
            RHAH_Mod.Settings.armedDemandEnabled = false;

            RHAH_ChoiceRuntime.Tick(choices, 1);

            Assert.Equal(RHAH_ChoiceAction.Ignore, orphan.Settled);
            Assert.Equal(RHAH_ChoiceAction.Deliver, delivered.Settled);
            RHAH_Mod.Settings.armedDemandEnabled = true;
            RHAH_ChoiceRuntime.Tick(choices, 500);
            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettleRequest(choices, orphan.Id, RHAH_ChoiceAction.Deliver, 500, true));
            Assert.Equal(RHAH_ChoiceAction.Ignore, orphan.Settled);
        }

        [Theory]
        [InlineData(RHAH_ChoiceAction.Reject)]
        [InlineData(RHAH_ChoiceAction.Attack)]
        [InlineData(RHAH_ChoiceAction.Capture)]
        [InlineData(RHAH_ChoiceAction.Enslave)]
        public void SwarmProvocationOverridesDepartureAndCannotBeReversedByStaleFood(RHAH_ChoiceAction requested)
        {
            RHAH_Mod.Settings.fightingEnabled = false;
            RHAH_Mod.Settings.batchTurnsHostile = false;
            Verse.Pawn first = Visitor(1);
            Verse.Pawn second = Visitor(2);
            foreach (Verse.Pawn pawn in new[] { first, second })
            {
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
                comp.ApplySeed(new RHAH_PawnSeed("I-052", 7, 0, RHAH_PawnRole.Thief, RHAH_Lifecycle.SeekingFood));
                comp.State.foodWaitUntilTick = 20;
                comp.State.leaveAfterGameTick = 20;
            }
            map.mapPawns = Uninitialized<MapPawns>();
            Field(typeof(MapPawns), "pawnsSpawned").SetValue(map.mapPawns, new List<Verse.Pawn> { first, second });
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord
            {
                DisplayId = "I-052", Choice = RHAH_ChoiceKind.Visitors,
                BatchId = 7, MapId = map.uniqueID, ExpireTick = 10
            };
            record.PawnLoadIds.Add(first.thingIDNumber);
            record.PawnLoadIds.Add(second.thingIDNumber);
            RHAH_ChoiceRuntime.Open(choices, record);
            RHAH_ChoiceRuntime.Tick(choices, 100);
            Assert.True(record.Open);
            Assert.Equal(RHAH_Attitude.Neutral, RHAH_Api.Get(first).Attitude);

            Assert.Equal(requested, RHAH_ChoiceRuntime.TrySettle(choices, record.Id, requested, 100, true, false));
            RHAH_ChoiceRuntime.Apply(record);
            foreach (Verse.Pawn pawn in new[] { first, second })
            {
                Assert.Equal(RHAH_Attitude.Hostile, RHAH_Api.Get(pawn).Attitude);
                Assert.Equal(RHAH_Lifecycle.SeekingFood, RHAH_Api.Get(pawn).Lifecycle);
                Assert.True(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Fight));
                Assert.False(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.ExitMap));
                Assert.False(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Steal));
                Assert.Equal(-1, CompRHAH_Pawn.TryGet(pawn).State.foodWaitUntilTick);
                Assert.Equal(-1, CompRHAH_Pawn.TryGet(pawn).State.leaveAfterGameTick);
                Assert.False(pawn.IsPrisoner);
                Assert.False(pawn.IsSlave);
            }
            RHAH_ChoiceRecord stale = new RHAH_ChoiceRecord
            {
                DisplayId = "I-052", Choice = RHAH_ChoiceKind.Visitors,
                BatchId = 7, MapId = map.uniqueID
            };
            stale.PawnLoadIds.Add(first.thingIDNumber);
            RHAH_ChoiceRuntime.Open(choices, stale);
            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, stale.Id, RHAH_ChoiceAction.Feed, 100, true, true));
            RHAH_BatchAttitude.AcceptFood(first);
            Assert.Equal(RHAH_Attitude.Hostile, RHAH_Api.Get(first).Attitude);
        }

        [Fact]
        public void PleadingJoinCannotAcceptDirectFoodDispatch()
        {
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord
            {
                DisplayId = "I-053", Choice = RHAH_ChoiceKind.Visitors, MapId = map.uniqueID
            };
            RHAH_ChoiceRuntime.Open(choices, record);
            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, RHAH_ChoiceAction.Feed, 0, true, true));
            Assert.True(record.Open);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AttackDoesNotOrderFightersToLeave(bool leaveTogether)
        {
            RHAH_Mod.Settings.batchLeavesTogether = leaveTogether;
            RHAH_Mod.Settings.batchTurnsHostile = false;
            Verse.Pawn pawn = Visitor(1);
            PawnDuty duty = Uninitialized<PawnDuty>();
            pawn.mindState.duty = duty;

            Assert.Equal(RHAH_ChoiceAction.Attack, Attack(pawn));

            Assert.Equal(RHAH_Attitude.Hostile, RHAH_Api.Get(pawn).Attitude);
            Assert.Equal(RHAH_Attitude.Neutral, RHAH_Api.Get(pawn).AttitudeAtArrival);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, RHAH_Api.Get(pawn).Lifecycle);
            Assert.True(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Fight));
            Assert.False(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.ExitMap));
            Assert.Same(duty, pawn.mindState.duty);
        }

        [Theory]
        [InlineData(true, RHAH_Lifecycle.Leaving)]
        [InlineData(false, RHAH_Lifecycle.SeekingFood)]
        public void FightDisabledRetainsConfiguredDeparture(bool leaveTogether, RHAH_Lifecycle expected)
        {
            RHAH_Mod.Settings.fightingEnabled = false;
            RHAH_Mod.Settings.batchLeavesTogether = leaveTogether;
            Verse.Pawn pawn = Visitor(1);

            Assert.Equal(RHAH_ChoiceAction.Attack, Attack(pawn));

            Assert.False(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Fight));
            Assert.Equal(expected, RHAH_Api.Get(pawn).Lifecycle);
        }

        [Fact]
        public void MixedFightGatesDoNotSendFightersOrUnselectedMembersAway()
        {
            Verse.Pawn fighter = Visitor(1);
            Verse.Pawn pacifist = Visitor(2);
            Verse.Pawn unselected = Visitor(3);
            RHAH_Api.SetGate(pacifist, RHAH_BehaviorGate.Fight, false);
            PawnDuty duty = Uninitialized<PawnDuty>();
            fighter.mindState.duty = duty;

            Assert.Equal(RHAH_ChoiceAction.Attack, Attack(fighter, pacifist));

            Assert.Equal(RHAH_Lifecycle.SeekingFood, RHAH_Api.Get(fighter).Lifecycle);
            Assert.Equal(RHAH_Lifecycle.Leaving, RHAH_Api.Get(pacifist).Lifecycle);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, RHAH_Api.Get(unselected).Lifecycle);
            Assert.Equal(RHAH_Attitude.Neutral, RHAH_Api.Get(unselected).Attitude);
            Assert.Same(duty, fighter.mindState.duty);
        }

        [Fact]
        public void AttackImmediatelyRepairsRelationsAndExistingTargets()
        {
            SetPlayerRelation(FactionRelationKind.Neutral, 0);
            Verse.Pawn pawn = Visitor(1);
            Building_TurretGun target = Uninitialized<Building_TurretGun>();
            target.def = Uninitialized<ThingDef>();
            target.def.category = ThingCategory.Building;
            target.SetFactionDirect(player);
            Field(typeof(Thing), "mapIndexOrState").SetValue(target, (sbyte)0);
            map.attackTargetsCache.Notify_ThingSpawned(target);
            Assert.DoesNotContain(target, map.attackTargetsCache.TargetsHostileToFaction(hostile));

            Assert.Equal(RHAH_ChoiceAction.Attack, Attack(pawn));

            Assert.True(hostile.HostileTo(player));
            Assert.True(player.HostileTo(hostile));
            Assert.Equal(-100, hostile.RelationWith(player).baseGoodwill);
            Assert.Equal(-100, player.RelationWith(hostile).baseGoodwill);
            Assert.Contains(target, map.attackTargetsCache.TargetsHostileToFaction(hostile));
        }


        [Theory]
        [InlineData("gate")]
        [InlineData("downed")]
        [InlineData("child")]
        [InlineData("baby")]
        [InlineData("neutral")]
        public void VisitorCombatRejectsGateHealthDevelopmentAndAttitudeBoundaries(string blocked)
        {
            Verse.Pawn pawn = Visitor(1);
            pawn.kindDef = new PawnKindDef();
            CompRHAH_Pawn.TryGet(pawn).State.SetAttitude(RHAH_Attitude.Hostile);
            if (blocked == "gate")
            {
                RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Fight, false);
            }
            else if (blocked == "downed")
            {
                Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Down);
            }
            else if (blocked == "child" || blocked == "baby")
            {
                LifeStageAge age = Uninitialized<LifeStageAge>();
                age.def = new LifeStageDef { developmentalStage = blocked == "child" ? DevelopmentalStage.Child : DevelopmentalStage.Baby };
                pawn.def.race.lifeStageAges = new List<LifeStageAge> { age };
                pawn.ageTracker = new Pawn_AgeTracker(pawn);
                Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
                RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Fight, true);
            }
            else
            {
                CompRHAH_Pawn.TryGet(pawn).State.SetAttitude(RHAH_Attitude.Neutral);
                RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Fight, true);
            }
            Assert.False(JobGiver_RHAH_Visitor.CanFight(pawn));
        }



        [Theory]
        [InlineData(RHAH_Lifecycle.Released)]
        [InlineData(RHAH_Lifecycle.Dead)]
        public void TerminalIdentityNeverBlocksWorkForResidualExpiredStay(RHAH_Lifecycle lifecycle)
        {
            Verse.Pawn pawn = Visitor(1);
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            comp.SetStay((int)RHAH_StayKind.Shelter, 100, 100);
            RHAH_Api.SetLifecycle(pawn, lifecycle);
            Assert.Equal(0, comp.State.stayKind);
            Assert.Equal(-1, comp.State.leaveAfterGameTick);
            pawn.SetFactionDirect(player);
            comp.SetStay((int)RHAH_StayKind.Shelter, 100, 0);
            Assert.False(RHAH_VisitorStay.BlocksAssignedWork(pawn, 200));
            Assert.False(RHAH_VisitorStay.Tick(pawn, 200));
            Assert.Same(player, pawn.Faction);
            Assert.Equal(lifecycle, comp.State.lifecycle);
        }

        [Theory]
        [InlineData((int)RHAH_StayKind.Shelter)]
        [InlineData((int)RHAH_StayKind.Hire)]
        [InlineData((int)RHAH_StayKind.Recruit)]
        public void WorkerMealsPreserveContractAndAllowWorkUntilExpiry(int kindValue)
        {
            RHAH_StayKind kind = (RHAH_StayKind)kindValue;
            Verse.Pawn pawn = Visitor(1);
            pawn.SetFactionDirect(player);
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            int deadline = RHAH_VisitorRules.BeginStay(0, kind, 5, 240);
            comp.SetStay((int)kind, deadline, deadline);
            LifeStageAge age = Uninitialized<LifeStageAge>();
            age.def = new LifeStageDef { bodySizeFactor = 1f, foodMaxFactor = 1f };
            pawn.def.race.lifeStageAges = new List<LifeStageAge> { age };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            pawn.needs = new Pawn_NeedsTracker();
            pawn.needs.food = Uninitialized<Need_Food>();
            Field(typeof(Need), "pawn").SetValue(pawn.needs.food, pawn);
            Field(typeof(Need), "curLevelInt").SetValue(pawn.needs.food, pawn.needs.food.MaxLevel);

            Assert.False(RHAH_Feeding.TryComplete(pawn));
            Assert.Equal(deadline, comp.State.leaveAfterGameTick);
            Assert.Equal(deadline, comp.State.stayRemainingTicks);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, comp.State.lifecycle);
            Assert.False(comp.State.hasBeenFed);
            Assert.False(RHAH_VisitorStay.BlocksAssignedWork(pawn, deadline - 1));
            Assert.True(RHAH_VisitorStay.BlocksAssignedWork(pawn, deadline));
        }

        [Theory]
        [InlineData((int)RHAH_StayKind.Shelter)]
        [InlineData((int)RHAH_StayKind.Hire)]
        [InlineData((int)RHAH_StayKind.Recruit)]
        public void WorkerMealsAreNotFilteredByVisitorReliefRules(int kind)
        {
            Verse.Pawn pawn = Visitor(1);
            pawn.SetFactionDirect(player);
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Fed);
            CompRHAH_Pawn.TryGet(pawn).SetStay(kind, 1000, 1000);
            RHAH_Mod.Settings.ignoreReliefAfterFed = true;
            Thing food = new Thing { def = Uninitialized<ThingDef>() };
            Field(typeof(Thing), "mapIndexOrState").SetValue(food, (sbyte)0);
            Job original = new Job { def = JobDefOf.Ingest, targetA = food };
            object[] args = { pawn, original };

            typeof(RHAH_ReliefFoodPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, args);

            Assert.Same(original, args[1]);
            Assert.Equal(1000, CompRHAH_Pawn.TryGet(pawn).State.leaveAfterGameTick);
        }

        [Fact]
        public void FoodAcceptanceDoesNotDowngradeFriendlyBatchMembers()
        {
            RHAH_DefOf.RHAH_Faction_Neutral = hostile.def;
            Verse.Pawn pawn = Visitor(1);
            CompRHAH_Pawn.TryGet(pawn).State.SetAttitude(RHAH_Attitude.Friendly);
            map.mapPawns = Uninitialized<MapPawns>();
            Field(typeof(MapPawns), "pawnsSpawned").SetValue(map.mapPawns, new List<Verse.Pawn> { pawn });
            RHAH_BatchAttitude.AcceptFood(pawn);
            Assert.Equal(RHAH_Attitude.Friendly, RHAH_Api.Get(pawn).Attitude);
        }

        [Fact]
        public void ClayFourthIngestionIsRejectedBeforeConsumption()
        {
            HediffDef previous = RHAH_DefOf.RHAH_ClaySatiety;
            ThingDef previousFood = RHAH_DefOf.RHAH_GuanyinTu;
            try
            {
                RHAH_DefOf.RHAH_ClaySatiety = new HediffDef();
                RHAH_DefOf.RHAH_GuanyinTu = Uninitialized<ThingDef>();
                Verse.Pawn pawn = Visitor(1);
                Hediff_RHAH_ClaySatiety clay = new Hediff_RHAH_ClaySatiety { pawn = pawn, def = RHAH_DefOf.RHAH_ClaySatiety };
                pawn.health.hediffSet.hediffs.Add(clay);
                Assert.True(clay.RegisterBite(0));
                Assert.True(clay.RegisterBite(1));
                Assert.True(clay.RegisterBite(2));
                Thing food = new Thing { def = RHAH_DefOf.RHAH_GuanyinTu, stackCount = 4 };
                float nutrition = 1f;
                Assert.False(RHAH_ClayIngestPatch.Prefix(food, pawn, ref nutrition));
                Assert.Equal(0f, nutrition);
                Assert.Equal(4, food.stackCount);
                Assert.False(clay.RegisterBite(3));
                Assert.True(clay.RegisterBite(15 * 60000));
            }
            finally
            {
                RHAH_DefOf.RHAH_ClaySatiety = previous;
                RHAH_DefOf.RHAH_GuanyinTu = previousFood;
            }
        }

        [Fact]
        public void NewFamineArrivalZerosFoodOnlyForNewMarkedNonPlayerPawn()
        {
            HediffDef previous = HediffDefOf.Malnutrition;
            try
            {
                HediffDefOf.Malnutrition = new HediffDef { hediffClass = typeof(Hediff), initialSeverity = 0.001f };
                Verse.Pawn pawn = Visitor(1);
                pawn.health.hediffSet.hediffs.Add(new Hediff { pawn = pawn, def = HediffDefOf.Malnutrition });
                LifeStageAge age = Uninitialized<LifeStageAge>();
                age.def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult, bodySizeFactor = 1f, foodMaxFactor = 1f };
                pawn.def.race.baseBodySize = 1f;
                pawn.def.race.lifeStageAges = new List<LifeStageAge> { age };
                pawn.ageTracker = new Pawn_AgeTracker(pawn);
                Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
                pawn.needs = new Pawn_NeedsTracker();
                pawn.needs.food = Uninitialized<Need_Food>();
                Field(typeof(Need), "pawn").SetValue(pawn.needs.food, pawn);
                Field(typeof(Need), "curLevelInt").SetValue(pawn.needs.food, 0.6f);
                CompRHAH_Pawn.TryGet(pawn).ApplySeed(new RHAH_PawnSeed("I-005", 7, 0, RHAH_PawnRole.Refugee, RHAH_Lifecycle.Arriving));
                Generation.RHAH_FamineArrival.Initialize(pawn);
                Assert.Equal(0f, pawn.needs.food.CurLevel);
                Assert.Equal(0.25f, pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition).Severity);
                RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.SeekingFood);
                RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Fed);
                pawn.needs.food.CurLevel = 0.8f;
                Generation.RHAH_FamineArrival.Initialize(pawn);
                Assert.Equal(0.8f, pawn.needs.food.CurLevel);
            }
            finally
            {
                HediffDefOf.Malnutrition = previous;
            }
        }
        [Fact]
        public void PatchedTradeableUsesActualVanillaPriceCacheAndScopesIdentity()
        {
            ITrader previousTrader = TradeSession.trader;
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("rhah.famine.price.test");
            MethodInfo target = typeof(Tradeable).GetMethod("GetPriceFor");
            try
            {
                harmony.Patch(target, postfix: new HarmonyLib.HarmonyMethod(typeof(Trade.RHAH_FaminePricePatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)));
                Verse.Pawn trader = Visitor(1);
                CompRHAH_Pawn.TryGet(trader).ApplySeed(new RHAH_PawnSeed("I-012", 7, 7, RHAH_PawnRole.Trader, RHAH_Lifecycle.SeekingFood));
                trader.trader = new Pawn_TraderTracker(trader);
                trader.trader.traderKind = new TraderKindDef();
                TradeSession.trader = trader;
                Tradeable trade = new Tradeable();
                ThingDef food = Uninitialized<ThingDef>();
                food.category = ThingCategory.Item;
                food.ingestible = new IngestibleProperties();
                Field(typeof(IngestibleProperties), "cachedNutrition").SetValue(food.ingestible, 0.9f);
                trade.thingsColony.Add(new Thing { def = food });
                Field(typeof(Tradeable), "pricePlayerBuy").SetValue(trade, 14f);
                Field(typeof(Tradeable), "pricePlayerSell").SetValue(trade, 6f);
                Assert.Equal(9f, trade.GetPriceFor(TradeAction.PlayerSells));
                Assert.Equal(14f, trade.GetPriceFor(TradeAction.PlayerBuys));
                food.ingestible = null;
                Assert.Equal(10.5f, trade.GetPriceFor(TradeAction.PlayerBuys));
                RHAH_Mod.Settings.famineTradeEnabled = false;
                Assert.Equal(14f, trade.GetPriceFor(TradeAction.PlayerBuys));
                RHAH_Mod.Settings.famineTradeEnabled = true;
                RHAH_Api.SetLifecycle(trader, RHAH_Lifecycle.Released);
                Assert.Equal(6f, trade.GetPriceFor(TradeAction.PlayerSells));
            }
            finally
            {
                harmony.Unpatch(target, HarmonyLib.HarmonyPatchType.All, harmony.Id);
                TradeSession.trader = previousTrader;
            }
        }

        [Fact]
        public void MedicalDowningPreservesRemainingResidenceAndExpiryBoundary()
        {
            Verse.Pawn pawn = Visitor(1);
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            comp.State.SetAttitude(RHAH_Attitude.Hostile);
            comp.SetStay((int)RHAH_StayKind.Shelter, 100, 20);
            pawn.SetFactionDirect(player);
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Down);
            Assert.False(RHAH_VisitorStay.Tick(pawn, 200));
            Assert.Equal(220, comp.State.leaveAfterGameTick);
            Assert.Equal(20, comp.State.stayRemainingTicks);
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Mobile);
            Assert.False(RHAH_VisitorStay.Tick(pawn, 219));
            pawn.SetFactionDirect(hostile);
            Assert.True(RHAH_VisitorStay.Tick(pawn, 220));
            Assert.Equal(RHAH_Lifecycle.Leaving, comp.State.lifecycle);
        }



        RHAH_ChoiceAction Attack(params Verse.Pawn[] pawns)
        {
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord
            {
                DisplayId = "I-005",
                Choice = RHAH_ChoiceKind.Visitors,
                BatchId = 7,
                MapId = map.uniqueID
            };
            for (int i = 0; i < pawns.Length; i++)
            {
                record.PawnLoadIds.Add(pawns[i].thingIDNumber);
            }
            RHAH_ChoiceRuntime.Open(choices, record);
            RHAH_ChoiceAction action = RHAH_ChoiceRuntime.TrySettle(choices, record.Id, RHAH_ChoiceAction.Attack, 0, true, false);
            RHAH_ChoiceRuntime.Apply(record);
            return action;
        }

        Verse.Pawn Visitor(int id)
        {
            // 保持既有敌对派系 避免 SetFaction 触发 Unity 引擎依赖
            Verse.Pawn pawn = Uninitialized<Verse.Pawn>();
            pawn.thingIDNumber = id;
            pawn.def = Uninitialized<ThingDef>();
            pawn.def.category = ThingCategory.Pawn;
            pawn.def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
            pawn.SetFactionDirect(hostile);
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            pawn.mindState = Uninitialized<Pawn_MindState>();
            pawn.mindState.mentalStateHandler = new MentalStateHandler(pawn);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn, def = new HediffDef() };
            mark.comps = new List<HediffComp> { new CompRHAH_Pawn { parent = mark } };
            pawn.health.hediffSet.hediffs.Add(mark);
            RHAH_Api.TryMarkOrigin(pawn, new RHAH_PawnSeed("I-005", 7, 0, RHAH_PawnRole.Refugee, RHAH_Lifecycle.SeekingFood));
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, false);
            ((HashSet<Verse.Pawn>)Field(typeof(WorldPawns), "pawnsAlive").GetValue(Find.WorldPawns)).Add(pawn);
            return pawn;
        }

        void SetPlayerRelation(FactionRelationKind kind, int goodwill)
        {
            hostile.SetRelation(new FactionRelation { other = player, kind = kind, baseGoodwill = goodwill });
            player.RelationWith(hostile).baseGoodwill = goodwill;
        }

        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(type.FullName + "." + name);
        }
    }
}
