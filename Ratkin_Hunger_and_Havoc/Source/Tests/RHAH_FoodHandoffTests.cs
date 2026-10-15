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
        public void TwoVisitorsKeepSeparateFoodWaits()
        {
            ThingDef meal = Meal();
            Verse.Pawn first = Visitor(11);
            Verse.Pawn second = Visitor(12);
            MapFor(first, second);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();

            Assert.False(job.BeginFoodWait(null, 2));
            Assert.False(job.BeginFoodWait(first, 0));
            Assert.True(job.BeginFoodWait(first, 2));
            Assert.False(job.BeginFoodWait(first, 2));
            Assert.True(job.BeginFoodWait(second, 2));
            Assert.False(job.BeginFoodWait(second, 2));

            Assert.True(job.WaitingForFood(first));
            Assert.True(job.WaitingForFood(second));
            Assert.Equal(2, job.FoodStillNeeded(first));
            Assert.Equal(2, job.FoodStillNeeded(second));
            Assert.False(job.FoodFilled());

            Hold(first, meal, 2);
            job.NoteFood(meal);
            Assert.False(job.WaitingForFood(first));
            Assert.Equal(0, job.FoodStillNeeded(first));
            Assert.True(job.WaitingForFood(second));
            Assert.Equal(2, job.FoodStillNeeded(second));
            Assert.False(job.FoodFilled());

            Hold(second, meal, 2);
            Assert.False(job.WaitingForFood(second));
            Assert.Equal(0, job.FoodStillNeeded(second));
            Assert.True(job.FoodFilled());
        }

        [Fact]
        public void RepeatedWaitDoesNotResetDeadlineAndSaveKeepsReceivers()
        {
            Verse.Pawn first = Visitor(21);
            Verse.Pawn second = Visitor(22);
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
                Assert.Equal("Thing_Visitor21", written.SelectSingleNode("/record/foodReceivers/li[1]")?.InnerText);
                Assert.Equal("Thing_Visitor22", written.SelectSingleNode("/record/foodReceivers/li[2]")?.InnerText);
                Assert.Equal("1", written.SelectSingleNode("/record/foodCount")?.InnerText);
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
                Assert.Equal(1, loaded.FoodStillNeeded(first));
                Assert.Equal(1, loaded.FoodStillNeeded(second));
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
                Assert.Equal(firstWaiting ? 6 : 0, loaded.FoodStillNeeded(first));
                Assert.Equal(secondWaiting ? 6 : 0, loaded.FoodStillNeeded(second));
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
                Assert.Equal(firstWaiting ? 6 : 0, reloaded.FoodStillNeeded(first));
                Assert.Equal(secondWaiting ? 6 : 0, reloaded.FoodStillNeeded(second));
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
        public void SecondVisitorStillCollectsWhileFirstIsBeingHauled()
        {
            ThingDef meal = Meal();
            Verse.Pawn first = Visitor(31);
            Verse.Pawn second = Visitor(32);
            Verse.Pawn hauler = Visitor(33);
            MapFor(first, second, hauler);
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor();
            Assert.True(job.BeginFoodWait(first, 2));
            Assert.True(job.BeginFoodWait(second, 2));
            job.NoteFood(meal);

            Delivery(hauler, first, meal, 2);
            Assert.Equal(0, job.FoodLeftToCollect(first));
            Assert.Equal(2, job.FoodLeftToCollect(second));

            Hold(first, meal, 2);
            Assert.False(job.WaitingForFood(first));
            Assert.True(job.WaitingForFood(second));
            Assert.Equal(2, job.FoodStillNeeded(second));
            Assert.Equal(2, job.FoodLeftToCollect(second));
            Assert.False(job.FoodFilled());
            Assert.False(new LordToil_RHAH_WaitFood(job).HasAllRequestedItems);
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

        static void Hold(Verse.Pawn receiver, ThingDef meal, int count)
        {
            Hold(receiver.inventory.innerContainer, meal, count);
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
            ThingDef meal = Uninitialized<ThingDef>();
            meal.defName = "MealSimple";
            meal.category = ThingCategory.Item;
            return meal;
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
