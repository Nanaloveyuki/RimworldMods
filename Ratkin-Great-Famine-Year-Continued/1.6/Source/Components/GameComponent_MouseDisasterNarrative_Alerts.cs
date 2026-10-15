using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public sealed class MouseDisasterJournalEntry : IExposable
    {
        public string label;
        public string text;
        public bool read;
        public void ExposeData()
        {
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref read, "read");
        }
    }

    public partial class GameComponent_MouseDisasterNarrative
    {
        private List<MouseDisasterJournalEntry> journalEntries = new List<MouseDisasterJournalEntry>();
        private int nextAlertRefresh = -1;
        private int unreadNarratives;
        private int pendingNarratives;
        public int UnreadNarrativeCount { get { RefreshNarrativeAlert(); return unreadNarratives; } }
        public int PendingNarrativeCount { get { RefreshNarrativeAlert(); return pendingNarratives; } }

        private void RefreshNarrativeAlert()
        {
            if (CurrentNarrativeTick < nextAlertRefresh) return;
            nextAlertRefresh = CurrentNarrativeTick + 60;
            unreadNarratives = journalEntries.Count(e => !e.read);
            pendingNarratives = narrativeVisits.Count(v => v.showLetter && !v.resolved) +
            n004Records.Count(r => r.outcome == MouseDisasterN004Outcome.Pending) +
            n005Records.Count(r => r.outcome == MouseDisasterN005Outcome.Accepted && !r.careResolved) +
            n006Records.Count(r => r.phase != MouseDisasterN006Phase.Tracking && r.phase != MouseDisasterN006Phase.Resolved) +
            n007Records.Count(r => r.phase != MouseDisasterN007Phase.Resolved) +
            (envoyPhase > 0 && envoyPhase < 4 ? 1 : 0) + (relicStarted && relicOutcome == 0 ? 1 : 0) +
            (returnPhase == 2 ? 1 : 0);
        }

        private void ExposeNarrativeAlerts()
        {
            Scribe_Collections.Look(ref journalEntries, "mouseDisaster_journalEntries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                journalEntries ??= new List<MouseDisasterJournalEntry>();
                journalEntries.RemoveAll(e => e == null);
            }
        }

        public void OpenNarrativeJournal()
        {
            var options = new List<FloatMenuOption>();
            string progress = "MouseDisaster_Story_AlertProgress".Translate(
                narrativeVisits.Count(v => v.showLetter && !v.resolved),
                n005Records.Count(r => r.outcome == MouseDisasterN005Outcome.Accepted && !r.careResolved),
                n007Records.Count(r => r.phase != MouseDisasterN007Phase.Resolved),
                envoyPhase > 0 && envoyPhase < 4 ? 1 : 0, relicStarted && relicOutcome == 0 ? 1 : 0);
            options.Add(new FloatMenuOption("MouseDisaster_Story_Progress".Translate(),
                    () => Find.WindowStack.Add(new Dialog_MessageBox(progress + BuildNarrativeProgressDetails()))));
            foreach (var record in n004Records.Where(r => r.outcome == MouseDisasterN004Outcome.Pending))
                AddNarrativeTarget(options, "N004", record.mother);
            foreach (var record in n005Records.Where(r => r.outcome == MouseDisasterN005Outcome.Accepted && !r.careResolved))
                AddNarrativeTarget(options, "N005", record.exchangeChildren.FirstOrDefault(p => p != null && p.Spawned));
            foreach (var record in n006Records.Where(r => r.phase != MouseDisasterN006Phase.Tracking && r.phase != MouseDisasterN006Phase.Resolved))
                AddNarrativeTarget(options, "N006", record.burrow);
            foreach (var record in n007Records.Where(r => r.phase != MouseDisasterN007Phase.Resolved))
                AddNarrativeTarget(options, "N007", record.pawnRecords.Select(p => p.pawn).FirstOrDefault(p => p != null && p.Spawned));
            if (envoyPhase > 0 && envoyPhase < 4) AddNarrativeTarget(options, "N008", envoy);
            if (relicStarted && relicOutcome == 0)
            {
                if (storyCache?.Spawned == true) AddNarrativeTarget(options, "N009", storyCache);
                else if (storySite != null) options.Add(new FloatMenuOption("MouseDisaster_Story_ToggleN009".Translate(),
                    () => CameraJumper.TryJumpAndSelect(storySite)));
            }
            if (returnPhase == 2) AddNarrativeTarget(options, "N007Return", recoveredVisitor);
            foreach (var entry in journalEntries.AsEnumerable().Reverse())
            {
                var captured = entry;
                options.Add(new FloatMenuOption((entry.read ? "" : "* ") + entry.label,
                    () => Find.WindowStack.Add(new Dialog_MessageBox(captured.text,
                        "MouseDisaster_Story_MarkRead".Translate(), () => { captured.read = true; nextAlertRefresh = -1; },
                        "Close".Translate(), null, captured.label))));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string BuildNarrativeProgressDetails()
        {
            var text = new StringBuilder();
            text.Append("\n\n").Append("MouseDisaster_Story_ClosedCounts".Translate(narrativeVisitSummaries.Count, aidCompleted));
            foreach (var visit in narrativeVisits.Where(v => v.showLetter && !v.resolved))
            {
                text.Append("\n\n").Append(("MouseDisaster_Story_" + visit.scene + "_Label").Translate());
                AppendCareProgress(text, visit.people, visit.created);
            }
            foreach (var record in n005Records.Where(r => r.outcome == MouseDisasterN005Outcome.Accepted && !r.careResolved))
            {
                text.Append("\n\n").Append("MouseDisaster_Story_ToggleN005".Translate());
                AppendCareProgress(text, record.care, record.createdTick, allowCaptiveCare: true);
            }
            foreach (var record in n004Records.Where(r => r.outcome == MouseDisasterN004Outcome.Pending))
            {
                text.Append("\n\n").Append("MouseDisaster_Story_ToggleN004".Translate());
                bool growing = record.children.Any(p => p != null && !p.Dead && !IsChildAge(p) && IsN004InPlayerDomain(p));
                bool missingReference = record.mother == null || record.children.Count == 0 ||
                    record.mother.Destroyed && !record.mother.Dead || record.children.Any(p => p == null || p.Destroyed && !p.Dead);
                text.Append("\n").Append((growing ? "MouseDisaster_Story_WaitGrowth" : "MouseDisaster_Story_WaitMissing").Translate());
                if (!growing)
                    text.Append("; ").Append("MouseDisaster_Story_ObservationDays".Translate(
                        (System.Math.Max(0, (missingReference ? MouseDisasterNarrativePolicy.MissingGraceTicks : MouseDisasterNarrativePolicy.ObservationTicks) - (CurrentNarrativeTick - record.createdTick)) /
                            (float)GenDate.TicksPerDay).ToString("0.0")));
                foreach (var child in record.children.Where(p => p != null && !p.Dead))
                    text.Append("\n").Append(child.LabelShortCap).Append(": ")
                        .Append("MouseDisaster_Story_ChildAge".Translate(child.ageTracker?.AgeBiologicalYearsFloat.ToString("0.0") ?? "?"));
            }
            foreach (var record in n007Records.Where(r => r.phase != MouseDisasterN007Phase.Resolved))
            {
                text.Append("\n\n").Append("MouseDisaster_Story_ToggleN007".Translate());
                foreach (var person in record.pawnRecords.Where(p => !p.handled && !p.died && !p.leftMap))
                {
                    string status = person.pawn == null ? "Missing" :
                        MouseDisasterUtility.IsPlayerAffiliatedRatkin(person.pawn) ? "Transferred" :
                        record.phase == MouseDisasterN007Phase.ReleasePending ? "Departure" :
                        HasN007Plague(person.pawn) ? "Treatment" : "Decision";
                    text.Append("\n").Append(person.pawn?.LabelShortCap.ToString() ?? "?").Append(": ")
                        .Append(("MouseDisaster_Story_Wait" + status).Translate());
                }
            }
            return text.ToString();
        }

        private void AppendCareProgress(StringBuilder text, IEnumerable<NarrativePawnObservation> people, int created, bool allowCaptiveCare = false)
        {
            foreach (var person in people)
            {
                text.Append("\n").Append(person.pawn?.LabelShortCap.ToString() ?? "?").Append(": ");
                if (person.end != NarrativePawnEnd.Pending)
                {
                    text.Append(("MouseDisaster_Story_End" + person.end).Translate());
                    continue;
                }
                if (!allowCaptiveCare && person.pawn != null && (person.pawn.IsPrisoner || person.pawn.IsSlave))
                {
                    text.Append("MouseDisaster_Story_EndDetained".Translate());
                    continue;
                }
                string reason = person.pawn == null ? "Missing" :
                    NarrativeInCare(person.pawn, allowCaptiveCare) ? (NarrativeCareHealthy(person.pawn) ? "Care" : "Treatment") : "Departure";
                text.Append(("MouseDisaster_Story_Wait" + reason).Translate());
                if (reason == "Care")
                    text.Append(" ").Append("MouseDisaster_Story_CareDays".Translate(
                        (person.careTicks / (float)GenDate.TicksPerDay).ToString("0.0"),
                        (MouseDisasterNarrativePolicy.CareTicks / (float)GenDate.TicksPerDay).ToString("0.0")));
                int remaining = person.pawn == null && person.missingSince >= 0
                    ? MouseDisasterNarrativePolicy.MissingGraceTicks - (CurrentNarrativeTick - person.missingSince)
                    : MouseDisasterNarrativePolicy.ObservationTicks - (CurrentNarrativeTick - created);
                text.Append("; ").Append("MouseDisaster_Story_ObservationDays".Translate(
                    (System.Math.Max(0, remaining) / (float)GenDate.TicksPerDay).ToString("0.0")));
            }
        }

        private static void AddNarrativeTarget(List<FloatMenuOption> options, string id, Thing target)
        {
            if (target?.Spawned != true) return;
            options.Add(new FloatMenuOption(("MouseDisaster_Story_Toggle" + id).Translate() + ": " + target.LabelShortCap,
                () => CameraJumper.TryJumpAndSelect(target)));
        }

        private static void ShowNarrativeEnding(string label, string body)
        {
            GameVictoryUtility.ShowCredits(label + "\n\n" + body, null, exitToMainMenu: false);
            ReceiveNarrativeLetterText(label, body, null);
        }
    }

    public class Alert_MouseDisasterNarrative : Alert
    {
        private GameComponent_MouseDisasterNarrative Narrative => Current.Game?.GetComponent<GameComponent_MouseDisasterNarrative>();
        public Alert_MouseDisasterNarrative() { defaultPriority = AlertPriority.Medium; }
        public override string GetLabel() => Suin
            ? "MouseDisaster_Story_AlertTrustLabel".Translate(Narrative?.NarratorTrust ?? 0).ToString()
            : "MouseDisaster_Story_AlertLabel".Translate().ToString();
        private bool Suin => Find.Storyteller?.def?.defName == GameComponent_MouseDisasterNarrative.NarratorDefName;
        public override TaggedString GetExplanation() => "MouseDisaster_Story_AlertText".Translate(
            Narrative?.UnreadNarrativeCount ?? 0, Narrative?.PendingNarrativeCount ?? 0).ToString() +
            (Suin ? "\n\n" + "MouseDisaster_Story_Trust".Translate(Narrative?.NarratorTrust ?? 0).ToString() : "");
        public override AlertReport GetReport() => Narrative != null &&
            (Suin || Narrative.UnreadNarrativeCount > 0 || Narrative.PendingNarrativeCount > 0);
        protected override void OnClick() { Narrative?.OpenNarrativeJournal(); }
    }
}
