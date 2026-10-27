using HarmonyLib;
using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 开门设置与离场共用安全限制 原版拒绝始终保留
    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.PawnCanOpen))]
    internal static class RHAH_DoorPatch
    {
        static void Postfix(Building_Door __instance, Verse.Pawn p, ref bool __result)
        {
            if (!__result)
            {
                return;
            }

            CompRHAH_Pawn visitorComp = RestrictedVisitor(p);
            if (visitorComp == null)
            {
                return;
            }

            __result = (RHAH_Mod.Settings?.allowVisitorDoors == true ||
                visitorComp.State.lifecycle == RHAH_Lifecycle.Leaving) && !HostileThreat(p.Map);
        }

        static CompRHAH_Pawn RestrictedVisitor(Verse.Pawn pawn)
        {
            if (pawn?.Faction == null || pawn.Faction.IsPlayer ||
                !RHAH_AttitudeFactions.IsAttitudeFaction(pawn.Faction))
            {
                return null;
            }

            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            return comp != null && comp.State.IsActiveVisitor ? comp : null;
        }

        internal static void InvalidateVisitorReachability()
        {
            List<Map> maps = Current.Game?.Maps;
            for (int mapIndex = 0; maps != null && mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                IReadOnlyList<Verse.Pawn> pawns = map?.mapPawns?.AllPawnsSpawned;
                if (map?.reachability == null || pawns == null)
                {
                    continue;
                }

                for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                {
                    Verse.Pawn pawn = pawns[pawnIndex];
                    if (RestrictedVisitor(pawn) != null)
                    {
                        map.reachability.ClearCacheFor(pawn);
                    }
                }
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
