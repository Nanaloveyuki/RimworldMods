using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class MouseDisasterSpawnSetupPatch
    {
        public static void Prefix(Pawn __instance, Map map, bool respawningAfterLoad)
        {
            // Pawn.SpawnSetup 的 Prefix 仍在 Thing.SpawnSetup 注册 DynamicDrawManager 之前，
            // 先完成温度服饰装备，避免在 Pawn 已进入动态绘制后修改其服饰/渲染树。
            if (respawningAfterLoad || __instance == null || __instance.Spawned || map == null)
            {
                return;
            }

            try
            {
                if (!MouseDisasterUtility.IsMouseDisasterPawn(__instance))
                {
                    return;
                }

                MouseDisasterUtility.ApplyTemperatureProtectionApparel(
                    __instance, map, __instance.Position);
            }
            catch (Exception exception)
            {
                // 温度服饰只是附加保护；即使第三方 Def 或地图温度异常，也不能阻断原版 SpawnSetup。
                Log.Warning("[MouseDisaster] Pre-spawn temperature apparel hook failed for " +
                    MouseDisasterTrace.DescribePawn(__instance) + "; " +
                    MouseDisasterTrace.DescribeMap(map) + ": " + exception);
            }
        }

        public static void Postfix(Pawn __instance)
        {
            // SpawnSetup 的 Postfix 处理地图缓存、事件通知和已加载鼠族 pawn 的身份迁移；
            // 温度服饰仍只在 Prefix 中处理，避免在动态绘制注册后修改渲染树。
            MouseDisasterUtility.MarkMapPawnCacheDirty(__instance);
            GameComponent_MouseDisasterEventBehavior.Component?.NotifySpawned(__instance);
            if (__instance != null && __instance.Spawned)
            {
                try
                {
                    MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(__instance);
                }
                catch (Exception exception)
                {
                    Log.Warning("[MouseDisaster] Spawned pawn xenotype normalization failed for " +
                        MouseDisasterTrace.DescribePawn(__instance) + ": " + exception);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class MouseDisasterSetFactionPatch
    {
        public static void Postfix(Pawn __instance)
        {
            MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    public static class MouseDisasterDeSpawnPatch
    {
        public static void Prefix(Pawn __instance)
        {
            MouseDisasterUtility.MarkMapPawnCacheDirty(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class MouseDisasterKillPatch
    {
        public static void Prefix(Pawn __instance)
        {
            MouseDisasterUtility.MarkMapPawnCacheDirty(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus), new[] { typeof(Faction), typeof(GuestStatus) })]
    public static class MouseDisasterGuestStatusPatch
    {
        private static readonly AccessTools.FieldRef<Pawn_GuestTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_GuestTracker, Pawn>("pawn");

        public static void Postfix(Pawn_GuestTracker __instance)
        {
            Pawn pawn = PawnField(__instance);
            if (pawn == null || pawn.Dead || !MouseDisasterUtility.IsRatkin(pawn))
            {
                return;
            }

            if (!MouseDisasterUtility.ShouldSkipIdentityNormalizationDuringFactionTeardown(pawn))
            {
                MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
            }

            MouseDisasterUtility.ConfigureNoRescueJoinForIncidentVisitor(pawn);
            MouseDisasterUtility.TryRecoverIncidentVisitorFromPlayerGuest(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_GuestTracker), "Notify_PawnUndowned")]
    public static class MouseDisasterGuestUndownedPatch
    {
        private static readonly AccessTools.FieldRef<Pawn_GuestTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_GuestTracker, Pawn>("pawn");

        public static void Postfix(Pawn_GuestTracker __instance)
        {
            Pawn pawn = PawnField(__instance);
            MouseDisasterUtility.TryRecoverIncidentVisitorFromPlayerGuest(pawn);
        }
    }

}
