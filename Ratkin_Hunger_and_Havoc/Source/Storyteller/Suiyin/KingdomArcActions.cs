using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.EventMgr;
using RimWorld;
using RimWorld.Planet;
using Oxygendsl.Mod.Events;
using Verse;
using WorldCaravan = RimWorld.Planet.Caravan;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class KingdomArcActions
    {
        internal static KingdomArcProgress Progress => KingdomArcRuntime.State?.Progress;
        internal static int Now => Find.TickManager.TicksGame;
        internal static bool Narrator => RHAH_EndingRuntime.IsNarrator(Find.Storyteller?.def?.defName);
        internal static int AddTicks(int tick, int duration) => (int)Math.Min(int.MaxValue, (long)tick + duration);
        internal static Map Home
        {
            get
            {
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++) if (maps[i].uniqueID == Progress.HomeId && maps[i].IsPlayerHome) return maps[i];
                return null;
            }
        }

        internal static bool Start()
        {
            KingdomArcProgress p = Progress;
            if (p == null || p.Stage != KingdomStage.None) return p != null;
            Map home = Find.AnyPlayerHomeMap;
            if (home == null || !home.Tile.Valid) return false;
            int chain = OxygenEventChainHost.Runtime.Start(RHAH_EventChainProvider.ProviderKey, KingdomCollaborationDefinition.DefinitionId,
                home.uniqueID, home.Tile, Now, -1, "", 0, AddTicks(Now, 1));
            if (chain == 0) return false;
            p.ChainId = chain;
            p.HomeId = home.uniqueID;
            p.Move(KingdomStage.Parcel);
            WorldObject_RHAH_KingdomSite court = Place(KingdomSiteKind.Hall);
            if (court == null)
            {
                p.Failure = KingdomFailure.Unavailable;
                p.Move(KingdomStage.InquiryClose);
                p.Due = AddTicks(Now, GenDate.TicksPerDay * 2);
                return true;
            }
            p.CourtSiteId = court.ID;
            p.Due = AddTicks(Now, Math.Max(GenDate.TicksPerDay * 2, CourierTicks(court.Tile, home.Tile)));
            return true;
        }

        internal static bool NeedsClock(KingdomArcProgress p)
        {
            return p.PendingLetter || p.ConsentDue >= 0 || p.Due >= 0 || p.Deadline >= 0 || (p.Stage == KingdomStage.Response && !p.ResultSent) || p.Stage == KingdomStage.Action || p.ActorId != 0;
        }

        internal static void Wake()
        {
            KingdomArcProgress p = Progress;
            if (p?.ChainId > 0)
                OxygenEventChainHost.Runtime?.SetStage(RHAH_EventChainProvider.ProviderKey, p.ChainId, 0, -1, "", AddTicks(Now, 1));
        }

        internal static void Commission(KingdomStage stage, int tick)
        {
            KingdomArcProgress p = Progress;
            p.Move(stage);
            p.Due = AddTicks(tick, GenDate.TicksPerDay * 2);
            p.Deadline = -1;
            p.Progress = 0;
            p.ResultSent = false;
            KingdomArcRuntime.State.Act = stage >= KingdomStage.Routes ? 4 : stage >= KingdomStage.Envoy ? 3 : 2;
        }

        internal static void Advance(int tick)
        {
            KingdomArcProgress p = Progress;
            if (p == null || p.Stage == KingdomStage.None || p.Stage == KingdomStage.Complete) return;
            if (p.PendingLetter && Narrator)
            {
                p.PendingLetter = false;
                KingdomArcRuntime.SendCurrent();
            }
            DeliverConsent(p, tick);
            if (p.Stage == KingdomStage.GuardTransit && p.Deadline >= 0 && tick >= p.Deadline && (p.Due < 0 || p.Due > p.Deadline))
            {
                p.GuardResult = KingdomNodeResult.Unknown;
                Commission(KingdomStage.Routes, tick);
                return;
            }
            if (p.Stage == KingdomStage.Response)
            {
                if (Narrator && !p.ResultSent)
                {
                    p.ResultSent = true;
                    KingdomArcRuntime.State.Act = 5;
                    KingdomArcRuntime.SendCurrent();
                }
                return;
            }
            if (p.Stage == KingdomStage.Action)
            {
                AdvanceAction(p, tick);
                return;
            }
            if (p.Stage == KingdomStage.Hall || p.Stage == KingdomStage.Charity)
            {
                Work(p, tick);
                if (p.Deadline >= 0 && tick >= p.Deadline)
                {
                    if (p.Stage == KingdomStage.Charity)
                    {
                        p.CharityInterrupted = p.Progress > 0 && !p.CharityObserved;
                        p.CharityResult = p.CharityInterrupted ? KingdomNodeResult.Partial : KingdomNodeResult.Expired;
                        if (p.Progress > 0 && !p.CharityObserved && !HasMaterial(p, KingdomMaterialKind.CharityReport))
                        {
                            WorldCaravan observer = FindActorCaravan(p.ActorId);
                            if (!KingdomMaterials.Acquire(p, KingdomMaterialKind.CharityReport, "Observation", Home, observer)) return;
                        }
                        Commission(KingdomStage.Guard, tick);
                    }
                    else
                    {
                        p.Failure = KingdomFailure.Expired;
                        p.HallResult = KingdomNodeResult.Expired;
                        Commission(KingdomStage.InquiryClose, tick);
                    }
                    RemoveSite(p.SiteId);
                    return;
                }
            }
            bool acceptedMail = p.Stage == KingdomStage.Parcel || p.Stage == KingdomStage.InterviewTransit || p.Stage == KingdomStage.GuardTransit;
            if (p.Due < 0 || tick < p.Due || (!acceptedMail && !Narrator)) return;
            if (Home == null) return;
            p.Due = -1;
            switch (p.Stage)
            {
                case KingdomStage.Parcel:
                    if (!HasMaterial(p, KingdomMaterialKind.ReceiptA) && !Give(KingdomMaterialKind.ReceiptA, "Palace")) { p.Due = AddTicks(tick, GenDate.TicksPerHour); return; }
                    if (!HasMaterial(p, KingdomMaterialKind.ReceiptB) && !Give(KingdomMaterialKind.ReceiptB, "Palace")) { p.Due = AddTicks(tick, GenDate.TicksPerHour); return; }
                    p.Move(KingdomStage.Receipts);
                    break;
                case KingdomStage.InterviewTransit:
                    if (p.Clerk == null) p.Clerk = GeneratePerson();
                    if (p.Clerk == null)
                    {
                        p.Failure = KingdomFailure.Unavailable;
                        Commission(KingdomStage.InquiryClose, tick);
                        return;
                    }
                    if (!Give(KingdomMaterialKind.Statement, "Clerk")) { p.Due = AddTicks(tick, GenDate.TicksPerHour); return; }
                    p.StatementReceived = true;
                    p.PrivateConsent = true;
                    p.Move(KingdomStage.Interview);
                    break;
                case KingdomStage.Hall:
                    WorldObject_RHAH_KingdomSite hall = Place(KingdomSiteKind.Hall);
                    if (hall == null)
                    {
                        p.Failure = KingdomFailure.Unavailable;
                        Commission(KingdomStage.InquiryClose, tick);
                        return;
                    }
                    p.SiteId = hall.ID;
                    p.Deadline = AddTicks(tick, GenDate.TicksPerDay * 7);
                    break;
                case KingdomStage.Envoy:
                    if (p.Attention > 0) SpawnEnvoy();
                    break;
                case KingdomStage.Charity:
                    WorldObject_RHAH_KingdomSite charity = Place(KingdomSiteKind.Charity);
                    if (charity == null)
                    {
                        Commission(KingdomStage.Guard, tick);
                        return;
                    }
                    p.SiteId = charity.ID;
                    p.CharityStart = AddTicks(tick, GenDate.TicksPerDay * 5);
                    p.CharityEnd = AddTicks(p.CharityStart, GenDate.TicksPerDay);
                    p.Deadline = p.CharityEnd;
                    break;
                case KingdomStage.GuardTransit:
                    if (!p.GuardPetition && !Give(KingdomMaterialKind.GuardReceipt, "Guard")) { p.Due = AddTicks(tick, GenDate.TicksPerHour); return; }
                    p.GuardReceived = !p.GuardPetition;
                    p.GuardResult = KingdomNodeResult.Completed;
                    Commission(KingdomStage.Routes, tick);
                    return;
            }
            p.ResultSent = true;
            if (Narrator) KingdomArcRuntime.SendCurrent();
            else p.PendingLetter = true;
        }

        internal static bool Choose(KingdomStage stage, int revision, string action)
        {
            KingdomArcProgress p = Progress;
            if (!KingdomArcRuntime.CanAct || p == null || !p.Matches(stage, revision) || !p.ResultSent) return false;
            if (stage != KingdomStage.Hall && stage != KingdomStage.Charity && stage != KingdomStage.Action && !Narrator) return false;
            switch (stage)
            {
                case KingdomStage.Receipts:
                    if (action == "Stop") Commission(KingdomStage.InquiryClose, Now);
                    else if (action == "Formal" || action == "Private")
                    {
                        p.Formal = action == "Formal";
                        p.Attention = p.Formal ? 1 : 0;
                        p.Move(KingdomStage.InterviewTransit);
                        p.ResultSent = false;
                        WorldObject_RHAH_KingdomSite clerkSite = Place(KingdomSiteKind.Meeting);
                        if (clerkSite == null) { p.Failure = KingdomFailure.Unavailable; Commission(KingdomStage.InquiryClose, Now); break; }
                        p.ClerkSiteId = clerkSite.ID;
                        p.Due = AddTicks(Now, CourierTicks(Home.Tile, clerkSite.Tile) * 2);
                    }
                    else return false;
                    break;
                case KingdomStage.Interview:
                    if (action == "Stop") Commission(KingdomStage.InquiryClose, Now);
                    else if (action == "Verify") Commission(KingdomStage.Hall, Now);
                    else if (action == "Named")
                    {
                        if (!AvailableClerk(p)) return false;
                        if (!RequestConsent(KingdomClerkReply.NamedPublication)) return false;
                        Wake();
                        return true;
                    }
                    else return false;
                    break;
                case KingdomStage.Hall:
                    if (action != "Decline") return false;
                    p.Failure = KingdomFailure.Cancelled;
                    p.HallResult = KingdomNodeResult.Declined;
                    RemoveSite(p.SiteId);
                    Commission(KingdomStage.InquiryClose, Now);
                    break;
                case KingdomStage.InquiryClose:
                    if (action != "Continue") return false;
                    Commission(KingdomStage.Envoy, Now);
                    break;
                case KingdomStage.Envoy:
                    if (action == "Handover")
                    {
                        Window_RHAH_KingdomMaterials.Open(stage, revision, KingdomMaterialSelectionPurpose.Handover);
                        return true;
                    }
                    else if (action != "Keep" && action != "Refuse") return false;
                    p.EnvoyResult = action == "Refuse" ? KingdomNodeResult.Declined : KingdomNodeResult.Completed;
                    ReleaseEnvoy();
                    Commission(KingdomStage.Charity, Now);
                    break;
                case KingdomStage.Charity:
                    if (action != "Decline") return false;
                    p.CharityInterrupted = p.Progress > 0 && !p.CharityObserved;
                    if (p.CharityInterrupted && !HasMaterial(p, KingdomMaterialKind.CharityReport) && !KingdomMaterials.Acquire(p, KingdomMaterialKind.CharityReport, "Observation", Home, FindActorCaravan(p.ActorId))) return false;
                    p.CharityResult = p.CharityInterrupted ? KingdomNodeResult.Partial : KingdomNodeResult.Declined;
                    RemoveSite(p.SiteId);
                    Commission(KingdomStage.Guard, Now);
                    break;
                case KingdomStage.Guard:
                    if (action == "Decline")
                    {
                        p.GuardResult = KingdomNodeResult.Declined;
                        Commission(KingdomStage.Routes, Now);
                    }
                    else if (action == "Receipt" || action == "Petition")
                    {
                        p.GuardPetition = action == "Petition";
                        p.Move(KingdomStage.GuardTransit);
                        p.ResultSent = false;
                        WorldObject_RHAH_KingdomSite source = Place(KingdomSiteKind.Hall);
                        if (source == null)
                        {
                            p.Failure = KingdomFailure.Unavailable;
                            Commission(KingdomStage.Routes, Now);
                        }
                        else
                        {
                            int travel = CourierTicks(Home.Tile, source.Tile);
                            p.Due = AddTicks(Now, travel * 2);
                            p.Deadline = AddTicks(Now, GenDate.TicksPerDay * 7);
                            RemoveSite(source.ID);
                        }
                    }
                    else return false;
                    break;
                case KingdomStage.Routes:
                    if (action == "Publish")
                    {
                        Window_RHAH_KingdomMaterials.Open(stage, revision, KingdomMaterialSelectionPurpose.Publication);
                        return true;
                    }
                    if (action == "Named")
                    {
                        bool requested = RequestConsent(KingdomClerkReply.NamedPublication);
                        if (requested) Wake();
                        return requested;
                    }
                    if (action == "Protect" && !p.TransferConsent)
                    {
                        bool requested = RequestConsent(KingdomClerkReply.SafeTransfer);
                        if (requested) Wake();
                        return requested;
                    }
                    if (action == "Pause")
                    {
                        p.Route = KingdomRoute.Pause;
                        p.Finish(KingdomFailure.Cancelled);
                    }
                    else if (action == "Repair" || action == "Protect")
                    {
                        if (!BeginRoute(action)) return false;
                    }
                    else return false;
                    break;
                case KingdomStage.Action:
                    if (action != "Cancel") return false;
                    p.Finish(KingdomFailure.Cancelled);
                    break;
                case KingdomStage.Response:
                    if (action == "Reconsider" && p.Outcome != KingdomOutcome.Success)
                    {
                        p.Route = KingdomRoute.None;
                        p.Outcome = KingdomOutcome.None;
                        p.Failure = KingdomFailure.None;
                        Commission(KingdomStage.Routes, Now);
                    }
                    else if (action == "Close") p.Move(KingdomStage.Complete);
                    else return false;
                    break;
                default: return false;
            }
            Wake();
            return true;
        }

        internal static bool Handover(KingdomStage stage, int revision, List<Thing> selected)
        {
            KingdomArcProgress p = Progress;
            if (!CanSelect(stage, revision, KingdomStage.Envoy) || !KingdomMaterials.ValidateSelection(Home, selected)) return false;
            Verse.Pawn recipient = p.Envoy;
            if (recipient == null)
            {
                recipient = GeneratePerson();
                if (recipient == null) return false;
            }
            else if (!recipient.Spawned || recipient.Map != Home || recipient.Dead || recipient.Downed) return false;
            List<Thing> moved = new List<Thing>();
            for (int i = 0; i < selected.Count; i++)
            {
                Thing item = selected[i];
                if (item.Spawned) item.DeSpawn();
                else item.holdingOwner?.Remove(item);
                if (!recipient.inventory.innerContainer.TryAdd(item))
                {
                    KingdomMaterials.Deliver(item, Home);
                    for (int j = 0; j < moved.Count; j++)
                    {
                        recipient.inventory.innerContainer.Remove(moved[j]);
                        KingdomMaterials.Deliver(moved[j], Home);
                    }
                    return false;
                }
                moved.Add(item);
            }
            for (int i = 0; i < moved.Count; i++)
            {
                Comp_RHAH_KingdomMaterial comp = moved[i].TryGetComp<Comp_RHAH_KingdomMaterial>();
                for (int j = 0; j < p.Materials.Count; j++) if (p.Materials[j].Number == comp.Number) p.Materials[j].Envoy = true;
            }
            p.Envoy = recipient;
            p.EnvoyHandedOver = true;
            p.EnvoyResult = KingdomNodeResult.Completed;
            if (!Give(KingdomMaterialKind.EnvoyReceipt, "Envoy"))
            {
                for (int i = 0; i < moved.Count; i++)
                {
                    recipient.inventory.innerContainer.Remove(moved[i]);
                    KingdomMaterials.Deliver(moved[i], Home);
                    Comp_RHAH_KingdomMaterial comp = moved[i].TryGetComp<Comp_RHAH_KingdomMaterial>();
                    for (int j = 0; j < p.Materials.Count; j++) if (p.Materials[j].Number == comp.Number) p.Materials[j].Envoy = false;
                }
                p.EnvoyHandedOver = false;
                p.EnvoyResult = KingdomNodeResult.None;
                return false;
            }
            ReleaseEnvoy();
            Commission(KingdomStage.Charity, Now);
            Wake();
            return true;
        }

        internal static bool BeginPublication(KingdomStage stage, int revision, List<Thing> selected)
        {
            KingdomArcProgress p = Progress;
            if (!CanSelect(stage, revision, KingdomStage.Routes) || !KingdomMaterials.ValidateSelection(Home, selected)) return false;
            WorldObject_RHAH_KingdomSite a = Place(KingdomSiteKind.Hall);
            WorldObject_RHAH_KingdomSite b = Place(KingdomSiteKind.Hall);
            if (a == null || b == null)
            {
                if (a != null) RemoveSite(a.ID);
                if (b != null) RemoveSite(b.ID);
                return false;
            }
            for (int i = 0; i < selected.Count; i++)
            {
                if ((p.PublicMask & 1) == 0) a.Supply.Add(KingdomMaterials.Copy(selected[i], !p.NamedConsent));
                if ((p.PublicMask & 2) == 0) b.Supply.Add(KingdomMaterials.Copy(selected[i], !p.NamedConsent));
            }
            p.Route = KingdomRoute.Publish;
            a.RecipientGroup = 1;
            b.RecipientGroup = 2;
            p.PublicDueA = (p.PublicMask & 1) == 0 ? AddTicks(Now, CourierTicks(Home.Tile, a.Tile) * 2) : -1;
            p.PublicDueB = (p.PublicMask & 2) == 0 ? AddTicks(Now, CourierTicks(Home.Tile, b.Tile) * 2) : -1;
            p.SiteId = a.ID;
            p.SafeSiteId = b.ID;
            p.Move(KingdomStage.Action);
            p.ResultSent = true;
            p.Deadline = AddTicks(Now, GenDate.TicksPerDay * 7);
            p.Due = -1;
            KingdomArcRuntime.SendCurrent();
            Wake();
            return true;
        }

        private static bool CanSelect(KingdomStage stage, int revision, KingdomStage required)
        {
            return KingdomArcRuntime.CanAct && Narrator && Progress != null && stage == required && Progress.Matches(stage, revision) && Progress.ResultSent;
        }

        private static bool BeginRoute(string action)
        {
            KingdomArcProgress p = Progress;
            if (action == "Protect")
            {
                if (!AvailableClerk(p) || !p.TransferConsent) return false;
                WorldObject_RHAH_KingdomSite meeting = Site(p.ClerkSiteId);
                WorldObject_RHAH_KingdomSite safe = Site(p.ConsentSafeSiteId);
                if (meeting == null || safe == null)
                {
                    if (meeting != null) RemoveSite(meeting.ID);
                    if (safe != null) RemoveSite(safe.ID);
                    return false;
                }
                p.Route = KingdomRoute.Protect;
                p.SiteId = meeting.ID;
                p.SafeSiteId = safe.ID;
                if (p.Clerk.GetCaravan() == null && !meeting.Recipients.Contains(p.Clerk)) meeting.Recipients.Add(p.Clerk);
            }
            else
            {
                WorldObject_RHAH_KingdomSite site = Place(KingdomSiteKind.Distribution);
                if (site == null) return false;
                for (int i = 0; i < 5 - p.Delivered; i++)
                {
                    Verse.Pawn recipient = GeneratePerson();
                    if (recipient == null) { RemoveSite(site.ID); return false; }
                    site.Recipients.Add(recipient);
                    Thing grain = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RawRice"));
                    grain.stackCount = 20;
                    site.Supply.Add(grain);
                }
                p.Route = KingdomRoute.Repair;
                p.SiteId = site.ID;
            }
            p.Move(KingdomStage.Action);
            p.Progress = 0;
            p.ResultSent = true;
            p.Deadline = AddTicks(Now, GenDate.TicksPerDay * 7);
            p.Due = -1;
            KingdomArcRuntime.SendCurrent();
            return true;
        }

        internal static void Interact(WorldObject_RHAH_KingdomSite site, WorldCaravan caravan)
        {
            KingdomArcProgress p = Progress;
            if (!KingdomArcRuntime.CanAct || site == null || caravan == null || caravan.Tile != site.Tile || !caravan.IsPlayerControlled) return;
            Verse.Pawn actor = Adult(caravan);
            if (p.Deadline >= 0 && Now >= p.Deadline)
            {
                Advance(Now);
                return;
            }
            if (actor == null) { Messages.Message("RHAH_Kingdom_NeedAdult".Translate(), MessageTypeDefOf.RejectInput, false); return; }
            if (p.Stage == KingdomStage.Action && p.Route == KingdomRoute.Protect)
            {
                if (!AvailableClerk(p) || !p.TransferConsent) return;
                if (site.ID == p.SiteId && site.Recipients.Contains(p.Clerk))
                {
                    site.Recipients.Remove(p.Clerk);
                    caravan.AddPawn(p.Clerk, true);
                    Messages.Message("RHAH_Kingdom_ClerkMet".Translate(p.Clerk.LabelShortCap), MessageTypeDefOf.NeutralEvent, false);
                }
                else if (site.ID == p.SafeSiteId && caravan.ContainsPawn(p.Clerk) && !p.Clerk.Downed && !p.Clerk.IsPrisoner && !p.Clerk.IsSlave)
                {
                    caravan.RemovePawn(p.Clerk);
                    site.Recipients.Add(p.Clerk);
                    p.Outcome = KingdomOutcome.Success;
                    p.Finish(KingdomFailure.None);
                }
            }
            else if (site.ID == p.SiteId && (p.Stage == KingdomStage.Hall || p.Stage == KingdomStage.Charity || p.Stage == KingdomStage.Action && p.Route == KingdomRoute.Repair))
            {
                if (p.Stage == KingdomStage.Hall && !KingdomMaterials.HasReceipts(KingdomMaterials.Held(null, caravan)))
                { Messages.Message("RHAH_Kingdom_NeedReceipts".Translate(), MessageTypeDefOf.RejectInput, false); return; }
                if (p.Stage == KingdomStage.Charity && (Now < p.CharityStart || Now >= p.CharityEnd))
                { Messages.Message("RHAH_Kingdom_NotStarted".Translate(), MessageTypeDefOf.RejectInput, false); return; }
                p.ActorId = actor.thingIDNumber;
                p.LastWorkTick = Now;
                caravan.pather.StopDead();
                Messages.Message("RHAH_Kingdom_WorkStarted".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            Wake();
        }

        private static void Work(KingdomArcProgress p, int tick)
        {
            if (p.ActorId == 0) return;
            WorldObject_RHAH_KingdomSite site = Site(p.SiteId);
            WorldCaravan caravan = FindActorCaravan(p.ActorId);
            Verse.Pawn actor = caravan == null ? null : Adult(caravan, p.ActorId);
            if (site == null || actor == null || caravan.Tile != site.Tile || caravan.pather.Moving)
            {
                if (p.Stage == KingdomStage.Hall) p.Progress = 0;
                p.ActorId = 0;
                p.LastWorkTick = -1;
                return;
            }
            int observed = p.Deadline >= 0 ? Math.Min(tick, p.Deadline) : tick;
            int elapsed = p.LastWorkTick < 0 ? 0 : Math.Max(0, observed - p.LastWorkTick);
            p.LastWorkTick = observed;
            p.Progress += elapsed;
            if (p.Stage == KingdomStage.Hall && p.Progress >= GenDate.TicksPerHour)
            {
                if (!KingdomMaterials.HasReceipts(KingdomMaterials.Held(null, caravan))) { p.Progress = 0; p.ActorId = 0; return; }
                if (!KingdomMaterials.Acquire(p, KingdomMaterialKind.VerifiedCopy, "Hall", null, caravan)) return;
                p.HallResult = KingdomNodeResult.Completed;
                p.Verified = true;
                p.Failure = KingdomFailure.None;
                RemoveSite(site.ID);
                Commission(KingdomStage.InquiryClose, tick);
            }
            else if (p.Stage == KingdomStage.Charity && p.Progress >= GenDate.TicksPerHour * 2)
            {
                if (!KingdomMaterials.Acquire(p, KingdomMaterialKind.CharityReport, "Observation", null, caravan)) return;
                p.CharityResult = KingdomNodeResult.Completed;
                p.CharityObserved = true;
                RemoveSite(site.ID);
                Commission(KingdomStage.Guard, tick);
            }
            else if (p.Stage == KingdomStage.Action && p.Route == KingdomRoute.Repair)
            {
                while (p.Progress >= GenDate.TicksPerHour / 2 && site.Recipients.Count > 0 && site.Supply.Count > 0)
                {
                    Verse.Pawn recipient = site.Recipients[0];
                    if (recipient.Dead || recipient.Destroyed) { p.Finish(KingdomFailure.Obstructed); return; }
                    Thing grain = site.Supply[0];
                    if (!recipient.inventory.innerContainer.TryAdd(grain)) { p.Finish(KingdomFailure.Obstructed); return; }
                    site.Supply.RemoveAt(0);
                    site.Recipients.RemoveAt(0);
                    p.Delivered++;
                    p.Progress -= GenDate.TicksPerHour / 2;
                }
                if (p.Delivered == 5)
                {
                    if (!KingdomMaterials.Acquire(p, KingdomMaterialKind.DistributionRecord, "Distribution", null, caravan)) return;
                    p.Finish(KingdomFailure.None);
                }
            }
        }

        private static void AdvanceAction(KingdomArcProgress p, int tick)
        {
            if (p.Route == KingdomRoute.Publish)
            {
                ConfirmPublication(p, 1, p.PublicDueA, p.SiteId, tick);
                ConfirmPublication(p, 2, p.PublicDueB, p.SafeSiteId, tick);
                if (p.PublicMask == 3) { p.Attention = 2; p.Finish(KingdomFailure.None); return; }
            }
            else if (p.Route == KingdomRoute.Repair) Work(p, tick);
            else if (p.Route == KingdomRoute.Protect)
            {
                if (p.Clerk?.Dead == true) { p.Finish(KingdomFailure.Death); return; }
                if (p.Clerk == null || p.Clerk.Destroyed) { p.Finish(KingdomFailure.LostContact); return; }
                if (p.Clerk.IsPrisoner || p.Clerk.IsSlave) { p.TransferConsent = false; p.Finish(KingdomFailure.Obstructed); return; }
            }
            if (p.Stage == KingdomStage.Action && tick >= p.Deadline) p.Finish(KingdomFailure.Expired);
        }

        private static void ConfirmPublication(KingdomArcProgress p, int mask, int due, int siteId, int tick)
        {
            if ((p.PublicMask & mask) != 0 || due < 0 || tick < due || due > p.Deadline) return;
            WorldObject_RHAH_KingdomSite site = Site(siteId);
            if (site == null || site.Supply.Count == 0) return;
            for (int i = 0; i < site.Supply.Count; i++)
            {
                Comp_RHAH_KingdomMaterial material = site.Supply[i].TryGetComp<Comp_RHAH_KingdomMaterial>();
                for (int j = 0; j < p.Materials.Count; j++) if (p.Materials[j].Number == material.Number) p.Materials[j].ExternalMask |= mask;
            }
            p.PublicMask |= mask;
            site.PacketReceived = true;
        }

        internal static bool AvailableClerk(KingdomArcProgress p) => p.Clerk != null && !p.Clerk.Dead && !p.Clerk.Destroyed && !p.Clerk.IsPrisoner && !p.Clerk.IsSlave;
        internal static Verse.Pawn Adult(WorldCaravan caravan, int id = 0)
        {
            for (int i = 0; i < caravan.PawnsListForReading.Count; i++)
            {
                Verse.Pawn pawn = caravan.PawnsListForReading[i];
                if ((id == 0 || pawn.thingIDNumber == id) && pawn.Faction == Faction.OfPlayer && pawn.RaceProps.Humanlike && pawn.ageTracker.AgeBiologicalYears >= 18 && !pawn.Dead && !pawn.Downed && !pawn.InMentalState && !pawn.IsPrisoner && !pawn.IsSlave) return pawn;
            }
            return null;
        }
        private static WorldCaravan FindActorCaravan(int actor)
        {
            List<WorldCaravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++) for (int j = 0; j < caravans[i].PawnsListForReading.Count; j++)
                if (caravans[i].PawnsListForReading[j].thingIDNumber == actor) return caravans[i];
            return null;
        }
        internal static WorldObject_RHAH_KingdomSite Site(int id) => Find.WorldObjects?.AllWorldObjects.Find(item => item.ID == id) as WorldObject_RHAH_KingdomSite;
        private static void RemoveSite(int id)
        {
            WorldObject_RHAH_KingdomSite site = Site(id);
            if (site != null && site.Recipients.Count == 0 && site.Supply.Count == 0) site.Destroy();
        }
        private static bool Give(KingdomMaterialKind kind, string source) => KingdomMaterials.Acquire(Progress, kind, source, Home);
        private static bool HasMaterial(KingdomArcProgress p, KingdomMaterialKind kind)
        {
            for (int i = 0; i < p.Materials.Count; i++) if (p.Materials[i].Kind == kind) return true;
            return false;
        }
        internal static int CourierTicks(PlanetTile from, PlanetTile to)
        {
            if (from == to) return GenDate.TicksPerHour;
            WorldPath path = from.Layer.Pather.FindPath(from, to, null);
            if (path == null || path == WorldPath.NotFound || !path.Found) return GenDate.TicksPerDay * 8;
            long cost = 0;
            List<PlanetTile> nodes = path.NodesReversed;
            for (int i = nodes.Count - 1; i > 0; i--) cost += Caravan_PathFollower.CostToMove(3300, nodes[i], nodes[i - 1]);
            path.ReleaseToPool();
            return (int)Math.Min(int.MaxValue / 4, Math.Max(GenDate.TicksPerHour, cost));
        }
        internal static WorldObject_RHAH_KingdomSite Place(KingdomSiteKind kind, PlanetTile? origin = null)
        {
            Map home = Home;
            if (home == null) return null;
            PlanetTile tile;
            PlanetTile from = origin ?? home.Tile;
            if (!TileFinder.TryFindPassableTileWithTraversalDistance(from, 1, 5, out tile,
                candidate => candidate != home.Tile && candidate != from && !Find.World.Impassable(candidate) && !Occupied(candidate) && TravelTicks(from, candidate, 10000) <= GenDate.TicksPerDay * 2)) return null;
            WorldObject_RHAH_KingdomSite site = (WorldObject_RHAH_KingdomSite)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("RHAH_KingdomSite"));
            site.Tile = tile;
            site.Kind = kind;
            Find.WorldObjects.Add(site);
            return site;
        }

        private static bool Occupied(PlanetTile tile)
        {
            foreach (WorldObject item in Find.WorldObjects.ObjectsAt(tile)) return true;
            return false;
        }

        private static int TravelTicks(PlanetTile from, PlanetTile to, int speed)
        {
            if (from == to) return 0;
            WorldPath path = from.Layer.Pather.FindPath(from, to, null);
            if (path == null || path == WorldPath.NotFound || !path.Found) return int.MaxValue;
            long cost = 0;
            List<PlanetTile> nodes = path.NodesReversed;
            for (int i = nodes.Count - 1; i > 0; i--) cost += Caravan_PathFollower.CostToMove(speed, nodes[i], nodes[i - 1]);
            path.ReleaseToPool();
            return (int)Math.Min(int.MaxValue, cost);
        }

        private static bool RequestConsent(KingdomClerkReply purpose)
        {
            KingdomArcProgress p = Progress;
            WorldObject_RHAH_KingdomSite clerkSite = Site(p.ClerkSiteId);
            if (!AvailableClerk(p) || clerkSite == null || p.ConsentRequest != KingdomClerkReply.None) return false;
            if (purpose == KingdomClerkReply.SafeTransfer)
            {
                WorldObject_RHAH_KingdomSite safe = Place(KingdomSiteKind.SafeHouse, clerkSite.Tile);
                if (safe == null) return false;
                p.SafeSiteId = safe.ID;
                p.ConsentSafeSiteId = safe.ID;
            }
            p.ConsentRequest = purpose;
            p.ConsentDue = AddTicks(Now, CourierTicks(Home.Tile, clerkSite.Tile) * 2);
            return true;
        }

        private static void DeliverConsent(KingdomArcProgress p, int tick)
        {
            if (p.ConsentRequest == KingdomClerkReply.None || tick < p.ConsentDue) return;
            KingdomClerkReply purpose = p.ConsentRequest;
            p.ConsentDue = -1;
            p.ConsentRequest = KingdomClerkReply.None;
            bool agrees = AvailableClerk(p) && (purpose != KingdomClerkReply.SafeTransfer || Site(p.ConsentSafeSiteId) != null);
            if (purpose == KingdomClerkReply.NamedPublication) p.NamedConsent = agrees;
            else p.TransferConsent = agrees;
            Find.LetterStack.ReceiveLetter("RHAH_Kingdom_ConsentTitle".Translate(),
                (agrees ? purpose == KingdomClerkReply.NamedPublication ? "RHAH_Kingdom_ConsentNamed" : "RHAH_Kingdom_ConsentTransfer" : "RHAH_Kingdom_ConsentUnavailable").Translate(p.Clerk?.LabelShortCap ?? ""), LetterDefOf.NeutralEvent);
        }
        private static Verse.Pawn GeneratePerson()
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(RHAH_DefOf.RHAH_Faction_Neutral);
            if (faction == null || RHAH_DefOf.RHAH_PawnKind_Ratkin == null) return null;
            Verse.Pawn person = PawnGenerator.GeneratePawn(new PawnGenerationRequest(RHAH_DefOf.RHAH_PawnKind_Ratkin, faction, PawnGenerationContext.NonPlayer, fixedBiologicalAge: 30f, fixedChronologicalAge: 30f));
            Find.WorldPawns.PassToWorld(person, PawnDiscardDecideMode.KeepForever);
            return person;
        }
        private static void SpawnEnvoy()
        {
            KingdomArcProgress p = Progress;
            if (p.Envoy != null) return;
            Verse.Pawn envoy = GeneratePerson();
            IntVec3 cell;
            if (envoy == null || !CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(Home), Home, CellFinder.EdgeRoadChance_Neutral, out cell)) return;
            Find.WorldPawns.RemovePawn(envoy);
            GenSpawn.Spawn(envoy, cell, Home);
            p.Envoy = envoy;
        }
        private static void ReleaseEnvoy()
        {
            Verse.Pawn envoy = Progress.Envoy;
            if (envoy?.Spawned == true && !envoy.Dead)
            {
                envoy.DeSpawn();
                Find.WorldPawns.PassToWorld(envoy, PawnDiscardDecideMode.KeepForever);
            }
        }
    }
}
