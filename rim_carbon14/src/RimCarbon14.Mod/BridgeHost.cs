using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using UnityEngine;
using Verse;

namespace RimCarbon14.Mod
{
    public sealed class CarbonMod : Verse.Mod
    {
        public CarbonMod(ModContentPack content) : base(content) { }
    }

    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            var obj = new GameObject("RimCarbon14");
            UnityEngine.Object.DontDestroyOnLoad(obj);
            obj.AddComponent<BridgeHost>();
        }
    }

    public sealed class BridgeHost : MonoBehaviour
    {
        public static BridgeHost Instance { get; private set; } = null!;
        public readonly EventBuffer Events = new EventBuffer();
        public readonly ObjectHandles Handles = new ObjectHandles();
        public readonly Dictionary<string, ToolSpec> Specs = new Dictionary<string, ToolSpec>();
        private readonly Dictionary<string, Func<JObject, Operation, JToken?>> tools = new Dictionary<string, Func<JObject, Operation, JToken?>>();
        public Operations Calls = null!;
        public ReflectionTools Reflection = null!;
        public HttpBridge Http = null!;
        public UiTools Ui = null!;
        public TraceTools Trace = null!;
        public WatchTools Watch = null!;
        public CaptureTools Capture = null!;
        public string InstanceId = Guid.NewGuid().ToString("N");
        public string DataDirectory = "";
        public string ArtifactDirectory = "";
        public int MainThreadId;
        [ThreadStatic] public static string? CurrentOperationId;
        private Game? game;
        private string? mutation;
        private string status = "{}";
        public string StatusJson => Volatile.Read(ref status);
        public bool Ready => Current.ProgramState == ProgramState.Playing && Current.Game != null && !LongEventHandler.AnyEventNowOrWaiting;
        public string Session => Handles.Session;
        public JObject Stamp() => new JObject { ["instanceId"] = InstanceId, ["session"] = Session, ["frame"] = Time.frameCount, ["tick"] = Current.Game?.tickManager?.TicksGame, ["utc"] = DateTime.UtcNow };
        public JObject LiveStatus() => (JObject)Wire.Token(new { protocolVersion = Wire.Version, instanceId = InstanceId, session = Session, pid = Process.GetCurrentProcess().Id, frame = Time.frameCount, tick = Current.Game?.tickManager?.TicksGame, utc = DateTime.UtcNow, programState = Current.ProgramState.ToString(), ready = Ready, loading = LongEventHandler.AnyEventNowOrWaiting, playerHasControl = Ready && Current.Game?.PlayerHasControl == true, paused = Current.Game?.tickManager?.Paused, gameVersion = RimWorld.VersionControl.CurrentVersionString, modVersion = "0.1.0" });

        public void Awake()
        {
            Instance = this;
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            DataDirectory = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimCarbon14");
            ArtifactDirectory = Path.Combine(DataDirectory, "artifacts", InstanceId);
            Directory.CreateDirectory(ArtifactDirectory);
            Calls = new Operations(Events);
            Reflection = new ReflectionTools(Handles);
            Ui = new UiTools(this);
            Trace = new TraceTools(this);
            Watch = new WatchTools(this);
            Capture = new CaptureTools(this);
            RegisterTools();
            Ui.Install();
            Application.runInBackground = true;
            Application.logMessageReceivedThreaded += OnLog;
            Http = new HttpBridge(this);
            Http.Start();
            Log.Message("[RimCarbon14] Ready. Discovery: " + Path.Combine(DataDirectory, "discovery.json"));
        }

        public void Update()
        {
            try
            {
                if (!ReferenceEquals(game, Current.Game))
                {
                    game = Current.Game;
                    Handles.Reset();
                    Ui.Reset();
                    Trace.Stop();
                    Watch.Stop();
                    Events.Add("session.changed", new { Session });
                }
                Ui.Advance();
                Trace.Advance();
                Watch.Advance();
                Application.runInBackground = true;
                Volatile.Write(ref status, Wire.Json(LiveStatus()));
                Calls.Pump();
            }
            catch (Exception ex) { Events.Add("bridge.update_error", new { ex.Message, stack = ex.StackTrace }); }
        }

        public void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Http?.Dispose();
            Trace?.Stop();
            Watch?.Stop("host_destroyed");
            Ui?.Dispose();
        }
        public void OnApplicationQuit() { Watch?.Stop("game_exit"); Http?.Dispose(); }
        private void OnLog(string message, string stack, LogType type)
        {
            Events.Add("game.log", new { message, stack, level = type.ToString(), thread = Thread.CurrentThread.ManagedThreadId });
        }

        public JObject Submit(CallRequest request)
        {
            if (!Specs.TryGetValue(request.Tool, out var spec)) throw new BridgeException("unknown_tool", request.Tool);
            if (request.Session == null) throw new BridgeException("missing_session", "Provide the session returned by /v1/status.");
            if (request.Arguments == null) throw new BridgeException("invalid_arguments", "arguments must be an object.");
            spec.Validate(request.Arguments);
            var handler = tools[request.Tool];
            return Calls.Submit(request, op =>
            {
                var changingSession = (request.Tool == "game_load" || request.Tool == "game_new_test") && op.Context != null;
                if (request.Session != null && request.Session != Session && !changingSession) throw new BridgeException("stale_session", "Game session changed.");
                if (spec.RequiresGame && !Ready) throw new BridgeException("game_not_ready", "An idle loaded game is required.");
                if (LongEventHandler.AnyEventNowOrWaiting && request.Tool != "game_load" && request.Tool != "game_new_test" && request.Tool != "game_state") return null;
                if (spec.Effect != "read")
                {
                    if (mutation != null && mutation != op.Id) return null;
                    if (mutation == null)
                    {
                        mutation = op.Id;
                        var cleanup = op.Cleanup;
                        op.Cleanup = () => { try { cleanup?.Invoke(); } finally { if (mutation == op.Id) mutation = null; } };
                    }
                }
                var priorOperation = CurrentOperationId;
                try
                {
                    CurrentOperationId = op.Id;
                    var result = handler(request.Arguments, op);
                    return result == null ? null : new JObject { ["data"] = result, ["stamp"] = Stamp() };
                }
                finally { CurrentOperationId = priorOperation; }
            });
        }

        public void Add(string name, string description, string effect, bool gameRequired, Func<JObject, Operation, JToken?> handler, params string[] parameters)
        {
            var properties = new JObject();
            var required = new JArray();
            foreach (var parameter in parameters)
            {
                var pair = parameter.Split(':');
                var key = pair[0].TrimEnd('!');
                var type = pair.Length > 1 ? pair[1] : "string";
                properties[key] = type == "any" ? new JObject() : new JObject { ["type"] = type };
                if (pair[0].EndsWith("!", StringComparison.Ordinal)) required.Add(key);
            }
            Specs.Add(name, new ToolSpec { Name = name, Description = description, Effect = effect, RequiresGame = gameRequired, InputSchema = new JObject { ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false } });
            tools.Add(name, handler);
        }
        public void AddRead(string name, string description, Func<JObject, JToken> handler, params string[] parameters) => Add(name, description, "read", false, (a, _) => handler(a), parameters);
        public JToken Probe(string name, JObject arguments)
        {
            if (!Specs.TryGetValue(name, out var spec) || spec.Effect != "read" || name == "wait_until") throw new BridgeException("invalid_probe", "A synchronous read-only tool is required.");
            if (spec.RequiresGame && !Ready) throw new BridgeException("game_not_ready", "Probe requires a loaded game.");
            spec.Validate(arguments);
            return tools[name](arguments, new Operation()) ?? throw new BridgeException("invalid_probe", "Probe did not return a synchronous snapshot.");
        }

        private void RegisterTools()
        {
            GameTools.Register(this);
            DiagnosticsTools.Register(this);
            AddRead("objects_types", "Find loaded types without constructing objects.", Reflection.SearchTypes, "search", "assembly", "offset:integer", "limit:integer");
            AddRead("objects_members", "List fields, properties and exact method IDs; no getters are evaluated.", Reflection.Members, "handle", "type", "search", "offset:integer", "limit:integer");
            AddRead("objects_read", "Read a field, never implicitly evaluate a property.", a => Reflection.Read(a, false), "handle", "type", "member!", "declaringType");
            Add("objects_evaluate", "Explicitly execute a property getter. May have side effects.", "invoke", false, (a, _) => Reflection.Read(a, true), "handle", "type", "member!", "declaringType");
            Add("objects_write", "Explicit field/property write without rollback. Boxed structs require writing back to their parent.", "write", false, (a, _) => Reflection.Write(a), "handle", "type", "member!", "declaringType", "value!:any");
            Add("objects_invoke", "Invoke an exact loaded method; no forced interruption or rollback. Task results are handles, not implicitly awaited.", "invoke", false, (a, _) => Reflection.Invoke(a), "methodId!", "handle", "arguments:array");
            AddRead("objects_items", "Explicit indexed access to IList/arrays, without arbitrary enumeration.", Reflection.Items, "handle!", "offset:integer", "limit:integer");
            AddRead("objects_release", "Release retained object handles.", a => Wire.Token(new { released = Handles.Release(((JArray?)a["handles"] ?? new JArray()).Values<string>().Select(x => x!)) }), "handles!:array");
            Ui.Register();
            Trace.Register();
            Watch.Register();
            Capture.Register();
        }
    }
}
