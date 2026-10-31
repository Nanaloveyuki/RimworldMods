using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    // 原版伤害不会按批次改态度 这里只接玩家外部暴力
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class RHAH_AttitudeHarmPatch
    {
        internal struct HarmContext
        {
            internal int ArmedChoiceId;
            internal Lord Camp;
        }

        internal static void Prefix(Thing __instance, out HarmContext __state)
        {
            Verse.Pawn pawn = __instance as Verse.Pawn;
            __state = new HarmContext
            {
                ArmedChoiceId = (pawn?.GetLord()?.LordJob as LordJob_RHAH_Visitor)?.ArmedChoiceId ?? 0,
                Camp = RHAH_CampResidents.CaptureHarm(pawn)
            };
        }

        internal static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result, HarmContext __state)
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
            if (RHAH_CampResidents.NotifyPlayerHarm(__state.Camp)) return;
            if (RHAH_ArmedDemand.NotifyPlayerHarm(__state.ArmedChoiceId)) return;
            RHAH_BatchAttitude.TryShift(harmed, RHAH_BatchReaction.Harm);
        }
    }
}
