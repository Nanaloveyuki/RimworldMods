using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public void AddPlayerLeashedPetsToFormingCaravan(Lord lord)
        {
            if (lord == null || !(lord.LordJob is LordJob_FormAndSendCaravan) || lord.ownedPawns == null)
            {
                return;
            }

            LordJob_FormAndSendCaravan caravanJob = (LordJob_FormAndSendCaravan)lord.LordJob;
            if (caravanJob.downedPawns == null)
            {
                caravanJob.downedPawns = new List<Pawn>();
            }

            List<Pawn> masters = new List<Pawn>(lord.ownedPawns);
            List<Pawn> pets = new List<Pawn>();
            for (int i = 0; i < masters.Count; i++)
            {
                Pawn master = masters[i];
                if (!LeadYourPetUtility.IsPlayerCaravanMaster(master))
                {
                    continue;
                }

                List<LeashLink> masterLinks = GetLinksForMaster(master);
                for (int j = 0; j < masterLinks.Count; j++)
                {
                    Pawn pet = masterLinks[j]?.Pet;
                    if (!CanAddPlayerLeashedPawnToCaravan(master, pet)
                        || lord.ownedPawns.Contains(pet)
                        || caravanJob.downedPawns.Contains(pet)
                        || pets.Contains(pet))
                    {
                        continue;
                    }

                    pets.Add(pet);
                }
            }

            for (int i = 0; i < pets.Count; i++)
            {
                CaravanFormingUtility.LateJoinFormingCaravan(pets[i], lord);
            }
        }

        public IEnumerable<Pawn> IncludePlayerLeashedPawnsInCaravan(IEnumerable<Pawn> pawns, Faction faction)
        {
            if (pawns == null || faction != Faction.OfPlayer)
            {
                return pawns;
            }

            List<Pawn> selectedPawns = pawns.ToList();
            List<Pawn> masters = new List<Pawn>(selectedPawns);
            for (int i = 0; i < masters.Count; i++)
            {
                Pawn master = masters[i];
                if (!LeadYourPetUtility.IsPlayerCaravanMaster(master))
                {
                    continue;
                }

                List<LeashLink> masterLinks = GetLinksForMaster(master);
                for (int j = 0; j < masterLinks.Count; j++)
                {
                    Pawn pet = masterLinks[j]?.Pet;
                    if (CanAddPlayerLeashedPawnToCaravan(master, pet) && !selectedPawns.Contains(pet))
                    {
                        selectedPawns.Add(pet);
                    }
                }
            }

            return selectedPawns;
        }

        public Caravan AddPlayerLeashedPetsToJoinableCaravan(Pawn master)
        {
            if (master == null || !master.Spawned || !LeadYourPetUtility.IsPlayerCaravanMaster(master))
            {
                return null;
            }

            Caravan caravan = CaravanExitMapUtility.FindCaravanToJoinFor(master);
            if (caravan == null)
            {
                return null;
            }

            List<LeashLink> masterLinks = new List<LeashLink>(GetLinksForMaster(master));
            for (int i = 0; i < masterLinks.Count; i++)
            {
                Pawn pet = masterLinks[i]?.Pet;
                if (!CanAddPlayerLeashedPawnToCaravan(master, pet) || caravan.ContainsPawn(pet))
                {
                    continue;
                }

                Lord petLord = pet.GetLord();
                if (petLord != null)
                {
                    petLord.Notify_PawnLost(pet, PawnLostCondition.ForcedToJoinOtherLord);
                }

                caravan.AddPawn(pet, true);
            }

            return caravan;
        }

        public void EndLeashesForCaravan(Caravan caravan)
        {
            if (caravan == null)
            {
                return;
            }

            List<LeashLink> snapshot = new List<LeashLink>(links);
            for (int i = 0; i < snapshot.Count; i++)
            {
                LeashLink link = snapshot[i];
                if (link != null && (caravan.ContainsPawn(link.Master) || caravan.ContainsPawn(link.Pet)))
                {
                    EndLink(link, false);
                }
            }
        }

        private bool CanAddPlayerLeashedPawnToCaravan(Pawn master, Pawn pet)
        {
            return master != null
                && pet != null
                && master != pet
                && !pet.DestroyedOrNull()
                && !pet.Dead
                && pet.CarriedBy != master
                && master.MapHeld != null
                && pet.MapHeld == master.MapHeld;
        }

        public void TryAssignTravelMouseEggs(Lord lord)
        {
            if (lord == null || lord.Map == null || lord.faction == null || lord.faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            if (!processedTravelLords.Add(lord.loadID))
            {
                return;
            }

            if (TryAssignMouseDisasterTravelMouseEggs(lord))
            {
                return;
            }

            if (!(lord.LordJob is LordJob_TradeWithColony) && !(lord.LordJob is LordJob_VisitColony))
            {
                return;
            }

            List<Pawn> adults = lord.ownedPawns.Where(LeadYourPetUtility.IsNonHostileAdultVisitor).ToList();
            if (adults.Count == 0)
            {
                return;
            }

            bool traderLord = lord.LordJob is LordJob_TradeWithColony;
            bool visitorLord = lord.LordJob is LordJob_VisitColony;
            Pawn trader = TraderCaravanUtility.FindTrader(lord);
            int remainingMouseEggs = LeadYourPetRules.ResolveOrdinaryTravelMouseEggTargetCount(
                traderLord: traderLord,
                visitorLord: visitorLord,
                hasEligibleAdults: adults.Count > 0,
                configuredCount: LeadYourPetUtility.OrdinaryTravelRatkinYoungCount,
                visitorsLeadRatkinYoung: LeadYourPetUtility.VisitorsLeadRatkinYoung);
            foreach (Pawn adult in adults.InRandomOrder())
            {
                bool hadCarriedEgg = adult?.carryTracker?.CarriedThing is Pawn carriedEgg
                    && LeadYourPetUtility.CanBePetMouseEgg(carriedEgg, out _);
                if (TryRegisterCarriedTravelMouseEgg(lord, adult, trader, traderLord))
                {
                    if (remainingMouseEggs > 0)
                    {
                        remainingMouseEggs--;
                    }

                    continue;
                }

                if (LeadYourPetRules.ShouldSkipRandomTravelMouseEggFallback(
                    hadCarriedMouseEgg: hadCarriedEgg,
                    carriedEggRegistrationSucceeded: false))
                {
                    continue;
                }

                if (remainingMouseEggs <= 0)
                {
                    continue;
                }

                Pawn egg = LeadYourPetUtility.GenerateMouseEggPawn(lord.Map);
                if (egg == null)
                {
                    continue;
                }

                IntVec3 cell = CellFinder.RandomClosewalkCellNear(adult.Position, lord.Map, 1);
                GenSpawn.Spawn(egg, cell, lord.Map);
                lord.AddPawn(egg);

                bool sellable = traderLord || adult.TraderKind != null;
                MarkAsTravelMouseEgg(egg, adult, trader, sellable);
                StartLeashInternal(adult, egg, LeashLinkKind.MouseEggPet, false);
                remainingMouseEggs--;
            }
        }

        private bool TryRegisterCarriedTravelMouseEgg(Lord lord, Pawn carrier, Pawn trader, bool traderLord)
        {
            Pawn carriedEgg = carrier?.carryTracker?.CarriedThing as Pawn;
            if (carriedEgg == null || GetLinkForPet(carriedEgg) != null || !LeadYourPetUtility.CanBePetMouseEgg(carriedEgg, out _))
            {
                return false;
            }

            bool dropSucceeded = carrier.carryTracker.TryDropCarriedThing(carrier.Position, ThingPlaceMode.Near, out Thing droppedThing);
            Pawn egg = droppedThing as Pawn;
            bool sellable = traderLord || carrier.TraderKind != null;
            if (!LeadYourPetRules.ShouldRegisterDroppedTravelMouseEgg(
                hadCarriedMouseEgg: true,
                dropSucceeded: dropSucceeded,
                droppedPawnValid: egg != null && egg.Spawned && egg.Map == carrier.Map))
            {
                MarkAsTravelMouseEgg(carriedEgg, carrier, trader, sellable);
                return true;
            }

            MarkAsTravelMouseEgg(egg, carrier, trader, sellable);
            StartLeashInternal(carrier, egg, LeashLinkKind.MouseEggPet, false);

            return true;
        }

        public void TryRestoreTravelMouseEggLeashAfterCarry(Pawn carrier, Pawn egg)
        {
            if (carrier == null || egg == null || GetLinkForPet(egg) != null
                || LeadYourPetUtility.IsNonPlayerTravelDeparture(carrier))
            {
                return;
            }

            MouseEggState state = GetMouseEggState(egg);
            if (state == null || !state.IsPet || !state.IsTravelStock || !LeadYourPetUtility.CanBePetMouseEgg(egg, out _))
            {
                return;
            }

            state.CurrentMaster = carrier;
            if (carrier.Spawned && egg.Spawned && carrier.Map == egg.Map)
            {
                StartLeashInternal(carrier, egg, LeashLinkKind.MouseEggPet, false);
            }
        }

        private bool TryAssignMouseDisasterTravelMouseEggs(Lord lord)
        {
            if (lord == null || lord.Map == null || lord.ownedPawns == null)
            {
                return false;
            }

            Pawn trader = lord.ownedPawns.FirstOrDefault(pawn =>
                pawn != null &&
                pawn.Spawned &&
                !pawn.Dead &&
                LeadYourPetUtility.IsMouseDisasterTravelTrader(pawn));
            if (trader == null)
            {
                return false;
            }

            List<Pawn> children = lord.ownedPawns.Where(pawn =>
                pawn != null &&
                pawn.Spawned &&
                !pawn.Dead &&
                LeadYourPetUtility.IsMouseDisasterTravelPawn(pawn)).ToList();
            if (children.Count == 0)
            {
                CellRect searchRect = CellRect.CenteredOn(trader.Position, 12);
                foreach (IntVec3 cell in searchRect)
                {
                    if (!cell.InBounds(lord.Map))
                    {
                        continue;
                    }

                    List<Thing> thingList = cell.GetThingList(lord.Map);
                    for (int i = 0; i < thingList.Count; i++)
                    {
                        Pawn pawn = thingList[i] as Pawn;
                        if (pawn == null)
                        {
                            continue;
                        }

                        if (LeadYourPetRules.IsEligibleNearbyMouseDisasterTravelChild(
                            isTravelChild: LeadYourPetUtility.IsMouseDisasterTravelPawn(pawn),
                            isSpawned: pawn.Spawned,
                            isDead: pawn.Dead,
                            sameFactionAsTraderLord: pawn.Faction == lord.faction,
                            hasNoLord: pawn.GetLord() == null,
                            isNearTrader: pawn.Position.InHorDistOf(trader.Position, 12f)))
                        {
                            children.Add(pawn);
                        }
                    }
                }
            }

            if (children.Count == 0)
            {
                return false;
            }

            bool sellAsPrisoner = lord.LordJob is LordJob_TradeWithColony;
            Pawn tradeLead = sellAsPrisoner ? TraderCaravanUtility.FindTrader(lord) ?? trader : trader;
            foreach (Pawn child in children)
            {
                if (sellAsPrisoner)
                {
                    LeadYourPetUtility.TryPrepareMouseDisasterTradablePrisoner(child, lord.faction);
                }

                MarkAsTravelMouseEgg(child, trader, tradeLead, sellAsPrisoner, preserveOwnership: true);
                MouseEggSpecialSource specialSource = LeadYourPetRules.ResolveTravelMouseEggSpecialSource(
                    LeadYourPetUtility.IsMouseDisasterTravelTrader(trader),
                    lord.LordJob is LordJob_DefendPoint,
                    sellAsPrisoner);
                if (specialSource != MouseEggSpecialSource.None)
                {
                    SetMouseEggSpecialSource(child, specialSource);
                }

                if (!TryStartRatkinMotherLeash(trader, child, false))
                {
                    StartLeashInternal(trader, child, LeashLinkKind.MouseEggPet, false);
                }
            }

            return true;
        }

        private void TryMoveBoughtPrisonerNearPrisonBed(Pawn pawn)
        {
            if (pawn.MapHeld == null || !pawn.Spawned)
            {
                return;
            }

            Building_Bed prisonBed = pawn.MapHeld.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().FirstOrDefault(x => x.ForPrisoners);
            if (prisonBed == null)
            {
                return;
            }

            IntVec3 cell = CellFinder.RandomClosewalkCellNear(prisonBed.Position, prisonBed.Map, 1);
            pawn.DeSpawnOrDeselect();
            GenSpawn.Spawn(pawn, cell, prisonBed.Map);
        }
    }
}
