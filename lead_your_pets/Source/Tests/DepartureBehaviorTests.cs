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
using Verse.AI.Group;
using Xunit;

namespace LeadYourPet.Tests
{
    // Behavioral regression tests for the non-player travel departure pipeline:
    // IsDepartureToil / IsNonPlayerTravelDeparture / IsUnprotectedTravelStock /
    // CanCarryForNonPlayerDeparture / CanPerformDepartureHold and
    // LeadYourPetGameComponent.PrepareTravelDeparture.
    //
    // These are not source-text or wiring tests: they construct real (if minimal) RimWorld
    // objects with FormatterServices.GetUninitializedObject, run the real production methods,
    // and assert observable results.
    //
    // Not covered here (kept out because they need the running game, not a managed unit host):
    //  * Anything that goes through native Unity: pawn.Drawer/renderer via Pawn.SetFaction or
    //    HediffSet.DirtyCache, and the Lord static cctor (MaterialPool.MatFrom).
    //  * TryGetTravelDepartureCarryJob's body, whose guard reads carrier.health.capacities and
    //    PawnCapacityDefOf.Manipulation; those need bound pawn-capacity defs.
    [Collection("Game state")]
    public class DepartureBehaviorTests : IDisposable
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly Game previousGame = Current.Game;
        private readonly LeadYourPetSettings previousSettings = LeadYourPetMod.Settings;

        private readonly Faction playerFaction;
        private readonly Faction visitorFaction;
        private readonly Lord lord;
        private readonly LeadYourPetGameComponent component;

        public DepartureBehaviorTests()
        {
            LeadYourPetMod.Settings = null;
            GodHandsCompat.Reset();

            playerFaction = RawFaction("PlayerColony", isPlayer: true);
            visitorFaction = RawFaction("Visitor", isPlayer: false);

            // VisitColony owns exitSubgraph; TravelAndExit's graph is { LordToil_Travel, LordToil_ExitMap }.
            LordJob_VisitColony visitJob = new LordJob_VisitColony();
            // exitSubgraph is the Farewell/exit branch: { travel-to-edge, exit map }.
            StateGraph exit = new StateGraph();
            exit.AddToil(new LordToil_Travel(new IntVec3(10, 0, 10)));
            exit.AddToil(new LordToil_ExitMap(LocomotionUrgency.Walk));
            visitJob.exitSubgraph = exit;

            lord = Raw<Lord>();
            lord.faction = visitorFaction;
            lord.ownedPawns = new List<Pawn>();
            Set(lord, "curJob", visitJob);
            // Arrival toil is a Travel toil that belongs to no exit subgraph.
            Set(lord, "curLordToil", new LordToil_Travel(new IntVec3(5, 0, 5)));

            Game game = Raw<Game>();
            game.tickManager = Raw<TickManager>();
            Map map = Raw<Map>();
            map.info = Raw<MapInfo>();
            map.info.Size = new IntVec3(300, 1, 300);
            Set(game, "maps", new List<Map> { map });
            Set(game, "components", new List<GameComponent>());
            component = new LeadYourPetGameComponent(game);
            game.components.Add(component);
            Current.Game = game;
        }

        // ---------------- definitions layer ----------------

        [Fact]
        public void ExitMapToilIsADepartureButArrivalTravelIsNot()
        {
            Assert.False(LeadYourPetUtility.IsDepartureToil(lord, CurrentLordToil()));
            Assert.False(LeadYourPetUtility.IsDepartureToil(lord, new LordToil_Travel(new IntVec3(7, 0, 7))));
            Assert.True(LeadYourPetUtility.IsDepartureToil(lord, VisitExitToil()));
            Assert.True(LeadYourPetUtility.IsDepartureToil(lord, new LordToil_ExitMap(LocomotionUrgency.Walk)));
            Assert.False(LeadYourPetUtility.IsDepartureToil(lord, null));
        }

        [Fact]
        public void PlayerLordFactionIsNeverANonPlayerDeparture()
        {
            Lord playerLord = NewLord(playerFaction);
            Pawn colonist = NewSpawnedPawn(playerLord, new IntVec3(1, 0, 1));

            Assert.False(LeadYourPetUtility.IsNonPlayerTravelDeparture(colonist));
        }

        [Fact]
        public void NonPlayerPawnInExitMapToilIsDepartingWhileArrivalTravelIsNot()
        {
            Pawn member = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));

            Assert.False(LeadYourPetUtility.IsNonPlayerTravelDeparture(member));

            Set(lord, "curLordToil", VisitExitToil());
            Assert.True(LeadYourPetUtility.IsNonPlayerTravelDeparture(member));
        }

        [Fact]
        public void PlayerFactionPawnInVisitorLordIsNotTreatedAsDeparting()
        {
            Pawn colonist = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            SetFaction(colonist, playerFaction);
            Set(lord, "curLordToil", VisitExitToil());

            Assert.False(LeadYourPetUtility.IsNonPlayerTravelDeparture(colonist));
        }

        [Fact]
        public void StandaloneTravelAndExitIsDepartingBeforeReachingTheEdge()
        {
            Set(lord, "curJob", new LordJob_TravelAndExit(new IntVec3(10, 0, 10)));
            Set(lord, "curLordToil", new LordToil_Travel(new IntVec3(10, 0, 10)));
            Pawn member = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));

            Assert.True(LeadYourPetUtility.IsNonPlayerTravelDeparture(member));
        }

        [Fact]
        public void GrabbedMemberBlocksOnlyScheduledDepartureArrivalChecks()
        {
            LordToil_Travel travel = (LordToil_Travel)((LordJob_VisitColony)lord.LordJob).exitSubgraph.lordToils[0];
            travel.lord = lord;
            Set(lord, "curLordToil", travel);
            Pawn member = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            object hand = new object();
            GodHandsCompat.RecordGrabbed(member, hand);
            Assert.False(Patch_Travel_CarriedMemberArrival.Prefix(travel));
            Set(Current.Game.tickManager, "ticksGameInt", 1);
            Assert.True(Patch_Travel_CarriedMemberArrival.Prefix(travel));
            Set(Current.Game.tickManager, "ticksGameInt", 205);
            Assert.False(Patch_Travel_CarriedMemberArrival.Prefix(travel));
            GodHandsCompat.RecordReleased(hand);
            Assert.True(Patch_Travel_CarriedMemberArrival.Prefix(travel));
        }


        [Fact]
        public void IncapacitatedTravelStockRemainsOwnedBeforeDeparture()
        {
            Pawn stock = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            MarkTravelStock(stock);

            bool remove = lord.LordJob.ShouldRemovePawn(stock, PawnLostCondition.Incapped);
            Patch_LordJob_KeepIncapacitatedTravelStock.Postfix(lord.LordJob, stock, PawnLostCondition.Incapped, ref remove);
            Assert.False(remove);

            remove = true;
            Patch_LordJob_KeepIncapacitatedTravelStock.Postfix(lord.LordJob, stock, PawnLostCondition.Killed, ref remove);
            Assert.True(remove);

            remove = true;
            Patch_LordJob_KeepIncapacitatedTravelStock.Postfix(lord.LordJob, stock, PawnLostCondition.ChangedFaction, ref remove);
            Assert.True(remove);
        }

        [Fact]
        public void IncapacitationDoesNotRetainVisitorsProtectedStockOrAnotherLordsStock()
        {
            Pawn member = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            bool remove = true;
            Patch_LordJob_KeepIncapacitatedTravelStock.Postfix(lord.LordJob, member, PawnLostCondition.Incapped, ref remove);
            Assert.True(remove);

            MarkTravelStock(member, currentMaster: NewSpawnedPawn(playerFaction, new IntVec3(2, 0, 2)));
            remove = true;
            Patch_LordJob_KeepIncapacitatedTravelStock.Postfix(lord.LordJob, member, PawnLostCondition.Incapped, ref remove);
            Assert.True(remove);

            Pawn otherStock = NewSpawnedPawn(NewLord(visitorFaction), new IntVec3(3, 0, 3));
            MarkTravelStock(otherStock);
            remove = true;
            Patch_LordJob_KeepIncapacitatedTravelStock.Postfix(lord.LordJob, otherStock, PawnLostCondition.Incapped, ref remove);
            Assert.True(remove);
        }
        [Fact]
        public void DeadTravelStockIsNeverRetainedOnIncapacitationSignal()
        {
            Pawn stock = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            MarkTravelStock(stock);
            Set(stock.health, "healthState", PawnHealthState.Dead);

            bool remove = true;
            Patch_LordJob_KeepIncapacitatedTravelStock.Postfix(lord.LordJob, stock, PawnLostCondition.Incapped, ref remove);

            Assert.True(remove);
        }

        [Fact]
        public void JoiningTravelLordPublishesBothSidesAndNeverStealsAnotherLordsMember()
        {
            Map map = Current.Game.Maps[0];
            lord.lordManager = Raw<LordManager>();
            lord.lordManager.map = map;
            map.attackTargetsCache = Raw<AttackTargetsCache>();
            Set(map.attackTargetsCache, "allTargets", new HashSet<IAttackTarget>());
            lord.CurLordToil.lord = lord;
            Pawn stock = NewSpawnedPawn(visitorFaction, new IntVec3(1, 0, 1));

            Assert.True(LeadYourPetGameComponent.TryEnsureTravelLordMembership(lord, stock));
            Assert.Same(lord, stock.GetLord());
            Assert.Contains(stock, lord.ownedPawns);
            Assert.NotNull(stock.mindState.duty);
            Assert.True(LeadYourPetGameComponent.TryEnsureTravelLordMembership(lord, stock));
            Assert.Single(lord.ownedPawns);

            Lord otherLord = NewLord(visitorFaction);
            Pawn otherStock = NewSpawnedPawn(otherLord, new IntVec3(2, 0, 2));
            Assert.False(LeadYourPetGameComponent.TryEnsureTravelLordMembership(lord, otherStock));
            Assert.Same(otherLord, otherStock.GetLord());
            Assert.Contains(otherStock, otherLord.ownedPawns);
            Assert.DoesNotContain(otherStock, lord.ownedPawns);
        }

        [Theory]
        [InlineData("AttackMelee")]
        [InlineData("AttackStatic")]
        [InlineData("Wait_Combat")]
        [InlineData("Rescue")]
        [InlineData("TendPatient")]
        [InlineData("Ingest")]
        [InlineData("GotoSafeTemperature")]
        [InlineData("Flee")]
        public void DutyCarryKeepsCombatCareAndEmergencyResults(string jobName)
        {
            Pawn carrier = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());
            Job job = new Job { def = new JobDef { defName = jobName } };
            ThinkNode source = new ThinkNode_Duty();
            ThinkResult result = new ThinkResult(job, source, JobTag.Misc);

            Patch_Duty_TravelDepartureCarry.Postfix((ThinkNode_Duty)source, carrier, default, ref result);

            Assert.Same(job, result.Job);
            Assert.Same(source, result.SourceNode);
            Assert.Equal(JobTag.Misc, result.Tag);
        }

        [Fact]
        public void DutyCarryDoesNotReplaceCombatMovementOrQueuedTravel()
        {
            Pawn carrier = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());
            Job combatMovement = new Job { def = new JobDef { defName = "Goto" } };
            ThinkNode combatSource = new JobGiver_AIDefendSelf();
            ThinkResult result = new ThinkResult(combatMovement, combatSource);
            Patch_Duty_TravelDepartureCarry.Postfix(new ThinkNode_Duty(), carrier, default, ref result);
            Assert.Same(combatMovement, result.Job);
            Assert.Same(combatSource, result.SourceNode);

            ThinkNode travelSource = new JobGiver_GotoTravelDestination();
            result = new ThinkResult(combatMovement, travelSource, fromQueue: true);
            Patch_Duty_TravelDepartureCarry.Postfix(new ThinkNode_Duty(), carrier, default, ref result);
            Assert.Same(combatMovement, result.Job);
            Assert.Same(travelSource, result.SourceNode);
            Assert.True(result.FromQueue);
        }

        [Fact]
        public void MasterExitLeavesExternallyHeldStockPlacedAndItsHoldJobUntouched()
        {
            LeadYourPetMod.Settings = new LeadYourPetSettings { leashedPawnMapExitBehavior = LeashedPawnMapExitBehavior.Disappear };
            Pawn master = NewSpawnedPawn(lord, new IntVec3(0, 0, 1));
            Pawn pet = NewSpawnedPawn(lord, new IntVec3(120, 0, 120));
            MarkTravelStock(pet, currentMaster: master, leashMaster: master);
            Job hold = new Job { def = new JobDef { defName = "RimTalk_BeingCarried_Idle" } };
            Set(pet.jobs, "curJob", hold);

            component.HandleMasterExitMap(master, Rot4.West);

            Assert.True(pet.Spawned);
            Assert.False(pet.Destroyed);
            Assert.Equal(new IntVec3(120, 0, 120), pet.Position);
            Assert.Same(hold, pet.CurJob);
            Assert.Same(lord, pet.GetLord());
            Assert.Contains(pet, lord.ownedPawns);
            Assert.Null(component.GetLinkForPet(pet));
        }


        [Fact]
        public void DepartingStockFollowsItsOwnLordMasterButNotAProtectedOrOtherLordMaster()
        {
            Pawn pet = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Pawn master = NewSpawnedPawn(lord, new IntVec3(2, 0, 2));
            MarkTravelStock(pet, currentMaster: master, leashMaster: master);
            LeashLink link = component.GetLinkForPet(pet);
            Assert.False(component.ShouldFollowDuringDeparture(link));

            Set(lord, "curLordToil", VisitExitToil());
            Assert.True(component.ShouldFollowDuringDeparture(link));

            master.lord = NewLord(visitorFaction);
            Assert.False(component.ShouldFollowDuringDeparture(link));
            master.lord = lord;
            SetFaction(master, playerFaction);
            Assert.False(component.ShouldFollowDuringDeparture(link));
        }

        // ---------------- stock protection layer ----------------

        [Fact]
        public void TravelStockWithPlayerMasterOrPlayerLinkIsProtectedFromDeparture()
        {
            Pawn barnaby = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());
            MarkTravelStock(barnaby);

            Assert.True(LeadYourPetUtility.IsUnprotectedTravelStock(barnaby));

            MarkTravelStock(barnaby, currentMaster: null, leashMaster: NewSpawnedPawn(playerFaction, new IntVec3(2, 0, 2)));
            Assert.False(LeadYourPetUtility.IsUnprotectedTravelStock(barnaby));

            MarkTravelStock(barnaby, currentMaster: NewSpawnedPawn(playerFaction, new IntVec3(3, 0, 3)), leashMaster: null);
            Assert.False(LeadYourPetUtility.IsUnprotectedTravelStock(barnaby));
        }

        [Fact]
        public void PlainVisitorWithoutTravelStockIsNotUnprotectedStock()
        {
            Pawn visitor = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Assert.False(LeadYourPetUtility.IsUnprotectedTravelStock(visitor));

            component.EnsureYoungRatkinState(visitor); // state exists but IsTravelStock is false
            Assert.False(LeadYourPetUtility.IsUnprotectedTravelStock(visitor));
        }

        // ---------------- carry authorization layer ----------------

        [Fact]
        public void CarryIsAllowedOnlyForSameLordUnprotectedTravelStock()
        {
            Pawn carrier = NewSpawnedPawn(lord, new IntVec3(2, 0, 2));
            Pawn load = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());

            Assert.False(LeadYourPetUtility.CanCarryForNonPlayerDeparture(carrier, load)); // not travel stock

            MarkTravelStock(load);
            Assert.True(LeadYourPetUtility.CanCarryForNonPlayerDeparture(carrier, load));

            MarkTravelStock(carrier); // both are stock with no player master: still allowed
            Assert.True(LeadYourPetUtility.CanCarryForNonPlayerDeparture(carrier, load));

            Assert.False(LeadYourPetUtility.CanCarryForNonPlayerDeparture(load, load)); // self carry
            Assert.False(LeadYourPetUtility.CanCarryForNonPlayerDeparture(carrier, null));
            Assert.False(LeadYourPetUtility.CanCarryForNonPlayerDeparture(null, load));
        }

        [Fact]
        public void CarryIsDirectionalAndTargetMustBeTheUnprotectedStock()
        {
            Pawn carrier = NewSpawnedPawn(lord, new IntVec3(2, 0, 2));
            Pawn load = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());
            MarkTravelStock(load); // load is stock, carrier is a plain visitor

            Assert.True(LeadYourPetUtility.CanCarryForNonPlayerDeparture(carrier, load));
            // A plain visitor is not unprotected stock, so it must not be carried even by stock.
            Assert.False(LeadYourPetUtility.CanCarryForNonPlayerDeparture(load, carrier));
        }

        [Fact]
        public void CarryAcrossLordsIsRejected()
        {
            Pawn carrier = NewSpawnedPawn(lord, new IntVec3(2, 0, 2));
            Lord otherLord = NewLord(visitorFaction);
            Pawn otherStock = NewSpawnedPawn(otherLord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());
            Set(otherLord, "curLordToil", ExitToil(otherLord));
            MarkTravelStock(carrier);
            MarkTravelStock(otherStock);

            Assert.False(LeadYourPetUtility.CanCarryForNonPlayerDeparture(carrier, otherStock));
        }

        [Fact]
        public void PlayerMasterLinkBlocksCarryEvenWhenStockIsMarked()
        {
            Pawn carrier = NewSpawnedPawn(lord, new IntVec3(2, 0, 2));
            Pawn load = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());
            MarkTravelStock(load, currentMaster: null, leashMaster: NewSpawnedPawn(playerFaction, new IntVec3(5, 0, 5)));

            Assert.False(LeadYourPetUtility.CanCarryForNonPlayerDeparture(carrier, load));
        }

        [Fact]
        public void RimTalkBeingCarriedJobReversesActorAndTarget()
        {
            Pawn carrier = NewSpawnedPawn(lord, new IntVec3(2, 0, 2));
            Pawn load = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Set(lord, "curLordToil", VisitExitToil());
            MarkTravelStock(load);

            // The job runs on the carried pawn; its target is the carrier holding it.
            Assert.True(LeadYourPetUtility.CanPerformDepartureHold(load, carrier, "RimTalk_BeingCarried_Idle"));
            Assert.True(LeadYourPetUtility.CanPerformDepartureHold(load, carrier, "RimTalk_BeingCarriedSleep"));
            // Only the RimTalk carry family is reversed; any other job keeps normal direction.
            Assert.False(LeadYourPetUtility.CanPerformDepartureHold(load, carrier, "RimTalk_PickUpToddler"));
            Assert.False(LeadYourPetUtility.CanPerformDepartureHold(load, carrier, null));
        }

        // ---------------- prepare-departure state transition ----------------


        [Fact]
        public void PrepareIgnoresPlayerMasteredStock()
        {
            Pawn protectedPet = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            Pawn playerMaster = NewSpawnedPawn(playerFaction, new IntVec3(2, 0, 2));
            YoungRatkinState state = MarkTravelStock(protectedPet, currentMaster: playerMaster, leashMaster: playerMaster);
            state.IsPet = true;
            Set(lord, "curLordToil", VisitExitToil());

            component.PrepareTravelDeparture(lord);

            Assert.NotNull(component.GetLinkForPet(protectedPet)); // player-owned leash survives
            Assert.True(state.IsPet);
            Assert.Same(playerMaster, state.CurrentMaster);
        }

        [Fact]
        public void PrepareOnlyProcessesTheProvidedLord()
        {
            Set(lord, "curLordToil", VisitExitToil());

            Lord otherLord = NewLord(visitorFaction);
            Set(otherLord, "curLordToil", ExitToil(otherLord));
            Pawn otherStock = NewSpawnedPawn(otherLord, new IntVec3(3, 0, 3));
            Pawn otherMaster = NewSpawnedPawn(otherLord, new IntVec3(4, 0, 4));
            YoungRatkinState otherState = MarkTravelStock(otherStock, currentMaster: otherMaster, leashMaster: otherMaster);
            otherState.IsPet = true;

            component.PrepareTravelDeparture(lord);

            Assert.NotNull(component.GetLinkForPet(otherStock)); // not the lord we were asked about
            Assert.True(otherState.IsPet);
            Assert.Same(otherMaster, otherState.CurrentMaster);
        }

        [Fact]
        public void PrepareKeepsYoungRatkinPetLeash()
        {
            AssertLeashStaysOnDeparture(LeashLinkKind.YoungRatkinPet);
        }

        [Fact]
        public void PrepareKeepsRatkinMotherBabyLeash()
        {
            AssertLeashStaysOnDeparture(LeashLinkKind.RatkinMotherBaby);
        }

        private void AssertLeashStaysOnDeparture(LeashLinkKind kind)
        {
            Pawn member = NewSpawnedPawn(lord, new IntVec3(1, 0, 1));
            member.health = Raw<Pawn_HealthTracker>();
            Set(member.health, "healthState", PawnHealthState.Down);
            Pawn master = NewSpawnedPawn(lord, new IntVec3(2, 0, 2));
            YoungRatkinState state = MarkTravelStock(member, currentMaster: master, leashMaster: master, linkKind: kind);
            state.IsPet = true;
            Set(lord, "curLordToil", VisitExitToil());

            component.PrepareTravelDeparture(lord);

            Assert.NotNull(component.GetLinkForPet(member));
            Assert.True(state.IsPet);
            Assert.Same(master, state.CurrentMaster);
            Assert.True(state.IsTravelStock);
            Assert.Same(lord, member.GetLord());
        }

        [Fact]
        public void PrepareReturnsForNullPlayerAndNonDepartureLords()
        {
            // Null lord: nothing to do, and the fixture must survive untouched.
            component.PrepareTravelDeparture(null);

            // Player-faction lord that is leaving: still must not be treated as a non-player departure.
            Lord playerLord = NewLord(playerFaction);
            Set(playerLord, "curLordToil", ExitToil(playerLord));
            Pawn playerStock = NewSpawnedPawn(playerLord, new IntVec3(2, 0, 2));
            Pawn playerMaster = NewSpawnedPawn(playerFaction, new IntVec3(3, 0, 3));
            YoungRatkinState playerState = MarkTravelStock(playerStock, currentMaster: playerMaster, leashMaster: playerMaster);
            playerState.IsPet = true;

            component.PrepareTravelDeparture(playerLord);

            Assert.NotNull(component.GetLinkForPet(playerStock));
            Assert.True(playerState.IsPet);
            Assert.Same(playerMaster, playerState.CurrentMaster);

            // Non-player lord stuck on an arrival toil: not a departure, so the leash stays.
            Pawn member = NewSpawnedPawn(lord, new IntVec3(4, 0, 4));
            Pawn master = NewSpawnedPawn(lord, new IntVec3(5, 0, 5));
            YoungRatkinState state = MarkTravelStock(member, currentMaster: master, leashMaster: master);
            state.IsPet = true;

            component.PrepareTravelDeparture(lord);

            Assert.NotNull(component.GetLinkForPet(member));
            Assert.True(state.IsPet);
        }

        private LordToil CurrentLordToil() => lord.CurLordToil;

        private LordToil VisitExitToil()
            => lord.LordJob is LordJob_VisitColony visit ? visit.exitSubgraph.lordToils[1] : null;

        private static LordToil ExitToil(Lord owner) => new LordToil_ExitMap(LocomotionUrgency.Walk);

        private static Lord NewLord(Faction faction)
        {
            Lord created = Raw<Lord>();
            created.faction = faction;
            created.ownedPawns = new List<Pawn>();
            LordJob_VisitColony job = new LordJob_VisitColony();
            StateGraph exit = new StateGraph();
            exit.AddToil(new LordToil_Travel(new IntVec3(10, 0, 10)));
            exit.AddToil(new LordToil_ExitMap(LocomotionUrgency.Walk));
            job.exitSubgraph = exit;
            Set(created, "curJob", job);
            Set(created, "curLordToil", new LordToil_Travel(new IntVec3(5, 0, 5)));
            return created;
        }

        // ---------------- fixture helpers ----------------

        private YoungRatkinState MarkTravelStock(
            Pawn pawn,
            Pawn currentMaster = null,
            Pawn leashMaster = null,
            LeashLinkKind linkKind = LeashLinkKind.YoungRatkinPet)
        {
            YoungRatkinState state = component.EnsureYoungRatkinState(pawn);
            state.IsTravelStock = true;
            state.CurrentMaster = currentMaster;
            if (leashMaster != null)
            {
                StartLink(pawn, leashMaster, linkKind);
            }

            return state;
        }

        private void StartLink(Pawn pet, Pawn master, LeashLinkKind kind)
        {
            LeashLink link = new LeashLink { Master = master, Pet = pet, Kind = kind };
            ((List<LeashLink>)Get(component, "links")).Add(link);
            // RegisterLink is the same entry point StartLeashInternal uses to publish a link.
            typeof(LeadYourPetGameComponent)
                .GetMethod("RegisterLink", Fields)
                .Invoke(component, new object[] { link });
        }

        private Pawn NewSpawnedPawn(Lord owner, IntVec3 position)
        {
            Pawn pawn = NewPawn(RandomId(), position);
            pawn.lord = owner;
            owner.ownedPawns.Add(pawn);
            return pawn;
        }

        private Pawn NewSpawnedPawn(Faction faction, IntVec3 position)
        {
            Pawn pawn = NewPawn(RandomId(), position);
            SetFaction(pawn, faction);
            return pawn;
        }

        private static int idCounter;

        private static int RandomId()
        {
            idCounter++;
            return idCounter;
        }

        private static Pawn NewPawn(int id, IntVec3 position)
        {
            Pawn pawn = Raw<Pawn>();
            pawn.def = Raw<ThingDef>();
            pawn.def.defName = "Ratkin";
            pawn.thingIDNumber = id;
            Set(pawn, "mapIndexOrState", (sbyte)0);
            Set(pawn, "positionInt", position);
            pawn.health = Raw<Pawn_HealthTracker>();
            Set(pawn.health, "pawn", pawn);
            Set(pawn.health, "healthState", PawnHealthState.Mobile);
            pawn.health.hediffSet = new HediffSet(pawn);
            pawn.mindState = Raw<Pawn_MindState>();
            Set(pawn.mindState, "pawn", pawn);
            pawn.jobs = Raw<Pawn_JobTracker>();
            Set(pawn.jobs, "pawn", pawn);
            return pawn;
        }

        private static void SetFaction(Thing thing, Faction faction)
        {
            // Thing.SetFactionDirect dereferences def.CanHaveFaction and can Log.Error; a raw ThingDef
            // has no defName-injected flags guaranteed. The property has no setter, so write the
            // protected base field directly, exactly as Thing.Faction reads it.
            Set(thing, "factionInt", faction);
        }

        private static Faction RawFaction(string defName, bool isPlayer)
        {
            Faction faction = Raw<Faction>();
            faction.def = Raw<FactionDef>();
            faction.def.defName = defName;
            faction.def.isPlayer = isPlayer;
            return faction;
        }

        public void Dispose()
        {
            GodHandsCompat.Reset();
            Current.Game = previousGame;
            LeadYourPetMod.Settings = previousSettings;
        }

        private static T Raw<T>() => (T)UninitializedObject.GetUninitializedObject(typeof(T));

        private static FieldInfo Field(object instance, string name)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Fields);
                if (field != null)
                {
                    return field;
                }
            }

            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        private static object Get(object instance, string name) => Field(instance, name).GetValue(instance);

        private static void Set(object instance, string name, object value) => Field(instance, name).SetValue(instance, value);
    }
}
