using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace RimCarbon14.Core
{
    // One main-thread window. Timestamps are supplied by the observer for deterministic tests.
    public sealed class WatchWindow
    {
        public sealed class Node
        {
            public int Id; public int? ParentId; public string MethodId = ""; public int SiteId; public int Depth;
            public long Calls; public long Exceptions; public double TotalMs; public double SelfMs;
            internal readonly List<double> Samples = new List<double>();
        }
        public sealed class Sample
        {
            public int NodeId; public long Calls; public long Exceptions; public double InclusiveMs; public double SelfMs;
        }
        public sealed class Tick
        {
            public int Index; public int GameTick; public double TotalMs; public bool Exception;
            public Sample[] Nodes = Array.Empty<Sample>();
        }
        private struct Frame { public int Token; public int Node; public double Start; public double Children; }
        private readonly List<Frame> stack = new List<Frame>();
        private readonly Dictionary<(int, int, string), int> paths = new Dictionary<(int, int, string), int>();
        private readonly Dictionary<int, Sample> current = new Dictionary<int, Sample>();
        private readonly int maxNodes;
        private readonly int maxSamples;
        private readonly int maxDepth;
        private int token;
        private int samples;
        private double tickStart;
        private int gameTick;
        public readonly List<Node> Nodes = new List<Node>();
        public readonly List<Tick> Ticks = new List<Tick>();
        public bool InTick { get; private set; }
        public int StackDepth => stack.Count;
        public string? CurrentMethod => stack.Count == 0 ? null : Nodes[stack[stack.Count - 1].Node].MethodId;
        public string? Limit { get; private set; }
        public long DepthLimitedCalls { get; private set; }
        public double TotalTickMs { get; private set; }
        public WatchWindow(int maxDepth, int maxNodes = 2048, int maxSamples = 100000)
        { this.maxDepth = maxDepth; this.maxNodes = maxNodes; this.maxSamples = maxSamples; }
        public void BeginTick(int tick, double now)
        {
            if (InTick) throw new InvalidOperationException("A tick is already open.");
            InTick = true; gameTick = tick; tickStart = now; current.Clear(); stack.Clear();
        }
        public int Enter(string methodId, int siteId, double now)
        {
            if (!InTick || Limit != null) return 0;
            if (stack.Count > maxDepth) { DepthLimitedCalls++; return 0; }
            int parent = stack.Count == 0 ? -1 : stack[stack.Count - 1].Node;
            var key = (parent, siteId, methodId);
            if (!paths.TryGetValue(key, out int id))
            {
                if (Nodes.Count >= maxNodes) { Limit = "node_limit"; return 0; }
                id = Nodes.Count; paths.Add(key, id);
                Nodes.Add(new Node { Id = id, ParentId = parent < 0 ? (int?)null : parent, MethodId = methodId, SiteId = siteId, Depth = stack.Count });
            }
            stack.Add(new Frame { Token = ++token, Node = id, Start = now });
            return token;
        }
        public void Exit(int call, double now, bool exception)
        {
            if (call == 0 || !InTick) return;
            if (stack.Count == 0 || stack[stack.Count - 1].Token != call) { Limit = "unbalanced_calls"; return; }
            int top = stack.Count - 1; var frame = stack[top]; stack.RemoveAt(top);
            double elapsed = Math.Max(0, now - frame.Start);
            if (stack.Count > 0) { var p = stack[top - 1]; p.Children += elapsed; stack[top - 1] = p; }
            if (!current.TryGetValue(frame.Node, out var sample)) current.Add(frame.Node, sample = new Sample { NodeId = frame.Node });
            sample.Calls++; if (exception) sample.Exceptions++;
            sample.InclusiveMs += elapsed; sample.SelfMs += Math.Max(0, elapsed - frame.Children);
        }
        public Tick? EndTick(double now, bool exception)
        {
            if (!InTick) return null;
            InTick = false;
            if (stack.Count != 0) Limit = "unbalanced_calls";
            stack.Clear();
            if (samples + current.Count > maxSamples) Limit = "sample_limit";
            if (Limit != null) { current.Clear(); return null; }
            var tick = new Tick { Index = Ticks.Count, GameTick = gameTick, TotalMs = Math.Max(0, now - tickStart), Exception = exception, Nodes = current.Values.OrderBy(s => s.NodeId).ToArray() };
            foreach (var sample in tick.Nodes)
            {
                var node = Nodes[sample.NodeId]; node.Calls += sample.Calls; node.Exceptions += sample.Exceptions;
                node.TotalMs += sample.InclusiveMs; node.SelfMs += sample.SelfMs; node.Samples.Add(sample.InclusiveMs);
            }
            samples += tick.Nodes.Length; TotalTickMs += tick.TotalMs; Ticks.Add(tick); current.Clear();
            return tick;
        }
        public void AbortTick() { InTick = false; stack.Clear(); current.Clear(); }
        public JObject Report()
        {
            int count = Ticks.Count;
            return new JObject
            {
                ["completedTicks"] = count, ["totalTickMs"] = TotalTickMs, ["limit"] = Limit,
                ["depthLimitedCalls"] = DepthLimitedCalls, ["sampleCount"] = samples,
                ["nodes"] = new JArray(Nodes.Select(n => new JObject
                {
                    ["id"] = n.Id, ["parentId"] = n.ParentId, ["methodId"] = n.MethodId, ["siteId"] = n.SiteId, ["depth"] = n.Depth,
                    ["calls"] = n.Calls, ["exceptions"] = n.Exceptions, ["inclusiveMs"] = n.TotalMs, ["selfMs"] = n.SelfMs,
                    ["inclusiveMsPerTick"] = count == 0 ? (double?)null : n.TotalMs / count,
                    ["selfMsPerTick"] = count == 0 ? (double?)null : n.SelfMs / count,
                    ["callsPerTick"] = count == 0 ? (double?)null : (double)n.Calls / count,
                    ["tickShare"] = Ratio(n.TotalMs, TotalTickMs), ["selfTickShare"] = Ratio(n.SelfMs, TotalTickMs),
                    ["parentShare"] = n.ParentId == null ? null : Ratio(n.TotalMs, Nodes[n.ParentId.Value].TotalMs),
                    ["p95MsPerTick"] = count == 0 ? (double?)null : Percentile(n.Samples, count, 0.95),
                    ["maxMsPerTick"] = count == 0 ? (double?)null : n.Samples.DefaultIfEmpty(0).Max()
                }))
            };
        }
        public JObject Page(int offset, int limit) => new JObject
        {
            ["total"] = Ticks.Count, ["nextOffset"] = Math.Min(Ticks.Count, offset + limit),
            ["items"] = Wire.Token(Ticks.Skip(offset).Take(limit))
        };
        private static double? Ratio(double a, double b) => b > 0 ? a / b : (double?)null;
        private static double Percentile(List<double> nonzeroTicks, int ticks, double p)
        {
            int index = (int)Math.Ceiling(ticks * p) - 1 - (ticks - nonzeroTicks.Count);
            return index < 0 ? 0 : nonzeroTicks.OrderBy(x => x).ElementAt(index);
        }
    }
}
