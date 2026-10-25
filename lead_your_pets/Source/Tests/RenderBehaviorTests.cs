#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Xunit;

namespace LeadYourPet.Tests
{
    [Collection("Game state")]
    public class RenderBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetSettings previousSettings = LeadYourPetMod.Settings;
        private readonly LeadYourPetGameComponent component;
        private readonly Map map;
        private readonly VisualPawn pet;
        private readonly VisualPawn master;
        private readonly LeashLink link;
        private PathGrid grid;

        // Only the visual source is replaced: production animation, leash, grid and patch logic run unchanged.
        private sealed class VisualPawn : Pawn
        {
            public Vector3 VisualPosition;
            public override Vector3 DrawPos => VisualPosition;
        }

        public RenderBehaviorTests()
        {
            LeadYourPetMod.Settings = null;
            Game game = Raw<Game>();
            game.tickManager = Raw<TickManager>();
            map = Raw<Map>();
            map.info = Raw<MapInfo>();
            map.info.Size = new IntVec3(60, 1, 60);
            map.cellIndices = new CellIndices(map);
            Set(game, "maps", new List<Map> { map });
            component = new LeadYourPetGameComponent(game);
            Set(game, "components", new List<GameComponent> { component });
            Current.Game = game;
            pet = NewPawn(1, new IntVec3(20, 0, 20));
            master = NewPawn(2, new IntVec3(18, 0, 20));
            link = new LeashLink { Master = master, Pet = pet };
            ((List<LeashLink>)Get(component, "links")).Add(link);
            Invoke("RegisterLink", link);
        }

        [Theory]
        [InlineData(PawnRenderFlags.Portrait)]
        [InlineData(PawnRenderFlags.Cache)]
        [InlineData(PawnRenderFlags.Statue)]
        [InlineData(PawnRenderFlags.Portrait | PawnRenderFlags.Cache)]
        public void OffMapRenderKeepsItsLocalMatrixAndFacing(PawnRenderFlags flags)
        {
            AddAnimation();
            Matrix4x4 local = Matrix4x4.TRS(new Vector3(0.3f, 0.2f, -0.1f), Quaternion.identity, Vector3.one);
            PawnDrawParms parms = new PawnDrawParms { matrix = local, flags = flags, facing = Rot4.East };
            Patch_PawnRenderer_GetDrawParms.Postfix(pet, ref parms);
            Assert.Equal(local, parms.matrix);
            Assert.Equal(Rot4.East, parms.facing);
        }

        [Fact]
        public void MapAnimationUsesItsWorldTransformAndOnlyThatPawnDisablesAtlasCache()
        {
            YoungRatkinInteractionAnimation animation = AddAnimation();
            animation.StartDrawPos = new Vector3(25.5f, 0f, 22.5f);
            PawnDrawParms parms = new PawnDrawParms { matrix = Matrix4x4.identity, flags = PawnRenderFlags.None, facing = Rot4.West };
            Patch_PawnRenderer_GetDrawParms.Postfix(pet, ref parms);
            Assert.Equal(25.5f, parms.matrix.m03);
            Assert.Equal(22.5f, parms.matrix.m23);
            Assert.Equal(Rot4.South, parms.facing);
            bool petCacheDisabled = false;
            bool masterCacheDisabled = false;
            Patch_PawnRenderer_ParallelGetPreRenderResults.Prefix(pet, ref petCacheDisabled);
            Patch_PawnRenderer_ParallelGetPreRenderResults.Prefix(master, ref masterCacheDisabled);
            Assert.True(petCacheDisabled);
            Assert.False(masterCacheDisabled);
            Assert.Equal(new IntVec3(20, 0, 20), pet.Position);
        }

        [Fact]
        public void DraggedPawnAlsoBypassesAtlasCache()
        {
            link.IsDragging = true;
            bool disableCache = false;
            Patch_PawnRenderer_ParallelGetPreRenderResults.Prefix(pet, ref disableCache);
            Assert.True(disableCache);
        }

        [Fact]
        public void RopeUsesAnimatedPetAndVisualMasterWithoutMovingEitherSimulationEndpoint()
        {
            YoungRatkinInteractionAnimation animation = AddAnimation();
            animation.StartDrawPos = new Vector3(22.25f, 0f, 21.75f);
            master.VisualPosition = new Vector3(18.75f, 0f, 20.25f);
            Assert.True(Patch_PawnRopeTracker_RopingDraw.TryGetCustomRopeEndpoints(pet, out Vector3 start, out Vector3 end));
            Assert.Equal(new Vector3(22.25f, 0f, 21.75f), start);
            Assert.Equal(new Vector3(18.75f, 0f, 20.25f), end);
            Assert.Equal(new IntVec3(20, 0, 20), pet.Position);
            Assert.Equal(new IntVec3(18, 0, 20), master.Position);
        }

        [Fact]
        public void CellRopeEndsAtCellCenterAndHidesBeyondHardLimit()
        {
            link.AnchorMode = LeashAnchorMode.Cell;
            link.AnchorCell = new IntVec3(21, 0, 20);
            Assert.True(Patch_PawnRopeTracker_RopingDraw.TryGetCustomRopeEndpoints(pet, out _, out Vector3 end));
            Assert.Equal(new Vector3(21.5f, 0f, 20.5f), end);
            link.AnchorCell = new IntVec3(40, 0, 20);
            Assert.False(Patch_PawnRopeTracker_RopingDraw.TryGetCustomRopeEndpoints(pet, out _, out _));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GrabbedVisualEndpointHidesCustomRope(bool grabMaster)
        {
            AddAnimation();
            GodHandsCompat.RecordGrabbed(grabMaster ? master : pet, new object());
            Assert.False(Patch_PawnRopeTracker_RopingDraw.TryGetCustomRopeEndpoints(pet, out _, out _));
        }

        [Theory]
        [InlineData("pet-dead")]
        [InlineData("master-dead")]
        [InlineData("pet-despawned")]
        [InlineData("master-despawned")]
        [InlineData("pet-exiting")]
        [InlineData("master-exiting")]
        [InlineData("master-replaced")]
        [InlineData("link-replaced")]
        [InlineData("anchor-changed")]
        [InlineData("map-changed")]
        [InlineData("pet-moved")]
        public void InvalidAnimationContextCancelsRatherThanApplyingOldLanding(string change)
        {
            YoungRatkinInteractionAnimation animation = AddAnimation();
            animation.FinalizeTeleport = true;
            animation.TargetCell = new IntVec3(23, 0, 20);
            animation.EndTick = -1;
            switch (change)
            {
                case "pet-dead": Set(pet.health, "healthState", PawnHealthState.Dead); break;
                case "master-dead": Set(master.health, "healthState", PawnHealthState.Dead); break;
                case "pet-despawned": Set(pet, "mapIndexOrState", (sbyte)-1); break;
                case "master-despawned": Set(master, "mapIndexOrState", (sbyte)-1); break;
                case "pet-exiting": pet.CurJob.exitMapOnArrival = true; break;
                case "master-exiting": master.CurJob.exitMapOnArrival = true; break;
                case "master-replaced": link.Master = NewPawn(3, master.Position); break;
                case "link-replaced":
                    Invoke("UnregisterLink", link);
                    Invoke("RegisterLink", new LeashLink { Pet = pet, Master = master });
                    break;
                case "anchor-changed": link.AnchorMode = LeashAnchorMode.Cell; link.AnchorCell = master.Position; break;
                case "map-changed":
                    Map otherMap = Raw<Map>();
                    otherMap.info = Raw<MapInfo>();
                    otherMap.info.Size = map.Size;
                    ((List<Map>)Get(Current.Game, "maps")).Add(otherMap);
                    Set(pet, "mapIndexOrState", (sbyte)1);
                    Set(master, "mapIndexOrState", (sbyte)1);
                    break;
                case "pet-moved": Set(pet, "positionInt", new IntVec3(21, 0, 20)); break;
            }
            IntVec3 position = pet.Position;
            Invoke("TickAnimations");
            Assert.Null(component.GetInteractionAnimationForPet(pet));
            Assert.Equal(position, pet.Position);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LosingMobilityOrStartingRestCancelsActiveInteraction(bool rest)
        {
            YoungRatkinInteractionAnimation animation = AddAnimation();
            animation.Kind = LeadYourPetInteractionKind.Whirl;
            if (rest)
            {
                pet.jobs.curDriver = Raw<JobDriver_FollowLeashMaster>();
                pet.jobs.curDriver.asleep = true;
            }
            else Set(pet.health, "healthState", PawnHealthState.Down);
            Invoke("TickAnimations");
            Assert.Null(component.GetInteractionAnimationForPet(pet));
            Assert.Equal(new IntVec3(20, 0, 20), pet.Position);
        }

        [Fact]
        public void SleepingFeedingStillDoesNotStartAnAnimation()
        {
            pet.jobs.curDriver = Raw<JobDriver_FollowLeashMaster>();
            pet.jobs.curDriver.asleep = true;
            Invoke("StartInteractionAnimation", master, pet, LeadYourPetInteractionKind.Fed);
            Assert.Null(component.GetInteractionAnimationForPet(pet));
        }

        [Fact]
        public void ReplacingExpiringAndStoppingAnimationsNeverLeavesAnOldPose()
        {
            Invoke("StartInteractionAnimation", master, pet, LeadYourPetInteractionKind.Whirl);
            YoungRatkinInteractionAnimation first = component.GetInteractionAnimationForPet(pet);
            Assert.True(component.HasLayingInteractionAnimation(pet));
            Invoke("StartInteractionAnimation", master, pet, LeadYourPetInteractionKind.Slap);
            YoungRatkinInteractionAnimation replacement = component.GetInteractionAnimationForPet(pet);
            Assert.NotSame(first, replacement);
            Assert.True(component.HasBlockingInteractionAnimation(pet));
            Assert.False(component.HasLayingInteractionAnimation(pet));
            replacement.EndTick = -1;
            Invoke("TickAnimations");
            Assert.Null(component.GetInteractionAnimationForPet(pet));
            Invoke("StartInteractionAnimation", master, pet, LeadYourPetInteractionKind.Whirl);
            Invoke("StopInteractionAnimation", pet, false);
            Assert.False(component.HasBlockingInteractionAnimation(pet));
            Assert.False(component.TryGetInteractionAnimationRenderData(pet, out _, out _, out _, out _));
        }


        [Theory]
        [InlineData("wall-in-path")]
        [InlineData("blocked-destination")]
        [InlineData("rope-shortened")]
        [InlineData("anchor-moved")]
        [InlineData("anchor-other-map")]
        public void LandingRechecksCurrentObstaclesAnchorAndLeashLength(string change)
        {
            InitializeGrid();
            YoungRatkinInteractionAnimation animation = AddAnimation();
            animation.FinalizeTeleport = true;
            animation.TargetCell = new IntVec3(23, 0, 20);
            animation.EndTick = -1;
            switch (change)
            {
                case "wall-in-path": Block(new IntVec3(21, 0, 20)); break;
                case "blocked-destination": Block(animation.TargetCell); break;
                case "rope-shortened": LeadYourPetMod.Settings = new LeadYourPetSettings { maxLeashLength = 2 }; break;
                case "anchor-moved": Set(master, "positionInt", new IntVec3(5, 0, 20)); break;
                case "anchor-other-map": Set(master, "mapIndexOrState", (sbyte)-1); break;
            }
            Invoke("TickAnimations");
            Assert.Null(component.GetInteractionAnimationForPet(pet));
            Assert.Equal(new IntVec3(20, 0, 20), pet.Position);
        }

        [Fact]
        public void KnockbackMeasuresCellAnchorRatherThanDistantFormerMaster()
        {
            InitializeGrid();
            link.AnchorMode = LeashAnchorMode.Cell;
            link.AnchorCell = new IntVec3(21, 0, 20);
            Set(master, "positionInt", new IntVec3(50, 0, 20));
            Assert.True((bool)Invoke("IsLegalKnockbackCell", master, pet, new IntVec3(23, 0, 20)));
            link.AnchorCell = new IntVec3(40, 0, 20);
            Assert.False((bool)Invoke("IsLegalKnockbackCell", master, pet, new IntVec3(23, 0, 20)));
        }

        [Fact]
        public void NoLegalKnockbackCellKeepsOriginalPositionWithoutInventingFallback()
        {
            InitializeGrid();
            LeadYourPetMod.Settings = new LeadYourPetSettings { maxLeashLength = 1 };
            link.AnchorMode = LeashAnchorMode.Cell;
            link.AnchorCell = new IntVec3(45, 0, 20);
            IntVec3 resolved = (IntVec3)Invoke("ResolveKnockbackCell", master, pet, 3);
            Assert.Equal(pet.Position, resolved);
        }

        private YoungRatkinInteractionAnimation AddAnimation()
        {
            YoungRatkinInteractionAnimation animation = new YoungRatkinInteractionAnimation
            {
                Pet = pet, Master = master, StartMap = map, StartLink = link,
                StartAnchorMode = link.AnchorMode, StartAnchorThing = link.AnchorThing,
                StartAnchorCell = link.AnchorCell, StartPetCell = pet.Position,
                Kind = LeadYourPetInteractionKind.Drag, EndTick = 100,
                StartDrawPos = pet.VisualPosition, PetUsesCustomRender = true,
                PetLaying = true, BlocksPetMovement = true
            };
            Invoke("RegisterInteractionAnimation", animation);
            return animation;
        }

        private void InitializeGrid()
        {
            map.thingGrid = new ThingGrid(map);
            grid = new PathGrid(map, new PathGridDef());
            PathingContext context = new PathingContext(map, grid);
            map.pathing = Raw<Pathing>();
            Set(map.pathing, "normal", context);
            Set(map.pathing, "fenceBlocked", context);
        }

        private void Block(IntVec3 cell)
        {
            var costs = grid.Grid_Unsafe;
            costs[map.cellIndices.CellToIndex(cell)] = 10000;
        }

        private static VisualPawn NewPawn(int id, IntVec3 position)
        {
            VisualPawn pawn = Raw<VisualPawn>();
            pawn.thingIDNumber = id;
            pawn.VisualPosition = position.ToVector3Shifted();
            pawn.def = new ThingDef
            {
                defName = "Ratkin", race = new RaceProperties
                {
                    alwaysAwake = true,
                    lifeStageAges = new List<LifeStageAge> { new LifeStageAge { def = new LifeStageDef() } }
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
            Set(pawn, "positionInt", position);
            pawn.jobs = Raw<Pawn_JobTracker>();
            Set(pawn.jobs, "pawn", pawn);
            pawn.jobs.curJob = new Job { def = new JobDef { defName = "RenderFixtureIdle" } };
            return pawn;
        }

        private object Invoke(string name, params object[] args) => typeof(LeadYourPetGameComponent).GetMethod(name, Fields).Invoke(component, args);
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
            grid?.Dispose();
            GodHandsCompat.Reset();
            Current.Game = previousGame;
            LeadYourPetMod.Settings = previousSettings;
        }
    }
}
#endif
