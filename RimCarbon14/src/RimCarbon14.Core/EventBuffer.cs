using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace RimCarbon14.Core
{
    public sealed class EventBuffer
    {
        private readonly object gate = new object();
        private readonly Queue<JObject> entries = new Queue<JObject>();
        private readonly int capacity;
        private long sequence;
        public EventBuffer(int capacity = 4096) { this.capacity = Math.Max(1, capacity); }
        public void Add(string kind, object payload)
        {
            lock (gate)
            {
                entries.Enqueue(new JObject { ["sequence"] = ++sequence, ["utc"] = DateTime.UtcNow, ["kind"] = kind, ["data"] = Wire.Token(payload) });
                while (entries.Count > capacity) entries.Dequeue();
            }
        }
        public JObject Read(long after, int limit)
        {
            lock (gate)
            {
                var first = entries.Count == 0 ? sequence + 1 : (long)entries.Peek()["sequence"]!;
                var items = entries.Where(e => (long)e["sequence"]! > after).Take(Math.Max(1, Math.Min(500, limit))).Select(e => e.DeepClone()).ToArray();
                return new JObject { ["items"] = new JArray(items), ["nextCursor"] = items.Length == 0 ? Math.Max(after, sequence) : (long)items.Last()["sequence"]!, ["lost"] = Math.Max(0, first - after - 1) };
            }
        }
    }
}
