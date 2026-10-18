using System;
using HarmonyLib;
using Verse;

namespace RatkinRecycle;

[HarmonyPatch(typeof(Thing), "Ingested")]
public static class Patch_Thing_Ingested
{
	private static void Postfix(Thing __instance, Pawn ingester)
	{
		try
		{
			if (ingester != null && RatkinRecycleUtility.IsRatkin(ingester) && RatkinRecycleUtility.IsFineOrBetter(__instance))
			{
				RatkinRecycleTracker.NotifyFineMeal(ingester, Find.TickManager.TicksGame);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] 进食补丁异常: " + ex);
		}
	}
}
