using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_VisitorCustodyTests : IDisposable
    {
        readonly Game previousGame;
        readonly ProgramState previousProgramState;
        readonly IRHAH_ApiHost previousHost;
        readonly List<IRHAH_PawnBehavior> previousBehaviors;
        readonly Map map;
        readonly Faction player;
        readonly Faction visitors;
        readonly GameComponent_RHAH_Game choices;
        int nextPawnId = 1;

        public RHAH_VisitorCustodyTests()
        {
            RimWorldAssemblies.EnsureResolved();
            previousGame = Current.Game;
            previousProgramState = Current.ProgramState;
            previousHost = (IRHAH_ApiHost)Field(typeof(RHAH_Api), "host").GetValue(null);
            previousBehaviors = new List<IRHAH_PawnBehavior>(
                (List<IRHAH_PawnBehavior>)Field(typeof(RHAH_PawnBehaviors), "handlers").GetValue(null));
            RHAH_PawnBehaviors.ResetForTests();
            RHAH_Api.Bind(new RHAH_ApiHost());

            Current.Game = Uninitialized<Game>();
            Current.Game.components = new List<GameComponent>();
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.tickManager = new TickManager();
            Current.ProgramState = ProgramState.Entry;
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            World world = Uninitialized<World>();
            world.factionManager = new FactionManager();
            world.worldPawns = new WorldPawns();
            world.worldObjects = new WorldObjectsHolder();
            Current.Game.World = world;
            player = NewFaction(true);
            visitors = NewFaction(false);
            Field(typeof(FactionManager), "ofPlayer").SetValue(world.factionManager, player);
            map = NewMap(41);
            choices = new GameComponent_RHAH_Game(Current.Game);
            Current.Game.components.Add(choices);
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            Current.ProgramState = previousProgramState;
            RHAH_Api.Bind(previousHost);
            RHAH_PawnBehaviors.ResetForTests();
            for (int i = 0; i < previousBehaviors.Count; i++)
            {
                RHAH_PawnBehaviors.Register(previousBehaviors[i]);
            }
        }

        [Theory]
        [InlineData("released")]
        [InlineData("player")]
        [InlineData("otherMap")]
        [InlineData("imprisonDenied")]
        public void CaptureEligibilityRejectsStaleOrForbiddenVisitors(string change)
        {
            Verse.Pawn pawn = Visitor(map);
            Assert.True(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));

            if (change == "released")
            {
                RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Released);
            }
            else if (change == "player")
            {
                pawn.SetFactionDirect(player);
            }
            else if (change == "otherMap")
            {
                Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)Find.Maps.IndexOf(NewMap(42)));
            }
            else
            {
                RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Imprison, false);
            }

            Assert.False(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
            Assert.False(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, true));
            Assert.False(RHAH_VisitorBatch.HasEligible(new List<Verse.Pawn> { pawn }, map.uniqueID, false));
        }

        [Fact]
        public void HireAndJoinDenialDoesNotDenyImprisonment()
        {
            Verse.Pawn pawn = Visitor(map);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Hire, false);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.JoinColony, false);

            Assert.True(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
            Assert.True(RHAH_VisitorBatch.HasEligible(new List<Verse.Pawn> { null, pawn }, map.uniqueID, false));

            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Imprison, false);
            Assert.False(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
        }


        [Fact]
        public void ActivePlagueDoesNotOverrideOtherwiseValidCustodyEligibility()
        {
            Verse.Pawn pawn = Visitor(map);
            Hediff plague = Plague(pawn, 0.4f);

            Assert.True(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
            Assert.True(RHAH_VisitorBatch.HasEligible(new List<Verse.Pawn> { pawn }, map.uniqueID, false));
            Assert.Same(plague, pawn.health.hediffSet.GetFirstHediffOfDef(plague.def));
            Assert.Equal(0.4f, plague.Severity);
        }

        [Theory]
        [InlineData(RHAH_BehaviorGate.JoinColony)]
        [InlineData(RHAH_BehaviorGate.Hire)]
        [InlineData(RHAH_BehaviorGate.Transfer)]
        public void ActivePlagueDoesNotOverrideDispositionGates(RHAH_BehaviorGate gate)
        {
            Verse.Pawn pawn = Visitor(map);
            Hediff plague = Plague(pawn, 0.4f);

            Assert.True(RHAH_Api.Allows(pawn, gate));
            RHAH_Api.SetGate(pawn, gate, false);
            Assert.False(RHAH_Api.Allows(pawn, gate));
            RHAH_Api.SetGate(pawn, gate, true);
            Assert.True(RHAH_Api.Allows(pawn, gate));
            Assert.Same(plague, pawn.health.hediffSet.GetFirstHediffOfDef(plague.def));
            Assert.Equal(0.4f, plague.Severity);
        }

        [Fact]
        public void TransferPreflightAllowsWaitingVisitorsButPreservesExplicitHolds()
        {
            Verse.Pawn pawn = Visitor(map);
            Assert.False(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.ExitMap));
            Assert.True(RHAH_ApiHost.CanTransfer(pawn));
            Assert.Equal(RHAH_Lifecycle.Arriving, RHAH_Api.Get(pawn).Lifecycle);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, false);
            Assert.False(RHAH_ApiHost.CanTransfer(pawn));
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, true);
            Assert.True(RHAH_ApiHost.CanTransfer(pawn));
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Transfer, false);
            Assert.False(RHAH_ApiHost.CanTransfer(pawn));
        }

        [Fact]
        public void MentalStateOverridesOtherwiseValidCustodyEligibility()
        {
            Verse.Pawn pawn = Visitor(map);
            Assert.True(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));

            Field(typeof(MentalStateHandler), "curStateInt").SetValue(
                pawn.mindState.mentalStateHandler, Uninitialized<MentalState_Berserk>());
            Assert.False(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
            Assert.False(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, true));
        }


        [Fact]
        public void DownedVisitorRemainsEligibleButExistingCustodyNeverDoes()
        {
            Verse.Pawn pawn = Visitor(map);
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Down);
            Assert.True(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));

            pawn.guest.guestStatusInt = GuestStatus.Prisoner;
            Assert.False(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
            pawn.guest.guestStatusInt = GuestStatus.Slave;
            Assert.False(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void CaptureNeedsNoPrisonCell(bool prison, bool edge)
        {
            Verse.Pawn pawn = Visitor(map);
            Room room = pawn.GetRoom();
            Field(typeof(Room), "isPrisonCell").SetValue(room, prison);
            Field(typeof(District), "numRegionsTouchingMapEdge").SetValue(room.Districts[0], edge ? 1 : 0);

            Assert.True(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
            Assert.True(RHAH_VisitorBatch.HasEligible(new List<Verse.Pawn> { pawn }, map.uniqueID, false));
        }

        [Theory]
        [InlineData("I-013", 1, 10)]
        [InlineData("I-013", 2, 20)]
        [InlineData("I-019", 1, 20)]
        [InlineData("I-019", 2, 20)]
        public void FoodReplacementNeedsItsOwnStockNotAnOutgoingChild(string displayId, int visitorCount, int required)
        {
            FieldInfo binding = Field(typeof(DefOfHelper), "bindingNow");
            bool previousBinding = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                RuntimeHelpers.RunClassConstructor(typeof(ThingDefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previousBinding);
            }
            RHAH_Settings previousSettings = RHAH_Mod.Settings;
            List<ThingDef> previousDefs = new List<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading);
            ThingDef previousMeal = DefDatabase<ThingDef>.GetNamedSilentFail("MealSimple");
            ThingDef meal = previousMeal ?? Uninitialized<ThingDef>();
            if (previousMeal == null) meal.defName = "MealSimple";
            if (previousMeal == null) DefDatabase<ThingDef>.Add(meal);
            try
            {
                RHAH_Mod.Settings = new RHAH_Settings { foodPerChild = 10, childExchangeFoodSubstitution = true };
                Verse.Pawn[] pawns = new Verse.Pawn[visitorCount];
                for (int i = 0; i < pawns.Length; i++)
                {
                    pawns[i] = Visitor(map);
                    pawns[i].def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
                    pawns[i].ageTracker = new Pawn_AgeTracker(pawns[i]);
                }
                RHAH_ChoiceRecord record = Open(pawns);
                record.DisplayId = displayId;
                record.Kind = RHAH_RequestKind.Baby;
                record.Choice = displayId == "I-013" ? RHAH_ChoiceKind.ChildExchange : RHAH_ChoiceKind.Aid;
                map.listerThings = new ListerThings(ListerThingsUse.Global, null);
                Thing stock = new Thing { def = meal, stackCount = required - 1 };
                Field(typeof(Thing), "mapIndexOrState").SetValue(stock, (sbyte)Find.Maps.IndexOf(map));
                ((Dictionary<ThingDef, List<Thing>>)Field(typeof(ListerThings), "listsByDef").GetValue(map.listerThings))[meal] = new List<Thing> { stock };

                Assert.False(RHAH_ChildExchange.CanDeliver(record, null, true));
                Assert.Equal(required - 1, stock.stackCount);
                Assert.False(RHAH_ChildExchange.TryExchange(record, null, true));
                Assert.Equal(required - 1, stock.stackCount);
                stock.stackCount = required;
                Assert.True(RHAH_ChildExchange.CanDeliver(record, null, true));
                Assert.False(RHAH_ChildExchange.CanDeliver(record, null, false));
                RHAH_Mod.Settings.childExchangeFoodSubstitution = false;
                Assert.False(RHAH_ChildExchange.CanDeliver(record, null, true));
                RHAH_Mod.Settings.childExchangeFoodSubstitution = true;
                RHAH_Mod.Settings.foodPerChild = 7;
                stock.stackCount = displayId == "I-019" ? 14 : visitorCount * 7;
                Assert.True(RHAH_ChildExchange.CanDeliver(record, null, true));
                stock.stackCount--;
                Assert.False(RHAH_ChildExchange.CanDeliver(record, null, true));
            }
            finally
            {
                RHAH_Mod.Settings = previousSettings;
                if (previousMeal == null)
                {
                    DefDatabase<ThingDef>.Clear();
                    for (int i = 0; i < previousDefs.Count; i++) DefDatabase<ThingDef>.Add(previousDefs[i]);
                }
            }
        }

        [Fact]
        public void NonChildRequestCannotTransferChildrenEvenWithStaleBabyKind()
        {
            RHAH_ChoiceRecord record = Open(Visitor(map));
            record.Kind = RHAH_RequestKind.Baby;
            Assert.False(RHAH_ChildExchange.CanDeliver(record, null, false));
            Assert.False(RHAH_ChildExchange.CanDeliver(record, null, true));
            Assert.Empty(RHAH_ChildExchange.Candidates(record));
        }

        [Fact]
        public void MissingCarriedRewardCannotPayOrCloseTransaction()
        {
            Verse.Pawn pawn = Visitor(map);
            RHAH_ChoiceRecord record = Open(pawn);
            record.DisplayId = "I-020";
            record.Kind = RHAH_RequestKind.SimpleMeal;
            record.Choice = RHAH_ChoiceKind.Intel;
            record.TransactionVersion = 1;
            record.QuotedFoodDefName = "RawPotatoes";
            record.QuotedFoodCount = 54;
            record.RewardDefNames.Add("Silver");
            record.RewardCounts.Add(100);
            record.RewardThingIds.Add(123);
            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettleRequest(choices, record.Id, RHAH_ChoiceAction.Deliver, 9, true));
            Assert.True(record.Open);
            Assert.Equal(54, record.QuotedFoodCount);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.Equal(RHAH_ChoiceAction.Timeout,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, RHAH_ChoiceAction.Timeout, 10, true, false));
            Assert.Equal(RHAH_ChoiceAction.Timeout, record.Settled);
            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettleRequest(choices, record.Id, RHAH_ChoiceAction.Deliver, 11, true));
            Assert.Equal(new[] { 123 }, record.RewardThingIds);
        }


        [Fact]
        public void CaptureGatePropagatesReleaseVetoWithoutChangingVisitorState()
        {
            Verse.Pawn pawn = Visitor(map);
            ReleasePolicy policy = new ReleasePolicy();
            RHAH_PawnBehaviors.Register(policy);
            Lord lord = Uninitialized<Lord>();
            PawnDuty duty = Uninitialized<PawnDuty>();
            Job job = Uninitialized<Job>();
            pawn.lord = lord;
            pawn.mindState.duty = duty;
            pawn.jobs = new Pawn_JobTracker(pawn);
            pawn.jobs.curJob = job;
            IRHAH_Pawn before = RHAH_Api.Get(pawn);
            Pawn_GuestTracker guest = pawn.guest;

            Assert.False(RHAH_ColonyGates.AllowsCapture(pawn, player));

            Assert.Equal(1, policy.ReleaseCalls);
            Assert.Equal(RHAH_ReleaseReason.Imprisoned, policy.LastReason);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.Equal(before.Lifecycle, RHAH_Api.Get(pawn).Lifecycle);
            Assert.Equal(before.SpawnBatchId, RHAH_Api.Get(pawn).SpawnBatchId);
            Assert.Same(visitors, pawn.Faction);
            Assert.Same(guest, pawn.guest);
            Assert.Equal(GuestStatus.Guest, guest.GuestStatus);
            Assert.False(pawn.IsPrisoner);
            Assert.False(pawn.IsSlave);
            Assert.Same(lord, pawn.GetLord());
            Assert.Same(duty, pawn.mindState.duty);
            Assert.Same(job, pawn.CurJob);
        }

        [Theory]
        [InlineData(RHAH_ChoiceAction.Capture)]
        [InlineData(RHAH_ChoiceAction.Enslave)]
        public void EmptyCustodyBatchKeepsChoiceOpen(RHAH_ChoiceAction action)
        {
            RHAH_ChoiceRecord record = Open();

            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, action, 9, true, false));

            Assert.True(record.Open);
            Assert.Equal(RHAH_ChoiceAction.None, record.Settled);
        }

        [Fact]
        public void CaptureIsBlockedByImprisonDenialNotByRoomStatus()
        {
            Verse.Pawn pawn = Visitor(map);
            Room room = pawn.GetRoom();
            Field(typeof(Room), "isPrisonCell").SetValue(room, false);
            Assert.True(RHAH_VisitorBatch.CanConvert(pawn, map.uniqueID, false));
            IntVec3 position = pawn.Position;
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Imprison, false);
            RHAH_ChoiceRecord record = Open(pawn);

            Assert.Equal(0, RHAH_VisitorBatch.Capture(new List<Verse.Pawn> { pawn }, map.uniqueID));
            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, RHAH_ChoiceAction.Capture, 9, true, false));

            Assert.True(record.Open);
            Assert.True(pawn.Spawned);
            Assert.Same(map, pawn.Map);
            Assert.Equal(position, pawn.Position);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.Equal(GuestStatus.Guest, pawn.guest.GuestStatus);
        }

        [Theory]
        [InlineData(RHAH_ChoiceAction.Capture)]
        [InlineData(RHAH_ChoiceAction.Enslave)]
        public void EntirelyIneligibleCustodyBatchKeepsChoiceAndPawnsUnchanged(RHAH_ChoiceAction action)
        {
            Verse.Pawn blocked = Visitor(map);
            RHAH_Api.SetGate(blocked, RHAH_BehaviorGate.Imprison, false);
            Verse.Pawn released = Visitor(map);
            RHAH_Api.SetLifecycle(released, RHAH_Lifecycle.Released);
            Verse.Pawn otherMap = Visitor(NewMap(42));
            Verse.Pawn colonist = Visitor(map);
            colonist.SetFactionDirect(player);
            RHAH_ChoiceRecord record = Open(blocked, released, otherMap, colonist);

            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, action, 9, true, false));

            Assert.True(record.Open);
            Assert.Equal(RHAH_ChoiceAction.None, record.Settled);
            Assert.True(RHAH_Api.IsVisitor(blocked));
            Assert.True(RHAH_Api.IsVisitor(otherMap));
            Assert.True(RHAH_Api.Get(released).IsReleased);
            Assert.Same(player, colonist.Faction);
            Assert.Equal(GuestStatus.Guest, blocked.guest.GuestStatus);
            Assert.Equal(GuestStatus.Guest, otherMap.guest.GuestStatus);
            Assert.Same(Find.Maps[1], otherMap.Map);
        }

        [Theory]
        [InlineData(RHAH_ChoiceAction.Capture)]
        [InlineData(RHAH_ChoiceAction.Enslave)]
        public void DisabledChoiceDoesNotConsultOrConvertItsEligibleVisitor(RHAH_ChoiceAction action)
        {
            Verse.Pawn pawn = Visitor(map);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Imprison, null);
            ReleasePolicy policy = new ReleasePolicy();
            RHAH_PawnBehaviors.Register(policy);
            RHAH_ChoiceRecord record = Open(pawn);

            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, action, 9, false, false));

            Assert.True(record.Open);
            Assert.Equal(0, policy.GateCalls);
            Assert.Equal(0, policy.ReleaseCalls);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.Equal(GuestStatus.Guest, pawn.guest.GuestStatus);
        }

        [Theory]
        [InlineData(RHAH_ChoiceAction.Capture)]
        [InlineData(RHAH_ChoiceAction.Enslave)]
        public void ExpiryWinsOverCustodyAndRepeatedClicksNeverRetryConversion(RHAH_ChoiceAction action)
        {
            Verse.Pawn pawn = Visitor(map);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Imprison, null);
            ReleasePolicy policy = new ReleasePolicy();
            RHAH_PawnBehaviors.Register(policy);
            RHAH_ChoiceRecord record = Open(pawn);

            Assert.Equal(RHAH_ChoiceAction.Timeout,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, action, 10, true, false));
            Assert.False(record.Open);
            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, action, 11, true, false));

            Assert.Equal(RHAH_ChoiceAction.Timeout, record.Settled);
            Assert.Equal(0, policy.GateCalls);
            Assert.Equal(0, policy.ReleaseCalls);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.Equal(GuestStatus.Guest, pawn.guest.GuestStatus);
        }

        [Theory]
        [InlineData(RHAH_ChoiceAction.Capture)]
        [InlineData(RHAH_ChoiceAction.Enslave)]
        public void AlreadySettledCustodyDoesNotConvertNewlyEligibleVisitors(RHAH_ChoiceAction action)
        {
            Verse.Pawn pawn = Visitor(map);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Imprison, null);
            ReleasePolicy policy = new ReleasePolicy();
            RHAH_PawnBehaviors.Register(policy);
            RHAH_ChoiceRecord record = Open(pawn);
            record.Settled = action;

            Assert.Equal(RHAH_ChoiceAction.None,
                RHAH_ChoiceRuntime.TrySettle(choices, record.Id, action, 9, true, false));

            Assert.Equal(action, record.Settled);
            Assert.Equal(0, policy.GateCalls);
            Assert.Equal(0, policy.ReleaseCalls);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.Equal(GuestStatus.Guest, pawn.guest.GuestStatus);
        }

        RHAH_ChoiceRecord Open(params Verse.Pawn[] pawns)
        {
            RHAH_ChoiceRecord record = new RHAH_ChoiceRecord
            {
                DisplayId = "I-004",
                Choice = RHAH_ChoiceKind.Visitors,
                BatchId = 7,
                MapId = map.uniqueID,
                ExpireTick = 10
            };
            for (int i = 0; i < pawns.Length; i++)
            {
                record.PawnLoadIds.Add(pawns[i].thingIDNumber);
            }
            return RHAH_ChoiceRuntime.Open(choices, record);
        }

        Verse.Pawn Visitor(Map target)
        {
            Verse.Pawn pawn = Uninitialized<Verse.Pawn>();
            pawn.thingIDNumber = nextPawnId++;
            pawn.def = Uninitialized<ThingDef>();
            pawn.def.defName = "CustodyVisitor";
            pawn.def.category = ThingCategory.Pawn;
            pawn.SetFactionDirect(visitors);
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)Find.Maps.IndexOf(target));
            Field(typeof(Thing), "positionInt").SetValue(pawn, new IntVec3(1, 0, 1));
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            pawn.mindState = Uninitialized<Pawn_MindState>();
            pawn.mindState.mentalStateHandler = new MentalStateHandler(pawn);
            pawn.guest = Uninitialized<Pawn_GuestTracker>();
            Field(typeof(Pawn_GuestTracker), "pawn").SetValue(pawn.guest, pawn);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn };
            CompRHAH_Pawn origin = new CompRHAH_Pawn { parent = mark };
            mark.comps = new List<HediffComp> { origin };
            pawn.health.hediffSet.hediffs.Add(mark);
            RHAH_Api.TryMarkOrigin(pawn, new RHAH_PawnSeed(
                sourceIncidentDisplayId: "I-004",
                spawnBatchId: 7,
                role: RHAH_PawnRole.Refugee,
                gateOverrides: new[]
                {
                    new KeyValuePair<RHAH_BehaviorGate, bool>(RHAH_BehaviorGate.Imprison, true)
                }));
            // 记录查找走原版世界角色集合 不触发地图容器的 Unity 主线程检查
            ((HashSet<Verse.Pawn>)Field(typeof(WorldPawns), "pawnsAlive").GetValue(Find.WorldPawns)).Add(pawn);
            return pawn;
        }
        static Hediff Plague(Verse.Pawn pawn, float severity)
        {
            HediffDef def = Uninitialized<HediffDef>();
            def.defName = "RHAH_Plague";
            Hediff hediff = Uninitialized<Hediff>();
            hediff.def = def;
            hediff.pawn = pawn;
            Field(typeof(Hediff), "severityInt").SetValue(hediff, severity);
            pawn.health.hediffSet.hediffs.Add(hediff);
            return hediff;
        }

        Map NewMap(int id)
        {
            Map result = Uninitialized<Map>();
            result.uniqueID = id;
            result.info = new MapInfo { Size = new IntVec3(3, 1, 3) };
            result.components = new List<MapComponent>();
            Find.Maps.Add(result);
            result.cellIndices = new CellIndices(result);
            result.regionGrid = new RegionGrid(result);
            result.regionDirtyer = new RegionDirtyer(result);
            result.regionAndRoomUpdater = new RegionAndRoomUpdater(result) { Enabled = false };
            Field(typeof(RegionAndRoomUpdater), "initialized").SetValue(result.regionAndRoomUpdater, true);
            Room room = NewRoom(true, false, 1);
            result.regionGrid.SetRegionAt(new IntVec3(1, 0, 1), room.Regions[0]);
            return result;
        }

        static Room NewRoom(bool prison, bool edge, int regionCount)
        {
            Room room = Uninitialized<Room>();
            District district = Uninitialized<District>();
            List<Region> regions = new List<Region>();
            for (int i = 0; i < regionCount; i++)
            {
                Region region = NewRegion(i + 1);
                Field(typeof(Region), "districtInt").SetValue(region, district);
                regions.Add(region);
            }
            Field(typeof(District), "regions").SetValue(district, regions);
            Field(typeof(District), "roomInt").SetValue(district, room);
            Field(typeof(District), "numRegionsTouchingMapEdge").SetValue(district, edge ? 1 : 0);
            Field(typeof(Room), "districts").SetValue(room, new List<District> { district });
            Field(typeof(Room), "tmpRegions").SetValue(room, new List<Region>());
            Field(typeof(Room), "isPrisonCell").SetValue(room, prison);
            return room;
        }

        static Region NewRegion(int id)
        {
            Region region = Uninitialized<Region>();
            region.id = id;
            region.type = RegionType.Normal;
            region.valid = true;
            region.links = new List<RegionLink>();
            return region;
        }

        static Faction NewFaction(bool isPlayer)
        {
            Faction faction = Uninitialized<Faction>();
            faction.def = Uninitialized<FactionDef>();
            faction.def.isPlayer = isPlayer;
            faction.def.humanlikeFaction = true;
            return faction;
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


        sealed class ReleasePolicy : IRHAH_PawnBehavior
        {
            internal int GateCalls;
            internal int ReleaseCalls;
            internal RHAH_ReleaseReason LastReason;

            public bool? Allows(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_BehaviorGate gate)
            {
                GateCalls++;
                return gate == RHAH_BehaviorGate.Imprison ? (bool?)true : null;
            }

            public bool? ShouldReleaseToColony(Verse.Pawn pawn, IRHAH_Pawn snapshot, RHAH_ReleaseReason reason)
            {
                ReleaseCalls++;
                LastReason = reason;
                return false;
            }
        }
    }
}
