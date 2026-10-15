using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        internal int LinkCount => links.Count;

        internal void ResumeAfterExternalGrab(Pawn pawn)
        {
            bool relevant = LeadYourPetUtility.IsUnprotectedTravelStock(pawn)
                && LeadYourPetUtility.IsNonPlayerTravelDeparture(pawn);
            for (int i = 0; i < links.Count; i++)
            {
                LeashLink link = links[i];
                if (ReferenceEquals(link.Pet, pawn) || ReferenceEquals(link.Master, pawn)
                    || ReferenceEquals(link.AnchorThing, pawn))
                {
                    link.ForceImmediateUpdate = true;
                    relevant = true;
                }
            }

            // ForceReleaseGrab clears ownership but does not end GodHands' long Wait job.
            if (relevant && pawn.CurJob?.def?.defName == "Wait" && pawn.CurJob.expiryInterval == 99999
                && pawn.Spawned && !pawn.Dead)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            }
        }

        private void ProcessLink(LeashLink link, int ticksGame)
        {
            if (link == null || link.Master == null || link.Pet == null || GetLinkForPet(link.Pet) != link)
            {
                return;
            }
            if (LeadYourPetUtility.IsNonPlayerTravelDeparture(link.Pet)
                || LeadYourPetUtility.IsNonPlayerTravelDeparture(link.Master))
            {
                PrepareTravelDeparture(link.Pet.GetLord() ?? link.Master.GetLord());
                if (GetLinkForPet(link.Pet) != link)
                {
                    return;
                }
            }

            if (GodHandsCompat.IsGrabbed(link.Pet) || GodHandsCompat.IsGrabbed(link.Master)
                || (LeadYourPetUtility.GetCurrentFollowThing(link) is Pawn grabbedAnchor && GodHandsCompat.IsGrabbed(grabbedAnchor)))
            {
                StopInteractionAnimation(link.Pet, finalizeTeleport: false);
                if (!GodHandsCompat.IsGrabbed(link.Pet))
                {
                    link.Pet.pather?.StopDead();
                }
                link.IsDragging = false;
                MaintainDraggedSlow(link.Pet, false);
                link.ForceImmediateUpdate = true;
                return;
            }

            bool forceImmediateUpdate = link.ForceImmediateUpdate;
            link.ForceImmediateUpdate = false;

            if (link.Pet.CarriedBy != null
                && link.Pet.CarriedBy.carryTracker != null
                && link.Pet.CarriedBy.carryTracker.CarriedThing == link.Pet)
            {
                bool dropSucceeded = false;
                if (LeadYourPetUtility.ShouldBlockCarryOfProtectedMouseEgg(link.Pet.CarriedBy, link.Pet))
                {
                    dropSucceeded = link.Pet.CarriedBy.carryTracker.TryDropCarriedThing(link.Pet.CarriedBy.Position, ThingPlaceMode.Near, out Thing _);
                }
                else
                {
                    return;
                }

                if (LeadYourPetRules.ShouldPauseLeashForCarriedPet(
                    petWasCarried: true,
                    dropSucceeded: dropSucceeded,
                    petStillUnavailable: link.Pet.CarriedBy != null || !link.Pet.Spawned))
                {
                    link.ForceImmediateUpdate = true;
                    return;
                }
            }

            if (LeadYourPetUtility.ShouldBreakNonPlayerTravelLeashAfterAcquisition(link))
            {
                EndLink(link, false);
                return;
            }

            if (ShouldBreak(link))
            {
                EndLink(link, false);
                return;
            }

            if (link.Kind == LeashLinkKind.MouseEggPet && !LeadYourPetUtility.CanBePetMouseEgg(link.Pet, out _))
            {
                ClearMouseEggPetState(link.Pet);
                return;
            }

            if (link.Kind == LeashLinkKind.RatkinMotherBaby && !LeadYourPetUtility.CanStartRatkinMotherLeash(link.Master, link.Pet, out _))
            {
                EndLink(link, false);
                return;
            }

            ValidateAnchor(link);
            bool fixedAnchor = LeadYourPetUtility.HasFixedAnchor(link);
            bool hostileAttack = LeadYourPetUtility.AllowsLeashedHostileAttack(link);
            if ((link.AnchorMode != LeashAnchorMode.None && IsRunningFollowMasterJob(link))
                || (hostileAttack && link.Pet.CurJobDef == LeadYourPetDefOf.LeadYourPet_FollowMaster))
            {
                ResetAnchorControl(link);
            }
            ReleaseExternalToddlerHold(link.Pet);
            if (ticksGame - link.LastMoodRefreshTick >= 180)
            {
                link.LastMoodRefreshTick = ticksGame;
                LeadYourPetUtility.MaintainLeashMood(link.Master, link.Pet);
            }

            if (HasBlockingInteractionAnimation(link.Pet))
            {
                if (fixedAnchor || hostileAttack || LeadYourPetUtility.ShouldPreserveMapExitMovement(link.Pet))
                {
                    StopInteractionAnimation(link.Pet, finalizeTeleport: false);
                }
                else
                {
                    link.Pet.pather?.StopDead();
                    return;
                }
            }

            if (fixedAnchor)
            {
                link.MouseEggDutyState = LeashedMouseEggDutyState.FollowMaster;
                UpdateMouseEggDutyReport(link);
            }
            if (LeadYourPetUtility.HasAnchoredCareJob(link))
            {
                link.IsDragging = false;
                MaintainDraggedSlow(link.Pet, false);
                if (link.Pet.CurJobDef != JobDefOf.Ingest)
                {
                    TryFeedPet(link, ticksGame);
                }
                return;
            }

            bool sleepingMaster = HandleSleepingMaster(link);
            bool runExpensiveUpdate = ShouldRunExpensiveLinkUpdate(link, ticksGame, forceImmediateUpdate);
            if (link.Kind == LeashLinkKind.MouseEggPet)
            {
                link.MouseEggDutyState = LeadYourPetRules.ResolveLeashedMouseEggDutyState(
                    masterSleeping: sleepingMaster,
                    masterEating: link.AnchorMode == LeashAnchorMode.None && LeadYourPetUtility.MasterEating(link.Master));
                UpdateMouseEggDutyReport(link);
            }

            bool draggingNow = LeadYourPetUtility.ShouldDrag(link);
            if (draggingNow && !link.IsDragging)
            {
                link.CachedHasValidAutoInteractionPool = false;
                link.Pet.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 0f, 0f, -1f, link.Master));
                if (link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby)
                {
                    ApplyInteraction(link.Master, link.Pet, LeadYourPetInteractionKind.Drag, true);
                }

                if (Rand.Chance(0.08f))
                {
                    AddDragBruise(link.Pet);
                }
            }

            link.IsDragging = draggingNow;
            MaintainDraggedSlow(link.Pet, draggingNow);

            IntVec3 targetCell = ResolveTargetCell(link);
            if (!targetCell.IsValid)
            {
                EndLink(link, false);
                return;
            }

            float distance = targetCell.DistanceTo(link.Pet.Position);
            bool preserveMapExit = LeadYourPetUtility.ShouldPreserveMapExitMovement(link.Pet);
            if (!preserveMapExit && distance > LeadYourPetUtility.HardLeashLength)
            {
                TightPullBack(link, targetCell);
                distance = targetCell.DistanceTo(link.Pet.Position);
            }

            if (hostileAttack)
            {
                return;
            }

            if (!preserveMapExit && (link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !LeadYourPetUtility.CanMouseEggMove(link.Pet) && draggingNow)
            {
                PullImmobileMouseEgg(link, targetCell);
            }

            if (!preserveMapExit && !fixedAnchor && (distance > LeadYourPetUtility.MaxLeashLength || draggingNow))
            {
                if (runExpensiveUpdate)
                {
                    GentlePullBack(link, targetCell, ticksGame, forceImmediateUpdate);
                }
            }

            if (!preserveMapExit && !sleepingMaster && (distance > LeadYourPetUtility.FollowThreshold || draggingNow || ShouldTargetMovementRefresh(link)))
            {
                if (runExpensiveUpdate)
                {
                    UpdateMouseEggDutyReport(link);
                    ForceFollow(link, ticksGame, forceImmediateUpdate);
                }
            }

            if (!preserveMapExit
                && link.Kind == LeashLinkKind.MouseEggPet
                && link.MouseEggDutyState != LeashedMouseEggDutyState.FollowMaster
                && !draggingNow
                && distance <= LeadYourPetUtility.MaxLeashLength)
            {
                link.Pet.pather?.StopDead();
                return;
            }

            if (runExpensiveUpdate)
            {
                link.LastExpensiveUpdateTick = ticksGame;

                if (fixedAnchor)
                {
                    TryFeedPet(link, ticksGame);
                }
                else if (link.Kind == LeashLinkKind.MouseEggPet)
                {
                    ProcessMouseEggPet(link, draggingNow, distance, ticksGame, sleepingMaster);
                }
                else if (link.Kind == LeashLinkKind.NormalPet)
                {
                    TryFeedPet(link, ticksGame);
                }
            }
        }

        private bool ShouldRunExpensiveLinkUpdate(LeashLink link, int ticksGame, bool forceImmediateUpdate)
        {
            return LeadYourPetRules.ShouldRunExpensiveLinkUpdate(
                currentTick: ticksGame,
                lastExpensiveUpdateTick: link.LastExpensiveUpdateTick,
                minIntervalTicks: 60,
                forceImmediate: forceImmediateUpdate);
        }

        private void ProcessMouseEggPet(LeashLink link, bool draggingNow, float distance, int ticksGame, bool sleepingMaster)
        {
            bool playerControlled = link.Master.Faction == Faction.OfPlayer;
            bool canUseSingleDirection = LeadYourPetUtility.CanUseSingleDirectionInteractions(link.Master, link.Pet);
            bool canAutoInteract = !sleepingMaster && !link.Master.Drafted && link.Master.pather?.Moving != true;
            if (canAutoInteract && ticksGame >= link.NextAutoInteractionTick && distance <= 5.9f && !draggingNow
                && !ShouldSuppressTravelAutoInteraction(link)
                && !LeadYourPetUtility.ShouldPreserveMapExitMovement(link.Pet))
            {
                link.NextAutoInteractionTick = ticksGame + NextAutoInteractionDelay(playerControlled);
                List<LeadYourPetInteractionKind> pool = GetCachedAutoInteractionPool(link, ticksGame, playerControlled, canUseSingleDirection, sleepingMaster);
                if (pool.Count > 0)
                {
                    LeadYourPetInteractionKind kind = pool.RandomElement();
                    ApplyInteraction(link.Master, link.Pet, kind, LeadYourPetUtility.IsBidirectionalInteraction(kind));
                }
            }

            TryFeedPet(link, ticksGame);

            if (!playerControlled)
            {
                return;
            }

            if (canAutoInteract && canUseSingleDirection && ticksGame - link.LastObserveMemoryTick > 1800 && distance <= 4.9f)
            {
                int observers = CountNearbyHumanlikeObservers(link.Master, link.Pet, 4f, 2);
                if (observers >= 2 && LeadYourPetUtility.CanPerformInteraction(link.Pet, LeadYourPetInteractionKind.Observe, out _))
                {
                    link.LastObserveMemoryTick = ticksGame;
                    ApplyInteraction(link.Master, link.Pet, LeadYourPetInteractionKind.Observe, false);
                }
            }
        }

        private List<LeadYourPetInteractionKind> GetCachedAutoInteractionPool(LeashLink link, int ticksGame, bool playerControlled, bool canUseSingleDirection, bool masterSleeping)
        {
            if (link.CachedAutoInteractionPool == null)
            {
                link.CachedAutoInteractionPool = new List<LeadYourPetInteractionKind>();
            }

            if (!LeadYourPetRules.ShouldRefreshAutoInteractionPool(
                currentTick: ticksGame,
                lastPoolTick: link.LastAutoInteractionPoolTick,
                cacheIntervalTicks: 300,
                cacheValid: link.CachedHasValidAutoInteractionPool,
                cachedPlayerControlled: link.CachedPlayerControlled,
                playerControlled: playerControlled,
                cachedCanUseSingleDirection: link.CachedCanUseSingleDirectionInteractions,
                canUseSingleDirection: canUseSingleDirection,
                cachedMasterSleeping: link.CachedMasterSleeping,
                masterSleeping: masterSleeping))
            {
                return link.CachedAutoInteractionPool;
            }

            IEnumerable<LeadYourPetInteractionKind> interactionSource;
            if (!playerControlled || canUseSingleDirection)
            {
                interactionSource = LeadYourPetUtility.AutomaticPositiveInteractions;
            }
            else if (LeadYourPetRules.ShouldUseBidirectionalMouseEggAutoInteractions(playerControlled, canUseSingleDirection, masterSleeping))
            {
                interactionSource = LeadYourPetUtility.BidirectionalInteractions;
            }
            else
            {
                interactionSource = Enumerable.Empty<LeadYourPetInteractionKind>();
            }

            link.CachedAutoInteractionPool.Clear();
            link.CachedAutoInteractionPool.AddRange(
                LeadYourPetUtility.AvailableInteractionsFor(link.Master, link.Pet, interactionSource));
            link.CachedPlayerControlled = playerControlled;
            link.CachedCanUseSingleDirectionInteractions = canUseSingleDirection;
            link.CachedMasterSleeping = masterSleeping;
            link.CachedHasValidAutoInteractionPool = true;
            link.LastAutoInteractionPoolTick = ticksGame;
            return link.CachedAutoInteractionPool;
        }

        private int NextAutoInteractionDelay(bool playerControlled)
        {
            return playerControlled ? Rand.RangeInclusive(1800, 3600) : Rand.RangeInclusive(4200, 7200);
        }

        private int CountNearbyHumanlikeObservers(Pawn master, Pawn pet, float radius, int earlyExitCount)
        {
            if (master?.Map?.mapPawns?.AllPawnsSpawned == null || pet == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<Pawn> spawnedPawns = master.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawnedPawns.Count; i++)
            {
                Pawn pawn = spawnedPawns[i];
                if (pawn == null || pawn == master || !pawn.RaceProps.Humanlike)
                {
                    continue;
                }

                if (!pawn.Position.InHorDistOf(pet.Position, radius))
                {
                    continue;
                }

                count++;
                if (count >= earlyExitCount)
                {
                    return count;
                }
            }

            return count;
        }

        private bool HandleSleepingMaster(LeashLink link)
        {
            if (link.AnchorMode != LeashAnchorMode.None)
            {
                link.MasterSleepSuspended = false;
                return false;
            }

            if (LeadYourPetUtility.MasterSleeping(link.Master))
            {
                link.MasterSleepSuspended = true;
                return true;
            }

            if (link.MasterSleepSuspended)
            {
                link.MasterSleepSuspended = false;
                if (!LeadYourPetUtility.ShouldPreserveMapExitMovement(link.Pet))
                {
                    TightPullBack(link, link.Master.Position);
                }
            }

            return false;
        }
        private bool ShouldSuppressTravelAutoInteraction(LeashLink link)
        {
            MouseEggState state = link?.Kind == LeashLinkKind.MouseEggPet ? GetMouseEggState(link.Pet) : null;
            Lord masterLord = link?.Master?.GetLord();
            return LeadYourPetRules.ShouldSuppressTravelAutoInteraction(
                masterIsPlayer: link?.Master?.Faction == Faction.OfPlayer,
                petIsTravelStock: state != null && state.IsTravelStock,
                petSharesMasterLord: masterLord != null && link.Pet.GetLord() == masterLord);
        }


        private void ValidateAnchor(LeashLink link)
        {
            if (link.AnchorMode == LeashAnchorMode.Thing)
            {
                if (link.AnchorThing == null || !link.AnchorThing.Spawned || link.AnchorThing.Map != link.Pet.Map)
                {
                    link.AnchorMode = LeashAnchorMode.None;
                    link.AnchorThing = null;
                }
            }
            else if (link.AnchorMode == LeashAnchorMode.Cell)
            {
                if (!link.AnchorCell.IsValid || !link.AnchorCell.InBounds(link.Pet.Map))
                {
                    link.AnchorMode = LeashAnchorMode.None;
                    link.AnchorCell = IntVec3.Invalid;
                }
            }
        }

        private IntVec3 ResolveTargetCell(LeashLink link)
        {
            Thing followThing = LeadYourPetUtility.GetCurrentFollowThing(link);
            if (followThing != null)
            {
                return followThing.Position;
            }

            if (link.AnchorMode == LeashAnchorMode.Cell)
            {
                return link.AnchorCell;
            }

            return link.Master?.Position ?? IntVec3.Invalid;
        }

        private bool ShouldTargetMovementRefresh(LeashLink link)
        {
            if (link.AnchorMode == LeashAnchorMode.Thing)
            {
                return link.AnchorThing is Pawn anchorPawn && anchorPawn.pather != null && anchorPawn.pather.Moving;
            }

            if (link.AnchorMode == LeashAnchorMode.None)
            {
                return link.Master.pather != null && link.Master.pather.Moving;
            }

            return false;
        }

        private void AddDragBruise(Pawn pet)
        {
            if (pet.health == null)
            {
                return;
            }

            BodyPartRecord part = pet.health.hediffSet.GetNotMissingParts().RandomElementWithFallback();
            Hediff hediff = HediffMaker.MakeHediff(LeadYourPetDefOf.LeadYourPet_DragBruise, pet, part);
            pet.health.AddHediff(hediff);
        }

        private void MaintainDraggedSlow(Pawn pet, bool dragging)
        {
            if (pet?.health == null)
            {
                return;
            }

            Hediff existing = pet.health.hediffSet.GetFirstHediffOfDef(LeadYourPetDefOf.LeadYourPet_DragSlow);
            if (dragging)
            {
                if (existing == null)
                {
                    pet.health.AddHediff(HediffMaker.MakeHediff(LeadYourPetDefOf.LeadYourPet_DragSlow, pet));
                }
            }
            else if (existing != null)
            {
                pet.health.RemoveHediff(existing);
            }
        }

        private void TryFeedPet(LeashLink link, int ticksGame)
        {
            if (link.Master == null || link.Pet == null || !link.Master.Spawned || !link.Pet.Spawned)
            {
                return;
            }

            if (ticksGame - link.LastFeedAttemptTick < 120)
            {
                return;
            }
            link.LastFeedAttemptTick = ticksGame;

            if (!LeadYourPetUtility.HasFixedAnchor(link) && link.Kind == LeashLinkKind.MouseEggPet
                && !LeadYourPetUtility.CanUseSingleDirectionInteractions(link.Master, link.Pet))
            {
                return;
            }

            if (link.MouseEggDutyState == LeashedMouseEggDutyState.WaitForFeeding)
            {
                return;
            }

            Hediff malnutrition = link.Pet.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            bool hungryEnough = link.Pet.needs?.food != null && link.Pet.needs.food.CurLevelPercentage <= 0.35f;
            if (!hungryEnough && (malnutrition == null || malnutrition.Severity < 0.05f))
            {
                return;
            }

            if (!LeadYourPetUtility.TryFeedTarget(link.Master, link.Pet, out string _, out LeadYourPetInteractionKind interactionKind))
            {
                return;
            }

            if (!LeadYourPetUtility.HasFixedAnchor(link))
            {
                ApplyInteraction(link.Master, link.Pet, interactionKind, false);
            }
        }

        private void GentlePullBack(LeashLink link, IntVec3 targetCell, int ticksGame, bool forceImmediateUpdate)
        {
            if ((link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !LeadYourPetUtility.CanMouseEggMove(link.Pet))
            {
                return;
            }

            if (!targetCell.IsValid)
            {
                return;
            }

            IntVec3 moveCell = targetCell;
            if (link.AnchorMode == LeashAnchorMode.None && link.Master != null)
            {
                moveCell = link.Master.Position - link.Master.Rotation.FacingCell;
            }

            if (!LeadYourPetRules.ShouldIssueFollowPath(
                currentTick: ticksGame,
                lastFollowIssueTick: link.LastFollowIssueTick,
                minIntervalTicks: 30,
                forceImmediate: forceImmediateUpdate))
            {
                return;
            }

            if (moveCell.IsValid && moveCell.InBounds(link.Pet.Map) && moveCell.Standable(link.Pet.Map) && link.Pet.CanReach(moveCell, PathEndMode.OnCell, Danger.Deadly))
            {
                link.Pet.pather.StartPath(moveCell, PathEndMode.OnCell);
                link.LastFollowIssueTick = ticksGame;
            }
        }

        private void TightPullBack(LeashLink link, IntVec3 targetCell)
        {
            if (link.Pet == null || !link.Pet.Spawned || !targetCell.IsValid || !targetCell.InBounds(link.Pet.Map)
                || GodHandsCompat.IsGrabbed(link.Pet) || GodHandsCompat.IsGrabbed(link.Master)
                || (LeadYourPetUtility.GetCurrentFollowThing(link) is Pawn anchor && GodHandsCompat.IsGrabbed(anchor)))
            {
                return;
            }

            IntVec3 nearCell = CellFinder.RandomClosewalkCellNear(targetCell, link.Pet.Map, 1);
            if (!nearCell.IsValid)
            {
                nearCell = targetCell;
            }

            link.Pet.pather?.StopDead();
            link.Pet.Position = nearCell;
            link.Pet.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
        }

        private void ForceFollow(LeashLink link, int ticksGame, bool forceImmediateUpdate)
        {
            if (link == null || link.Pet == null || LeadYourPetUtility.ShouldPreserveMapExitMovement(link.Pet)
                || GodHandsCompat.IsGrabbed(link.Pet) || GodHandsCompat.IsGrabbed(link.Master))
            {
                return;
            }

            bool masterIsPlayerControlled = link.Master != null && link.Master.Faction == Faction.OfPlayer;
            if (!masterIsPlayerControlled && IsRunningFollowMasterJob(link))
            {
                link.Pet.jobs?.EndCurrentJob(JobCondition.Succeeded, false, true);
            }

            if ((link.Kind == LeashLinkKind.MouseEggPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !LeadYourPetUtility.CanMouseEggMove(link.Pet))
            {
                return;
            }

            bool shouldIssueFollowPath = LeadYourPetRules.ShouldIssueFollowPath(
                currentTick: ticksGame,
                lastFollowIssueTick: link.LastFollowIssueTick,
                minIntervalTicks: 30,
                forceImmediate: forceImmediateUpdate);
            if (!shouldIssueFollowPath)
            {
                return;
            }
            Thing followThing = LeadYourPetUtility.GetCurrentFollowThing(link);
            bool fixedAnchor = link.AnchorMode != LeashAnchorMode.None && !ShouldTargetMovementRefresh(link);
            if (LeadYourPetRules.ShouldKeepFixedAnchorStill(
                anchorIsFixed: fixedAnchor,
                distance: followThing != null
                    ? followThing.Position.DistanceTo(link.Pet.Position)
                    : link.AnchorCell.DistanceTo(link.Pet.Position),
                leashLength: LeadYourPetUtility.MaxLeashLength,
                dragging: link.IsDragging))
            {
                return;
            }

            if (fixedAnchor)
            {
                LocalTargetInfo anchorTarget = followThing != null
                    ? new LocalTargetInfo(followThing)
                    : new LocalTargetInfo(link.AnchorCell);
                PathEndMode mode = anchorTarget.HasThing ? PathEndMode.Touch : PathEndMode.OnCell;
                if (link.Pet.CurJobDef != LeadYourPetDefOf.LeadYourPet_FollowMaster
                    && link.Pet.CanReach(anchorTarget, mode, Danger.Deadly))
                {
                    Job returnJob = JobMaker.MakeJob(LeadYourPetDefOf.LeadYourPet_FollowMaster, anchorTarget);
                    if (link.Pet.jobs.TryTakeOrderedJob(returnJob, JobTag.Misc, false))
                    {
                        link.LastFollowIssueTick = ticksGame;
                    }
                }
                return;
            }

            if (followThing != null && link.Pet.CanReach(followThing, PathEndMode.Touch, Danger.Deadly))
            {
                if (LeadYourPetRules.ShouldIssueLeashFollowJob(
                    masterIsPlayerControlled: masterIsPlayerControlled,
                    followTargetIsMaster: followThing == link.Master,
                    alreadyFollowingSameMaster: IsRunningFollowMasterJob(link),
                    canReachTarget: true,
                    shouldIssueFollowPath: shouldIssueFollowPath))
                {
                    Job followJob = JobMaker.MakeJob(LeadYourPetDefOf.LeadYourPet_FollowMaster, link.Master);
                    followJob.playerForced = false;
                    if (link.Pet.jobs != null && link.Pet.jobs.TryTakeOrderedJob(followJob, JobTag.Misc, false))
                    {
                        link.LastFollowIssueTick = ticksGame;
                        return;
                    }
                }

                link.Pet.pather.StartPath(followThing, PathEndMode.Touch);
                link.LastFollowIssueTick = ticksGame;
                return;
            }

        }

        private bool IsRunningFollowMasterJob(LeashLink link)
        {
            return link?.Pet?.CurJob != null
                && link.Pet.CurJobDef == LeadYourPetDefOf.LeadYourPet_FollowMaster
                && link.Pet.CurJob.targetA.Thing == link.Master;
        }

        private void UpdateMouseEggDutyReport(LeashLink link)
        {
            if (link?.Pet?.CurJob == null || link.Kind != LeashLinkKind.MouseEggPet)
            {
                return;
            }

            if (link.MouseEggDutyState == LeashedMouseEggDutyState.FollowMaster)
            {
                if (link.Pet.CurJob.reportStringOverride == LeadYourPetUtility.GetMouseEggDutyReport(LeashedMouseEggDutyState.SleepAtLeash)
                    || link.Pet.CurJob.reportStringOverride == LeadYourPetUtility.GetMouseEggDutyReport(LeashedMouseEggDutyState.WaitForFeeding))
                {
                    link.Pet.CurJob.reportStringOverride = null;
                }

                return;
            }

            link.Pet.CurJob.reportStringOverride = LeadYourPetUtility.GetMouseEggDutyReport(link.MouseEggDutyState);
        }

        public void TightenMouseEggLeashes(Pawn master)
        {
            foreach (LeashLink link in GetLinksForMaster(master))
            {
                if (link.Kind != LeashLinkKind.MouseEggPet && link.Kind != LeashLinkKind.RatkinMotherBaby)
                {
                    continue;
                }

                if (!link.Pet.Spawned || link.Pet.Map != master.Map
                    || LeadYourPetUtility.ShouldPreserveMapExitMovement(link.Pet)
                    || LeadYourPetUtility.ShouldPreserveMapExitMovement(master))
                {
                    continue;
                }

                TightPullBack(link, master.Position);
            }
        }

        private void PullImmobileMouseEgg(LeashLink link, IntVec3 targetCell)
        {
            if (link.Pet == null || !link.Pet.Spawned || !targetCell.IsValid
                || GodHandsCompat.IsGrabbed(link.Pet) || GodHandsCompat.IsGrabbed(link.Master))
            {
                return;
            }

            IntVec3 pullCell = targetCell;
            if (link.AnchorMode == LeashAnchorMode.None && link.Master != null)
            {
                pullCell = link.Master.Position - (link.Master.Rotation.FacingCell * 2);
                if (!pullCell.InBounds(link.Master.Map) || !pullCell.Standable(link.Master.Map))
                {
                    pullCell = link.Master.Position - link.Master.Rotation.FacingCell;
                }

                if (!pullCell.InBounds(link.Master.Map) || !pullCell.Standable(link.Master.Map))
                {
                    pullCell = CellFinder.RandomClosewalkCellNear(link.Master.Position, link.Master.Map, 2);
                }
            }
            else if (!pullCell.InBounds(link.Pet.Map) || !pullCell.Standable(link.Pet.Map))
            {
                pullCell = CellFinder.RandomClosewalkCellNear(targetCell, link.Pet.Map, 1);
            }

            if (!pullCell.IsValid || pullCell == link.Pet.Position)
            {
                return;
            }

            link.Pet.pather?.StopDead();
            link.Pet.Position = pullCell;
            link.Pet.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
        }
    }
}
