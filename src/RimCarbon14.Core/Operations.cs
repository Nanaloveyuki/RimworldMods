using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace RimCarbon14.Core
{
    public sealed class Operation
    {
        internal Func<Operation, JToken?> Step = null!;
        internal DateTime Deadline;
        internal string Fingerprint = "";
        public string Id { get; internal set; } = "";
        public string Tool { get; internal set; } = "";
        public string State { get; internal set; } = "queued";
        public string? Session { get; internal set; }
        public bool CancelRequested { get; internal set; }
        public bool Started { get; internal set; }
        public DateTime CreatedAtUtc { get; internal set; }
        public DateTime? FinishedAtUtc { get; internal set; }
        public JToken? Result { get; internal set; }
        public JToken? Error { get; internal set; }
        public bool Terminal => State == "completed" || State == "failed" || State == "cancelled";
        public Action? Cleanup { get; set; }
        public object? Context { get; set; }
        public void ThrowIfCancelled() { if (CancelRequested) throw new BridgeException("cancelled", "Cancelled; already applied effects are not rolled back."); }
        internal JObject Snapshot() => (JObject)Wire.Token(new { Id, Tool, State, Session, Started, CancelRequested, CreatedAtUtc, FinishedAtUtc, Result, Error });
    }

    public sealed class Operations
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, Operation> calls = new Dictionary<string, Operation>();
        private readonly Queue<Operation> pending = new Queue<Operation>();
        private readonly Func<DateTime> now;
        private readonly EventBuffer events;
        private readonly int capacity;
        public Operations(EventBuffer events, int capacity = 2048, Func<DateTime>? clock = null) { this.events = events; this.capacity = capacity; now = clock ?? (() => DateTime.UtcNow); }

        public JObject Submit(CallRequest request, Func<Operation, JToken?> step)
        {
            lock (gate)
            {
                if (string.IsNullOrWhiteSpace(request.RequestId) || request.RequestId.Length > 128) throw new BridgeException("invalid_request_id", "Provide a unique requestId, at most 128 characters.");
                var fingerprint = Wire.Json(new { request.Tool, request.Arguments, request.Session, request.TimeoutMs });
                if (calls.TryGetValue(request.RequestId, out var prior))
                {
                    if (prior.Fingerprint != fingerprint) throw new BridgeException("request_conflict", "Request ID already used with different arguments.");
                    return prior.Snapshot();
                }
                if (Math.Abs((now() - request.IssuedAtUtc.ToUniversalTime()).TotalSeconds) > 60) throw new BridgeException("request_expired", "New requests must be issued within 60 seconds. Do not replay an uncertain operation.");
                foreach (var old in calls.Values.Where(o => o.Terminal && now() - o.FinishedAtUtc > TimeSpan.FromMinutes(5)).ToArray()) calls.Remove(old.Id);
                if (calls.Count >= capacity) throw new BridgeException("queue_full", "Operation retention capacity reached; retry later with a new request.");
                if (request.TimeoutMs < 1 || request.TimeoutMs > 900000) throw new BridgeException("invalid_timeout", "timeoutMs must be between 1 and 900000.");
                var op = new Operation { Id = request.RequestId, Tool = request.Tool, Session = request.Session, CreatedAtUtc = now(), Deadline = now().AddMilliseconds(request.TimeoutMs), Step = step, Fingerprint = fingerprint };
                calls.Add(op.Id, op);
                pending.Enqueue(op);
                events.Add("operation.queued", new { op.Id, op.Tool, op.Session });
                return op.Snapshot();
            }
        }

        public JObject Get(string id) { lock (gate) return Find(id).Snapshot(); }
        public JObject Cancel(string id)
        {
            lock (gate)
            {
                var op = Find(id);
                if (!op.Terminal) op.CancelRequested = true;
                return op.Snapshot();
            }
        }
        private Operation Find(string id) => calls.TryGetValue(id, out var op) ? op : throw new BridgeException("operation_not_found", "Operation missing or retention expired. Do not assume it was not executed.");

        public void Pump(int budgetMs = 3, int maximumSteps = 32)
        {
            var timer = Stopwatch.StartNew();
            int count;
            lock (gate) count = Math.Min(maximumSteps, pending.Count);
            for (var i = 0; i < count && timer.ElapsedMilliseconds < budgetMs; i++)
            {
                Operation op;
                lock (gate)
                {
                    if (pending.Count == 0) break;
                    op = pending.Dequeue();
                    if (op.Terminal) continue;
                    if (op.CancelRequested || now() >= op.Deadline)
                    {
                        Finish(op, op.CancelRequested ? "cancelled" : "failed", null, new BridgeException(op.CancelRequested ? "cancelled" : "timeout", "Operation stopped at a scheduler boundary; applied effects are not rolled back."));
                        continue;
                    }
                    op.State = "running";
                    op.Started = true;
                }
                try
                {
                    // Do not hold the protocol lock while executing arbitrary game code.
                    var result = op.Step(op);
                    lock (gate)
                    {
                        if (result != null) Finish(op, "completed", result, null);
                        else pending.Enqueue(op);
                    }
                }
                catch (Exception ex)
                {
                    lock (gate) Finish(op, ex is BridgeException b && b.Code == "cancelled" ? "cancelled" : "failed", null, ex);
                }
            }
        }
        private void Finish(Operation op, string state, JToken? result, Exception? ex)
        {
            op.State = state;
            op.Result = result;
            op.Error = ex == null ? null : Wire.Token(new { code = (ex as BridgeException)?.Code ?? "execution_error", message = ex.Message, type = ex.GetType().FullName, stack = ex.StackTrace });
            op.FinishedAtUtc = now();
            try { op.Cleanup?.Invoke(); } catch (Exception cleanupError) { events.Add("operation.cleanup_error", new { op.Id, cleanupError.Message }); }
            op.Context = null;
            op.Step = null!;
            op.Cleanup = null;
            events.Add("operation." + state, op.Snapshot());
        }
    }
}
