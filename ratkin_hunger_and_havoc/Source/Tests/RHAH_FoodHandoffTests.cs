using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using HungerAndHavoc.Api;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_FoodHandoffTests : System.IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly LoadSaveMode previousMode = Scribe.mode;
        readonly ScribeSaver previousSaver = Scribe.saver;
        readonly ScribeLoader previousLoader = Scribe.loader;
        readonly bool previousProfiling = DeepProfiler.enabled;
        readonly object previousPrefs = typeof(Prefs).GetField("data", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        public RHAH_FoodHandoffTests()
        {
            RimWorldAssemblies.EnsureResolved();
            Current.Game = Uninitialized<Game>();
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            Scribe.mode = LoadSaveMode.Inactive;
            Scribe.saver = new ScribeSaver();
            Scribe.loader = new ScribeLoader();
            DeepProfiler.enabled = false;
            typeof(Prefs).GetField("data", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, new PrefsData { devMode = false });
        }

        public void Dispose()
        {
            Scribe.ForceStop();
            Current.Game = previousGame;
            Scribe.mode = previousMode;
            Scribe.saver = previousSaver;
            Scribe.loader = previousLoader;
            DeepProfiler.enabled = previousProfiling;
            typeof(Prefs).GetField("data", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, previousPrefs);
        }

        [Fact]
        public void DeliveredNutritionFoldsIntoWholeMeals()
        {
            ThingDef meal = Meal();
            ThingDef biscuit = Biscuit();
            Verse.Pawn visitor = Visitor(11);
            MapFor(visitor);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();

            Assert.False(job.BeginFoodWait(null, 2));
            Assert.False(job.BeginFoodWait(visitor, 0));
            Assert.True(job.BeginFoodWait(visitor, 2));
            Assert.False(job.BeginFoodWait(visitor, 2));

            Assert.True(job.WaitingForFood(visitor));
            Assert.Equal(2, job.FoodStillNeeded(visitor, meal));
            Assert.False(job.FoodFilled());

            // 先给别的食物 按营养折算 不锁食物类型
            Deliver(job, visitor, biscuit, 10);
            Assert.True(job.WaitingForFood(visitor));
            Assert.Equal(2, job.FoodStillNeeded(visitor, meal));
            Assert.Equal(26, job.FoodStillNeeded(visitor, biscuit));

            Deliver(job, visitor, meal, 1);
            Assert.True(job.WaitingForFood(visitor));
            Assert.Equal(1, job.FoodStillNeeded(visitor, meal));
            Assert.False(job.FoodFilled());

            Deliver(job, visitor, meal, 1);
            Assert.False(job.WaitingForFood(visitor));
            Assert.Equal(0, job.FoodStillNeeded(visitor, meal));
            Assert.True(job.FoodFilled());
        }

        [Fact]
        public void LatestFoodIsOnlyALabelAndEachReceiverKeepsItsOwnLedger()
        {
            ThingDef meal = Meal();
            ThingDef biscuit = Biscuit();
            Verse.Pawn first = Visitor(21);
            Verse.Pawn second = Visitor(22);
            MapFor(first, second);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            Assert.True(job.BeginFoodWait(first, 2));
            Assert.True(job.BeginFoodWait(second, 2));

            job.NoteFood(biscuit);
            Assert.Same(biscuit, job.RequestedFood);
            job.NoteFood(meal);
            Assert.Same(meal, job.RequestedFood);

            // 交付账记在收到的那个来客身上 给谁算谁
            Deliver(job, first, meal, 2);
            Assert.False(job.WaitingForFood(first));
            Assert.True(job.WaitingForFood(second));
            Assert.Equal(2, job.FoodStillNeeded(second, meal));
            Assert.False(job.FoodFilled());

            Deliver(job, second, meal, 2);
            Assert.False(job.WaitingForFood(second));
            Assert.True(job.FoodFilled());
        }

        [Fact]
        public void RepeatedWaitDoesNotResetDeadlineAndSaveKeepsReceivers()
        {
            ThingDef meal = Meal();
            Verse.Pawn first = Visitor(31);
            Verse.Pawn second = Visitor(32);
            MapFor(first, second);
            CompRHAH_Pawn firstComp = CompRHAH_Pawn.TryGet(first);
            firstComp.SetFoodWait(9000);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            Assert.True(job.BeginFoodWait(first, 1));
            Assert.True(job.BeginFoodWait(second, 1));
            Assert.False(job.BeginFoodWait(first, 1));
            Assert.Equal(9000, firstComp.State.foodWaitUntilTick);

            string path = Path.GetTempFileName();
            try
            {
                LoadedObjectDirectory directory = (LoadedObjectDirectory)Field(typeof(CrossRefHandler), "loadedObjectDirectory")
                    .GetValue(Scribe.loader.crossRefs);
                Scribe.saver.InitSaving(path, "record");
                job.ExposeData();
                Scribe.saver.FinalizeSaving();

                XmlDocument written = new XmlDocument();
                written.Load(path);
                Assert.Equal("Thing_Visitor31", written.SelectSingleNode("/record/foodReceivers/li[1]")?.InnerText);
                Assert.Equal("Thing_Visitor32", written.SelectSingleNode("/record/foodReceivers/li[2]")?.InnerText);
                Assert.Equal("1", written.SelectSingleNode("/record/foodCount")?.InnerText);
                Assert.Null(written.SelectSingleNode("/record/foodReceivers/li[3]"));
                Assert.Null(written.SelectSingleNode("/record/foodReceiver"));

                Scribe.loader.InitLoading(path);
                directory.RegisterLoaded(first);
                directory.RegisterLoaded(second);
                LordJob_RHAH_Visitor loaded = new LordJob_RHAH_Visitor();
                Scribe.loader.curParent = loaded;
                loaded.ExposeData();
                Scribe.loader.crossRefs.RegisterForCrossRefResolve(loaded);
                Scribe.loader.initer.RegisterForPostLoadInit(loaded);
                Scribe.loader.FinalizeLoading();
                Assert.True(loaded.WaitingForFood(first));
                Assert.True(loaded.WaitingForFood(second));
                Assert.Equal(1, loaded.FoodStillNeeded(first, meal));
                Assert.Equal(1, loaded.FoodStillNeeded(second, meal));
                Assert.False(loaded.BeginFoodWait(first, 1));
                Assert.Equal(9000, firstComp.State.foodWaitUntilTick);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void NeutralCeasefireSurvivesSaveLoadResaveWhileMissingLegacyAttitudeFallsBack(bool legacy)
        {
            string path = Path.GetTempFileName();
            try
            {
                CompRHAH_Pawn comp = new CompRHAH_Pawn();
                comp.ApplySeed(new RHAH_PawnSeed(attitudeAtArrival: RHAH_Attitude.Hostile));
                comp.State.SetAttitude(RHAH_Attitude.Neutral);
                Scribe.saver.InitSaving(path, "record");
                comp.CompExposeData();
                Scribe.saver.FinalizeSaving();
                XmlDocument saved = new XmlDocument();
                saved.Load(path);
                Assert.Equal("Neutral", saved.SelectSingleNode("/record/attitude")?.InnerText);
                if (legacy)
                {
                    XmlNode attitude = saved.SelectSingleNode("/record/attitude");
                    attitude.ParentNode.RemoveChild(attitude);
                    saved.Save(path);
                }
                string original = File.ReadAllText(path);
                CompRHAH_Pawn loaded = LoadComp(path);
                RHAH_Attitude expected = legacy ? RHAH_Attitude.Hostile : RHAH_Attitude.Neutral;
                Assert.Equal(expected, loaded.State.attitude);
                Assert.Equal(RHAH_Attitude.Hostile, loaded.State.attitudeAtArrival);
                Assert.Equal(original, File.ReadAllText(path));
                Scribe.saver.InitSaving(path, "record");
                loaded.CompExposeData();
                Scribe.saver.FinalizeSaving();
                Assert.Equal(expected, LoadComp(path).State.attitude);
            }
            finally
            {
                File.Delete(path);
            }
        }

        static CompRHAH_Pawn LoadComp(string path)
        {
            CompRHAH_Pawn comp = new CompRHAH_Pawn();
            Scribe.loader.InitLoading(path);
            CompRecord record = new CompRecord(comp);
            Scribe.loader.curParent = record;
            record.ExposeData();
            Scribe.loader.crossRefs.RegisterForCrossRefResolve(record);
            Scribe.loader.initer.RegisterForPostLoadInit(record);
            Scribe.loader.FinalizeLoading();
            return comp;
        }

        sealed class CompRecord : IExposable
        {
            readonly CompRHAH_Pawn comp;

            internal CompRecord(CompRHAH_Pawn comp)
            {
                this.comp = comp;
            }

            public void ExposeData()
            {
                comp.CompExposeData();
            }
        }

        [Theory]
        [InlineData("<foodReceiver>Thing_Visitor41</foodReceiver>", true, false)]
        [InlineData("<foodReceiver IsNull='True'/>", false, false)]
        [InlineData("<foodReceiver>null</foodReceiver>", false, false)]
        [InlineData("<foodReceiver/>", false, false)]
        [InlineData("", false, false)]
        [InlineData("<foodReceiver>Thing_Visitor41</foodReceiver><foodReceivers><li>Thing_Visitor42</li></foodReceivers>", false, true)]
        [InlineData("<foodReceiver>Thing_Visitor41</foodReceiver><foodReceivers/>", false, false)]
        [InlineData("<foodReceiver>Thing_Visitor41</foodReceiver><foodReceivers IsNull='True'/>", false, false)]
        public void LegacyFoodWaitMigratesBeforeReferenceLoadingAndSavesOnlyNewKey(
            string receiverXml, bool firstWaiting, bool secondWaiting)
        {
            ThingDef meal = Meal();
            Verse.Pawn first = Visitor(41);
            Verse.Pawn second = Visitor(42);
            MapFor(first, second);
            CompRHAH_Pawn.TryGet(first).SetFoodWait(9000);
            string path = Path.GetTempFileName();
            try
            {
                XmlDocument original = new XmlDocument();
                original.LoadXml("<record>" + receiverXml + "<foodCount>6</foodCount></record>");
                original.Save(path);
                string originalBytes = File.ReadAllText(path);
                LordJob_RHAH_Visitor loaded = LoadWait(path, first, second);
                Assert.Equal(firstWaiting, loaded.WaitingForFood(first));
                Assert.Equal(secondWaiting, loaded.WaitingForFood(second));
                Assert.Equal(firstWaiting ? 6 : 0, loaded.FoodStillNeeded(first, meal));
                Assert.Equal(secondWaiting ? 6 : 0, loaded.FoodStillNeeded(second, meal));
                Assert.Equal(9000, CompRHAH_Pawn.TryGet(first).State.foodWaitUntilTick);
                Assert.Equal(originalBytes, File.ReadAllText(path));

                Scribe.saver.InitSaving(path, "record");
                loaded.ExposeData();
                Scribe.saver.FinalizeSaving();
                XmlDocument saved = new XmlDocument();
                saved.Load(path);
                Assert.Null(saved.SelectSingleNode("/record/foodReceiver"));
                Assert.Equal(firstWaiting ? "Thing_Visitor41" : secondWaiting ? "Thing_Visitor42" : null,
                    saved.SelectSingleNode("/record/foodReceivers/li")?.InnerText);
                LordJob_RHAH_Visitor reloaded = LoadWait(path, first, second);
                Assert.Equal(firstWaiting, reloaded.WaitingForFood(first));
                Assert.Equal(secondWaiting, reloaded.WaitingForFood(second));
                Assert.Equal(firstWaiting ? 6 : 0, reloaded.FoodStillNeeded(first, meal));
                Assert.Equal(secondWaiting ? 6 : 0, reloaded.FoodStillNeeded(second, meal));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void DeliveredNutritionRoundTripsThroughSave()
        {
            ThingDef meal = Meal();
            Verse.Pawn visitor = Visitor(51);
            MapFor(visitor);
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(visitor);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            Assert.True(job.BeginFoodWait(visitor, 2));
            Deliver(job, visitor, meal, 1);
            Assert.Equal(0.9, comp.State.foodNutritionDelivered, 5);
            Assert.True(job.WaitingForFood(visitor));
            Assert.Equal(1, job.FoodStillNeeded(visitor, meal));

            string path = Path.GetTempFileName();
            try
            {
                Scribe.saver.InitSaving(path, "record");
                comp.CompExposeData();
                Scribe.saver.FinalizeSaving();
                XmlDocument written = new XmlDocument();
                written.Load(path);
                Assert.NotNull(written.SelectSingleNode("/record/foodNutritionDelivered"));

                CompRHAH_Pawn loaded = LoadComp(path);
                Assert.Equal(0.9, loaded.State.foodNutritionDelivered, 5);
                Deliver(job, visitor, meal, 1);
                Assert.False(job.WaitingForFood(visitor));
                Assert.True(job.FoodFilled());
            }
            finally
            {
                File.Delete(path);
            }
        }

        static LordJob_RHAH_Visitor LoadWait(string path, params Verse.Pawn[] pawns)
        {
            Scribe.loader.InitLoading(path);
            LoadedObjectDirectory directory = (LoadedObjectDirectory)Field(typeof(CrossRefHandler), "loadedObjectDirectory")
                .GetValue(Scribe.loader.crossRefs);
            foreach (Verse.Pawn pawn in pawns)
            {
                directory.RegisterLoaded(pawn);
            }
            LordJob_RHAH_Visitor loaded = new LordJob_RHAH_Visitor();
            Scribe.loader.curParent = loaded;
            loaded.ExposeData();
            Scribe.loader.crossRefs.RegisterForCrossRefResolve(loaded);
            Scribe.loader.initer.RegisterForPostLoadInit(loaded);
            Scribe.loader.FinalizeLoading();
            return loaded;
        }

        [Fact]
        public void MenuUnitsArePreviewedBeforeRegistrationAndTheLoopSignalStaysBinary()
        {
            ThingDef meal = Meal();
            Verse.Pawn visitor = Visitor(61);
            MapFor(visitor);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();

            // 未登记时菜单按设置里的份数预演 不能是 0
            Assert.Equal(0, job.FoodStillNeeded(visitor, meal));
            Assert.Equal(2, job.FoodUnitsFor(visitor, meal, 2));

            Assert.True(job.BeginFoodWait(visitor, 2));
            Assert.True(job.WaitingForFood(visitor));
            Assert.Equal(2, job.FoodUnitsFor(visitor, meal, 2));
            Assert.Equal(1, job.FoodLeftToCollect(visitor));

            Deliver(job, visitor, meal, 1);
            Assert.Equal(1, job.FoodUnitsFor(visitor, meal, 2));
            Assert.Equal(1, job.FoodLeftToCollect(visitor));

            Deliver(job, visitor, meal, 1);
            Assert.Equal(0, job.FoodUnitsFor(visitor, meal, 2));
            Assert.Equal(0, job.FoodLeftToCollect(visitor));
            Assert.True(job.FoodFilled());
        }

        [Fact]
        public void HaulUnitsFollowTheFoodInHandAndSkipOwnTrip()
        {
            ThingDef meal = Meal();
            ThingDef biscuit = Biscuit();
            Verse.Pawn first = Visitor(71);
            Verse.Pawn second = Visitor(72);
            Verse.Pawn hauler = Visitor(73);
            MapFor(first, second, hauler);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            Assert.True(job.BeginFoodWait(first, 2));
            Assert.True(job.BeginFoodWait(second, 2));
            job.NoteFood(biscuit);

            Thing mealStack = new Thing { def = meal };
            Thing biscuitStack = new Thing { def = biscuit };

            // 这趟搬什么就按什么折件 不看上一次给过什么
            Assert.Equal(2, job.HaulUnitsFor(second, mealStack, null));
            Assert.Equal(36, job.HaulUnitsFor(second, biscuitStack, null));

            // 别人在途的那一趟要扣 自己这一趟不扣 扣完为 0 就是这一趟不用搬
            JobDriver_GiveToPawn driver = Delivery(hauler, second, meal, 2);
            Assert.Equal(0, job.HaulUnitsFor(second, mealStack, null));
            Assert.Equal(2, job.HaulUnitsFor(second, mealStack, driver));
            Assert.Equal(2, job.HaulUnitsFor(first, mealStack, driver));
        }


        static JobDriver_GiveToPawn Delivery(Verse.Pawn hauler, Verse.Pawn receiver, ThingDef meal, int carried)
        {
            JobDriver_GiveToPawn driver = Uninitialized<JobDriver_GiveToPawn>();
            driver.pawn = hauler;
            driver.job = new Job();
            driver.job.targetB = receiver;
            hauler.jobs.curDriver = driver;
            hauler.carryTracker = new Pawn_CarryTracker(hauler);
            if (carried > 0)
            {
                Hold(hauler.carryTracker.innerContainer, meal, carried);
            }

            return driver;
        }

        static void Hold(ThingOwner<Thing> container, ThingDef meal, int count)
        {
            container.InnerListForReading.Add(new Thing { def = meal, stackCount = count });
        }


        static Map MapFor(params Verse.Pawn[] pawns)
        {
            Map map = Uninitialized<Map>();
            map.mapPawns = Uninitialized<MapPawns>();
            Field(typeof(MapPawns), "pawnsSpawned").SetValue(map.mapPawns, new List<Verse.Pawn>(pawns));
            Find.Maps.Add(map);
            for (int i = 0; i < pawns.Length; i++)
            {
                Field(typeof(Thing), "mapIndexOrState").SetValue(pawns[i], (sbyte)(Find.Maps.Count - 1));
            }

            return map;
        }

        static Verse.Pawn Visitor(int id)
        {
            Verse.Pawn pawn = new Verse.Pawn();
            pawn.thingIDNumber = id;
            pawn.def = Uninitialized<ThingDef>();
            pawn.def.defName = "Visitor";
            pawn.def.category = ThingCategory.Pawn;
            pawn.health = Uninitialized<Pawn_HealthTracker>();
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            pawn.inventory = Uninitialized<Pawn_InventoryTracker>();
            Field(typeof(Pawn_InventoryTracker), "pawn").SetValue(pawn.inventory, pawn);
            Field(typeof(Pawn_InventoryTracker), "innerContainer").SetValue(pawn.inventory,
                new ThingOwner<Thing>(pawn.inventory, false));
            pawn.jobs = Uninitialized<Pawn_JobTracker>();
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn };
            mark.comps = new List<HediffComp> { new CompRHAH_Pawn { parent = mark } };
            pawn.health.hediffSet.hediffs.Add(mark);
            return pawn;
        }

        static ThingDef Meal()
        {
            return RHAH_ArmedFoodPaymentTests.Meal("MealSimple", 0.9f, "ludeon.rimworld");
        }

        static ThingDef Biscuit()
        {
            return RHAH_ArmedFoodPaymentTests.Meal("Pemmican", 0.05f, "ludeon.rimworld");
        }

        static void Deliver(LordJob_RHAH_Visitor job, Verse.Pawn receiver, ThingDef food, int count)
        {
            job.NoteFoodDelivered(receiver, new Thing { def = food, stackCount = count }, count);
        }

        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        static FieldInfo Field(System.Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
    }
}
