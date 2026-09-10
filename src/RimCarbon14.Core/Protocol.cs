using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace RimCarbon14.Core
{
    public static class Wire
    {
        public const int Version = 1;
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 32
        };
        public static string Json(object value) => JsonConvert.SerializeObject(value, Settings);
        public static JToken Token(object? value) => value == null ? JValue.CreateNull() : JToken.FromObject(value, JsonSerializer.Create(Settings));
        public static string Required(JObject args, string name) => (string?)args[name] is string s && s.Length > 0 ? s : throw new BridgeException("invalid_arguments", "Missing " + name);
        public static int Limit(JObject args, int maximum = 200) => Math.Max(1, Math.Min(maximum, (int?)args["limit"] ?? 50));
    }

    public sealed class BridgeException : Exception
    {
        public string Code { get; }
        public BridgeException(string code, string message) : base(message) { Code = code; }
    }

    public sealed class Discovery
    {
        public int ProtocolVersion { get; set; } = Wire.Version;
        public string InstanceId { get; set; } = "";
        public int Pid { get; set; }
        public string Endpoint { get; set; } = "";
        public string Token { get; set; } = "";
        public string SaveDataFolder { get; set; } = "";
        public DateTime StartedAtUtc { get; set; }
    }

    public sealed class ToolSpec
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Effect { get; set; } = "read";
        public bool RequiresGame { get; set; }
        public JObject InputSchema { get; set; } = JObject.Parse("{\"type\":\"object\",\"properties\":{}}");
        public void Validate(JObject arguments)
        {
            var properties = (JObject)InputSchema["properties"]!;
            foreach (var required in (JArray?)InputSchema["required"] ?? new JArray())
                if (!arguments.ContainsKey((string)required!)) throw new BridgeException("invalid_arguments", "Missing " + required);
            foreach (var property in arguments.Properties())
            {
                if (!properties.TryGetValue(property.Name, out var schema)) throw new BridgeException("invalid_arguments", "Unknown argument " + property.Name);
                var type = (string?)schema["type"];
                var actual = property.Value.Type;
                var valid = type == null || (type == "string" && actual == JTokenType.String) || (type == "integer" && actual == JTokenType.Integer) || (type == "number" && (actual == JTokenType.Integer || actual == JTokenType.Float)) || (type == "boolean" && actual == JTokenType.Boolean) || (type == "object" && actual == JTokenType.Object) || (type == "array" && actual == JTokenType.Array);
                if (!valid) throw new BridgeException("invalid_arguments", property.Name + " must be " + type);
            }
        }
    }

    public sealed class CallRequest
    {
        public string RequestId { get; set; } = "";
        public DateTime IssuedAtUtc { get; set; }
        public string Tool { get; set; } = "";
        public JObject Arguments { get; set; } = new JObject();
        public int TimeoutMs { get; set; } = 10000;
        public string? Session { get; set; }
    }
}
