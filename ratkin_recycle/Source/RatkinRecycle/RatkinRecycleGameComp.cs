using System;
using UnityEngine;
using Verse;

namespace RatkinRecycle;

public class RatkinRecycleGameComp : GameComponent
{
	private int counter;

	public RatkinRecycleGameComp()
	{
	}

	public RatkinRecycleGameComp(Game game)
	{
	}

	public override void GameComponentTick()
	{
		try
		{
			if (Find.TickManager != null && !Find.TickManager.Paused && Current.ProgramState == ProgramState.Playing)
			{
				RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
				if (settings != null && settings.enabled && --counter <= 0)
				{
					counter = Mathf.Max(10, settings.checkInterval);
					RatkinRecycleUtility.TickScan();
					RatkinRecycleTracker.PurgeDead();
				}
			}
		}
		catch (Exception ex)
		{
			Log.ErrorOnce("[RatkinRecycle] 扫描异常: " + ex, 14334751);
		}
	}
}
