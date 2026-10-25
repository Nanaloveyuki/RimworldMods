using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_DepartureExitTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.departure.exit.tests");
        readonly Dictionary<FieldInfo, object> previousDefs = new Dictionary<FieldInfo, object>();
        static bool blockerJobAvailable;
        static int nextId = 1900;

        public RHAH_DepartureExitTests()
        {
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Bind(typeof(JobDefOf), "Goto", new JobDef { defName = "Goto" });
            Bind(typeof(JobDefOf), "Wait", new JobDef { defName = "Wait" });
            Bind(typeof(RHAH_DefOf), "RHAH_DropChild", new JobDef { defName = "RHAH_DropChild" });
            Bind(typeof(RHAH_DefOf), "RHAH_CarryYoung", new JobDef { defName = "RHAH_CarryYoung" });
            Bind(typeof(RHAH_DefOf), "RHAH_VisitorLeave", new DutyDef { defName = "RHAH_VisitorLeave" });
            RHAH_Mod.Settings.familyDropEnabled = false;
            blockerJobAvailable = true;
            harmony.Patch(typeof(RCellFinder).GetMethod("TryFindBestExitSpot"),
                prefix: new HarmonyMethod(typeof(RHAH_DepartureExitTests), nameof(NoNormalExit)));
            harmony.Patch(typeof(JobGiver_ExitMap).GetMethod("TryGiveJob", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: new HarmonyMethod(typeof(RHAH_DepartureExitTests), nameof(BlockerJob)));
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

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void NoExitDoesNotIssueSoloBlockerJobWhileHoldingChild(bool childMayExit, bool carrierMayCarry)
        {
            Verse.Pawn carrier = Visitor();
            Verse.Pawn child = Visitor();
            RHAH_Api.SetGate(child, RHAH_BehaviorGate.ExitMap, childMayExit);
            RHAH_Api.SetGate(carrier, RHAH_BehaviorGate.Carry, carrierMayCarry);
            carrier.carryTracker.innerContainer.InnerListForReading.Add(child);

            ThinkResult result = new JobGiver_RHAH_Leave().TryIssueJobPackage(carrier, default);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FailedExitCooldownDoesNotOverrideExitDenialOrIncapacitation(bool downed)
        {
            Verse.Pawn pawn = Visitor();
            blockerJobAvailable = false;
            ThinkResult first = new JobGiver_RHAH_Leave().TryIssueJobPackage(pawn, default);
            Assert.True(first.IsValid);
            Assert.Same(JobDefOf.Wait, first.Job.def);
            if (downed)
            {
                Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, PawnHealthState.Down);
            }
            else
            {
                RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, false);
            }

            ThinkResult result = new JobGiver_RHAH_Leave().TryIssueJobPackage(pawn, default);

            Assert.False(result.IsValid);
        }

        Verse.Pawn Visitor()
        {
            Verse.Pawn pawn = (Verse.Pawn)typeof(RHAH_AttackChoiceTests)
                .GetMethod("Visitor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture, new object[] { nextId++ });
            LifeStageAge adult = (LifeStageAge)FormatterServices.GetUninitializedObject(typeof(LifeStageAge));
            adult.def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult };
            pawn.def.race.lifeStageAges = new List<LifeStageAge> { adult };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            pawn.carryTracker = new Pawn_CarryTracker(pawn);
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, true);
            return pawn;
        }

        void Bind(Type type, string name, object value)
        {
            FieldInfo field = Field(type, name);
            previousDefs.Add(field, field.GetValue(null));
            field.SetValue(null, value);
        }

        static bool NoNormalExit(ref IntVec3 spot, ref bool __result)
        {
            spot = IntVec3.Invalid;
            __result = false;
            return false;
        }

        static bool BlockerJob(ref Job __result)
        {
            __result = blockerJobAvailable ? new Job { def = new JobDef { defName = "Mine" } } : null;
            return false;
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
