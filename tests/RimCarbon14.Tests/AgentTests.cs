using System.Xml.Linq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Client;
using Newtonsoft.Json.Linq;
using RimCarbon14.Agent;
using RimCarbon14.Core;
using Xunit;

namespace RimCarbon14.Tests
{
    public sealed class AgentTests
    {
        [Fact] public async Task StdioMcpInitializesAndListsOfflineLifecycleTools()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "RimCarbon14.slnx"))) root = root.Parent;
            Assert.NotNull(root);
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            var dll = Path.Combine(root.FullName, "src", "RimCarbon14.Agent", "bin", "Debug", "net10.0", "RimCarbon14.Agent.dll");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "carbon-test", Command = host,
                Arguments = new[] { dll, "mcp", "--profile", Path.Combine(root.FullName, "local", "offline-test"), "--game-root", root.FullName }
            }), cancellationToken: timeout.Token);
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
            Assert.Contains(tools, tool => tool.Name == "process_start");
            Assert.Contains(tools, tool => tool.Name == "bridge_call");
            Assert.Contains(tools, tool => tool.Name == "desktop_input");
            var result = await client.CallToolAsync("bridge_status", cancellationToken: timeout.Token);
            Assert.True(result.IsError);
        }
        [Fact] public void MinimalProfilePreservesLanguageWithoutChangingSource()
        {
            var source = XDocument.Parse("<PrefsData><langFolderName>ChineseSimplified</langFolderName><uiScale>1.25</uiScale><runInBackground>False</runInBackground></PrefsData>");
            var result = GameProcess.PrepareTestPreferences(source);
            Assert.Equal("ChineseSimplified", result.Root!.Element("langFolderName")!.Value);
            Assert.Equal("1.25", result.Root.Element("uiScale")!.Value);
            Assert.Equal("true", result.Root.Element("runInBackground")!.Value);
            Assert.Equal("False", source.Root!.Element("runInBackground")!.Value);
        }
        [Fact] public void ScenarioReferencesPreserveTypedValues()
        {
            var result = TestRunner.Resolve(JObject.Parse("{\"ticks\":{\"$ref\":\"a.count\"}}"), JObject.Parse("{\"a\":{\"count\":3}}"));
            Assert.Equal(JTokenType.Integer, result["ticks"]!.Type);
            Assert.Equal(3, (int)result["ticks"]!);
        }
        [Fact] public void RecoveryRequiresAnExplicitBoundAndResetSave()
        {
            Assert.Equal(0, TestRunner.RecoveryLimit(new JObject()));
            Assert.Equal(1, TestRunner.RecoveryLimit(JObject.Parse("{\"recovery\":{\"maxCrashRestarts\":1,\"resetSave\":\"fixture\"}}")));
            Assert.Throws<BridgeException>(() => TestRunner.RecoveryLimit(JObject.Parse("{\"recovery\":{\"maxCrashRestarts\":1}}")));
            Assert.Throws<BridgeException>(() => TestRunner.RecoveryLimit(JObject.Parse("{\"recovery\":{\"maxCrashRestarts\":4,\"resetSave\":\"fixture\"}}")));
        }
        [Theory]
        [InlineData("{}")]
        [InlineData("{\"kind\":\"click\",\"x\":\"2\"}")]
        [InlineData("{\"kind\":\"click\",\"unexpected\":true}")]
        public async Task DesktopContractRejectsInvalidArgumentsBeforeLaunchingInput(string json)
        {
            using var bridge = new BridgeClient(AppContext.BaseDirectory);
            var process = new GameProcess(bridge, AppContext.BaseDirectory);
            var error = await Assert.ThrowsAsync<BridgeException>(() => Program.Dispatch(bridge, process, "desktop_input", JObject.Parse(json), CancellationToken.None));
            Assert.Equal("invalid_arguments", error.Code);
        }
        [Fact] public void AssertionsFailOnWrongOrMissingData()
        {
            var value = JObject.Parse("{\"count\":2}");
            TestRunner.Assert(value, JObject.Parse("{\"path\":\"count\",\"min\":1,\"max\":3}"));
            Assert.Throws<BridgeException>(() => TestRunner.Assert(value, JObject.Parse("{\"path\":\"count\",\"equals\":3}")));
            Assert.Throws<BridgeException>(() => TestRunner.Assert(value, JObject.Parse("{\"path\":\"missing\",\"exists\":true}")));
        }
    }
}
