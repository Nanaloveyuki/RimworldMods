using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimCarbon14.Core;

namespace RimCarbon14.Mod
{
    internal sealed class WatchInstrumentation
    {
        internal sealed class Site
        {
            public int Id; public string CallerId = ""; public string DeclaredMethodId = ""; public string Dispatch = "";
            public int InstructionIndex; internal MethodInfo Target = null!;
        }
        private readonly Harmony harmony = new Harmony("nanaloveyuki.rimcarbon14.watch");
        private readonly Queue<(MethodInfo Method, int Depth)> pending = new Queue<(MethodInfo, int)>();
        private readonly Dictionary<MethodInfo, int> depths = new Dictionary<MethodInfo, int>();
        private readonly Dictionary<(int, Type), string> targets = new Dictionary<(int, Type), string>();
        private readonly Dictionary<MethodInfo, int> rootIds = new Dictionary<MethodInfo, int>();
        private readonly Dictionary<MethodInfo, string> rootMethods = new Dictionary<MethodInfo, string>();
        private readonly Dictionary<MethodInfo, string> bodyMethods = new Dictionary<MethodInfo, string>();
        internal readonly List<Site> Sites = new List<Site>();
        internal readonly Dictionary<string, string> Methods = new Dictionary<string, string>();
        internal readonly List<object> Gaps = new List<object>();
        internal int OmittedGaps;
        private readonly HashSet<MethodInfo> patched = new HashSet<MethodInfo>();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private Type[]? types;
        private int maxDepth;
        private int methodBudget;
        private int siteBudget;
        private int compilingDepth;
        private bool stopping;
        private static WatchInstrumentation Current => BridgeHost.Instance.Watch.Instrumentation;
        internal static readonly MethodInfo TickMethod = AccessTools.Method(typeof(Verse.TickManager), "DoSingleTick");
        internal void Install(MethodInfo[] roots, int depth, int maxMethods, int maxSites)
        {
            maxDepth = depth; methodBudget = maxMethods; siteBudget = maxSites;
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (!CanDescend(root)) throw new BridgeException("unsupported_watch_root", root.ToString() ?? "Method");
                rootIds.Add(root, -i - 1); rootMethods.Add(root, AddMethod(root)); Enqueue(root, 0);
            }
            try
            {
                while (pending.Count > 0)
                {
                    var item = pending.Dequeue();
                    if (!patched.Add(item.Method)) continue;
                    compilingDepth = item.Depth;
                    bool tick = item.Method == TickMethod;
                    bodyMethods[item.Method] = ReflectionTools.MethodId(item.Method);
                    harmony.Patch(item.Method,
                        new HarmonyMethod(typeof(WatchTools), tick ? nameof(WatchTools.TickBefore) : nameof(WatchTools.BodyBefore)) { priority = Priority.First },
                        null, item.Depth < maxDepth ? new HarmonyMethod(typeof(WatchInstrumentation), nameof(Transpile)) { priority = Priority.Last } : null,
                        new HarmonyMethod(typeof(WatchTools), tick ? nameof(WatchTools.TickAfter) : nameof(WatchTools.BodyAfter)) { priority = Priority.Last });
                    if (item.Depth < maxDepth) expanded.Add(ReflectionTools.MethodId(item.Method));
                }
                if (!patched.Contains(TickMethod))
                    harmony.Patch(TickMethod, new HarmonyMethod(typeof(WatchTools), nameof(WatchTools.TickBefore)) { priority = Priority.First },
                        null, null, new HarmonyMethod(typeof(WatchTools), nameof(WatchTools.TickAfter)) { priority = Priority.Last });
            }
            catch { Stop(); throw; }
        }
        internal void Stop() { stopping = true; harmony.UnpatchAll(harmony.Id); }
        internal int RootId(MethodInfo method) => rootIds.TryGetValue(method, out int id) ? id : 0;
        internal string RootMethodId(MethodInfo method) => rootMethods[method];
        internal string BodyMethodId(MethodInfo method) => bodyMethods[method];
        internal object RootMetadata() => rootIds.Select(p => new { siteId = p.Value, methodId = rootMethods[p.Key] }).ToArray();
        internal bool Expanded(string method) => expanded.Contains(method);
        internal string Resolve(Site site, object? receiver)
        {
            if (site.Dispatch != "callvirt" || !site.Target.IsVirtual || receiver == null) return site.DeclaredMethodId;
            Type type = receiver.GetType(); var key = (site.Id, type);
            if (targets.TryGetValue(key, out var id)) return id;
            var target = ResolveVirtual(site.Target, type) ?? site.Target;
            id = AddMethod(target); targets.Add(key, id); return id;
        }
        private string AddMethod(MethodInfo method)
        {
            string id = ReflectionTools.MethodId(method);
            Methods[id] = method.DeclaringType?.FullName + "." + method;
            return id;
        }
        private void Enqueue(MethodInfo method, int depth)
        {
            if (depth >= maxDepth && !rootIds.ContainsKey(method)) return;
            if (!CanDescend(method)) { Gap(new { methodId = ReflectionTools.MethodId(method), reason = "body_not_expanded" }); return; }
            // Reflection on a subclass can return inherited bodies with a different ReflectedType.
            // Harmony requires the declaration, not that reflected alias.
            if (method.ReflectedType != method.DeclaringType) method = (MethodInfo)(method.Module.ResolveMethod(method.MetadataToken) ?? throw new BridgeException("method_not_found", "The declaring method is no longer available."));
            if (depths.ContainsKey(method)) return;
            if (depths.Count >= methodBudget) { Gap(new { methodId = ReflectionTools.MethodId(method), reason = "method_limit" }); return; }
            depths.Add(method, depth); pending.Enqueue((method, depth));
        }
        private void Gap(object value) { if (Gaps.Count < 4096) Gaps.Add(value); else OmittedGaps++; }
        private void Descend(MethodInfo method, bool virtualCall, int depth)
        {
            Enqueue(method, depth);
            if (depth >= maxDepth || !virtualCall || !method.IsVirtual || method.IsFinal) return;
            if (types == null)
                types = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && IsGameAssembly(a)).SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { Gap(new { assembly = a.FullName, reason = "partial_type_load" }); return ex.Types.Where(t => t != null).Cast<Type>().ToArray(); }
                }).ToArray();
            foreach (var type in types.Where(t => !t.IsInterface && !t.ContainsGenericParameters && method.DeclaringType!.IsAssignableFrom(t)))
            {
                var target = ResolveVirtual(method, type);
                if (target != null) Enqueue(target, depth);
            }
        }
        private static MethodInfo? ResolveVirtual(MethodInfo method, Type type)
        {
            if (method.DeclaringType!.IsInterface)
            {
                var map = type.GetInterfaceMap(method.DeclaringType);
                int i = Array.IndexOf(map.InterfaceMethods, method); return i < 0 ? null : map.TargetMethods[i];
            }
            var definition = method.GetBaseDefinition();
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.IsVirtual && m.GetBaseDefinition() == definition);
        }
        private static bool IsGameAssembly(Assembly a)
        {
            string name = a.GetName().Name ?? "";
            return a != typeof(WatchTools).Assembly && a != typeof(Wire).Assembly && a != typeof(Harmony).Assembly &&
                a != typeof(object).Assembly && !name.StartsWith("System", StringComparison.Ordinal) && !name.StartsWith("Unity", StringComparison.Ordinal) &&
                !name.StartsWith("Microsoft", StringComparison.Ordinal) && !name.StartsWith("Mono", StringComparison.Ordinal) && name != "netstandard" && name != "Newtonsoft.Json";
        }
        private static bool CanDescend(MethodInfo m) => IsGameAssembly(m.Module.Assembly) && !m.IsGenericMethod && m.DeclaringType?.IsGenericType != true && !m.ContainsGenericParameters && !m.IsAbstract && m.GetMethodBody() != null;
        private static bool UnsafeType(Type t) => t.IsPointer || t.IsByRef && UnsafeType(t.GetElementType()!) || t == typeof(TypedReference) || t.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute");
        private static string? Unsupported(CodeInstruction call, CodeInstruction? previous)
        {
            if (previous?.opcode.OpCodeType == OpCodeType.Prefix) return "prefixed_call";
            if (!(call.operand is MethodInfo m)) return "constructor_or_indirect_call";
            if (m.IsGenericMethod || m.DeclaringType?.IsGenericType == true || m.ContainsGenericParameters || m.ReturnType.IsByRef || UnsafeType(m.ReturnType) || m.GetParameters().Any(p => UnsafeType(p.ParameterType)) ||
                (m.CallingConvention & CallingConventions.VarArgs) != 0) return "unsupported_signature";
            if (m.DeclaringType == null || UnsafeType(m.DeclaringType)) return "unsupported_declaring_type";
            return null;
        }
        private static IEnumerable<CodeInstruction> Transpile(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions)
        {
            var self = Current;
            // Harmony can rebuild a method between removing our prefix and our transpiler.
            if (self.stopping) return instructions;
            var codes = instructions.ToList(); string caller = ReflectionTools.MethodId((MethodInfo)__originalMethod);
            int callerDepth = self.depths.TryGetValue((MethodInfo)__originalMethod, out int knownDepth) ? knownDepth : self.compilingDepth;
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt && code.opcode != OpCodes.Calli && code.opcode != OpCodes.Newobj) continue;
                string? reason = Unsupported(code, i == 0 ? null : codes[i - 1]);
                if (code.opcode == OpCodes.Newobj || code.opcode == OpCodes.Calli) reason = "constructor_or_indirect_call";
                if (self.Sites.Count >= self.siteBudget) reason = "site_limit";
                if (reason != null) { self.Gap(new { callerId = caller, instructionIndex = i, reason }); continue; }
                var method = (MethodInfo)code.operand;
                // Bridge internals must never recursively observe the observer.
                if (method.Module.Assembly == typeof(WatchTools).Assembly || method.Module.Assembly == typeof(Wire).Assembly) continue;
                var site = new Site { Id = self.Sites.Count, CallerId = caller, DeclaredMethodId = self.AddMethod(method), Target = method, Dispatch = code.opcode.Name!, InstructionIndex = i };
                self.Sites.Add(site);
                self.Descend(method, code.opcode == OpCodes.Callvirt, callerDepth + 1);
                codes[i] = new CodeInstruction(code) { opcode = OpCodes.Call, operand = Wrapper(method, code.opcode, site.Id) };
            }
            return codes;
        }
        private static DynamicMethod Wrapper(MethodInfo target, OpCode opcode, int site)
        {
            var parameters = target.GetParameters().Select(p => p.ParameterType).ToList();
            if (!target.IsStatic) parameters.Insert(0, target.DeclaringType!.IsValueType ? target.DeclaringType.MakeByRefType() : target.DeclaringType);
            var method = new DynamicMethod("CarbonWatch_" + site, target.ReturnType, parameters.ToArray(), typeof(WatchTools).Module, true);
            var il = method.GetILGenerator();
            var token = il.DeclareLocal(typeof(int)); var success = il.DeclareLocal(typeof(bool));
            LocalBuilder? result = target.ReturnType == typeof(void) ? null : il.DeclareLocal(target.ReturnType);
            il.Emit(OpCodes.Ldc_I4, site);
            if (!target.IsStatic && !target.DeclaringType!.IsValueType) il.Emit(OpCodes.Ldarg_0); else il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(WatchTools), nameof(WatchTools.CallBefore))); il.Emit(OpCodes.Stloc, token);
            il.BeginExceptionBlock();
            for (int i = 0; i < parameters.Count; i++) il.Emit(OpCodes.Ldarg, i);
            il.Emit(opcode, target); if (result != null) il.Emit(OpCodes.Stloc, result);
            il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Stloc, success);
            il.BeginFinallyBlock();
            il.Emit(OpCodes.Ldloc, token); il.Emit(OpCodes.Ldloc, success);
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(WatchTools), nameof(WatchTools.CallAfter)));
            il.EndExceptionBlock();
            if (result != null) il.Emit(OpCodes.Ldloc, result);
            il.Emit(OpCodes.Ret); return method;
        }
    }
}
