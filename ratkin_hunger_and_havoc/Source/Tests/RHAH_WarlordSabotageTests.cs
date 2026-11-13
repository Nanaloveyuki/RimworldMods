using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
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
    public sealed class RHAH_WarlordSabotageTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.warlord.sabotage.tests");
        readonly Dictionary<FieldInfo, object> previousDefs = new Dictionary<FieldInfo, object>();
        static bool flammable;
        static bool fireSucceeds;
        static int fires;
        static int hits;
        static Thing instigator;
        static DamageInfo damage;

        public RHAH_WarlordSabotageTests()
        {
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Bind(typeof(RHAH_DefOf), "RHAH_WarlordVandalize", new JobDef { defName = "RHAH_WarlordVandalize" });
            Bind(typeof(RHAH_DefOf), "RHAH_WarlordIgnite", new JobDef { defName = "RHAH_WarlordIgnite" });
            Bind(typeof(DamageDefOf), "Blunt", new DamageDef { defName = "Blunt" });
            flammable = true;
            fireSucceeds = true;
            fires = hits = 0;
            instigator = null;
            harmony.Patch(AccessTools.Method(typeof(FireUtility), nameof(FireUtility.IsBurning), new[] { typeof(Thing) }),
                prefix: new HarmonyMethod(typeof(RHAH_WarlordSabotageTests), nameof(NotBurning)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.FlammableNow)),
                prefix: new HarmonyMethod(typeof(RHAH_WarlordSabotageTests), nameof(IsFlammable)));
            harmony.Patch(AccessTools.Method(typeof(FireUtility), nameof(FireUtility.TryStartFireIn)),
                prefix: new HarmonyMethod(typeof(RHAH_WarlordSabotageTests), nameof(StartFire)));
            harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)),
                prefix: new HarmonyMethod(typeof(RHAH_WarlordSabotageTests), nameof(TakeDamage)));
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            foreach (KeyValuePair<FieldInfo, object> previous in previousDefs)
            {
                previous.Key.SetValue(null, previous.Value);
            }
            fixture.Dispose();
        }

        [Fact]
        public void MobileSquadBabyCanSabotageWithoutCombatVerbs()
        {
            Assert.True(JobGiver_RHAH_WarlordSabotage.CanAct(Baby()));
        }

        [Theory]
        [InlineData("prisoner")]
        [InlineData("slave")]
        [InlineData("joined")]
        [InlineData("released")]
        [InlineData("leaving")]
        [InlineData("downed")]
        [InlineData("child")]
        [InlineData("otherEvent")]
        [InlineData("fightDenied")]
        public void ChangedIdentityOrMobilityStopsSabotageBeforeDamage(string change)
        {
            Verse.Pawn pawn = Baby();
            Building target = PlayerBuilding();
            Assert.True(JobGiver_RHAH_WarlordSabotage.CanAct(pawn));
            if (change == "prisoner" || change == "slave")
            {
                pawn.guest = new Pawn_GuestTracker(pawn);
                Field(typeof(Pawn_GuestTracker), "guestStatusInt").SetValue(pawn.guest,
                    change == "prisoner" ? GuestStatus.Prisoner : GuestStatus.Slave);
            }
            else if (change == "joined") pawn.SetFactionDirect(Faction.OfPlayer);
            else if (change == "released") CompRHAH_Pawn.TryGet(pawn).State.lifecycle = RHAH_Lifecycle.Released;
            else if (change == "leaving") CompRHAH_Pawn.TryGet(pawn).State.lifecycle = RHAH_Lifecycle.Leaving;
            else if (change == "downed") Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Down);
            else if (change == "child") pawn.ageTracker.CurLifeStage.developmentalStage = DevelopmentalStage.Child;
            else if (change == "otherEvent") CompRHAH_Pawn.TryGet(pawn).State.sourceIncidentDisplayId = "I-056";
            else RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.Fight, false);

            Assert.False(JobDriver_RHAH_WarlordSabotage.TryApply(pawn, target, true));
            Assert.Equal(0, fires);
            Assert.Equal(0, hits);
        }

        [Theory]
        [InlineData(true, true, 1, 0)]
        [InlineData(true, false, 1, 1)]
        [InlineData(false, true, 0, 1)]
        public void AdjacentSabotageIgnitesOrDamagesTheBuilding(bool ignite, bool fireWorks, int expectedFires, int expectedHits)
        {
            Verse.Pawn pawn = Baby();
            fireSucceeds = fireWorks;

            Assert.True(JobDriver_RHAH_WarlordSabotage.TryApply(pawn, PlayerBuilding(), ignite));

            Assert.Equal(expectedFires, fires);
            Assert.Equal(expectedHits, hits);
            Assert.Same(pawn, instigator);
            if (hits > 0)
            {
                Assert.Same(DamageDefOf.Blunt, damage.Def);
                Assert.Equal(6f, damage.Amount);
            }
        }

        [Theory]
        [InlineData("neutral")]
        [InlineData("far")]
        [InlineData("unspawned")]
        public void InvalidOrDistantBuildingCannotBeDamagedOrIgnited(string change)
        {
            Verse.Pawn pawn = Baby();
            Building target = PlayerBuilding();
            if (change == "neutral") target.SetFactionDirect(null);
            else if (change == "far") Field(typeof(Thing), "positionInt").SetValue(target, new IntVec3(20, 0, 20));
            else Field(typeof(Thing), "mapIndexOrState").SetValue(target, (sbyte)-1);

            Assert.False(JobDriver_RHAH_WarlordSabotage.TryApply(pawn, target, true));
            Assert.Equal(0, hits);
            Assert.Equal(0, fires);
        }

        [Theory]
        [InlineData(true, 0f, true)]
        [InlineData(true, 0.35f, false)]
        [InlineData(false, 0f, false)]
        public void OnlyFlammableBuildingsCanReceiveIgnitionJobs(bool canBurn, float roll, bool ignition)
        {
            Building target = PlayerBuilding();
            flammable = canBurn;

            Job job = JobGiver_RHAH_WarlordSabotage.CreateJob(target, roll);

            Assert.Same(ignition ? RHAH_DefOf.RHAH_WarlordIgnite : RHAH_DefOf.RHAH_WarlordVandalize, job.def);
            Assert.Same(target, job.targetA.Thing);
        }

        Verse.Pawn Baby()
        {
            Verse.Pawn pawn = (Verse.Pawn)typeof(RHAH_AttackChoiceTests)
                .GetMethod("Visitor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture, new object[] { 2201 });
            LifeStageAge stage = (LifeStageAge)FormatterServices.GetUninitializedObject(typeof(LifeStageAge));
            stage.def = new LifeStageDef { developmentalStage = DevelopmentalStage.Baby };
            pawn.def.race.lifeStageAges = new List<LifeStageAge> { stage };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            Field(typeof(Thing), "positionInt").SetValue(pawn, IntVec3.Zero);
            CompRHAH_Pawn.TryGet(pawn).ApplySeed(new RHAH_PawnSeed("I-055", 7, 0,
                RHAH_PawnRole.RatkinYoung, RHAH_Lifecycle.SeekingFood, attitudeAtArrival: RHAH_Attitude.Hostile));
            return pawn;
        }

        static Building PlayerBuilding()
        {
            Building target = new Building
            {
                def = new ThingDef { category = ThingCategory.Building, useHitPoints = true, size = IntVec2.One },
                HitPoints = 50
            };
            target.SetFactionDirect(Faction.OfPlayer);
            Field(typeof(Thing), "mapIndexOrState").SetValue(target, (sbyte)0);
            Field(typeof(Thing), "positionInt").SetValue(target, new IntVec3(1, 0, 0));
            return target;
        }

        void Bind(Type type, string name, object value)
        {
            FieldInfo field = Field(type, name);
            previousDefs.Add(field, field.GetValue(null));
            field.SetValue(null, value);
        }

        static bool NotBurning(ref bool __result)
        {
            __result = false;
            return false;
        }

        static bool IsFlammable(ref bool __result)
        {
            __result = flammable;
            return false;
        }

        static bool StartFire(Thing instigator, ref bool __result)
        {
            fires++;
            RHAH_WarlordSabotageTests.instigator = instigator;
            __result = fireSucceeds;
            return false;
        }

        static bool TakeDamage(DamageInfo dinfo)
        {
            hits++;
            damage = dinfo;
            instigator = dinfo.Instigator;
            return false;
        }

        static FieldInfo Field(Type type, string name)
        {
            return AccessTools.Field(type, name) ?? throw new InvalidOperationException(type.FullName + "." + name);
        }
    }
}
