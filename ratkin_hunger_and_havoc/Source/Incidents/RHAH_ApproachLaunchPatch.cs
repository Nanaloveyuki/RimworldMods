using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // 起飞选点先命中远行队图标 太空上的队伍不该挡住空轨道格
    [HarmonyPatch(typeof(WorldTargeter), "CurrentTargetUnderMouse")]
    internal static class RHAH_ApproachLaunchPatch
    {
        static void Postfix(ref GlobalTargetInfo __result)
        {
            if (!Find.WorldTargeter.IsTargeting || __result.WorldObject is not WorldObject_RHAH_Approach approach)
            {
                return;
            }

            if (RHAH_ApproachRules.BlocksLaunch(RHAH_Approach.SpaceLayer(approach.Tile)))
            {
                return;
            }

            PlanetTile tile = approach.Tile;
            if (!tile.Valid)
            {
                __result = GlobalTargetInfo.Invalid;
                return;
            }

            foreach (WorldObject candidate in Find.WorldObjects.ObjectsAt(tile))
            {
                if (candidate != approach && candidate.def.validLaunchTarget)
                {
                    __result = candidate;
                    return;
                }
            }

            __result = new GlobalTargetInfo(tile);
        }
    }
}
