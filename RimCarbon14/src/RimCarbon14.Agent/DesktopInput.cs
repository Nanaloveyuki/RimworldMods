using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;

namespace RimCarbon14.Agent
{
    public static class DesktopInput
    {
        public static async Task<JToken> Send(BridgeClient bridge, JObject args, CancellationToken ct)
        {
            if (!OperatingSystem.IsWindows()) throw new BridgeException("unsupported_platform", "Desktop input requires Windows.");
            ct.ThrowIfCancellationRequested();
            var script = Path.Combine(AppContext.BaseDirectory, "desktop-input.ps1");
            if (!File.Exists(script)) throw new BridgeException("missing_input_helper", script);
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var value in new[] { "-NoProfile", "-NonInteractive", "-File", script, "-Profile", bridge.Profile, "-Kind", Wire.Required(args, "kind") }) info.ArgumentList.Add(value);
            foreach (var name in new[] { "x", "y", "endX", "endY", "button", "wheelDelta", "key" })
                if (args[name] != null) { info.ArgumentList.Add("-" + name); info.ArgumentList.Add(args[name]!.ToString()); }
            if (args["text"] != null) { info.ArgumentList.Add("-TextBase64"); info.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes((string)args["text"]!))); }
            using var process = Process.Start(info) ?? throw new BridgeException("input_start_failed", "PowerShell did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            // Do not terminate a gesture midway: the helper must release pressed buttons/keys.
            await process.WaitForExitAsync(CancellationToken.None);
            var output = await stdout; var error = await stderr;
            if (process.ExitCode != 0) throw new BridgeException("desktop_input_failed", error.Trim());
            return JObject.Parse(output);
        }
    }
}
