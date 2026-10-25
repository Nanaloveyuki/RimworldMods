using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public bool StartLeash(Pawn master, Pawn pet, bool markAsYoungRatkinPet, bool playerForced = true)
        {
            string reasonKey;
            if (!LeadYourPetUtility.CanStartPlayerPetLeash(master, pet, markAsYoungRatkinPet, out reasonKey))
            {
                if (playerForced && master != null && master.Faction == Faction.OfPlayer && !reasonKey.NullOrEmpty())
                {
                    Messages.Message(reasonKey.Translate().Resolve(), master, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return StartLeashInternal(master, pet, markAsYoungRatkinPet ? LeashLinkKind.YoungRatkinPet : LeashLinkKind.NormalPet, playerForced);
        }

        public bool TryStartRatkinMotherLeash(Pawn mother, Pawn baby, bool showMessage = false)
        {
            string reasonKey;
            if (!LeadYourPetUtility.CanStartRatkinMotherLeash(mother, baby, out reasonKey))
            {
                if (showMessage && mother != null && mother.Faction == Faction.OfPlayer && !reasonKey.NullOrEmpty())
                {
                    Messages.Message(reasonKey.Translate().Resolve(), mother, MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            YoungRatkinState state = EnsureYoungRatkinState(baby);
            state.SpecialSource = LeadYourPetRules.ResolveMotherLeashSpecialSource(
                state.SpecialSource,
                LeadYourPetUtility.IsRatkinDisasterBeggarAdult(mother),
                LeadYourPetUtility.HasParentRelation(baby, mother));

            return StartLeashInternal(mother, baby, LeashLinkKind.RatkinMotherBaby, showMessage);
        }

        public void TryStopRatkinMotherLeash(Pawn baby)
        {
            LeashLink link = GetLinkForPet(baby);
            if (link != null && link.Kind == LeashLinkKind.RatkinMotherBaby)
            {
                EndLink(link, false);
            }
        }

        public void EndLeashForMaster(Pawn master, bool showMessage = true)
        {
            if (master == null) return;
            while (linksByMaster.TryGetValue(master, out List<LeashLink> masterLinks) && masterLinks.Count > 0)
            {
                EndLink(masterLinks[masterLinks.Count - 1], showMessage);
            }
        }

        public void EndLeashForPet(Pawn pet, bool showMessage = true)
        {
            LeashLink link = GetLinkForPet(pet);
            if (link != null)
            {
                EndLink(link, showMessage);
            }
        }

        public void ClearYoungRatkinPetState(Pawn pawn)
        {
            YoungRatkinState state = GetYoungRatkinState(pawn);
            if (state != null)
            {
                state.IsPet = false;
                state.CurrentMaster = null;
            }

            EndLeashForPet(pawn, false);
            PruneYoungRatkinStateIfEmpty(pawn);
        }

        private bool StartLeashInternal(Pawn master, Pawn pet, LeashLinkKind kind, bool playerForced)
        {
            if (master == null || pet == null || master == pet || master.Dead || pet.Dead)
            {
                return false;
            }

            LeashLink existing = GetLinkForPet(pet);
            if (LeadYourPetRules.ShouldRejectPlayerTakeoverOfNonPlayerLeash(
                hasExistingLink: existing != null && existing.Master != null,
                existingMasterIsDifferent: existing != null && existing.Master != master,
                existingMasterIsPlayer: existing?.Master?.Faction == Faction.OfPlayer,
                newMasterIsPlayer: master.Faction == Faction.OfPlayer))
            {
                return false;
            }

            EndLeashForPet(pet, false);
            ReleaseIncomingLeashCycle(master, pet);

            LeashLink link = new LeashLink
            {
                Master = master,
                Pet = pet,
                Kind = kind,
                CreatedTick = Find.TickManager.TicksGame,
                NextAutoInteractionTick = Find.TickManager.TicksGame + Rand.RangeInclusive(900, 1800),
                UpdateBucket = Mathf.Abs(pet.thingIDNumber) % 4,
                LastExpensiveUpdateTick = Find.TickManager.TicksGame,
                ForceImmediateUpdate = true,
                CachedAutoInteractionPool = new List<LeadYourPetInteractionKind>()
            };
            links.Add(link);
            RegisterLink(link);

            if (kind == LeashLinkKind.YoungRatkinPet)
            {
                YoungRatkinState state = EnsureYoungRatkinState(pet);
                state.IsPet = true;
                state.CurrentMaster = master;
                state.TravelEggAggressionHandled = false;
            }

            if (kind == LeashLinkKind.YoungRatkinPet || kind == LeashLinkKind.RatkinMotherBaby)
            {
                TryDropCarriedPawnAfterBecomingYoungRatkinPet(pet);
            }

            if (kind == LeashLinkKind.YoungRatkinPet)
            {
                HandleAggressiveVisitorYoungRatkinLeash(master, pet);
            }

            if (playerForced && master.Faction == Faction.OfPlayer)
            {
                string key = kind == LeashLinkKind.RatkinMotherBaby ? "LeadYourPet_Message_StartedMotherLeash" : "LeadYourPet_Message_StartedLeash";
                Messages.Message(key.Translate(master.LabelShort, pet.LabelShort).Resolve(), master, MessageTypeDefOf.PositiveEvent, false);
            }

            int ticksGame = Find.TickManager.TicksGame;
            link.LastMoodRefreshTick = ticksGame;
            LeadYourPetUtility.MaintainLeashMood(master, pet);
            if (master.Spawned && pet.Spawned && master.Map == pet.Map)
            {
                ForceFollow(link, ticksGame, true);
            }

            return true;
        }

        private bool ShouldBreak(LeashLink link)
        {
            Pawn master = link.Master;
            Pawn pet = link.Pet;
            bool allowImmobileYoungRatkin = (link.Kind == LeashLinkKind.YoungRatkinPet || link.Kind == LeashLinkKind.RatkinMotherBaby) && !LeadYourPetUtility.CanYoungRatkinMove(pet);
            YoungRatkinState state = link.Kind == LeashLinkKind.YoungRatkinPet ? GetYoungRatkinState(pet) : null;
            bool exemptTravelYoungRatkinCaravanLeash = link.Kind == LeashLinkKind.YoungRatkinPet
                && state != null
                && state.IsTravelStock
                && master.Faction != Faction.OfPlayer;
            return LeadYourPetRules.ShouldBreakLeashLifecycle(
                masterDestroyed: master.DestroyedOrNull(),
                petDestroyed: pet.DestroyedOrNull(),
                masterSpawned: master.Spawned,
                petSpawned: pet.Spawned,
                masterAndPetOnSameMap: master.Map == pet.Map,
                masterDead: master.Dead,
                petDead: pet.Dead,
                masterDowned: master.Downed,
                petDowned: pet.Downed,
                allowImmobilePetDowned: allowImmobileYoungRatkin,
                masterInMentalState: master.InMentalState,
                masterIsCaravanMember: master.IsCaravanMember(),
                exemptTravelYoungRatkinCaravanLeash: exemptTravelYoungRatkinCaravanLeash,
                playerMasterExitsMapOnArrival: master.Faction == Faction.OfPlayer && master.CurJob != null && master.CurJob.exitMapOnArrival,
                preservePlayerCaravanLeash: LeadYourPetUtility.IsPlayerCaravanMaster(master));
        }

        private void ReleaseIncomingLeashCycle(Pawn master, Pawn pet)
        {
            Pawn ancestor = master;
            for (int i = 0; ancestor != null && i <= links.Count; i++)
            {
                if (ancestor == pet)
                {
                    // The new direction wins; keep ordinary chains and other pets intact.
                    EndLeashForPet(master, false);
                    return;
                }

                ancestor = GetLinkForPet(ancestor)?.Master;
            }
        }

        private void EndLink(LeashLink link, bool showMessage)
        {
            links.Remove(link);
            UnregisterLink(link);
            StopInteractionAnimation(link.Pet, finalizeTeleport: false);
            if (link.Pet.CurJob != null && link.Pet.CurJobDef == LeadYourPetDefOf.LeadYourPet_FollowMaster)
            {
                link.Pet.pather?.StopDead();
                link.Pet.jobs?.EndCurrentJob(JobCondition.InterruptForced, false);
            }
            LeadYourPetUtility.ClearLeashMood(link.Master, link.Pet);
            MaintainDraggedSlow(link.Pet, false);
            link.CachedHasValidAutoInteractionPool = false;

            if (link.Kind == LeashLinkKind.YoungRatkinPet || link.Kind == LeashLinkKind.RatkinMotherBaby)
            {
                YoungRatkinState state = GetYoungRatkinState(link.Pet);
                if (state != null && state.CurrentMaster == link.Master)
                {
                    if (state.IsTravelStock && link.Pet.IsPrisonerOfColony)
                    {
                        ClearTravelStock(link.Pet);
                    }

                    state.CurrentMaster = null;
                    state.IsPet = false;
                }
            }

            PruneYoungRatkinStateIfEmpty(link.Pet);

            if (showMessage && link.Master != null && link.Master.Faction == Faction.OfPlayer)
            {
                string key = link.Kind == LeashLinkKind.RatkinMotherBaby ? "LeadYourPet_Message_EndedMotherLeash" : "LeadYourPet_Message_EndedLeash";
                Messages.Message(key.Translate(link.Master.LabelShort, link.Pet.LabelShort).Resolve(), link.Master, MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void HandleAggressiveVisitorYoungRatkinLeash(Pawn master, Pawn pet)
        {
            if (!LeadYourPetUtility.ShouldTreatVisitorYoungRatkinPetLeashAsAggressive(master, pet))
            {
                return;
            }

            YoungRatkinState state = EnsureYoungRatkinState(pet);
            if (state.TravelEggAggressionHandled)
            {
                return;
            }

            state.TravelEggAggressionHandled = true;
            LeadYourPetUtility.ClearRatkinDisasterVisitorCover(pet);

            int hostileCount;
            SinglePawnBuffer.Clear();
            SinglePawnBuffer.Add(pet);
            if (LeadYourPetUtility.TryMakeRatkinDisasterVisitorsHostile(SinglePawnBuffer, out hostileCount))
            {
                SinglePawnBuffer.Clear();
                return;
            }
            SinglePawnBuffer.Clear();

            Lord lord = pet.GetLord();
            Faction faction = pet.Faction;
            lord?.Notify_PawnLost(pet, PawnLostCondition.ForcedByPlayerAction);

            if (faction == null || Faction.OfPlayer == null || faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            LeadYourPetUtility.MakeRatkinDisasterFactionHostileToPlayer(faction, explicitDriveAway: true);
            if (!faction.HostileTo(Faction.OfPlayer))
            {
                Faction.OfPlayer.TryAffectGoodwillWith(
                    faction,
                    Faction.OfPlayer.GoodwillToMakeHostile(faction),
                    canSendMessage: true,
                    canSendHostilityLetter: true,
                    HistoryEventDefOf.AttackedCaravan,
                    pet);
                faction.TryAffectGoodwillWith(
                    Faction.OfPlayer,
                    faction.GoodwillToMakeHostile(Faction.OfPlayer),
                    canSendMessage: false,
                    canSendHostilityLetter: false,
                    HistoryEventDefOf.AttackedCaravan,
                    pet);
            }
        }

        private void RegisterLink(LeashLink link)
        {
            if (link == null || link.Pet == null || link.Master == null)
            {
                return;
            }

            link.UpdateBucket = Mathf.Abs(link.Pet.thingIDNumber) % 4;
            linkByPet[link.Pet] = link;
            if (!linksByMaster.TryGetValue(link.Master, out List<LeashLink> masterLinks))
            {
                masterLinks = new List<LeashLink>();
                linksByMaster[link.Master] = masterLinks;
            }

            masterLinks.Add(link);
        }

        private void UnregisterLink(LeashLink link)
        {
            if (link == null)
            {
                return;
            }

            if (link.Pet != null)
            {
                linkByPet.Remove(link.Pet);
            }

            if (link.Master != null && linksByMaster.TryGetValue(link.Master, out List<LeashLink> masterLinks))
            {
                masterLinks.Remove(link);
                if (masterLinks.Count == 0)
                {
                    linksByMaster.Remove(link.Master);
                }
            }
        }
    }
}
