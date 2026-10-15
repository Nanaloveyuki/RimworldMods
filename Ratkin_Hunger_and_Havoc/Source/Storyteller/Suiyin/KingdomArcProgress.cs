using System;
using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal enum KingdomStage
    {
        None, Parcel, Receipts, InterviewTransit, Interview, Hall, InquiryClose,
        Envoy, Charity, Guard, GuardTransit, Routes, Action, Response, Complete
    }

    internal enum KingdomRoute { None, Repair, Publish, Protect, Pause }
    internal enum KingdomOutcome { None, Success, Partial, Failed }
    internal enum KingdomFailure { None, Cancelled, Expired, Unavailable, Withdrawn, LostContact, Death, Obstructed }
    internal enum KingdomClerkReply { None, NamedPublication, SafeTransfer }
    internal enum KingdomNodeResult { None, Completed, Declined, Unavailable, Expired, Partial, Unknown }

    internal sealed class KingdomArcProgress : IExposable
    {
        public KingdomArcProgress() { }
        internal KingdomStage Stage;
        internal int ChainId;
        internal int Revision;
        internal int Cursor;
        internal int Due = -1;
        internal int Deadline = -1;
        internal int HomeId = -1;
        internal int SiteId;
        internal int SafeSiteId;
        internal int ConsentSafeSiteId;
        internal int ActorId;
        internal int Progress;
        internal int LastWorkTick = -1;
        internal int CharityStart = -1;
        internal int CharityEnd = -1;
        internal int CourtSiteId;
        internal int ClerkSiteId;
        internal int ConsentDue = -1;
        internal KingdomClerkReply ConsentRequest;
        internal KingdomNodeResult HallResult;
        internal KingdomNodeResult EnvoyResult;
        internal KingdomNodeResult CharityResult;
        internal KingdomNodeResult GuardResult;
        internal List<string> RouteHistory = new List<string>();
        internal bool Formal;
        internal bool Verified;
        internal bool PrivateConsent;
        internal bool NamedConsent;
        internal bool TransferConsent;
        internal bool StatementReceived;
        internal bool EnvoyHandedOver;
        internal bool CharityObserved;
        internal bool CharityInterrupted;
        internal bool GuardPetition;
        internal bool GuardReceived;
        internal bool PendingLetter;
        internal bool ResultSent;
        internal int Delivered;
        internal int PublicMask;
        internal int PublicDueA = -1;
        internal int PublicDueB = -1;
        internal int Attention;
        internal KingdomRoute Route;
        internal KingdomOutcome Outcome;
        internal KingdomFailure Failure;
        internal Verse.Pawn Clerk;
        internal Verse.Pawn Envoy;
        internal List<KingdomMaterialRecord> Materials = new List<KingdomMaterialRecord>();

        internal void Move(KingdomStage stage)
        {
            Stage = stage;
            Revision++;
            Cursor = 0;
            ActorId = 0;
            LastWorkTick = -1;
        }

        internal bool Matches(KingdomStage stage, int revision)
        {
            return Stage == stage && Revision == revision;
        }

        internal void Finish(KingdomFailure failure)
        {
            Failure = failure;
            Outcome = Route == KingdomRoute.Repair
                ? Delivered == 5 ? KingdomOutcome.Success : Delivered > 0 ? KingdomOutcome.Partial : KingdomOutcome.Failed
                : Route == KingdomRoute.Publish
                    ? PublicMask == 3 ? KingdomOutcome.Success : PublicMask != 0 ? KingdomOutcome.Partial : KingdomOutcome.Failed
                    : Route == KingdomRoute.Protect && Outcome == KingdomOutcome.Success ? KingdomOutcome.Success : KingdomOutcome.Failed;
            RouteHistory.Add(Route + ":" + Outcome + ":" + failure + ":" + Delivered + ":" + PublicMask);
            Move(KingdomStage.Response);
            Due = -1;
            Deadline = -1;
            ResultSent = false;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Stage, "stage", KingdomStage.None);
            Scribe_Values.Look(ref ChainId, "chainId", 0);
            Scribe_Values.Look(ref Revision, "revision", 0);
            Scribe_Values.Look(ref Cursor, "cursor", 0);
            Scribe_Values.Look(ref Due, "due", -1);
            Scribe_Values.Look(ref Deadline, "deadline", -1);
            Scribe_Values.Look(ref HomeId, "homeId", -1);
            Scribe_Values.Look(ref SiteId, "siteId", 0);
            Scribe_Values.Look(ref ConsentSafeSiteId, "consentSafeSiteId", 0);
            Scribe_Values.Look(ref SafeSiteId, "safeSiteId", 0);
            Scribe_Values.Look(ref ActorId, "actorId", 0);
            Scribe_Values.Look(ref Progress, "progress", 0);
            Scribe_Values.Look(ref LastWorkTick, "lastWorkTick", -1);
            Scribe_Values.Look(ref CharityStart, "charityStart", -1);
            Scribe_Values.Look(ref CharityEnd, "charityEnd", -1);
            Scribe_Values.Look(ref CourtSiteId, "courtSiteId", 0);
            Scribe_Values.Look(ref ClerkSiteId, "clerkSiteId", 0);
            Scribe_Values.Look(ref ConsentDue, "consentDue", -1);
            Scribe_Values.Look(ref ConsentRequest, "consentRequest", KingdomClerkReply.None);
            Scribe_Values.Look(ref HallResult, "hallResult", KingdomNodeResult.None);
            Scribe_Values.Look(ref EnvoyResult, "envoyResult", KingdomNodeResult.None);
            Scribe_Values.Look(ref CharityResult, "charityResult", KingdomNodeResult.None);
            Scribe_Values.Look(ref GuardResult, "guardResult", KingdomNodeResult.None);
            Scribe_Collections.Look(ref RouteHistory, "routeHistory", LookMode.Value);
            Scribe_Values.Look(ref Formal, "formal", false);
            Scribe_Values.Look(ref Verified, "verified", false);
            Scribe_Values.Look(ref PrivateConsent, "privateConsent", false);
            Scribe_Values.Look(ref NamedConsent, "namedConsent", false);
            Scribe_Values.Look(ref TransferConsent, "transferConsent", false);
            Scribe_Values.Look(ref StatementReceived, "statementReceived", false);
            Scribe_Values.Look(ref EnvoyHandedOver, "envoyHandedOver", false);
            Scribe_Values.Look(ref CharityObserved, "charityObserved", false);
            Scribe_Values.Look(ref CharityInterrupted, "charityInterrupted", false);
            Scribe_Values.Look(ref GuardPetition, "guardPetition", false);
            Scribe_Values.Look(ref GuardReceived, "guardReceived", false);
            Scribe_Values.Look(ref ResultSent, "resultSent", false);
            Scribe_Values.Look(ref PendingLetter, "pendingLetter", false);
            Scribe_Values.Look(ref Delivered, "delivered", 0);
            Scribe_Values.Look(ref PublicMask, "publicMask", 0);
            Scribe_Values.Look(ref PublicDueA, "publicDueA", -1);
            Scribe_Values.Look(ref PublicDueB, "publicDueB", -1);
            Scribe_Values.Look(ref Attention, "attention", 0);
            Scribe_Values.Look(ref Route, "route", KingdomRoute.None);
            Scribe_Values.Look(ref Outcome, "outcome", KingdomOutcome.None);
            Scribe_Values.Look(ref Failure, "failure", KingdomFailure.None);
            Scribe_References.Look(ref Clerk, "clerk");
            Scribe_References.Look(ref Envoy, "envoy");
            Scribe_Collections.Look(ref Materials, "materials", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Materials = Materials ?? new List<KingdomMaterialRecord>();
                RouteHistory = RouteHistory ?? new List<string>();
            }
        }
    }
}
