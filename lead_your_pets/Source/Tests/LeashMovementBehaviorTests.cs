using System;
using System.Collections.Generic;
using System.Reflection;
#if RIMWORLD_1_7
using UninitializedObject = System.Runtime.CompilerServices.RuntimeHelpers;
#else
using UninitializedObject = System.Runtime.Serialization.FormatterServices;
#endif
using RimWorld;
using Verse;
using Verse.AI;
using Xunit;

namespace LeadYourPet.Tests
{
    [Collection("Game state")]
    public class LeashMovementBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetGameComponent component;
        private readonly Pawn pet;
        private readonly LeashLink link;

        public LeashMovementBehaviorTests()
        {
            Game game = Raw<Game>();
            Map map = Raw<Map>();
            Set(game, "maps", new List<Map> { map });
            component = new LeadYourPetGameComponent(game);
            Set(game, "components", new List<GameComponent> { component });
            Current.Game = game;
            pet = Raw<Pawn>();
            pet.def = new ThingDef { race = new RaceProperties { intelligence = Intelligence.Humanlike, alwaysAwake = true } };
            pet.health = Raw<Pawn_HealthTracker>();
            pet.health.capacities = Raw<PawnCapacitiesHandler>();
            Set(pet.health.capacities, "pawn", pet);
            pet.jobs = Raw<Pawn_JobTracker>();
            Set(pet.jobs, "pawn", pet);
            Set(pet, "mapIndexOrState", (sbyte)0);
            link = new LeashLink { Pet = pet, Master = Raw<Pawn>() };
        }

        [Theory]
        [InlineData(LeashAnchorMode.None)]
        [InlineData(LeashAnchorMode.Cell)]
        public void SleepingPetKeepsItsTaskAndRejectsPhysicalInteractions(LeashAnchorMode anchor)
        {
            link.AnchorMode = anchor;
            Set(pet.health, "healthState", PawnHealthState.Mobile);
            Job sleep = new Job { def = new JobDef { defName = "Sleep" } };
            Set(pet.jobs, "curJob", sleep);
            JobDriver_FollowLeashMaster driver = Raw<JobDriver_FollowLeashMaster>();
            driver.asleep = true;
            Set(pet.jobs, "curDriver", driver);
            Assert.True(LeadYourPetUtility.HasLeashCareJob(link));
            InvokeFollow();
            Assert.Same(sleep, pet.CurJob);
            Assert.False(LeadYourPetUtility.CanPerformInteraction(pet, LeadYourPetInteractionKind.Whirl, out _));
            Assert.True(LeadYourPetUtility.CanPerformInteraction(pet, LeadYourPetInteractionKind.Fed, out _));
        }

        [Fact]
        public void DownedPetDoesNotReceiveAPathOrFollowJob()
        {
            Set(pet.health, "healthState", PawnHealthState.Down);
            Job rest = new Job { def = new JobDef { defName = "DownedRest" } };
            Set(pet.jobs, "curJob", rest);
            Assert.False(LeadYourPetUtility.CanMouseEggMove(pet));
            InvokeFollow();
            Assert.Same(rest, pet.CurJob);
            Assert.False(LeadYourPetUtility.CanPerformInteraction(pet, LeadYourPetInteractionKind.Kick, out _));
        }

        private void InvokeFollow() => typeof(LeadYourPetGameComponent).GetMethod("ForceFollow", Fields)
            .Invoke(component, new object[] { link, 0, true });

        public void Dispose() => Current.Game = previousGame;
        private static T Raw<T>() => (T)UninitializedObject.GetUninitializedObject(typeof(T));
        private static void Set(object instance, string name, object value)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields);
                if (field == null) continue;
                field.SetValue(instance, value);
                return;
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }
    }
}
