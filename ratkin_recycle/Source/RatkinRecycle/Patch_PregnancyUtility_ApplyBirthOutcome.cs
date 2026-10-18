using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RatkinRecycle;

[HarmonyPatch(typeof(PregnancyUtility), "ApplyBirthOutcome")]
public static class Patch_PregnancyUtility_ApplyBirthOutcome
{
	private static void Postfix(Pawn geneticMother, Thing birtherThing)
	{
		try
		{
			Pawn pawn = geneticMother ?? (birtherThing as Pawn);
			if (pawn != null)
			{
				RatkinRecycleTracker.NotifyBirth(pawn, Find.TickManager.TicksGame);
				if (RatkinRecycleMod.Settings != null && RatkinRecycleMod.Settings.debugLog)
				{
					Log.Message("[RatkinRecycle] " + pawn.LabelShort + " 生产了,进入鼠妈状态。");
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] 出生补丁异常: " + ex);
		}
	}
}
