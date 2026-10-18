using System.Collections.Generic;
using Verse;
using RimWorld;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class KingdomArcPresentation
    {
        internal static KingdomArcSegment[] Scenes(KingdomStage stage, KingdomArcProgress p)
        {
            string body = "RHAH_Kingdom_Stage_" + stage;
            string image = "K2-E1-two-receipts-1";
            switch (stage)
            {
                case KingdomStage.Interview: image = "K2-E2-dismissed-clerk-1"; break;
                case KingdomStage.Hall: image = "K2-E3-ration-hall-1"; break;
                case KingdomStage.InquiryClose:
                    body += p.Verified ? "_Verified" : "_Unverified";
                    image = "K2-E4-two-edits-3-revised";
                    break;
                case KingdomStage.Envoy: image = "K3-E1-royal-envoy-1"; break;
                case KingdomStage.Charity: image = "K3-E2-royal-charity-2"; break;
                case KingdomStage.Guard:
                    body += p.CharityObserved ? "_Observed" : p.CharityInterrupted ? "_Interrupted" : "_Hearsay";
                    image = p.CharityObserved ? "K3-E2-royal-charity-4" : p.CharityInterrupted ? "K3-E2-royal-charity-5" : "K3-E2-royal-charity-6";
                    break;
                case KingdomStage.Routes: image = "K3-E4-record-destination-1"; break;
                case KingdomStage.Action:
                    body += "_" + p.Route;
                    image = p.Route == KingdomRoute.Repair ? "K4-E1-repair-2" : p.Route == KingdomRoute.Publish ? "K4-E2-publish-1" : "K4-E3-protect-2";
                    break;
                case KingdomStage.Response:
                case KingdomStage.Complete:
                    body = "RHAH_Kingdom_Response_" + p.Route + "_" + p.Outcome;
                    image = ResponseImage(p);
                    break;
            }
            List<KingdomArcSegment> result = new List<KingdomArcSegment>();
            if (stage == KingdomStage.Interview)
            {
                Add(result, "ClerkWords", "K2-E2-dismissed-clerk-1", "Clerk");
                Add(result, "ClerkRequest", "K2-E2-dismissed-clerk-2", "Letter");
            }
            if (stage == KingdomStage.Envoy)
            {
                Add(result, "EnvoyWords", "K3-E1-royal-envoy-1", "Envoy");
                Add(result, "FatherStamp", "K3-E1-royal-envoy-5", "Letter");
            }
            if (stage == KingdomStage.Guard)
            {
                Add(result, "GuardCommander", "K3-E3-grain-guard-1", "Commander");
                Add(result, "GuardQuestion", "K3-E3-grain-guard-2", "Letter");
            }
            if (stage == KingdomStage.Routes)
            {
                Add(result, "WaitingDiary", "K3-E4-record-destination-1", "Diary");
                Add(result, p.GuardReceived ? "GuardReceipt" : p.GuardPetition ? "GuardPetition" : "GuardUnknown", p.GuardReceived ? "K3-E3-grain-guard-3" : p.GuardPetition && p.GuardResult == KingdomNodeResult.Completed ? "K3-E3-grain-guard-4" : "K3-E3-grain-guard-5", "Letter");
            }
            result.Add(new KingdomArcSegment("RHAH_Kingdom_Title", "RHAH_Kingdom_Letter", body, "KingdomArc/" + image, 0d));
            if (stage == KingdomStage.Response || stage == KingdomStage.Complete)
            {
                string suffix = p.Route == KingdomRoute.Protect ? p.Failure == KingdomFailure.Death ? "ProtectDeath" : p.Outcome == KingdomOutcome.Success ? "ProtectSafe" : "ProtectFailed" : "PhaseClosing";
                Add(result, suffix, p.Route == KingdomRoute.Repair ? "K5-E1-repair-ending-5" : "K5-E5-open-ending-3", "Letter");
            }
            return result.ToArray();
        }

        private static void Add(List<KingdomArcSegment> result, string suffix, string image, string speaker)
        {
            result.Add(new KingdomArcSegment("RHAH_Kingdom_Title", "RHAH_Kingdom_" + speaker, "RHAH_Kingdom_Scene_" + suffix, "KingdomArc/" + image, 0d));
        }

        private static string ResponseImage(KingdomArcProgress p)
        {
            if (p.Route == KingdomRoute.Repair) return p.Outcome == KingdomOutcome.Success ? "K5-E1-repair-ending-1" : p.Outcome == KingdomOutcome.Partial ? "K5-E1-repair-ending-3" : "K5-E1-repair-ending-4-revised";
            if (p.Route == KingdomRoute.Publish) return p.Outcome == KingdomOutcome.Success ? "K5-E2-publish-ending-3" : p.Outcome == KingdomOutcome.Partial ? "K5-E2-publish-ending-4" : "K5-E2-publish-ending-5";
            if (p.Route == KingdomRoute.Protect) return p.Outcome == KingdomOutcome.Success ? "K5-E3-protect-ending-1" : p.Failure == KingdomFailure.Death ? "K5-E3-protect-ending-3" : "K5-E3-protect-ending-4";
            return "K5-E5-open-ending-1";
        }

        internal static string Conditions(KingdomArcProgress p)
        {
            Map home = KingdomArcActions.Home;
            string text = "\n\n" + "RHAH_Kingdom_Materials".Translate() + "\n" + KingdomMaterials.List(KingdomArcActions.Home);
            if (p.Deadline >= 0) text += "\n" + "RHAH_Kingdom_Deadline".Translate(Date(p.Deadline));
            if (p.Stage == KingdomStage.Charity) text += "\n" + "RHAH_Kingdom_CharitySchedule".Translate(Date(p.CharityStart), Date(p.CharityEnd));
            WorldObject_RHAH_KingdomSite site = KingdomArcActions.Site(p.SiteId);
            if (site != null && home != null) text += "\n" + "RHAH_Kingdom_Location".Translate(site.Label, site.Tile.ToString(), (KingdomArcActions.CourierTicks(home.Tile, site.Tile) / (float)GenDate.TicksPerDay).ToString("0.0"));
            WorldObject_RHAH_KingdomSite safe = KingdomArcActions.Site(p.SafeSiteId);
            if (p.Route == KingdomRoute.Protect && safe != null) text += "\n" + "RHAH_Kingdom_SafeDestination".Translate(safe.Tile.ToString());
            text += "\n" + (p.NamedConsent ? "RHAH_Kingdom_NameConsentYes" : "RHAH_Kingdom_NameConsentNo").Translate();
            if (p.ConsentRequest != KingdomClerkReply.None) text += "\n" + "RHAH_Kingdom_ConsentPending".Translate(Date(p.ConsentDue));
            if (p.Stage == KingdomStage.Action && p.Route == KingdomRoute.Repair) text += "\n" + "RHAH_Kingdom_RepairProgress".Translate(p.Delivered);
            if (p.Stage == KingdomStage.Action && p.Route == KingdomRoute.Publish) text += "\n" + "RHAH_Kingdom_PublicProgress".Translate(p.PublicMask == 3 ? 2 : p.PublicMask == 0 ? 0 : 1);
            if (p.Failure != KingdomFailure.None) text += "\n" + ("RHAH_Kingdom_Failure_" + p.Failure).Translate();
            if (p.Clerk != null) text += "\n" + "RHAH_Kingdom_ClerkIdentity".Translate(p.Clerk.LabelShortCap, p.Clerk.Dead ? "RHAH_Kingdom_Dead".Translate() : "RHAH_Kingdom_Alive".Translate());
            return text;
        }

        private static string Date(int tick)
        {
            Map home = KingdomArcActions.Home;
            return tick < 0 || home == null ? "" : GenDate.DateFullStringAt((long)tick + Find.TickManager.TicksAbs - Find.TickManager.TicksGame, Find.WorldGrid.LongLatOf(home.Tile));
        }

        internal static string[] Choices(KingdomStage stage)
        {
            switch (stage)
            {
                case KingdomStage.Receipts: return new[] { "Formal", "Private", "Stop" };
                case KingdomStage.Interview: return new[] { "Verify", "Named", "Stop" };
                case KingdomStage.Hall:
                case KingdomStage.Charity: return new[] { "Locate", "Decline" };
                case KingdomStage.InquiryClose: return new[] { "Continue" };
                case KingdomStage.Envoy: return new[] { "Keep", "Handover", "Refuse" };
                case KingdomStage.Guard: return new[] { "Receipt", "Petition", "Decline" };
                case KingdomStage.Routes: return new[] { "Repair", "Publish", "Named", "Protect", "Defer", "Pause" };
                case KingdomStage.Action: return new[] { "Locate", "Cancel" };
                case KingdomStage.Response: return new[] { "Close", "Reconsider" };
                default: return new string[0];
            }
        }
    }
}
