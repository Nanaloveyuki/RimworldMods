using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Oxygendsl.Mod.Editor
{
    public sealed class OxygenEditorServer : IDisposable
    {
        const int MaxBody = 8 * 1024 * 1024;
        const int MaxText = 1024 * 1024;
        readonly Func<IReadOnlyList<OxygenEditorDocument>> documents;
        readonly Func<string, string, string, OxygenEditorDocument> create;
        readonly Dictionary<string, string> assets = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly ConcurrentQueue<Work> queue = new ConcurrentQueue<Work>();
        readonly object gate = new object();
        readonly HashSet<TcpClient> clients = new HashSet<TcpClient>();
        TcpListener listener;
        Thread acceptThread;
        volatile bool running;
        bool started;
        int pumpThread;
        string origin, host, token;
        public bool Running { get { return running; } }
        public string Url { get; private set; }

        public OxygenEditorServer(string assetRoot, Func<IReadOnlyList<OxygenEditorDocument>> documents,
            Func<string, string, string, OxygenEditorDocument> createDocument = null)
        {
            this.documents = documents ?? throw new ArgumentNullException(nameof(documents));
            create = createDocument;
            string root = Path.GetFullPath(assetRoot);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Redirected editor assets are not allowed.");
            foreach (string name in new[] { "index.html", "app.js", "app.css", "blocks.js", "blocks.css" })
            {
                string file = Path.Combine(root, name);
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) assets.Add("/" + name, file);
            }
            string media = Path.Combine(root, "blockly");
            if (Directory.Exists(media) && (File.GetAttributes(media) & FileAttributes.ReparsePoint) == 0)
                foreach (string file in Directory.GetFiles(media))
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                        assets.Add("/blockly/" + Path.GetFileName(file), file);
            if (!assets.ContainsKey("/index.html")) throw new FileNotFoundException("The bundled editor index is missing.");
            assets.Add("/", assets["/index.html"]);
        }

        public void Start()
        {
            lock (gate)
            {
                if (started) throw new InvalidOperationException("An editor server can only be started once.");
                started = true;
                pumpThread = Thread.CurrentThread.ManagedThreadId;
                var bytes = new byte[32];
                using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
                token = Hex(bytes);
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start(8);
                host = "127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
                origin = "http://" + host;
                Url = origin + "/#token=" + token;
                running = true;
                acceptThread = new Thread(Accept) { IsBackground = true, Name = "Oxygen local editor" };
                acceptThread.Start();
            }
        }

        public void Pump()
        {
            if (Thread.CurrentThread.ManagedThreadId != pumpThread) throw new InvalidOperationException("Editor callbacks must run on the starting game thread.");
            for (int count = 0; count < 8 && running && queue.TryDequeue(out Work work); count++)
            {
                if (Interlocked.CompareExchange(ref work.State, 1, 0) != 0) continue;
                try { work.Result.TrySetResult(Route(work.Request)); }
                catch (Exception exception) { work.Result.TrySetException(exception); }
                finally { Interlocked.Exchange(ref work.State, 2); }
            }
        }

        void Accept()
        {
            while (running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch (SocketException) { if (!running) return; continue; }
                catch (ObjectDisposedException) { return; }
                lock (gate)
                {
                    if (!running || clients.Count >= 8) { client.Close(); continue; }
                    clients.Add(client);
                }
                ThreadPool.QueueUserWorkItem(_ => Serve(client));
            }
        }

        void Serve(TcpClient client)
        {
            try
            {
                client.ReceiveTimeout = 5000; client.SendTimeout = 5000;
                using (NetworkStream stream = client.GetStream())
                {
                    try
                    {
                        Request request = ReadRequest(stream);
                        if (!request.Path.StartsWith("/api/", StringComparison.Ordinal))
                        {
                            if (request.Method != "GET") throw new HttpError(405, "GET is required.");
                            if (!assets.TryGetValue(request.Path, out string file)) throw new HttpError(404, "Asset not found.");
                            Send(stream, 200, Mime(file), File.ReadAllBytes(file));
                            return;
                        }
                        string provided;
                        if (!request.Headers.TryGetValue("X-Oxygen-Token", out provided) || !TokenMatches(provided)) throw new HttpError(403, "Invalid editor session token.");
                        var work = new Work(request);
                        lock (gate)
                        {
                            if (!running) throw new HttpError(503, "Editor stopped.");
                            if (queue.Count >= 16) throw new HttpError(503, "The editor request queue is full.");
                            queue.Enqueue(work);
                        }
                        if (!work.Result.Task.Wait(15000))
                        {
                            bool cancelled = Interlocked.CompareExchange(ref work.State, 3, 0) == 0;
                            throw new HttpError(503, cancelled ? "The game thread is busy. The pending request was not started."
                                : "The game-thread request is still running. Reload the document to inspect its result.");
                        }
                        object result = work.Result.Task.GetAwaiter().GetResult();
                        SendJson(stream, 200, result);
                    }
                    catch (Exception exception)
                    {
                        if (exception is AggregateException aggregate) exception = aggregate.GetBaseException();
                        HttpError error = exception as HttpError;
                        int code = error != null ? error.Code : exception is OxygenEditorJsonException || exception is ArgumentException ? 400 : 500;
                        SendJson(stream, code, error?.Payload ?? new Dictionary<string, object> { ["error"] = exception.Message, ["message"] = exception.Message });
                    }
                }
            }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            finally { lock (gate) clients.Remove(client); client.Close(); }
        }

        Request ReadRequest(NetworkStream stream)
        {
            var header = new byte[16384];
            int length = 0;
            while (true)
            {
                int next = stream.ReadByte();
                if (next < 0) throw new HttpError(400, "Incomplete request headers.");
                if (length == header.Length) throw new HttpError(431, "Request headers are too large.");
                header[length++] = (byte)next;
                if (length >= 4 && header[length - 4] == 13 && header[length - 3] == 10 && header[length - 2] == 13 && header[length - 1] == 10) break;
            }
            string[] rows = Encoding.ASCII.GetString(header, 0, length - 4).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = rows[0].Split(' ');
            if (first.Length != 3 || first[2] != "HTTP/1.1" || !first[1].StartsWith("/", StringComparison.Ordinal)) throw new HttpError(400, "Invalid request line.");
            var request = new Request { Method = first[0], Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) };
            for (int index = 1; index < rows.Length; index++)
            {
                int colon = rows[index].IndexOf(':');
                if (colon <= 0) throw new HttpError(400, "Invalid request header.");
                string name = rows[index].Substring(0, colon), value = rows[index].Substring(colon + 1).Trim();
                if (request.Headers.ContainsKey(name)) throw new HttpError(400, "Duplicate request header.");
                request.Headers.Add(name, value);
            }
            if (!request.Headers.TryGetValue("Host", out string authority) || authority != host) throw new HttpError(403, "Foreign Host rejected.");
            if (request.Headers.TryGetValue("Origin", out string source) && source != origin) throw new HttpError(403, "Foreign Origin rejected.");
            if (request.Headers.ContainsKey("Transfer-Encoding")) throw new HttpError(400, "Chunked requests are not supported.");
            int query = first[1].IndexOf('?');
            request.Path = Uri.UnescapeDataString(query < 0 ? first[1] : first[1].Substring(0, query));
            request.Query = query < 0 ? string.Empty : first[1].Substring(query + 1);
            if (request.Path.IndexOfAny(new[] { '\\', '\0', '#' }) >= 0 || request.Path.Contains("..")) throw new HttpError(400, "Unsafe request path.");
            int bodyLength = 0;
            if (request.Headers.TryGetValue("Content-Length", out string bodySize)
                && (!int.TryParse(bodySize, out bodyLength) || bodyLength < 0 || bodyLength > MaxBody)) throw new HttpError(413, "Request body is too large.");
            if (request.Method == "POST" && (!request.Headers.TryGetValue("Content-Type", out string contentType) || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))) throw new HttpError(415, "JSON content is required.");
            var body = new byte[bodyLength];
            int read = 0;
            while (read < body.Length) { int count = stream.Read(body, read, body.Length - read); if (count == 0) throw new HttpError(400, "Incomplete request body."); read += count; }
            request.Body = new UTF8Encoding(false, true).GetString(body);
            return request;
        }

        object Route(Request request)
        {
            if (request.Method == "GET")
            {
                if (request.Path == "/api/documents")
                {
                    var result = new List<Dictionary<string, object>>();
                    foreach (OxygenEditorDocument document in documents()) result.Add(Describe(document));
                    return result;
                }
                if (request.Path == "/api/document") return ReadDocument(Find(Query(request.Query, "id")));
                throw new HttpError(404, "Unknown API endpoint.");
            }
            if (request.Method != "POST") throw new HttpError(405, "GET or POST is required.");
            Dictionary<string, object> body = OxygenEditorJson.Object(request.Body);
            if (request.Path == "/api/analyze")
            {
                string source = Text(body, "source");
                return new Dictionary<string, object> { ["diagnostics"] = OxygenEditorAnalysis.Analyze(source, OxygenEditorJson.Bool(body, "isModule"), OxygenEditorJson.String(body, "language")) };
            }
            if (request.Path == "/api/create")
            {
                if (create == null) throw new HttpError(403, "This host does not support new documents.");
                return ReadDocument(create(OxygenEditorJson.String(body, "packageId"), OxygenEditorJson.String(body, "path"), Text(body, "text")));
            }
            OxygenEditorDocument target = Find(OxygenEditorJson.String(body, "id"));
            if (request.Path == "/api/preview")
            {
                if (target.Preview == null) throw new HttpError(403, "This document has no preview provider.");
                return target.Preview(Text(body, "source"), Text(body, "facts"));
            }
            if (request.Path != "/api/save" && request.Path != "/api/apply") throw new HttpError(404, "Unknown API endpoint.");
            string current = target.Read() ?? string.Empty;
            string hash = Digest(current);
            if (OxygenEditorJson.String(body, "expectedHash") != hash)
                throw new HttpError(409, "The document changed. Local edits were not saved or applied.", new Dictionary<string, object>
                    { ["error"] = "The document changed. Local edits were not saved or applied.", ["hash"] = hash, ["text"] = current });
            if (request.Path == "/api/save")
            {
                if (!target.Writable || target.Write == null) throw new HttpError(403, "This document is read only.");
                target.Write(Text(body, "text"));
                return ReadDocument(target);
            }
            if (target.Apply == null || target.Kind == "blocks") throw new HttpError(403, "This document cannot be applied. Export blocks to source first.");
            OxygenEditorActionResult applied = target.Apply();
            return new Dictionary<string, object> { ["success"] = applied != null && applied.Success, ["message"] = applied?.Message ?? "No apply result was returned." };
        }

        OxygenEditorDocument Find(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new HttpError(400, "A document ID is required.");
            foreach (OxygenEditorDocument document in documents()) if (document.Id == id) return document;
            throw new HttpError(404, "This document is no longer available in the active catalog.");
        }
        static Dictionary<string, object> Describe(OxygenEditorDocument document)
        {
            return new Dictionary<string, object> { ["id"] = document.Id, ["packageId"] = document.PackageId, ["title"] = document.Title,
                ["path"] = document.Path, ["kind"] = document.Kind, ["writable"] = document.Writable && document.Write != null,
                ["isModule"] = document.IsModule, ["canApply"] = document.Apply != null && document.Kind != "blocks", ["canPreview"] = document.Preview != null };
        }
        static Dictionary<string, object> ReadDocument(OxygenEditorDocument document)
        {
            string text = document.Read() ?? string.Empty;
            var result = Describe(document); result["text"] = text; result["hash"] = Digest(text); return result;
        }
        static string Text(Dictionary<string, object> body, string key)
        {
            string text = OxygenEditorJson.String(body, key);
            if (text == null || text.Length > MaxText) throw new HttpError(413, key + " must be at most 1 MiB of UTF-16 text.");
            return text;
        }
        static string Query(string query, string key)
        {
            foreach (string part in query.Split('&')) { int equal = part.IndexOf('='); if (equal >= 0 && part.Substring(0, equal) == key) return Uri.UnescapeDataString(part.Substring(equal + 1)); }
            return null;
        }
        static string Digest(string text)
        {
            var bytes = new byte[text.Length * 2];
            for (int index = 0; index < text.Length; index++) { bytes[index * 2] = (byte)text[index]; bytes[index * 2 + 1] = (byte)(text[index] >> 8); }
            using (SHA256 hash = SHA256.Create()) return Hex(hash.ComputeHash(bytes));
        }
        static string Hex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2]; const string digits = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++) { chars[index * 2] = digits[bytes[index] >> 4]; chars[index * 2 + 1] = digits[bytes[index] & 15]; }
            return new string(chars);
        }
        bool TokenMatches(string candidate)
        {
            if (candidate == null || candidate.Length != token.Length) return false;
            int difference = 0; for (int index = 0; index < token.Length; index++) difference |= token[index] ^ candidate[index]; return difference == 0;
        }
        static string Mime(string file)
        {
            switch (Path.GetExtension(file)) { case ".html": return "text/html; charset=utf-8"; case ".js": return "text/javascript; charset=utf-8"; case ".css": return "text/css; charset=utf-8"; case ".svg": return "image/svg+xml"; case ".png": return "image/png"; case ".mp3": return "audio/mpeg"; case ".wav": return "audio/wav"; default: return "application/octet-stream"; }
        }
        static void SendJson(NetworkStream stream, int status, object payload) { Send(stream, status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(OxygenEditorJson.Serialize(payload))); }
        static void Send(NetworkStream stream, int status, string mime, byte[] body)
        {
            string policy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; object-src 'none'";
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " " + (status == 200 ? "OK" : "Error") + "\r\nContent-Type: " + mime + "\r\nContent-Length: " + body.Length
                + "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: " + policy + "\r\n\r\n");
            stream.Write(header, 0, header.Length); stream.Write(body, 0, body.Length);
        }

        public void Dispose()
        {
            lock (gate)
            {
                running = false;
                listener?.Stop();
                foreach (TcpClient client in clients) client.Close();
                while (queue.TryDequeue(out Work work)) { Interlocked.CompareExchange(ref work.State, 3, 0); work.Result.TrySetException(new HttpError(503, "Editor stopped.")); }
            }
            if (acceptThread != null && Thread.CurrentThread != acceptThread) acceptThread.Join(1000);
        }
        sealed class Request { public string Method, Path, Query, Body; public Dictionary<string, string> Headers; }
        sealed class Work
        {
            public readonly Request Request;
            public readonly TaskCompletionSource<object> Result = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            public int State;
            public Work(Request request) { Request = request; }
        }
        sealed class HttpError : Exception
        {
            public readonly int Code;
            public readonly object Payload;
            public HttpError(int code, string message, object payload = null) : base(message) { Code = code; Payload = payload; }
        }
    }
}
