using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_DoorPatchTests : IDisposable
    {
        readonly RHAH_Settings previousSettings = RHAH_Mod.Settings;
        readonly Game previousGame = Current.Game;
        readonly IRHAH_ApiHost previousHost;
        readonly FactionDef[] previousDefs;
        readonly FactionDef[] attitudeDefs = new FactionDef[5];

        public RHAH_DoorPatchTests()
        {
            FieldInfo binding = typeof(DefOfHelper).GetField("bindingNow", BindingFlags.Static | BindingFlags.NonPublic);
            bool previousBinding = (bool)binding.GetValue(null);
            try
            {
                binding.SetValue(null, true);
                RuntimeHelpers.RunClassConstructor(typeof(RHAH_DefOf).TypeHandle);
            }
            finally
            {
                binding.SetValue(null, previousBinding);
            }
            previousDefs = new[]
            {
                RHAH_DefOf.RHAH_Faction_Hostile, RHAH_DefOf.RHAH_Faction_LeaningHostile,
                RHAH_DefOf.RHAH_Faction_Neutral, RHAH_DefOf.RHAH_Faction_LeaningFriendly,
                RHAH_DefOf.RHAH_Faction_Friendly
            };
            for (int i = 0; i < attitudeDefs.Length; i++)
            {
                attitudeDefs[i] = new FactionDef { hidden = true };
            }
            SetDefs(attitudeDefs);
            previousHost = (IRHAH_ApiHost)typeof(RHAH_Api).GetField("host", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            RHAH_Api.Bind(new RHAH_ApiHost());
            RHAH_Mod.Settings = new RHAH_Settings();
            Current.Game = (Game)FormatterServices.GetUninitializedObject(typeof(Game));
            typeof(Game).GetField("maps", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Current.Game, new List<Map>());
        }

        public void Dispose()
        {
            RHAH_Mod.Settings = previousSettings;
            Current.Game = previousGame;
            RHAH_Api.Bind(previousHost);
            SetDefs(previousDefs);
        }

        [Theory]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        public void PlayerWorkersWithActiveOriginKeepVanillaDoorPermission(int kind, bool allowed)
        {
            RHAH_Mod.Settings.SetAllowVisitorDoors(allowed);
            Verse.Pawn pawn = Member(new Faction { def = new FactionDef { isPlayer = true } });
            CompRHAH_Pawn.TryGet(pawn).SetStay((int)kind, 600000, 600000);
            Assert.True(RHAH_Api.IsVisitor(pawn));
            Assert.True(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void ActiveVisitorsCannotOpenDoors(int index)
        {
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[index] });
            Assert.False(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(4, false)]
        [InlineData(0, true)]
        [InlineData(4, true)]
        public void LeavingVisitorsKeepVanillaDoorPermission(int index, bool allowed)
        {
            RHAH_Mod.Settings.SetAllowVisitorDoors(allowed);
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[index] });
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            Assert.True(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OtherFactionsAndFactionlessDeparturesKeepVanillaPermission(bool allowed)
        {
            RHAH_Mod.Settings.SetAllowVisitorDoors(allowed);
            Assert.True(OpenResult(Member(new Faction { def = new FactionDef() }), true));
            Assert.False(OpenResult(Member(new Faction { def = new FactionDef() }), false));
            Verse.Pawn pawn = Member(null);
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            Assert.True(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReleasedAndUnmarkedPawnsAreNotRestricted(bool allowed)
        {
            RHAH_Mod.Settings.SetAllowVisitorDoors(allowed);
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[2] });
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Released);
            Assert.True(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
            pawn.health.hediffSet.hediffs.Clear();
            Assert.True(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void AllowedSeekingVisitorsKeepVanillaDoorPermission(int index)
        {
            RHAH_Mod.Settings.SetAllowVisitorDoors(true);
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[index] });
            Assert.True(OpenResult(pawn, true));
            Assert.False(OpenResult(pawn, false));
        }

        [Fact]
        public void MissingSettingsKeepDepartureOnlyBehavior()
        {
            RHAH_Mod.Settings = null;
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[2] });
            Assert.False(OpenResult(pawn, true));
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            Assert.True(OpenResult(pawn, true));
        }

        [Theory]
        [InlineData(false, RHAH_Lifecycle.Leaving)]
        [InlineData(true, RHAH_Lifecycle.Leaving)]
        [InlineData(true, RHAH_Lifecycle.SeekingFood)]
        public void HostileThreatBlocksVisitorsRegardlessOfSetting(bool allowed, RHAH_Lifecycle lifecycle)
        {
            RHAH_Mod.Settings.SetAllowVisitorDoors(allowed);
            Verse.Pawn pawn = Member(new Faction { def = attitudeDefs[2] });
            RHAH_Api.SetLifecycle(pawn, lifecycle);
            Map map = AddMap(pawn);
            Harmony harmony = new Harmony("rhah.door.hostile-threat.test");
            MethodInfo method = typeof(GenHostility).GetMethod(nameof(GenHostility.AnyHostileActiveThreatToPlayer));
            MethodInfo prefix = typeof(RHAH_DoorPatchTests).GetMethod(nameof(ReportHostileThreat), BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                Assert.True(GenHostility.AnyHostileActiveThreatToPlayer(map));
                Assert.False(OpenResult(pawn, true));
                Assert.False(OpenResult(pawn, false));
            }
            finally
            {
                harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id);
            }
        }

        [Fact]
        public void DoorSettingInvalidatesOnlyAffectedReachabilityAndOnlyWhenChanged()
        {
            Verse.Pawn visitor = Member(new Faction { def = attitudeDefs[2] });
            Verse.Pawn other = Member(new Faction { def = new FactionDef() });
            Map map = AddMap(visitor, other);
            ReachabilityCache cache = (ReachabilityCache)typeof(Reachability)
                .GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map.reachability);
            District first = new District { ID = 1 };
            District second = new District { ID = 2 };
            TraverseParms visitorParms = new TraverseParms { pawn = visitor };
            TraverseParms otherParms = new TraverseParms { pawn = other };
            cache.AddCachedResult(first, second, visitorParms, false);
            cache.AddCachedResult(first, second, otherParms, false);

            RHAH_Mod.Settings.SetAllowVisitorDoors(true);
            Assert.Equal(BoolUnknown.Unknown, cache.CachedResultFor(first, second, visitorParms));
            Assert.Equal(BoolUnknown.False, cache.CachedResultFor(first, second, otherParms));

            cache.AddCachedResult(first, second, visitorParms, true);
            RHAH_Mod.Settings.SetAllowVisitorDoors(true);
            Assert.Equal(BoolUnknown.True, cache.CachedResultFor(first, second, visitorParms));
            RHAH_Mod.Settings.SetAllowVisitorDoors(false);
            Assert.Equal(BoolUnknown.Unknown, cache.CachedResultFor(first, second, visitorParms));

            RHAH_Mod.Settings.SetAllowVisitorDoors(true);
            cache.AddCachedResult(first, second, visitorParms, true);
            RHAH_Mod.Settings.ResetToDefaults();
            Assert.False(RHAH_Mod.Settings.allowVisitorDoors);
            Assert.Equal(BoolUnknown.Unknown, cache.CachedResultFor(first, second, visitorParms));
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("<famineVisitorsOpenDoors>False</famineVisitorsOpenDoors>", false)]
        [InlineData("<famineVisitorsOpenDoors>True</famineVisitorsOpenDoors>", true)]
        public void DoorSettingPreservesHistoricalSaveKey(string savedField, bool expected)
        {
            ScribeSaver previousSaver = Scribe.saver;
            ScribeLoader previousLoader = Scribe.loader;
            LoadSaveMode previousMode = Scribe.mode;
            bool previousProfiling = DeepProfiler.enabled;
            FieldInfo prefs = typeof(Prefs).GetField("data", BindingFlags.Static | BindingFlags.NonPublic);
            object previousPrefs = prefs.GetValue(null);
            string path = Path.GetTempFileName();
            try
            {
                Scribe.saver = new ScribeSaver();
                Scribe.loader = new ScribeLoader();
                Scribe.mode = LoadSaveMode.Inactive;
                DeepProfiler.enabled = false;
                prefs.SetValue(null, new PrefsData { devMode = false });
                XmlDocument fixture = new XmlDocument();
                fixture.LoadXml("<settings>" + savedField + "</settings>");
                fixture.Save(path);

                RHAH_Settings loaded = LoadSettings(path);
                Assert.Equal(expected, loaded.allowVisitorDoors);
                loaded.SetAllowVisitorDoors(true);
                Scribe.saver.InitSaving(path, "settings");
                loaded.ExposeData();
                Scribe.saver.FinalizeSaving();
                XmlDocument written = new XmlDocument();
                written.Load(path);
                Assert.Equal("True", written.SelectSingleNode("/settings/famineVisitorsOpenDoors")?.InnerText);
                Assert.Null(written.SelectSingleNode("/settings/allowVisitorDoors"));
                Assert.True(LoadSettings(path).allowVisitorDoors);
            }
            finally
            {
                Scribe.ForceStop();
                Scribe.saver = previousSaver;
                Scribe.loader = previousLoader;
                Scribe.mode = previousMode;
                DeepProfiler.enabled = previousProfiling;
                prefs.SetValue(null, previousPrefs);
                File.Delete(path);
            }
        }

        static RHAH_Settings LoadSettings(string path)
        {
            Scribe.loader.InitLoading(path);
            RHAH_Settings settings = new RHAH_Settings();
            Scribe.loader.curParent = settings;
            settings.ExposeData();
            Scribe.loader.initer.RegisterForPostLoadInit(settings);
            Scribe.loader.FinalizeLoading();
            return settings;
        }

        static bool ReportHostileThreat(ref bool __result)
        {
            __result = true;
            return false;
        }

        static Map AddMap(params Verse.Pawn[] pawns)
        {
            Map map = (Map)FormatterServices.GetUninitializedObject(typeof(Map));
            map.components = new List<MapComponent>();
            map.mapPawns = (MapPawns)FormatterServices.GetUninitializedObject(typeof(MapPawns));
            map.reachability = new Reachability(map);
            typeof(MapPawns).GetField("pawnsSpawned", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(map.mapPawns, new List<Verse.Pawn>(pawns));
            Find.Maps.Add(map);
            for (int i = 0; i < pawns.Length; i++)
            {
                typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(pawns[i], (sbyte)(Find.Maps.Count - 1));
            }
            return map;
        }

        static bool OpenResult(Verse.Pawn pawn, bool original)
        {
            object[] args = { null, pawn, original };
            typeof(RHAH_DoorPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            return (bool)args[2];
        }

        static Verse.Pawn Member(Faction faction)
        {
            Verse.Pawn pawn = new Verse.Pawn();
            pawn.def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            pawn.def.category = ThingCategory.Pawn;
            pawn.SetFactionDirect(faction);
            pawn.health = (Pawn_HealthTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_HealthTracker));
            pawn.health.hediffSet = new HediffSet(pawn);
            Hediff_RHAH_Mark mark = new Hediff_RHAH_Mark { pawn = pawn, def = new HediffDef() };
            CompRHAH_Pawn comp = new CompRHAH_Pawn { parent = mark };
            mark.comps = new List<HediffComp> { comp };
            pawn.health.hediffSet.hediffs.Add(mark);
            comp.ApplySeed(new RHAH_PawnSeed("I-005", 7, 0, RHAH_PawnRole.Refugee, RHAH_Lifecycle.SeekingFood));
            return pawn;
        }

        static void SetDefs(FactionDef[] defs)
        {
            RHAH_DefOf.RHAH_Faction_Hostile = defs[0];
            RHAH_DefOf.RHAH_Faction_LeaningHostile = defs[1];
            RHAH_DefOf.RHAH_Faction_Neutral = defs[2];
            RHAH_DefOf.RHAH_Faction_LeaningFriendly = defs[3];
            RHAH_DefOf.RHAH_Faction_Friendly = defs[4];
        }
    }
}
