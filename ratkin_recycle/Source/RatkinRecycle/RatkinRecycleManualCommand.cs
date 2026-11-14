using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RatkinRecycle;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class RatkinRecycleManualCommand
{
	private static string T(string chinese, string english) => LanguageDatabase.activeLanguage?.folderName?.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase) == true ? chinese : english;

	private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
	{
		foreach (Gizmo gizmo in __result) yield return gizmo;
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null || !settings.enabled || !settings.manualEnabled || !RatkinRecycleUtility.IsRatkin(__instance)
			|| !__instance.Spawned || !__instance.CanTakeOrder)
			yield break;
		Command_Target command = new Command_Target
		{
			defaultLabel = T("触发鼠族回收", "Trigger Ratkin recycling"),
			defaultDesc = T("指定另一名鼠族并主动回收。双方年龄、身份、幼鼠分界、禁用和食物检查均不限制主动回收。捕食者须具备移动和操作能力。", "Choose another Ratkin for manual recycling. Ages, statuses, young-predator rules, forbidden targets and food checks do not restrict manual orders. The eater must be capable of moving and manipulation."),
			icon = TexCommand.AttackMelee,
			groupable = false,
			targetingParams = new TargetingParameters
			{
				canTargetBuildings = false,
				canTargetMechs = false,
				validator = target => target.Thing is Pawn victim && CanSelect(__instance, victim)
			},
			action = target => IssueOrder(__instance, target.Pawn)
		};
		AcceptanceReport readiness = CanIssue(__instance);
		if (!readiness.Accepted) command.Disable(readiness.Reason);
		yield return command;
	}

	internal static AcceptanceReport CanIssue(Pawn pawn)
	{
		if (!RatkinRecycleUtility.CanEat(pawn, true))
			return T("当前无法执行主动回收。", "This pawn cannot perform manual recycling now.");
		if (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null)
			return T("请先放下正在携带的物品或角色。", "Put down the carried item or pawn first.");
		if (pawn.CurJobDef == RatkinRecycleDefOf.EatRatEgg || pawn.CurJobDef == RatkinRecycleDefOf.ManualRecycleRatkinYoung)
			return T("正在执行回收。", "Already recycling.");
		return true;
	}

	internal static bool CanSelect(Pawn eater, Pawn victim)
	{
		return CanIssue(eater).Accepted
			&& RatkinRecycleUtility.CanTarget(eater, victim, RatkinRecycleUtility.GetEaterKind(eater, true), true)
			&& eater.CanReserve(victim) && eater.CanReach(victim, PathEndMode.Touch, Danger.Deadly);
	}

	private static void IssueOrder(Pawn eater, Pawn victim)
	{
		if (!CanSelect(eater, victim))
		{
			Messages.Message(T("无法回收该目标；请检查捕食者能力、目标可达性和预约。", "Cannot recycle this target. Check eater capacities, reachability and reservations."), eater, MessageTypeDefOf.RejectInput, historical: false);
			return;
		}
		Job job = JobMaker.MakeJob(RatkinRecycleDefOf.ManualRecycleRatkinYoung, victim);
		if (!eater.jobs.TryTakeOrderedJob(job))
			Messages.Message(T("无法接受回收命令。", "The recycling order could not be accepted."), eater, MessageTypeDefOf.RejectInput, historical: false);
	}
}
