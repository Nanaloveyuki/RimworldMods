using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_FoodHandoffStateTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly IRHAH_ApiHost previousHost;

        public RHAH_FoodHandoffStateTests()
        {
            previousHost = (IRHAH_ApiHost)typeof(RHAH_Api).GetField("host", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            RHAH_Api.Bind(new RHAH_ApiHost());
            Current.Game = Uninitialized<Game>();
            Current.Game.tickManager = new TickManager();
            typeof(Game).GetField("maps", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Current.Game, new List<Map>());
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            RHAH_Api.Bind(previousHost);
        }

        [Fact]
        public void OneExpiredReceiverDoesNotCancelAnotherReceiversDelivery()
        {
            Verse.Pawn first = Visitor(1);
            Verse.Pawn second = Visitor(2);
            MapFor(first, second);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            job.BeginFoodWait(first, 1);
            job.BeginFoodWait(second, 1);
            CompRHAH_Pawn.TryGet(first).SetFoodWait(100);
            CompRHAH_Pawn.TryGet(second).SetFoodWait(300);

            SetTick(200);
            Assert.False(Expired(job));
            Assert.Equal(1, job.FoodStillNeeded(second, Meal()));
            SetTick(300);
            Assert.True(Expired(job));
        }

        [Fact]
        public void PartialDeliveryKeepsTheWaitAndAFullOneEndsIt()
        {
            Verse.Pawn first = Visitor(1);
            Verse.Pawn second = Visitor(2);
            MapFor(first, second);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            job.BeginFoodWait(first, 2);
            job.BeginFoodWait(second, 2);
            ThingDef meal = Meal();
            job.NoteFood(meal);
            job.NoteFoodDelivered(first, new Thing { def = meal }, 1);
            job.NoteFoodDelivered(second, new Thing { def = meal }, 1);
            CompRHAH_Pawn.TryGet(first).SetFoodWait(100);
            CompRHAH_Pawn.TryGet(second).SetFoodWait(300);

            SetTick(200);
            Assert.False(Expired(job));
            Assert.False(new LordToil_RHAH_WaitFood(job).HasAllRequestedItems);
            Assert.True(job.WaitingForFood(first));
            Assert.Equal(1, job.FoodStillNeeded(first, meal));

            job.NoteFoodDelivered(first, new Thing { def = meal }, 1);
            Assert.False(job.WaitingForFood(first));
            Assert.True(job.WaitingForFood(second));
            Assert.False(job.FoodFilled());

            job.NoteFoodDelivered(second, new Thing { def = meal }, 1);
            Assert.True(job.FoodFilled());
            Assert.True(new LordToil_RHAH_WaitFood(job).HasAllRequestedItems);
        }

        [Fact]
        public void ExitingFoodWaitClearsEachReceiversOwnNutrition()
        {
            Verse.Pawn pawn = Visitor(1);
            MapFor(pawn);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            ThingDef previousMeal = Meal();
            job.BeginFoodWait(pawn, 1);
            job.NoteFood(previousMeal);
            job.NoteFoodDelivered(pawn, new Thing { def = previousMeal }, 1);
            LordToil_RHAH_WaitFood wait = new LordToil_RHAH_WaitFood(job);
            Assert.True(wait.HasAllRequestedItems);

            wait.Cleanup();
            Assert.False(job.ListedForFood(pawn));
            Assert.Null(job.RequestedFood);
            Assert.Equal(0d, CompRHAH_Pawn.TryGet(pawn).State.foodNutritionDelivered, 5);
            Assert.True(job.BeginFoodWait(pawn, 2));
            ThingDef nextMeal = RHAH_ArmedFoodPaymentTests.Meal("NutrientPaste", 0.5f, "ludeon.rimworld");
            job.NoteFood(nextMeal);
            Assert.Same(nextMeal, job.RequestedFood);
            Assert.Equal(4, job.FoodStillNeeded(pawn, nextMeal));
            Assert.False(wait.HasAllRequestedItems);
        }


        static ThingDef Meal()
        {
            return RHAH_ArmedFoodPaymentTests.Meal("MealSimple", 0.9f, "ludeon.rimworld");
        }

        static void MapFor(params Verse.Pawn[] pawns)
        {
            Map map = Uninitialized<Map>();
            map.mapPawns = Uninitialized<MapPawns>();
            typeof(MapPawns).GetField("pawnsSpawned", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(map.mapPawns, new List<Verse.Pawn>(pawns));
            Find.Maps.Add(map);
            foreach (Verse.Pawn pawn in pawns)
            {
                typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pawn, (sbyte)(Find.Maps.Count - 1));
            }
        }

        static Verse.Pawn Visitor(int id)
        {
            Verse.Pawn pawn = new Verse.Pawn { thingIDNumber = id };
            pawn.def = Uninitialized<ThingDef>();
            pawn.def.category = ThingCategory.Pawn;
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            pawn.health.hediffSet = new HediffSet(pawn);
            pawn.inventory = Uninitialized<Pawn_InventoryTracker>();
            typeof(Pawn_HealthTracker).GetField("healthState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(pawn.health, PawnHealthState.Mobile);
            typeof(Pawn_InventoryTracker).GetField("innerContainer", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(pawn.inventory, new ThingOwner<Thing>(pawn.inventory, false));
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn };
            CompRHAH_Pawn comp = new CompRHAH_Pawn { parent = mark };
            mark.comps = new List<HediffComp> { comp };
            pawn.health.hediffSet.hediffs.Add(mark);
            comp.ApplySeed(new RHAH_PawnSeed("I-005", 7, 0, RHAH_PawnRole.Refugee, RHAH_Lifecycle.SeekingFood));
            return pawn;
        }

        static void SetTick(int tick)
        {
            typeof(TickManager).GetField("ticksGameInt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Find.TickManager, tick);
        }

        static bool Expired(LordJob_RHAH_Visitor job)
        {
            return (bool)typeof(LordJob_RHAH_Visitor).GetMethod("FoodWaitExpired", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(job, null);
        }

        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
    }
}
