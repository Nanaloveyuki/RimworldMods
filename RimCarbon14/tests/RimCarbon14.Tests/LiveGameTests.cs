using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json.Linq;
using RimCarbon14.Agent;
using Xunit;

namespace RimCarbon14.Tests
{
    public sealed class LiveGameFactAttribute : FactAttribute
    {
        public LiveGameFactAttribute() { if (Environment.GetEnvironmentVariable("CARBON_LIVE_PROFILE") == null) Skip = "Set CARBON_LIVE_PROFILE and RIMWORLD_DIR to opt into real-game acceptance."; }
    }
    public sealed class LiveGameTests
    {
        [LiveGameFact] public async Task PublishedMcpExposesRealGameToolsAndPngContent()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "RimCarbon14.slnx"))) root = root.Parent;
            Assert.NotNull(root);
            var profile = Environment.GetEnvironmentVariable("CARBON_LIVE_PROFILE")!;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "carbon-live-test", Command = Path.Combine(root.FullName, "artifacts", "RimCarbon14", "Tools", "win-x64", "RimCarbon14.Agent.exe"),
                Arguments = new[] { "mcp", "--profile", profile, "--game-root", Environment.GetEnvironmentVariable("RIMWORLD_DIR")! }
            }), cancellationToken: timeout.Token);
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
            Assert.Contains(tools, t => t.Name == "pawn_get");
            Assert.Contains(tools, t => t.Name == "capture_screen");
            Assert.Contains(tools, t => t.Name == "desktop_input");
            foreach (var name in new[] { "watch_start", "watch_snapshot", "watch_report", "watch_expand", "watch_stop" })
                Assert.Contains(tools, t => t.Name == name);
            var result = await client.CallToolAsync("capture_screen", cancellationToken: timeout.Token);
            Assert.False(result.IsError);
            var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
            var bytes = image.DecodedData.ToArray();
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray());
            Assert.True(bytes.Length > 10000);
            File.WriteAllBytes(Path.Combine(profile, "RimCarbon14", "mcp-acceptance.png"), bytes);
            if (Environment.GetEnvironmentVariable("CARBON_DESKTOP_TESTS") == "1")
            {
                using var bridge = new BridgeClient(profile);
                var layout = await bridge.Call("ui_layout", new JObject(), ct: timeout.Token);
                var field = ((JArray)layout.SelectToken("result.data.elements")!).First(e => (string?)e["kind"] == "text" && ((string?)e["surface"])!.Contains("ProbeWindow", StringComparison.Ordinal));
                async Task Input(object arguments)
                {
                    var values = JObject.FromObject(arguments).Properties().ToDictionary(p => p.Name, p => (object?)((JValue)p.Value).Value);
                    var reply = await client.CallToolAsync("desktop_input", values, cancellationToken: timeout.Token);
                    Assert.False(reply.IsError);
                }
                await Input(new { kind = "click", x = (int)field["screenRect"]!["x"]! + 40, y = (int)field["screenRect"]!["y"]! + 15 });
                await Input(new { kind = "key", key = "Ctrl+A" });
                await Input(new { kind = "text", text = "mcp-desktop-42" });
                var rootObject = await bridge.Call("objects_read", new JObject { ["type"] = "RimCarbon14.Fixture.Fixture", ["member"] = "Root" }, ct: timeout.Token);
                var text = await bridge.Call("objects_read", new JObject { ["handle"] = rootObject.SelectToken("result.data.handle"), ["member"] = "Text" }, ct: timeout.Token);
                Assert.Equal("mcp-desktop-42", (string?)text.SelectToken("result.data"));
            }
        }
        [LiveGameFact] public async Task HttpRejectsMissingCredentialsAndBrowserOrigins()
        {
            using var bridge = new BridgeClient(Environment.GetEnvironmentVariable("CARBON_LIVE_PROFILE")!);
            var d = bridge.Discover(); using var http = new HttpClient();
            using var anonymous = await http.GetAsync(new Uri(new Uri(d.Endpoint), "v1/status"));
            Assert.Equal(HttpStatusCode.Forbidden, anonymous.StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(d.Endpoint), "v1/status"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", d.Token); request.Headers.Add("Origin", "https://example.invalid");
            using var browser = await http.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, browser.StatusCode);
            Assert.Equal(d.InstanceId, (string?)(await bridge.Status())["instanceId"]);
        }
    }
}
