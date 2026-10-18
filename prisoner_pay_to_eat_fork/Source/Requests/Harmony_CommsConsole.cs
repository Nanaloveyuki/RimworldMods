using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 通讯台右键菜单追加"为囚犯索要饭票"：殖民者或奴隶可用，打开索要窗口
    /// （选囚犯/定金额/看成功率），确认后该小人走到通讯台打电话。
    /// </summary>
    [HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetFloatMenuOptions))]
    public static class Harmony_CommsConsole
    {
        [HarmonyPostfix]
        public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result,
            Building_CommsConsole __instance, Pawn myPawn)
        {
            foreach (var o in __result) yield return o;

            var s = PrisonersPayToEat2Mod.Settings;
            if (s == null || !s.enableFactionRequest) yield break;
            if (myPawn == null || (!myPawn.IsColonistPlayerControlled && !myPawn.IsSlaveOfColony)) yield break;
            if (!__instance.CanUseCommsNow) yield break;
            if (!FactionRequestSystem.AnyEligiblePrisoner(__instance.Map)) yield break;
            if (!myPawn.CanReach(__instance, PathEndMode.Touch, Danger.Deadly)) yield break;

            var console = __instance;
            yield return new FloatMenuOption("PPTE2_RequestTicketsOption".Translate(),
                () => Find.WindowStack.Add(new Dialog_RequestTickets(console, myPawn)));
        }
    }
}
