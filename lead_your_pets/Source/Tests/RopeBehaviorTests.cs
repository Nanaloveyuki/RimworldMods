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
    public class RopeBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetSettings previousSettings = LeadYourPetMod.Settings;
        private readonly LeadYourPetGameComponent component;
        private readonly Pawn pet;
        private readonly Pawn master;
        private readonly LeashLink link;

        public RopeBehaviorTests()
        {
            LeadYourPetMod.Settings = null;
            Game game = Raw<Game>();
            Map map = Raw<Map>();
            map.info = Raw<MapInfo>();
            map.info.Size = new IntVec3(300, 1, 300);
            Set(game, "maps", new List<Map> { map });
            component = new LeadYourPetGameComponent(game);
            Set(game, "components", new List<GameComponent> { component });
            Current.Game = game;
            pet = NewPawn(1, new IntVec3(20, 0, 20));
            master = NewPawn(2, new IntVec3(24, 0, 20));
            link = new LeashLink { Master = master, Pet = pet };
            ((List<LeashLink>)Get(component, "links")).Add(link);
            typeof(LeadYourPetGameComponent).GetMethod("RegisterLink", Fields).Invoke(component, new object[] { link });
        }

        [Theory]
        [InlineData(15, true)]
        [InlineData(16, false)]
        [InlineData(254, false)]
        public void RopeStopsAtHardLeashBoundary(int distance, bool visible)
        {
            Set(master, "positionInt", pet.Position + new IntVec3(distance, 0, 0));
            Assert.Equal(visible, LeadYourPetUtility.TryGetCustomRopeTarget(pet, out LocalTargetInfo target));
            if (visible) Assert.Same(master, target.Thing);
            else Assert.False(target.IsValid);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DepartureMovementKeepsTheRopeOnEitherEndpoint(bool masterExits)
        {
            Job exit = Raw<Job>();
            exit.exitMapOnArrival = true;
            Set((masterExits ? master : pet).jobs, "curJob", exit);
            Assert.True(LeadYourPetUtility.TryGetCustomRopeTarget(pet, out LocalTargetInfo target));
            Assert.Same(master, target.Thing);
            Assert.Same(link, component.GetLinkForPet(pet));
        }

        [Fact]
        public void CellAnchorKeepsItsOwnTargetWhenTheOldMasterMovesAway()
        {
            link.AnchorMode = LeashAnchorMode.Cell;
            link.AnchorCell = new IntVec3(21, 0, 20);
            Set(master, "positionInt", new IntVec3(200, 0, 200));
            Assert.Null(LeadYourPetUtility.GetCurrentFollowThing(link));
            Assert.True(LeadYourPetUtility.TryGetCustomRopeTarget(pet, out LocalTargetInfo target));
            Assert.False(target.HasThing);
            Assert.Equal(link.AnchorCell, target.Cell);
        }

        [Fact]
        public void HostileAttackSettingRestoresEnemyHostilityAndCanBeTurnedBackOff()
        {
            Faction faction = Raw<Faction>();
            Set(master, "factionInt", faction);
            pet.kindDef = new PawnKindDef { hostileToAll = true };
            master.kindDef = new PawnKindDef();
            bool hostile = true;
            Patch_GenHostility_ThingThing.Postfix(pet, master, ref hostile);
            Assert.False(hostile);

            LeadYourPetMod.Settings = new LeadYourPetSettings { leashedHostilesCanAttack = true };
            hostile = true;
            Patch_GenHostility_ThingThing.Postfix(pet, master, ref hostile);
            Assert.True(hostile);
            Assert.True(LeadYourPetUtility.AllowsLeashedHostileAttack(link));
            hostile = true;
            Patch_GenHostility_ThingFaction.Postfix(pet, faction, ref hostile);
            Assert.True(hostile);

            LeadYourPetMod.Settings.leashedHostilesCanAttack = false;
            Assert.False(LeadYourPetUtility.AllowsLeashedHostileAttack(link));
            hostile = true;
            Patch_GenHostility_ThingFaction.Postfix(pet, faction, ref hostile);
            Assert.False(hostile);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GrabbedEndpointHidesRopeUntilRelease(bool grabMaster)
        {
            object controller = new object();
            GodHandsCompat.RecordGrabbed(grabMaster ? master : pet, controller);
            Assert.False(LeadYourPetUtility.TryGetCustomRopeTarget(pet, out _));
            GodHandsCompat.RecordReleased(controller);
            Assert.True(LeadYourPetUtility.TryGetCustomRopeTarget(pet, out _));
        }

        [Fact]
        public void GrabbedAnchorDoesNotFallBackToAnOtherwiseValidCell()
        {
            link.AnchorMode = LeashAnchorMode.Thing;
            link.AnchorThing = master;
            GodHandsCompat.RecordGrabbed(master, new object());
            Assert.False(LeadYourPetUtility.TryGetCustomRopeTarget(pet, out _));
        }

        [Fact]
        public void GrabCancelsAnimationWithoutApplyingItsOldTeleport()
        {
            pet.health = Raw<Pawn_HealthTracker>();
            pet.health.hediffSet = new HediffSet(pet);
            master.health = Raw<Pawn_HealthTracker>();
            Set(pet.health, "healthState", PawnHealthState.Mobile);
            Set(master.health, "healthState", PawnHealthState.Mobile);
            YoungRatkinInteractionAnimation animation = new YoungRatkinInteractionAnimation
            {
                Pet = pet, Master = master, FinalizeTeleport = true,
                StartMap = pet.Map, StartLink = link, StartPetCell = pet.Position,
                StartAnchorMode = link.AnchorMode, StartAnchorThing = link.AnchorThing, StartAnchorCell = link.AnchorCell,
                TargetCell = new IntVec3(200, 0, 200), PetUsesCustomRender = true
            };
            typeof(LeadYourPetGameComponent).GetMethod("RegisterInteractionAnimation", Fields).Invoke(component, new object[] { animation });
            IntVec3 grabbedPosition = pet.Position;
            GodHandsCompat.RecordGrabbed(pet, new object());
            Assert.False(component.TryGetInteractionAnimationRenderData(pet, out _, out _, out _, out _));
            typeof(LeadYourPetGameComponent).GetMethod("ProcessLink", Fields).Invoke(component, new object[] { link, 0 });
            Assert.Null(component.GetInteractionAnimationForPet(pet));
            Assert.Equal(grabbedPosition, pet.Position);
            Assert.True(link.ForceImmediateUpdate);
        }

        public void Dispose()
        {
            GodHandsCompat.Reset();
            Current.Game = previousGame;
            LeadYourPetMod.Settings = previousSettings;
        }

        private static T Raw<T>() => (T)UninitializedObject.GetUninitializedObject(typeof(T));
        private static Pawn NewPawn(int id, IntVec3 position)
        {
            Pawn pawn = Raw<Pawn>();
            pawn.def = Raw<ThingDef>();
            pawn.def.defName = "Ratkin";
            pawn.thingIDNumber = id;
            Set(pawn, "mapIndexOrState", (sbyte)0);
            Set(pawn, "positionInt", position);
            pawn.jobs = Raw<Pawn_JobTracker>();
            Set(pawn.jobs, "pawn", pawn);
            return pawn;
        }
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
    }
}
