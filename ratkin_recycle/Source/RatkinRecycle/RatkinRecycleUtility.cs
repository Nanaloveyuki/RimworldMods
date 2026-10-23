using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RatkinRecycle;

public static class RatkinRecycleUtility
{
	private static List<string> raceKeywordsCache;

	private static string cachedRaceKeywords;

	private static readonly List<Pawn> eggBuffer = new List<Pawn>();

	private static readonly List<Pawn> eaterBuffer = new List<Pawn>();

	private static readonly List<BodyPartRecord> biteParts = new List<BodyPartRecord>();
	private static readonly Dictionary<BodyPartDef, int> partPriorities = new Dictionary<BodyPartDef, int>();

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
		cachedRaceKeywords = null;
		RatkinRecyclePolicy.InvalidateBodyParts();
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
		if (p?.RaceProps == null || !p.RaceProps.Humanlike)
		{
			return false;
		}
		string defName = p.def.defName;
		List<string> keywords = RaceKeywords();
		for (int i = 0; i < keywords.Count; i++)
		{
			if (defName.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	private static List<string> RaceKeywords()
	{
		string keywords = RatkinRecycleMod.Settings?.raceKeywords ?? string.Empty;
		if (raceKeywordsCache == null || !string.Equals(cachedRaceKeywords, keywords, StringComparison.Ordinal))
		{
			cachedRaceKeywords = keywords;
			raceKeywordsCache ??= new List<string>();
			raceKeywordsCache.Clear();
			foreach (string keyword in keywords.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string trimmed = keyword.Trim();
				if (trimmed.Length > 0) raceKeywordsCache.Add(trimmed);
			}
		}
		return raceKeywordsCache;
	}

	public static float AgeYears(Pawn p)
	{
		if (p?.ageTracker == null)
		{
			return 0f;
		}
		return p.ageTracker.AgeBiologicalYearsFloat;
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
		if (RatkinRecycleGameComp.Current?.HasGaveBirthRecently(p, settings.postpartumHours) == true)
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
		RatkinRecycleGameComp history = RatkinRecycleGameComp.Current;
		long lastFineMealTick = history?.GetLastFineMealTick(p) ?? -1;
		long lastBirthTick = history?.GetLastBirthTick(p) ?? -1;
		if (settings.fineMealMode == 0)
		{
			return lastFineMealTick < 0 || (lastBirthTick >= 0 && lastFineMealTick < lastBirthTick);
		}
		long num = (long)(Mathf.Max(1f, settings.fineMealHours) * 2500f);
		if (lastFineMealTick >= 0)
		{
			return Find.TickManager.TicksGame - lastFineMealTick > num;
		}
		return true;
	}

	public static EaterKind GetEaterKind(Pawn p)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (!CanEat(p)) return EaterKind.None;
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

	public static bool CanEat(Pawn pawn)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		return settings != null && settings.enabled && IsRatkin(pawn) && !pawn.Dead && !pawn.Downed &&
			pawn.needs?.food != null && RatkinRecyclePolicy.AgeAllowed(pawn, settings.minEaterAge, settings.maxEaterAge, settings.eaterAgeUnlimited) &&
			(settings.eaterStatuses & RatkinRecyclePolicy.Status(pawn)) != 0 &&
			(!settings.skipDrafted || !pawn.Drafted) && (!settings.skipMentalState || !pawn.InMentalState);
	}

	public static bool IsVictimAllowed(Pawn pawn)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		return settings != null && settings.enabled && IsRatkin(pawn) && !pawn.Dead &&
			RatkinRecyclePolicy.AgeAllowed(pawn, settings.minVictimAge, settings.maxVictimAge, settings.victimAgeUnlimited) &&
			(settings.victimStatuses & RatkinRecyclePolicy.Status(pawn)) != 0;
	}

	public static bool CanTarget(Pawn eater, Pawn victim, EaterKind kind)
	{
		return kind != EaterKind.None && eater != null && eater != victim && IsVictimAllowed(victim) && victim.Spawned && eater.Spawned && victim.Map == eater.Map &&
			!(RatkinRecycleMod.Settings.limitYoungPredators && kind == EaterKind.Egg && AgeYears(victim) >= RatkinRecycleMod.Settings.splitEggAge) &&
			!victim.IsForbidden(eater);
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
		Pawn nearest = null;
		Pawn nearestOwn = null;
		float nearestDistance = float.MaxValue;
		float nearestOwnDistance = float.MaxValue;
		float radiusSquared = settings.searchRadius * settings.searchRadius;
		for (int i = 0; i < eggs.Count; i++)
		{
			Pawn victim = eggs[i];
			if (!CanTarget(eater, victim, kind))
				continue;
			float distance = (victim.Position - eater.Position).LengthHorizontalSquared;
			if (distance > radiusSquared) continue;
			bool own = kind == EaterKind.Mom && IsOwnEgg(eater, victim);
			if (distance >= (own ? nearestOwnDistance : nearestDistance)) continue;
			if (victim.IsForbidden(eater) || !eater.CanReserve(victim) || !eater.CanReach(victim, PathEndMode.Touch, Danger.Deadly)) continue;
			if (own)
			{
				nearestOwn = victim;
				nearestOwnDistance = distance;
			}
			else
			{
				nearest = victim;
				nearestDistance = distance;
			}
		}
		return nearest ?? nearestOwn;
	}

	public static void TickScan()
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null || !settings.enabled)
		{
			return;
		}
		int ticksGame = Find.TickManager.TicksGame;
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
				if (pawn != null && !pawn.Dead && pawn.Spawned && IsRatkin(pawn))
				{
					if (flag && AgeYears(pawn) < settings.maxEggAge && pawn.needs != null && !pawn.needs.TryGetNeed(MoodNeed, out var _))
						pawn.needs.AddOrRemoveNeedsAsAppropriate();
					if (IsVictimAllowed(pawn))
					{
						eggBuffer.Add(pawn);
					}
					if (pawn.CurJobDef != RatkinRecycleDefOf.EatRatEgg && pawn.carryTracker?.CarriedThing == null && CanEat(pawn) && pawn.needs.food.CurLevelPercentage < num)
					{
						eaterBuffer.Add(pawn);
					}
				}
			}
			if (eggBuffer.Count == 0)
			{
				continue;
			}
			for (int l = 0; l < eaterBuffer.Count; l++)
			{
				Pawn pawn3 = eaterBuffer[l];
				RatkinRecycleGameComp history = RatkinRecycleGameComp.Current;
				if (history?.OnCooldown(pawn3, ticksGame) == true)
				{
					continue;
				}
				EaterKind eaterKind = GetEaterKind(pawn3);
				if (eaterKind == EaterKind.None || !IsHungryEnough(pawn3, eaterKind))
				{
					continue;
				}
				if (settings.preferAvailableFood && (pawn3.CurJobDef == JobDefOf.Ingest || pawn3.CurJobDef == JobDefOf.TakeFromOtherInventory))
				{
					continue;
				}
				history?.SetCooldown(pawn3, ticksGame + retryCooldown);
				Pawn pawn4 = TryFindTargetFromEggs(pawn3, eaterKind, eggBuffer);
				if (pawn4 == null) continue;
				Job job;
				if (RatkinRecycleFood.TryFindFood(pawn3, out Thing food, out ThingDef foodDef))
				{
					job = RatkinRecycleFood.MakeFoodJob(pawn3, food, foodDef);
					if (job == null) continue;
				}
				else
				{
					job = JobMaker.MakeJob(RatkinRecycleDefOf.EatRatEgg, pawn4);
				}
				try
				{
					pawn3.jobs.StartJob(job, JobCondition.InterruptForced);
					if (settings.debugLog && job.def == RatkinRecycleDefOf.EatRatEgg)
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
		eggBuffer.Clear();
		eaterBuffer.Clear();
	}

	public static BodyPartRecord PickBitePart(Pawn victim)
	{
		HediffSet health = victim?.health?.hediffSet;
		if (health == null) return null;
		List<BodyPartRecord> parts = victim.RaceProps.body.AllParts;
		int bestPriority = int.MaxValue;
		biteParts.Clear();
		for (int i = 0; i < parts.Count; i++)
		{
			BodyPartRecord part = parts[i];
			if (!RatkinRecycleMod.Settings.BodyPartAllowed(part.def.defName) || !IsNaturalPresentPart(health, part)) continue;
			int priority = RatkinRecycleMod.Settings.limbsFirst ? PartPriority(part) : 0;
			if (priority > bestPriority) continue;
			if (priority < bestPriority)
			{
				biteParts.Clear();
				bestPriority = priority;
			}
			biteParts.Add(part);
		}
		BodyPartRecord result = biteParts.Count == 0 ? null : biteParts.RandomElement();
		biteParts.Clear();
		return result;
	}

	private static bool IsNaturalPresentPart(HediffSet health, BodyPartRecord part)
	{
		for (BodyPartRecord ancestor = part; ancestor != null; ancestor = ancestor.parent)
			if (health.PartIsMissing(ancestor) || health.HasDirectlyAddedPartFor(ancestor)) return false;
		return true;
	}

	private static int PartPriority(BodyPartRecord part)
	{
		if (!partPriorities.TryGetValue(part.def, out int priority))
		{
			priority = IsExtremity(part) ? 0 : IsHeadPart(part) ? 2 : 1;
			partPriorities.Add(part.def, priority);
		}
		return priority;
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
				if (corpse != null && !corpse.Destroyed && eater != null &&
					(corpse.ParentHolder == eater.carryTracker || (corpse.Spawned && corpse.Map == eater.MapHeld)))
				{
					corpse.Destroy();
				}
			}
			if (settings.leaveMeat && settings.meatCount > 0 && map != null && loc.IsValid)
			{
				ThingDef namedSilentFail = victim?.RaceProps?.meatDef;
				if (namedSilentFail != null)
				{
					Thing meat = ThingMaker.MakeThing(namedSilentFail);
					meat.stackCount = Mathf.Min(settings.meatCount, namedSilentFail.stackLimit);
					GenPlace.TryPlaceThing(meat, loc, map, ThingPlaceMode.Near, out var _);
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

	public static void OnEggPartiallyEaten(Pawn eater, Pawn victim, EaterKind kind, bool own, int tier, string stopReason)
	{
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		if (settings == null)
		{
			return;
		}
		try
		{
			GainEggThoughts(eater, victim, kind, own, tier);
			SendEatLetter(eater, victim, kind, own, tier, eatenFully: false, stopReason);
			if (settings.debugLog)
			{
				Log.Message($"[RatkinRecycle] {eater?.LabelShort} 停止啃食 {victim?.LabelShort}: {stopReason} (own={own}, tier={tier})");
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

	private static void SendEatLetter(Pawn eater, Pawn victim, EaterKind kind, bool own, int tier, bool eatenFully, string stopReason = null)
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
				bool exhausted = stopReason == "exhausted";
				text2 = exhausted ? "鼠鼠停嘴了吱" : "鼠鼠啃饱停嘴了吱";
				text3 = exhausted
					? string.Format("{0}放下了{1}{2}。{1}还活着，身上已经没有允许继续啃食的部位。", text, victim.LabelShort, own ? "(亲生)" : "(非亲生)")
					: string.Format("{0}啃了{1}几口，吃饱收工{2}。{1}还活着。", text, victim.LabelShort, own ? "(亲生)" : "(非亲生)");
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
