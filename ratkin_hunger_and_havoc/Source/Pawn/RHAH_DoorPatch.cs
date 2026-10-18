using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 默认不让饥民开门 地图上有敌人时即使允许也不开
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    internal static class RHAH_DoorPatch
    {
        static void Postfix(Building_Door __instance, Verse.Pawn p, ref bool __result)
        {
            if (!__result || p?.Faction == null || p.Faction.IsPlayer ||
                !RHAH_AttitudeFactions.IsAttitudeFaction(p.Faction) || !RHAH_Api.IsVisitor(p))
            {
                return;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null || !settings.famineVisitorsOpenDoors || HostileThreat(p.Map))
            {
                __result = false;
            }
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
