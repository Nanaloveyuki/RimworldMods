using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RatkinRecycle;

[HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
public static class Patch_NeedsTracker_ShouldHaveNeed
{
	private static readonly FieldInfo pawnField = AccessTools.Field(typeof(Pawn_NeedsTracker), "pawn");

	private static void Postfix(Pawn_NeedsTracker __instance, NeedDef nd, ref bool __result)
	{
		try
		{
			if (__result)
			{
				return;
			}
			NeedDef moodNeed = RatkinRecycleUtility.MoodNeed;
			if (moodNeed == null || nd != moodNeed)
			{
				return;
			}
			RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
			if (settings != null && settings.babyMoodNeed)
			{
				Pawn pawn = (Pawn)pawnField.GetValue(__instance);
				if (pawn != null && RatkinRecycleUtility.IsRatkin(pawn) && !(RatkinRecycleUtility.AgeYears(pawn) >= settings.maxEggAge))
				{
					__result = true;
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] ShouldHaveNeed 补丁异常: " + ex);
		}
	}
}
