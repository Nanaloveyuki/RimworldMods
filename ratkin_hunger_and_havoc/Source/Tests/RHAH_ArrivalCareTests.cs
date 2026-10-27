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
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_ArrivalCareTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.arrival.care.tests");
        readonly JobDef previousCarry = RHAH_DefOf.RHAH_CarryToSafety;
        readonly IntVec3 destination = new IntVec3(50, 0, 50);
        readonly Lord lord;
        static int nextId = 2200;

        public RHAH_ArrivalCareTests()
        {
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            RHAH_DefOf.RHAH_CarryToSafety = new JobDef { defName = "RHAH_CarryToSafety" };
            harmony.Patch(typeof(ReachabilityUtility).GetMethod("CanReach", new[] { typeof(Verse.Pawn), typeof(LocalTargetInfo), typeof(PathEndMode), typeof(Danger), typeof(bool), typeof(bool), typeof(TraverseMode) }),
                prefix: new HarmonyMethod(typeof(RHAH_ArrivalCareTests), nameof(Allow)));
            harmony.Patch(typeof(ReservationUtility).GetMethod("CanReserve"),
                prefix: new HarmonyMethod(typeof(RHAH_ArrivalCareTests), nameof(Allow)));
            lord = new Lord();
            LordJob_RHAH_Visitor job = new LordJob_RHAH_Visitor(null, destination, RHAH_PawnRole.Refugee);
            Field(typeof(Lord), "curJob").SetValue(lord, job);
            job.lord = lord;
            Field(typeof(LordJob_RHAH_Visitor), "waitSpotResolved").SetValue(job, true);
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            RHAH_DefOf.RHAH_CarryToSafety = previousCarry;
            fixture.Dispose();
        }

        [Fact]
        public void AdultCollectsImmobileBabyAtEntryWithoutRequiringExitPermission()
        {
            Verse.Pawn adult = Visitor(DevelopmentalStage.Adult, false);
            Verse.Pawn baby = Visitor(DevelopmentalStage.Baby, true);
            Job job = JobGiver_RHAH_CarryToSafety.TryCreate(adult);
            Assert.NotNull(job);
            Assert.Same(baby, job.targetA.Thing);
            Assert.Equal(destination, job.targetB.Cell);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, RHAH_Api.Get(baby).Lifecycle);
            Assert.False(RHAH_Api.Allows(baby, RHAH_BehaviorGate.ExitMap));
        }

        [Fact]
        public void DeliveredBabyIsNotRepeatedlyPickedUp()
        {
            Verse.Pawn adult = Visitor(DevelopmentalStage.Adult, false);
            Verse.Pawn baby = Visitor(DevelopmentalStage.Baby, true);
            Field(typeof(Thing), "positionInt").SetValue(baby, destination);
            Assert.Null(JobGiver_RHAH_CarryToSafety.TryCreate(adult));
        }

        [Fact]
        public void HeldBabyResumesTransportWithTheSameCaregiver()
        {
            Verse.Pawn adult = Visitor(DevelopmentalStage.Adult, false);
            Verse.Pawn baby = Visitor(DevelopmentalStage.Baby, true);
            adult.holdingOwner = new ThingOwner<Thing>(adult.Map);
            baby.holdingOwner = adult.carryTracker.innerContainer;
            adult.carryTracker.innerContainer.InnerListForReading.Add(baby);
            Field(typeof(Thing), "mapIndexOrState").SetValue(baby, (sbyte)-1);

            Job job = JobGiver_RHAH_CarryToSafety.TryCreate(adult);

            Assert.NotNull(job);
            Assert.Same(baby, job.targetA.Thing);
            Assert.Equal(destination, job.targetB.Cell);
        }


        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DepartureOrDeniedCarryDoesNotBecomeInboundTransport(bool departing)
        {
            Verse.Pawn adult = Visitor(DevelopmentalStage.Adult, false);
            Visitor(DevelopmentalStage.Baby, true);
            if (departing) RHAH_Api.SetLifecycle(adult, RHAH_Lifecycle.Leaving);
            else RHAH_Api.SetGate(adult, RHAH_BehaviorGate.Carry, false);
            Assert.Null(JobGiver_RHAH_CarryToSafety.TryCreate(adult));
        }

        [Fact]
        public void ReleasedOrDepartingChildIsNotReclaimed()
        {
            Verse.Pawn adult = Visitor(DevelopmentalStage.Adult, false);
            Verse.Pawn baby = Visitor(DevelopmentalStage.Baby, true);
            RHAH_Api.SetLifecycle(baby, RHAH_Lifecycle.Leaving);
            Assert.Null(JobGiver_RHAH_CarryToSafety.TryCreate(adult));
            RHAH_Api.SetLifecycle(baby, RHAH_Lifecycle.Released);
            Assert.Null(JobGiver_RHAH_CarryToSafety.TryCreate(adult));
        }

        Verse.Pawn Visitor(DevelopmentalStage stage, bool downed)
        {
            Verse.Pawn pawn = (Verse.Pawn)typeof(RHAH_AttackChoiceTests).GetMethod("Visitor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture, new object[] { nextId++ });
            LifeStageAge age = (LifeStageAge)FormatterServices.GetUninitializedObject(typeof(LifeStageAge));
            age.def = new LifeStageDef { developmentalStage = stage, alwaysDowned = downed };
            pawn.def.race.lifeStageAges = new List<LifeStageAge> { age };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(pawn.health, downed ? PawnHealthState.Down : PawnHealthState.Mobile);
            Field(typeof(Thing), "positionInt").SetValue(pawn, new IntVec3(1, 0, 1));
            pawn.carryTracker = new Pawn_CarryTracker(pawn);
            pawn.lord = lord;
            lord.ownedPawns.Add(pawn);
            return pawn;
        }

        static bool Allow(ref bool __result)
        {
            __result = true;
            return false;
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
    }
}
