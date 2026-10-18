using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace RimCarbon14.Core
{
    public sealed class ObjectHandles
    {
        private readonly Dictionary<string, object> objects = new Dictionary<string, object>();
        private readonly int capacity;
        public string Session { get; private set; } = Guid.NewGuid().ToString("N");
        public ObjectHandles(int capacity = 4096) { this.capacity = capacity; }
        public void Reset() { objects.Clear(); Session = Guid.NewGuid().ToString("N"); }
        public string Add(object value)
        {
            foreach (var pair in objects) if (ReferenceEquals(pair.Value, value)) return pair.Key;
            if (objects.Count >= capacity) throw new BridgeException("handle_limit", "Release unused handles before inspecting more objects.");
            var id = Session + ":" + Guid.NewGuid().ToString("N");
            objects.Add(id, value);
            return id;
        }
        public object Get(string id)
        {
            if (!id.StartsWith(Session + ":", StringComparison.Ordinal)) throw new BridgeException("stale_handle", "Handle belongs to a previous game session.");
            return objects.TryGetValue(id, out var value) ? value : throw new BridgeException("handle_not_found", "Handle was released or is unknown.");
        }
        public int Release(IEnumerable<string> ids) => ids.Count(id => objects.Remove(id));
        public JToken Describe(object? value)
        {
            if (value == null) return JValue.CreateNull();
            var type = value.GetType();
            if (type.IsPrimitive || value is string || value is decimal || value is DateTime || value is Guid) return Wire.Token(value);
            if (type.IsEnum) return new JObject { ["type"] = type.FullName, ["value"] = value.ToString() };
            return new JObject { ["handle"] = Add(value), ["type"] = type.FullName, ["snapshot"] = false };
        }
    }
}
