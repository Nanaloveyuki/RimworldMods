using System;
using System.Collections.Generic;
using IrisMenus;
using Verse;

namespace RatkinRecycle.IrisMenus;

[StaticConstructorOnStartup]
public static class RatkinRecycleMenus
{
	static RatkinRecycleMenus() => LongEventHandler.ExecuteWhenFinished(Register);

	private static void Register()
	{
		RatkinRecycleMod owner = LoadedModManager.GetMod<RatkinRecycleMod>();
		RatkinRecycleSettings settings = RatkinRecycleMod.Settings;
		string lastRaceKeywords = settings.raceKeywords;
		foreach (string id in RatkinRecycleSettings.PageIds)
		{
			string pageId = id;
			MenuRegistry.RegisterSubItemListing(owner, pageId,
				() => RatkinRecycleSettings.PageTitle(pageId), list =>
				{
					settings.DrawPage(pageId, list, MenuControls.Anchor);
					if (lastRaceKeywords != settings.raceKeywords)
					{
						lastRaceKeywords = settings.raceKeywords;
						MenuRegistry.InvalidateSearchIndex();
					}
				});
			MenuRegistry.RegisterSearchProvider(owner, pageId, () => SearchEntries(pageId));
		}
	}

	public static IEnumerable<MenuSearchEntry> SearchEntries(string pageId)
	{
		foreach (var item in Entries(pageId))
		{
			var entry = item;
			yield return new MenuSearchEntry(entry.id, () => T(entry.zh, entry.en),
				() => entry.id + " " + entry.zh + " " + entry.en,
				() => RatkinRecycleSettings.PageTitle(pageId));
		}
		if (pageId == "targets")
		{
			foreach (string role in new[] { "eater", "victim" })
			{
				foreach (var status in new[]
				{
					(RatkinRecycleStatus.Prisoner, "殖民地囚犯", "Colony prisoners"),
					(RatkinRecycleStatus.Slave, "殖民地奴隶", "Colony slaves"),
					(RatkinRecycleStatus.Colonist, "玩家自由殖民者", "Free player colonists"),
					(RatkinRecycleStatus.FriendlyOutsider, "非敌对外来者：访客、商队", "Non-hostile outsiders: visitors and traders"),
					(RatkinRecycleStatus.HostileOutsider, "敌对外来者", "Hostile outsiders"),
					(RatkinRecycleStatus.Factionless, "无派系角色", "Factionless pawns")
				})
				{
					string roleId = role;
					var selected = status;
					yield return new MenuSearchEntry(roleId + ":" + selected.Item1,
						() => T(roleId == "eater" ? "捕食者：" : "被捕食者：", roleId == "eater" ? "Eaters: " : "Victims: ") + T(selected.Item2, selected.Item3),
						() => selected.Item2 + " " + selected.Item3 + " status 身份",
						() => RatkinRecycleSettings.PageTitle(pageId));
				}
			}
		}
		if (pageId == "chewing")
		{
			foreach (BodyPartDef part in RatkinRecyclePolicy.BodyParts)
			{
				BodyPartDef selected = part;
				yield return new MenuSearchEntry("part:" + selected.defName, () => selected.LabelCap,
					() => selected.defName + " 部位 天然 啃咬 body part natural bite",
					() => T("允许直接啃咬的天然部位", "Natural parts allowed as direct bite targets"));
			}
		}
	}

	private static string T(string chinese, string english) => LanguageDatabase.activeLanguage?.folderName?.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase) == true ? chinese : english;

	private static IEnumerable<(string id, string zh, string en)> Entries(string pageId)
	{
		switch (pageId)
		{
			case "general":
				yield return ("section:enabled", "回收行为", "Recycling behavior");
				yield return ("enabled", "启用捕食行为", "Enable predation");
				yield return ("showLetter", "死亡信件", "Victim death letters");
				yield return ("showStopLetter", "停嘴信件", "Stop-chewing letters");
				yield return ("section:hunger", "饥饿与母性判定", "Hunger and mothers");
				yield return ("momHungerPct", "鼠妈饱食度阈值", "Mother hunger threshold");
				yield return ("otherHungerPct", "其他鼠族饱食度阈值", "Other Ratkin hunger threshold");
				yield return ("adultAgeYears", "鼠妈最低生理年龄", "Mother minimum biological age");
				yield return ("postpartumHours", "产后母性窗口", "Postpartum mother window");
				yield return ("momOwnEggFallback", "亲生鼠蛋母性判定", "Mother fallback with living biological RatkinYoung");
				yield return ("maxEggAge", "鼠蛋阶段上限", "RatkinYoung classification cutoff");
				yield return ("section:fineMeal", "精致食物", "Fine meals");
				yield return ("fineMealMode", "精致食物判定方式", "Fine-meal rule");
				yield return ("fineMealHours", "精致食物回顾窗口", "Fine-meal lookback window");
				break;
			case "targets":
				yield return ("section:eater", "捕食者身份与年龄", "Eater statuses and ages");
				yield return ("minEaterAge", "捕食者最低生理年龄", "Minimum eater biological age");
				yield return ("maxEaterAge", "捕食者最高生理年龄", "Maximum eater biological age");
				yield return ("eaterAgeUnlimited", "捕食者年龄无上限", "Unlimited maximum eater age");
				yield return ("skipDrafted", "跳过征召捕食者", "Skip drafted eaters");
				yield return ("skipMentalState", "跳过精神崩溃捕食者", "Skip eaters in a mental state");
				yield return ("section:victim", "被捕食者身份与年龄", "Victim statuses and ages");
				yield return ("minVictimAge", "被捕食者最低生理年龄", "Minimum victim biological age");
				yield return ("maxVictimAge", "被捕食者最高生理年龄", "Maximum victim biological age");
				yield return ("victimAgeUnlimited", "被捕食者年龄无上限", "Unlimited maximum victim age");
				yield return ("section:youngPredators", "幼鼠捕食限制", "Young predators");
				yield return ("limitYoungPredators", "幼鼠捕食者只能捕食小鼠蛋", "Restrict young eaters to younger RatkinYoung");
				yield return ("splitEggAge", "大鼠蛋与小鼠蛋的分界", "Older/younger RatkinYoung split age");
				yield return ("searchRadius", "搜索半径", "Search radius");
				break;
			case "chewing":
				yield return ("section:bites", "啃食过程", "Bites");
				yield return ("biteInterval", "每口间隔", "Bite interval");
				yield return ("biteDamage", "每口伤害", "Bite damage");
				yield return ("nutritionPerBite", "每口营养", "Nutrition per bite");
				yield return ("stopAtFullness", "停嘴饱食度", "Stop at fullness");
				yield return ("nutritionOnFinish", "死亡后额外营养", "Extra nutrition after victim death");
				yield return ("limbsFirst", "优先四肢", "Prioritize limbs");
				yield return ("destroyCorpse", "销毁尸体", "Destroy corpse");
				yield return ("leaveMeat", "留下鼠肉", "Leave Ratkin meat");
				yield return ("meatCount", "留下鼠肉数量", "Meat quantity");
				yield return ("section:bodyParts", "允许直接啃咬的天然部位", "Natural bite-target whitelist");
				break;
			case "moods":
				yield return ("section:moodStages", "心情与成长阶段", "Moods and age stages");
				yield return ("babyMoodNeed", "鼠蛋心情需求条", "RatkinYoung mood need");
				yield return ("tierToddlerMax", "幼儿心情档上限", "Toddler mood age cutoff");
				yield return ("tierChildMax", "儿童心情档上限", "Child mood age cutoff");
				yield return ("section:motherOther", "鼠妈吃非亲生鼠蛋", "Mother eating unrelated RatkinYoung");
				yield return ("section:motherOwn", "鼠妈吃亲生鼠蛋", "Mother eating her own RatkinYoung");
				yield return ("section:otherEater", "其他捕食者心情", "Other eater moods");
				yield return ("section:eaten", "被捕食者心情", "Victim moods");
				foreach (var group in new[]
				{
					("momNonOwn", "鼠妈吃非亲生鼠蛋", "Mother eating unrelated RatkinYoung"),
					("momOwn", "鼠妈吃亲生鼠蛋", "Mother eating her own RatkinYoung"),
					("other", "其他捕食者", "Other eaters"),
					("eaten", "被捕食者", "Victims")
				})
				{
					yield return (group.Item1 + "Toddler", group.Item2 + "：幼儿", group.Item3 + ": toddler");
					yield return (group.Item1 + "Child", group.Item2 + "：儿童", group.Item3 + ": child");
					yield return (group.Item1 + "Adult", group.Item2 + "：成年", group.Item3 + ": adult");
				}
				break;
			case "station":
				yield return ("section:station", "自动止血装置", "Automatic bleeding-control station");
				yield return ("bleedStationEnabled", "启用止血装置", "Enable bleeding-control station");
				yield return ("bleedStationRadius", "止血装置治疗半径", "Station treatment radius");
				yield return ("bleedStationInterval", "止血装置治疗间隔", "Station treatment interval");
				yield return ("bleedStationHeal", "止血装置伤口愈合量", "Station wound healing amount");
				yield return ("bleedStationHealFriendly", "治疗玩家与友方角色", "Treat player and friendly pawns");
				yield return ("bleedStationHealHostile", "治疗敌对角色", "Treat hostile pawns");
				yield return ("bleedStationHealWild", "治疗无派系角色", "Treat factionless pawns");
				yield return ("bleedStationHealAnimals", "治疗动物", "Treat animals");
				break;
			case "advanced":
				yield return ("section:scheduling", "检查频率", "Scheduling");
				yield return ("checkInterval", "捕食检查间隔", "Predation check interval");
				yield return ("retryCooldown", "失败重试冷却", "Retry cooldown");
				yield return ("section:raceKeywords", "生效种族关键字", "Affected race keywords");
				yield return ("debugLog", "调试日志", "Debug logging");
				yield return ("section:reset", "恢复全部默认设置", "Restore all default settings");
				break;
		}
	}
}
