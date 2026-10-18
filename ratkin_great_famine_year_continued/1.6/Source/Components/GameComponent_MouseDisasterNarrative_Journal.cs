using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MouseDisaster
{
    public enum NarrativePawnEnd { Pending, Left, Settled, Dead, Detained, Missing }

    public sealed class NarrativePawnObservation : IExposable
    {
        public Pawn pawn;
        public int careTicks;
        public int missingSince = -1;
        public NarrativePawnEnd end;
        public bool identityChanged;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref careTicks, "careTicks");
            Scribe_Values.Look(ref missingSince, "missingSince", -1);
            Scribe_Values.Look(ref end, "end");
            Scribe_Values.Look(ref identityChanged, "identityChanged", false);
        }
    }

    public sealed class NarrativeVisit : IExposable
    {
        public int id;
        public int mapId;
        public int created;
        public string scene;
        public List<NarrativePawnObservation> people = new List<NarrativePawnObservation>();
        public bool delivered;
        public bool driven;
        public int deliveredTick = -1;
        public int drivenTick = -1;
        public bool resolved;
        public bool counted;
        public bool showLetter = true;
        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref mapId, "mapId");
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref scene, "scene");
            Scribe_Collections.Look(ref people, "people", LookMode.Deep);
            Scribe_Values.Look(ref delivered, "delivered");
            Scribe_Values.Look(ref driven, "driven");
            Scribe_Values.Look(ref deliveredTick, "deliveredTick", -1);
            Scribe_Values.Look(ref drivenTick, "drivenTick", -1);
            Scribe_Values.Look(ref resolved, "resolved");
            Scribe_Values.Look(ref counted, "counted");
            Scribe_Values.Look(ref showLetter, "showLetter", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                people ??= new List<NarrativePawnObservation>();
                people.RemoveAll(p => p == null);
            }
        }
    }

    public sealed class NarrativeVisitSummary : IExposable
    {
        public int id;
        public string scene;
        public int created;
        public int completed = -1;
        public bool delivered, driven, counted;
        public int deliveredTick = -1, drivenTick = -1;
        public int left, settled, dead, detained, missing, identityChanged;

        public static NarrativeVisitSummary FromVisit(NarrativeVisit visit, int completedTick)
        {
            var summary = new NarrativeVisitSummary
            {
                id = visit.id, scene = visit.scene, created = visit.created, completed = completedTick,
                delivered = visit.delivered, driven = visit.driven, counted = visit.counted,
                deliveredTick = visit.deliveredTick, drivenTick = visit.drivenTick
            };
            foreach (var person in visit.people)
            {
                switch (person.end)
                {
                    case NarrativePawnEnd.Left: summary.left++; break;
                    case NarrativePawnEnd.Settled: summary.settled++; break;
                    case NarrativePawnEnd.Dead: summary.dead++; break;
                    case NarrativePawnEnd.Detained: summary.detained++; break;
                    default: summary.missing++; break;
                }
                if (person.identityChanged) summary.identityChanged++;
            }
            return summary;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref scene, "scene");
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref completed, "completed", -1);
            Scribe_Values.Look(ref delivered, "delivered");
            Scribe_Values.Look(ref driven, "driven");
            Scribe_Values.Look(ref counted, "counted");
            Scribe_Values.Look(ref deliveredTick, "deliveredTick", -1);
            Scribe_Values.Look(ref drivenTick, "drivenTick", -1);
            Scribe_Values.Look(ref left, "left");
            Scribe_Values.Look(ref settled, "settled");
            Scribe_Values.Look(ref dead, "dead");
            Scribe_Values.Look(ref detained, "detained");
            Scribe_Values.Look(ref missing, "missing");
            Scribe_Values.Look(ref identityChanged, "identityChanged");
        }
    }

    public partial class GameComponent_MouseDisasterNarrative
    {
        private List<NarrativeVisit> narrativeVisits = new List<NarrativeVisit>();
        private List<NarrativeVisitSummary> narrativeVisitSummaries = new List<NarrativeVisitSummary>();
        private List<string> narrativeFlags = new List<string>();
        private int nextVisitId = 1;
        private int aidCompleted;
        private int successfulBroadcasts;
        private int forceDepartures;
        private int nextEchoTick;
        private int nextPopulationTick;
        private int adultRatkinCount;
        private int firstNarrativeTick = -1;

        private void ExposeJournalData()
        {
            Scribe_Collections.Look(ref countedNarrativeFlags, "countedNarrativeFlags", LookMode.Value);
            Scribe_Collections.Look(ref narrativeVisits, "narrativeVisits", LookMode.Deep);
            Scribe_Collections.Look(ref narrativeVisitSummaries, "narrativeVisitSummaries", LookMode.Deep);
            Scribe_Collections.Look(ref narrativeFlags, "narrativeFlags", LookMode.Value);
            Scribe_Values.Look(ref nextVisitId, "nextVisitId", 1);
            Scribe_Values.Look(ref aidCompleted, "aidCompleted");
            Scribe_Values.Look(ref successfulBroadcasts, "successfulBroadcasts");
            Scribe_Values.Look(ref forceDepartures, "forceDepartures");
            Scribe_Values.Look(ref nextEchoTick, "nextEchoTick");
            Scribe_Values.Look(ref nextPopulationTick, "nextPopulationTick");
            Scribe_Values.Look(ref adultRatkinCount, "adultRatkinCount");
            Scribe_Values.Look(ref firstNarrativeTick, "firstNarrativeTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                narrativeVisits ??= new List<NarrativeVisit>();
                narrativeVisits.RemoveAll(v => v == null);
                narrativeFlags ??= new List<string>();
                countedNarrativeFlags ??= new List<string>(narrativeFlags);
                narrativeFlags.RemoveAll(string.IsNullOrWhiteSpace);
                narrativeVisitSummaries ??= new List<NarrativeVisitSummary>();
                narrativeVisitSummaries.RemoveAll(v => v == null);
                nextVisitId = System.Math.Max(nextVisitId, narrativeVisits.Select(v => v.id)
                    .Concat(narrativeVisitSummaries.Select(v => v.id)).DefaultIfEmpty(0).Max() + 1);
                ArchiveResolvedVisits();
            }
        }

        private void ArchiveResolvedVisits()
        {
            foreach (var visit in narrativeVisits)
                if (visit.resolved)
                    // Old saves have no completion timestamp; do not invent one or replay rewards.
                    narrativeVisitSummaries.Add(NarrativeVisitSummary.FromVisit(visit, -1));
            narrativeVisits.RemoveAll(v => v.resolved);
        }

        public int TrackNarrativeVisit(string scene, Map map, IEnumerable<Pawn> pawns)
        {
            if (scene == null || map == null) return -1;
            var visit = new NarrativeVisit { id = nextVisitId++, scene = scene, mapId = map.uniqueID, created = CurrentNarrativeTick };
            visit.showLetter = NarrativeEnabled(scene);
            visit.people = (pawns ?? Enumerable.Empty<Pawn>()).Where(p => p != null).Distinct()
                .Select(p => new NarrativePawnObservation { pawn = p }).ToList();
            if (visit.people.Count == 0) return -1;
            narrativeVisits.Add(visit);
            if (CountsNarrativeState && firstNarrativeTick < 0) firstNarrativeTick = CurrentNarrativeTick;
            return visit.id;
        }

        public void NotifyNarrativeDelivery(IEnumerable<Pawn> pawns)
        {
            var set = new HashSet<Pawn>(pawns ?? Enumerable.Empty<Pawn>());
            foreach (var visit in narrativeVisits.Where(v => !v.resolved && v.people.Any(p => set.Contains(p.pawn))))
            {
                if (!visit.delivered) visit.deliveredTick = CurrentNarrativeTick;
                visit.delivered = true;
            }
        }

        public void NotifyNarrativeForce(IEnumerable<Pawn> pawns)
        {
            var set = new HashSet<Pawn>(pawns ?? Enumerable.Empty<Pawn>());
            foreach (var visit in narrativeVisits.Where(v => !v.resolved && !v.driven && v.people.Any(p => set.Contains(p.pawn))))
            {
                visit.driven = true;
                visit.drivenTick = CurrentNarrativeTick;
                if (CountsNarrativeState) forceDepartures++;
                ChangeNarratorTrust(-2);
            }
        }

        public void NotifyNarrativeBroadcast() { if (CountsNarrativeState) successfulBroadcasts++; }

        public void NotifyNarrativeExit(Pawn pawn, int mapId)
        {
            if (pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony) return;
            foreach (var visit in narrativeVisits.Where(v => !v.resolved && v.mapId == mapId))
                foreach (var person in visit.people.Where(p => p.pawn == pawn && p.end == NarrativePawnEnd.Pending))
                    person.end = NarrativePawnEnd.Left;
            foreach (var record in n007Records.Where(r => r.mapId == mapId && r.phase != MouseDisasterN007Phase.Resolved))
            {
                var person = record.pawnRecords.FirstOrDefault(p => p.pawn == pawn && !p.handled && !p.died);
                if (person == null) continue;
                person.leftMap = true;
                person.recovered = !HasN007Plague(pawn);
            }
        }

        public void NotifyNarrativeVisitorIdentityChanged(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            foreach (var visit in narrativeVisits.Where(v => !v.resolved))
            {
                foreach (var person in visit.people.Where(p => p.pawn == pawn))
                {
                    person.identityChanged = true;
                }
            }
        }

        public void RecordCaravanNarrative(bool paid)
        {
            string key = paid ? "S12Paid" : "S12Fight";
            CompleteNarrativeFlag("S12");
            if (NarrativeEnabled("S12")) SendJournalOnce(key, null);
        }

        private List<string> countedNarrativeFlags = new List<string>();

        private void CompleteNarrativeFlag(string key)
        {
            // Operational flags always deduplicate rewards; only progression respects the counter toggle.
            if (CountsNarrativeState && !countedNarrativeFlags.Contains(key)) countedNarrativeFlags.Add(key);
            if (!narrativeFlags.Contains(key)) narrativeFlags.Add(key);
            if (CountsNarrativeState && firstNarrativeTick < 0) firstNarrativeTick = CurrentNarrativeTick;
        }

        private void SendJournalOnce(string key, Map map, params NamedArgument[] args)
        {
            if (narrativeFlags.Contains("Letter:" + key)) return;
            CompleteNarrativeFlag("Letter:" + key);
            if (IsNarratorActive())
                ReceiveNarrativeLetterText(("MouseDisaster_Story_" + key + "_Label").Translate(),
                    ("MouseDisaster_Story_" + key + "_Text").Translate(args), map);
        }

        private void TryNarrativeEcho(string key, Map map)
        {
            if (!NarrativeEnabled("Echo") || !IsNarratorActive() || CurrentNarrativeTick < nextEchoTick ||
                narrativeFlags.Contains("Letter:" + key) || !Rand.Chance(MouseDisasterNarrativePolicy.ScaledChance(
                    MouseDisasterNarrativePolicy.EchoChance(narratorTrust), MouseDisasterMod.Settings?.narrativeEchoChancePercent ?? 100f))) return;
            nextEchoTick = CurrentNarrativeTick + GenDate.TicksPerDay * (MouseDisasterMod.Settings?.narrativeEchoCooldownDays ?? 3);
            SendJournalOnce(key, map);
        }

        internal static bool NarrativeInCare(Pawn pawn, bool allowCaptiveCare = false)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed &&
                ((pawn.Faction == Faction.OfPlayer && !pawn.IsPrisoner && !pawn.IsSlave) ||
                    allowCaptiveCare && (pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)) &&
                (pawn.MapHeld?.IsPlayerHome == true || pawn.IsPlayerControlledCaravanMember());
        }

        internal static bool NarrativeCareHealthy(Pawn pawn)
        {
            return pawn?.needs?.food?.CurLevelPercentage > 0.15f && pawn.health?.hediffSet?.HasHediff(HediffDefOf.Malnutrition) != true &&
                pawn.health?.hediffSet?.HasHediff(MouseDisasterDefOf.MouseDisaster_Plague) != true;
        }

        private void ScanNarrativePawn(NarrativePawnObservation person, int mapId, int created, bool allowCaptiveCare = false)
        {
            if (person.end != NarrativePawnEnd.Pending) return;
            Pawn p = person.pawn;
            if (p == null)
            {
                if (person.missingSince < 0) person.missingSince = CurrentNarrativeTick;
                if (CurrentNarrativeTick - person.missingSince >= MouseDisasterNarrativePolicy.MissingGraceTicks)
                    person.end = NarrativePawnEnd.Missing;
                return;
            }
            person.missingSince = -1;
            if (p.Dead) person.end = NarrativePawnEnd.Dead;
            else if (p.Destroyed) person.end = NarrativePawnEnd.Missing;
            else if ((p.IsPrisoner || p.IsSlave) && !allowCaptiveCare) person.end = NarrativePawnEnd.Detained;
            else if (NarrativeInCare(p, allowCaptiveCare))
            {
                if (NarrativeCareHealthy(p)) person.careTicks += MouseDisasterNarrativePolicy.CheckTicks;
                if (person.careTicks >= MouseDisasterNarrativePolicy.CareTicks) person.end = NarrativePawnEnd.Settled;
            }
            else if (!Find.Maps.Any(m => m.uniqueID == mapId)) person.end = NarrativePawnEnd.Missing;
            else if (p.MapHeld?.uniqueID != mapId) person.end = NarrativePawnEnd.Left;
            if (person.end == NarrativePawnEnd.Pending && CurrentNarrativeTick - created >= MouseDisasterNarrativePolicy.ObservationTicks)
                person.end = NarrativePawnEnd.Missing;
        }

        private void ProcessNarrativeJournal()
        {
            ProcessOpeningNarrative(Find.AnyPlayerHomeMap);
            for (int visitIndex = 0; visitIndex < narrativeVisits.Count; visitIndex++)
            {
                var visit = narrativeVisits[visitIndex];
                if (visit.resolved)
                {
                    narrativeVisitSummaries.Add(NarrativeVisitSummary.FromVisit(visit, -1));
                    narrativeVisits.RemoveAt(visitIndex--);
                    continue;
                }
                if (visit.scene == "S03")
                {
                    var children = visit.people.Select(p => p.pawn).Where(p => p?.relations != null)
                        .SelectMany(p => p.relations.Children)
                        .Where(p => p != null && p.ageTracker.AgeChronologicalTicks <= CurrentNarrativeTick - visit.created + MouseDisasterNarrativePolicy.CheckTicks)
                        .Distinct().ToList();
                    foreach (var child in children)
                        if (!visit.people.Any(p => p.pawn == child)) visit.people.Add(new NarrativePawnObservation { pawn = child });
                }
                foreach (var person in visit.people) ScanNarrativePawn(person, visit.mapId, visit.created);
                if (visit.people.Any(p => p.end == NarrativePawnEnd.Pending)) continue;
                visit.resolved = true;
                var summary = NarrativeVisitSummary.FromVisit(visit, CurrentNarrativeTick);
                int left = summary.left, settled = summary.settled, dead = summary.dead,
                    detained = summary.detained, missing = summary.missing;
                if (CountsNarrativeState && MouseDisasterNarrativePolicy.IsAidComplete(
                    visit.delivered && visit.scene != "S07", visit.driven, visit.people.Count, left, settled,
                    summary.identityChanged,
                    visit.people.Count(p => p.identityChanged && p.end == NarrativePawnEnd.Detained)))
                {
                    visit.counted = true;
                    summary.counted = true;
                    aidCompleted++;
                    ChangeNarratorTrust(2);
                }
                string outcome = dead > 0 ? "Dead" : detained > 0 ? "Detained" : missing > 0 ? "Missing" : settled > 0 ? "Settled" : "Left";
                CompleteNarrativeFlag(visit.scene);
                string signature = "Visit:" + visit.scene + ":" + outcome + ":" + visit.delivered;
                if (visit.showLetter && !narrativeFlags.Contains(signature) && IsNarratorActive())
                {
                    CompleteNarrativeFlag(signature);
                    Map map = Find.Maps.FirstOrDefault(m => m.uniqueID == visit.mapId);
                    string body = ("MouseDisaster_Story_" + visit.scene + "_Text").Translate() + "\n\n" +
                        "MouseDisaster_Story_VisitCounts".Translate(left, settled, dead, detained, missing) + "\n" +
                        (visit.counted ? "MouseDisaster_Story_AidCounted" : "MouseDisaster_Story_AidNotCounted").Translate();
                    ReceiveNarrativeLetterText(("MouseDisaster_Story_" + visit.scene + "_Label").Translate(), body, map);
                }
                narrativeVisitSummaries.Add(summary);
                narrativeVisits.RemoveAt(visitIndex--);
            }
            ProcessN005Care();
            ProcessStoryTasks();
            ProcessNarrativeEndings();
            if (narrativeFlags.Contains("N004") && narrativeFlags.Contains("N005")) TryNarrativeEcho("BridgeChild", Find.AnyPlayerHomeMap);
            if (narrativeFlags.Contains("N006Trace") && storySite != null) TryNarrativeEcho("BridgeGrain", Find.AnyPlayerHomeMap);
            if (narrativeFlags.Contains("N007Recovered") && envoyPhase > 0) TryNarrativeEcho("BridgePlague", Find.AnyPlayerHomeMap);
            if (envoyTraded && relicOutcome > 0) TryNarrativeEcho("BridgeEnvoy", Find.AnyPlayerHomeMap);
        }

        private void ProcessNarrativeEndings()
        {
            if (!NarrativeEnabled("N010")) return;
            if (CountsNarrativeState && CurrentNarrativeTick >= nextPopulationTick)
            {
                nextPopulationTick = CurrentNarrativeTick + GenDate.TicksPerYear;
                adultRatkinCount = Find.Maps.Where(m => m.IsPlayerHome).Select(m => m.mapPawns.FreeColonists
                    .Count(p => p.DevelopmentalStage == DevelopmentalStage.Adult && MouseDisasterUtility.IsRatkin(p) &&
                        MouseDisasterUtility.IsMouseDisasterPawn(p))).DefaultIfEmpty(0).Max();
            }
            var settings = MouseDisasterMod.Settings;
            if (!IsNarratorActive() && settings?.endingsWithoutSuin == false) return;
            int completed = countedNarrativeFlags.Count(f => (f.Length == 3 && f[0] == 'S') || (f.Length == 4 && f[0] == 'N'));
            bool mature = completed >= 6 && firstNarrativeTick >= 0 && CurrentNarrativeTick - firstNarrativeTick >=
                GenDate.TicksPerDay * (settings?.narrativeEndingDelayDays ?? 30);
            if (completed > 0 && narratorTrust <= -75) mature = true;
            string ending = MouseDisasterNarrativePolicy.Ending(narratorTrust, IsNarratorActive(), aidCompleted, successfulBroadcasts,
                forceDepartures, adultRatkinCount, settings?.narrativeAidGoal ?? 99, settings?.narrativeBroadcastGoal ?? 3,
                settings?.narrativeDriveLimit ?? 3, settings?.narrativeAdultGoal ?? 100, mature, NarrativeEnabled);
            if (ending != null && NarrativeEnabled(ending) && !narrativeFlags.Contains(ending))
            {
                // A lower ending never replaces a previously achieved higher milestone.
                if (!narrativeFlags.Contains("E02") && !(narrativeFlags.Contains("E01") && ending != "E02"))
                {
                    CompleteNarrativeFlag(ending);
                    string key = IsNarratorActive() ? ending : "PublicEnding";
                    ShowNarrativeEnding(("MouseDisaster_Story_" + key + "_Label").Translate(),
                        ("MouseDisaster_Story_" + key + "_Text").Translate(aidCompleted, successfulBroadcasts, adultRatkinCount));
                }
            }
        }
    }
}
