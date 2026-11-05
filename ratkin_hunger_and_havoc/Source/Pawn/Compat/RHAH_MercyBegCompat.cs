using System;
using System.Reflection;
using HarmonyLib;
using HungerAndHavoc.Api;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    // 战败求饶把 敌对派系 + Goto(exitMapOnArrival) 读作溃逃, 会在离场途中接管来客并弹出求饶菜单.
    // 本模组来客由选择信统一处置, 不进对方候选名单.
    internal static class RHAH_MercyBegCompat
    {
        const string PackageId = "satsukiseiyoshi.mercybeg";
        const string ScannerType = "MercyBeg.MapComponent_MercyBegScanner";

        internal static void Apply(Harmony harmony)
        {
            if (harmony == null || !ModsConfig.IsActive(PackageId))
            {
                return;
            }

            try
            {
                Type scanner = AccessTools.TypeByName(ScannerType);
                MethodInfo isCandidate = scanner == null
                    ? null
                    : AccessTools.Method(scanner, "IsCandidate", new[] { typeof(Verse.Pawn) });
                if (isCandidate == null || !isCandidate.IsStatic || isCandidate.ReturnType != typeof(bool))
                {
                    Log.Warning("[RHAH] MercyBeg scanner contract not found; leaving its begging behavior untouched");
                    return;
                }

                harmony.Patch(isCandidate,
                    prefix: new HarmonyMethod(typeof(RHAH_MercyBegCompat), nameof(Prefix)));
            }
            catch (Exception exception)
            {
                Log.Warning("[RHAH] MercyBeg compat skipped: " + exception);
            }
        }

        internal static bool ShouldSuppress(Verse.Pawn pawn)
        {
            return RHAH_Api.IsVisitor(pawn);
        }

        // 按下标取参 对方改了形参名也不会让补丁绑定失败
        static bool Prefix(Verse.Pawn __0, ref bool __result)
        {
            if (!ShouldSuppress(__0))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
