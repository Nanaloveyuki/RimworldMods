using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Dubs Bad Hygiene（卫生与温控）软兼容：全部走反射，不引用 DBH 程序集。
    /// 扫描 DBH 程序集中名字含 Toilet/Shower/Wash/Bath/Sink 的 JobDriver / JobGiver / WorkGiver：
    ///   - 扣费点：当前 JobDriver.TryMakePreToilReservations 成功后（开始使用时缴费，一次一结）；
    ///   - 禁用点：同名方法的 prefix 与 JobGiver/WorkGiver 的 postfix——余额不足时工作不派发，
    ///     囚犯只能憋着（DBH 自有憋尿事故机制）或忍着不洗；河里洗澡目标不是建筑，永远不收费。
    /// 用量取不到 DBH 真实值时退化为固定默认（马桶 5L+0.5Wd，淋浴 20L+1Wd），价格均可在设置调整。
    /// 只 patch 在各类型自身声明的方法（DeclaringType == 该类型），绝不动基类方法。
    /// </summary>
    public static class HygieneBridge
    {
        public const string PackageId = "dubwise.dubsbadhygiene";

        private static bool? _present;
        public static bool Present
        {
            get
            {
                if (_present.HasValue) return _present.Value;
                _present = false;
                foreach (var mc in LoadedModManager.RunningModsListForReading)
                    if (string.Equals(mc.PackageId, PackageId, StringComparison.OrdinalIgnoreCase))
                    { _present = true; break; }
                return _present.Value;
            }
        }

        // 兜底每次用量（取不到 DBH 真实消耗时）
        private const float ToiletWaterLiters = 5f;
        private const float ToiletPowerWd = 0.5f;
        private const float ShowerWaterLiters = 20f;
        private const float ShowerPowerWd = 1f;

        // 建筑 defName -> 每次用量（升, Wd）；反射读取一次后缓存
        private static readonly Dictionary<string, Usage> usageCache = new Dictionary<string, Usage>();
        private struct Usage { public float liters; public float wd; }

        // 拦截提示节流（每囚犯每 0.1 天最多一条）
        private static readonly Dictionary<int, int> lastBlockedMsgTick = new Dictionary<int, int>();
        private const int BlockedMsgIntervalTicks = 6000;

        public static void ResetTransientState()
        {
            lastBlockedMsgTick.Clear();
            usageCache.Clear();
        }

        // ================= 安装补丁 =================

        public static void Apply(Harmony harmony)
        {
            if (!Present) return;
            try
            {
                int patched = 0;
                foreach (var asm in FindDbhAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { types = e.Types; }
                    if (types == null) continue;

                    foreach (var t in types)
                    {
                        if (t == null || t.IsAbstract || t.Name == null) continue;
                        if (!IsHygieneLike(t.Name)) continue;

                        if (typeof(JobDriver).IsAssignableFrom(t))
                        {
                            var m = DeclaredOnly(t, "TryMakePreToilReservations", typeof(bool), typeof(bool));
                            if (m != null && Patch(harmony, m,
                                    new HarmonyMethod(typeof(HygieneBridge), nameof(Prefix_DriverReserve)),
                                    new HarmonyMethod(typeof(HygieneBridge), nameof(Postfix_DriverReserve))))
                                patched++;
                        }
                        else if (typeof(ThinkNode_JobGiver).IsAssignableFrom(t))
                        {
                            var m = DeclaredOnly(t, "TryGiveJob", typeof(Job), typeof(Pawn));
                            if (m != null && Patch(harmony, m, null,
                                    new HarmonyMethod(typeof(HygieneBridge), nameof(Postfix_JobGiver))))
                                patched++;
                        }
                        else if (typeof(WorkGiver_Scanner).IsAssignableFrom(t))
                        {
                            foreach (var m in DeclaredOnlyAll(t, "JobOnThing"))
                            {
                                if (!MatchesSignature(m, typeof(Job), typeof(Pawn), typeof(Thing), typeof(bool))) continue;
                                if (Patch(harmony, m, null,
                                        new HarmonyMethod(typeof(HygieneBridge), nameof(Postfix_WorkGiver))))
                                    patched++;
                            }
                        }
                    }
                }
                if (PrisonersPayToEat2Mod.Settings?.logVerbose == true || patched == 0)
                    Log.Message($"[PPTE2] Dubs Bad Hygiene detected; hygiene-fee patches applied to {patched} methods.");
                if (patched == 0)
                    Log.Error("[PPTE2] Dubs Bad Hygiene is loaded but its 1.7 hygiene API was not found. Hygiene fee compatibility is unavailable; check the installed DBH version.");
            }
            catch (Exception e)
            {
                Log.Error($"[PPTE2] HygieneBridge.Apply failed; Dubs Bad Hygiene fee compatibility is unavailable: {e}");
            }
        }

        private static IEnumerable<Assembly> FindDbhAssemblies()
        {
            foreach (var mc in LoadedModManager.RunningModsListForReading)
            {
                if (!string.Equals(mc.PackageId, PackageId, StringComparison.OrdinalIgnoreCase)) continue;
                var list = mc.assemblies?.loadedAssemblies;
                if (list == null) continue;
                foreach (var a in list)
                    if (a != null) yield return a;
            }
        }

        /// <summary>仅取该类型自身声明的方法（防止误 patch 基类，殃及所有 JobDriver）。</summary>
        private static MethodInfo DeclaredOnly(Type t, string name, Type returnType, params Type[] parameterTypes)
        {
            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                null, parameterTypes, null);
            return m != null && m.DeclaringType == t && m.ReturnType == returnType ? m : null;
        }

        private static bool MatchesSignature(MethodInfo method, Type returnType, params Type[] parameterTypes)
        {
            if (method.ReturnType != returnType) return false;
            var parameters = method.GetParameters();
            if (parameters.Length != parameterTypes.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].ParameterType != parameterTypes[i]) return false;
            return true;
        }

        private static IEnumerable<MethodInfo> DeclaredOnlyAll(Type t, string name)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (m.Name == name && m.DeclaringType == t) yield return m;
        }

        private static bool Patch(Harmony harmony, MethodInfo target, HarmonyMethod prefix, HarmonyMethod postfix)
        {
            try
            {
                harmony.Patch(target, prefix, postfix);
                if (PrisonersPayToEat2Mod.Settings?.logVerbose == true)
                    Log.Message($"[PPTE2] Hygiene fee patch: {target.DeclaringType.FullName}.{target.Name}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"[PPTE2] Failed to patch Dubs Bad Hygiene {target.DeclaringType.FullName}.{target.Name}: {e}");
                return false;
            }
        }

        private static bool IsHygieneLike(string typeName)
        {
            string n = typeName.ToLowerInvariant();
            return n.Contains("toilet") || n.Contains("shower") || n.Contains("bath")
                || n.Contains("wash") || n.Contains("sink") || n.Contains("latrine");
        }

        private static bool IsShowerLike(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            return n.Contains("shower") || n.Contains("bath") || n.Contains("wash") || n.Contains("sink");
        }

        // ================= 计费与禁用判定 =================

        /// <summary>卫生费是否对该囚犯生效（开关 + 囚犯 + 未豁免）。</summary>
        public static bool FeesApply(Pawn pawn)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (s == null || !s.enableHygieneFees) return false;
            if (pawn == null || !pawn.IsPrisonerOfColony) return false;
            return PrisonersPayToEat2Manager.Current?.IsExempt(pawn, FeeCategory.Hygiene) != true;
        }

        /// <summary>
        /// 估算某次使用的价格（饭票）。目标不是建筑（河里洗澡等）免费。
        /// 用量优先反射读 DBH 建筑组件的真实值，取不到用固定默认。
        /// </summary>
        public static float FeeFor(Job job, string contextName)
        {
            var building = job?.targetA.Thing as Building;
            if (building == null) return 0f;
            var s = PrisonersPayToEat2Mod.Settings;
            var usage = UsageFor(building, contextName);
            return usage.liters * s.waterPricePerLiter + usage.wd * s.powerPricePerWd;
        }

        private static Usage UsageFor(Building building, string contextName)
        {
            string key = building.def?.defName ?? "?";
            if (usageCache.TryGetValue(key, out var u)) return u;

            u = IsShowerLike(contextName) || IsShowerLike(building.def?.defName)
                ? new Usage { liters = ShowerWaterLiters, wd = ShowerPowerWd }
                : new Usage { liters = ToiletWaterLiters, wd = ToiletPowerWd };

            // 尽力反射读取 DBH 组件上的真实用量字段/属性（名字含 water/litre 或 power/wd）
            try
            {
                foreach (var comp in building.AllComps)
                {
                    if (comp == null) continue;
                    float? water = TryReadNumber(comp, "water", "litre", "liter");
                    float? power = TryReadNumber(comp, "power", "electric", "wd");
                    if (water.HasValue && water.Value > 0f) u.liters = water.Value;
                    if (power.HasValue && power.Value > 0f) u.wd = power.Value;
                }
            }
            catch { /* 反射失败就用默认值 */ }

            usageCache[key] = u;
            if (PrisonersPayToEat2Mod.Settings?.logVerbose == true)
                Log.Message($"[PPTE2] Hygiene usage for {key}: {u.liters}L + {u.wd}Wd per use");
            return u;
        }

        /// <summary>在 comp 及其 props 上找第一个名字含任一关键字的 float/int 数值。</summary>
        private static float? TryReadNumber(ThingComp comp, params string[] keywords)
        {
            foreach (var host in new object[] { comp, comp.props })
            {
                if (host == null) continue;
                var t = host.GetType();
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (f.FieldType != typeof(float) && f.FieldType != typeof(int)) continue;
                    if (!NameMatches(f.Name, keywords)) continue;
                    return Convert.ToSingle(f.GetValue(host));
                }
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (p.PropertyType != typeof(float) && p.PropertyType != typeof(int)) continue;
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    if (!NameMatches(p.Name, keywords)) continue;
                    return Convert.ToSingle(p.GetValue(host, null));
                }
            }
            return null;
        }

        private static bool NameMatches(string name, string[] keywords)
        {
            string n = name.ToLowerInvariant();
            foreach (var k in keywords)
                if (n.Contains(k)) return true;
            return false;
        }

        private static void Charge(Pawn pawn, Job job, string contextName)
        {
            if (!FeesApply(pawn)) return;
            float cost = FeeFor(job, contextName);
            if (cost < 0.009f) return;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;
            cost = Mathf.Min(cost, mgr.SpendingPower(pawn, allowCredit: false));
            if (cost < 0.009f) return;
            mgr.PayWithSupport(pawn, cost);
            if (pawn.Spawned)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "-" + cost.ToString("0.##"), new Color(0.55f, 0.75f, 1f));
            if (PrisonersPayToEat2Mod.Settings.logVerbose)
                Log.Message($"[PPTE2] {pawn.LabelShortCap} hygiene fee {cost:0.##} tickets ({contextName}). balance={mgr.Balance(pawn):0.##}");
        }

        /// <summary>余额不足则禁用：返回 true 表示这次使用被拦下。</summary>
        private static bool ShouldBlock(Pawn pawn, Job job, string contextName)
        {
            if (!FeesApply(pawn)) return false;
            float cost = FeeFor(job, contextName);
            if (cost < 0.009f) return false; // 免费（河里/未知目标）不拦
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return false;
            float available = mgr.AvailableBalance(pawn);
            if (available >= cost) return false;
            NotifyBlocked(pawn, cost, available);
            return true;
        }

        private static void NotifyBlocked(Pawn pawn, float cost, float balance)
        {
            int now = Find.TickManager.TicksGame;
            if (lastBlockedMsgTick.TryGetValue(pawn.thingIDNumber, out int last)
                && now - last < BlockedMsgIntervalTicks) return;
            lastBlockedMsgTick[pawn.thingIDNumber] = now;
            Messages.Message("PPTE2_HygieneBlocked".Translate(pawn.LabelShortCap,
                    cost.ToString("0.##"), PPTEName.Ticket, balance.ToString("0.##")),
                pawn, MessageTypeDefOf.RejectInput);
        }

        // ================= Harmony 回调（由反射动态挂接） =================

        private static bool Prefix_DriverReserve(JobDriver __instance, ref bool __result)
        {
            try
            {
                var pawn = __instance?.pawn;
                if (ShouldBlock(pawn, __instance?.job, __instance?.GetType().Name))
                {
                    __result = false;
                    return false; // 跳过原方法：保留不住，工作失败
                }
            }
            catch (Exception e) { Log.ErrorOnce($"[PPTE2] hygiene block error: {e}", 181576001); }
            return true;
        }

        private static void Postfix_DriverReserve(JobDriver __instance, bool __result)
        {
            try
            {
                // 1.7 checks queued/ordered jobs before StartJob, then reserves again for the
                // active driver. Only the latter starts a use and may debit the account.
                if (!__result || __instance?.pawn?.jobs?.curDriver != __instance) return;
                Charge(__instance?.pawn, __instance?.job, __instance?.GetType().Name);
            }
            catch (Exception e) { Log.ErrorOnce($"[PPTE2] hygiene charge error: {e}", 181576002); }
        }

        private static void Postfix_JobGiver(Pawn pawn, ref Job __result)
        {
            try
            {
                if (__result == null) return;
                if (ShouldBlock(pawn, __result, __result.def?.defName))
                    __result = null;
            }
            catch (Exception e) { Log.ErrorOnce($"[PPTE2] hygiene jobgiver error: {e}", 181576003); }
        }

        private static void Postfix_WorkGiver(Pawn pawn, Thing t, ref Job __result)
        {
            try
            {
                if (__result == null) return;
                if (ShouldBlock(pawn, __result, t?.def?.defName))
                    __result = null;
            }
            catch (Exception e) { Log.ErrorOnce($"[PPTE2] hygiene workgiver error: {e}", 181576004); }
        }
    }
}
