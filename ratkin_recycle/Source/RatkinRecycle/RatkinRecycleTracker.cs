using System.Collections.Generic;
using Verse;

namespace RatkinRecycle;

public static class RatkinRecycleTracker
{
	private static readonly Dictionary<Pawn, long> lastFineMealTick = new Dictionary<Pawn, long>();

	private static readonly Dictionary<Pawn, long> lastBirthTick = new Dictionary<Pawn, long>();

	private static readonly Dictionary<Pawn, long> nextAttemptTick = new Dictionary<Pawn, long>();

	private static int purgeCounter;

	public static void NotifyFineMeal(Pawn p, long tick)
	{
		if (p != null)
		{
			lastFineMealTick[p] = tick;
		}
	}

	public static void NotifyBirth(Pawn mother, long tick)
	{
		if (mother != null)
		{
			lastBirthTick[mother] = tick;
			lastFineMealTick[mother] = 0L;
		}
	}

	public static long GetLastFineMealTick(Pawn p)
	{
		if (p == null || !lastFineMealTick.TryGetValue(p, out var value))
		{
			return 0L;
		}
		return value;
	}

	public static long GetLastBirthTick(Pawn p)
	{
		if (p == null || !lastBirthTick.TryGetValue(p, out var value))
		{
			return 0L;
		}
		return value;
	}

	public static bool HasGaveBirthRecently(Pawn p, float hours)
	{
		if (p == null || !lastBirthTick.TryGetValue(p, out var value))
		{
			return false;
		}
		long num = (long)(hours * 2500f);
		return Find.TickManager.TicksGame - value <= num;
	}

	public static bool OnCooldown(Pawn p, int tick)
	{
		if (p != null && nextAttemptTick.TryGetValue(p, out var value))
		{
			return tick < value;
		}
		return false;
	}

	public static void SetCooldown(Pawn p, long tick)
	{
		if (p != null)
		{
			nextAttemptTick[p] = tick;
		}
	}

	public static void PurgeDead()
	{
		if (++purgeCounter >= 200)
		{
			purgeCounter = 0;
			PurgeDict(lastFineMealTick);
			PurgeDict(lastBirthTick);
			PurgeDict(nextAttemptTick);
		}
	}

	private static void PurgeDict(Dictionary<Pawn, long> dict)
	{
		if (dict.Count == 0)
		{
			return;
		}
		List<Pawn> list = null;
		foreach (Pawn key in dict.Keys)
		{
			if (key == null || key.Destroyed || key.Dead)
			{
				(list ?? (list = new List<Pawn>())).Add(key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				dict.Remove(list[i]);
			}
		}
	}
}
