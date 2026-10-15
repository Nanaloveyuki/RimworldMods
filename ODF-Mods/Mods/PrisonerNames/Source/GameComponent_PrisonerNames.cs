using System;
using System.Collections.Generic;
using System.Text;
using Oxygendsl.Core;
using Oxygendsl.Mod;
using RimWorld;
using UnityEngine;
using Verse;

namespace Odf.PrisonerNames
{
    public sealed class PrisonerEntry
    {
        public Pawn Pawn;
        public int Number;
        public string ManualAlias;
        public string Alias;
        public double Score;
        public string Grade;
        public string ManualGrade;
        public bool ManualScore;
        public double? NextThreshold;
    }
    public sealed class RecordView
    {
        public double Score;
        public bool ManualScore;
        public string ManualGrade;
        public ScalarBag Values;
    }

    public sealed class GameComponent_PrisonerNames : GameComponent
    {
        const float RefreshSeconds = 1f;
        const int CustodyDayTicks = 60000;

        string activeSource = string.Empty;
        string draftSource = string.Empty;
        string eventSource = string.Empty;
        string eventDraft = string.Empty;
        bool enabled = true;
        bool customMode;
        string policyText = string.Empty;
        int revision;
        int nextNumber = 1;
        string lastError = string.Empty;
        string lastEventError = string.Empty;
        float elapsed;
        bool restored;
        NamePolicy policy = NamePolicy.CreateDefault();
        NamingRules rules;
        List<SavedEntry> saved = new List<SavedEntry>();
        readonly Dictionary<int, Record> records = new Dictionary<int, Record>();
        readonly Dictionary<int, Pawn> pawns = new Dictionary<int, Pawn>();
        readonly List<PrisonerEntry> published = new List<PrisonerEntry>();
        readonly List<int> retired = new List<int>();
        readonly List<Pending> pending = new List<Pending>();

        public GameComponent_PrisonerNames(Game game) { }

        public static GameComponent_PrisonerNames Current
        {
            get
            {
                if (Verse.Current.ProgramState != ProgramState.Playing || Verse.Current.Game == null) return null;
                return Verse.Current.Game.GetComponent<GameComponent_PrisonerNames>();
            }
        }

        public bool Enabled { get { return enabled; } }
        public bool CustomMode { get { return customMode; } }
        public NamePolicy Policy { get { return policy; } }
        public string ActiveSource { get { return activeSource; } }
        public string DraftSource { get { return draftSource; } set { draftSource = value ?? string.Empty; } }
        public string EventSource { get { return eventSource; } }
        public string EventDraft { get { return eventDraft; } set { eventDraft = value ?? string.Empty; } }
        public string LastError { get { return lastError; } }
        public string LastEventError { get { return lastEventError; } }
        public int Revision { get { return revision; } }
        public IReadOnlyList<PrisonerEntry> Prisoners { get { return published; } }

        public bool Apply(string source)
        {
            return Apply(source, eventSource, true);
        }

        public bool Apply(string source, string events)
        {
            return Apply(source, events, false);
        }

        public bool Apply(string source, string events, bool namingOnly)
        {
            EnsureReady();
            string candidate = source ?? string.Empty;
            string eventCandidate = events ?? string.Empty;
            string keptDraft = namingOnly ? eventDraft : draftSource;
            NamingRules compiled;
            try { compiled = NamingRules.Compile(Combine(candidate, eventCandidate), localization: OxygenLocalization.Instance); }
            catch (Exception exception) { lastError = Describe(exception); return false; }
            if (!TryCommit(compiled, out string error)) { lastError = error; return false; }
            rules = compiled;
            activeSource = candidate;
            eventSource = eventCandidate;
            if (namingOnly) { draftSource = candidate; eventDraft = keptDraft; customMode = true; }
            else { eventDraft = eventCandidate; draftSource = keptDraft; }
            lastError = string.Empty;
            revision++;
            return true;
        }

        static string Combine(string names, string events)
        {
            if (string.IsNullOrWhiteSpace(events)) return names ?? string.Empty;
            return (names ?? string.Empty) + "\n" + events;
        }

        public void SetEnabled(bool value)
        {
            EnsureReady();
            if (enabled == value) return;
            enabled = value;
            RefreshNow();
            revision++;
        }

        public void RefreshNow()
        {
            EnsureReady();
            elapsed = 0f;
            if (!enabled || rules == null)
            {
                if (SyncMembership()) revision++;
                return;
            }
            if (TryCommit(rules, out string error))
            {
                if (!string.IsNullOrEmpty(lastError)) revision++;
                lastError = string.Empty;
            }
            else lastError = error;
        }

        public bool SetManual(Pawn pawn, string alias)
        {
            EnsureReady();
            if (!Eligible(pawn)) return false;
            string normalized;
            try
            {
                normalized = NamingRules.ValidateAlias(alias);
            }
            catch (Exception exception)
            {
                lastError = Describe(exception);
                return false;
            }
            Record record = Ensure(pawn);
            if (string.Equals(record.Manual, normalized, StringComparison.Ordinal)) return true;
            record.Manual = normalized;
            if (!string.IsNullOrEmpty(normalized)) record.Alias = normalized;
            else if (enabled && rules != null)
            {
                try
                {
                    record.Alias = rules.Evaluate(PrisonerFacts.Create(pawn, record.Number, View(record)), GameNow());
                }
                catch (Exception exception)
                {
                    lastError = Describe(exception);
                    Publish(false);
                    revision++;
                    return true;
                }
            }
            else record.Alias = null;
            lastError = string.Empty;
            Publish(false);
            revision++;
            return true;
        }

        public string Preview(string source, Pawn pawn)
        {
            NamingRules compiled = NamingRules.Compile(source ?? string.Empty, localization: OxygenLocalization.Instance);
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            Record record = Find(pawn);
            int number = record == null || record.Number < 1 ? nextNumber : record.Number;
            return compiled.Evaluate(PrisonerFacts.Create(pawn, number, record == null ? null : View(record)), GameNow());
        }

        public string GetAlias(Pawn pawn)
        {
            if (!enabled || !Eligible(pawn)) return null;
            Record record = Find(pawn);
            if (record == null || string.IsNullOrEmpty(record.Alias)) return null;
            return record.Alias;
        }
        public bool SetScore(Pawn pawn, double? score)
        {
            EnsureReady();
            if (!Eligible(pawn)) return false;
            if (score.HasValue && (double.IsNaN(score.Value) || double.IsInfinity(score.Value)))
            {
                lastError = "A score must be a finite number.";
                return false;
            }
            Record record = Ensure(pawn);
            record.ManualScore = score.HasValue;
            if (score.HasValue) record.Score = score.Value;
            RefreshNow();
            revision++;
            return true;
        }

        public bool SetGrade(Pawn pawn, string grade)
        {
            EnsureReady();
            if (!Eligible(pawn)) return false;
            string normalized = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim();
            if (normalized != null && normalized.Length > 40)
            {
                lastError = "A grade may contain at most 40 characters.";
                return false;
            }
            Record record = Ensure(pawn);
            record.ManualGrade = normalized;
            RefreshNow();
            revision++;
            return true;
        }

        public bool TrySetPolicy(NamePolicy candidate, out string error, bool replaceNamingRule = false)
        {
            EnsureReady();
            error = null;
            try { candidate = NormalizePolicy(candidate); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (customMode && !replaceNamingRule)
            {
                policy = candidate;
                policyText = PolicyText(candidate);
                revision++;
                RefreshNow();
                return true;
            }
            string previous = activeSource;
            string previousDraft = draftSource;
            NamePolicy previousPolicy = policy;
            policy = candidate;
            string generated = ConfigSource();
            if (!Apply(generated))
            {
                error = lastError;
                policy = previousPolicy;
                activeSource = previous;
                draftSource = previousDraft;
                RefreshNow();
                return false;
            }
            customMode = false;
            policyText = PolicyText(candidate);
            return true;
        }


        public void UseCustom(string source)
        {
            EnsureReady();
            customMode = true;
            draftSource = source ?? draftSource ?? string.Empty;
            revision++;
        }

        public void Handle(OxygenPawnEvent message)
        {
            if (!ReferenceEquals(Current, this) || message == null || !Eligible(message.Subject)) return;
            EnsureReady();
            if (!enabled) return;
            Record record = Ensure(message.Subject);
            double proposed = record.ManualScore ? record.Score : record.Score + ConfiguredDelta(message);
            if (rules == null || !rules.HasEvents)
            {
                if (!record.ManualScore) record.Score = proposed;
                RefreshNow();
                return;
            }
            ScalarBag copy = record.Values.Copy();
            var state = new EventState(copy, proposed, record.ManualScore);
            try
            {
                rules.EvaluateEvent(PrisonerFacts.Create(message.Subject, record.Number, View(copy, proposed, record.ManualScore, record.ManualGrade)), GameNow(), EventFacts.Create(message), state);
            }
            catch (Exception exception)
            {
                lastEventError = Describe(exception);
                revision++;
                return;
            }
            record.Values.Replace(copy);
            if (!record.ManualScore) record.Score = state.ScoreChanged ? state.Score : proposed;
            lastEventError = string.Empty;
            RefreshNow();
        }

        double ConfiguredDelta(OxygenPawnEvent message)
        {
            if (message == null || message.Facts == null) return 0d;
            object value;
            if (message.Id == "prisoner.resistance_reduced" && message.Facts.TryGetMember("amount", out value))
                return ToFinite(value) * policy.ResistancePerPoint;
            if (message.Id == "prisoner.custody_day") return policy.CustodyDayScore;
            if (message.Id == "prisoner.prison_break") return policy.PrisonBreakScore;
            return 0d;
        }

        static double ToFinite(object value)
        {
            try
            {
                double number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                return double.IsNaN(number) || double.IsInfinity(number) ? 0d : number;
            }
            catch (Exception) { return 0d; }
        }

        static RecordView View(Record record)
        {
            return record == null ? null : View(record.Values, record.Score, record.ManualScore, record.ManualGrade);
        }

        static RecordView View(ScalarBag values, double score, bool manualScore)
        {
            return View(values, score, manualScore, null);
        }

        static RecordView View(ScalarBag values, double score, bool manualScore, string manualGrade)
        {
            return new RecordView { Values = values, Score = score, ManualScore = manualScore, ManualGrade = manualGrade };
        }

        void AdvanceCustody()
        {
            int tick = CurrentTick();
            if (tick < 0) return;
            List<Pawn> colony = ColonyPrisoners();
            bool changed = false;
            for (int i = 0; i < colony.Count; i++)
            {
                Pawn pawn = colony[i];
                if (!Eligible(pawn)) continue;
                Record record = Ensure(pawn);
                if (!record.CustodyKnown || record.LastCustodyTick == int.MinValue || tick < record.LastCustodyTick)
                {
                    record.LastCustodyTick = tick;
                    record.CustodyKnown = true;
                    changed = true;
                    continue;
                }
                int elapsedTicks = tick - record.LastCustodyTick;
                record.LastCustodyTick = tick;
                if (elapsedTicks <= 0 || Verse.Find.TickManager.Paused) continue;
                record.CustodyRemainder += elapsedTicks;
                while (record.CustodyRemainder >= CustodyDayTicks)
                {
                    record.CustodyRemainder -= CustodyDayTicks;
                    PublishCustody(pawn);
                    changed = true;
                }
            }
            if (changed) revision++;
        }

        void PublishCustody(Pawn pawn)
        {
            OxygenEvents.Publish(PrisonerNamesHost.PackageId, "prisoner.custody_day", pawn, new DictionaryOxygenHostValue(new Dictionary<string, object>
            {
                ["tick"] = CurrentTick()
            }));
        }

        static int CurrentTick()
        {
            TickManager ticks = Verse.Find.TickManager;
            return ticks == null ? -1 : ticks.TicksGame;
        }


        internal static string ConfigSource()
        {
            return "fn codename(pawn): string? {\n    return pawn.formatted()\n}\n";
        }

        static NamePolicy NormalizePolicy(NamePolicy value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            NamePolicy copy = value.Clone();
            copy.ValidateNaming();
            if (double.IsNaN(copy.NeutralScore) || double.IsInfinity(copy.NeutralScore))
                throw new ArgumentException("The neutral score must be finite.");
            var seen = new HashSet<double>();
            for (int i = 0; i < copy.Grades.Count; i++)
            {
                GradeBand band = copy.Grades[i];
                if (band == null || string.IsNullOrWhiteSpace(band.Name)) throw new ArgumentException("Every grade needs a name.");
                if (double.IsNaN(band.Minimum) || double.IsPositiveInfinity(band.Minimum)) throw new ArgumentException("A grade threshold must be a number.");
                if (!seen.Add(band.Minimum)) throw new ArgumentException("Grade thresholds must be unique.");
                band.Name = band.Name.Trim();
            }
            if (copy.Grades.Count == 0) copy.Grades.Add(new GradeBand { Minimum = double.NegativeInfinity, Name = NamePolicy.DefaultGrade });
            return copy;
        }

        static string PolicyText(NamePolicy value)
        {
            var text = new StringBuilder();
            text.Append(NamePolicy.StyleName(value.Style)).Append('\n').Append(value.Pattern).Append('\n').Append(value.ChinesePattern).Append('\n');
            text.Append(value.DefaultPrefix).Append('\n').Append(value.ChineseDefaultPrefix).Append('\n');
            text.Append(value.NeutralScore.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            text.Append(value.ResistancePerPoint.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            text.Append(value.CustodyDayScore.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            text.Append(value.PrisonBreakScore.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            for (int i = 0; i < value.Races.Count; i++) text.Append(value.Races[i].Race).Append('=').Append(value.Races[i].Prefix).Append('\n');
            text.Append("---\n");
            for (int i = 0; i < value.Grades.Count; i++)
                text.Append(value.Grades[i].Minimum.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('=').Append(value.Grades[i].Name).Append('\n');
            text.Append("---ages\n").Append(value.UseAgePatterns ? "1" : "0").Append('\n');
            for (int i = 0; i < value.Ages.Count; i++) AppendAge(text, value.Ages[i]);
            for (int i = 0; i < value.RaceNames.Count; i++)
            {
                RaceNameRule race = value.RaceNames[i];
                text.Append("---race\t").Append(Encode(race.Race)).Append('\t').Append(race.Enabled ? "1" : "0").Append('\n');
                for (int j = 0; j < race.Ages.Count; j++) AppendAge(text, race.Ages[j]);
            }
            return text.ToString();
        }

        static void AppendAge(StringBuilder text, AgeNameRule band) => text.Append(band.MinimumAge.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Append('\t').Append(Encode(band.Pattern)).Append('\t').Append(Encode(band.ChinesePattern)).Append('\n');
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

        public override void FinalizeInit()
        {
            Restore();
        }

        public override void LoadedGame()
        {
            Restore();
        }

        public override void StartedNewGame()
        {
            if (string.IsNullOrEmpty(activeSource) && string.IsNullOrEmpty(draftSource))
            {
                customMode = false;
                policyText = PolicyText(policy);
                activeSource = draftSource = ConfigSource();
                eventSource = eventDraft = NamingRules.DefaultEvents();
            }
        }

        public override void GameComponentUpdate()
        {
            if (Verse.Current.ProgramState != ProgramState.Playing) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < RefreshSeconds) return;
            elapsed = 0f;
            EnsureReady();
            AdvanceCustody();
            if (!enabled || rules == null)
            {
                if (SyncMembership()) revision++;
                return;
            }
            string previous = lastError;
            if (TryCommit(rules, out string error))
            {
                if (!string.IsNullOrEmpty(previous)) revision++;
                lastError = string.Empty;
            }
            else if (!string.Equals(previous, error, StringComparison.Ordinal))
            {
                lastError = error;
                revision++;
            }
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                EnsureReady();
                saved = Capture();
            }
            Scribe_Values.Look(ref activeSource, "activeSource", string.Empty);
            Scribe_Values.Look(ref draftSource, "draftSource", string.Empty);
            Scribe_Values.Look(ref eventSource, "eventSource", string.Empty);
            Scribe_Values.Look(ref eventDraft, "eventDraft", string.Empty);
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref customMode, "customMode", false);
            Scribe_Values.Look(ref policyText, "policyText", string.Empty);
            Scribe_Values.Look(ref nextNumber, "nextNumber", 1);
            Scribe_Collections.Look(ref saved, "entries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (activeSource == null) activeSource = string.Empty;
                if (draftSource == null) draftSource = string.Empty;
                if (saved == null) saved = new List<SavedEntry>();
                if (nextNumber < 1) nextNumber = 1;
                restored = false;
            }
        }

        void Restore()
        {
            records.Clear();
            pawns.Clear();
            int highest = 0;
            if (saved != null)
            {
                for (int i = 0; i < saved.Count; i++)
                {
                    SavedEntry entry = saved[i];
                    if (entry == null || entry.Pawn == null || entry.Pawn.Destroyed) continue;
                    int number = entry.Number > highest ? entry.Number : highest;
                    highest = number;
                    var record = new Record
                    {
                        Number = entry.Number < 1 ? 0 : entry.Number,
                        Manual = NormalizeLoaded(entry.Manual),
                        Score = double.IsNaN(entry.Score) || double.IsInfinity(entry.Score) ? NamePolicy.DefaultNeutralScore : entry.Score,
                        ManualScore = entry.ManualScore,
                        ManualGrade = string.IsNullOrWhiteSpace(entry.ManualGrade) ? null : entry.ManualGrade,
                        CustodyRemainder = Math.Max(0, entry.CustodyRemainder),
                        LastCustodyTick = int.MinValue,
                        CustodyKnown = false
                    };
                    RestoreValues(record, entry.Values);
                    records[entry.Pawn.thingIDNumber] = record;
                    pawns[entry.Pawn.thingIDNumber] = entry.Pawn;
                }
            }
            nextNumber = Math.Max(Math.Max(nextNumber, 1), highest + 1);
            bool hadPolicy = !string.IsNullOrEmpty(policyText);
            if (!hadPolicy && !string.IsNullOrEmpty(activeSource)) customMode = true;
            policy = ParsePolicy(policyText);
            if (string.IsNullOrEmpty(activeSource) && string.IsNullOrEmpty(draftSource))
            {
                customMode = false;
                policyText = PolicyText(policy);
                activeSource = draftSource = ConfigSource();
                eventSource = eventDraft = NamingRules.DefaultEvents();
            }
            else if (!customMode) activeSource = ConfigSource();
            if (string.IsNullOrEmpty(eventDraft)) eventDraft = eventSource;
            rules = null;
            if (!string.IsNullOrEmpty(activeSource) || !string.IsNullOrEmpty(eventSource))
            {
                try
                {
                    rules = NamingRules.Compile(Combine(activeSource, eventSource), localization: OxygenLocalization.Instance);
                    lastError = string.Empty;
                }
                catch (Exception exception)
                {
                    rules = null;
                    lastError = Describe(exception);
                }
            }
            restored = true;
            if (enabled && rules != null) TryCommit(rules, out _);
            else SyncMembership();
            revision++;
        }

        void EnsureReady()
        {
            if (!restored && Scribe.mode == LoadSaveMode.Inactive) Restore();
        }

        bool TryCommit(NamingRules candidate, out string error)
        {
            error = null;
            if (candidate == null)
            {
                if (SyncMembership()) revision++;
                return true;
            }
            pending.Clear();
            int reserved = nextNumber;
            List<Pawn> colony = ColonyPrisoners();
            for (int i = 0; i < colony.Count; i++)
            {
                Pawn pawn = colony[i];
                if (!Eligible(pawn)) continue;
                Record record = Find(pawn);
                int number = record != null && record.Number > 0 ? record.Number : reserved++;
                string alias = record == null ? null : record.Manual;
                if (string.IsNullOrEmpty(alias))
                {
                    try
                    {
                        alias = candidate.Evaluate(PrisonerFacts.Create(pawn, number, record == null ? null : View(record)), GameNow());
                    }
                    catch (Exception exception)
                    {
                        error = Describe(exception);
                        if (SyncMembership()) revision++;
                        return false;
                    }
                }
                pending.Add(new Pending(pawn, number, alias));
            }
            bool changed = RetireMissing(colony);
            for (int i = 0; i < pending.Count; i++)
            {
                Pending item = pending[i];
                Record record = Find(item.Pawn);
                if (record == null || record.Number != item.Number || !string.Equals(record.Alias, item.Alias, StringComparison.Ordinal))
                    changed = true;
                record = Ensure(item.Pawn);
                record.Number = item.Number;
                record.Alias = item.Alias;
            }
            if (nextNumber < reserved) nextNumber = reserved;
            Publish(false);
            if (changed) revision++;
            return true;
        }

        bool SyncMembership()
        {
            List<Pawn> colony = ColonyPrisoners();
            bool changed = RetireMissing(colony);
            for (int i = 0; i < colony.Count; i++)
            {
                Pawn pawn = colony[i];
                if (!Eligible(pawn)) continue;
                if (!records.ContainsKey(pawn.thingIDNumber))
                {
                    Ensure(pawn);
                    changed = true;
                }
            }
            if (!enabled) changed |= ClearShown();
            Publish(false);
            return changed;
        }

        bool RetireMissing(List<Pawn> colony)
        {
            retired.Clear();
            foreach (KeyValuePair<int, Pawn> pair in pawns)
            {
                bool active = false;
                for (int i = 0; i < colony.Count; i++)
                {
                    if (colony[i] == pair.Value && Eligible(colony[i]))
                    {
                        active = true;
                        break;
                    }
                }
                if (!active) retired.Add(pair.Key);
            }
            bool changed = false;
            for (int i = 0; i < retired.Count; i++)
            {
                Record record;
                if (!records.TryGetValue(retired[i], out record)) continue;
                if (record.Alias != null) { record.Alias = null; changed = true; }
                if (record.CustodyKnown) { record.CustodyKnown = false; record.LastCustodyTick = int.MinValue; changed = true; }
            }
            return changed;
        }

        bool ClearShown()
        {
            bool changed = false;
            foreach (KeyValuePair<int, Record> pair in records)
            {
                if (pair.Value.Alias == null) continue;
                pair.Value.Alias = null;
                changed = true;
            }
            return changed;
        }

        void Publish(bool includeInactive)
        {
            published.Clear();
            foreach (KeyValuePair<int, Record> pair in records)
            {
                Pawn pawn;
                if (!pawns.TryGetValue(pair.Key, out pawn) || pawn == null || pawn.Destroyed) continue;
                if (!includeInactive && !Eligible(pawn)) continue;
                string grade = string.IsNullOrEmpty(pair.Value.ManualGrade) ? policy.GradeFor(pair.Value.Score) : pair.Value.ManualGrade;
                published.Add(new PrisonerEntry
                {
                    Pawn = pawn,
                    Number = pair.Value.Number,
                    ManualAlias = pair.Value.Manual,
                    Alias = enabled && Eligible(pawn) ? pair.Value.Alias : null,
                    Score = pair.Value.Score,
                    Grade = grade,
                    ManualGrade = pair.Value.ManualGrade,
                    ManualScore = pair.Value.ManualScore,
                    NextThreshold = policy.NextThreshold(pair.Value.Score)
                });
            }
            published.Sort(Compare);
        }

        Record Ensure(Pawn pawn)
        {
            pawns[pawn.thingIDNumber] = pawn;
            Record record;
            if (!records.TryGetValue(pawn.thingIDNumber, out record))
            {
                record = new Record { Number = nextNumber++, Score = policy.NeutralScore };
                records[pawn.thingIDNumber] = record;
            }
            else if (record.Number < 1) record.Number = nextNumber++;
            if (nextNumber <= record.Number) nextNumber = record.Number + 1;
            return record;
        }

        Record Find(Pawn pawn)
        {
            if (pawn == null) return null;
            Record record;
            return records.TryGetValue(pawn.thingIDNumber, out record) ? record : null;
        }

        List<SavedEntry> Capture()
        {
            var list = new List<SavedEntry>(records.Count);
            foreach (KeyValuePair<int, Record> pair in records)
            {
                Pawn pawn;
                if (!pawns.TryGetValue(pair.Key, out pawn) || pawn == null || pawn.Destroyed) continue;
                list.Add(Capture(pawn, pair.Value));
            }
            return list;
        }
        static SavedEntry Capture(Pawn pawn, Record record)
        {
            var entry = new SavedEntry
            {
                Pawn = pawn,
                Number = record.Number,
                Manual = record.Manual,
                Score = record.Score,
                ManualScore = record.ManualScore,
                ManualGrade = record.ManualGrade,
                CustodyRemainder = record.CustodyRemainder,
                LastCustodyTick = record.LastCustodyTick,
                CustodyKnown = record.CustodyKnown,
                Values = new List<SavedValue>()
            };
            foreach (KeyValuePair<string, ScalarValue> pair in record.Values.Entries())
            {
                object raw = pair.Value.Unwrap();
                var value = new SavedValue { Key = pair.Key };
                if (raw == null) value.Kind = "null";
                else if (raw is bool flag) { value.Kind = "bool"; value.Bool = flag; }
                else if (raw is string text) { value.Kind = "string"; value.Text = text; }
                else { value.Kind = "number"; value.Number = Convert.ToDouble(raw, System.Globalization.CultureInfo.InvariantCulture); }
                entry.Values.Add(value);
            }
            return entry;
        }

        static void RestoreValues(Record record, List<SavedValue> values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                SavedValue value = values[i];
                if (value == null || string.IsNullOrEmpty(value.Key)) continue;
                if (value.Kind == "null") record.Values.Set(value.Key, null);
                else if (value.Kind == "bool") record.Values.Set(value.Key, value.Bool);
                else if (value.Kind == "string") record.Values.Set(value.Key, value.Text ?? string.Empty);
                else if (value.Kind == "number" && !double.IsNaN(value.Number) && !double.IsInfinity(value.Number)) record.Values.Set(value.Key, value.Number);
            }
        }

        static NamePolicy ParsePolicy(string text)
        {
            NamePolicy parsed = NamePolicy.CreateDefault();
            if (string.IsNullOrEmpty(text)) return parsed;
            parsed.UseAgePatterns = false;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 9) return parsed;
            NameStyle style;
            if (NamePolicy.TryParseStyle(lines[0], out style)) parsed.Style = style;
            parsed.Pattern = lines[1];
            parsed.ChinesePattern = lines[2];
            parsed.DefaultPrefix = lines[3];
            parsed.ChineseDefaultPrefix = lines[4];
            double neutral;
            if (double.TryParse(lines[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out neutral)
                && !double.IsNaN(neutral) && !double.IsInfinity(neutral)) parsed.NeutralScore = neutral;
            parsed.ResistancePerPoint = Finite(lines[6], 0d);
            parsed.CustodyDayScore = Finite(lines[7], 0d);
            parsed.PrisonBreakScore = Finite(lines[8], 0d);
            parsed.Races.Clear();
            parsed.Grades.Clear();
            bool grades = false;
            bool ages = false;
            RaceNameRule race = null;
            for (int i = 9; i < lines.Length; i++)
            {
                if (lines[i] == "---ages")
                {
                    ages = true;
                    parsed.Ages.Clear();
                    parsed.RaceNames.Clear();
                    parsed.UseAgePatterns = ++i < lines.Length && lines[i] == "1";
                    continue;
                }
                if (ages)
                {
                    if (string.IsNullOrEmpty(lines[i])) continue;
                    string[] parts = lines[i].Split('\t');
                    if (parts.Length != 3) throw new ArgumentException("Invalid saved age rule.");
                    if (parts[0] == "---race")
                    {
                        race = new RaceNameRule { Race = Decode(parts[1]), Enabled = parts[2] == "1" };
                        parsed.RaceNames.Add(race);
                    }
                    else
                    {
                        var band = new AgeNameRule { MinimumAge = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), Pattern = Decode(parts[1]), ChinesePattern = Decode(parts[2]) };
                        (race == null ? parsed.Ages : race.Ages).Add(band);
                    }
                    continue;
                }
                if (lines[i] == "---") { grades = true; continue; }
                int split = lines[i].IndexOf('=');
                if (split <= 0) continue;
                string left = lines[i].Substring(0, split);
                string right = lines[i].Substring(split + 1);
                if (!grades) parsed.Races.Add(new RacePrefix { Race = left, Prefix = right });
                else
                {
                    double minimum;
                    if (double.TryParse(left, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out minimum))
                        parsed.Grades.Add(new GradeBand { Minimum = minimum, Name = right });
                }
            }
            return parsed;
        }

        static double Finite(string text, double fallback)
        {
            double value;
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) return fallback;
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : value;
        }



        static List<Pawn> ColonyPrisoners()
        {
            return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_PrisonersOfColony;
        }

        static bool Eligible(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && !pawn.Dead && pawn.RaceProps != null && pawn.RaceProps.Humanlike && pawn.IsPrisonerOfColony;
        }

        static GameContext GameNow()
        {
            TickManager ticks = Verse.Find.TickManager;
            int tick = ticks == null ? 0 : ticks.TicksAbs;
            return new GameContext { Tick = tick, Hour = GenDate.HourFloat(tick, 0f) };
        }

        static string NormalizeLoaded(string alias)
        {
            try { return NamingRules.ValidateAlias(alias); }
            catch (Exception) { return null; }
        }

        static string Describe(Exception exception)
        {
            ScriptEvaluationException script = exception as ScriptEvaluationException;
            if (script != null) return script.Line + ":" + script.Column + " " + script.Message;
            return exception.Message;
        }

        static int Compare(PrisonerEntry left, PrisonerEntry right)
        {
            int number = left.Number.CompareTo(right.Number);
            if (number != 0) return number;
            return string.Compare(PrisonerFacts.OriginalShort(left.Pawn), PrisonerFacts.OriginalShort(right.Pawn), StringComparison.Ordinal);
        }

        sealed class Record
        {
            public int Number;
            public string Manual;
            public string Alias;
            public double Score = NamePolicy.DefaultNeutralScore;
            public bool ManualScore;
            public string ManualGrade;
            public int CustodyRemainder;
            public int LastCustodyTick = int.MinValue;
            public bool CustodyKnown;
            public readonly ScalarBag Values = new ScalarBag();
        }

        struct Pending
        {
            public readonly Pawn Pawn;
            public readonly int Number;
            public readonly string Alias;
            public Pending(Pawn pawn, int number, string alias)
            {
                Pawn = pawn;
                Number = number;
                Alias = alias;
            }
        }

        public sealed class SavedEntry : IExposable
        {
            public Pawn Pawn;
            public int Number;
            public string Manual;
            public double Score = NamePolicy.DefaultNeutralScore;
            public bool ManualScore;
            public string ManualGrade;
            public int CustodyRemainder;
            public int LastCustodyTick = int.MinValue;
            public bool CustodyKnown;
            public List<SavedValue> Values = new List<SavedValue>();

            public void ExposeData()
            {
                Scribe_References.Look(ref Pawn, "pawn");
                Scribe_Values.Look(ref Number, "number", 0);
                Scribe_Values.Look(ref Manual, "manual");
                Scribe_Values.Look(ref Score, "score", NamePolicy.DefaultNeutralScore);
                Scribe_Values.Look(ref ManualScore, "manualScore", false);
                Scribe_Values.Look(ref ManualGrade, "manualGrade");
                Scribe_Values.Look(ref CustodyRemainder, "custodyRemainder", 0);
                Scribe_Values.Look(ref LastCustodyTick, "lastCustodyTick", int.MinValue);
                Scribe_Values.Look(ref CustodyKnown, "custodyKnown", false);
                Scribe_Collections.Look(ref Values, "values", LookMode.Deep);
                if (Scribe.mode == LoadSaveMode.LoadingVars && Values == null) Values = new List<SavedValue>();
            }
        }

        public sealed class SavedValue : IExposable
        {
            public string Key;
            public string Kind;
            public bool Bool;
            public string Text;
            public double Number;

            public void ExposeData()
            {
                Scribe_Values.Look(ref Key, "key");
                Scribe_Values.Look(ref Kind, "kind");
                Scribe_Values.Look(ref Bool, "bool", false);
                Scribe_Values.Look(ref Text, "text");
                Scribe_Values.Look(ref Number, "number", 0d);
            }
        }
    }
}
