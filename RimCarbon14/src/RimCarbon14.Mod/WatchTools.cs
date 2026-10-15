using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using Verse;

namespace RimCarbon14.Mod
{
    public sealed class WatchTools
    {
        private readonly BridgeHost host;
        internal WatchInstrumentation Instrumentation = new WatchInstrumentation();
        private WatchWindow? window;
        private volatile bool active;
        private bool cleanup;
        private string id = "";
        private string mode = "watch";
        private string state = "idle";
        private string? reason;
        private string? error;
        private string? outputPath;
        private StreamWriter? writer;
        private string session = "";
        private DateTime armedUtc;
        private DateTime? startedUtc;
        private DateTime? endedUtc;
        private DateTime deadline;
        private int? tickCount;
        private int depth;
        private int rootTickCall;
        private int[] roots = Array.Empty<int>();
        private JObject configuration = new JObject();
        private TickManager? tickManager;
        private long outsideTickCalls;
        private long otherThreadCalls;
        private bool suppressed;
        private string? expectedBody;
        private long unobservedEntries;
        private int writtenNodes;
        public struct BodyState { public bool Observed; public bool PriorSuppressed; public string? PriorExpected; public int Token; }
        private static WatchTools Self => BridgeHost.Instance.Watch;
        private static double Now => Stopwatch.GetTimestamp() * (1000d / Stopwatch.Frequency);
        public WatchTools(BridgeHost host) { this.host = host; }
        public void Register()
        {
            host.Add("watch_start", "Observe multiple roots in one complete-tick window. mode=watch keeps data in memory; mode=output additionally flushes JSONL to disk. No UI. Starting while active is rejected.", "instrument", true,
                (a, _) => Start(a), "roots!:array", "mode!", "ticks:integer", "durationSeconds:integer", "depth:integer", "maxMethods:integer", "maxSites:integer", "maxNodes:integer", "maxSamples:integer");
            host.AddRead("watch_snapshot", "Read aggregates for completed ticks, coverage and optional paginated tick data. Does not stop observation.", Snapshot, "watchId!", "offset:integer", "limit:integer");
            host.AddRead("watch_report", "Read the data-only report; active reports are provisional. Tick shares are ratios, not additive across ancestors.", Snapshot, "watchId!", "offset:integer", "limit:integer");
            host.Add("watch_stop", "Stop this watch at the last completed tick and finalize output if enabled. Retains the in-memory report until the next start.", "instrument", false,
                (a, _) => { CheckId(a); Stop("requested"); return Snapshot(a); }, "watchId!");
            host.Add("watch_expand", "Increase the shared depth in a NEW window. Returns previousReport and current with a new watchId; never mixes timings from different coverage depths.", "instrument", true,
                (a, _) =>
                {
                    CheckId(a); int nextDepth = Number(a, "depth", depth + 1, depth + 1, 8);
                    var next = (JObject)configuration.DeepClone(); next["depth"] = nextDepth;
                    Stop("expanded"); var previous = Report();
                    return new JObject { ["previousReport"] = previous, ["current"] = Start(next) };
                }, "watchId!", "depth:integer");
        }
        private static int Number(JObject a, string name, int fallback, int min, int max)
        {
            int value = (int?)a[name] ?? fallback;
            if (value < min || value > max) throw new BridgeException("invalid_watch_limit", name + " must be " + min + ".." + max + ".");
            return value;
        }
        private JToken Start(JObject a)
        {
            if (active || cleanup) throw new BridgeException("watch_active", "Stop the current watch before replacing its window.");
            string selectedMode = Wire.Required(a, "mode");
            if (selectedMode != "watch" && selectedMode != "output") throw new BridgeException("invalid_watch_mode", "Use watch or output.");
            if (a["ticks"] != null && a["durationSeconds"] != null) throw new BridgeException("invalid_watch_window", "Choose ticks or durationSeconds, not both.");
            var values = (JArray)a["roots"]!;
            if (values.Any(v => v.Type != JTokenType.String)) throw new BridgeException("invalid_watch_roots", "roots must contain exact method ID strings.");
            var methods = values.Values<string>().Distinct().Select(x => ReflectionTools.Method(x!)).ToArray();
            if (methods.Length < 1 || methods.Length > 16) throw new BridgeException("invalid_watch_roots", "Select 1..16 exact managed root methods.");
            int selectedDepth = Number(a, "depth", 2, 0, 8);
            int methodLimit = Number(a, "maxMethods", 128, methods.Length, 512);
            int siteLimit = Number(a, "maxSites", 4096, 1, 16384);
            int nodeLimit = Number(a, "maxNodes", 2048, 1, 8192);
            int sampleLimit = Number(a, "maxSamples", 100000, 1, 1000000);
            int? ticks = a["durationSeconds"] == null ? Number(a, "ticks", 600, 1, 10000) : (int?)null;
            int seconds = Number(a, "durationSeconds", 300, 1, 300);
            Instrumentation = new WatchInstrumentation();
            window = new WatchWindow(selectedDepth, nodeLimit, sampleLimit);
            id = Guid.NewGuid().ToString("N"); mode = selectedMode; session = host.Session;
            state = "arming"; reason = error = outputPath = null; startedUtc = endedUtc = null;
            outsideTickCalls = otherThreadCalls = 0; depth = selectedDepth; tickCount = ticks;
            suppressed = false; expectedBody = null; unobservedEntries = 0;
            writtenNodes = 0;
            configuration = (JObject)a.DeepClone(); configuration["depth"] = depth;
            tickManager = Current.Game.tickManager;
            try
            {
                Instrumentation.Install(methods, depth, methodLimit, siteLimit);
                roots = methods.Select(Instrumentation.RootId).ToArray();
                armedUtc = DateTime.UtcNow; deadline = armedUtc.AddSeconds(seconds);
                if (mode == "output")
                {
                    outputPath = Path.Combine(host.ArtifactDirectory, id + ".jsonl");
                    writer = new StreamWriter(new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                    Write("header", Report());
                }
                state = "armed"; active = true;
                return Report();
            }
            catch (Exception ex)
            {
                active = false; tickManager = null; state = "failed"; reason = "start_failed"; error = ex.Message;
                Instrumentation.Stop(); writer?.Dispose(); writer = null; throw;
            }
        }
        private void CheckId(JObject a)
        {
            if (Wire.Required(a, "watchId") != id || window == null) throw new BridgeException("stale_watch", "The watch does not belong to the retained window.");
        }
        private JToken Snapshot(JObject a)
        {
            CheckId(a); var report = Report();
            if (a["offset"] != null || a["limit"] != null)
                report["ticks"] = window!.Page(Number(a, "offset", 0, 0, 10000), Number(a, "limit", 100, 1, 200));
            return report;
        }
        private JObject Report()
        {
            var report = window?.Report() ?? new JObject();
            report["watchId"] = id; report["mode"] = mode; report["state"] = state; report["active"] = active;
            report["instanceId"] = host.InstanceId; report["session"] = session;
            report["armedUtc"] = armedUtc; report["startedUtc"] = startedUtc; report["endedUtc"] = endedUtc; report["deadlineUtc"] = deadline;
            report["requestedTicks"] = tickCount; report["depth"] = depth; report["reason"] = reason; report["error"] = error;
            report["configuration"] = configuration.DeepClone();
            report["outputPath"] = outputPath;
            report["outputRoute"] = outputPath == null ? null : "/v1/artifacts/" + id + ".jsonl";
            report["outsideTickRootCalls"] = outsideTickCalls; report["otherThreadRootCalls"] = Interlocked.Read(ref otherThreadCalls);
            report["unobservedBodyEntries"] = unobservedEntries;
            report["scope"] = "main-thread synchronous call sites within complete DoSingleTick invocations; outermost selected roots; no UI/frame or async causality";
            report["timing"] = "Stopwatch elapsed time, not CPU time; includes instrumentation and other Harmony patches; self is residual after observed children; ratios use measured tick elapsed time";
            report["rootSiteIds"] = new JArray(roots);
            report["roots"] = Wire.Token(Instrumentation.RootMetadata());
            report["methods"] = Wire.Token(Instrumentation.Methods.Select(p => new { methodId = p.Key, signature = p.Value, bodyExpanded = Instrumentation.Expanded(p.Key) }));
            report["sites"] = Wire.Token(Instrumentation.Sites);
            report["coverageGaps"] = Wire.Token(Instrumentation.Gaps);
            report["omittedCoverageGaps"] = Instrumentation.OmittedGaps;
            return report;
        }
        private void Write(string kind, JToken data)
        {
            if (writer == null) return;
            writer.WriteLine(Wire.Json(new { kind, watchId = id, data })); writer.Flush();
        }
        public void Advance()
        {
            if (active && DateTime.UtcNow >= deadline && !window!.InTick) Complete("duration_elapsed");
            if (cleanup) Finish();
        }
        private void Complete(string cause)
        {
            active = false; cleanup = true; tickManager = null; reason = cause; endedUtc = DateTime.UtcNow;
            state = error == null ? "completed" : "failed";
        }
        public void Stop(string cause = "session_changed")
        {
            if (active) { window?.AbortTick(); Complete(cause); }
            if (cleanup) Finish();
        }
        private void Finish()
        {
            cleanup = false;
            try { Instrumentation.Stop(); Write("report", Report()); }
            catch (Exception ex) { error = ex.Message; state = "failed"; reason = "finalize_failed"; host.Events.Add("watch.error", new { watchId = id, error }); }
            finally { try { writer?.Dispose(); } catch (IOException ex) { error = ex.Message; state = "failed"; } writer = null; }
        }
        private void ObserverError(Exception ex)
        {
            if (!active) return;
            error = ex.ToString(); window?.AbortTick(); Complete("observer_error");
            host.Events.Add("watch.error", new { watchId = id, error });
        }
        public static void TickBefore(TickManager __instance, out bool __state)
        {
            __state = false; var s = Self;
            if (!s.active || Thread.CurrentThread.ManagedThreadId != s.host.MainThreadId) return;
            try
            {
                if (!ReferenceEquals(__instance, s.tickManager) || s.host.Session != s.session) { s.Complete("session_changed"); return; }
                if (DateTime.UtcNow >= s.deadline) { s.Complete("duration_elapsed"); return; }
                if (s.startedUtc == null) s.startedUtc = DateTime.UtcNow;
                s.suppressed = false; s.expectedBody = null;
                s.state = "recording"; s.window!.BeginTick(__instance.TicksGame, Now); __state = true;
                int root = s.Instrumentation.RootId(WatchInstrumentation.TickMethod);
                s.rootTickCall = root == 0 ? 0 : s.window.Enter(s.Instrumentation.RootMethodId(WatchInstrumentation.TickMethod), root, Now);
            }
            catch (Exception ex) { s.ObserverError(ex); }
        }
        public static void TickAfter(bool __state, TickManager __instance, Exception? __exception)
        {
            if (!__state) return; var s = Self;
            try
            {
                s.window!.Exit(s.rootTickCall, Now, __exception != null);
                var tick = s.window.EndTick(Now, __exception != null);
                if (tick != null)
                {
                    tick.GameTick = __instance.TicksGame;
                    if (s.writer != null)
                    {
                        var data = (JObject)Wire.Token(tick);
                        // Completed JSONL ticks must remain interpretable if the final report is lost.
                        data["nodeDefinitions"] = Wire.Token(s.window.Nodes.Skip(s.writtenNodes).Select(n => new
                        {
                            n.Id, n.ParentId, n.MethodId, n.SiteId, n.Depth,
                            signature = s.Instrumentation.Methods[n.MethodId]
                        }));
                        s.Write("tick", data); s.writtenNodes = s.window.Nodes.Count;
                    }
                }
                if (s.window.Limit != null) s.Complete(s.window.Limit);
                else if (s.tickCount != null && s.window.Ticks.Count >= s.tickCount) s.Complete("ticks_completed");
                else if (s.window.Ticks.Count >= 10000) s.Complete("tick_limit");
                else if (DateTime.UtcNow >= s.deadline) s.Complete("duration_elapsed");
            }
            catch (Exception ex) { s.ObserverError(ex); }
        }
        public static void BodyBefore(MethodBase __originalMethod, out BodyState __state)
        {
            __state = default; var s = Self; var instrumentation = s.Instrumentation; if (!s.active) return;
            var method = (MethodInfo)__originalMethod;
            int root = instrumentation.RootId(method);
            if (Thread.CurrentThread.ManagedThreadId != s.host.MainThreadId) { if (root != 0) Interlocked.Increment(ref s.otherThreadCalls); return; }
            try
            {
                if (!s.window!.InTick) { if (root != 0) s.outsideTickCalls++; return; }
                __state = new BodyState { Observed = true, PriorSuppressed = s.suppressed, PriorExpected = s.expectedBody };
                string methodId = s.Instrumentation.BodyMethodId(method);
                if (root != 0 && s.window.StackDepth == 0)
                {
                    s.suppressed = false; s.expectedBody = null;
                    __state.Token = s.window.Enter(methodId, root, Now);
                }
                else if (!s.suppressed && s.expectedBody == methodId) s.expectedBody = null;
                else
                {
                    // A callback through an unexpanded body must not attach to an older invocation
                    // merely because that invocation happens to have the same method identity.
                    if (!s.suppressed && s.window.StackDepth != 0) s.unobservedEntries++;
                    s.suppressed = true;
                }
            }
            catch (Exception ex) { s.ObserverError(ex); }
        }
        public static void BodyAfter(BodyState __state, Exception? __exception)
        {
            if (!__state.Observed) return;
            CallAfter(__state.Token, __exception == null);
            Self.suppressed = __state.PriorSuppressed; Self.expectedBody = __state.PriorExpected;
        }
        public static int CallBefore(int siteId, object? receiver)
        {
            var s = Self;
            if (!s.active || Thread.CurrentThread.ManagedThreadId != s.host.MainThreadId || !s.window!.InTick || s.suppressed) return 0;
            try
            {
                var site = s.Instrumentation.Sites[siteId];
                if (s.window.CurrentMethod != site.CallerId) return 0;
                string target = s.Instrumentation.Resolve(site, receiver);
                int token = s.window.Enter(target, site.Id, Now);
                if (token != 0) s.expectedBody = target;
                return token;
            }
            catch (Exception ex) { s.ObserverError(ex); return 0; }
        }
        public static void CallAfter(int token, bool success)
        {
            if (token == 0) return; var s = Self;
            try { s.window!.Exit(token, Now, !success); s.expectedBody = null; }
            catch (Exception ex) { s.ObserverError(ex); }
        }
    }
}
