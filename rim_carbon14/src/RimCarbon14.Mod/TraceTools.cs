using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using Verse;

namespace RimCarbon14.Mod
{
    public sealed class TraceTools
    {
        private readonly BridgeHost host;
        private readonly Harmony harmony = new Harmony("nanaloveyuki.rimcarbon14.trace");
        private readonly object gate = new object();
        private readonly Dictionary<string, Stats> stats = new Dictionary<string, Stats>();
        private volatile bool active;
        private DateTime until;
        private bool detailed;
        private bool stack;
        private int maximum;
        private int recorded;
        private int dropped;
        private object? instanceFilter;
        private string[] fields = Array.Empty<string>();
        private string sessionId = "";
        [ThreadStatic] private static Stack<Call>? nesting;
        private static TraceTools Self => BridgeHost.Instance.Trace;
        private sealed class Stats { public long Calls; public long Exceptions; public long Skipped; public long Ticks; public long Max; public long MissingTimings; }
        private sealed class Call
        {
            public string Id = ""; public string? Parent; public string Method = ""; public long Start; public string Session = ""; public bool Original; public JToken? Arguments; public JToken? Result; public string? OperationId; public JObject? Stamp; public bool MissingTiming;
        }
        public TraceTools(BridgeHost host) { this.host = host; }
        public void Register()
        {
            host.AddRead("trace_patches", "Inspect Harmony patch owners and declared ordering, not an inferred execution chain.", Patches, "methodId!");
            host.Add("trace_start", "Start bounded profiling of explicit managed method IDs. Details, argument fields and stack traces are opt-in. Measures instrumented synchronous invocations.", "instrument", false, (a, _) => Start(a), "methodIds!:array", "durationSeconds:integer", "detailed:boolean", "stack:boolean", "maxRecords:integer", "instanceHandle", "fields:array");
            host.AddRead("trace_status", "Get bounded aggregate counters and detailed-event drop count.", _ => Snapshot());
            host.Add("trace_stop", "Stop profiling and remove only RimCarbon14 trace patches; aggregates remain available.", "instrument", false, (_, __) => { Stop(); return Snapshot(); });
        }
        private JToken Start(JObject args)
        {
            Stop();
            var ids = (args["methodIds"] as JArray)?.Values<string>().Select(x => x!).Distinct().ToArray() ?? Array.Empty<string>();
            if (ids.Length < 1 || ids.Length > 32) throw new BridgeException("invalid_method_count", "Select 1..32 explicit methods per session.");
            var methods = ids.Select(ReflectionTools.Method).ToArray();
            if (methods.Any(m => !ReflectionTools.Supported(m) || m.IsAbstract || m.GetMethodBody() == null || m.DeclaringType?.Assembly == typeof(TraceTools).Assembly || m.DeclaringType?.Assembly == typeof(Wire).Assembly)) throw new BridgeException("unsupported_trace_target", "Only managed methods with bodies outside bridge infrastructure can be instrumented.");
            lock (gate)
            {
                stats.Clear(); recorded = 0; dropped = 0;
                detailed = (bool?)args["detailed"] ?? false; stack = (bool?)args["stack"] ?? false;
                maximum = Math.Max(1, Math.Min(10000, (int?)args["maxRecords"] ?? 1000));
                until = DateTime.UtcNow.AddSeconds(Math.Max(1, Math.Min(300, (int?)args["durationSeconds"] ?? 10)));
                instanceFilter = args["instanceHandle"] == null ? null : host.Handles.Get((string)args["instanceHandle"]!);
                fields = ((JArray?)args["fields"] ?? new JArray()).Values<string>().Take(16).Select(x => x!).ToArray();
                sessionId = Guid.NewGuid().ToString("N");
                foreach (var id in ids) stats.Add(id, new Stats());
            }
            try
            {
                foreach (var method in methods)
                    harmony.Patch(method, new HarmonyMethod(typeof(TraceTools), nameof(Before)) { priority = Priority.First }, new HarmonyMethod(typeof(TraceTools), method.ReturnType == typeof(void) ? nameof(AfterVoid) : nameof(After)), null, new HarmonyMethod(typeof(TraceTools), nameof(FinalizeCall)));
                active = true;
            }
            catch { harmony.UnpatchAll(harmony.Id); throw; }
            return Snapshot();
        }
        public void Advance() { if (active && DateTime.UtcNow >= until) Stop(); }
        public void Stop() { active = false; harmony.UnpatchAll(harmony.Id); instanceFilter = null; }
        private JToken Snapshot()
        {
            lock (gate) return Wire.Token(new { active, sessionId, untilUtc = until, detailed, stack, recorded, dropped, scope = "instrumented synchronous calls; not whole-program or async causality", methods = stats.Select(p => new { methodId = p.Key, calls = p.Value.Calls, exceptions = p.Value.Exceptions, skipped = p.Value.Skipped, missingTimings = p.Value.MissingTimings, totalMs = p.Value.Ticks * 1000d / Stopwatch.Frequency, maxMs = p.Value.Max * 1000d / Stopwatch.Frequency }) });
        }
        private JToken Patches(JObject args)
        {
            var m = ReflectionTools.Method(Wire.Required(args, "methodId")); var info = Harmony.GetPatchInfo(m);
            JToken Describe(IEnumerable<Patch> patches) => Wire.Token(patches.Select(p => new { p.owner, p.priority, p.before, p.after, method = p.PatchMethod.ToString(), assembly = p.PatchMethod.DeclaringType?.Assembly.FullName }));
            return new JObject { ["methodId"] = ReflectionTools.MethodId(m), ["prefixes"] = info == null ? new JArray() : Describe(info.Prefixes), ["postfixes"] = info == null ? new JArray() : Describe(info.Postfixes), ["transpilers"] = info == null ? new JArray() : Describe(info.Transpilers), ["finalizers"] = info == null ? new JArray() : Describe(info.Finalizers) };
        }
        private static void Before(MethodBase __originalMethod, object? __instance, object[] __args, bool __runOriginal, out Call? __state)
        {
            __state = null;
            var s = Self;
            if (!s.active || DateTime.UtcNow >= s.until || (s.instanceFilter != null && !ReferenceEquals(s.instanceFilter, __instance))) return;
            try
            {
                if (nesting == null) nesting = new Stack<Call>();
                if (nesting.Count > 0 && nesting.Peek().Session != s.sessionId) nesting.Clear();
                var c = new Call { Id = Guid.NewGuid().ToString("N"), Parent = nesting.Count == 0 ? null : nesting.Peek().Id, Method = ReflectionTools.MethodId(__originalMethod), Original = __runOriginal, Session = s.sessionId };
                c.OperationId = BridgeHost.CurrentOperationId;
                if (s.detailed && Thread.CurrentThread.ManagedThreadId == s.host.MainThreadId) c.Stamp = s.host.Stamp();
                if (s.detailed) c.Arguments = new JArray(__args.Take(32).Select(s.Preview));
                c.Start = Stopwatch.GetTimestamp(); nesting.Push(c); __state = c;
            }
            catch (Exception ex) { s.host.Events.Add("trace.observer_error", new { ex.Message }); }
        }
        private JToken Preview(object? value)
        {
            if (value == null) return JValue.CreateNull();
            var t = value.GetType();
            if (value is string text) return new JValue(text.Length > 2048 ? text.Substring(0, 2048) : text);
            if (t.IsPrimitive || t.IsEnum || value is decimal) return Wire.Token(value);
            var result = new JObject { ["type"] = t.FullName, ["snapshot"] = false };
            if (Thread.CurrentThread.ManagedThreadId != host.MainThreadId || fields.Length == 0) return result;
            var selected = new JObject();
            foreach (var name in fields)
            {
                var field = AccessTools.Field(t, name);
                if (field == null) continue;
                var v = field.GetValue(value);
                selected[name] = v == null ? JValue.CreateNull() : v.GetType().IsPrimitive || v is string || v.GetType().IsEnum ? Wire.Token(v) : Wire.Token(new { type = v.GetType().FullName, snapshot = false });
            }
            result["fields"] = selected; result["snapshot"] = true; return result;
        }
        private static void After(Call? __state, object? __result, bool __runOriginal)
        {
            if (__state == null) return;
            __state.Original = __runOriginal;
            try { if (Self.detailed) __state.Result = Self.Preview(__result); }
            catch (Exception ex) { Self.host.Events.Add("trace.observer_error", new { ex.Message }); }
        }
        private static void AfterVoid(Call? __state, bool __runOriginal) { if (__state != null) __state.Original = __runOriginal; }
        private static void FinalizeCall(Call? __state, Exception? __exception, MethodBase __originalMethod, object? __instance)
        {
            if (__state == null)
            {
                if (!Self.active || DateTime.UtcNow >= Self.until || (Self.instanceFilter != null && !ReferenceEquals(Self.instanceFilter, __instance))) return;
                // A higher-priority skipping/throwing prefix can prevent our prefix from running.
                __state = new Call { Id = Guid.NewGuid().ToString("N"), Method = ReflectionTools.MethodId(__originalMethod), Session = Self.sessionId, MissingTiming = true, Original = false };
            }
            try
            {
                var s = Self; var c = __state; var elapsed = c.MissingTiming ? 0 : Stopwatch.GetTimestamp() - c.Start;
                if (nesting != null && nesting.Count > 0 && ReferenceEquals(nesting.Peek(), c)) nesting.Pop();
                lock (s.gate)
                {
                    if (c.Session != s.sessionId || !s.stats.TryGetValue(c.Method, out var stat)) return;
                    stat.Calls++; stat.Ticks += elapsed; stat.Max = Math.Max(stat.Max, elapsed);
                    if (c.MissingTiming) stat.MissingTimings++;
                    if (__exception != null) stat.Exceptions++;
                    if (!c.MissingTiming && !c.Original) stat.Skipped++;
                    if (s.detailed)
                    {
                        if (s.recorded >= s.maximum) { s.dropped++; return; }
                        s.recorded++;
                        s.host.Events.Add("trace.call", new { sessionId = c.Session, callId = c.Id, parentId = c.Parent, operationId = c.OperationId, stamp = c.Stamp, methodId = c.Method, thread = Thread.CurrentThread.ManagedThreadId, elapsedMs = c.MissingTiming ? (double?)null : elapsed * 1000d / Stopwatch.Frequency, ranOriginal = c.MissingTiming ? (bool?)null : c.Original, arguments = c.Arguments, result = c.Result, exception = __exception?.ToString(), stack = s.stack ? new StackTrace(2, false).ToString() : null });
                    }
                }
            }
            catch (Exception ex) { Self.host.Events.Add("trace.observer_error", new { ex.Message }); }
        }
    }
}
