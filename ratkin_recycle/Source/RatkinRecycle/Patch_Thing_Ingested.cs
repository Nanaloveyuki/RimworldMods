using HarmonyLib;
using Verse;

namespace RatkinRecycle;

[HarmonyPatch(typeof(Thing), "Ingested")]
public static class Patch_Thing_Ingested
{
	private static void Postfix(Thing __instance, Pawn ingester, float __result)
	{
		// Ingested returns actual nutrition, including zero for rejected/destroyed food.
		if (!(__result > 0f) || ingester == null || !RatkinRecycleUtility.IsRatkin(ingester) ||
			!RatkinRecycleUtility.IsFineOrBetter(__instance))
		{
			return;
		}
		TickManager tickManager = Verse.Current.Game?.tickManager;
		if (tickManager != null)
		{
			RatkinRecycleGameComp.Current?.NotifyFineMeal(ingester, tickManager.TicksGame);
		}
	}
}
