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
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_DepartureFoodTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly Harmony harmony = new Harmony("rhah.departure.food.tests");
        readonly Dictionary<FieldInfo, object> previousJobs = new Dictionary<FieldInfo, object>();
        readonly Map map;
        static int nextId = 1800;

        public RHAH_DepartureFoodTests()
        {
            map = (Map)Field(typeof(RHAH_AttackChoiceTests), "map").GetValue(fixture);
            map.pawnDestinationReservationManager = Uninitialized<PawnDestinationReservationManager>();
            map.reservationManager = new ReservationManager(map);
            map.enrouteManager = new EnrouteManager(map);
            map.physicalInteractionReservationManager = Uninitialized<PhysicalInteractionReservationManager>();
            map.attackTargetReservationManager = new AttackTargetReservationManager(map);
            // Queue cleanup iterates the managers even when there are no reservations.
            Field(typeof(PawnDestinationReservationManager), "reservedDestinations").SetValue(
                map.pawnDestinationReservationManager, new Dictionary<Faction, PawnDestinationReservationManager.PawnDestinationSet>());
            Field(typeof(PhysicalInteractionReservationManager), "reservations").SetValue(
                map.physicalInteractionReservationManager, new List<PhysicalInteractionReservationManager.PhysicalInteractionReservation>());
            foreach (string name in new[] { "Ingest", "Harvest", "TakeFromOtherInventory", "Goto", "Wait", "Steal" })
                BindJob(typeof(JobDefOf), name);
            foreach (string name in new[] { "RHAH_Gnaw", "RHAH_CarryYoung", "RHAH_DropChild", "RHAH_MotherFeed" })
                BindJob(typeof(RHAH_DefOf), name);
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            foreach (KeyValuePair<FieldInfo, object> previous in previousJobs) previous.Key.SetValue(null, previous.Value);
            fixture.Dispose();
        }

        [Fact]
        public void LeaveOrderDiscardsPreviouslyQueuedFoodBeforeItCanResume()
        {
            Verse.Pawn departing = Visitor();
            Verse.Pawn staying = Visitor();
            Job queuedMeal = new Job { def = JobDefOf.Ingest };
            departing.jobs.jobQueue.EnqueueLast(queuedMeal);
            departing.jobs.jobQueue.EnqueueFirst(new Job { def = JobDefOf.Harvest });
            LocalTargetInfo reserved = new IntVec3(4, 0, 4);
            map.physicalInteractionReservationManager.Reserve(departing, queuedMeal, reserved);
            Job othersMeal = new Job { def = JobDefOf.Ingest };
            staying.jobs.jobQueue.EnqueueLast(othersMeal);
            departing.needs = new Pawn_NeedsTracker();
            departing.needs.food = Uninitialized<Need_Food>();
            Field(typeof(Need), "curLevelInt").SetValue(departing.needs.food, 0f);

            RHAH_BatchAttitude.OrderVisitorLeave(new List<Verse.Pawn> { departing });

            Assert.Equal(RHAH_Lifecycle.Leaving, RHAH_Api.Get(departing).Lifecycle);
            Assert.Equal(0, departing.jobs.jobQueue.Count);
            Assert.False(map.physicalInteractionReservationManager.IsReservedBy(departing, reserved));
            Assert.False(new ThinkNode_QueuedJob().TryIssueJobPackage(departing, default).IsValid);
            Assert.Same(othersMeal, staying.jobs.jobQueue.Peek().job);
            Assert.Equal(0f, departing.needs.food.CurLevel);
        }

        [Theory]
        [InlineData("ingest")]
        [InlineData("gnaw")]
        [InlineData("harvest")]
        [InlineData("takeInventory")]
        [InlineData("vanillaFoodGiver")]
        [InlineData("reliefFoodGiver")]
        public void FoodCannotReplaceAnActiveExitJobAtStartJob(string kind)
        {
            Verse.Pawn pawn = Visitor();
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, true);
            Job exit = new Job { def = new JobDef { defName = "Departure" }, exitMapOnArrival = true };
            pawn.jobs.curJob = exit;
            harmony.CreateClassProcessor(typeof(RHAH_StayWorkPatch)).Patch();
            Job food = FoodJob(kind, out ThinkNode giver);

            pawn.jobs.StartJob(food, JobCondition.InterruptOptional, giver, resumeCurJobAfterwards: true);

            Assert.Same(exit, pawn.jobs.curJob);
            Assert.Equal(0, pawn.jobs.jobQueue.Count);
        }

        [Fact]
        public void DepartingVisitorGetsNoVanillaOrCustomFoodPackage()
        {
            Verse.Pawn pawn = Visitor();
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, true);
            harmony.CreateClassProcessor(typeof(RHAH_GnawFoodPatch)).Patch();
            harmony.CreateClassProcessor(typeof(RHAH_ReliefFoodPatch)).Patch();

            Assert.False(new JobGiver_GetFood().TryIssueJobPackage(pawn, default).IsValid);
            Assert.False(new JobGiver_RHAH_Feed().TryIssueJobPackage(pawn, default).IsValid);
            Assert.Null(JobGiver_RHAH_Gnaw.TryCreate(pawn, true));
        }

        [Fact]
        public void FoodGateLeavesNormalVisitorsColonistsAndOtherLordsAlone()
        {
            Verse.Pawn visitor = Visitor();
            Job meal = FoodJob("ingest", out ThinkNode giver);
            Assert.True(StartAllowed(visitor, meal, giver));
            RHAH_Api.SetLifecycle(visitor, RHAH_Lifecycle.Leaving);
            RHAH_Api.SetGate(visitor, RHAH_BehaviorGate.ExitMap, true);
            visitor.SetFactionDirect(Faction.OfPlayer);
            Assert.True(StartAllowed(visitor, meal, giver));

            Verse.Pawn otherLord = Visitor();
            RHAH_Api.SetLifecycle(otherLord, RHAH_Lifecycle.Leaving);
            RHAH_Api.SetGate(otherLord, RHAH_BehaviorGate.ExitMap, true);
            otherLord.lord = Uninitialized<Lord>();
            Field(typeof(Lord), "curJob").SetValue(otherLord.lord, new LordJob_AssaultColony());
            Assert.True(StartAllowed(otherLord, meal, giver));
        }

        [Fact]
        public void ChildCareAndSpecialDepartureJobsRemainAllowed()
        {
            Verse.Pawn pawn = Visitor();
            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Leaving);
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, true);
            foreach (JobDef def in new[]
            {
                RHAH_DefOf.RHAH_CarryYoung, RHAH_DefOf.RHAH_DropChild, RHAH_DefOf.RHAH_MotherFeed,
                JobDefOf.Steal, JobDefOf.Wait, JobDefOf.Goto
            })
            {
                Assert.False(JobGiver_RHAH_Leave.RejectsFoodJob(pawn, new Job { def = def }, null));
            }
            RHAH_Api.SetGate(pawn, RHAH_BehaviorGate.ExitMap, false);
            Assert.True(StartAllowed(pawn, FoodJob("ingest", out ThinkNode giver), giver));
            Assert.False(RHAH_Api.Allows(pawn, RHAH_BehaviorGate.ExitMap));
            Assert.Null(JobGiver_RHAH_Leave.TryCreate(pawn));
        }

        Verse.Pawn Visitor()
        {
            Verse.Pawn pawn = (Verse.Pawn)Method(typeof(RHAH_AttackChoiceTests), "Visitor")
                .Invoke(fixture, new object[] { nextId++ });
            LifeStageAge adult = Uninitialized<LifeStageAge>();
            adult.def = new LifeStageDef { developmentalStage = DevelopmentalStage.Adult };
            pawn.def.race.lifeStageAges = new List<LifeStageAge> { adult };
            pawn.ageTracker = new Pawn_AgeTracker(pawn);
            Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
            pawn.jobs = new Pawn_JobTracker(pawn);
            return pawn;
        }

        static Job FoodJob(string kind, out ThinkNode giver)
        {
            giver = null;
            JobDef def = JobDefOf.Ingest;
            if (kind == "gnaw") def = RHAH_DefOf.RHAH_Gnaw;
            else if (kind == "harvest") def = JobDefOf.Harvest;
            else if (kind == "takeInventory") def = JobDefOf.TakeFromOtherInventory;
            else if (kind == "vanillaFoodGiver")
            {
                def = new JobDef { defName = "HopperFill" };
                giver = new JobGiver_GetFood();
            }
            else if (kind == "reliefFoodGiver")
            {
                def = new JobDef { defName = "ReliefFood" };
                giver = new JobGiver_RHAH_Feed();
            }
            ThingDef edible = Uninitialized<ThingDef>();
            edible.ingestible = new IngestibleProperties { foodType = FoodTypeFlags.Meal, preferability = FoodPreferability.MealSimple };
            Field(typeof(IngestibleProperties), "cachedNutrition").SetValue(edible.ingestible, 0.9f);
            return new Job { def = def, targetA = new Thing { def = edible } };
        }

        void BindJob(Type type, string name)
        {
            FieldInfo field = Field(type, name);
            previousJobs.Add(field, field.GetValue(null));
            field.SetValue(null, new JobDef { defName = name });
        }

        static bool StartAllowed(Verse.Pawn pawn, Job job, ThinkNode giver)
        {
            return (bool)Method(typeof(RHAH_StayWorkPatch), "Prefix").Invoke(null, new object[] { pawn, job, giver });
        }

        static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        static T Uninitialized<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
