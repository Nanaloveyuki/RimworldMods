using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace RimCarbon14.Core
{
    public sealed class ReflectionTools
    {
        private readonly ObjectHandles handles;
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        public ReflectionTools(ObjectHandles handles) { this.handles = handles; }
        public static IEnumerable<Type> Types(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
        }
        public static Type TypeByName(string name)
        {
            var type = Type.GetType(name, false);
            if (type != null) return type;
            var matches = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).Where(t => t != null).Distinct().ToArray();
            if (matches.Length != 1) throw new BridgeException("type_not_unique", "Use an assembly-qualified type name: " + name);
            return matches[0]!;
        }
        public static string MethodId(MethodBase method) => method.Module.ModuleVersionId.ToString("N") + ":" + method.MetadataToken;
        public static MethodInfo Method(string id)
        {
            var parts = id.Split(':');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var moduleId) || !int.TryParse(parts[1], out var token)) throw new BridgeException("invalid_method_id", "Use an ID returned by objects_members.");
            foreach (var module in AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetModules()))
                if (module.ModuleVersionId == moduleId && module.ResolveMethod(token) is MethodInfo method) return method;
            throw new BridgeException("method_not_found", "Method no longer available.");
        }
        private static IEnumerable<Type> Hierarchy(Type type) { for (Type? t = type; t != null; t = t.BaseType) yield return t; }
        private (Type type, object? instance) Target(JObject args)
        {
            if (args["handle"] != null) { var obj = handles.Get(Wire.Required(args, "handle")); return (obj.GetType(), obj); }
            return (TypeByName(Wire.Required(args, "type")), null);
        }
        public JToken SearchTypes(JObject args)
        {
            var search = (string?)args["search"] ?? "";
            var assembly = (string?)args["assembly"];
            return Page(AppDomain.CurrentDomain.GetAssemblies().Where(a => assembly == null || a.GetName().Name == assembly).SelectMany(Types)
                .Where(t => (t.FullName ?? t.Name).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(t => Wire.Token(new { name = t.FullName, qualifiedName = t.AssemblyQualifiedName, assembly = t.Assembly.GetName().Name })), args);
        }
        public JToken Members(JObject args)
        {
            var target = Target(args);
            var members = Hierarchy(target.type).SelectMany(t => t.GetMembers(Flags)).Where(m => m is FieldInfo || m is PropertyInfo || m is MethodInfo);
            if (args["search"] is JValue text) members = members.Where(m => m.Name.IndexOf((string)text!, StringComparison.OrdinalIgnoreCase) >= 0);
            return Page(members.Select(m =>
            {
                var item = new JObject { ["name"] = m.Name, ["declaringType"] = m.DeclaringType!.AssemblyQualifiedName, ["kind"] = m.MemberType.ToString() };
                if (m is FieldInfo f) { item["type"] = f.FieldType.FullName; item["static"] = f.IsStatic; item["writable"] = !f.IsInitOnly && !f.IsLiteral; }
                if (m is PropertyInfo p) { item["type"] = p.PropertyType.FullName; item["readable"] = p.CanRead; item["writable"] = p.CanWrite; item["evaluated"] = false; }
                if (m is MethodInfo method)
                {
                    item["methodId"] = MethodId(method); item["signature"] = method.ToString(); item["static"] = method.IsStatic;
                    item["returnType"] = method.ReturnType.FullName;
                    item["parameters"] = Wire.Token(method.GetParameters().Select(p => new { p.Name, type = p.ParameterType.FullName, p.IsOut, p.IsOptional }));
                    item["supported"] = Supported(method);
                }
                return (JToken)item;
            }), args);
        }
        private MemberInfo Member(JObject args, Type type)
        {
            var declaring = (string?)args["declaringType"];
            var matches = Hierarchy(type).Where(t => declaring == null || t.AssemblyQualifiedName == declaring || t.FullName == declaring)
                .SelectMany(t => t.GetMember(Wire.Required(args, "member"), Flags)).Where(m => m is FieldInfo || m is PropertyInfo).ToArray();
            if (matches.Length != 1) throw new BridgeException("member_not_unique", "Specify member and declaringType from objects_members.");
            return matches[0];
        }
        public JToken Read(JObject args, bool evaluateProperty)
        {
            var target = Target(args);
            var member = Member(args, target.type);
            if (member is FieldInfo field) return handles.Describe(field.GetValue(target.instance));
            if (!evaluateProperty) throw new BridgeException("explicit_evaluation_required", "Use objects_evaluate to run a property getter.");
            return handles.Describe(((PropertyInfo)member).GetValue(target.instance, null));
        }
        public JToken Write(JObject args)
        {
            var target = Target(args);
            var member = Member(args, target.type);
            if (member is FieldInfo field)
            {
                if (field.IsLiteral || field.IsInitOnly) throw new BridgeException("readonly_field", "Cannot write a constant or readonly field.");
                field.SetValue(target.instance, ConvertValue(args["value"], field.FieldType));
            }
            else { var p = (PropertyInfo)member; p.SetValue(target.instance, ConvertValue(args["value"], p.PropertyType), null); }
            return new JObject { ["applied"] = true, ["boxedValueType"] = target.instance?.GetType().IsValueType ?? false };
        }
        public object? ConvertValue(JToken? value, Type type)
        {
            if (type.IsByRef) type = type.GetElementType()!;
            if (value == null || value.Type == JTokenType.Null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) throw new BridgeException("null_value", "Non-nullable value required for " + type.FullName);
                return null;
            }
            if (value is JObject obj && obj["handle"] != null)
            {
                var target = handles.Get((string)obj["handle"]!);
                if (!type.IsInstanceOfType(target)) throw new BridgeException("type_mismatch", "Handle is not assignable to " + type.FullName);
                return target;
            }
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) return ConvertValue(value, underlying);
            if (type.IsEnum) return Enum.Parse(type, (string)value!, false);
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid)) return value.ToObject(type);
            if (type.IsArray && type.GetArrayRank() == 1 && value is JArray array)
            {
                var result = Array.CreateInstance(type.GetElementType()!, array.Count);
                for (var i = 0; i < array.Count; i++) result.SetValue(ConvertValue(array[i], type.GetElementType()!), i);
                return result;
            }
            if (type.IsValueType && value is JObject fields)
            {
                var boxed = Activator.CreateInstance(type)!;
                foreach (var entry in fields.Properties())
                {
                    var field = type.GetField(entry.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null || field.IsInitOnly) throw new BridgeException("invalid_struct_field", entry.Name);
                    field.SetValue(boxed, ConvertValue(entry.Value, field.FieldType));
                }
                return boxed;
            }
            throw new BridgeException("unsupported_value", "Pass an existing object handle for " + type.FullName);
        }
        public static bool Supported(MethodInfo method) => !method.ContainsGenericParameters && !method.ReturnType.IsByRef && !method.ReturnType.IsPointer && method.GetParameters().All(p => !(p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType).IsPointer);
        public JToken Invoke(JObject args)
        {
            var method = Method(Wire.Required(args, "methodId"));
            if (!Supported(method)) throw new BridgeException("unsupported_signature", "Open generic, pointer and byref-return methods are not supported.");
            var instance = method.IsStatic ? null : handles.Get(Wire.Required(args, "handle"));
            var supplied = args["arguments"] as JArray ?? new JArray();
            var parameters = method.GetParameters();
            if (supplied.Count != parameters.Length) throw new BridgeException("argument_count", "Provide one entry per parameter, including null for out parameters.");
            var values = parameters.Select((p, i) => p.IsOut ? null : ConvertValue(supplied[i], p.ParameterType)).ToArray();
            object? result;
            try { result = method.Invoke(instance, values); }
            catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            return new JObject { ["result"] = handles.Describe(result), ["byRef"] = new JArray(parameters.Select((p, i) => new { p, i }).Where(x => x.p.ParameterType.IsByRef).Select(x => new JObject { ["index"] = x.i, ["value"] = handles.Describe(values[x.i]) })) };
        }
        public JToken Items(JObject args)
        {
            var value = handles.Get(Wire.Required(args, "handle"));
            if (!(value is IList list)) throw new BridgeException("unsupported_collection", "Only explicit IList/array indexing is supported; arbitrary enumerators are not executed.");
            return Page(Enumerable.Range(0, list.Count).Select(i => new JObject { ["index"] = i, ["value"] = handles.Describe(list[i]) }), args);
        }
        public static JToken Page(IEnumerable<JToken> source, JObject args)
        {
            var offset = Math.Max(0, (int?)args["offset"] ?? 0);
            var limit = Wire.Limit(args);
            var items = source.Skip(offset).Take(limit + 1).ToArray();
            return new JObject { ["items"] = new JArray(items.Take(limit)), ["offset"] = offset, ["nextOffset"] = items.Length > limit ? (JToken)(offset + limit) : JValue.CreateNull(), ["truncated"] = items.Length > limit };
        }
    }
}
