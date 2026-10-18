using System;
using UnityEngine;
using Verse;

namespace RatkinRecycle;

public class RatkinRecycleSettings : ModSettings
{
	public bool enabled = true;

	public bool showLetter = true;

	public bool showStopLetter = true;

	public bool babyMoodNeed = true;

	public bool debugLog;

	public bool allowPlayerFaction = true;

	public bool allowFriendlyFaction = true;

	public bool allowHostileFaction = true;

	public bool allowNoFaction = true;

	public string raceKeywords = "Ratkin";

	public float momHungerPct = 0.5f;

	public float otherHungerPct = 0.3f;

	public float adultAgeYears = 1f;

	public float postpartumHours = 120f;

	public bool momOwnEggFallback = true;

	public int fineMealMode;

	public float fineMealHours = 48f;

	public float maxEggAge = 3f;

	public float splitEggAge = 1f;

	public float minEaterAge = 1f;

	public float searchRadius = 40f;

	public bool skipCarriedTarget = true;

	public bool skipDrafted = true;

	public bool skipMentalState = true;

	public int checkInterval = 120;

	public int retryCooldown = 900;

	public int biteInterval = 90;

	public float biteDamage = 4f;

	public float nutritionPerBite = 0.05f;

	public float stopAtFullness = 0.7f;

	public float nutritionOnFinish = 0.4f;

	public bool limbsFirst = true;

	public bool destroyCorpse = true;

	public bool leaveMeat;

	public int meatCount = 2;

	public float tierToddlerMax = 1f;

	public float tierChildMax = 3f;

	public float momNonOwnToddler = 20f;

	public float momNonOwnChild = 20f;

	public float momNonOwnAdult = 20f;

	public float momOwnToddler = -20f;

	public float momOwnChild = -20f;

	public float momOwnAdult = 15f;

	public float otherToddler = 10f;

	public float otherChild = 10f;

	public float otherAdult = 10f;

	public float eatenToddler = -50f;

	public float eatenChild = -50f;

	public float eatenAdult = -50f;

	public bool bleedStationEnabled = true;

	public float bleedStationRadius = 12f;

	public int bleedStationInterval = 120;

	public float bleedStationHeal = 0.03f;

	public bool bleedStationHealFriendly = true;

	public bool bleedStationHealHostile;

	public bool bleedStationHealWild = true;

	public bool bleedStationHealAnimals = true;

	private Vector2 scrollPos;

	public override void ExposeData()
	{
		Scribe_Values.Look(ref enabled, "enabled", defaultValue: true);
		Scribe_Values.Look(ref showLetter, "showLetter", defaultValue: true);
		Scribe_Values.Look(ref showStopLetter, "showStopLetter", defaultValue: true);
		Scribe_Values.Look(ref babyMoodNeed, "babyMoodNeed", defaultValue: true);
		Scribe_Values.Look(ref debugLog, "debugLog", defaultValue: false);
		Scribe_Values.Look(ref allowPlayerFaction, "allowPlayerFaction", defaultValue: true);
		Scribe_Values.Look(ref allowFriendlyFaction, "allowFriendlyFaction", defaultValue: true);
		Scribe_Values.Look(ref allowHostileFaction, "allowHostileFaction", defaultValue: true);
		Scribe_Values.Look(ref allowNoFaction, "allowNoFaction", defaultValue: true);
		Scribe_Values.Look(ref raceKeywords, "raceKeywords", "Ratkin");
		Scribe_Values.Look(ref momHungerPct, "momHungerPct", 0.5f);
		Scribe_Values.Look(ref otherHungerPct, "otherHungerPct", 0.3f);
		Scribe_Values.Look(ref adultAgeYears, "adultAgeYears", 1f);
		Scribe_Values.Look(ref postpartumHours, "postpartumHours", 120f);
		Scribe_Values.Look(ref momOwnEggFallback, "momOwnEggFallback", defaultValue: true);
		Scribe_Values.Look(ref fineMealMode, "fineMealMode", 0);
		Scribe_Values.Look(ref fineMealHours, "fineMealHours", 48f);
		Scribe_Values.Look(ref maxEggAge, "maxEggAge", 3f);
		Scribe_Values.Look(ref splitEggAge, "splitEggAge", 1f);
		Scribe_Values.Look(ref minEaterAge, "minEaterAge", 1f);
		Scribe_Values.Look(ref searchRadius, "searchRadius", 40f);
		Scribe_Values.Look(ref skipCarriedTarget, "skipCarriedTarget", defaultValue: true);
		Scribe_Values.Look(ref skipDrafted, "skipDrafted", defaultValue: true);
		Scribe_Values.Look(ref skipMentalState, "skipMentalState", defaultValue: true);
		Scribe_Values.Look(ref checkInterval, "checkInterval", 120);
		Scribe_Values.Look(ref retryCooldown, "retryCooldown", 900);
		Scribe_Values.Look(ref biteInterval, "biteInterval", 90);
		Scribe_Values.Look(ref biteDamage, "biteDamage", 4f);
		Scribe_Values.Look(ref nutritionPerBite, "nutritionPerBite", 0.05f);
		Scribe_Values.Look(ref stopAtFullness, "stopAtFullness", 0.7f);
		Scribe_Values.Look(ref nutritionOnFinish, "nutritionOnFinish", 0.4f);
		Scribe_Values.Look(ref limbsFirst, "limbsFirst", defaultValue: true);
		Scribe_Values.Look(ref destroyCorpse, "destroyCorpse", defaultValue: true);
		Scribe_Values.Look(ref leaveMeat, "leaveMeat", defaultValue: false);
		Scribe_Values.Look(ref meatCount, "meatCount", 2);
		Scribe_Values.Look(ref tierToddlerMax, "tierToddlerMax", 1f);
		Scribe_Values.Look(ref tierChildMax, "tierChildMax", 3f);
		Scribe_Values.Look(ref momNonOwnToddler, "momNonOwnToddler", 20f);
		Scribe_Values.Look(ref momNonOwnChild, "momNonOwnChild", 20f);
		Scribe_Values.Look(ref momNonOwnAdult, "momNonOwnAdult", 20f);
		Scribe_Values.Look(ref momOwnToddler, "momOwnToddler", -20f);
		Scribe_Values.Look(ref momOwnChild, "momOwnChild", -20f);
		Scribe_Values.Look(ref momOwnAdult, "momOwnAdult", 15f);
		Scribe_Values.Look(ref otherToddler, "otherToddler", 10f);
		Scribe_Values.Look(ref otherChild, "otherChild", 10f);
		Scribe_Values.Look(ref otherAdult, "otherAdult", 10f);
		Scribe_Values.Look(ref eatenToddler, "eatenToddler", -50f);
		Scribe_Values.Look(ref eatenChild, "eatenChild", -50f);
		Scribe_Values.Look(ref eatenAdult, "eatenAdult", -50f);
		Scribe_Values.Look(ref bleedStationEnabled, "bleedStationEnabled", defaultValue: true);
		Scribe_Values.Look(ref bleedStationRadius, "bleedStationRadius", 12f);
		Scribe_Values.Look(ref bleedStationInterval, "bleedStationInterval", 120);
		Scribe_Values.Look(ref bleedStationHeal, "bleedStationHeal", 0.03f);
		Scribe_Values.Look(ref bleedStationHealFriendly, "bleedStationHealFriendly", defaultValue: true);
		Scribe_Values.Look(ref bleedStationHealHostile, "bleedStationHealHostile", defaultValue: false);
		Scribe_Values.Look(ref bleedStationHealWild, "bleedStationHealWild", defaultValue: true);
		Scribe_Values.Look(ref bleedStationHealAnimals, "bleedStationHealAnimals", defaultValue: true);
	}

	public void ResetDefaults()
	{
		enabled = true;
		showLetter = true;
		showStopLetter = true;
		babyMoodNeed = true;
		debugLog = false;
		allowPlayerFaction = (allowFriendlyFaction = (allowHostileFaction = (allowNoFaction = true)));
		raceKeywords = "Ratkin";
		momHungerPct = 0.5f;
		otherHungerPct = 0.3f;
		adultAgeYears = 1f;
		postpartumHours = 120f;
		momOwnEggFallback = true;
		fineMealMode = 0;
		fineMealHours = 48f;
		maxEggAge = 3f;
		splitEggAge = 1f;
		minEaterAge = 1f;
		searchRadius = 40f;
		skipCarriedTarget = true;
		skipDrafted = true;
		skipMentalState = true;
		checkInterval = 120;
		retryCooldown = 900;
		biteInterval = 90;
		biteDamage = 4f;
		nutritionPerBite = 0.05f;
		stopAtFullness = 0.7f;
		nutritionOnFinish = 0.4f;
		limbsFirst = true;
		destroyCorpse = true;
		leaveMeat = false;
		meatCount = 2;
		tierToddlerMax = 1f;
		tierChildMax = 3f;
		momNonOwnToddler = (momNonOwnChild = (momNonOwnAdult = 20f));
		momOwnToddler = -20f;
		momOwnChild = -20f;
		momOwnAdult = 15f;
		otherToddler = (otherChild = (otherAdult = 10f));
		eatenToddler = (eatenChild = (eatenAdult = -50f));
		bleedStationEnabled = true;
		bleedStationRadius = 12f;
		bleedStationInterval = 120;
		bleedStationHeal = 0.03f;
		bleedStationHealFriendly = true;
		bleedStationHealHostile = false;
		bleedStationHealWild = true;
		bleedStationHealAnimals = true;
	}

	public void DoWindowContents(Rect inRect)
	{
		if (inRect.width <= 0f || inRect.height <= 0f)
		{
			return;
		}
		float a = MeasureHeight();
		float width = Mathf.Max(10f, inRect.width - 16f);
		float height = Mathf.Max(10f, Mathf.Max(a, inRect.height - 40f));
		Widgets.BeginScrollView(viewRect: new Rect(0f, 0f, width, height), outRect: inRect, scrollPosition: ref scrollPos);
		try
		{
			float y = 0f;
			DrawAll(ref y, dry: false);
		}
		catch (Exception ex)
		{
			Log.Error("[RatkinRecycle] 设置界面绘制异常: " + ex);
		}
		finally
		{
			Widgets.EndScrollView();
		}
	}

	private float MeasureHeight()
	{
		float y = 0f;
		DrawAll(ref y, dry: true);
		return y + 10f;
	}

	private void DrawAll(ref float y, bool dry)
	{
		SectionHeader(ref y, "主开关", dry);
		y = CheckLine(ref y, "启用本模组", ref enabled, dry);
		y = CheckLine(ref y, "鼠蛋被吃掉后,左上角弹出信件提示", ref showLetter, dry);
		y = CheckLine(ref y, "啃饱停嘴(鼠蛋还活着)时也弹信件提醒", ref showStopLetter, dry);
		y = CheckLine(ref y, "鼠蛋(0-3岁)拥有真正的心情需求条,被吃心情显示在需求面板", ref babyMoodNeed, dry);
		y = CheckLine(ref y, "调试日志", ref debugLog, dry);
		SectionHeader(ref y, "触发阈值", dry);
		y = PctSlider(ref y, "鼠妈饥饿阈值(饱食度低于该比例会吃崽)", ref momHungerPct, dry);
		y = PctSlider(ref y, "其他鼠族饥饿阈值", ref otherHungerPct, dry);
		y = FloatSlider(ref y, "鼠妈成年判定的最小年龄(岁,0=不限制)", ref adultAgeYears, 0f, 13f, dry);
		y = FloatSlider(ref y, "产后窗口(小时,生产后多久内算鼠妈)", ref postpartumHours, 1f, 720f, dry);
		y = CheckLine(ref y, "当前仍有0-3岁亲生鼠蛋时,也视为鼠妈", ref momOwnEggFallback, dry);
		y = IntSlider(ref y, "鼠妈吃崽的精致食物判定:0=生产后未吃过精致+;1=最近N小时内没吃", ref fineMealMode, 0, 1, dry);
		if (fineMealMode > 0)
		{
			y = FloatSlider(ref y, "    未吃精致+食物的窗口(小时)", ref fineMealHours, 1f, 240f, dry);
		}
		SectionHeader(ref y, "目标范围", dry);
		y = FloatSlider(ref y, "鼠蛋最大年龄(岁,超过则不再被捕食)", ref maxEggAge, 0.5f, 13f, dry);
		y = FloatSlider(ref y, "大蛋捕食小蛋的分界(岁)", ref splitEggAge, 0.1f, 6f, dry);
		y = FloatSlider(ref y, "允许捕食的最低年龄(岁)", ref minEaterAge, 0.1f, 13f, dry);
		y = FloatSlider(ref y, "搜索半径(格)", ref searchRadius, 5f, 200f, dry);
		y = CheckLine(ref y, "跳过被抱着/背着的鼠蛋", ref skipCarriedTarget, dry);
		y = CheckLine(ref y, "跳过征召中的捕食者", ref skipDrafted, dry);
		y = CheckLine(ref y, "跳过精神崩溃中的捕食者", ref skipMentalState, dry);
		y = IntSlider(ref y, "全局扫描间隔(tick,60=1秒)", ref checkInterval, 30, 600, dry);
		y = IntSlider(ref y, "单鼠失败后的重试冷却(tick)", ref retryCooldown, 60, 7200, dry);
		SectionHeader(ref y, "成长阶段划分(心情档次:幼儿/儿童/成年)", dry);
		y = FloatSlider(ref y, "幼儿上限(岁)", ref tierToddlerMax, 0.1f, 6f, dry);
		y = FloatSlider(ref y, "儿童上限(岁)", ref tierChildMax, 0.5f, 13f, dry);
		SectionHeader(ref y, "啃食过程", dry);
		y = IntSlider(ref y, "每口间隔(tick,60=1秒)", ref biteInterval, 15, 300, dry);
		y = FloatSlider(ref y, "每口伤害(随机附赠0~2)", ref biteDamage, 0.5f, 30f, dry);
		y = FloatSlider(ref y, "每口获得的营养", ref nutritionPerBite, 0f, 0.2f, dry);
		y = PctSlider(ref y, "饱食度超过该比例就停嘴(0=不停止,会啃完)", ref stopAtFullness, dry, 0f);
		y = FloatSlider(ref y, "真正吃完(鼠蛋死亡)后一次性获得的营养", ref nutritionOnFinish, 0f, 1f, dry);
		y = CheckLine(ref y, "优先啃四肢,躯干其次,头部最后", ref limbsFirst, dry);
		y = CheckLine(ref y, "吃完后毁掉尸体(吃得很干净)", ref destroyCorpse, dry);
		y = CheckLine(ref y, "吃完后原地留下鼠肉(回收)", ref leaveMeat, dry);
		if (leaveMeat)
		{
			y = IntSlider(ref y, "    留下的鼠肉数量(份)", ref meatCount, 0, 20, dry);
		}
		SectionHeader(ref y, "阵营与种族", dry);
		y = CheckLine(ref y, "允许玩家阵营捕食", ref allowPlayerFaction, dry);
		y = CheckLine(ref y, "允许友方阵营捕食", ref allowFriendlyFaction, dry);
		y = CheckLine(ref y, "允许敌对阵营捕食", ref allowHostileFaction, dry);
		y = CheckLine(ref y, "允许无阵营(野生/遗迹)捕食", ref allowNoFaction, dry);
		Label(ref y, "生效种族(ThingDef.defName 包含即匹配,逗号分隔):", dry);
		y = TextLine(ref y, "", ref raceKeywords, 2f, dry);
		SectionHeader(ref y, "自动止血装置", dry);
		y = CheckLine(ref y, "启用自动止血装置", ref bleedStationEnabled, dry);
		y = FloatSlider(ref y, "治疗半径(格)", ref bleedStationRadius, 2f, 40f, dry);
		y = IntSlider(ref y, "治疗间隔(tick,60=1秒)", ref bleedStationInterval, 20, 600, dry);
		y = FloatSlider(ref y, "每间隔愈合的伤口量(流血伤口严重度)", ref bleedStationHeal, 0.001f, 0.2f, dry);
		y = CheckLine(ref y, "治疗玩家/友方单位", ref bleedStationHealFriendly, dry);
		y = CheckLine(ref y, "治疗敌对单位", ref bleedStationHealHostile, dry);
		y = CheckLine(ref y, "治疗无阵营(野生)单位", ref bleedStationHealWild, dry);
		y = CheckLine(ref y, "治疗动物", ref bleedStationHealAnimals, dry);
		SectionHeader(ref y, "心情数值", dry);
		Label(ref y, "—— 鼠妈吃非亲生鼠蛋 ——", dry);
		y = FloatSlider(ref y, "幼儿", ref momNonOwnToddler, -100f, 100f, dry);
		y = FloatSlider(ref y, "儿童", ref momNonOwnChild, -100f, 100f, dry);
		y = FloatSlider(ref y, "成年", ref momNonOwnAdult, -100f, 100f, dry);
		Label(ref y, "—— 鼠妈吃亲生鼠蛋 ——", dry);
		y = FloatSlider(ref y, "幼儿", ref momOwnToddler, -100f, 100f, dry);
		y = FloatSlider(ref y, "儿童", ref momOwnChild, -100f, 100f, dry);
		y = FloatSlider(ref y, "成年", ref momOwnAdult, -100f, 100f, dry);
		Label(ref y, "—— 其他鼠族吃鼠蛋 ——", dry);
		y = FloatSlider(ref y, "幼儿", ref otherToddler, -100f, 100f, dry);
		y = FloatSlider(ref y, "儿童", ref otherChild, -100f, 100f, dry);
		y = FloatSlider(ref y, "成年", ref otherAdult, -100f, 100f, dry);
		Label(ref y, "—— 被吃者 ——", dry);
		y = FloatSlider(ref y, "幼儿", ref eatenToddler, -100f, 100f, dry);
		y = FloatSlider(ref y, "儿童", ref eatenChild, -100f, 100f, dry);
		y = FloatSlider(ref y, "成年", ref eatenAdult, -100f, 100f, dry);
		SectionHeader(ref y, "其他", dry);
		if (!dry && Widgets.ButtonText(new Rect(0f, y, 160f, 28f), "恢复默认设置"))
		{
			ResetDefaults();
		}
		y += 40f;
	}

	private void SectionHeader(ref float y, string text, bool dry)
	{
		if (!dry)
		{
			GUI.color = new Color(0.8f, 0.85f, 1f);
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(0f, y, 600f, 28f), text);
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
		}
		y += 32f;
	}

	private void Label(ref float y, string text, bool dry)
	{
		if (!dry)
		{
			Widgets.Label(new Rect(0f, y, 600f, 22f), text);
		}
		y += 24f;
	}

	private float FloatSlider(ref float y, string label, ref float value, float min, float max, bool dry)
	{
		if (!dry)
		{
			Widgets.Label(new Rect(0f, y, 440f, 24f), label);
			Widgets.Label(new Rect(450f, y, 70f, 24f), value.ToString("0.##"));
			value = Widgets.HorizontalSlider(new Rect(520f, y + 6f, 250f, 20f), value, min, max, middleAlignment: false, null, null, null, 0.05f);
		}
		y += 28f;
		return y;
	}

	private float PctSlider(ref float y, string label, ref float value, bool dry, float min = 0.05f)
	{
		if (!dry)
		{
			Widgets.Label(new Rect(0f, y, 440f, 24f), label);
			Widgets.Label(new Rect(450f, y, 70f, 24f), (value <= 0f) ? "关" : ((value * 100f).ToString("0") + "%"));
			value = Widgets.HorizontalSlider(new Rect(520f, y + 6f, 250f, 20f), value, min, 1f, middleAlignment: false, null, null, null, 0.01f);
		}
		y += 28f;
		return y;
	}

	private float IntSlider(ref float y, string label, ref int value, int min, int max, bool dry)
	{
		if (!dry)
		{
			Widgets.Label(new Rect(0f, y, 440f, 24f), label);
			Widgets.Label(new Rect(450f, y, 70f, 24f), value.ToString());
			value = (int)Widgets.HorizontalSlider(new Rect(520f, y + 6f, 250f, 20f), value, min, max, middleAlignment: false, null, null, null, 1f);
		}
		y += 28f;
		return y;
	}

	private float CheckLine(ref float y, string label, ref bool value, bool dry)
	{
		if (!dry)
		{
			Widgets.CheckboxLabeled(new Rect(0f, y, 560f, 24f), label, ref value);
		}
		y += 26f;
		return y;
	}

	private float TextLine(ref float y, string label, ref string value, float height, bool dry)
	{
		if (!dry)
		{
			if (!label.NullOrEmpty())
			{
				Widgets.Label(new Rect(0f, y, 220f, 22f), label);
			}
			Rect rect = new Rect(label.NullOrEmpty() ? 0f : 220f, y, 470f, 20f * height);
			value = Widgets.TextField(rect, value, 400);
		}
		y += 20f * height + 6f;
		return y;
	}
}
