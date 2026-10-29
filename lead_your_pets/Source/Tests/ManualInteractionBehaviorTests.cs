#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using Verse.AI;
using Xunit;

namespace LeadYourPet.Tests
{
    [Collection("Game state")]
    public class ManualInteractionBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetSettings previousSettings = LeadYourPetMod.Settings;
        private readonly JobDef previousIngest = JobDefOf.Ingest;
        private readonly JobDef previousLayDown = JobDefOf.LayDown;
        private readonly JobDef previousResting = JobDefOf.LayDownResting;
        private readonly JobDef previousAwake = JobDefOf.LayDownAwake;
        private readonly LeadYourPetGameComponent component;
        private readonly Pawn master;
        private readonly Pawn pet;

        public ManualInteractionBehaviorTests()
        {
            LeadYourPetMod.Settings = new LeadYourPetSettings { showInteractionText = false };
            JobDefOf.Ingest = new JobDef { defName = "Ingest" };
            JobDefOf.LayDown = new JobDef { defName = "LayDown" };
            JobDefOf.LayDownResting = new JobDef { defName = "LayDownResting" };
            JobDefOf.LayDownAwake = new JobDef { defName = "LayDownAwake" };
            Faction faction = Raw<Faction>();
            faction.def = new FactionDef { isPlayer = true };
            Game game = Raw<Game>();
            game.InitData = new GameInitData { playerFaction = faction };
            game.tickManager = Raw<TickManager>();
            Map map = Raw<Map>();
            Set(game, "maps", new List<Map> { map });
            component = new LeadYourPetGameComponent(game);
            Set(game, "components", new List<GameComponent> { component });
            Current.Game = game;
            master = NewPawn(1, faction);
            pet = NewPawn(2, faction);
            pet.needs = new Pawn_NeedsTracker(pet);
            pet.needs.play = Raw<Need_Play>();
            pet.needs.play.CurLevel = 0.25f;
            LeashLink link = new LeashLink { Master = master, Pet = pet, Kind = LeashLinkKind.YoungRatkinPet };
            ((List<LeashLink>)Get(component, "links")).Add(link);
            typeof(LeadYourPetGameComponent).GetMethod("RegisterLink", Fields).Invoke(component, new object[] { link });
        }

        [Theory]
        [InlineData(DevelopmentalStage.Baby, "anesthetized")]
        [InlineData(DevelopmentalStage.Baby, "sleeping")]
        [InlineData(DevelopmentalStage.Baby, "resting")]
        [InlineData(DevelopmentalStage.Baby, "resting-awake")]
        [InlineData(DevelopmentalStage.Baby, "eating")]
        [InlineData(DevelopmentalStage.Child, "anesthetized")]
        [InlineData(DevelopmentalStage.Child, "sleeping")]
        [InlineData(DevelopmentalStage.Child, "resting")]
        [InlineData(DevelopmentalStage.Child, "resting-awake")]
        [InlineData(DevelopmentalStage.Child, "eating")]
        public void ManualInteractionWorksDuringCareWithoutInterruptingTheTask(DevelopmentalStage stage, string state)
        {
            SetState(stage, state);
            Job care = pet.CurJob;
            Assert.False(LeadYourPetUtility.CanTriggerInteraction(master, pet, LeadYourPetInteractionKind.Pat, out _));
            Assert.True(LeadYourPetUtility.CanTriggerInteraction(master, pet, LeadYourPetInteractionKind.Pat, out _, manual: true));

            component.TriggerManualInteraction(master, pet, LeadYourPetInteractionKind.Pat);

            Assert.Equal(0.35f, pet.needs.play.CurLevel, 4);
            Assert.Same(care, pet.CurJob);
            Assert.Null(component.GetInteractionAnimationForPet(pet));
        }

        [Fact]
        public void ManualInteractionStillRejectsDeadTargetsAndMissingParts()
        {
            Assert.False(LeadYourPetUtility.CanTriggerInteraction(master, pet, LeadYourPetInteractionKind.PullTail, out string reason, manual: true));
            Assert.Equal("LeadYourPet_Reason_MissingTail", reason);
            Set(pet.health, "healthState", PawnHealthState.Dead);
            component.TriggerManualInteraction(master, pet, LeadYourPetInteractionKind.Pat);
            Assert.Equal(0.25f, pet.needs.play.CurLevel);
        }

        private void SetState(DevelopmentalStage stage, string state)
        {
            pet.def.race.lifeStageAges[0].def.developmentalStage = stage;
            Set(pet.ageTracker, "ageBiologicalTicksInt", (stage == DevelopmentalStage.Baby ? 1L : 8L) * 3600000L);
            pet.jobs.curJob = new Job
            {
                def = state == "eating" ? JobDefOf.Ingest
                    : state == "resting" ? JobDefOf.LayDownResting
                    : state == "resting-awake" ? JobDefOf.LayDownAwake : JobDefOf.LayDown
            };
            JobDriver_FollowLeashMaster driver = Raw<JobDriver_FollowLeashMaster>();
            driver.asleep = state == "sleeping";
            pet.jobs.curDriver = driver;
            Set(pet.health, "healthState", state == "anesthetized" ? PawnHealthState.Down : PawnHealthState.Mobile);
        }

        private static Pawn NewPawn(int id, Faction faction)
        {
            Pawn pawn = Raw<Pawn>();
            pawn.thingIDNumber = id;
            pawn.def = new ThingDef
            {
                defName = "Human", race = new RaceProperties
                {
                    intelligence = Intelligence.Humanlike,
                    alwaysAwake = true,
                    lifeStageAges = new List<LifeStageAge> { new LifeStageAge { def = new LifeStageDef { developmentalStage = DevelopmentalStage.Child } } }
                }
            };
            pawn.health = Raw<Pawn_HealthTracker>();
            Set(pawn.health, "healthState", PawnHealthState.Mobile);
            pawn.health.capacities = Raw<PawnCapacitiesHandler>();
            Set(pawn.health.capacities, "pawn", pawn);
            pawn.ageTracker = Raw<Pawn_AgeTracker>();
            Set(pawn.ageTracker, "pawn", pawn);
            Set(pawn.ageTracker, "cachedLifeStageIndex", 0);
            Set(pawn, "mapIndexOrState", (sbyte)0);
            Set(pawn, "factionInt", faction);
            pawn.jobs = Raw<Pawn_JobTracker>();
            Set(pawn.jobs, "pawn", pawn);
            pawn.jobs.curJob = new Job { def = new JobDef { defName = "Work" } };
            return pawn;
        }

        private static T Raw<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(object instance, string name)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields);
                if (field != null) return field;
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }
        private static object Get(object instance, string name) => Field(instance, name).GetValue(instance);
        private static void Set(object instance, string name, object value) => Field(instance, name).SetValue(instance, value);

        public void Dispose()
        {
            Current.Game = previousGame;
            LeadYourPetMod.Settings = previousSettings;
            JobDefOf.Ingest = previousIngest;
            JobDefOf.LayDown = previousLayDown;
            JobDefOf.LayDownResting = previousResting;
            JobDefOf.LayDownAwake = previousAwake;
            GodHandsCompat.Reset();
        }
    }
}
#endif
