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
            Assert.Equal(1, job.FoodStillNeeded(second));
            SetTick(300);
            Assert.True(Expired(job));
        }

        [Fact]
        public void DeliveredReceiverDoesNotEndAnotherReceiversWait()
        {
            Verse.Pawn first = Visitor(1);
            Verse.Pawn second = Visitor(2);
            MapFor(first, second);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            job.BeginFoodWait(first, 1);
            job.BeginFoodWait(second, 1);
            ThingDef meal = Uninitialized<ThingDef>();
            job.NoteFood(meal);
            first.inventory.innerContainer.InnerListForReading.Add(new Thing { def = meal, stackCount = 1 });
            CompRHAH_Pawn.TryGet(first).SetFoodWait(100);
            CompRHAH_Pawn.TryGet(second).SetFoodWait(300);

            SetTick(200);
            Assert.False(Expired(job));
            Assert.False(new LordToil_RHAH_WaitFood(job).HasAllRequestedItems);
            Assert.Equal(0, job.FoodStillNeeded(first));
            Assert.Equal(1, job.FoodStillNeeded(second));
        }

        [Fact]
        public void ExitingFoodWaitAllowsANewRoundWithDifferentFood()
        {
            Verse.Pawn pawn = Visitor(1);
            MapFor(pawn);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            ThingDef previousMeal = Uninitialized<ThingDef>();
            job.BeginFoodWait(pawn, 1);
            job.NoteFood(previousMeal);
            pawn.inventory.innerContainer.InnerListForReading.Add(new Thing { def = previousMeal, stackCount = 1 });
            LordToil_RHAH_WaitFood wait = new LordToil_RHAH_WaitFood(job);
            Assert.True(wait.HasAllRequestedItems);

            wait.Cleanup();
            Assert.False(job.ListedForFood(pawn));
            Assert.Null(job.RequestedFood);
            Assert.True(job.BeginFoodWait(pawn, 2));
            ThingDef nextMeal = Uninitialized<ThingDef>();
            job.NoteFood(nextMeal);
            Assert.Same(nextMeal, job.RequestedFood);
            Assert.Equal(2, job.FoodStillNeeded(pawn));
            Assert.False(wait.HasAllRequestedItems);
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
