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
    // VisitColony, TradeWithColony, TravelAndExit and DefendPoint inherit this method.
    [HarmonyPatch(typeof(LordJob), nameof(LordJob.ShouldRemovePawn))]
    internal static class Patch_LordJob_KeepIncapacitatedTravelStock
    {
        internal static void Postfix(LordJob __instance, Pawn p, PawnLostCondition reason, ref bool __result)
        {
            Lord lord = p?.GetLord();
            if (__result && reason == PawnLostCondition.Incapped && p != null && !p.Dead
                && lord?.LordJob == __instance && lord.faction != null && !lord.faction.IsPlayer
                && lord.ownedPawns.Contains(p) && LeadYourPetUtility.IsUnprotectedTravelStock(p))
            {
                __result = false;
            }
        }
    }


    [HarmonyPatch(typeof(ThinkNode_Duty), nameof(ThinkNode_Duty.TryIssueJobPackage))]
    internal static class Patch_Duty_TravelDepartureCarry
    {
        internal static void Postfix(ThinkNode_Duty __instance, Pawn pawn, JobIssueParams jobParams, ref ThinkResult __result)
        {
            if (!CanReplaceWithDepartureCarry(__result))
            {
                return;
            }

            LeadYourPetGameComponent component = LeadYourPetUtility.Component;
            if (component == null || !LeadYourPetUtility.IsNonPlayerTravelDeparture(pawn))
            {
                return;
            }

            component.PrepareTravelDeparture(pawn.GetLord());
            Job job = component.TryGetTravelDepartureCarryJob(pawn);
            if (job == null)
            {
                return;
            }

            Lord lord = pawn.GetLord();
            ThinkResult result = lord.Notify_DutyResult(new ThinkResult(job, __instance), pawn, jobParams);
            if (result.Job == null)
            {
                return;
            }

            result.Job.lord = lord;
            result.Job.source = pawn.mindState.duty.source;
            result.Job.dutyTag = pawn.mindState.duty.tag;
            __result = result;
        }

        private static bool CanReplaceWithDepartureCarry(ThinkResult result)
        {
            if (result.FromQueue)
            {
                return false;
            }

            Job job = result.Job;
            if (job == null)
            {
                return true;
            }

            string name = job.def?.defName;
            if (name == "Follow" || name == "FollowClose" || name == "Wait" || name == "Wait_Wander" || name == "GotoWander")
            {
                return true;
            }

            // Combat and urgent-needs branches can also return Goto; only replace a
            // known travel/exit producer, never arbitrary movement from those branches.
            return name == "Goto" && (result.SourceNode is JobGiver_GotoTravelDestination
                || result.SourceNode is JobGiver_ExitMap);
        }
    }

    [HarmonyPatch(typeof(LordToil_Travel), nameof(LordToil_Travel.LordToilTick))]
    internal static class Patch_Travel_CarriedMemberArrival
    {
        internal static bool Prefix(LordToil_Travel __instance)
        {
            if (Find.TickManager.TicksGame % 205 != 0
                || !LeadYourPetUtility.IsDepartureToil(__instance.lord, __instance))
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
