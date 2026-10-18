using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinRecycle;

public static class RatkinRecycleUtility
{
	private static List<string> raceKeywordsCache;

	private static int raceKeywordsHash = int.MinValue;

	private static readonly List<Pawn> eggBuffer = new List<Pawn>();

	private static readonly List<Pawn> eaterBuffer = new List<Pawn>();

	private static NeedDef cachedMoodNeed;

	private static int ensureCounter;

	public static NeedDef MoodNeed
	{
		get
		{
			if (cachedMoodNeed == null)
			{
				cachedMoodNeed = DefDatabase<NeedDef>.GetNamedSilentFail("Mood");
			}
			return cachedMoodNeed;
		}
	}

	public static void InvalidateCaches()
	{
		raceKeywordsHash = int.MinValue;
	}

	public static void ApplyMoodValues()
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings != null)
		{
			TrySetMood(RatkinRecycleDefOf.MomEatNonOwnEggToddler, settings.momNonOwnToddler);
			TrySetMood(RatkinRecycleDefOf.MomEatNonOwnEggChild, settings.momNonOwnChild);
			TrySetMood(RatkinRecycleDefOf.MomEatNonOwnEggAdult, settings.momNonOwnAdult);
			TrySetMood(RatkinRecycleDefOf.MomEatOwnEggToddler, settings.momOwnToddler);
			TrySetMood(RatkinRecycleDefOf.MomEatOwnEggChild, settings.momOwnChild);
			TrySetMood(RatkinRecycleDefOf.MomEatOwnEggAdult, settings.momOwnAdult);
			TrySetMood(RatkinRecycleDefOf.OtherEatEggToddler, settings.otherToddler);
			TrySetMood(RatkinRecycleDefOf.OtherEatEggChild, settings.otherChild);
			TrySetMood(RatkinRecycleDefOf.OtherEatEggAdult, settings.otherAdult);
			TrySetMood(RatkinRecycleDefOf.EatenEggToddler, settings.eatenToddler);
			TrySetMood(RatkinRecycleDefOf.EatenEggChild, settings.eatenChild);
			TrySetMood(RatkinRecycleDefOf.EatenEggAdult, settings.eatenAdult);
		}
	}

	private static void TrySetMood(ThoughtDef def, float value)
	{
		if (def != null && def.stages != null && def.stages.Count != 0)
		{
			def.stages[0].baseMoodEffect = value;
		}
	}

	public static bool IsRatkin(Pawn p)
	{
		if (p == null || p.def == null || !p.RaceProps.Humanlike)
		{
			return false;
		}
		string defName = p.def.defName;
		foreach (string item in RaceKeywords())
		{
			if (item.Length > 0 && defName.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	private static IEnumerable<string> RaceKeywords()
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null)
		{
			return Array.Empty<string>();
		}
		int hashCode = (settings.raceKeywords ?? string.Empty).GetHashCode();
		if (raceKeywordsHash != hashCode)
		{
			raceKeywordsHash = hashCode;
			raceKeywordsCache = (from x in (settings.raceKeywords ?? "Ratkin").Split(new char[4] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
				select x.Trim() into x
				where x.Length > 0
				select x).ToList();
		}
		return raceKeywordsCache;
	}

	public static float AgeYears(Pawn p)
	{
		if (p?.ageTracker == null)
		{
			return 0f;
		}
		return (float)p.ageTracker.AgeBiologicalTicks / 60000f;
	}

	public static int GrowthTier(Pawn p)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		float num = AgeYears(p);
		if (num < settings.tierToddlerMax)
		{
			return 0;
		}
		if (num < settings.tierChildMax)
		{
			return 1;
		}
		return 2;
	}

	public static bool IsMom(Pawn p)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (p == null || p.gender != Gender.Female || p.ageTracker == null)
		{
			return false;
		}
		if (settings.adultAgeYears > 0f && AgeYears(p) < settings.adultAgeYears)
		{
			return false;
		}
		if (RatkinRecycleTracker.HasGaveBirthRecently(p, settings.postpartumHours))
		{
			return true;
		}
		if (settings.momOwnEggFallback && p.relations != null)
		{
			foreach (Pawn child in p.relations.Children)
			{
				if (child != null && !child.Dead && IsRatkin(child) && AgeYears(child) < settings.maxEggAge)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool MomFoodEligible(Pawn p)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		long lastFineMealTick = RatkinRecycleTracker.GetLastFineMealTick(p);
		long lastBirthTick = RatkinRecycleTracker.GetLastBirthTick(p);
		if ((float)settings.fineMealMode <= 0f)
		{
			return lastFineMealTick < lastBirthTick;
		}
		long num = (long)(Mathf.Max(1f, settings.fineMealHours) * 2500f);
		if (lastFineMealTick > 0)
		{
			return Find.TickManager.TicksGame - lastFineMealTick > num;
		}
		return true;
	}

	public static EaterKind GetEaterKind(Pawn p)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (!IsRatkin(p) || p.Dead || p.Downed)
		{
			return EaterKind.None;
		}
		if (p.needs?.food == null)
		{
			return EaterKind.None;
		}
		if (AgeYears(p) < settings.minEaterAge)
		{
			return EaterKind.None;
		}
		if (IsMom(p))
		{
			if (!MomFoodEligible(p))
			{
				return EaterKind.None;
			}
			return EaterKind.Mom;
		}
		if (AgeYears(p) < settings.maxEggAge)
		{
			return EaterKind.Egg;
		}
		return EaterKind.Other;
	}

	public static bool IsHungryEnough(Pawn p, EaterKind kind)
	{
		if (p.needs?.food == null)
		{
			return false;
		}
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		float num = ((kind == EaterKind.Mom) ? settings.momHungerPct : settings.otherHungerPct);
		return p.needs.food.CurLevelPercentage < num;
	}

	private static bool FactionAllowed(Faction f, Faction player, bool allowPlayer, bool allowHostile, bool allowFriendly)
	{
		if (f == player)
		{
			return allowPlayer;
		}
		if (player != null && f.HostileTo(player))
		{
			return allowHostile;
		}
		return allowFriendly;
	}

	public static bool IsOwnEgg(Pawn eater, Pawn victim)
	{
		if (eater == null || victim == null || victim.relations == null)
		{
			return false;
		}
		try
		{
			if (victim.GetMother() == eater)
			{
				return true;
			}
		}
		catch (Exception)
		{
		}
		if (eater.relations != null)
		{
			foreach (Pawn child in eater.relations.Children)
			{
				if (child == victim)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static Pawn TryFindTargetFromEggs(Pawn eater, EaterKind kind, List<Pawn> eggs)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (eggs == null || eggs.Count == 0)
		{
			return null;
		}
		float num = settings.maxEggAge;
		if (kind == EaterKind.Egg)
		{
			num = Mathf.Min(settings.splitEggAge, settings.maxEggAge);
		}
		Pawn pawn = null;
		Pawn pawn2 = null;
		Pawn result = null;
		float num2 = float.MaxValue;
		float num3 = float.MaxValue;
		float num4 = float.MaxValue;
		float num5 = settings.searchRadius * settings.searchRadius;
		for (int i = 0; i < eggs.Count; i++)
		{
			Pawn pawn3 = eggs[i];
			if (pawn3 == eater || AgeYears(pawn3) >= num)
			{
				continue;
			}
			float num6 = pawn3.Position.x - eater.Position.x;
			float num7 = pawn3.Position.z - eater.Position.z;
			float num8 = num6 * num6 + num7 * num7;
			if (num8 > num5 || !eater.CanReach(pawn3, PathEndMode.Touch, Danger.Deadly))
			{
				continue;
			}
			float num9 = Mathf.Sqrt(num8);
			if (kind == EaterKind.Mom)
			{
				if (IsOwnEgg(eater, pawn3))
				{
					if (num9 < num3)
					{
						num3 = num9;
						pawn2 = pawn3;
					}
				}
				else if (num9 < num2)
				{
					num2 = num9;
					pawn = pawn3;
				}
			}
			else if (num9 < num4)
			{
				num4 = num9;
				result = pawn3;
			}
		}
		if (kind == EaterKind.Mom)
		{
			return pawn ?? pawn2;
		}
		return result;
	}

	public static void TickScan()
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null || !settings.enabled)
		{
			return;
		}
		int ticksGame = Find.TickManager.TicksGame;
		float maxEggAge = settings.maxEggAge;
		bool skipCarriedTarget = settings.skipCarriedTarget;
		bool skipDrafted = settings.skipDrafted;
		bool skipMentalState = settings.skipMentalState;
		bool allowNoFaction = settings.allowNoFaction;
		Faction ofPlayer = Faction.OfPlayer;
		bool allowPlayerFaction = settings.allowPlayerFaction;
		bool allowHostileFaction = settings.allowHostileFaction;
		bool allowFriendlyFaction = settings.allowFriendlyFaction;
		int retryCooldown = settings.retryCooldown;
		float num = Mathf.Max(settings.momHungerPct, settings.otherHungerPct);
		bool flag = settings.babyMoodNeed && MoodNeed != null && ++ensureCounter % 60 == 0;
		for (int i = 0; i < Find.Maps.Count; i++)
		{
			Map map = Find.Maps[i];
			if (map == null || map.mapPawns == null)
			{
				continue;
			}
			eggBuffer.Clear();
			eaterBuffer.Clear();
			IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
			for (int j = 0; j < allPawnsSpawned.Count; j++)
			{
				Pawn pawn = allPawnsSpawned[j];
				if (pawn != null && !pawn.Dead && !pawn.Downed && pawn.Spawned && IsRatkin(pawn))
				{
					if (AgeYears(pawn) < maxEggAge && (!skipCarriedTarget || !(pawn.ParentHolder is Pawn_CarryTracker)))
					{
						eggBuffer.Add(pawn);
					}
					if (pawn.needs?.food != null && !(pawn.needs.food.CurLevelPercentage >= num) && !((pawn.Faction == null) ? (!allowNoFaction) : (!FactionAllowed(pawn.Faction, ofPlayer, allowPlayerFaction, allowHostileFaction, allowFriendlyFaction))) && (!skipDrafted || !pawn.Drafted) && (!skipMentalState || !pawn.InMentalState))
					{
						eaterBuffer.Add(pawn);
					}
				}
			}
			if (eggBuffer.Count == 0)
			{
				continue;
			}
			if (flag)
			{
				for (int k = 0; k < eggBuffer.Count; k++)
				{
					Pawn pawn2 = eggBuffer[k];
					if (pawn2.needs != null && !pawn2.needs.TryGetNeed(MoodNeed, out var _))
					{
						try
						{
							pawn2.needs.AddOrRemoveNeedsAsAppropriate();
						}
						catch (Exception ex)
						{
							Log.ErrorOnce("[RatkinRecycle] AddOrRemoveNeedsAsAppropriate 异常: " + ex, 48865);
						}
					}
				}
			}
			for (int l = 0; l < eaterBuffer.Count; l++)
			{
				Pawn pawn3 = eaterBuffer[l];
				if (RatkinRecycleTracker.OnCooldown(pawn3, ticksGame))
				{
					continue;
				}
				RatkinRecycleTracker.SetCooldown(pawn3, ticksGame + retryCooldown);
				EaterKind eaterKind = GetEaterKind(pawn3);
				if (eaterKind == EaterKind.None || !IsHungryEnough(pawn3, eaterKind))
				{
					continue;
				}
				Pawn pawn4 = TryFindTargetFromEggs(pawn3, eaterKind, eggBuffer);
				if (pawn4 == null)
				{
					continue;
				}
				Job job = JobMaker.MakeJob(RatkinRecycleDefOf.EatRatEgg, pawn4);
				if (!pawn3.Reserve(pawn4, job, 1, -1, null, errorOnFailed: false))
				{
					continue;
				}
				try
				{
					pawn3.jobs.StartJob(job, JobCondition.InterruptForced);
					if (settings.debugLog)
					{
						Log.Message($"[RatkinRecycle] {pawn3.LabelShort}({eaterKind}) 开始捕食 {pawn4.LabelShort},年龄 {AgeYears(pawn4):0.##} 岁");
					}
				}
				catch (Exception ex2)
				{
					if (settings.debugLog)
					{
						Log.Warning("[RatkinRecycle] StartJob 失败: " + ex2);
					}
				}
			}
		}
	}

	public static BodyPartRecord PickBitePart(Pawn victim)
	{
		HediffSet hediffSet = victim?.health?.hediffSet;
		if (hediffSet == null)
		{
			return null;
		}
		List<BodyPartRecord> list = hediffSet.GetNotMissingParts().ToList();
		if (list.Count == 0)
		{
			return null;
		}
		if (!RatkinRecycleMod.Settings.limbsFirst)
		{
			return list.RandomElement();
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (IsExtremity(list[i]))
			{
				num++;
			}
			else if (IsHeadPart(list[i]))
			{
				num3++;
			}
			else
			{
				num2++;
			}
		}
		if (num > 0)
		{
			return RandomFromWhere(list, IsExtremity, num);
		}
		if (num2 > 0)
		{
			return RandomFromWhere(list, (BodyPartRecord p) => !IsExtremity(p) && !IsHeadPart(p), num2);
		}
		if (num3 > 0)
		{
			return RandomFromWhere(list, IsHeadPart, num3);
		}
		return list.RandomElement();
	}

	private static BodyPartRecord RandomFromWhere(List<BodyPartRecord> parts, Func<BodyPartRecord, bool> pred, int count)
	{
		int num = Rand.RangeInclusive(0, count - 1);
		for (int i = 0; i < parts.Count; i++)
		{
			if (pred(parts[i]))
			{
				if (num == 0)
				{
					return parts[i];
				}
				num--;
			}
		}
		return parts[0];
	}

	private static bool IsExtremity(BodyPartRecord part)
	{
		string text = part?.def?.defName ?? string.Empty;
		if (!text.Contains("Arm") && !text.Contains("Leg") && !text.Contains("Hand") && !text.Contains("Foot") && !text.Contains("Shoulder"))
		{
			return text.Contains("Tail");
		}
		return true;
	}

	private static bool IsHeadPart(BodyPartRecord part)
	{
		string text = part?.def?.defName ?? string.Empty;
		if (!text.Contains("Head") && !text.Contains("Brain") && !text.Contains("Skull") && !text.Contains("Eye") && !text.Contains("Ear") && !text.Contains("Jaw"))
		{
			return text.Contains("Nose");
		}
		return true;
	}

	public static void OnEggEaten(Pawn eater, Pawn victim, EaterKind kind, bool own, int tier)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null)
		{
			return;
		}
		try
		{
			GainEggThoughts(eater, victim, kind, own, tier);
			if (eater != null && !eater.Dead && eater.needs?.food != null)
			{
				eater.needs.food.CurLevel = Mathf.Min(eater.needs.food.MaxLevel, eater.needs.food.CurLevel + settings.nutritionOnFinish);
			}
			SendEatLetter(eater, victim, kind, own, tier, eatenFully: true);
			Map map = ((victim != null && victim.MapHeld != null) ? victim.MapHeld : eatMap(eater, victim));
			IntVec3 loc = ((victim != null && victim.Spawned) ? victim.Position : ((eater != null && eater.Spawned) ? eater.Position : IntVec3.Invalid));
			if (settings.destroyCorpse && victim != null)
			{
				Corpse corpse = victim.Corpse;
				if (corpse != null && !corpse.Destroyed)
				{
					corpse.Destroy();
				}
			}
			if (settings.leaveMeat && settings.meatCount > 0 && map != null && loc.IsValid)
			{
				ThingDef namedSilentFail = DefDatabase<ThingDef>.GetNamedSilentFail("Meat_Ratkin");
				if (namedSilentFail != null)
				{
					for (int i = 0; i < settings.meatCount; i++)
					{
						GenSpawn.Spawn(namedSilentFail, loc, map);
					}
				}
			}
			if (settings.debugLog)
			{
				Log.Message($"[RatkinRecycle] {eater?.LabelShort} 吃掉了 {victim?.LabelShort}(own={own}, tier={tier})");
			}
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] OnEggEaten 异常: " + ex);
		}
	}

	public static void OnEggPartiallyEaten(Pawn eater, Pawn victim, EaterKind kind, bool own, int tier)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null)
		{
			return;
		}
		try
		{
			GainEggThoughts(eater, victim, kind, own, tier);
			SendEatLetter(eater, victim, kind, own, tier, eatenFully: false);
			if (settings.debugLog)
			{
				Log.Message($"[RatkinRecycle] {eater?.LabelShort} 啃了几口吃饱停嘴,{victim?.LabelShort} 还活着(own={own}, tier={tier})");
			}
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] OnEggPartiallyEaten 异常: " + ex);
		}
	}

	public static void GainEggThoughts(Pawn eater, Pawn victim, EaterKind kind, bool own, int tier)
	{
		ThoughtDef eaterThought = GetEaterThought(kind, own, tier);
		ThoughtDef victimThought = GetVictimThought(tier);
		if (eater != null && eater.needs?.mood?.thoughts?.memories != null)
		{
			eater.needs.mood.thoughts.memories.TryGainMemory(eaterThought, victim);
		}
		if (victim != null && victim.needs?.mood?.thoughts?.memories != null)
		{
			victim.needs.mood.thoughts.memories.TryGainMemory(victimThought, eater);
		}
	}

	public static ThoughtDef GetEaterThought(EaterKind kind, bool own, int tier)
	{
		switch (tier)
		{
		case 0:
			if (kind != EaterKind.Mom)
			{
				return RatkinRecycleDefOf.OtherEatEggToddler;
			}
			if (!own)
			{
				return RatkinRecycleDefOf.MomEatNonOwnEggToddler;
			}
			return RatkinRecycleDefOf.MomEatOwnEggToddler;
		case 1:
			if (kind != EaterKind.Mom)
			{
				return RatkinRecycleDefOf.OtherEatEggChild;
			}
			if (!own)
			{
				return RatkinRecycleDefOf.MomEatNonOwnEggChild;
			}
			return RatkinRecycleDefOf.MomEatOwnEggChild;
		default:
			if (kind != EaterKind.Mom)
			{
				return RatkinRecycleDefOf.OtherEatEggAdult;
			}
			if (!own)
			{
				return RatkinRecycleDefOf.MomEatNonOwnEggAdult;
			}
			return RatkinRecycleDefOf.MomEatOwnEggAdult;
		}
	}

	public static ThoughtDef GetVictimThought(int tier)
	{
		return tier switch
		{
			0 => RatkinRecycleDefOf.EatenEggToddler, 
			1 => RatkinRecycleDefOf.EatenEggChild, 
			_ => RatkinRecycleDefOf.EatenEggAdult, 
		};
	}

	private static void SendEatLetter(Pawn eater, Pawn victim, EaterKind kind, bool own, int tier, bool eatenFully)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings != null && victim != null && (eatenFully ? settings.showLetter : settings.showStopLetter))
		{
			string text = ((eater != null) ? eater.LabelShort : "某只鼠鼠");
			string text2;
			string text3;
			if (eatenFully)
			{
				text2 = "鼠蛋被吃掉了吱";
				text3 = string.Format("{0}把{1}啃了个干净{2}。", text, victim.LabelShort, own ? "(亲生)" : "(非亲生)");
			}
			else
			{
				text2 = "鼠鼠啃饱停嘴了吱";
				text3 = string.Format("{0}啃了{1}几口,吃饱收工{2}。{1}还活着。", text, victim.LabelShort, own ? "(亲生)" : "(非亲生)");
			}
			string text4 = FormatMoodShort(text, GetEaterThought(kind, own, tier));
			string text5 = FormatMoodShort(victim.LabelShort, GetVictimThought(tier));
			if (text4 != null)
			{
				text3 = text3 + "\n心情:" + text4 + ((text5 != null) ? (" / " + text5) : "");
			}
			Letter letter = LetterMaker.MakeLetter(text2, text3, LetterDefOf.NeutralEvent, new LookTargets(victim));
			Find.LetterStack.ReceiveLetter(letter);
		}
	}

	private static string FormatMoodShort(string pawnName, ThoughtDef def)
	{
		if (def == null || def.stages == null || def.stages.Count == 0)
		{
			return null;
		}
		float baseMoodEffect = def.stages[0].baseMoodEffect;
		string text = ((baseMoodEffect >= 0f) ? "+" : "");
		return $"{pawnName}【{def.stages[0].label}】{text}{baseMoodEffect:0}";
	}

	private static Map eatMap(Pawn eater, Pawn victim)
	{
		if (eater != null && eater.MapHeld != null)
		{
			return eater.MapHeld;
		}
		if (victim != null && victim.MapHeld != null)
		{
			return victim.MapHeld;
		}
		return null;
	}

	public static bool IsFineOrBetter(Thing food)
	{
		if (food == null || food.def == null || food.def.ingestible == null)
		{
			return false;
		}
		return (int)food.def.ingestible.preferability >= 9;
	}
}
