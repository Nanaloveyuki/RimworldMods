using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_RefugeeCampDeathTests : IDisposable
    {
        readonly Game previousGame = Current.Game;
        readonly ScribeSaver previousSaver = Scribe.saver;
        readonly ScribeLoader previousLoader = Scribe.loader;
        readonly LoadSaveMode previousMode = Scribe.mode;
        readonly bool previousProfiling = DeepProfiler.enabled;
        readonly object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
        readonly List<WorldObjectDef> previousDefs = new List<WorldObjectDef>(DefDatabase<WorldObjectDef>.AllDefsListForReading);
        readonly WorldObjectDef campDef = new WorldObjectDef { defName = "RHAH_CampDeathTest" };
        readonly string path = Path.GetTempFileName();

        public RHAH_RefugeeCampDeathTests()
        {
            Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
            Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
            Scribe.saver = new ScribeSaver();
            Scribe.loader = new ScribeLoader();
            Scribe.mode = LoadSaveMode.Inactive;
            DeepProfiler.enabled = false;
            Field(typeof(Prefs), "data").SetValue(null, new PrefsData { devMode = false });
            DefDatabase<WorldObjectDef>.Add(campDef);
        }

        public void Dispose()
        {
            Scribe.ForceStop();
            Current.Game = previousGame;
            Scribe.saver = previousSaver;
            Scribe.loader = previousLoader;
            Scribe.mode = previousMode;
            DeepProfiler.enabled = previousProfiling;
            Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
            DefDatabase<WorldObjectDef>.Clear();
            foreach (WorldObjectDef def in previousDefs)
            {
                DefDatabase<WorldObjectDef>.Add(def);
            }
            File.Delete(path);
        }

        [Fact]
        public void EmptyCampDoesNotCountAsCleared()
        {
            Assert.False(Camp().AllDead());
        }

        [Fact]
        public void DestroyedDeadPawnsCountTowardKillAll()
        {
            Verse.Pawn first = Resident(101);
            Verse.Pawn second = Resident(102);
            WorldObject_RHAH_RefugeeCamp camp = Camp(first, second);

            Die(first);
            Assert.True(first.Dead);
            Assert.True(first.Destroyed);
            Assert.False(camp.AllDead());

            Die(second);
            Assert.True(camp.AllDead());
        }

        [Theory]
        [InlineData("mobile")]
        [InlineData("downed")]
        [InlineData("captured")]
        public void LivingResidentsPreventClearRegardlessOfDowningOrCapture(string state)
        {
            Verse.Pawn dead = Resident(111);
            Verse.Pawn living = Resident(112);
            Die(dead);
            if (state == "downed")
            {
                Field(typeof(Pawn_HealthTracker), "healthState").SetValue(living.health, PawnHealthState.Down);
                Assert.True(living.Downed);
            }
            else if (state == "captured")
            {
                living.guest = new Pawn_GuestTracker(living);
                Field(typeof(Pawn_GuestTracker), "guestStatusInt").SetValue(living.guest, GuestStatus.Prisoner);
                Assert.True(living.IsPrisoner);
            }

            Assert.False(living.Dead);
            Assert.False(Camp(dead, living).AllDead());
        }

        [Fact]
        public void ScribeRetainsDestroyedDeadReferencesForKillAllAfterReload()
        {
            Verse.Pawn first = Resident(121);
            Verse.Pawn second = Resident(122);
            Die(first);
            Die(second);
            Save(Camp(first, second));

            XmlDocument written = new XmlDocument();
            written.Load(path);
            Assert.Equal(first.GetUniqueLoadID(), written.SelectSingleNode("/camp/residents/li[1]")?.InnerText);
            Assert.Equal(second.GetUniqueLoadID(), written.SelectSingleNode("/camp/residents/li[2]")?.InnerText);

            Verse.Pawn loadedFirst = Resident(121);
            Verse.Pawn loadedSecond = Resident(122);
            Die(loadedFirst);
            Die(loadedSecond);
            WorldObject_RHAH_RefugeeCamp loaded = Load(loadedFirst, loadedSecond);

            Assert.True(loaded.AllDead());
        }

        [Fact]
        public void ScribeKeepsPartialDeathsOngoingUntilTheReloadedSurvivorDies()
        {
            Verse.Pawn dead = Resident(131);
            Verse.Pawn living = Resident(132);
            Die(dead);
            Save(Camp(dead, living));

            Verse.Pawn loadedDead = Resident(131);
            Verse.Pawn loadedLiving = Resident(132);
            Die(loadedDead);
            WorldObject_RHAH_RefugeeCamp loaded = Load(loadedDead, loadedLiving);

            Assert.False(loaded.AllDead());
            Die(loadedLiving);
            Assert.True(loaded.AllDead());
        }

        [Fact]
        public void OldSaveNullResidentIsUnknownRatherThanProofOfDeath()
        {
            Verse.Pawn dead = Resident(141);
            Die(dead);
            Save(Camp(dead, Resident(142)));
            XmlDocument oldSave = new XmlDocument();
            oldSave.Load(path);
            oldSave.SelectSingleNode("/camp/residents/li[2]").InnerText = "null";
            oldSave.Save(path);

            Assert.False(Load(dead).AllDead());
        }

        WorldObject_RHAH_RefugeeCamp Camp(params Verse.Pawn[] residents)
        {
            WorldObject_RHAH_RefugeeCamp camp = new WorldObject_RHAH_RefugeeCamp { def = campDef };
            for (int i = 0; i < residents.Length; i++)
            {
                camp.AddResident(residents[i]);
            }
            return camp;
        }

        void Save(WorldObject_RHAH_RefugeeCamp camp)
        {
            Scribe.saver.InitSaving(path, "camp");
            camp.ExposeData();
            Scribe.saver.FinalizeSaving();
        }

        WorldObject_RHAH_RefugeeCamp Load(params Verse.Pawn[] residents)
        {
            Scribe.loader.InitLoading(path);
            LoadedObjectDirectory directory = (LoadedObjectDirectory)Field(typeof(CrossRefHandler), "loadedObjectDirectory")
                .GetValue(Scribe.loader.crossRefs);
            for (int i = 0; i < residents.Length; i++)
            {
                directory.RegisterLoaded(residents[i]);
            }
            WorldObject_RHAH_RefugeeCamp loaded = new WorldObject_RHAH_RefugeeCamp();
            Scribe.loader.curParent = loaded;
            loaded.ExposeData();
            Scribe.loader.crossRefs.RegisterForCrossRefResolve(loaded);
            Scribe.loader.initer.RegisterForPostLoadInit(loaded);
            Scribe.loader.FinalizeLoading();
            return loaded;
        }

        static Verse.Pawn Resident(int id)
        {
            Verse.Pawn pawn = new Verse.Pawn
            {
                thingIDNumber = id,
                def = new ThingDef { defName = "CampResident", category = ThingCategory.Pawn, race = new RaceProperties() }
            };
            pawn.health = new Pawn_HealthTracker(pawn);
            return pawn;
        }

        static void Die(Verse.Pawn pawn)
        {
            pawn.health.SetDead();
            // 原版 Kill 最终将 Thing 标为 Destroyed；无需在引用判定测试中启动整张地图
            Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)-2);
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }
    }
}
