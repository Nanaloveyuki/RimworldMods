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
            new Harmony(RHAH_Runtime.HarmonyId).PatchAll();
            HungerAndHavoc.EventMgr.RHAH_EventChainStartup.Initialize();
        }
    }
}
