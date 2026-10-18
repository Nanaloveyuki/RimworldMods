using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

namespace RatkinRecycle;

public partial class RatkinRecycleSettings
{
	private static readonly string[] pageIds = { "general", "targets", "chewing", "moods", "station", "advanced" };
	public static IReadOnlyList<string> PageIds => pageIds;
	private readonly Dictionary<string, string> numberBuffers = new Dictionary<string, string>(StringComparer.Ordinal);
	private readonly Vector2[] pageScroll = new Vector2[6];
	private Listing_Standard nativeListing;
	private List<TabRecord> tabs;
	private int selectedPage;

	public static string PageTitle(string pageId)
	{
		return pageId switch
		{
			"general" => T("常规", "General"),
			"targets" => T("捕食双方", "Participants"),
			"chewing" => T("啃食与部位", "Chewing"),
			"moods" => T("心情", "Moods"),
			"station" => T("止血装置", "Station"),
			"advanced" => T("高级", "Advanced"),
			_ => throw new ArgumentException("Unknown settings page: " + pageId, nameof(pageId))
		};
	}

	private static string T(string chinese, string english) => LanguageDatabase.activeLanguage?.folderName?.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase) == true ? chinese : english;

	private void ClearEditBuffers() => numberBuffers.Clear();

	public void DoWindowContents(Rect inRect)
	{
		GameFont font = Text.Font;
		TextAnchor textAnchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		Color color = GUI.color;
		bool enabled = GUI.enabled;
		try
		{
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Text.WordWrap = true;
			DrawNativeWindow(inRect);
		}
		finally
		{
			Text.Font = font;
			Text.Anchor = textAnchor;
			Text.WordWrap = wordWrap;
			GUI.color = color;
			GUI.enabled = enabled;
		}
	}

	private void DrawNativeWindow(Rect inRect)
	{
		if (inRect.width <= 40f || inRect.height <= 80f) return;
		if (tabs == null)
		{
			tabs = new List<TabRecord>(pageIds.Length);
			for (int i = 0; i < pageIds.Length; i++)
			{
				int index = i;
				tabs.Add(new TabRecord(PageTitle(pageIds[i]), () => selectedPage = index, () => selectedPage == index));
			}
		}
		for (int i = 0; i < tabs.Count; i++) tabs[i].label = PageTitle(pageIds[i]);
		float tabHeight = TabDrawer.GetOverflowTabHeight(inRect, tabs, 110f, 180f);
		TabDrawer.DrawTabsOverflow(inRect, tabs, 110f, 180f);
		Rect panel = new Rect(inRect.x, inRect.y + tabHeight, inRect.width, inRect.height - tabHeight);
		Widgets.DrawMenuSection(panel);
		Rect viewport = panel.ContractedBy(10f);
		float width = Math.Max(1f, viewport.width - 18f);
		Listing_Standard listing = nativeListing ??= new Listing_Standard { maxOneColumn = true };
		listing.Begin(new Rect(0f, 0f, width, 1000000f));
		float measured;
		try
		{
			DrawPageCore(pageIds[selectedPage], listing, null, true);
			measured = listing.CurHeight + 12f;
		}
		finally { listing.End(); }
		Rect content = new Rect(0f, 0f, width, Math.Max(viewport.height, measured));
		Widgets.BeginScrollView(viewport, ref pageScroll[selectedPage], content);
		try
		{
			listing.Begin(content);
			try { DrawPage(pageIds[selectedPage], listing); }
			finally { listing.End(); }
		}
		finally { Widgets.EndScrollView(); }
	}

	public void DrawPage(string pageId, Listing_Standard list, Action<Listing_Standard, string, float> anchor = null)
	{
		GameFont font = Text.Font;
		TextAnchor textAnchor = Text.Anchor;
		bool wordWrap = Text.WordWrap;
		Color color = GUI.color;
		bool enabled = GUI.enabled;
		try
		{
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Text.WordWrap = true;
			DrawPageCore(pageId, list, anchor, false);
		}
		finally
		{
			Text.Font = font;
			Text.Anchor = textAnchor;
			Text.WordWrap = wordWrap;
			GUI.color = color;
			GUI.enabled = enabled;
		}
	}

	private void DrawPageCore(string pageId, Listing_Standard list, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		switch (pageId)
		{
			case "general": DrawGeneral(list, anchor, measure); break;
			case "targets": DrawTargets(list, anchor, measure); break;
			case "chewing": DrawChewing(list, anchor, measure); break;
			case "moods": DrawMoods(list, anchor, measure); break;
			case "station": DrawStation(list, anchor, measure); break;
			case "advanced": DrawAdvanced(list, anchor, measure); break;
			default: throw new ArgumentException("Unknown settings page: " + pageId, nameof(pageId));
		}
	}

	private void DrawGeneral(Listing_Standard list, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Section(list, "enabled", T("回收行为", "Recycling behavior"), anchor, measure);
		Check(list, "enabled", T("启用捕食行为", "Enable predation"), ref enabled, anchor, measure);
		Check(list, "showLetter", T("捕食目标死亡时发来信件", "Send a letter when the victim dies"), ref showLetter, anchor, measure);
		Check(list, "showStopLetter", T("吃饱或没有可啃部位而停嘴，且目标存活时发来信件", "Send a letter when chewing stops with the victim alive, from fullness or no eligible parts"), ref showStopLetter, anchor, measure);
		Section(list, "hunger", T("饥饿与母性判定", "Hunger and mothers"), anchor, measure);
		Number(list, "momHungerPct", T("鼠妈饱食度触发阈值（0–1）", "Mother hunger threshold (0–1 fullness)"), ref momHungerPct, anchor, measure, 0f, 1f);
		Number(list, "otherHungerPct", T("其他鼠族饱食度触发阈值（0–1）", "Other Ratkin hunger threshold (0–1 fullness)"), ref otherHungerPct, anchor, measure, 0f, 1f);
		Number(list, "adultAgeYears", T("鼠妈最低生理年龄（岁）", "Minimum biological age for mothers (years)"), ref adultAgeYears, anchor, measure);
		Number(list, "postpartumHours", T("产后母性窗口（游戏小时）", "Postpartum mother window (game hours)"), ref postpartumHours, anchor, measure);
		Check(list, "momOwnEggFallback", T("仍有亲生鼠蛋时，也视为鼠妈", "Also count mothers with living biological RatkinYoung"), ref momOwnEggFallback, anchor, measure);
		Number(list, "maxEggAge", T("鼠蛋阶段上限（生理年龄，岁）", "RatkinYoung classification cutoff (biological years)"), ref maxEggAge, anchor, measure);
		Label(list, T("这个上限用于母性补充判定、幼鼠捕食限制和幼鼠心情需求。被捕食者年龄请在“捕食双方”中单独设置。", "This cutoff controls the mother fallback, young-predator rules and young mood needs. Set the victim age range separately under Participants."), measure);
		Section(list, "fineMeal", T("精致食物", "Fine meals"), anchor, measure);
		bool recent = fineMealMode == 1;
		Check(list, "fineMealMode", T("按最近一段时间内是否吃过精致及以上食物判定", "Check fine-or-better meals within a recent time window"), ref recent, anchor, measure);
		if (!measure) fineMealMode = recent ? 1 : 0;
		Label(list, T("关闭时，从最近一次生产开始计算；开启时，使用下面的时间窗口。", "When off, count meals since the latest birth. When on, use the window below."), measure);
		Number(list, "fineMealHours", T("精致食物回顾窗口（游戏小时）", "Fine-meal lookback window (game hours)"), ref fineMealHours, anchor, measure, enabled: recent);
	}

	private void DrawTargets(Listing_Standard list, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Section(list, "eater", T("捕食者", "Eaters"), anchor, measure);
		Label(list, T("身份按当前状态分别判定：囚犯、奴隶、自由殖民者、外来者。默认排除自由殖民者。", "Each role uses the pawn's current status. Free player colonists are excluded by default."), measure);
		Statuses(list, "eater", ref eaterStatuses, anchor, measure);
		Number(list, "minEaterAge", T("最低生理年龄（岁，含）", "Minimum biological age (years, inclusive)"), ref minEaterAge, anchor, measure);
		Check(list, "eaterAgeUnlimited", T("捕食者年龄无上限", "No maximum eater age"), ref eaterAgeUnlimited, anchor, measure);
		Number(list, "maxEaterAge", T("最高生理年龄（岁，不含）", "Maximum biological age (years, exclusive)"), ref maxEaterAge, anchor, measure, enabled: !eaterAgeUnlimited);
		Check(list, "skipDrafted", T("跳过征召中的捕食者", "Skip drafted eaters"), ref skipDrafted, anchor, measure);
		Check(list, "skipMentalState", T("跳过精神崩溃中的捕食者", "Skip eaters in a mental state"), ref skipMentalState, anchor, measure);
		Section(list, "victim", T("被捕食者", "Victims"), anchor, measure);
		Statuses(list, "victim", ref victimStatuses, anchor, measure);
		Number(list, "minVictimAge", T("最低生理年龄（岁，含）", "Minimum biological age (years, inclusive)"), ref minVictimAge, anchor, measure);
		Check(list, "victimAgeUnlimited", T("被捕食者年龄无上限", "No maximum victim age"), ref victimAgeUnlimited, anchor, measure);
		Number(list, "maxVictimAge", T("最高生理年龄（岁，不含）", "Maximum biological age (years, exclusive)"), ref maxVictimAge, anchor, measure, enabled: !victimAgeUnlimited);
		Label(list, T("捕食从地图上未被抱起的目标开始。访客和商队自身的回收行为免除玩家好感惩罚与由此引发的转敌；玩家派系或玩家拘押的捕食者造成伤害时，仍按原版处理。", "Predation starts with an unheld target on the map. Recycling by visitors and traders is exempt from player goodwill penalties and resulting hostility. Damage by player-faction or player-held eaters still follows vanilla rules."), measure);
		Section(list, "youngPredators", T("幼鼠捕食限制", "Young predators"), anchor, measure);
		Check(list, "limitYoungPredators", T("幼鼠捕食者只能捕食分界年龄以下的幼鼠", "Restrict young eaters to young victims below the split age"), ref limitYoungPredators, anchor, measure);
		Number(list, "splitEggAge", T("大鼠蛋与小鼠蛋的分界（生理年龄，岁）", "Older/younger RatkinYoung split (biological years)"), ref splitEggAge, anchor, measure);
		Number(list, "searchRadius", T("搜索半径（格）", "Search radius (cells)"), ref searchRadius, anchor, measure);
	}

	private void DrawChewing(Listing_Standard list, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Section(list, "bites", T("啃食过程", "Bites"), anchor, measure);
		Integer(list, "biteInterval", T("每口间隔（tick，最少 10；1 倍速每秒 60 tick）", "Bite interval (ticks, minimum 10; 60 ticks per second at 1× speed)"), ref biteInterval, anchor, measure, 10);
		Number(list, "biteDamage", T("每口基础伤害（额外随机增加 0–2）", "Base bite damage (plus a random 0–2)"), ref biteDamage, anchor, measure);
		Number(list, "nutritionPerBite", T("每口获得的营养", "Nutrition per bite"), ref nutritionPerBite, anchor, measure);
		Number(list, "stopAtFullness", T("停嘴饱食度（0–1；0 = 持续啃食）", "Stop at fullness (0–1; 0 = keep chewing)"), ref stopAtFullness, anchor, measure, 0f, 1f);
		Number(list, "nutritionOnFinish", T("目标死亡后额外获得的营养", "Extra nutrition when the victim dies"), ref nutritionOnFinish, anchor, measure);
		Check(list, "limbsFirst", T("优先四肢，其次躯干，最后头部", "Prioritize limbs, then torso, then head"), ref limbsFirst, anchor, measure);
		Check(list, "destroyCorpse", T("目标死亡后销毁尸体", "Destroy the corpse after the victim dies"), ref destroyCorpse, anchor, measure);
		Check(list, "leaveMeat", T("目标死亡后留下鼠肉", "Leave Ratkin meat after the victim dies"), ref leaveMeat, anchor, measure);
		Integer(list, "meatCount", T("留下的鼠肉数量（份）", "Meat left behind (units)"), ref meatCount, anchor, measure, enabled: leaveMeat);
		Section(list, "bodyParts", T("允许直接啃咬的天然部位", "Natural parts allowed as direct bite targets"), anchor, measure);
		Label(list, T("每种部位只列一次。人工部件始终排除。取消某部位只阻止直接选中；啃掉上级部位仍可能连带失去它，伤口也仍可能流血。新模组添加的部位默认未勾选。", "Each part type appears once. Artificial parts are always excluded. Unchecking a part prevents direct targeting; losing a parent part can still remove it, and wounds can still bleed. New modded part types start unchecked."), measure);
		IReadOnlyList<BodyPartDef> parts = RatkinRecyclePolicy.BodyParts;
		if (parts.Count == 0) Label(list, T("当前种族范围内没有可列出的部位。请检查高级页中的种族关键字。", "No body parts are available for the selected races. Check the race keywords under Advanced."), measure);
		foreach (BodyPartDef part in parts)
		{
			bool allowed = BodyPartAllowed(part.defName);
			Check(list, "part:" + part.defName, part.LabelCap, ref allowed, anchor, measure, part.defName);
			if (!measure) SetBodyPartAllowed(part.defName, allowed);
		}
	}

	private void DrawMoods(Listing_Standard list, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Section(list, "moodStages", T("心情与成长阶段", "Moods and age stages"), anchor, measure);
		Check(list, "babyMoodNeed", T("鼠蛋阶段拥有心情需求条", "Give RatkinYoung a mood need"), ref babyMoodNeed, anchor, measure);
		Number(list, "tierToddlerMax", T("幼儿心情档上限（生理年龄，岁）", "Toddler mood cutoff (biological years)"), ref tierToddlerMax, anchor, measure);
		Number(list, "tierChildMax", T("儿童心情档上限（生理年龄，岁）", "Child mood cutoff (biological years)"), ref tierChildMax, anchor, measure);
		Label(list, T("以下数值为心情增减：正数提高心情，负数降低心情。", "Mood offsets below: positive values improve mood; negative values lower it."), measure);
		MoodGroup(list, "motherOther", T("鼠妈吃非亲生鼠蛋", "Mother eating unrelated RatkinYoung"), "momNonOwn", ref momNonOwnToddler, ref momNonOwnChild, ref momNonOwnAdult, anchor, measure);
		MoodGroup(list, "motherOwn", T("鼠妈吃亲生鼠蛋", "Mother eating her own RatkinYoung"), "momOwn", ref momOwnToddler, ref momOwnChild, ref momOwnAdult, anchor, measure);
		MoodGroup(list, "otherEater", T("其他捕食者", "Other eaters"), "other", ref otherToddler, ref otherChild, ref otherAdult, anchor, measure);
		MoodGroup(list, "eaten", T("被捕食者", "Victims"), "eaten", ref eatenToddler, ref eatenChild, ref eatenAdult, anchor, measure);
	}

	private void MoodGroup(Listing_Standard list, string id, string title, string prefix, ref float toddler, ref float child, ref float adult, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Section(list, id, title, anchor, measure);
		Number(list, prefix + "Toddler", T("幼儿", "Toddler"), ref toddler, anchor, measure, float.MinValue);
		Number(list, prefix + "Child", T("儿童", "Child"), ref child, anchor, measure, float.MinValue);
		Number(list, prefix + "Adult", T("成年", "Adult"), ref adult, anchor, measure, float.MinValue);
	}

	private void DrawStation(Listing_Standard list, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Section(list, "station", T("自动止血装置", "Automatic bleeding-control station"), anchor, measure);
		Check(list, "bleedStationEnabled", T("启用自动止血装置", "Enable the bleeding-control station"), ref bleedStationEnabled, anchor, measure);
		Number(list, "bleedStationRadius", T("治疗半径（格）", "Treatment radius (cells)"), ref bleedStationRadius, anchor, measure);
		Integer(list, "bleedStationInterval", T("治疗间隔（tick；1 倍速每秒 60 tick）", "Treatment interval (ticks; 60 ticks per second at 1× speed)"), ref bleedStationInterval, anchor, measure, 1);
		Number(list, "bleedStationHeal", T("每次愈合的流血伤口严重度", "Bleeding wound severity healed per treatment"), ref bleedStationHeal, anchor, measure);
		Check(list, "bleedStationHealFriendly", T("治疗玩家与友方角色", "Treat player and friendly pawns"), ref bleedStationHealFriendly, anchor, measure);
		Check(list, "bleedStationHealHostile", T("治疗敌对角色", "Treat hostile pawns"), ref bleedStationHealHostile, anchor, measure);
		Check(list, "bleedStationHealWild", T("治疗无派系角色", "Treat factionless pawns"), ref bleedStationHealWild, anchor, measure);
		Check(list, "bleedStationHealAnimals", T("治疗动物", "Treat animals"), ref bleedStationHealAnimals, anchor, measure);
	}

	private void DrawAdvanced(Listing_Standard list, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Section(list, "scheduling", T("检查频率", "Scheduling"), anchor, measure);
		Integer(list, "checkInterval", T("捕食检查间隔（tick，最少 10）", "Predation check interval (ticks, minimum 10)"), ref checkInterval, anchor, measure, 10);
		Integer(list, "retryCooldown", T("捕食失败后的重试冷却（tick）", "Retry cooldown after failed predation (ticks)"), ref retryCooldown, anchor, measure, 1);
		Section(list, "raceKeywords", T("生效种族", "Affected races"), anchor, measure);
		Label(list, T("填写种族标识中包含的关键字，以逗号分隔。例如 Ratkin。", "Enter comma-separated keywords contained in the race identifier, such as Ratkin."), measure);
		if (!measure) anchor?.Invoke(list, "raceKeywordsField", 32f);
		Rect field = list.GetRect(32f);
		if (!measure)
		{
			string next = Widgets.TextField(field, raceKeywords ?? "", 400);
			if (next != raceKeywords)
			{
				raceKeywords = next;
				RatkinRecyclePolicy.InvalidateBodyParts();
			}
		}
		list.Gap(8f);
		Check(list, "debugLog", T("输出调试日志", "Enable debug logging"), ref debugLog, anchor, measure);
		Section(list, "reset", T("恢复默认", "Restore defaults"), anchor, measure);
		Label(list, T("恢复所有六页的默认值，包括年龄、身份、部位和心情。自由殖民者将再次从捕食双方中排除。", "Restore defaults on all six pages, including ages, statuses, body parts and moods. Free player colonists will again be excluded from both roles."), measure);
		Rect button = list.GetRect(32f);
		if (!measure && Widgets.ButtonText(button, T("恢复全部默认设置", "Restore all default settings"))) ResetDefaults();
		list.Gap(8f);
	}

	private void Statuses(Listing_Standard list, string prefix, ref RatkinRecycleStatus statuses, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		Status(list, prefix, RatkinRecycleStatus.Prisoner, T("殖民地囚犯", "Colony prisoners"), ref statuses, anchor, measure);
		Status(list, prefix, RatkinRecycleStatus.Slave, T("殖民地奴隶", "Colony slaves"), ref statuses, anchor, measure);
		Status(list, prefix, RatkinRecycleStatus.Colonist, T("玩家自由殖民者", "Free player colonists"), ref statuses, anchor, measure);
		Status(list, prefix, RatkinRecycleStatus.FriendlyOutsider, T("非敌对外来者（含访客、商队）", "Non-hostile outsiders (including visitors and traders)"), ref statuses, anchor, measure);
		Status(list, prefix, RatkinRecycleStatus.HostileOutsider, T("敌对外来者", "Hostile outsiders"), ref statuses, anchor, measure);
		Status(list, prefix, RatkinRecycleStatus.Factionless, T("无派系角色", "Factionless pawns"), ref statuses, anchor, measure);
	}

	private void Status(Listing_Standard list, string prefix, RatkinRecycleStatus flag, string label, ref RatkinRecycleStatus statuses, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		bool active = (statuses & flag) != 0;
		Check(list, prefix + ":" + flag, label, ref active, anchor, measure);
		if (!measure) statuses = active ? statuses | flag : statuses & ~flag;
	}

	private static void Section(Listing_Standard list, string id, string label, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		list.Gap(10f);
		float height = Math.Max(28f, Text.CalcHeight(label, list.ColumnWidth));
		if (!measure) anchor?.Invoke(list, "section:" + id, height);
		Rect rect = list.GetRect(height);
		if (!measure) Widgets.Label(rect, label);
		if (measure) list.Gap(8f); else list.GapLine(8f);
	}

	private static void Label(Listing_Standard list, string text, bool measure)
	{
		Rect rect = list.GetRect(Text.CalcHeight(text, list.ColumnWidth));
		if (!measure) Widgets.Label(rect, text);
		list.Gap(8f);
	}

	private static void Check(Listing_Standard list, string id, string label, ref bool value, Action<Listing_Standard, string, float> anchor, bool measure, string tooltip = null)
	{
		float height = Math.Max(30f, Text.CalcHeight(label, Math.Max(1f, list.ColumnWidth - 32f)));
		if (!measure) anchor?.Invoke(list, id, height);
		Rect row = list.GetRect(height);
		if (!measure)
		{
			Widgets.CheckboxLabeled(row, label, ref value);
			if (!string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(row, tooltip);
		}
		list.Gap(4f);
	}

	private static Rect NumberRow(Listing_Standard list, string id, string label, Action<Listing_Standard, string, float> anchor, bool measure)
	{
		float fieldWidth = Math.Min(180f, list.ColumnWidth * 0.36f);
		float labelWidth = Math.Max(1f, list.ColumnWidth - fieldWidth - 12f);
		float height = Math.Max(32f, Text.CalcHeight(label, labelWidth));
		if (!measure) anchor?.Invoke(list, id, height);
		Rect row = list.GetRect(height);
		if (!measure) Widgets.Label(new Rect(row.x, row.y, labelWidth, height), label);
		list.Gap(6f);
		return new Rect(row.xMax - fieldWidth, row.y, fieldWidth, 30f);
	}

	private void Number(Listing_Standard list, string id, string label, ref float value, Action<Listing_Standard, string, float> anchor, bool measure, float min = 0f, float max = float.MaxValue, bool enabled = true)
	{
		Rect field = NumberRow(list, id, label, anchor, measure);
		if (measure) return;
		if (!numberBuffers.TryGetValue(id, out string buffer)) buffer = value.ToString("R", CultureInfo.InvariantCulture);
		bool previous = GUI.enabled;
		try
		{
			GUI.enabled = previous && enabled;
			string next = Widgets.TextField(field, buffer);
			if (next != buffer && enabled && float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed) && parsed >= min && parsed <= max) value = parsed;
			numberBuffers[id] = next;
		}
		finally { GUI.enabled = previous; }
	}

	private void Integer(Listing_Standard list, string id, string label, ref int value, Action<Listing_Standard, string, float> anchor, bool measure, int min = 0, bool enabled = true)
	{
		Rect field = NumberRow(list, id, label, anchor, measure);
		if (measure) return;
		if (!numberBuffers.TryGetValue(id, out string buffer)) buffer = value.ToString(CultureInfo.InvariantCulture);
		bool previous = GUI.enabled;
		try
		{
			GUI.enabled = previous && enabled;
			string next = Widgets.TextField(field, buffer);
			if (next != buffer && enabled && int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= min) value = parsed;
			numberBuffers[id] = next;
		}
		finally { GUI.enabled = previous; }
	}
}
