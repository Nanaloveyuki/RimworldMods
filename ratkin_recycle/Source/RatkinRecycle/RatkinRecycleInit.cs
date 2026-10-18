// Decompiled from the original RatkinRecycle.dll; Harmony ID changed by Nanaloveyuki on 2026-10-18.
using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace RatkinRecycle;

[StaticConstructorOnStartup]
public static class RatkinRecycleInit
{
	static RatkinRecycleInit()
	{
		try
		{
			new Harmony("nanaloveyuki.ratkinrecycle").PatchAll(Assembly.GetExecutingAssembly());
			RatkinRecycleUtility.ApplyMoodValues();
			Log.Message("[RatkinRecycle] 鼠族回收行为已加载。");
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] 初始化失败: " + ex);
		}
	}
}
