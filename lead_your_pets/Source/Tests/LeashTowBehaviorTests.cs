#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using Verse.AI;
using Xunit;

namespace LeadYourPet.Tests
{
    [Collection("Game state")]
    public class LeashTowBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetSettings previousSettings = LeadYourPetMod.Settings;
        private readonly LeadYourPetGameComponent component;
        private readonly Map map;
        private readonly Pawn pet;
        private readonly Pawn master;
        private readonly LeashLink link;

        public LeashTowBehaviorTests()
        {
            LeadYourPetMod.Settings = null;
            Game game = Raw<Game>();
            game.tickManager = Raw<TickManager>();
            map = Raw<Map>();
            map.info = Raw<MapInfo>();
            map.info.Size = new IntVec3(60, 1, 60);
            map.cellIndices = new CellIndices(map);
            map.thingGrid = new ThingGrid(map);
            map.coverGrid = new CoverGrid(map);
            map.gasGrid = new GasGrid(map);
            map.regionGrid = new RegionGrid(map);
            PathGrid grid = new PathGrid(map, new PathGridDef());
            PathingContext context = new PathingContext(map, grid);
            map.pathing = Raw<Pathing>();
            Set(map.pathing, "normal", context);
            Set(map.pathing, "fenceBlocked", context);
            Set(game, "maps", new List<Map> { map });
            component = new LeadYourPetGameComponent(game);
            Set(game, "components", new List<GameComponent> { component });
            Current.Game = game;
            pet = NewPawn(1, new IntVec3(40, 0, 20));
            master = NewPawn(2, new IntVec3(20, 0, 20));
            link = new LeashLink { Master = master, Pet = pet };
            ((List<LeashLink>)Get(component, "links")).Add(link);
            InvokeOnComponent("RegisterLink", link);
        }

        [Fact]
        public void SleepingPetOutsideTheLeashIsTowedOntoTheRingAndKeepsItsJob()
        {
            Job sleep = SleepJob();
            Set(master.pather, "moving", true);

            Invoke("TowPassiveLeashPet", link, false);

            Assert.Same(sleep, pet.CurJob);
            Assert.True(pet.jobs.curDriver.asleep);
            Assert.True(pet.Position.DistanceTo(master.Position) <= LeadYourPetUtility.MaxLeashLength);
            Assert.NotEqual(new IntVec3(40, 0, 20), pet.Position);
        }

        [Fact]
        public void TowStopsInsideTheLeashLength()
        {
            SleepJob();
            Set(master.pather, "moving", true);
            Set(pet, "positionInt", new IntVec3(26, 0, 20));

            Invoke("TowPassiveLeashPet", link, false);

            Assert.Equal(new IntVec3(26, 0, 20), pet.Position);
        }

        [Fact]
        public void StandingMasterDoesNotTowSleepingPet()
        {
            SleepJob();
            Set(master.pather, "moving", false);

            Invoke("TowPassiveLeashPet", link, false);

            Assert.Equal(new IntVec3(40, 0, 20), pet.Position);
        }

        [Fact]
        public void FixedAnchorIsNotDraggedTowardsItsCell()
        {
            SleepJob();
            link.AnchorMode = LeashAnchorMode.Cell;
            link.AnchorCell = master.Position;

            Invoke("TowPassiveLeashPet", link, false);

            Assert.Equal(new IntVec3(40, 0, 20), pet.Position);
        }

        [Fact]
        public void AwakePetWithIdleJobIsNotTowed()
        {
            Set(pet.jobs, "curJob", new Job { def = new JobDef { defName = "TowFixtureIdle" } });
            Set(pet.jobs, "curDriver", new JobDriver_FollowLeashMaster());
            pet.jobs.curDriver.asleep = false;
            Set(master.pather, "moving", true);

            Assert.False(LeadYourPetUtility.CannotFollowOnItsOwn(link));
            Invoke("TowPassiveLeashPet", link, false);

            Assert.Equal(new IntVec3(40, 0, 20), pet.Position);
        }

        [Fact]
        public void MentalStatePetCanBeTowedWithoutTouchingItsState()
        {
            Job mental = new Job { def = new JobDef { defName = "TowFixtureBreak" } };
            Set(pet.jobs, "curJob", mental);
            Set(pet.jobs, "curDriver", new JobDriver_FollowLeashMaster());
            pet.mindState = Raw<Pawn_MindState>();
            Set(pet.mindState, "mentalStateHandler", new MentalStateHandler(pet));
            Set(pet.mindState.mentalStateHandler, "curStateInt", Raw<MentalState>());
            Set(master.pather, "moving", true);

            Assert.True(LeadYourPetUtility.CannotFollowOnItsOwn(link));
            Invoke("TowPassiveLeashPet", link, false);

            Assert.Same(mental, pet.CurJob);
            Assert.True(pet.Position.DistanceTo(master.Position) <= LeadYourPetUtility.MaxLeashLength);
        }

        [Fact]
        public void WanderingMentalStatePetIsTowedEvenWhileTheMasterStands()
        {
            Job mental = new Job { def = new JobDef { defName = "TowFixtureBreak" } };
            Set(pet.jobs, "curJob", mental);
            Set(pet.jobs, "curDriver", new JobDriver_FollowLeashMaster());
            pet.mindState = Raw<Pawn_MindState>();
            Set(pet.mindState, "mentalStateHandler", new MentalStateHandler(pet));
            Set(pet.mindState.mentalStateHandler, "curStateInt", Raw<MentalState>());
            Set(pet.pather, "moving", true);
            Set(pet.pather, "destination", LocalTargetInfo.Invalid);

            Invoke("TowPassiveLeashPet", link, false);

            Assert.Same(mental, pet.CurJob);
            Assert.True(pet.Position.DistanceTo(master.Position) <= LeadYourPetUtility.MaxLeashLength);
        }

        [Fact]
        public void RestingAwakePetIsLeftAlone()
        {
            Set(pet.jobs, "curJob", new Job { def = new JobDef { defName = "TowFixtureRest" } });
            Set(pet.jobs, "curDriver", new JobDriver_FollowLeashMaster());
            pet.jobs.curDriver.asleep = false;
            Set(master.pather, "moving", true);

            Assert.False(LeadYourPetUtility.CannotFollowOnItsOwn(link));
            Invoke("TowPassiveLeashPet", link, false);

            Assert.Equal(new IntVec3(40, 0, 20), pet.Position);
        }

        [Fact]
        public void DownedPetsKeepTheirExistingPullBackPath()
        {
            SleepJob();
            Set(pet.health, "healthState", PawnHealthState.Down);
            Set(master.pather, "moving", true);

            Assert.False(LeadYourPetUtility.CannotFollowOnItsOwn(link));
            Invoke("TowPassiveLeashPet", link, false);

            Assert.Equal(new IntVec3(40, 0, 20), pet.Position);
        }

        [Fact]
        public void TowRuleRequiresEveryGuardedCondition()
        {
            Assert.True(LeadYourPetRules.ShouldTowLeashedPet(true, false, true, false, false, 12f, 10f));
            Assert.False(LeadYourPetRules.ShouldTowLeashedPet(false, false, true, false, false, 12f, 10f));
            Assert.False(LeadYourPetRules.ShouldTowLeashedPet(true, true, true, false, false, 12f, 10f));
            Assert.False(LeadYourPetRules.ShouldTowLeashedPet(true, false, false, false, false, 12f, 10f));
            Assert.False(LeadYourPetRules.ShouldTowLeashedPet(true, false, true, true, false, 12f, 10f));
            Assert.False(LeadYourPetRules.ShouldTowLeashedPet(true, false, true, false, true, 12f, 10f));
            Assert.False(LeadYourPetRules.ShouldTowLeashedPet(true, false, true, false, false, 10f, 10f));
        }

        [Fact]
        public void TowCellSitsOnThePetSideOfTheRing()
        {
            Set(pet, "positionInt", new IntVec3(26, 0, 20));
            Assert.False(LeadYourPetUtility.TryFindTowCell(pet, master.Position, 8f, out _));

            Set(pet, "positionInt", new IntVec3(40, 0, 20));
            Assert.True(LeadYourPetUtility.TryFindTowCell(pet, master.Position, 8f, out IntVec3 cell));
            Assert.True(cell.DistanceTo(master.Position) <= 8f);
            Assert.True(cell.x > master.Position.x);
            Assert.True(cell.Standable(map));
        }

        private Job SleepJob()
        {
            Job sleep = new Job { def = new JobDef { defName = "TowFixtureSleep" } };
            Set(pet.jobs, "curJob", sleep);
            JobDriver_FollowLeashMaster driver = Raw<JobDriver_FollowLeashMaster>();
            driver.asleep = true;
            Set(pet.jobs, "curDriver", driver);
            return sleep;
        }

        private void Invoke(string name, params object[] args) =>
            typeof(LeadYourPetGameComponent).GetMethod(name, Fields).Invoke(component, args);

        private void InvokeOnComponent(string name, params object[] args) => Invoke(name, args);

        private static Pawn NewPawn(int id, IntVec3 position)
        {
            Pawn pawn = Raw<Pawn>();
            pawn.def = new ThingDef
            {
                defName = "Ratkin",
                race = new RaceProperties { alwaysAwake = true }
            };
            pawn.thingIDNumber = id;
            pawn.health = Raw<Pawn_HealthTracker>();
            Set(pawn.health, "healthState", PawnHealthState.Mobile);
            pawn.jobs = Raw<Pawn_JobTracker>();
            Set(pawn.jobs, "pawn", pawn);
            pawn.pather = new Pawn_PathFollower(pawn);
            Set(pawn, "mapIndexOrState", (sbyte)0);
            Set(pawn, "positionInt", position);
            return pawn;
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            LeadYourPetMod.Settings = previousSettings;
        }

        private static T Raw<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

        private static FieldInfo Field(object instance, string name)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields);
                if (field != null) return field;
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        private static object Get(object instance, string name) => Field(instance, name).GetValue(instance);
        private static void Set(object instance, string name, object value) => Field(instance, name).SetValue(instance, value);
    }
}
#endif
