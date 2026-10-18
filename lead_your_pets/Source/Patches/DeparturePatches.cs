using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    [HarmonyPatch(typeof(Lord), nameof(Lord.GotoToil))]
    internal static class Patch_Lord_TravelDeparture
    {
        internal static void Postfix(Lord __instance)
        {
            LeadYourPetUtility.Component?.PrepareTravelDeparture(__instance, interruptJobs: true);
        }
    }

    [HarmonyPatch(typeof(ThinkNode_Duty), nameof(ThinkNode_Duty.TryIssueJobPackage))]
    internal static class Patch_Duty_TravelDepartureCarry
    {
        internal static bool Prefix(ThinkNode_Duty __instance, Pawn pawn, JobIssueParams jobParams, ref ThinkResult __result)
        {
            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component == null || !LeadYourPetUtility.IsNonPlayerTravelDeparture(pawn))
            {
                return true;
            }

            component.PrepareTravelDeparture(pawn.GetLord());
            Job job = component.TryGetTravelDepartureCarryJob(pawn);
            if (job == null)
            {
                return true;
            }

            __result = pawn.GetLord().Notify_DutyResult(new ThinkResult(job, __instance), pawn, jobParams);
            if (__result.Job != null)
            {
                __result.Job.lord = pawn.GetLord();
                __result.Job.source = pawn.mindState.duty.source;
                __result.Job.dutyTag = pawn.mindState.duty.tag;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(LordToil_Travel), nameof(LordToil_Travel.LordToilTick))]
    internal static class Patch_Travel_CarriedMemberArrival
    {
        internal static bool Prefix(LordToil_Travel __instance)
        {
            if (!LeadYourPetUtility.IsDepartureToil(__instance.lord, __instance))
            {
                return true;
            }

            for (int i = 0; i < __instance.lord.ownedPawns.Count; i++)
            {
                Pawn member = __instance.lord.ownedPawns[i];
                if (GodHandsCompat.IsGrabbed(member)
                    || (!member.Spawned && member.CarriedBy != null
                        && LeadYourPetUtility.IsUnprotectedTravelStock(member)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
