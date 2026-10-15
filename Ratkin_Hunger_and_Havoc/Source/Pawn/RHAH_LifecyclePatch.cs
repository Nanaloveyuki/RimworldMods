using HarmonyLib;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Verse.AI;
using HungerAndHavoc.Core;
namespace HungerAndHavoc.Pawn
{
    // 原版吃完一口后才记吃饱 啃树皮不走这条
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest))]
    internal static class RHAH_IngestPatch
    {
        static void Postfix(Verse.Pawn ingester, Toil __result)
        {
            if (__result == null || ingester == null || !RHAH_Api.IsVisitor(ingester))
            {
                return;
            }

            float before = -1f;
            __result.AddPreInitAction(delegate
            {
                before = ingester.needs?.food?.CurLevel ?? -1f;
            });
            __result.AddFinishAction(delegate
            {
                RHAH_Feeding.TryComplete(ingester);
                if (before >= 0f && ingester.needs?.food?.CurLevel > before)
                {
                    RHAH_Begging.NoteFoodReceived(ingester);
                }
                Thing eaten = ingester.jobs?.curJob?.GetTarget(TargetIndex.A).Thing;
                Compat.RHAH_RatEggCuisine.NoteEaten(ingester, eaten?.def?.defName);
            });
        }
    }

    // 原版死亡不会改来源生命周期 这里只收本模组仍活跃的来客
    [HarmonyPatch(typeof(Verse.Pawn), nameof(Verse.Pawn.Kill))]
    internal static class RHAH_DeathPatch
    {
        static void Postfix(Verse.Pawn __instance, DamageInfo? dinfo)
        {
            if (__instance == null || !__instance.Dead)
            {
                return;
            }

            HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.NotifyDeath(__instance, dinfo?.Instigator?.Faction == Faction.OfPlayer ||
                (!dinfo.HasValue && HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.PlayerFatalBleeding(__instance)));
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(__instance);
            if (comp == null || !comp.State.IsActiveVisitor)
            {
                return;
            }

            RHAH_Api.SetLifecycle(__instance, RHAH_Lifecycle.Dead);
            RHAH_VisitorGroup.NotifyDead(__instance);
            if (HungerAndHavoc.Incidents.RHAH_Interception.IsInterception(comp.State.spawnBatchId))
            {
                return;
            }

            RHAH_SuiyinTrust.Note(1, RHAH_SuiyinTrust.Value(RHAH_SuiyinTrust.Kill, RHAH_Mod.Settings == null ? RHAH_SuiyinTrust.Kill : RHAH_Mod.Settings.trustCaptive));
        }
    }

    // 走到边缘离图才算成功离开 俘虏和奴隶不算
    [HarmonyPatch(typeof(Verse.Pawn), nameof(Verse.Pawn.ExitMap))]
    internal static class RHAH_ExitTrustPatch
    {

        static void Postfix(Verse.Pawn __instance)
        {
            if (__instance == null || __instance.Spawned || __instance.Dead)
            {
                return;
            }

            HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.NotifyPawnChanged(__instance);
            if (__instance.IsPrisoner || __instance.IsSlave)
            {
                return;
            }

            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(__instance);
            if (comp == null || comp.State.lifecycle != RHAH_Lifecycle.Leaving)
            {
                return;
            }
            if (HungerAndHavoc.Incidents.RHAH_Interception.IsInterception(comp.State.spawnBatchId))
            {
                return;
            }


            if (HungerAndHavoc.Incidents.RHAH_ChoiceRuntime.TryJoinAlly(__instance))
            {
                return;
            }

            RHAH_SuiyinTrust.Note(1, RHAH_SuiyinTrust.Value(RHAH_SuiyinTrust.Leave, RHAH_Mod.Settings == null ? RHAH_SuiyinTrust.Leave : RHAH_Mod.Settings.trustLeave));
        }
    }

    // 十四岁心情只在生物学生日结算 不扫图
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    internal static class RHAH_BirthdayMoodPatch
    {
        static void Postfix(Verse.Pawn ___pawn, int birthdayAge)
        {
            HungerAndHavoc.EventMgr.RHAH_EventFollowMood.OnBirthday(___pawn, birthdayAge);
            HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.NotifyPawnChanged(___pawn);
        }
    }
}
