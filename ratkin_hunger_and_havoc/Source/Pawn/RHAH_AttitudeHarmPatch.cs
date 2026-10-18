using HarmonyLib;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    // 原版伤害不会按批次改态度 这里只接玩家外部暴力
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class RHAH_AttitudeHarmPatch
    {
        static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (__result == null || __result.totalDamageDealt <= 0f || dinfo.Def == null ||
                !dinfo.Def.harmsHealth || !dinfo.Def.ExternalViolenceFor(__instance))
            {
                return;
            }

            if (dinfo.Instigator?.Faction != Faction.OfPlayer)
            {
                return;
            }

            Verse.Pawn harmed = __instance as Verse.Pawn;
            if (harmed == null)
            {
                return;
            }

            HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.NotePlayerWounds(harmed, __result.hediffs);
            RHAH_BatchAttitude.TryShift(harmed, RHAH_BatchReaction.Harm);
        }
    }
}
