using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;

namespace RatkinRecycle;

public interface IRatkinRecycleSettingsRenderer
{
	bool IsMeasuring { get; }
	void Section(string id, string label);
	void Label(string text);
	void Check(string id, string label, ref bool value, string tooltip = null);
	void Number(string id, string label, ref float value, float min = 0f, float max = float.MaxValue, bool enabled = true);
	void Integer(string id, string label, ref int value, int min = 0, bool enabled = true);
	void TextField(string id, ref string value, int maxLength);
	bool Button(string id, string label);
}

public partial class RatkinRecycleSettings
{
	private static readonly string[] pageIds = { "general", "targets", "chewing", "moods", "station", "advanced" };
	public static IReadOnlyList<string> PageIds => pageIds;
	private readonly Dictionary<string, string> numberBuffers = new Dictionary<string, string>(StringComparer.Ordinal);
	private readonly Vector2[] pageScroll = new Vector2[6];
	private Listing_Standard nativeListing;
	private VanillaRenderer vanillaRenderer;
	public int EditRevision { get; private set; }
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

	private void ClearEditBuffers()
	{
		numberBuffers.Clear();
		EditRevision++;
	}

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
		DrawPageWindow(pageIds[selectedPage], panel.ContractedBy(10f));
	}

	public void DrawPageWindow(string pageId, Rect viewport)
	{
		int index = Array.IndexOf(pageIds, pageId);
		if (index < 0) throw new ArgumentException("Unknown settings page: " + pageId, nameof(pageId));
		if (viewport.width <= 20f || viewport.height <= 0f) return;
		float width = Math.Max(1f, viewport.width - 18f);
		Listing_Standard listing = nativeListing ??= new Listing_Standard { maxOneColumn = true };
		listing.Begin(new Rect(0f, 0f, width, 1000000f));
		float measured;
		try
		{
			DrawListingPage(pageId, listing, null, true);
			measured = listing.CurHeight + 12f;
		}
		finally { listing.End(); }
		Rect content = new Rect(0f, 0f, width, Math.Max(viewport.height, measured));
		Widgets.BeginScrollView(viewport, ref pageScroll[index], content);
		try
		{
			listing.Begin(content);
			try { DrawListingPage(pageId, listing); }
			finally { listing.End(); }
		}
		finally { Widgets.EndScrollView(); }
	}

	private void DrawListingPage(string pageId, Listing_Standard list, Action<Listing_Standard, string, float> anchor = null, bool measure = false)
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
			VanillaRenderer renderer = vanillaRenderer ??= new VanillaRenderer(this);
			renderer.Begin(list, anchor, measure);
			DrawPage(pageId, renderer);
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

	public void DrawPage(string pageId, IRatkinRecycleSettingsRenderer renderer)
	{
		switch (pageId)
		{
			case "general": DrawGeneral(renderer); break;
			case "targets": DrawTargets(renderer); break;
			case "chewing": DrawChewing(renderer); break;
			case "moods": DrawMoods(renderer); break;
			case "station": DrawStation(renderer); break;
			case "advanced": DrawAdvanced(renderer); break;
			default: throw new ArgumentException("Unknown settings page: " + pageId, nameof(pageId));
		}
	}

	private void DrawGeneral(IRatkinRecycleSettingsRenderer renderer)
	{
		renderer.Section("enabled", T("回收行为", "Recycling behavior"));
		renderer.Check("enabled", T("启用捕食行为", "Enable predation"), ref enabled);
		renderer.Check("showLetter", T("捕食目标死亡时发来信件", "Send a letter when the victim dies"), ref showLetter);
		renderer.Check("showStopLetter", T("吃饱或没有可啃部位而停嘴，且目标存活时发来信件", "Send a letter when chewing stops with the victim alive, from fullness or no eligible parts"), ref showStopLetter);
		renderer.Check("preferAvailableFood", T("自动回收时优先普通食物", "Prefer available food during automatic recycling"), ref preferAvailableFood);
		renderer.Label(T("仅影响自动回收，默认开启。检查自身背包、原版允许食用且可达的地图食物，以及同一访客／商队队伍中可达驮兽的食物；不要求精致食物。禁止食用、不可达、无法预约、空营养膏机和需先收获的作物不算。抱起前及每口前复查，发现食物就停止回收并尝试放下鼠蛋。关闭后恢复原来的自动回收优先级。", "Applies only to automatic recycling; enabled by default. Check personal inventory, reachable map food allowed by vanilla, and reachable pack animals in the same visiting group; fine meals are not required. Forbidden, unreachable or unreservable food, empty dispensers and crops requiring harvest do not count. Recheck before pickup and every bite; when food is found, stop recycling and try to put down the RatkinYoung. Disable to restore the previous automatic recycling priority."));
		renderer.Section("hunger", T("自动回收的饥饿与母性判定", "Automatic recycling: hunger and mothers"));
		renderer.Check("useMalnutritionTrigger", T("按营养不良严重度触发，替代饱食度", "Trigger by malnutrition severity instead of fullness"), ref useMalnutritionTrigger);
		renderer.Number("momHungerPct", T("鼠妈饱食度触发阈值（0–1）", "Mother hunger threshold (0–1 fullness)"), ref momHungerPct, 0f, 1f, enabled: !useMalnutritionTrigger);
		renderer.Number("otherHungerPct", T("其他鼠族饱食度触发阈值（0–1）", "Other Ratkin hunger threshold (0–1 fullness)"), ref otherHungerPct, 0f, 1f, enabled: !useMalnutritionTrigger);
		renderer.Number("momMalnutritionSeverity", T("鼠妈营养不良严重度阈值（0–1）", "Mother malnutrition severity threshold (0–1)"), ref momMalnutritionSeverity, 0f, 1f, enabled: useMalnutritionTrigger);
		renderer.Number("otherMalnutritionSeverity", T("其他鼠族营养不良严重度阈值（0–1）", "Other Ratkin malnutrition severity threshold (0–1)"), ref otherMalnutritionSeverity, 0f, 1f, enabled: useMalnutritionTrigger);
		renderer.Label(T("以下启动阈值仅影响自动回收。营养不良模式要求角色正在挨饿，且实际营养不良严重度大于 0 并达到设定阈值；进食后残留的营养不良不会触发。阈值过高时，角色可能已经倒地。严重度只决定是否开始，自动回收开始后仍按停嘴饱食度结束。主动回收不受这些阈值或吃饱停嘴限制。灰显的阈值属于另一种模式。", "These trigger thresholds apply only to automatic recycling. Malnutrition mode requires the pawn to be starving, with actual malnutrition severity above 0 and at or above the threshold; lingering malnutrition after eating does not trigger recycling. A high threshold may leave the pawn downed before it can act. Severity only controls starting; automatic chewing still ends at the configured fullness. Manual recycling ignores these thresholds and does not stop at fullness. Grayed-out thresholds belong to the other mode."));
		renderer.Number("adultAgeYears", T("鼠妈最低生理年龄（岁）", "Minimum biological age for mothers (years)"), ref adultAgeYears);
		renderer.Number("postpartumHours", T("产后母性窗口（游戏小时）", "Postpartum mother window (game hours)"), ref postpartumHours);
		renderer.Check("momOwnEggFallback", T("仍有亲生鼠蛋时，也视为鼠妈", "Also count mothers with living biological RatkinYoung"), ref momOwnEggFallback);
		renderer.Number("maxEggAge", T("鼠蛋阶段上限（生理年龄，岁）", "RatkinYoung classification cutoff (biological years)"), ref maxEggAge);
		renderer.Label(T("这个上限用于母性补充判定、幼鼠捕食限制和幼鼠心情需求。被捕食者年龄请在“捕食双方”中单独设置。", "This cutoff controls the mother fallback, young-predator rules and young mood needs. Set the victim age range separately under Participants."));
		renderer.Section("fineMeal", T("自动回收的精致食物判定", "Fine-meal rules for automatic recycling"));
		bool recent = fineMealMode == 1;
		renderer.Check("fineMealMode", T("按最近一段时间内是否吃过精致及以上食物判定", "Check fine-or-better meals within a recent time window"), ref recent);
		if (!renderer.IsMeasuring) fineMealMode = recent ? 1 : 0;
		renderer.Label(T("仅影响自动回收。关闭时，从最近一次生产开始计算；开启时，使用下面的时间窗口。", "Applies only to automatic recycling. When off, count meals since the latest birth. When on, use the window below."));
		renderer.Number("fineMealHours", T("精致食物回顾窗口（游戏小时）", "Fine-meal lookback window (game hours)"), ref fineMealHours, enabled: recent);
		renderer.Section("manual", T("主动回收", "Manual recycling"));
		renderer.Check("manualEnabled", T("启用主动回收命令", "Enable the manual recycling command"), ref manualEnabled);
		renderer.Label(T("玩家可命令可控制的鼠族主动回收。可配置的目标限制仅为“捕食双方”中的被捕食者年龄范围；主动回收不受双方身份、捕食者年龄、饥饿、幼鼠分界、禁用目标或普通／精致食物检查限制，也不会因吃饱而停嘴。目标仍须是存活、同地图、非自身、可预约且可达的鼠族。", "Players can order controllable Ratkin to recycle manually. The only configurable target restriction is the victim age range under Participants. Manual orders ignore both roles' status filters, eater age, hunger, young-predator split, forbidden targets and available-food/fine-meal checks, and do not stop at fullness. Targets must still be living Ratkin on the same map, not the eater, reservable and reachable."));
	}

	private void DrawTargets(IRatkinRecycleSettingsRenderer renderer)
	{
		renderer.Section("eater", T("自动回收的捕食者", "Automatic recycling: eaters"));
		renderer.Label(T("本节只影响自动回收。身份按当前状态判定，默认排除自由殖民者。主动回收不受捕食者身份、年龄或下方幼鼠分界限制。", "This section applies only to automatic recycling. Status uses the pawn's current role; free player colonists are excluded by default. Manual orders ignore eater status, age and the young-predator split below."));
		Statuses(renderer, "eater", ref eaterStatuses);
		renderer.Number("minEaterAge", T("最低生理年龄（岁，含）", "Minimum biological age (years, inclusive)"), ref minEaterAge);
		renderer.Check("eaterAgeUnlimited", T("捕食者年龄无上限", "No maximum eater age"), ref eaterAgeUnlimited);
		renderer.Number("maxEaterAge", T("最高生理年龄（岁，不含）", "Maximum biological age (years, exclusive)"), ref maxEaterAge, enabled: !eaterAgeUnlimited);
		renderer.Check("skipDrafted", T("跳过征召中的捕食者", "Skip drafted eaters"), ref skipDrafted);
		renderer.Check("skipMentalState", T("跳过精神崩溃中的捕食者", "Skip eaters in a mental state"), ref skipMentalState);
		renderer.Section("victim", T("被捕食者", "Victims"));
		renderer.Label(T("身份仅限制自动回收；下方被捕食者年龄范围同时适用于自动和主动回收。", "Status filters apply only to automatic recycling; the victim age range below applies to both automatic and manual recycling."));
		Statuses(renderer, "victim", ref victimStatuses);
		renderer.Number("minVictimAge", T("最低生理年龄（岁，含）", "Minimum biological age (years, inclusive)"), ref minVictimAge);
		renderer.Check("victimAgeUnlimited", T("被捕食者年龄无上限", "No maximum victim age"), ref victimAgeUnlimited);
		renderer.Number("maxVictimAge", T("最高生理年龄（岁，不含）", "Maximum biological age (years, exclusive)"), ref maxVictimAge, enabled: !victimAgeUnlimited);
		renderer.Label(T("捕食从地图上未被抱起的目标开始。访客和商队自身的回收行为免除玩家好感惩罚与由此引发的转敌；玩家派系或玩家拘押的捕食者造成伤害时，仍按原版处理。", "Predation starts with an unheld target on the map. Recycling by visitors and traders is exempt from player goodwill penalties and resulting hostility. Damage by player-faction or player-held eaters still follows vanilla rules."));
		renderer.Section("youngPredators", T("自动回收的幼鼠捕食限制", "Automatic recycling: young predators"));
		renderer.Check("limitYoungPredators", T("幼鼠捕食者只能捕食分界年龄以下的幼鼠", "Restrict young eaters to young victims below the split age"), ref limitYoungPredators);
		renderer.Number("splitEggAge", T("大鼠蛋与小鼠蛋的分界（生理年龄，岁）", "Older/younger RatkinYoung split (biological years)"), ref splitEggAge);
		renderer.Number("searchRadius", T("自动回收搜索半径（格）", "Automatic recycling search radius (cells)"), ref searchRadius);
	}

	private void DrawChewing(IRatkinRecycleSettingsRenderer renderer)
	{
		renderer.Section("bites", T("啃食过程", "Bites"));
		renderer.Integer("biteInterval", T("每口间隔（tick，最少 10；1 倍速每秒 60 tick）", "Bite interval (ticks, minimum 10; 60 ticks per second at 1× speed)"), ref biteInterval, 10);
		renderer.Number("biteDamage", T("每口基础伤害（额外随机增加 0–2）", "Base bite damage (plus a random 0–2)"), ref biteDamage);
		renderer.Number("nutritionPerBite", T("每口获得的营养", "Nutrition per bite"), ref nutritionPerBite);
		renderer.Number("stopAtFullness", T("自动回收停嘴饱食度（0–1；0 = 持续啃食）", "Automatic recycling stop fullness (0–1; 0 = keep chewing)"), ref stopAtFullness, 0f, 1f);
		renderer.Number("nutritionOnFinish", T("目标死亡后额外获得的营养", "Extra nutrition when the victim dies"), ref nutritionOnFinish);
		renderer.Check("limbsFirst", T("优先四肢，其次躯干，最后头部", "Prioritize limbs, then torso, then head"), ref limbsFirst);
		renderer.Check("destroyCorpse", T("目标死亡后销毁尸体", "Destroy the corpse after the victim dies"), ref destroyCorpse);
		renderer.Check("leaveMeat", T("目标死亡后留下鼠肉", "Leave Ratkin meat after the victim dies"), ref leaveMeat);
		renderer.Integer("meatCount", T("留下的鼠肉数量（份）", "Meat left behind (units)"), ref meatCount, enabled: leaveMeat);
		renderer.Section("bodyParts", T("允许直接啃咬的天然部位", "Natural parts allowed as direct bite targets"));
		renderer.Label(T("每种部位只列一次。人工部件始终排除。取消某部位只阻止直接选中；啃掉上级部位仍可能连带失去它，伤口也仍可能流血。新模组添加的部位默认未勾选。", "Each part type appears once. Artificial parts are always excluded. Unchecking a part prevents direct targeting; losing a parent part can still remove it, and wounds can still bleed. New modded part types start unchecked."));
		IReadOnlyList<BodyPartDef> parts = RatkinRecyclePolicy.BodyParts;
		if (parts.Count == 0) renderer.Label(T("当前种族范围内没有可列出的部位。请检查高级页中的种族关键字。", "No body parts are available for the selected races. Check the race keywords under Advanced."));
		foreach (BodyPartDef part in parts)
		{
			bool allowed = BodyPartAllowed(part.defName);
			renderer.Check("part:" + part.defName, part.LabelCap, ref allowed, part.defName);
			if (!renderer.IsMeasuring) SetBodyPartAllowed(part.defName, allowed);
		}
	}

	private void DrawMoods(IRatkinRecycleSettingsRenderer renderer)
	{
		renderer.Section("moodStages", T("心情与成长阶段", "Moods and age stages"));
		renderer.Check("babyMoodNeed", T("鼠蛋阶段拥有心情需求条", "Give RatkinYoung a mood need"), ref babyMoodNeed);
		renderer.Number("tierToddlerMax", T("幼儿心情档上限（生理年龄，岁）", "Toddler mood cutoff (biological years)"), ref tierToddlerMax);
		renderer.Number("tierChildMax", T("儿童心情档上限（生理年龄，岁）", "Child mood cutoff (biological years)"), ref tierChildMax);
		renderer.Label(T("以下数值为心情增减：正数提高心情，负数降低心情。", "Mood offsets below: positive values improve mood; negative values lower it."));
		MoodGroup(renderer, "motherOther", T("鼠妈吃非亲生鼠蛋", "Mother eating unrelated RatkinYoung"), "momNonOwn", ref momNonOwnToddler, ref momNonOwnChild, ref momNonOwnAdult);
		MoodGroup(renderer, "motherOwn", T("鼠妈吃亲生鼠蛋", "Mother eating her own RatkinYoung"), "momOwn", ref momOwnToddler, ref momOwnChild, ref momOwnAdult);
		MoodGroup(renderer, "otherEater", T("其他捕食者", "Other eaters"), "other", ref otherToddler, ref otherChild, ref otherAdult);
		MoodGroup(renderer, "eaten", T("被捕食者", "Victims"), "eaten", ref eatenToddler, ref eatenChild, ref eatenAdult);
	}

	private void MoodGroup(IRatkinRecycleSettingsRenderer renderer, string id, string title, string prefix, ref float toddler, ref float child, ref float adult)
	{
		renderer.Section(id, title);
		renderer.Number(prefix + "Toddler", T("幼儿", "Toddler"), ref toddler, float.MinValue);
		renderer.Number(prefix + "Child", T("儿童", "Child"), ref child, float.MinValue);
		renderer.Number(prefix + "Adult", T("成年", "Adult"), ref adult, float.MinValue);
	}

	private void DrawStation(IRatkinRecycleSettingsRenderer renderer)
	{
		renderer.Section("station", T("自动止血装置", "Automatic bleeding-control station"));
		renderer.Check("bleedStationEnabled", T("启用自动止血装置", "Enable the bleeding-control station"), ref bleedStationEnabled);
		renderer.Number("bleedStationRadius", T("治疗半径（格）", "Treatment radius (cells)"), ref bleedStationRadius, 0f, CompAutoStopBleeding.MaximumRadius);
		renderer.Integer("bleedStationInterval", T("治疗间隔（tick；1 倍速每秒 60 tick）", "Treatment interval (ticks; 60 ticks per second at 1× speed)"), ref bleedStationInterval, 1);
		renderer.Number("bleedStationHeal", T("每次愈合的流血伤口严重度", "Bleeding wound severity healed per treatment"), ref bleedStationHeal);
		renderer.Check("bleedStationHealFriendly", T("治疗玩家与友方角色", "Treat player and friendly pawns"), ref bleedStationHealFriendly);
		renderer.Check("bleedStationHealHostile", T("治疗敌对角色", "Treat hostile pawns"), ref bleedStationHealHostile);
		renderer.Check("bleedStationHealWild", T("治疗无派系角色", "Treat factionless pawns"), ref bleedStationHealWild);
		renderer.Check("bleedStationHealAnimals", T("治疗动物", "Treat animals"), ref bleedStationHealAnimals);
	}

	private void DrawAdvanced(IRatkinRecycleSettingsRenderer renderer)
	{
		renderer.Section("scheduling", T("检查频率", "Scheduling"));
		renderer.Integer("checkInterval", T("捕食检查间隔（tick，最少 10）", "Predation check interval (ticks, minimum 10)"), ref checkInterval, 10);
		renderer.Integer("retryCooldown", T("捕食失败后的重试冷却（tick）", "Retry cooldown after failed predation (ticks)"), ref retryCooldown, 1);
		renderer.Section("raceKeywords", T("生效种族", "Affected races"));
		renderer.Label(T("填写种族标识中包含的关键字，以逗号分隔。例如 Ratkin。", "Enter comma-separated keywords contained in the race identifier, such as Ratkin."));
		string previous = raceKeywords;
		renderer.TextField("raceKeywordsField", ref raceKeywords, 400);
		if (!renderer.IsMeasuring && previous != raceKeywords) RatkinRecyclePolicy.InvalidateBodyParts();
		renderer.Check("debugLog", T("输出调试日志", "Enable debug logging"), ref debugLog);
		renderer.Section("reset", T("恢复默认", "Restore defaults"));
		renderer.Label(T("恢复所有六页的默认值，包括年龄、身份、部位和心情。自由殖民者将再次从捕食双方中排除。", "Restore defaults on all six pages, including ages, statuses, body parts and moods. Free player colonists will again be excluded from both roles."));
		if (renderer.Button("resetDefaults", T("恢复全部默认设置", "Restore all default settings"))) ResetDefaults();
	}

	private void Statuses(IRatkinRecycleSettingsRenderer renderer, string prefix, ref RatkinRecycleStatus statuses)
	{
		Status(renderer, prefix, RatkinRecycleStatus.Prisoner, T("殖民地囚犯", "Colony prisoners"), ref statuses);
		Status(renderer, prefix, RatkinRecycleStatus.Slave, T("殖民地奴隶", "Colony slaves"), ref statuses);
		Status(renderer, prefix, RatkinRecycleStatus.Colonist, T("玩家自由殖民者", "Free player colonists"), ref statuses);
		Status(renderer, prefix, RatkinRecycleStatus.FriendlyOutsider, T("非敌对外来者（含访客、商队）", "Non-hostile outsiders (including visitors and traders)"), ref statuses);
		Status(renderer, prefix, RatkinRecycleStatus.HostileOutsider, T("敌对外来者", "Hostile outsiders"), ref statuses);
		Status(renderer, prefix, RatkinRecycleStatus.Factionless, T("无派系角色", "Factionless pawns"), ref statuses);
	}

	private void Status(IRatkinRecycleSettingsRenderer renderer, string prefix, RatkinRecycleStatus flag, string label, ref RatkinRecycleStatus statuses)
	{
		bool active = (statuses & flag) != 0;
		renderer.Check(prefix + ":" + flag, T("自动回收：", "Automatic recycling: ") + label, ref active);
		if (!renderer.IsMeasuring) statuses = active ? statuses | flag : statuses & ~flag;
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

	private sealed class VanillaRenderer : IRatkinRecycleSettingsRenderer
	{
		private readonly RatkinRecycleSettings settings;
		private Listing_Standard list;
		private Action<Listing_Standard, string, float> anchor;
		public bool IsMeasuring { get; private set; }

		public VanillaRenderer(RatkinRecycleSettings settings) => this.settings = settings;

		public void Begin(Listing_Standard listing, Action<Listing_Standard, string, float> focus, bool measure)
		{
			list = listing;
			anchor = focus;
			IsMeasuring = measure;
		}

		public void Section(string id, string label) => RatkinRecycleSettings.Section(list, id, label, anchor, IsMeasuring);
		public void Label(string text) => RatkinRecycleSettings.Label(list, text, IsMeasuring);
		public void Check(string id, string label, ref bool value, string tooltip = null) => RatkinRecycleSettings.Check(list, id, label, ref value, anchor, IsMeasuring, tooltip);
		public void Number(string id, string label, ref float value, float min = 0f, float max = float.MaxValue, bool enabled = true) => settings.Number(list, id, label, ref value, anchor, IsMeasuring, min, max, enabled);
		public void Integer(string id, string label, ref int value, int min = 0, bool enabled = true) => settings.Integer(list, id, label, ref value, anchor, IsMeasuring, min, enabled);

		public void TextField(string id, ref string value, int maxLength)
		{
			if (!IsMeasuring) anchor?.Invoke(list, id, 32f);
			Rect rect = list.GetRect(32f);
			if (!IsMeasuring) value = Widgets.TextField(rect, value ?? "", maxLength);
			list.Gap(8f);
		}

		public bool Button(string id, string label)
		{
			if (!IsMeasuring) anchor?.Invoke(list, id, 32f);
			Rect rect = list.GetRect(32f);
			bool clicked = !IsMeasuring && Widgets.ButtonText(rect, label);
			list.Gap(8f);
			return clicked;
		}
	}
}
