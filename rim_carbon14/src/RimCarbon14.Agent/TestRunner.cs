using System;
using System.Collections.Generic;
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
    public static class TestRunner
    {
        public static async Task<int> Run(BridgeClient bridge, GameProcess process, string file, CancellationToken ct)
        {
            var scenario = JObject.Parse(File.ReadAllText(file));
            var output = Path.Combine(bridge.Profile, "RimCarbon14", "runs", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(output);
            var maximumRestarts = RecoveryLimit(scenario);
            var steps = (JArray)((JArray?)scenario["steps"] ?? throw new BridgeException("invalid_scenario", "steps array is required.")).DeepClone();
            if (maximumRestarts > 0)
            {
                steps.Insert(0, new JObject { ["id"] = "_recovery_load", ["tool"] = "game_load", ["arguments"] = new JObject { ["name"] = scenario["recovery"]!["resetSave"] }, ["timeoutMs"] = 180000 });
                steps.Insert(0, new JObject { ["id"] = "_recovery_start", ["tool"] = "process_start", ["arguments"] = new JObject { ["timeoutMs"] = 600000 } });
            }
            var ids = new HashSet<string>();
            foreach (var item in steps)
            {
                if (item is not JObject step) throw new BridgeException("invalid_scenario", "Steps must be objects.");
                var id = Wire.Required(step, "id");
                if (id == "_evidence" || id != Path.GetFileName(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !ids.Add(id))
                    throw new BridgeException("invalid_scenario", "Step IDs must be unique filename-safe names without evidence/recovery-reserved IDs.");
            }
            var attempts = new JArray(); bool passed = false;
            for (int attempt = 0; attempt <= maximumRestarts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var path = maximumRestarts == 0 ? output : Path.Combine(output, "attempt-" + (attempt + 1));
                Directory.CreateDirectory(path);
                var result = await RunAttempt(bridge, process, scenario, steps, path, ct);
                attempts.Add(new JObject { ["attempt"] = attempt + 1, ["passed"] = result.Passed, ["processExitedDuringStep"] = result.Crashed, ["output"] = path });
                passed = result.Passed;
                if (passed || !result.Crashed || ct.IsCancellationRequested) break;
            }
            var summary = new JObject { ["passed"] = passed, ["recovered"] = passed && attempts.Count > 1, ["output"] = output, ["attempts"] = attempts };
            File.WriteAllText(Path.Combine(output, "run.json"), Wire.Json(summary));
            if (maximumRestarts > 0)
            {
                File.WriteAllText(Path.Combine(output, "results.json"), Wire.Json(summary));
                new XDocument(new XElement("testsuite", new XAttribute("name", (string?)scenario["name"] ?? "scenario"), new XAttribute("tests", 1), new XAttribute("failures", passed ? 0 : 1),
                    new XElement("properties", new XElement("property", new XAttribute("name", "attempts"), new XAttribute("value", attempts.Count))),
                    new XElement("testcase", new XAttribute("name", "scenario"), passed ? null : new XElement("failure", "Scenario failed; see attempt directories."), new XElement("system-out", Wire.Json(summary))))).Save(Path.Combine(output, "junit.xml"));
            }
            Console.WriteLine(Wire.Json(summary));
            return passed ? 0 : 1;
        }
        public static int RecoveryLimit(JObject scenario)
        {
            var recovery = scenario["recovery"];
            if (recovery == null) return 0;
            if (recovery is not JObject obj || obj["maxCrashRestarts"]?.Type != JTokenType.Integer) throw new BridgeException("invalid_recovery", "recovery.maxCrashRestarts must be an integer.");
            var maximum = (int)obj["maxCrashRestarts"]!;
            if (maximum < 0 || maximum > 3) throw new BridgeException("invalid_recovery", "maxCrashRestarts must be 0..3.");
            if (maximum > 0) Wire.Required(obj, "resetSave");
            return maximum;
        }
        private static async Task<(bool Passed, bool Crashed)> RunAttempt(BridgeClient bridge, GameProcess process, JObject scenario, JArray steps, string output, CancellationToken ct)
        {
            var results = new JObject(); var cases = new List<XElement>(); var failed = false;
            var evidence = new JObject(); results["_evidence"] = evidence;
            var crashed = false;
            foreach (var item in steps)
            {
                var step = (JObject)item; var id = Wire.Required(step, "id"); var timer = Stopwatch.StartNew();
                var ownedBefore = process.IsOwnedAlive;
                try
                {
                    var tool = Wire.Required(step, "tool");
                    var args = (JObject)Resolve(step["arguments"] ?? new JObject(), results);
                    JToken result;
                    if ((string?)step["tool"] == "bridge_call") result = await Program.Dispatch(bridge, process, tool, args, ct);
                    else if (tool == "desktop_input" || tool.StartsWith("process_", StringComparison.Ordinal) || tool.StartsWith("profile_", StringComparison.Ordinal) || tool.StartsWith("bridge_", StringComparison.Ordinal)) result = await Program.Dispatch(bridge, process, tool, args, ct);
                    else result = await bridge.Call(tool, args, (int?)step["timeoutMs"] ?? 10000, ct);
                    results[id] = result;
                    if ((string?)result["state"] is "failed" or "cancelled") throw new BridgeException("tool_failed", Wire.Json(result["error"]!));
                    foreach (var assertion in (JArray?)step["assert"] ?? new JArray()) Assert(result, (JObject)assertion);
                    if ((string?)result.SelectToken("result.data.artifactId") is string artifact) File.WriteAllBytes(Path.Combine(output, id + ".png"), await bridge.Artifact(artifact, ct));
                    cases.Add(new XElement("testcase", new XAttribute("name", id), new XAttribute("time", timer.Elapsed.TotalSeconds)));
                }
                catch (Exception ex)
                {
                    crashed = ownedBefore && !process.IsOwnedAlive && (string?)step["tool"] is not ("process_stop" or "game_exit");
                    failed = true; evidence["failedStep"] = id; evidence["error"] = Wire.Token(new { type = ex.GetType().Name, message = ex.Message });
                    evidence["processExitedDuringStep"] = crashed;
                    cases.Add(new XElement("testcase", new XAttribute("name", id), new XAttribute("time", timer.Elapsed.TotalSeconds), new XElement("failure", ex.Message)));
                    try
                    {
                        var shot = await bridge.Call("capture_screen", new JObject(), 3000, ct);
                        evidence["failureCapture"] = shot;
                        if ((string?)shot.SelectToken("result.data.artifactId") is string artifact) File.WriteAllBytes(Path.Combine(output, "failure.png"), await bridge.Artifact(artifact, ct));
                    }
                    catch (Exception captureError) { evidence["failureCaptureError"] = captureError.Message; }
                    break;
                }
            }
            try
            {
                evidence["environment"] = await bridge.Status(ct);
                var d = bridge.Discover(); var pages = new JArray(); long cursor = 0;
                for (int i = 0; i < 20; i++)
                {
                    var page = await bridge.Request(d, HttpMethod.Get, "v1/events?after=" + cursor + "&limit=500", ct: ct); pages.Add(page);
                    var next = (long)page["nextCursor"]!; if (next == cursor || ((JArray)page["items"]!).Count == 0) break; cursor = next;
                }
                File.WriteAllText(Path.Combine(output, "events.json"), Wire.Json(pages));
            }
            catch (Exception ex) { evidence["eventsError"] = ex.Message; }
            var log = process.LogPath;
            if (File.Exists(log))
            {
                using var source = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var target = File.Create(Path.Combine(output, "Player.log")); await source.CopyToAsync(target, ct);
            }
            File.WriteAllText(Path.Combine(output, "results.json"), Wire.Json(results));
            new XDocument(new XElement("testsuite", new XAttribute("name", (string?)scenario["name"] ?? "scenario"), new XAttribute("tests", cases.Count), new XAttribute("failures", failed ? 1 : 0), cases)).Save(Path.Combine(output, "junit.xml"));
            return (!failed, crashed);
        }
        public static JToken Resolve(JToken value, JObject results)
        {
            if (value is JObject obj && obj.Count == 1 && obj["$ref"] is JValue reference)
                return results.SelectToken((string)reference!, true)!.DeepClone();
            if (value is JObject map) return new JObject(map.Properties().Select(p => new JProperty(p.Name, Resolve(p.Value, results))));
            if (value is JArray array) return new JArray(array.Select(x => Resolve(x, results)));
            return value.DeepClone();
        }
        public static void Assert(JToken result, JObject assertion)
        {
            var path = Wire.Required(assertion, "path"); var value = result.SelectToken(path);
            if (assertion["exists"] != null && ((bool)assertion["exists"]! != (value != null))) throw new BridgeException("assertion_failed", path + " existence mismatch.");
            if (assertion.ContainsKey("equals") && !JToken.DeepEquals(value, assertion["equals"])) throw new BridgeException("assertion_failed", path + " expected " + assertion["equals"] + ", got " + value);
            if (assertion["min"] != null && (value == null || (double)value < (double)assertion["min"]!)) throw new BridgeException("assertion_failed", path + " below minimum.");
            if (assertion["max"] != null && (value == null || (double)value > (double)assertion["max"]!)) throw new BridgeException("assertion_failed", path + " above maximum.");
        }
    }
}
