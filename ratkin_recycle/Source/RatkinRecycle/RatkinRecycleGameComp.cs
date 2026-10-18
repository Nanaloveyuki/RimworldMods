using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RatkinRecycle;

public class RatkinRecycleGameComp : GameComponent
{
	private const int CleanupInterval = 2500;

	private readonly Game game;
	private Dictionary<Pawn, long> lastFineMealTick = new Dictionary<Pawn, long>();
	private Dictionary<Pawn, long> lastBirthTick = new Dictionary<Pawn, long>();
	private Dictionary<Pawn, long> nextAttemptTick = new Dictionary<Pawn, long>();
	private List<Pawn> fineMealKeys;
	private List<long> fineMealValues;
	private List<Pawn> birthKeys;
	private List<long> birthValues;
	private List<Pawn> cooldownKeys;
	private List<long> cooldownValues;
	private readonly List<Pawn> purgeBuffer = new List<Pawn>();
	private int counter;
	private int cleanupCounter;

	public static RatkinRecycleGameComp Current => Verse.Current.Game?.GetComponent<RatkinRecycleGameComp>();

	public RatkinRecycleGameComp(Game game)
	{
		this.game = game;
	}

	public override void ExposeData()
	{
		if (Scribe.mode == LoadSaveMode.Saving)
		{
			PurgeDead();
		}
		// Reference keys resolve in a later Scribe pass, so each dictionary needs retained working lists.
		Scribe_Collections.Look(ref lastFineMealTick, "lastFineMealTick", LookMode.Reference, LookMode.Value, ref fineMealKeys, ref fineMealValues, logNullErrors: false);
		Scribe_Collections.Look(ref lastBirthTick, "lastBirthTick", LookMode.Reference, LookMode.Value, ref birthKeys, ref birthValues, logNullErrors: false);
		Scribe_Collections.Look(ref nextAttemptTick, "nextAttemptTick", LookMode.Reference, LookMode.Value, ref cooldownKeys, ref cooldownValues, logNullErrors: false);
		lastFineMealTick ??= new Dictionary<Pawn, long>();
		lastBirthTick ??= new Dictionary<Pawn, long>();
		nextAttemptTick ??= new Dictionary<Pawn, long>();
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			PurgeDead();
		}
	}

	public void NotifyFineMeal(Pawn pawn, long tick)
	{
		if (pawn != null && !pawn.Destroyed && !pawn.Dead && tick >= 0)
		{
			lastFineMealTick[pawn] = tick;
		}
	}

	public void NotifyBirth(Pawn mother, long tick)
	{
		if (mother != null && !mother.Destroyed && !mother.Dead && tick >= 0)
		{
			lastBirthTick[mother] = tick;
			// A meal before this delivery does not satisfy the new postpartum meal requirement.
			lastFineMealTick.Remove(mother);
		}
	}

	public long GetLastFineMealTick(Pawn pawn)
	{
		return pawn != null && lastFineMealTick.TryGetValue(pawn, out long tick) ? tick : -1L;
	}

	public long GetLastBirthTick(Pawn pawn)
	{
		return pawn != null && lastBirthTick.TryGetValue(pawn, out long tick) ? tick : -1L;
	}

	public bool HasGaveBirthRecently(Pawn pawn, float hours)
	{
		long birthTick = GetLastBirthTick(pawn);
		TickManager tickManager = game?.tickManager;
		if (birthTick < 0 || tickManager == null || !(hours >= 0f))
		{
			return false;
		}
		long elapsed = (long)tickManager.TicksGame - birthTick;
		return elapsed >= 0 && elapsed <= hours * 2500d;
	}

	public bool OnCooldown(Pawn pawn, int tick)
	{
		return pawn != null && nextAttemptTick.TryGetValue(pawn, out long untilTick) && tick < untilTick;
	}

	public void SetCooldown(Pawn pawn, long tick)
	{
		if (pawn != null && !pawn.Destroyed && !pawn.Dead && tick >= 0)
		{
			nextAttemptTick[pawn] = tick;
		}
	}

	public void PurgeDead()
	{
		long now = game?.tickManager?.TicksGame ?? -1;
		PurgeDictionary(lastFineMealTick, -1L);
		PurgeDictionary(lastBirthTick, -1L);
		PurgeDictionary(nextAttemptTick, now);
	}

	private void PurgeDictionary(Dictionary<Pawn, long> history, long expiredAt)
	{
		purgeBuffer.Clear();
		foreach (KeyValuePair<Pawn, long> entry in history)
		{
			Pawn pawn = entry.Key;
			// Unspawned living pawns include caravans and world mothers; keep their maternal history.
			if (pawn == null || pawn.Destroyed || pawn.Dead || (expiredAt >= 0 && entry.Value <= expiredAt))
			{
				purgeBuffer.Add(pawn);
			}
		}
		for (int i = 0; i < purgeBuffer.Count; i++)
		{
			history.Remove(purgeBuffer[i]);
		}
		purgeBuffer.Clear();
	}

	public override void GameComponentTick()
	{
		TickManager tickManager = game?.tickManager;
		if (tickManager == null || tickManager.Paused || Verse.Current.ProgramState != ProgramState.Playing)
		{
			return;
		}
		if (--cleanupCounter <= 0)
		{
			cleanupCounter = CleanupInterval;
			PurgeDead();
		}
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings != null && settings.enabled && --counter <= 0)
		{
			counter = Mathf.Max(10, settings.checkInterval);
			RatkinRecycleUtility.TickScan();
		}
	}
}
