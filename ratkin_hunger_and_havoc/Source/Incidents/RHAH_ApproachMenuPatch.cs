using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // 右键只问鼠标下的物体 玩家队图标盖住同格鼠族队时菜单里没有拦截
    [HarmonyPatch(typeof(FloatMenuMakerWorld), nameof(FloatMenuMakerWorld.ChoicesAtFor), typeof(Vector2), typeof(RimWorld.Planet.Caravan))]
    internal static class RHAH_ApproachMenuPatch
    {
        static void Postfix(Vector2 mousePos, RimWorld.Planet.Caravan caravan, List<FloatMenuOption> __result)
        {
            if (__result == null || caravan == null || Find.WorldObjects == null)
            {
                return;
            }

            List<WorldObject> underMouse = GenWorldUI.WorldObjectsUnderMouse(mousePos);
            bool playerHit = false;
            for (int i = 0; i < underMouse.Count; i++)
            {
                if (underMouse[i] == caravan)
                {
                    playerHit = true;
                    break;
                }
            }

            List<WorldObject> objects = Find.WorldObjects.AllWorldObjects;
            for (int i = 0; i < objects.Count; i++)
            {
                WorldObject_RHAH_Approach approach = objects[i] as WorldObject_RHAH_Approach;
                if (approach == null)
                {
                    continue;
                }

                bool listed = false;
                for (int hit = 0; hit < underMouse.Count; hit++)
                {
                    if (underMouse[hit] == approach)
                    {
                        listed = true;
                        break;
                    }
                }
                if (!RHAH_ApproachRules.IncludeApproachMenu(
                    playerHit,
                    listed,
                    approach.Tile.Valid && approach.Tile == caravan.Tile,
                    approach.CanIntercept(caravan)))
                {
                    continue;
                }

                foreach (FloatMenuOption option in approach.GetFloatMenuOptions(caravan))
                {
                    __result.Add(option);
                }
            }
        }
    }
}
