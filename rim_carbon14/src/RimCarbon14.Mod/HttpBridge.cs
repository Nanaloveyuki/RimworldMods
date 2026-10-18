using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;

namespace RimCarbon14.Mod
{
    public sealed class HttpBridge : IDisposable
    {
        private readonly BridgeHost host;
        private readonly HttpListener listener = new HttpListener();
        private readonly SemaphoreSlim slots = new SemaphoreSlim(16);
        private readonly string token;
        private bool stopped;
        private string path = "";
        public HttpBridge(BridgeHost host)
        {
            this.host = host;
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            token = Convert.ToBase64String(bytes);
        }
        public void Start()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            var endpoint = "http://127.0.0.1:" + port + "/";
            listener.Prefixes.Add(endpoint);
            listener.Start();
            path = Path.Combine(host.DataDirectory, "discovery.json");
            var discovery = new Discovery { InstanceId = host.InstanceId, Pid = System.Diagnostics.Process.GetCurrentProcess().Id, Endpoint = endpoint, Token = token, SaveDataFolder = Verse.GenFilePaths.SaveDataFolderPath, StartedAtUtc = DateTime.UtcNow };
            File.WriteAllText(path + ".tmp", Wire.Json(discovery));
            if (File.Exists(path)) File.Delete(path);
            File.Move(path + ".tmp", path);
            _ = Listen();
        }
        private async Task Listen()
        {
            while (!stopped)
            {
                try
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    if (!slots.Wait(0)) { context.Response.StatusCode = 503; context.Response.Close(); continue; }
                    _ = Process(context);
                }
                catch (Exception ex) { if (!stopped) host.Events.Add("http.error", new { ex.Message }); }
            }
        }
        private async Task Process(HttpListenerContext context)
        {
            try
            {
                if (!IPAddress.IsLoopback(context.Request.RemoteEndPoint.Address) || context.Request.Headers["Origin"] != null || context.Request.Headers["Authorization"] != "Bearer " + token)
                { await Reply(context, 403, "{\"error\":\"unauthorized\"}"); return; }
                var route = context.Request.Url!.AbsolutePath;
                var method = context.Request.HttpMethod;
                if (method == "GET" && route == "/v1/status") { await Reply(context, 200, host.StatusJson); return; }
                if (method == "GET" && route == "/v1/tools") { await Reply(context, 200, Wire.Json(host.Specs.Values)); return; }
                if (method == "GET" && route == "/v1/events")
                {
                    long.TryParse(context.Request.QueryString["after"], out var after);
                    int.TryParse(context.Request.QueryString["limit"], out var limit);
                    await Reply(context, 200, Wire.Json(host.Events.Read(after, limit > 0 ? limit : 100))); return;
                }
                if (route.StartsWith("/v1/operations/", StringComparison.Ordinal))
                {
                    var id = Uri.UnescapeDataString(route.Substring("/v1/operations/".Length));
                    if (method == "GET") { await Reply(context, 200, Wire.Json(host.Calls.Get(id))); return; }
                    if (method == "DELETE") { await Reply(context, 200, Wire.Json(host.Calls.Cancel(id))); return; }
                }
                if (method == "GET" && route.StartsWith("/v1/artifacts/", StringComparison.Ordinal))
                {
                    var name = route.Substring("/v1/artifacts/".Length);
                    var extension = Path.GetExtension(name);
                    if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _) || (extension != ".png" && extension != ".jsonl") || name != Path.GetFileName(name)) throw new BridgeException("invalid_artifact", "Invalid artifact ID.");
                    var file = Path.Combine(host.ArtifactDirectory, name);
                    if (!File.Exists(file)) throw new BridgeException("artifact_not_found", name);
                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        context.Response.ContentType = extension == ".png" ? "image/png" : "application/x-ndjson; charset=utf-8";
                        long remaining = stream.Length; context.Response.ContentLength64 = remaining;
                        var buffer = new byte[65536];
                        while (remaining > 0)
                        {
                            int read = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                            if (read == 0) break;
                            await context.Response.OutputStream.WriteAsync(buffer, 0, read); remaining -= read;
                        }
                    }
                    return;
                }
                if (method == "POST" && route == "/v1/calls")
                {
                    if (context.Request.ContentLength64 < 0 || context.Request.ContentLength64 > 1024 * 1024) throw new BridgeException("invalid_body_size", "A Content-Length of at most 1 MiB is required.");
                    string body;
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) body = await reader.ReadToEndAsync();
                    var call = JsonConvert.DeserializeObject<CallRequest>(body, Wire.Settings) ?? throw new BridgeException("invalid_body", "Expected a call object.");
                    await Reply(context, 202, Wire.Json(host.Submit(call))); return;
                }
                await Reply(context, 404, "{\"error\":\"route_not_found\"}");
            }
            catch (Exception ex)
            {
                try { await Reply(context, ex is BridgeException ? 400 : 500, Wire.Json(new { error = new { code = (ex as BridgeException)?.Code ?? "http_error", message = ex.Message } })); }
                catch (Exception sendError) { host.Events.Add("http.response_error", new { sendError.Message }); }
            }
            finally { context.Response.Close(); slots.Release(); }
        }
        private static async Task Reply(HttpListenerContext context, int code, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = code; context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }
        public void Dispose()
        {
            if (stopped) return;
            stopped = true;
            listener.Close();
            if (File.Exists(path))
            {
                try { if ((string?)JObject.Parse(File.ReadAllText(path))["instanceId"] == host.InstanceId) File.Delete(path); }
                catch (IOException ex) { host.Events.Add("discovery.cleanup_error", new { ex.Message }); }
            }
        }
    }
}
