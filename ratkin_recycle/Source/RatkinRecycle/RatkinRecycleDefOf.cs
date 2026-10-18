using RimWorld;
using Verse;

namespace RatkinRecycle;

[DefOf]
public static class RatkinRecycleDefOf
{
	public static JobDef EatRatEgg;

	public static ThoughtDef MomEatNonOwnEggToddler;

	public static ThoughtDef MomEatNonOwnEggChild;

	public static ThoughtDef MomEatNonOwnEggAdult;

	public static ThoughtDef MomEatOwnEggToddler;

	public static ThoughtDef MomEatOwnEggChild;

	public static ThoughtDef MomEatOwnEggAdult;

	public static ThoughtDef OtherEatEggToddler;

	public static ThoughtDef OtherEatEggChild;

	public static ThoughtDef OtherEatEggAdult;

	public static ThoughtDef EatenEggToddler;

	public static ThoughtDef EatenEggChild;

	public static ThoughtDef EatenEggAdult;

	static RatkinRecycleDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(RatkinRecycleDefOf));
	}
}
