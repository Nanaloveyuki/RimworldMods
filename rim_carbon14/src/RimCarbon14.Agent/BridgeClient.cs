using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;

namespace RimCarbon14.Agent
{
    public sealed class BridgeClient : IDisposable
    {
        private readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        public string Profile { get; }
        public BridgeClient(string profile) { Profile = Path.GetFullPath(profile); }
        public Discovery Discover()
        {
            var file = Path.Combine(Profile, "RimCarbon14", "discovery.json");
            if (!File.Exists(file)) throw new BridgeException("bridge_offline", "No discovery file in " + file);
            var d = JsonConvert.DeserializeObject<Discovery>(File.ReadAllText(file), Wire.Settings)!;
            if (d.ProtocolVersion != Wire.Version) throw new BridgeException("protocol_mismatch", "Unsupported bridge protocol.");
            var uri = new Uri(d.Endpoint);
            if (uri.Scheme != "http" || uri.Host != "127.0.0.1") throw new BridgeException("invalid_endpoint", "Discovery endpoint must be loopback HTTP.");
            return d;
        }
        public async Task<JToken> Request(Discovery d, HttpMethod method, string route, object? body = null, CancellationToken ct = default)
        {
            using var message = new HttpRequestMessage(method, new Uri(new Uri(d.Endpoint), route));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", d.Token);
            if (body != null) message.Content = new StringContent(Wire.Json(body), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(message, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new BridgeException((int)response.StatusCode == 400 || (int)response.StatusCode == 403 ? "request_rejected" : "bridge_http_error", $"HTTP {(int)response.StatusCode}: {text}");
            return JToken.Parse(text);
        }
        public async Task<JToken> Status(CancellationToken ct = default)
        {
            var state = await Request(Discover(), HttpMethod.Get, "v1/status", ct: ct);
            var utc = (DateTime?)state["utc"];
            var ageMs = utc.HasValue ? Math.Max(0, (DateTime.UtcNow - utc.Value.ToUniversalTime()).TotalMilliseconds) : double.PositiveInfinity;
            state["heartbeatAgeMs"] = double.IsInfinity(ageMs) ? JValue.CreateNull() : new JValue(ageMs);
            state["mainThreadResponsive"] = ageMs < 3000;
            return state;
        }
        public async Task<List<ToolSpec>> Tools(CancellationToken ct = default) => (await Request(Discover(), HttpMethod.Get, "v1/tools", ct: ct)).ToObject<List<ToolSpec>>()!;
        public async Task<JToken> Call(string tool, JObject args, int timeoutMs = 10000, CancellationToken ct = default)
        {
            var discovery = Discover();
            var status = await Request(discovery, HttpMethod.Get, "v1/status", ct: ct);
            var request = new CallRequest { RequestId = Guid.NewGuid().ToString("N"), IssuedAtUtc = DateTime.UtcNow, Tool = tool, Arguments = args, TimeoutMs = timeoutMs, Session = (string?)status["session"] };
            JToken op;
            var accepted = false;
            try
            {
                op = await Request(discovery, HttpMethod.Post, "v1/calls", request, ct);
                accepted = true;
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs + 5000);
                while ((string?)op["state"] == "queued" || (string?)op["state"] == "running")
                {
                    if (DateTime.UtcNow > deadline) throw new BridgeException("bridge_unresponsive", "Operation exceeded its deadline without a terminal response.");
                    await Task.Delay(50, ct);
                    op = await Request(discovery, HttpMethod.Get, "v1/operations/" + request.RequestId, ct: ct);
                }
            }
            catch (BridgeException ex) when (!accepted && ex.Code == "request_rejected") { throw; }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                {
                    try { await Request(discovery, HttpMethod.Delete, "v1/operations/" + request.RequestId); }
                    catch (Exception cancelError) { Console.Error.WriteLine("Cancellation acknowledgement unavailable: " + cancelError.Message); }
                }
                throw new BridgeException("operation_uncertain", $"{ex.Message} Instance={discovery.InstanceId}, operationId={request.RequestId}. Query this operation; do not replay the mutation.");
            }
            return op;
        }
        public async Task<byte[]> Artifact(string id, CancellationToken ct = default)
        {
            var d = Discover();
            using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(d.Endpoint), "v1/artifacts/" + Uri.EscapeDataString(id)));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", d.Token);
            using var response = await http.SendAsync(message, ct); response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        public void Dispose() => http.Dispose();
    }
}
