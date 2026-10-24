using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RatkinRecycle;

public class CompAutoStopBleeding : ThingComp
{
	// NumCellsInRadius rejects the maximum itself, so leave a small float-safe margin.
	public static readonly float MaximumRadius = Mathf.Max(0f, GenRadial.MaxRadialPatternRadius - 0.001f);

	public static float EffectiveRadius(float radius)
	{
		if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
		{
			return 0f;
		}
		return Mathf.Min(radius, MaximumRadius);
	}

	internal static void DrawRange(IntVec3 center)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null || !settings.bleedStationEnabled)
		{
			return;
		}
		float radius = EffectiveRadius(settings.bleedStationRadius);
		if (radius > 0f)
		{
			GenDraw.DrawRadiusRing(center, radius);
		}
	}

	public override void PostDrawExtraSelectionOverlays()
	{
		base.PostDrawExtraSelectionOverlays();
		if (parent.Spawned && !parent.Destroyed)
		{
			DrawRange(parent.Position);
		}
	}

	private int nextTick;

	private int patientsCount;

	public override void CompTick()
	{
		base.CompTick();
		if (!parent.Spawned || parent.Destroyed)
		{
			return;
		}
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null || !settings.bleedStationEnabled)
		{
			return;
		}
		int ticksGame = Find.TickManager.TicksGame;
		int num = Mathf.Max(20, settings.bleedStationInterval);
		if (ticksGame < nextTick)
		{
			return;
		}
		nextTick = ticksGame + num;
		float radius = EffectiveRadius(settings.bleedStationRadius);
		if (radius <= 0f)
		{
			patientsCount = 0;
			return;
		}
		Map map = parent.Map;
		if (map == null)
		{
			return;
		}
		int num2 = 0;
		foreach (Thing item in GenRadial.RadialDistinctThingsAround(parent.Position, map, radius, useCenter: true))
		{
			if (!(item is Pawn { Dead: false, Spawned: not false, RaceProps: not null } pawn) || !pawn.RaceProps.IsFlesh)
			{
				continue;
			}
			if (pawn.RaceProps.Humanlike)
			{
				if (!StationFactionAllowed(pawn, settings))
				{
					continue;
				}
			}
			else if (!settings.bleedStationHealAnimals)
			{
				continue;
			}
			if (HealBleeding(pawn))
			{
				num2++;
			}
		}
		patientsCount = num2;
		if (settings.debugLog && num2 > 0)
		{
			Log.Message($"[RatkinRecycle] 止血装置 {parent.Label} 治疗了 {num2} 名伤员(范围内流血者)");
		}
	}

	public override string CompInspectStringExtra()
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null || !settings.bleedStationEnabled)
		{
			return "自动止血装置(已停用)";
		}
		string text = "自动止血:范围 " + EffectiveRadius(settings.bleedStationRadius).ToString("0.#") + " 格,每 " + Mathf.Max(20, settings.bleedStationInterval) + " tick 治疗一次";
		if (patientsCount > 0)
		{
			text = text + "\n正在治疗 " + patientsCount + " 名流血伤员";
		}
		return text;
	}

	private static bool StationFactionAllowed(Pawn p, RatkinRecycleSettings S)
	{
		if (p.Faction == null)
		{
			return S.bleedStationHealWild;
		}
		Faction ofPlayer = Faction.OfPlayer;
		if (p.Faction == ofPlayer)
		{
			return S.bleedStationHealFriendly;
		}
		if (ofPlayer != null && p.Faction.HostileTo(ofPlayer))
		{
			return S.bleedStationHealHostile;
		}
		return S.bleedStationHealFriendly;
	}

	private bool HealBleeding(Pawn p)
	{
		if (p.health?.hediffSet == null)
		{
			return false;
		}
		if (p.health.hediffSet.BleedRateTotal <= 0f)
		{
			return false;
		}
		float num = Mathf.Max(0.001f, RatkinRecycleMod.Settings.bleedStationHeal);
		List<Hediff> hediffs = p.health.hediffSet.hediffs;
		bool flag = false;
		for (int num2 = hediffs.Count - 1; num2 >= 0; num2--)
		{
			Hediff hediff = hediffs[num2];
			if (hediff is Hediff_Injury hediff_Injury && hediff_Injury.def.injuryProps != null && hediff_Injury.def.injuryProps.bleedRate > 0f)
			{
				hediff_Injury.Severity -= num;
				flag = true;
				if (hediff_Injury.Severity <= 0f)
				{
					p.health.RemoveHediff(hediff_Injury);
				}
			}
			else if (hediff is Hediff_MissingPart { IsFresh: not false } hediff_MissingPart && hediff_MissingPart.def.injuryProps != null && hediff_MissingPart.def.injuryProps.bleedRate > 0f)
			{
				hediff_MissingPart.IsFresh = false;
				flag = true;
			}
		}
		if (flag)
		{
			p.health.hediffSet.DirtyCache();
			if (p.Spawned && p.Map != null)
			{
				FleckMaker.ThrowMetaIcon(p.Position, p.Map, FleckDefOf.HealingCross);
			}
		}
		return flag;
	}
}
