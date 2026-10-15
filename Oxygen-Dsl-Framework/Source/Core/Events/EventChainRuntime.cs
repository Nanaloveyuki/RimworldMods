using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Oxygendsl.Core.Events
{
    // All live operations belong to the constructing game thread. Persistence setters
    // are a load/migration boundary, not an alternative way to mutate the live index.
    public sealed class EventChainRuntime
    {
        public const int ApiVersion = 1;
        public const int SchemaVersion = 1;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly List<EventChainProviderState> states;
        private readonly IReadOnlyList<EventChainProviderState> stateView;
        private readonly Dictionary<string, Registration> registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
        private readonly Dictionary<string, Partition> partitions = new Dictionary<string, Partition>(StringComparer.Ordinal);
        private readonly List<Entry> heap = new List<Entry>();
        private Entry callback;
        private bool ready, ticking, preparing;
        private int activeCount, currentTick;
        private long callbackCount;

        public EventChainRuntime(List<EventChainProviderState> states)
        {
            this.states = states ?? throw new ArgumentNullException(nameof(states));
            stateView = states.AsReadOnly();
            foreach (EventChainProviderState state in states)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.ProviderId))
                    throw new ArgumentException("A provider partition has no owner.", nameof(states));
                if (partitions.ContainsKey(state.ProviderId))
                    throw new ArgumentException("Duplicate provider partition: " + state.ProviderId, nameof(states));
                partitions.Add(state.ProviderId, new Partition(state));
            }
        }

        public IReadOnlyList<EventChainProviderState> States { get { CheckThread(); return stateView; } }
        public int ActiveCount { get { CheckThread(); return activeCount; } }
        public int NextDueTick { get { CheckThread(); return heap.Count == 0 ? -1 : heap[0].Due; } }
        public long CallbackCount { get { CheckThread(); return callbackCount; } }

        public void Register(IEventChainProvider provider)
        {
            CheckThread();
            if (callback != null || ticking) throw new InvalidOperationException("Providers cannot change during an EventChain callback.");
            Registration candidate = new Registration(provider);
            Registration previous;
            if (registrations.TryGetValue(candidate.Id, out previous))
            {
                if (ReferenceEquals(previous.Provider, provider)) return;
                if (previous.Provider.StateVersion != provider.StateVersion)
                    throw new InvalidOperationException("Provider state version replacement is incompatible: " + candidate.Id);
                Partition partition;
                if (partitions.TryGetValue(candidate.Id, out partition))
                {
                    if (partition.State.SchemaVersion != SchemaVersion || partition.State.StateVersion != provider.StateVersion)
                        throw new InvalidOperationException("Loaded provider state is incompatible: " + candidate.Id);
                    var prepared = new Dictionary<int, object>();
                    if (partition.State.Instances != null)
                        foreach (EventChainInstance instance in partition.State.Instances)
                        {
                            if (instance == null || instance.End != EventChainEnd.None) continue;
                            IEventChainDefinition definition;
                            if (!candidate.Definitions.TryGetValue(instance.DefinitionId, out definition) || definition.Version != instance.DefinitionVersion)
                                throw new InvalidOperationException("Definition replacement is incompatible: " + instance.DefinitionId);
                            EventChainInstance copy = Copy(instance);
                            copy.PreparedState = null;
                            Prepare(definition, copy);
                            prepared.Add(instance.Id, copy.PreparedState);
                        }
                    // No mutation before every active definition and payload has passed.
                    foreach (EventChainInstance instance in partition.State.Instances ?? new List<EventChainInstance>())
                        if (instance != null && instance.End == EventChainEnd.None) instance.PreparedState = prepared[instance.Id];
                }
            }
            registrations[candidate.Id] = candidate;
            if (ready)
            {
                Partition partition;
                if (!partitions.TryGetValue(candidate.Id, out partition))
                {
                    partition = AddPartition(new EventChainProviderState { ProviderId = candidate.Id, StateVersion = provider.StateVersion });
                }
                Rebuild(partition, candidate, previous == null);
            }
        }

        public void Activate(int tick)
        {
            CheckThread();
            if (callback != null || ticking) throw new InvalidOperationException("Cannot activate during a callback.");
            if (ready) return;
            currentTick = tick;
            foreach (Registration registration in registrations.Values)
                if (!partitions.ContainsKey(registration.Id))
                    AddPartition(new EventChainProviderState { ProviderId = registration.Id, StateVersion = registration.Provider.StateVersion });
            foreach (Partition partition in partitions.Values)
            {
                Registration registration;
                registrations.TryGetValue(partition.State.ProviderId, out registration);
                Rebuild(partition, registration, true);
                if (partition.Reason.Length == 0 && partition.State.Mode != EventChainMode.Active && partition.State.PausedTick < 0)
                    partition.State.PausedTick = tick;
            }
            ready = true;
        }

        public bool HasProviderState(string providerId) { CheckThread(); return providerId != null && partitions.ContainsKey(providerId); }
        public EventChainProviderState GetProviderState(string providerId)
        { CheckThread(); Partition partition; return providerId != null && partitions.TryGetValue(providerId, out partition) ? partition.State : null; }

        public bool Import(string providerId, IReadOnlyList<EventChainInstance> instances, int nextId, string migrationStatus, out string error)
        {
            CheckThread();
            error = "";
            if (callback != null || ticking) { error = "Cannot import during a callback."; return false; }
            if (string.IsNullOrWhiteSpace(providerId)) { error = "Provider owner is missing."; return false; }
            if (partitions.ContainsKey(providerId)) { error = "An authoritative provider partition already exists."; return false; }
            Registration registration;
            if (!registrations.TryGetValue(providerId, out registration)) { error = "Provider owner is unavailable: " + providerId; return false; }
            if (instances == null) { error = "Migration instances are missing."; return false; }
            try
            {
                var state = new EventChainProviderState { ProviderId = providerId, StateVersion = registration.Provider.StateVersion,
                    NextId = nextId, MigrationStatus = migrationStatus ?? "" };
                var ids = new HashSet<int>();
                foreach (EventChainInstance source in instances)
                {
                    ValidateInstance(source, ids);
                    IEventChainDefinition definition;
                    if (!registration.Definitions.TryGetValue(source.DefinitionId, out definition))
                        throw new InvalidOperationException("Unknown definition: " + source.DefinitionId);
                    if (definition.Version != source.DefinitionVersion)
                        throw new InvalidOperationException("Definition version mismatch: " + source.DefinitionId);
                    EventChainInstance copy = Copy(source);
                    copy.PreparedState = null;
                    if (copy.End == EventChainEnd.None) Prepare(definition, copy);
                    state.Instances.Add(copy);
                }
                ValidateNextId(state);
                Partition partition = AddPartition(state);
                partition.Prepared = true;
                if (ready) Rebuild(partition, registration, false);
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        public int Start(string providerId, string definitionId, int siteId, int tile, int tick, int deadlineTick,
            string payload, int stage = 0, int nextTick = -1)
        {
            CheckThread();
            Partition partition;
            Registration registration;
            if (!ready || providerId == null || !partitions.TryGetValue(providerId, out partition)
                || partition.State.Mode != EventChainMode.Active || partition.Reason.Length != 0
                || !registrations.TryGetValue(providerId, out registration)) return 0;
            IEventChainDefinition definition;
            if (definitionId == null || !registration.Definitions.TryGetValue(definitionId, out definition))
                throw new ArgumentException("Unknown EventChain definition: " + definitionId, nameof(definitionId));
            ValidateSchedule(nextTick, Math.Max(tick, currentTick));
            if (deadlineTick < -1 || stage < 0) throw new ArgumentOutOfRangeException("EventChain stage/deadline");
            if (partition.State.NextId <= 0 || partition.State.NextId == int.MaxValue)
                throw new InvalidOperationException("EventChain instance IDs are exhausted.");
            var instance = new EventChainInstance { Id = partition.State.NextId, DefinitionId = definitionId,
                DefinitionVersion = definition.Version, SiteId = siteId, Tile = tile, Stage = stage, StartedTick = tick,
                DeadlineTick = deadlineTick, NextTick = nextTick, Payload = payload ?? "" };
            Prepare(definition, instance);
            partition.State.Instances.Add(instance);
            partition.State.NextId++;
            var entry = new Entry(partition, instance, definition);
            partition.Entries.Add(instance.Id, entry);
            AddActive(entry);
            Schedule(entry);
            return instance.Id;
        }

        public bool SetStage(string providerId, int id, int stage, int deadlineTick, string payload, int nextTick)
        {
            CheckThread();
            Entry entry = GetEntry(providerId, id);
            if (entry == null || entry.Instance.End != EventChainEnd.None || entry.Partition.Reason.Length != 0 || entry.Reason.Length != 0) return false;
            RejectSelf(entry);
            if (stage < 0 || deadlineTick < -1 || nextTick < -1) throw new ArgumentOutOfRangeException("EventChain stage/schedule");
            if (ready) ValidateSchedule(nextTick, currentTick);
            EventChainInstance copy = Copy(entry.Instance);
            copy.Stage = stage; copy.DeadlineTick = deadlineTick; copy.NextTick = nextTick; copy.Payload = payload ?? "";
            copy.Revision = NextRevision(copy.Revision);
            if (!string.Equals(copy.Payload, entry.Instance.Payload, StringComparison.Ordinal)) Prepare(entry.Definition, copy);
            entry.Instance.Stage = copy.Stage; entry.Instance.DeadlineTick = copy.DeadlineTick;
            entry.Instance.NextTick = copy.NextTick; entry.Instance.Payload = copy.Payload;
            entry.Instance.PreparedState = copy.PreparedState; entry.Instance.Revision = copy.Revision;
            Schedule(entry);
            return true;
        }

        public bool End(string providerId, int id, int tick, EventChainEnd end)
        {
            CheckThread();
            if (!ValidEnd(end) || end == EventChainEnd.None) throw new ArgumentOutOfRangeException(nameof(end));
            Entry entry = GetEntry(providerId, id);
            if (entry == null || entry.Partition.Reason.Length != 0) return false;
            RejectSelf(entry);
            if (entry.Instance.End != EventChainEnd.None) return entry.Instance.End == end;
            Finish(entry, end);
            return true;
        }

        public EventChainInstance Find(string providerId, int id)
        { CheckThread(); Entry entry = GetEntry(providerId, id); return entry == null ? null : entry.Instance; }

        public int ActiveCountFor(string providerId, string definitionId = null, int siteId = int.MinValue)
        {
            CheckThread();
            Partition partition;
            if (providerId == null || !partitions.TryGetValue(providerId, out partition)) return 0;
            int count;
            if (definitionId == null)
                return siteId == int.MinValue ? partition.ActiveCount : partition.SiteCounts.TryGetValue(siteId, out count) ? count : 0;
            if (siteId == int.MinValue) return partition.DefinitionCounts.TryGetValue(definitionId, out count) ? count : 0;
            return partition.Counts.TryGetValue(new CountKey(definitionId, siteId), out count) ? count : 0;
        }

        public bool SetMode(string providerId, EventChainMode mode, int tick, string reason = "")
        {
            CheckThread();
            if ((int)mode < 0 || (int)mode > 2) throw new ArgumentOutOfRangeException(nameof(mode));
            Partition partition;
            if (providerId == null || !partitions.TryGetValue(providerId, out partition)) return false;
            Registration registration;
            if (!registrations.TryGetValue(providerId, out registration) || partition.State.SchemaVersion != SchemaVersion
                || partition.State.StateVersion != registration.Provider.StateVersion || partition.Reason.Length != 0) return false;
            if (callback != null && ReferenceEquals(callback.Partition, partition))
                throw new InvalidOperationException("A callback cannot change its own provider mode.");
            EventChainProviderState state = partition.State;
            if (state.Mode == mode) { state.Reason = reason ?? ""; return true; }
            if (mode != EventChainMode.Active)
            {
                if (state.Mode == EventChainMode.Active || state.PausedTick < 0) state.PausedTick = tick;
                state.Mode = mode; state.Reason = reason ?? "";
                foreach (Entry entry in partition.Entries.Values) RemoveDue(entry);
                return true;
            }
            long elapsed = state.PausedTick < 0 ? 0 : Math.Max(0L, (long)tick - state.PausedTick);
            // Validate the entire shift first. Never partially move a provider's clocks.
            foreach (EventChainInstance instance in state.Instances ?? new List<EventChainInstance>())
                if (instance != null && instance.End == EventChainEnd.None
                    && (Overflows(instance.NextTick, elapsed) || Overflows(instance.DeadlineTick, elapsed)))
                { state.Reason = "Resume would overflow the simulation clock."; return false; }
            foreach (EventChainInstance instance in state.Instances ?? new List<EventChainInstance>())
                if (instance != null && instance.End == EventChainEnd.None)
                {
                    if (instance.NextTick >= 0) instance.NextTick = (int)(instance.NextTick + elapsed);
                    if (instance.DeadlineTick >= 0) instance.DeadlineTick = (int)(instance.DeadlineTick + elapsed);
                }
            state.Mode = mode; state.PausedTick = -1; state.Reason = reason ?? "";
            foreach (Entry entry in partition.Entries.Values) Schedule(entry);
            return true;
        }

        public void Tick(int tick)
        {
            CheckThread();
            if (ticking || callback != null) throw new InvalidOperationException("EventChain Tick is non-reentrant.");
            currentTick = Math.Max(currentTick, tick);
            if (!ready || heap.Count == 0 || heap[0].Due > tick) return;
            ticking = true;
            try
            {
                while (heap.Count != 0 && heap[0].Due <= tick)
                {
                    Entry entry = heap[0];
                    RemoveDue(entry);
                    Process(entry, tick);
                }
            }
            finally { ticking = false; }
        }

        public string Describe()
        {
            CheckThread();
            var text = new StringBuilder();
            text.Append("EventChain API ").Append(ApiVersion).Append(", schema ").Append(SchemaVersion)
                .Append(", ").Append(ready ? "active" : "awaiting activation").Append(", open ").Append(activeCount)
                .Append(", due ").Append(NextDueTick).Append(", callbacks ").Append(callbackCount);
            foreach (Partition partition in partitions.Values)
            {
                EventChainProviderState state = partition.State;
                text.AppendLine().Append(state.ProviderId).Append(": requested ").Append(state.Mode)
                    .Append(", open ").Append(partition.ActiveCount).Append(", schema ").Append(state.SchemaVersion)
                    .Append(", state version ").Append(state.StateVersion);
                Registration registration;
                text.Append(registrations.TryGetValue(state.ProviderId, out registration) ? ", registered" : ", owner-unavailable");
                if (partition.Reason.Length != 0) text.Append("; ").Append(partition.Reason);
                if (!string.IsNullOrEmpty(state.Reason)) text.Append("; ").Append(state.Reason);
                if (!string.IsNullOrEmpty(state.MigrationStatus)) text.Append("; migration ").Append(state.MigrationStatus);
                foreach (Entry entry in partition.Entries.Values)
                    if (entry.Instance.End != EventChainEnd.None || !string.IsNullOrEmpty(entry.Instance.PauseReason) || entry.Reason.Length != 0)
                    {
                        text.AppendLine().Append("  #").Append(entry.Instance.Id).Append(" ").Append(entry.Instance.DefinitionId)
                            .Append(": ").Append(entry.Instance.End == EventChainEnd.None ? "paused" : entry.Instance.End.ToString());
                        if (entry.Reason.Length != 0) text.Append("; ").Append(entry.Reason);
                        if (!string.IsNullOrEmpty(entry.Instance.PauseReason)) text.Append("; ").Append(entry.Instance.PauseReason);
                    }
            }
            foreach (Registration registration in registrations.Values)
                if (!partitions.ContainsKey(registration.Id)) text.AppendLine().Append(registration.Id).Append(": registered, awaiting migration/activation");
            return text.ToString();
        }

        private void Process(Entry entry, int tick)
        {
            EventChainInstance instance = entry.Instance;
            if (instance.End != EventChainEnd.None) return;
            try
            {
                if (instance.DeadlineTick >= 0 && (tick > instance.DeadlineTick || instance.NextTick < 0 || instance.NextTick > tick))
                { Finish(entry, EventChainEnd.Expired); return; }
                if (instance.Revision == int.MaxValue) throw new InvalidOperationException("EventChain revision is exhausted.");
                EventChainDecision decision;
                Snapshot before = new Snapshot(instance);
                callback = entry;
                try { callbackCount++; decision = entry.Definition.Evaluate(instance, tick); before.RequireUnchanged(instance); }
                finally { before.Restore(instance); callback = null; }
                ValidateDecision(decision, tick);
                if (decision.End != EventChainEnd.None) { Finish(entry, decision.End); return; }
                if (decision.ActionId != null)
                {
                    string key = instance.Revision.ToString(CultureInfo.InvariantCulture) + ":" + decision.ActionId;
                    EventChainReceipt receipt;
                    if (entry.Receipts.TryGetValue(key, out receipt))
                    {
                        if (receipt.Status == "pending" || receipt.Status == "failed")
                            throw new InvalidOperationException("Unresolved action receipt: " + key);
                    }
                    else
                    {
                        receipt = new EventChainReceipt { Key = key, ActionId = decision.ActionId,
                            Revision = instance.Revision, Tick = tick, Status = "pending" };
                        instance.Receipts.Add(receipt);
                        entry.Receipts.Add(key, receipt);
                        // The same object is serialized by the host, including a save during Apply.
                        EventChainActionResult result;
                        before = new Snapshot(instance);
                        ReceiptSnapshot pending = new ReceiptSnapshot(receipt);
                        callback = entry;
                        try
                        {
                            callbackCount++;
                            result = entry.Definition.Apply(instance, tick, decision.ActionId);
                            before.RequireUnchanged(instance);
                            pending.RequireUnchanged(receipt);
                        }
                        finally { before.Restore(instance); pending.Restore(receipt); callback = null; }
                        ValidateResult(result);
                        receipt.Status = result.Status; receipt.Data = result.Data ?? ""; receipt.Reason = result.Reason ?? "";
                        if (result.Status == "failed")
                        {
                            instance.Revision = NextRevision(instance.Revision);
                            instance.PauseReason = "Action failed: " + (receipt.Reason.Length == 0 ? decision.ActionId : receipt.Reason);
                            return;
                        }
                        if (result.End != EventChainEnd.None) { Finish(entry, result.End); return; }
                    }
                }
                if (decision.Stage >= 0) instance.Stage = decision.Stage;
                instance.NextTick = decision.NextTick;
                instance.Revision = NextRevision(instance.Revision);
                if (instance.DeadlineTick >= 0 && tick >= instance.DeadlineTick) Finish(entry, EventChainEnd.Expired, false);
                else Schedule(entry);
            }
            catch (Exception exception)
            {
                instance.PauseReason = "EventChain callback paused: " + exception.Message;
                RemoveDue(entry);
            }
        }

        private Partition AddPartition(EventChainProviderState state)
        {
            var partition = new Partition(state);
            partitions.Add(state.ProviderId, partition);
            states.Add(state);
            return partition;
        }

        private void Rebuild(Partition partition, Registration registration, bool prepare)
        {
            foreach (Entry old in partition.Entries.Values) { RemoveDue(old); if (old.Counted) RemoveActive(old); }
            partition.Entries.Clear();
            partition.Reason = registration == null ? "owner-unavailable" : partition.State.SchemaVersion != SchemaVersion
                ? "schema mismatch" : partition.State.StateVersion != registration.Provider.StateVersion ? "provider state version mismatch" : "";
            try
            {
                if ((int)partition.State.Mode < 0 || (int)partition.State.Mode > 2)
                    throw new InvalidOperationException("Invalid requested provider mode.");
                var ids = new HashSet<int>();
                if (partition.State.Instances == null) throw new InvalidOperationException("Provider instances are missing.");
                foreach (EventChainInstance instance in partition.State.Instances) ValidateInstance(instance, ids);
                ValidateNextId(partition.State);
            }
            catch (Exception exception) { partition.Reason = "Malformed provider state: " + exception.Message; return; }
            foreach (EventChainInstance instance in partition.State.Instances)
            {
                IEventChainDefinition definition = null;
                if (registration != null) registration.Definitions.TryGetValue(instance.DefinitionId, out definition);
                var entry = new Entry(partition, instance, definition);
                partition.Entries.Add(instance.Id, entry);
                foreach (EventChainReceipt receipt in instance.Receipts)
                {
                    entry.Receipts.Add(receipt.Key, receipt);
                    if (receipt.Status == "pending" || receipt.Status == "failed") entry.Reason = "Unresolved " + receipt.Status + " action receipt: " + receipt.Key;
                }
                if (instance.End != EventChainEnd.None) continue;
                AddActive(entry);
                if (partition.Reason.Length != 0) continue;
                if (definition == null) { entry.Reason = "Unknown definition: " + instance.DefinitionId; continue; }
                if (definition.Version != instance.DefinitionVersion) { entry.Reason = "Definition version mismatch: " + instance.DefinitionId; continue; }
                if (prepare && !partition.Prepared)
                    try { Prepare(definition, instance); }
                    catch (Exception exception) { instance.PauseReason = "Prepare failed: " + exception.Message; }
                Schedule(entry);
            }
            if (partition.Reason.Length == 0) partition.Prepared = true;
        }

        private void Prepare(IEventChainDefinition definition, EventChainInstance instance)
        {
            Snapshot snapshot = new Snapshot(instance);
            preparing = true;
            try { definition.Prepare(instance); snapshot.RequireUnchanged(instance); }
            finally { snapshot.Restore(instance); preparing = false; }
        }

        private static void ValidateInstance(EventChainInstance instance, HashSet<int> ids)
        {
            if (instance == null || instance.Id <= 0 || !ids.Add(instance.Id)) throw new InvalidOperationException("Invalid/duplicate instance ID.");
            if (string.IsNullOrWhiteSpace(instance.DefinitionId) || instance.DefinitionVersion <= 0)
                throw new InvalidOperationException("Invalid definition identity/version.");
            if (instance.Stage < 0 || instance.Revision < 0 || instance.NextTick < -1 || instance.DeadlineTick < -1 || !ValidEnd(instance.End))
                throw new InvalidOperationException("Invalid instance stage, clock, revision or end.");
            if (instance.Receipts == null) throw new InvalidOperationException("Instance receipts are missing.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventChainReceipt receipt in instance.Receipts)
                if (receipt == null || string.IsNullOrEmpty(receipt.Key) || string.IsNullOrEmpty(receipt.ActionId)
                    || !keys.Add(receipt.Key) || receipt.Revision < 0 || receipt.Revision > instance.Revision
                    || (receipt.Status != "pending" && receipt.Status != "applied" && receipt.Status != "ignored" && receipt.Status != "failed"))
                    throw new InvalidOperationException("Invalid/duplicate action receipt.");
        }

        private static void ValidateNextId(EventChainProviderState state)
        {
            if (state.NextId <= 0) throw new InvalidOperationException("Invalid next instance ID.");
            foreach (EventChainInstance instance in state.Instances)
                if (instance.Id >= state.NextId) throw new InvalidOperationException("Next instance ID would reuse a saved ID.");
        }

        private static EventChainInstance Copy(EventChainInstance source)
        {
            var copy = new EventChainInstance { Id = source.Id, DefinitionId = source.DefinitionId, DefinitionVersion = source.DefinitionVersion,
                SiteId = source.SiteId, Tile = source.Tile, Stage = source.Stage, StartedTick = source.StartedTick, DeadlineTick = source.DeadlineTick,
                NextTick = source.NextTick, Revision = source.Revision, Payload = source.Payload, PauseReason = source.PauseReason,
                End = source.End, PreparedState = source.PreparedState };
            foreach (EventChainReceipt receipt in source.Receipts)
                copy.Receipts.Add(new EventChainReceipt { Key = receipt.Key, ActionId = receipt.ActionId, Status = receipt.Status,
                    Data = receipt.Data, Reason = receipt.Reason, Revision = receipt.Revision, Tick = receipt.Tick });
            return copy;
        }

        private static void ValidateDecision(EventChainDecision decision, int tick)
        {
            if (decision == null || !ValidEnd(decision.End) || decision.Stage < -1)
                throw new InvalidOperationException("Invalid EventChain decision.");
            if (decision.End != EventChainEnd.None)
            {
                if (decision.ActionId != null || decision.NextTick != -1 || decision.Stage != -1)
                    throw new InvalidOperationException("Terminal decisions cannot also act or schedule.");
            }
            else
            {
                if (decision.ActionId != null && string.IsNullOrWhiteSpace(decision.ActionId)) throw new InvalidOperationException("Empty action identity.");
                ValidateSchedule(decision.NextTick, tick);
            }
        }
        private static void ValidateResult(EventChainActionResult result)
        {
            if (result == null || !ValidEnd(result.End) || (result.Status != "applied" && result.Status != "ignored" && result.Status != "failed")
                || (result.Status != "applied" && result.End != EventChainEnd.None)) throw new InvalidOperationException("Invalid action result.");
        }
        private static void ValidateSchedule(int nextTick, int tick)
        { if (nextTick != -1 && nextTick <= tick) throw new InvalidOperationException("NextTick must be later than the current tick, or -1."); }
        private static bool ValidEnd(EventChainEnd end) { return (int)end >= 0 && (int)end <= 4; }
        private static bool Overflows(int clock, long elapsed) { return clock >= 0 && clock + elapsed > int.MaxValue; }
        private static int NextRevision(int revision)
        { if (revision == int.MaxValue) throw new InvalidOperationException("EventChain revision is exhausted."); return revision + 1; }
        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId) throw new InvalidOperationException("EventChain runtime belongs to its constructing thread.");
            if (preparing) throw new InvalidOperationException("Prepare cannot reenter the EventChain runtime.");
        }
        private void RejectSelf(Entry entry)
        { if (ReferenceEquals(callback, entry)) throw new InvalidOperationException("An EventChain callback cannot mutate its own instance."); }
        private Entry GetEntry(string providerId, int id)
        { Partition partition; Entry entry; return providerId != null && partitions.TryGetValue(providerId, out partition) && partition.Entries.TryGetValue(id, out entry) ? entry : null; }
        private void Finish(Entry entry, EventChainEnd end, bool revise = true)
        {
            if (revise) entry.Instance.Revision = NextRevision(entry.Instance.Revision);
            entry.Instance.End = end; entry.Instance.NextTick = -1;
            RemoveDue(entry); RemoveActive(entry);
        }
        private void AddActive(Entry entry)
        {
            entry.Counted = true; activeCount++; entry.Partition.ActiveCount++;
            Change(entry.Partition.DefinitionCounts, entry.Instance.DefinitionId, 1);
            Change(entry.Partition.SiteCounts, entry.Instance.SiteId, 1);
            Change(entry.Partition.Counts, new CountKey(entry.Instance.DefinitionId, entry.Instance.SiteId), 1);
        }
        private void RemoveActive(Entry entry)
        {
            if (!entry.Counted) return;
            entry.Counted = false; activeCount--; entry.Partition.ActiveCount--;
            Change(entry.Partition.DefinitionCounts, entry.Instance.DefinitionId, -1);
            Change(entry.Partition.SiteCounts, entry.Instance.SiteId, -1);
            Change(entry.Partition.Counts, new CountKey(entry.Instance.DefinitionId, entry.Instance.SiteId), -1);
        }
        private static void Change<TKey>(Dictionary<TKey, int> counts, TKey key, int delta)
        { int count; counts.TryGetValue(key, out count); count += delta; if (count == 0) counts.Remove(key); else counts[key] = count; }

        private void Schedule(Entry entry)
        {
            RemoveDue(entry);
            EventChainInstance instance = entry.Instance;
            if (instance.End != EventChainEnd.None || entry.Partition.State.Mode != EventChainMode.Active
                || entry.Partition.Reason.Length != 0 || entry.Reason.Length != 0 || !string.IsNullOrEmpty(instance.PauseReason)) return;
            int due = instance.NextTick;
            if (instance.DeadlineTick >= 0 && (due < 0 || instance.DeadlineTick < due)) due = instance.DeadlineTick;
            if (due < 0) return;
            entry.Due = due; entry.HeapIndex = heap.Count; heap.Add(entry); Up(entry.HeapIndex);
        }
        private static bool Before(Entry left, Entry right)
        {
            if (left.Due != right.Due) return left.Due < right.Due;
            int owner = string.CompareOrdinal(left.Partition.State.ProviderId, right.Partition.State.ProviderId);
            return owner < 0 || owner == 0 && left.Instance.Id < right.Instance.Id;
        }
        private void Swap(int a, int b)
        { Entry entry = heap[a]; heap[a] = heap[b]; heap[b] = entry; heap[a].HeapIndex = a; heap[b].HeapIndex = b; }
        private void Up(int index)
        { while (index > 0) { int parent = (index - 1) / 2; if (!Before(heap[index], heap[parent])) break; Swap(index, parent); index = parent; } }
        private void Down(int index)
        {
            while (true)
            {
                int child = index * 2 + 1;
                if (child >= heap.Count) return;
                if (child + 1 < heap.Count && Before(heap[child + 1], heap[child])) child++;
                if (!Before(heap[child], heap[index])) return;
                Swap(index, child); index = child;
            }
        }
        private void RemoveDue(Entry entry)
        {
            int index = entry.HeapIndex;
            if (index < 0) return;
            int last = heap.Count - 1;
            entry.HeapIndex = -1;
            if (index == last) { heap.RemoveAt(last); return; }
            heap[index] = heap[last]; heap[index].HeapIndex = index; heap.RemoveAt(last);
            if (index > 0 && Before(heap[index], heap[(index - 1) / 2])) Up(index); else Down(index);
        }

        private sealed class Registration
        {
            internal readonly string Id;
            internal readonly IEventChainProvider Provider;
            internal readonly Dictionary<string, IEventChainDefinition> Definitions = new Dictionary<string, IEventChainDefinition>(StringComparer.Ordinal);
            internal Registration(IEventChainProvider provider)
            {
                if (provider == null) throw new ArgumentNullException(nameof(provider));
                if (string.IsNullOrWhiteSpace(provider.ProviderId) || provider.ApiVersion != ApiVersion || provider.StateVersion <= 0 || provider.Definitions == null)
                    throw new ArgumentException("Invalid/incompatible EventChain provider.", nameof(provider));
                Id = provider.ProviderId; Provider = provider;
                foreach (IEventChainDefinition definition in provider.Definitions)
                {
                    if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || definition.Version <= 0 || Definitions.ContainsKey(definition.Id))
                        throw new ArgumentException("Invalid/duplicate EventChain definition.", nameof(provider));
                    Definitions.Add(definition.Id, definition);
                }
            }
        }
        private sealed class Partition
        {
            internal readonly EventChainProviderState State;
            internal readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
            internal readonly Dictionary<string, int> DefinitionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly Dictionary<int, int> SiteCounts = new Dictionary<int, int>();
            internal readonly Dictionary<CountKey, int> Counts = new Dictionary<CountKey, int>();
            internal int ActiveCount;
            internal bool Prepared;
            internal string Reason = "";
            internal Partition(EventChainProviderState state) { State = state; }
        }
        private sealed class Entry
        {
            internal readonly Partition Partition;
            internal readonly EventChainInstance Instance;
            internal readonly IEventChainDefinition Definition;
            internal readonly Dictionary<string, EventChainReceipt> Receipts = new Dictionary<string, EventChainReceipt>(StringComparer.Ordinal);
            internal int HeapIndex = -1, Due;
            internal bool Counted;
            internal string Reason = "";
            internal Entry(Partition partition, EventChainInstance instance, IEventChainDefinition definition)
            { Partition = partition; Instance = instance; Definition = definition; }
        }
        private struct CountKey : IEquatable<CountKey>
        {
            private readonly string definition;
            private readonly int site;
            internal CountKey(string definition, int site) { this.definition = definition; this.site = site; }
            public bool Equals(CountKey other) { return site == other.site && string.Equals(definition, other.definition, StringComparison.Ordinal); }
            public override bool Equals(object obj) { return obj is CountKey && Equals((CountKey)obj); }
            public override int GetHashCode() { return unchecked(StringComparer.Ordinal.GetHashCode(definition) * 397 ^ site); }
        }
        private struct ReceiptSnapshot
        {
            private readonly string key, action, status, data, reason;
            private readonly int revision, tick;
            internal ReceiptSnapshot(EventChainReceipt receipt)
            { key = receipt.Key; action = receipt.ActionId; status = receipt.Status; data = receipt.Data; reason = receipt.Reason; revision = receipt.Revision; tick = receipt.Tick; }
            internal void RequireUnchanged(EventChainReceipt receipt)
            {
                if (key != receipt.Key || action != receipt.ActionId || status != receipt.Status || data != receipt.Data
                    || reason != receipt.Reason || revision != receipt.Revision || tick != receipt.Tick)
                    throw new InvalidOperationException("A host action changed its pending receipt before commit.");
            }
            internal void Restore(EventChainReceipt receipt)
            { receipt.Key = key; receipt.ActionId = action; receipt.Status = status; receipt.Data = data; receipt.Reason = reason; receipt.Revision = revision; receipt.Tick = tick; }
        }
        private struct Snapshot
        {
            private readonly int id, version, site, tile, stage, started, deadline, next, revision;
            private readonly string definition, payload, pause;
            private readonly EventChainEnd end;
            private readonly List<EventChainReceipt> receipts;
            private readonly int receiptCount;
            internal Snapshot(EventChainInstance instance)
            {
                id = instance.Id; version = instance.DefinitionVersion; site = instance.SiteId; tile = instance.Tile;
                stage = instance.Stage; started = instance.StartedTick; deadline = instance.DeadlineTick; next = instance.NextTick;
                revision = instance.Revision; definition = instance.DefinitionId; payload = instance.Payload;
                pause = instance.PauseReason; end = instance.End; receipts = instance.Receipts; receiptCount = receipts.Count;
            }
            internal void RequireUnchanged(EventChainInstance instance)
            {
                if (id != instance.Id || version != instance.DefinitionVersion || site != instance.SiteId || tile != instance.Tile
                    || stage != instance.Stage || started != instance.StartedTick || deadline != instance.DeadlineTick || next != instance.NextTick
                    || revision != instance.Revision || definition != instance.DefinitionId || payload != instance.Payload || pause != instance.PauseReason
                    || end != instance.End || !ReferenceEquals(receipts, instance.Receipts) || receiptCount != receipts.Count)
                    throw new InvalidOperationException("A definition changed persisted instance state outside its decision/result.");
            }
            internal void Restore(EventChainInstance instance)
            {
                instance.Id = id; instance.DefinitionVersion = version; instance.SiteId = site; instance.Tile = tile;
                instance.Stage = stage; instance.StartedTick = started; instance.DeadlineTick = deadline; instance.NextTick = next;
                instance.Revision = revision; instance.DefinitionId = definition; instance.Payload = payload;
                instance.PauseReason = pause; instance.End = end; instance.Receipts = receipts;
            }
        }
    }
}
