using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ReliefFoodPatchTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Dictionary<FieldInfo, object> previousJobs = new Dictionary<FieldInfo, object>();
        readonly Map map;
        readonly Area_RHAH_Relief relief;
        readonly MapComponent_RHAH_Map mapState;

        public RHAH_ReliefFoodPatchTests()
        {
            map = (Map)Field(typeof(RHAH_AttackChoiceTests), "map").GetValue(fixture);
            map.info = new MapInfo { Size = new IntVec3(8, 1, 8) };
            map.cellIndices = new CellIndices(map);
            map.areaManager = new AreaManager(map);
            map.lordManager = new LordManager(map);
            relief = Uninitialized<Area_RHAH_Relief>();
            relief.areaManager = map.areaManager;
            Field(typeof(Area), "innerGrid").SetValue(relief, new BoolGrid(map));
            map.areaManager.AllAreas.Add(relief);
            mapState = new MapComponent_RHAH_Map(map);
            map.components.Add(mapState);
            foreach (string name in new[] { "Ingest", "Harvest", "TakeFromOtherInventory" })
            {
                FieldInfo field = Field(typeof(JobDefOf), name);
                previousJobs.Add(field, field.GetValue(null));
                field.SetValue(null, new JobDef { defName = name });
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<FieldInfo, object> previous in previousJobs)
            {
                previous.Key.SetValue(null, previous.Value);
            }
            fixture.Dispose();
        }

        [Theory]
        [InlineData("Ingest")]
        [InlineData("Harvest")]
        [InlineData("TakeFromOtherInventory")]
        public void HungryVisitorCannotBypassReliefThroughVanillaFoodJob(string jobName)
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob(jobName);

            Assert.Null(Filter(pawn, original));
            Assert.False(RHAH_Api.Get(pawn).HasBeenFed);
        }

        [Fact]
        public void VanillaMealInsideReliefRemainsAvailable()
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob();
            relief.InnerGrid[original.targetA.Thing.Position] = true;

            Assert.Same(original, Filter(pawn, original));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DisabledFoodIsRejectedEvenWhenOutsideEatingIsAllowed(bool outside)
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob();
            RHAH_Mod.Settings.allowEatOutsideRelief = outside;
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.EatOutsideRelief, true);
            if (!outside) relief.InnerGrid[original.targetA.Thing.Position] = true;
            RHAH_Mod.Settings.SetReliefFoodEnabled(original.targetA.Thing.def.defName, false);

            Assert.Null(Filter(pawn, original));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void OutsideEatingRequiresSettingAndBehaviorGate(bool setting, bool gate, bool allowed)
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob();
            RHAH_Mod.Settings.allowEatOutsideRelief = setting;
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.EatOutsideRelief, gate);

            Assert.Equal(allowed, Filter(pawn, original) == original);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void FedExemptionRequiresActualFedStateAndSetting(bool fed, bool setting, bool allowed)
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob();
            RHAH_Mod.Settings.ignoreReliefAfterFed = setting;
            if (fed) RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Fed);

            Assert.Equal(allowed, Filter(pawn, original) == original);
        }

        [Fact]
        public void DisabledReliefLeavesVanillaFoodUntouched()
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob();
            RHAH_Mod.Settings.reliefEnabled = false;
            RHAH_Mod.Settings.SetReliefFoodEnabled(original.targetA.Thing.def.defName, false);

            Assert.Same(original, Filter(pawn, original));
        }

        [Fact]
        public void ClosedReliefGateRejectsVanillaFoodInsideArea()
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob();
            relief.InnerGrid[original.targetA.Thing.Position] = true;
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.FeedFromRelief, false);

            Assert.Null(Filter(pawn, original));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ColonistsAndOtherLordsKeepVanillaFoodJobs(bool otherLord)
        {
            Verse.Pawn pawn = Visitor();
            Job original = FoodJob();
            if (otherLord)
            {
                pawn.lord = Uninitialized<Lord>();
                Field(typeof(Lord), "curJob").SetValue(pawn.lord, new LordJob_AssaultColony());
            }
            else
            {
                pawn.SetFactionDirect(Faction.OfPlayer);
            }

            Assert.Same(original, Filter(pawn, original));
        }

        Verse.Pawn Visitor()
        {
            Verse.Pawn pawn = (Verse.Pawn)Method(typeof(RHAH_InventoryFeedingTests), "Visitor")
                .Invoke(null, new object[] { 3100 });
            pawn.mindState = Uninitialized<Pawn_MindState>();
            pawn.mindState.mentalStateHandler = new MentalStateHandler(pawn);
            // Keep rejection tests focused on the original job rather than another food search.
            mapState.SetFoodSearchTick(pawn.thingIDNumber, int.MaxValue);
            return pawn;
        }

        static Job FoodJob(string jobName = "Ingest")
        {
            ThingDef edible = (ThingDef)Method(typeof(RHAH_InventoryFeedingTests), "Meal").Invoke(null, null);
            Thing food = new Thing { def = edible, stackCount = 1 };
            Field(typeof(Thing), "mapIndexOrState").SetValue(food, (sbyte)0);
            Field(typeof(Thing), "positionInt").SetValue(food, new IntVec3(4, 0, 4));
            return new Job { def = (JobDef)Field(typeof(JobDefOf), jobName).GetValue(null), targetA = food };
        }

        static Job Filter(Verse.Pawn pawn, Job original)
        {
            object[] arguments = { pawn, original };
            Method(typeof(RHAH_ReliefFoodPatch), "Postfix").Invoke(null, arguments);
            return (Job)arguments[1];
        }

        static T Uninitialized<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
        static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
