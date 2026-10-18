using HarmonyLib;
using RimWorld;
using Verse;

namespace RatkinRecycle;

[HarmonyPatch(typeof(PregnancyUtility), "ApplyBirthOutcome")]
public static class Patch_PregnancyUtility_ApplyBirthOutcome
{
	private static void Postfix(RitualOutcomePossibility outcome, Thing birtherThing, Thing __result)
	{
		// Vanilla returns a newborn for live birth, or its corpse for stillbirth (-1).
		// Genetic mothers can be donors to a surrogate or a vat; only a pawn birther is postpartum.
		if (!(birtherThing is Pawn mother) || outcome == null || outcome.positivityIndex < -1 ||
			!(outcome.positivityIndex >= 0 ? __result is Pawn : __result is Corpse corpse && corpse.InnerPawn != null) ||
			!RatkinRecycleUtility.IsRatkin(mother))
		{
			return;
		}
		RatkinRecycleGameComp history = RatkinRecycleGameComp.Current;
		TickManager tickManager = Verse.Current.Game?.tickManager;
		if (history == null || tickManager == null)
		{
			return;
		}
		history.NotifyBirth(mother, tickManager.TicksGame);
		if (RatkinRecycleMod.Settings?.debugLog == true)
		{
			Log.Message("[RatkinRecycle] " + mother.LabelShort + " 生产了,进入鼠妈状态。");
		}
	}
}
