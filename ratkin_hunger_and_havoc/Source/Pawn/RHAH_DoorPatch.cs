using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 来客只在离场时开门 地图上有敌人时离场也不开
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    internal static class RHAH_DoorPatch
    {
        static void Postfix(Building_Door __instance, Verse.Pawn p, ref bool __result)
        {
            if (!__result || p?.Faction == null || p.Faction.IsPlayer ||
                !RHAH_AttitudeFactions.IsAttitudeFaction(p.Faction))
            {
                return;
            }

            CompRHAH_Pawn visitorComp = CompRHAH_Pawn.TryGet(p);
            if (visitorComp == null)
            {
                return;
            }

            // 先判该不该管 非活跃访客保留原版结果
            IRHAH_Pawn visitorSnapshot = RHAH_Api.Get(p);
            if (visitorSnapshot == null || !visitorSnapshot.IsActiveVisitor)
            {
                return;
            }

            // 敌情先于离场放行 否则围城时离场的客人会给敌人开门
            if (HostileThreat(p.Map))
            {
                __result = false;
                return;
            }

            // 离场的人要走门 锁着门只会逼他去砸殖民者的墙 走不出去时 Leave 里有凿墙兜底
            if (visitorComp.State.lifecycle == RHAH_Lifecycle.Leaving)
            {
                return;
            }

            __result = false;
        }

        static bool HostileThreat(Map map)
        {
            return map != null && GenHostility.AnyHostileActiveThreatToPlayer(map);
        }
    }

    // 来客入场就会给自己治疗 玩家错过检疫信就救不回来
    [HarmonyPatch(typeof(JobGiver_SelfTend), "TryGiveJob")]
    internal static class RHAH_PlagueTendPatch
    {
        static bool Prefix(Verse.Pawn pawn)
        {
            return !RHAH_Plague.HasActive(pawn) || !RHAH_Api.IsVisitor(pawn);
        }
    }

    // 安全模式停在致死线以下 只剩无法行动
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.Severity), MethodType.Setter)]
    internal static class RHAH_PlagueSafePatch
    {
        static void Prefix(Hediff __instance, ref float value)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null || !settings.plagueSafeMode || __instance?.def == null ||
                __instance.def.defName != "RHAH_Plague" || __instance.def.lethalSeverity <= 0f)
            {
                return;
            }

            float cap = __instance.def.lethalSeverity - 0.001f;
            if (value > cap)
            {
                value = cap;
            }
        }
    }
}
