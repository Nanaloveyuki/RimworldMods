using System;
using UnityEngine;
using Verse;

namespace RatkinRecycle;

public class RatkinRecycleMod : Mod
{
	public static RatkinRecycleMod Instance;

	public static RatkinRecycleSettings Settings;

	public RatkinRecycleMod(ModContentPack content)
		: base(content)
	{
		Instance = this;
		Settings = GetSettings<RatkinRecycleSettings>();
		Settings.Normalize();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		try
		{
			if (Settings == null)
			{
				Log.Warning("[RatkinRecycle] 设置尚未初始化,跳过绘制。");
			}
			else
			{
				Settings.DoWindowContents(inRect);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] 设置窗口异常: " + ex);
		}
	}

	public override string SettingsCategory()
	{
		return "鼠族回收行为 RatkinRecycle";
	}

	public override void WriteSettings()
	{
		Settings?.Normalize();
		base.WriteSettings();
		try
		{
			RatkinRecycleUtility.ApplyMoodValues();
			RatkinRecycleUtility.InvalidateCaches();
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] WriteSettings 异常: " + ex);
		}
	}
}
