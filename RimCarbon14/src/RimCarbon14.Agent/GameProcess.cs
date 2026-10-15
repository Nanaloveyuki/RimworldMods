using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;

namespace RimCarbon14.Agent
{
    public sealed class GameProcess
    {
        private readonly BridgeClient bridge;
        private readonly string gameRoot;
        private string OwnerFile => Path.Combine(bridge.Profile, "RimCarbon14", "process.json");
        public GameProcess(BridgeClient bridge, string gameRoot) { this.bridge = bridge; this.gameRoot = Path.GetFullPath(gameRoot); }
        public JObject Prepare(string? source, bool minimal)
        {
            var config = Path.Combine(bridge.Profile, "Config");
            if (Directory.Exists(config) && Directory.EnumerateFileSystemEntries(config).Any()) throw new BridgeException("profile_exists", "Profile configuration already exists; it will not be overwritten.");
            Directory.CreateDirectory(config); Directory.CreateDirectory(Path.Combine(bridge.Profile, "Saves"));
            if (minimal)
            {
                new XDocument(new XElement("ModsConfigData", new XElement("version", File.ReadAllText(Path.Combine(gameRoot, "Version.txt")).Trim()), new XElement("activeMods", new[] { "brrainz.harmony", "ludeon.rimworld", "nanaloveyuki.rimcarbon14" }.Select(id => new XElement("li", id))))).Save(Path.Combine(config, "ModsConfig.xml"));
                var sourceProfile = source ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Ludeon Studios", "RimWorld by Ludeon Studios");
                var prefsFile = Path.Combine(sourceProfile, "Config", "Prefs.xml");
                if (File.Exists(prefsFile))
                {
                    var prefs = XDocument.Load(prefsFile);
                    PrepareTestPreferences(prefs).Save(Path.Combine(config, "Prefs.xml"));
                }
            }
            else
            {
                if (source == null) throw new BridgeException("missing_source", "Provide a source save-data folder or request a minimal profile.");
                foreach (var file in Directory.GetFiles(Path.Combine(source, "Config"), "*.xml")) File.Copy(file, Path.Combine(config, Path.GetFileName(file)));
                var mods = XDocument.Load(Path.Combine(config, "ModsConfig.xml"));
                var active = mods.Root!.Element("activeMods")!;
                if (!active.Elements("li").Any(e => e.Value == "nanaloveyuki.rimcarbon14")) active.Add(new XElement("li", "nanaloveyuki.rimcarbon14"));
                mods.Save(Path.Combine(config, "ModsConfig.xml"));
            }
            return new JObject { ["profile"] = bridge.Profile, ["minimal"] = minimal };
        }
        public static XDocument PrepareTestPreferences(XDocument source)
        {
            var prefs = new XDocument(source);
            // Preserve language, accessibility and other preferences; only the isolated rendering profile changes.
            prefs.Root!.SetElementValue("runInBackground", true);
            prefs.Root.SetElementValue("fullscreen", false);
            prefs.Root.SetElementValue("screenWidth", 1280);
            prefs.Root.SetElementValue("screenHeight", 720);
            return prefs;
        }
        private Process? Owned()
        {
            if (!File.Exists(OwnerFile)) return null;
            var owner = JObject.Parse(File.ReadAllText(OwnerFile));
            try
            {
                var p = Process.GetProcessById((int)owner["pid"]!);
                if (p.HasExited || p.StartTime.ToUniversalTime().Ticks != (long)owner["startTicks"]! || !string.Equals(p.MainModule?.FileName, Path.Combine(gameRoot, "RimWorldWin64.exe"), StringComparison.OrdinalIgnoreCase)) { p.Dispose(); return null; }
                return p;
            }
            catch (ArgumentException) { return null; }
        }
        private FileStream LifecycleLease()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OwnerFile)!);
            try { return new FileStream(Path.Combine(bridge.Profile, "RimCarbon14", "lifecycle.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) { throw new BridgeException("lifecycle_busy", "Another companion is managing this profile: " + ex.Message); }
        }
        public bool IsOwnedAlive { get { using var process = Owned(); return process != null; } }
        public async Task<JObject> Start(int timeoutMs, CancellationToken ct)
        {
            using var lease = LifecycleLease();
            return await StartCore(timeoutMs, ct);
        }
        private async Task<JObject> StartCore(int timeoutMs, CancellationToken ct)
        {
            using var owned = Owned();
            if (owned != null) return await WaitReady(owned, timeoutMs, ct);
            if (!File.Exists(Path.Combine(bridge.Profile, "Config", "ModsConfig.xml"))) throw new BridgeException("profile_not_prepared", "Run profile_prepare first.");
            if (File.Exists(Path.Combine(bridge.Profile, "RimCarbon14", "discovery.json")))
            {
                try { var status = await bridge.Status(ct); throw new BridgeException("unowned_instance", "An unowned bridge is already active in this profile: " + status["instanceId"]); }
                catch (HttpRequestException) { }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(OwnerFile)!);
            // RimWorld must own a rendered window: a hidden Unity player can tick without repainting its UI.
            // Shell launch prevents the long-lived game from inheriting the companion's stdio/MCP pipes.
            var info = new ProcessStartInfo(Path.Combine(gameRoot, "RimWorldWin64.exe")) { WorkingDirectory = gameRoot, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal };
            info.ArgumentList.Add("-savedatafolder=" + bridge.Profile);
            info.ArgumentList.Add("-screen-fullscreen"); info.ArgumentList.Add("0");
            info.ArgumentList.Add("-screen-width"); info.ArgumentList.Add("1280");
            info.ArgumentList.Add("-screen-height"); info.ArgumentList.Add("720");
            info.ArgumentList.Add("-logFile"); info.ArgumentList.Add(Path.Combine(bridge.Profile, "Player.log"));
            using var process = Process.Start(info) ?? throw new BridgeException("start_failed", "Process did not start.");
            File.WriteAllText(OwnerFile, Wire.Json(new { pid = process.Id, startTicks = process.StartTime.ToUniversalTime().Ticks, executable = info.FileName }));
            return await WaitReady(process, timeoutMs, ct);
        }
        private async Task<JObject> WaitReady(Process process, int timeoutMs, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            string last = "No bridge yet";
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                if (process.HasExited) throw new BridgeException("game_exited", $"Game exited with {process.ExitCode}; see {Path.Combine(bridge.Profile, "Player.log")}");
                try
                {
                    var status = await bridge.Status(ct);
                    if ((int?)status["pid"] == process.Id && (int?)status["frame"] > 0 && (bool?)status["mainThreadResponsive"] == true) return new JObject { ["pid"] = process.Id, ["bridge"] = status };
                }
                catch (Exception ex) when (ex is IOException || ex is BridgeException || ex is TaskCanceledException) { last = ex.Message; }
                catch (System.Net.Http.HttpRequestException ex) { last = ex.Message; }
                await Task.Delay(500, ct);
            }
            throw new BridgeException("startup_timeout", "Game process remains running; bridge did not become ready: " + last);
        }
        public async Task<JObject> Status(CancellationToken ct)
        {
            using var owned = Owned();
            var status = new JObject { ["ownedProcessAlive"] = owned != null, ["pid"] = owned?.Id };
            try
            {
                status["bridge"] = await bridge.Status(ct);
                status["bridgeReachable"] = true;
                status["mainThreadResponsive"] = (bool?)status["bridge"]!["mainThreadResponsive"] ?? false;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                status["bridgeReachable"] = false; status["mainThreadResponsive"] = false;
                status["bridgeError"] = ex.Message;
            }
            return status;
        }
        public async Task<JObject> Stop(bool force, CancellationToken ct)
        {
            using var lease = LifecycleLease();
            return await StopCore(force, ct);
        }
        private async Task<JObject> StopCore(bool force, CancellationToken ct)
        {
            using var process = Owned();
            if (process == null) return new JObject { ["stopped"] = false, ["reason"] = "No owned running process." };
            if (!force)
            {
                try { await bridge.Call("game_exit", new JObject(), 3000, ct); }
                catch (Exception ex) { Console.Error.WriteLine("Shutdown acknowledgement: " + ex.Message); }
                var done = process.WaitForExitAsync(ct);
                if (await Task.WhenAny(done, Task.Delay(15000, ct)) != done) throw new BridgeException("shutdown_timeout", "Game remains running. Explicit force=true is required to terminate it.");
            }
            else { process.Kill(true); await process.WaitForExitAsync(ct); }
            return new JObject { ["stopped"] = true, ["pid"] = process.Id, ["forced"] = force };
        }
        public async Task<JObject> Restart(bool force, int timeoutMs, CancellationToken ct)
        {
            using var lease = LifecycleLease();
            await StopCore(force, ct);
            return await StartCore(timeoutMs, ct);
        }
    }
}
