using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_InventoryFeedingTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ProgramState previousProgramState = Current.ProgramState;
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly IRHAH_ApiHost previousHost;

        static RHAH_InventoryFeedingTests()
        {
            // 无界面测试只初始化数据类型 跳过原版贴图加载日志
            using (Log.LockMessages())
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Need).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Pawn_InventoryTracker).TypeHandle);
            }
        }

        public RHAH_InventoryFeedingTests()
        {
            previousHost = (IRHAH_ApiHost)Field(typeof(RHAH_Api), "host").GetValue(null);
            RHAH_Api.Bind(new RHAH_ApiHost());
            RHAH_Mod.Settings = new RHAH_Settings { reliefEnabled = true };
            Current.Game = Uninitialized<Game>();
            Current.ProgramState = ProgramState.Entry;
            Current.Game.tickManager = new TickManager();
            Map map = Uninitialized<Map>();
            map.components = new List<MapComponent>();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map> { map });
        }

        public void Dispose()
        {
            Current.Game = previousGame;
            Current.ProgramState = previousProgramState;
            RHAH_Mod.Settings = previousSettings;
            RHAH_Api.Bind(previousHost);
        }

        [Fact]
        public void OwnMealBypassesMapChecksButKeepsFoodRestrictions()
        {
            Verse.Pawn pawn = Visitor(1);
            Thing meal = Hold(pawn, Meal());
            Assert.Equal(RHAH_FoodReject.None, RHAH_ReliefFood.Reject(pawn, meal, false));

            RHAH_Mod.Settings.SetReliefFoodEnabled(meal.def.defName, false);
            Assert.Equal(RHAH_FoodReject.DisabledFood, RHAH_ReliefFood.Reject(pawn, meal, false));
            RHAH_Mod.Settings.SetReliefFoodEnabled(meal.def.defName, true);
            pawn.def.race.foodType = FoodTypeFlags.Plant;
            Assert.Equal(RHAH_FoodReject.WillNotEat, RHAH_ReliefFood.Reject(pawn, meal, false));
        }

        [Fact]
        public void OtherVisitorsInventoryDoesNotBypassReliefZone()
        {
            Verse.Pawn pawn = Visitor(1);
            Verse.Pawn carrier = Visitor(2);
            Field(typeof(Thing), "mapIndexOrState").SetValue(carrier, (sbyte)-1);
            Field(typeof(Thing), "holdingOwner").SetValue(carrier, new ThingOwner<Verse.Pawn>(pawn.Map));
            Thing meal = Hold(carrier, Meal());
            Assert.Same(pawn.Map, meal.MapHeld);
            Assert.Equal(RHAH_FoodReject.Zone, RHAH_ReliefFood.Reject(pawn, meal, false));
        }

        [Fact]
        public void InedibleInventoryItemDoesNotReceiveOwnershipExemption()
        {
            Verse.Pawn pawn = Visitor(1);
            ThingDef def = Uninitialized<ThingDef>();
            def.defName = "Wood";
            def.category = ThingCategory.Item;
            Thing item = Hold(pawn, def);
            Assert.Equal(RHAH_FoodReject.NotIngestible, RHAH_ReliefFood.Reject(pawn, item, false));
        }

        [Fact]
        public void VisitorWithoutManipulationCannotSelfFeedFromInventory()
        {
            Verse.Pawn pawn = Visitor(1);
            pawn.health.hediffSet.hediffs.Add(new Hediff
            {
                pawn = pawn,
                def = new HediffDef
                {
                    defName = HungerAndHavoc.Pawn.Compat.RHAH_ChildMovement.LearningManipulation,
                    stages = new List<HediffStage> { new HediffStage { minSeverity = 0f } }
                }
            });
            Assert.Equal(RHAH_FoodReject.TooYoung, RHAH_ReliefFood.Reject(pawn, Hold(pawn, Meal()), false));
        }

        [Fact]
        public void UnacceptableFirstItemDoesNotHideLaterEdibleMeal()
        {
            Verse.Pawn pawn = Visitor(1);
            ThingDef unsuitable = Meal();
            unsuitable.defName = "Hay";
            unsuitable.ingestible.foodType = FoodTypeFlags.Plant;
            Hold(pawn, unsuitable);
            Thing meal = Hold(pawn, Meal());

            Thing selected = (Thing)typeof(JobGiver_RHAH_Feed)
                .GetMethod("FoodInInventory", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { pawn });

            Assert.Same(meal, selected);
        }

        static Verse.Pawn Visitor(int id)
        {
            Verse.Pawn pawn = Uninitialized<Verse.Pawn>();
            pawn.thingIDNumber = id;
            pawn.def = Uninitialized<ThingDef>();
            pawn.def.category = ThingCategory.Pawn;
            pawn.def.race = new RaceProperties
            {
                intelligence = Intelligence.Humanlike,
                foodType = FoodTypeFlags.Meal,
                lifeStageAges = new List<LifeStageAge>
                {
                    AdultStage()
                }
            };
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            pawn.needs = new Pawn_NeedsTracker();
            pawn.needs.food = Uninitialized<Need_Food>();
            Field(typeof(Need), "pawn").SetValue(pawn.needs.food, pawn);
            Field(typeof(Need), "curLevelInt").SetValue(pawn.needs.food, 0.3f);
            pawn.inventory = Uninitialized<Pawn_InventoryTracker>();
            Field(typeof(Pawn_InventoryTracker), "pawn").SetValue(pawn.inventory, pawn);
            pawn.inventory.innerContainer = new ThingOwner<Thing>(pawn.inventory, false);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn, def = new HediffDef() };
            mark.comps = new List<HediffComp> { new CompRHAH_Pawn { parent = mark } };
            pawn.health.hediffSet.hediffs.Add(mark);
            RHAH_Api.TryMarkOrigin(pawn, new RHAH_PawnSeed("I-005", 7, 0, RHAH_PawnRole.Refugee, RHAH_Lifecycle.SeekingFood));
            return pawn;
        }

        static LifeStageAge AdultStage()
        {
            LifeStageAge age = Uninitialized<LifeStageAge>();
            age.def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult, bodySizeFactor = 1f, foodMaxFactor = 1f };
            return age;
        }

        static ThingDef Meal()
        {
            ThingDef def = Uninitialized<ThingDef>();
            def.defName = "MealSimple";
            def.category = ThingCategory.Item;
            def.ingestible = new IngestibleProperties { foodType = FoodTypeFlags.Meal, preferability = FoodPreferability.MealSimple, maxNumToIngestAtOnce = 1 };
            Field(typeof(IngestibleProperties), "cachedNutrition").SetValue(def.ingestible, 0.9f);
            return def;
        }

        static Thing Hold(Verse.Pawn pawn, ThingDef def)
        {
            Thing item = new Thing { def = def, stackCount = 1 };
            pawn.inventory.innerContainer.InnerListForReading.Add(item);
            Field(typeof(Thing), "holdingOwner").SetValue(item, pawn.inventory.innerContainer);
            return item;
        }

        static T Uninitialized<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));

        static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
