using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using Verse;

namespace HungerAndHavoc.Core
{
    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            // 启动时绑定 API 宿主 再打 Harmony 补丁
            RHAH_Api.Bind(new RHAH_ApiHost());
            Harmony harmony = new Harmony(RHAH_Runtime.HarmonyId);
            harmony.PatchAll();
            HungerAndHavoc.Pawn.Compat.RHAH_MercyBegCompat.Apply(harmony);
            HungerAndHavoc.EventMgr.RHAH_EventChainStartup.Initialize();
        }
    }
}
