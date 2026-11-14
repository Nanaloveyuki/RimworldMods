using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Narrative;
using HungerAndHavoc.Pawn;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_VisitorRecoveryTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.visitor.recovery.tests");
        readonly Dictionary<FieldInfo, object> previousDefs = new Dictionary<FieldInfo, object>();
        readonly Map map;
        readonly int previousMainThreadId = (int)Field(typeof(UnityData), "mainThreadId").GetValue(null);
        int nextId = 2600;

        public RHAH_VisitorRecoveryTests()
        {
            Rand.PushState(170114);
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.signalManager = new SignalManager();
            map = Find.Maps[0];
            map.mapPawns = new MapPawns(map);
            map.lordManager = new LordManager(map);
            Bind(typeof(RHAH_DefOf), nameof(RHAH_DefOf.RHAH_VisitorSeek), new DutyDef());
            Bind(typeof(RHAH_DefOf), nameof(RHAH_DefOf.RHAH_VisitorTravel), new DutyDef());
            Bind(typeof(RHAH_DefOf), nameof(RHAH_DefOf.RHAH_VisitorLeave), new DutyDef());
            Bind(typeof(DutyDefOf), nameof(DutyDefOf.WanderClose_NoNeeds), new DutyDef());
            harmony.Patch(AccessTools.Method(typeof(RCellFinder), nameof(RCellFinder.TryFindRandomSpotJustOutsideColony),
                    new[] { typeof(Verse.Pawn), typeof(IntVec3).MakeByRefType() }),
                prefix: new HarmonyMethod(typeof(RHAH_VisitorRecoveryTests), nameof(KeepPosition)));
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            Field(typeof(UnityData), "mainThreadId").SetValue(null, previousMainThreadId);
            foreach (KeyValuePair<FieldInfo, object> pair in previousDefs) pair.Key.SetValue(null, pair.Value);
            Rand.PopState();
            fixture.Dispose();
        }

        [Fact]
        public void ReleasedFromContainerRejoinsFamilyWithoutChangingVisitState()
        {
            Verse.Pawn mother = Visitor(RHAH_PawnRole.Mother);
            Verse.Pawn child = Visitor();
            Lord family = CareGroup(mother);
            RHAH_PawnState state = CompRHAH_Pawn.TryGet(child).State;
            state.parentPawnLoadId = mother.thingIDNumber;
            state.lifecycle = RHAH_Lifecycle.Fed;
            state.hasBeenFed = true;
            state.leaveAfterGameTick = 1234;
            state.foodWaitUntilTick = 987;
            List<Verse.Pawn> pawns = new List<Verse.Pawn> { mother, child };

            RHAH_VisitorRecovery.ReconnectYoungVisitors(pawns, map);
            RHAH_VisitorRecovery.ReconnectYoungVisitors(pawns, map);

            Assert.Same(family, child.GetLord());
            Assert.Equal(2, family.ownedPawns.Count);
            Assert.Same(RHAH_DefOf.RHAH_VisitorSeek, child.mindState.duty.def);
            Assert.Equal(RHAH_Lifecycle.Fed, state.lifecycle);
            Assert.True(state.hasBeenFed);
            Assert.Equal(1234, state.leaveAfterGameTick);
            Assert.Equal(987, state.foodWaitUntilTick);
        }

        [Fact]
        public void MissingOriginalLordCreatesOrdinaryCareWithoutReopeningARequest()
        {
            Verse.Pawn child = Visitor();
            RHAH_PawnState state = CompRHAH_Pawn.TryGet(child).State;
            state.leaveAfterGameTick = 1234;
            state.foodWaitUntilTick = 987;
            var choices = Current.Game.GetComponent<GameComponent_RHAH_Game>().OpenChoices;
            int requests = choices.Count;

            RHAH_VisitorRecovery.ReconnectYoungVisitors(new List<Verse.Pawn> { child }, map);

            Lord lord = Assert.Single(map.lordManager.lords);
            Assert.Same(lord, child.GetLord());
            Assert.IsType<LordToil_RHAH_VisitorSeek>(lord.CurLordToil);
            LordJob_RHAH_Visitor job = Assert.IsType<LordJob_RHAH_Visitor>(lord.LordJob);
            Assert.Equal(0, job.ArmedChoiceId);
            Assert.False(job.ArmedAssault);
            Assert.Equal(requests, choices.Count);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, state.lifecycle);
            Assert.Equal(1234, state.leaveAfterGameTick);
            Assert.Equal(987, state.foodWaitUntilTick);
        }

        [Theory]
        [InlineData("batch")]
        [InlineData("family")]
        [InlineData("faction")]
        [InlineData("map")]
        public void DifferentGroupCannotAdoptTheReturningChild(string difference)
        {
            Verse.Pawn other = Visitor(RHAH_PawnRole.Mother);
            Lord unrelated = CareGroup(other);
            Verse.Pawn child = Visitor();
            if (difference == "batch") CompRHAH_Pawn.TryGet(other).State.spawnBatchId++;
            if (difference == "family") CompRHAH_Pawn.TryGet(other).State.relationshipGroupId++;
            if (difference == "faction") other.SetFactionDirect(new Faction { def = new FactionDef() });
            if (difference == "map") Field(typeof(Thing), "mapIndexOrState").SetValue(other, (sbyte)-1);

            RHAH_VisitorRecovery.ReconnectYoungVisitors(new List<Verse.Pawn> { other, child }, map);

            Assert.NotNull(child.GetLord());
            Assert.NotSame(unrelated, child.GetLord());
            Assert.DoesNotContain(child, unrelated.ownedPawns);
        }

        [Fact]
        public void ExistingLordIsNeverReplaced()
        {
            Verse.Pawn child = Visitor();
            Lord original = new Lord { lordManager = map.lordManager };
            Field(typeof(Lord), "curJob").SetValue(original, new LordJob_AssaultColony());
            original.ownedPawns.Add(child);
            child.lord = original;
            map.lordManager.lords.Add(original);
            RHAH_VisitorRecovery.ReconnectYoungVisitors(new List<Verse.Pawn> { child }, map);
            Assert.Same(original, child.GetLord());
            Assert.Single(map.lordManager.lords);
        }

        [Theory]
        [InlineData("released")]
        [InlineData("leaving")]
        [InlineData("player")]
        [InlineData("shelter")]
        [InlineData("adult")]
        [InlineData("interception")]
        public void IneligibleVisitorsDoNotAcquireANewCareGroup(string reason)
        {
            Verse.Pawn child = Visitor();
            RHAH_PawnState state = CompRHAH_Pawn.TryGet(child).State;
            if (reason == "released") state.lifecycle = RHAH_Lifecycle.Released;
            if (reason == "leaving") state.lifecycle = RHAH_Lifecycle.Leaving;
            if (reason == "player") child.SetFactionDirect(Faction.OfPlayer);
            if (reason == "shelter") state.stayKind = (int)RHAH_StayKind.Shelter;
            if (reason == "adult") SetStage(child, DevelopmentalStage.Adult);
            if (reason == "interception")
            {
                NarrativeState narrative = new NarrativeState(Current.Game);
                Current.Game.components.Add(narrative);
                narrative.Commit(book => book.OpenInterception(map.uniqueID, state.spawnBatchId, 0,
                    new List<int> { child.thingIDNumber }));
            }

            RHAH_VisitorRecovery.ReconnectYoungVisitors(new List<Verse.Pawn> { child }, map);

            Assert.Null(child.GetLord());
            Assert.Empty(map.lordManager.lords);
        }

        [Theory]
        [InlineData(false, RHAH_Lifecycle.SeekingFood)]
        [InlineData(true, RHAH_Lifecycle.SeekingFood)]
        [InlineData(false, RHAH_Lifecycle.Leaving)]
        [InlineData(true, RHAH_Lifecycle.Leaving)]
        public void DeliberatelyDroppedChildStaysOutsideCareEvenAfterMotherLeaves(bool worldMother, RHAH_Lifecycle lifecycle)
        {
            Verse.Pawn mother = Visitor(RHAH_PawnRole.Mother);
            Verse.Pawn child = Visitor();
            RHAH_PawnState family = CompRHAH_Pawn.TryGet(mother).State;
            family.childPawnLoadIds.Add(child.thingIDNumber);
            family.droppedChildLoadIds.Add(child.thingIDNumber);
            RHAH_PawnState state = CompRHAH_Pawn.TryGet(child).State;
            state.parentPawnLoadId = mother.thingIDNumber;
            state.lifecycle = lifecycle;
            List<Verse.Pawn> pawns = new List<Verse.Pawn> { child };
            if (worldMother) Field(typeof(Thing), "mapIndexOrState").SetValue(mother, (sbyte)-1);
            else
            {
                CareGroup(mother);
                ((HashSet<Verse.Pawn>)Field(typeof(WorldPawns), "pawnsAlive").GetValue(Find.WorldPawns)).Remove(mother);
                ((List<Verse.Pawn>)Field(typeof(MapPawns), "pawnsSpawned").GetValue(map.mapPawns)).Add(mother);
                map.listerThings = new ListerThings(ListerThingsUse.Global);
                map.passingShipManager = new PassingShipManager(map);
                Field(typeof(Map), "spawnedThings").SetValue(map, new ThingOwner<Thing>(map));
                Field(typeof(UnityData), "mainThreadId").SetValue(null, Thread.CurrentThread.ManagedThreadId);
                Current.ProgramState = ProgramState.Playing;
                pawns.Add(mother);
            }

            RHAH_VisitorRecovery.ReconnectYoungVisitors(pawns, map);
            RHAH_VisitorStay.RebuildDepartureGroups(pawns, map);

            Assert.Null(child.GetLord());
            Assert.Equal(lifecycle, state.lifecycle);
        }

        Verse.Pawn Visitor(RHAH_PawnRole role = RHAH_PawnRole.RatkinYoung)
        {
            Verse.Pawn pawn = (Verse.Pawn)typeof(RHAH_AttackChoiceTests)
                .GetMethod("Visitor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture, new object[] { nextId++ });
            Field(typeof(Thing), "positionInt").SetValue(pawn, new IntVec3(10, 0, 10));
            pawn.Faction.def.autoFlee = false;
            RHAH_PawnState state = CompRHAH_Pawn.TryGet(pawn).State;
            state.role = role;
            state.relationshipGroupId = 7;
            SetStage(pawn, role == RHAH_PawnRole.Mother ? DevelopmentalStage.Adult : DevelopmentalStage.Baby);
            return pawn;
        }

        Lord CareGroup(Verse.Pawn member)
        {
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor(member.Faction, member.Position, RHAH_PawnRole.Mother);
            Field(typeof(LordJob_RHAH_Visitor), "waitSpotResolved").SetValue(job, true);
            Lord lord = LordMaker.MakeNewLord(member.Faction, job, map, new[] { member });
            lord.GotoToil(lord.Graph.lordToils.OfType<LordToil_RHAH_VisitorSeek>().Single());
            return lord;
        }

        static void SetStage(Verse.Pawn pawn, DevelopmentalStage stage)
        {
            pawn.def.race.lifeStageAges = new List<LifeStageAge>
            {
                new LifeStageAge { def = new LifeStageDef { developmentalStage = stage } }
            };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        }

        void Bind(Type type, string name, object value)
        {
            FieldInfo field = Field(type, name);
            previousDefs.Add(field, field.GetValue(null));
            field.SetValue(null, value);
        }

        static bool KeepPosition() => false;
        static FieldInfo Field(Type type, string name) => AccessTools.Field(type, name);
    }
}
