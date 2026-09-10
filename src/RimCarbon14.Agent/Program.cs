using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;

namespace RimCarbon14.Agent
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args[0] == "--help") { Console.WriteLine("RimCarbon14.Agent <mcp|prepare|start|stop|restart|status|tools|call TOOL|run FILE> --profile PATH --game-root PATH [--args JSON|--args-file FILE] [--timeout-ms N] [--minimal] [--source PATH] [--force]"); return 0; }
                string? Option(string key) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                var profile = Option("--profile") ?? throw new ArgumentException("--profile is required.");
                var gameRoot = Option("--game-root") ?? Environment.GetEnvironmentVariable("RIMWORLD_DIR") ?? throw new ArgumentException("--game-root or RIMWORLD_DIR is required.");
                using var bridge = new BridgeClient(profile); var process = new GameProcess(bridge, gameRoot);
                var arguments = JObject.Parse(Option("--args-file") is string file ? File.ReadAllText(file) : Option("--args") ?? "{}");
                var timeout = int.TryParse(Option("--timeout-ms"), out var ms) ? ms : 600000;
                if (args[0] == "mcp") { await Serve(bridge, process); return 0; }
                if (args[0] == "run") return await TestRunner.Run(bridge, process, args[1], CancellationToken.None);
                JToken result = args[0] switch
                {
                    "prepare" => process.Prepare(Option("--source"), args.Contains("--minimal")),
                    "start" => await process.Start(timeout, CancellationToken.None),
                    "stop" => await process.Stop(args.Contains("--force"), CancellationToken.None),
                    "restart" => await process.Restart(args.Contains("--force"), timeout, CancellationToken.None),
                    "status" => await bridge.Status(),
                    "tools" => Wire.Token(await bridge.Tools()),
                    "call" => await Dispatch(bridge, process, args[1], arguments, CancellationToken.None, int.TryParse(Option("--timeout-ms"), out var callMs) ? callMs : 10000),
                    _ => throw new ArgumentException("Unknown command: " + args[0])
                };
                Console.WriteLine(Wire.Json(result));
                return (string?)result["state"] is "failed" or "cancelled" ? 1 : 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(Wire.Json(new { error = (ex as BridgeException)?.Code ?? ex.GetType().Name, message = ex.Message })); return 1; }
        }
        private static readonly ToolSpec[] LocalTools =
        {
            Spec("desktop_input", "Move the real Windows mouse or send keys/text to the foreground game. kind: click/drag/scroll/key/text. Coordinates: game-client physical pixels. key: Escape, Ctrl+A, etc. Do not use concurrently with manual input. Check game state afterwards; sent is not verified effect.", "kind!:string", "x:integer", "y:integer", "endX:integer", "endY:integer", "button:string", "wheelDelta:integer", "key:string", "text:string"),
            Spec("process_start", "Start the prepared isolated game profile and wait for bridge readiness.", "timeoutMs:integer"),
            Spec("process_status", "Distinguish owned process liveness, bridge reachability and main-thread heartbeat."),
            Spec("process_stop", "Stop only this profile's owned game process; force explicitly terminates an unresponsive process.", "force:boolean"),
            Spec("process_restart", "Stop and restart owned game; does not replay prior mutations.", "force:boolean", "timeoutMs:integer"),
            Spec("profile_prepare", "Create an isolated profile from a config copy or a minimal vanilla/Harmony/Carbon mod list.", "source:string", "minimal:boolean"),
            Spec("bridge_status", "Read current cached main-thread heartbeat and readiness."),
            Spec("bridge_events", "Read game logs, tool lifecycle and detailed trace events with a loss-aware cursor.", "after:integer", "limit:integer"),
            Spec("bridge_operation", "Query or cancel an uncertain operation in the currently connected instance.", "id:string", "cancel:boolean"),
            Spec("bridge_call", "Call a game tool by name, including before MCP tool-list refresh. Mutation is never automatically replayed.", "tool:string", "arguments:object", "timeoutMs:integer")
        };
        private static ToolSpec Spec(string name, string description, params string[] properties) => new ToolSpec
        {
            Name = name, Description = description, Effect = name is "bridge_status" or "bridge_events" or "process_status" ? "read" : "write",
            InputSchema = new JObject
            {
                ["type"] = "object", ["additionalProperties"] = false,
                ["properties"] = new JObject(properties.Select(p => new JProperty(p.Split(':')[0].TrimEnd('!'), new JObject { ["type"] = p.Split(':')[1] }))),
                ["required"] = new JArray(properties.Where(p => p.Split(':')[0].EndsWith('!')).Select(p => p.Split(':')[0].TrimEnd('!')))
            }
        };
        public static async Task<JToken> Dispatch(BridgeClient bridge, GameProcess process, string name, JObject args, CancellationToken ct, int timeoutMs = 10000)
        {
            LocalTools.FirstOrDefault(t => t.Name == name)?.Validate(args);
            return name switch
            {
                "desktop_input" => await DesktopInput.Send(bridge, args, ct),
                "process_start" => await process.Start((int?)args["timeoutMs"] ?? 600000, ct),
                "process_status" => await process.Status(ct),
                "process_stop" => await process.Stop((bool?)args["force"] ?? false, ct),
                "process_restart" => await process.Restart((bool?)args["force"] ?? false, (int?)args["timeoutMs"] ?? 600000, ct),
                "profile_prepare" => process.Prepare((string?)args["source"], (bool?)args["minimal"] ?? false),
                "bridge_status" => await bridge.Status(ct),
                "bridge_events" => await bridge.Request(bridge.Discover(), HttpMethod.Get, $"v1/events?after={(long?)args["after"] ?? 0}&limit={(int?)args["limit"] ?? 100}", ct: ct),
                "bridge_operation" => await bridge.Request(bridge.Discover(), ((bool?)args["cancel"] ?? false) ? HttpMethod.Delete : HttpMethod.Get, "v1/operations/" + Uri.EscapeDataString(Wire.Required(args, "id")), ct: ct),
                "bridge_call" => await bridge.Call(Wire.Required(args, "tool"), args["arguments"] as JObject ?? new JObject(), (int?)args["timeoutMs"] ?? 10000, ct),
                _ => await bridge.Call(name, args, timeoutMs, ct)
            };
        }
        private static async Task Serve(BridgeClient bridge, GameProcess process)
        {
            var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
            builder.Services.AddMcpServer().WithStdioServerTransport()
                .WithListToolsHandler(async (_, ct) =>
                {
                    var tools = LocalTools.ToList();
                    try { tools.AddRange(await bridge.Tools(ct)); }
                    catch (Exception ex) { Console.Error.WriteLine("Game tool discovery unavailable: " + ex.Message); }
                    return new ListToolsResult { Tools = tools.Select(t => new Tool { Name = t.Name, Description = t.Description, InputSchema = JsonDocument.Parse(t.InputSchema.ToString()).RootElement.Clone(), Annotations = new ToolAnnotations { ReadOnlyHint = t.Effect == "read", DestructiveHint = t.Effect != "read", OpenWorldHint = false } }).ToList() };
                })
                .WithCallToolHandler(async (context, ct) =>
                {
                    try
                    {
                        var name = context.Params!.Name;
                        var data = context.Params.Arguments == null ? new JObject() : JObject.Parse(System.Text.Json.JsonSerializer.Serialize(context.Params.Arguments));
                        var result = await Dispatch(bridge, process, name, data, ct);
                        var content = new List<ContentBlock> { new TextContentBlock { Text = Wire.Json(result) } };
                        if ((string?)result.SelectToken("result.data.artifactId") is string artifact)
                            content.Add(ImageContentBlock.FromBytes(await bridge.Artifact(artifact, ct), "image/png"));
                        if (name is "process_start" or "process_restart") await context.Server.SendNotificationAsync("notifications/tools/list_changed", ct);
                        return new CallToolResult { Content = content, IsError = (string?)result["state"] is "failed" or "cancelled" };
                    }
                    catch (Exception ex) { return new CallToolResult { IsError = true, Content = new List<ContentBlock> { new TextContentBlock { Text = Wire.Json(new { error = (ex as BridgeException)?.Code ?? ex.GetType().Name, message = ex.Message }) } } }; }
                });
            await builder.Build().RunAsync();
        }
    }
}
