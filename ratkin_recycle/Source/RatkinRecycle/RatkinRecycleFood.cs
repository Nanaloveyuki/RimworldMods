using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RatkinRecycle;

internal static class RatkinRecycleFood
{
	public static bool HasAvailableFood(Pawn pawn)
	{
		return TryFindFood(pawn, out _, out _);
	}

	public static Job MakeFoodJob(Pawn pawn, Thing source, ThingDef foodDef)
	{
		if (!Toils_Ingest.TryFindChairOrSpot(pawn, source, out _)) return null;
		Pawn holder = (source.ParentHolder as Pawn_InventoryTracker)?.pawn;
		Job job = holder != null && holder != pawn
			? JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, source, holder)
			: JobMaker.MakeJob(JobDefOf.Ingest, source);
		job.count = FoodUtility.WillIngestStackCountOf(pawn, foodDef, FoodUtility.GetNutrition(pawn, source, foodDef));
		return job;
	}

	public static bool TryFindFood(Pawn pawn, out Thing source, out ThingDef foodDef)
	{
		source = null;
		foodDef = null;
		if (RatkinRecycleMod.Settings?.preferAvailableFood != true || pawn?.Spawned != true || pawn.needs?.food == null) return false;
		bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;
		if (FoodUtility.TryFindBestFoodSourceFor(pawn, pawn, desperate, out source, out foodDef,
			canRefillDispenser: false, canUseInventory: true, canUsePackAnimalInventory: true,
			allowForbidden: false, allowCorpse: false, allowSociallyImproper: false, allowHarvest: false)
			&& source != null && !(source is Pawn) && !(source is Corpse)
			&& foodDef?.IsNutritionGivingIngestible == true && FoodUtility.GetNutrition(pawn, source, foodDef) > 0f)
		{
			return true;
		}
		// Vanilla's pack-inventory fallback only searches colony animals, not visiting caravans.
		Lord lord = pawn.GetLord();
		if (lord != null && pawn.RaceProps.ToolUser && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn carrier = lord.ownedPawns[i];
				if (carrier == pawn || !carrier.Spawned || carrier.Map != pawn.Map || carrier.Faction != pawn.Faction
					|| !carrier.RaceProps.packAnimal || carrier.IsForbidden(pawn)) continue;
				Thing food = FoodUtility.BestFoodInInventory(carrier, pawn, FoodPreferability.DesperateOnly);
				if (food == null || food is Corpse || FoodUtility.GetNutrition(pawn, food, food.def) <= 0f
					|| !pawn.CanReach(carrier, PathEndMode.Touch, Danger.Some)) continue;
				source = food;
				foodDef = food.def;
				return true;
			}
		}
		source = null;
		foodDef = null;
		return false;
	}
}
