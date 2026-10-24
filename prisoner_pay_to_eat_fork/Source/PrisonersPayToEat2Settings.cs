using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace PrisonersPayToEat2
{
    public class PrisonersPayToEat2Settings : ModSettings
    {
        // Global toggles & multipliers
        public bool enableOrganHarvest = true;
        public bool organSaleAffectsGoodwill = true;
        public float foodPriceMultiplier = 1.0f;
        public float wageMultiplier = 1.0f;
        public float silverToTicketRate = 1.0f;
        public int startingTicketsPerPrisoner = 3;
        public bool ignoreDuringRiot = true;
        // Credit applies to consumption; changing its limit never forgives existing debt.
        public bool allowMealDebt = true;
        public float mealDebtFloor = -100f;
        public float organSaleTriggerBalance = 0f;
        public bool logVerbose = false;

        // Ransom (赎身): a prisoner with enough tickets (and enough time served) may ask to buy
        // freedom; the player approves, the prisoner pays, and is released automatically.
        public bool enableRansom = true;
        public float ransomTicketCost = 1000f;
        public float ransomMinDays = 0f;

        // 儿童父母代付：儿童囚犯（Biotech 13 岁以下）吃饭时可由在押的父母代付饭票。
        // 扣费模式见 ChildPayMode：先自己 / 先父母 / 合并钱包（家庭按比例分摊）。
        public bool enableChildParentPay = true;
        public ChildPayMode childPayMode = ChildPayMode.OwnFirst;

        // Custom display name for the meal ticket currency (empty = default localized name).
        public string customTicketName = "";

        // Hourly meal-ticket wage per WorkTypeDef (work eligibility is decided by Prison Labor).
        public float defaultWagePerHour = 2f;
        public Dictionary<string, float> workTypeWages = new Dictionary<string, float>();

        // Piece-rate ("按量计费"): master switch + per-work-type flag + per-unit wage.
        public bool enablePieceRate = false;
        public List<string> pieceRateWorkTypes = new List<string>();               // flagged as piece-rate
        public Dictionary<string, float> workTypePieceWages = new Dictionary<string, float>();

        public bool enableIndividualWorkGiverBilling = false;
        public Dictionary<string, float> workGiverWages = new Dictionary<string, float>();
        public Dictionary<string, float> workGiverPieceWages = new Dictionary<string, float>();
        public Dictionary<string, bool> workGiverPieceModes = new Dictionary<string, bool>();

        // Per-foodDef custom price override (absolute ticket cost per eaten unit; supports decimals).
        public Dictionary<string, float> customFoodPrices = new Dictionary<string, float>();

        // ============ 囚犯社会行为（借款 / 抢劫 / 乞讨） ============
        public bool enableLoans = true;
        public float loanTriggerBalance = 10f;        // 余额低于此值会发起借款/乞讨/抢劫
        public float loanCooldownDays = 1f;           // 借款冷却（游戏天）
        public float maxLoanAmount = 50f;             // 单次借款/玩家借贷上限
        public float loanInterestBase = 0.3f;         // 好感度 0 时的利率
        public float loanInterestPerOpinion = 0.003f; // 每点好感度降低的利率
        public float loanInterestMin = 0f;
        public float loanInterestMax = 1f;
        public bool allowPlayerLoans = false;         // 允许囚犯向玩家借（逐笔弹窗批准）
        public bool badDebtMoodPenalty = true;        // 坏账时贷方心情减益

        public bool enableRobbery = true;
        public float robberyCooldownDays = 3f;        // 抢劫冷却（游戏天）——低频
        public float robberyHungerThreshold = 0.4f;   // 食物水平低于此值（饿急眼）才考虑抢
        public float robberyTakePercent = 0.3f;       // 抢走目标余额的比例

        public bool enableBegging = true;
        public float beggingCooldownDays = 1f;        // 乞讨冷却（游戏天）
        public float beggingAmountMin = 1f;
        public float beggingAmountMax = 3f;
        public float beggingChanceBase = 0.35f;       // 好感度 0 时的乞讨成功率
        public float beggingChancePerOpinion = 0.004f; // 每点好感度增加的成功率

        // ============ 内置囚犯劳工系统（v2.3+，安装了 Prison Labor 时自动停用） ============
        public bool enableBuiltinPrisonLabor = true;  // 内置劳工系统总开关
        public bool prisonerWorkDefaultOn = true;     // 新囚犯默认强制工作（每囚犯可用三态覆盖）
        public bool enableMotivation = true;          // 动机系统：无人看管/饥饿时动机下降，过低怠工
        public float motivationWorkThreshold = 0.2f;  // 动机低于此值拒绝工作（怠工）
        public List<string> prisonerAllowedWorkTypes = PrisonerWorkSystem.DefaultWorkTypes();

        public bool forbidPrisonerMedicalRest = false;
        public bool forbidNonCriticalPrisonerMedicalRest = false;
        public float criticalDiseaseThreshold = 0.9f;

        // ============ 派系索要（通讯台）：向囚犯所属派系索要白银，白银空投、囚犯加饭票 ============
        public bool enableFactionRequest = true;        // 总开关
        public bool enableCellAutoRequest = true;       // 牢房内有通讯台时，没钱囚犯自己走过去索要
        public float requestSilverMinFactor = 0.1f;     // 可索要范围下限 = 囚犯市场价值 × 此系数
        public float requestSilverMaxFactor = 0.3f;     // 可索要范围上限
        public float requestBaseChance = 0.5f;          // 基础成功率
        public float requestChancePerSocial = 0.02f;    // 每点社交技能的成功率加成
        public float requestChancePerValue = 0.0001f;   // 每点囚犯市场价值的成功率加成
        public float requestAmountPenalty = 0.3f;       // 按索要额占上限比例扣减的成功率
        public float requestCooldownDays = 1f;          // 每名囚犯的索要冷却（游戏天）
        public float requestTriggerBalance = 5f;        // 自动索要触发余额（低于此值才会去）

        // ============ 囚犯娱乐（娱乐需求条 + 娱乐费） ============
        public bool enablePrisonerJoyNeed = true;       // 囚犯娱乐需求条（原版囚犯没有这条）
        public bool enableJoyFees = true;               // 囚犯使用娱乐建筑收费
        public float joyFeePerUse = 50f;                // 默认娱乐费（白银/次，按汇率折算饭票）
        public Dictionary<string, float> customJoyFees = new Dictionary<string, float>(); // 按建筑单独定价

        // ============ 卫生费（Dubs Bad Hygiene）：上厕所/洗澡按消耗的水电收费，付不起就禁用 ============
        public bool enableHygieneFees = true;           // 卫生费总开关
        public float waterPricePerLiter = 0.2f;         // 水单价（饭票/升）
        public float powerPricePerWd = 0.05f;           // 电单价（饭票/Wd）

        // ============ 免付费（全局内容豁免；每囚犯按类别豁免在配置囚犯窗口） ============
        public bool freeAllFood = false;                // 吃任何东西免费
        public bool freeJoyAll = false;                 // 所有囚犯免娱乐费
        public bool freeHygieneAll = false;             // 所有囚犯免卫生费
        // 指定食物免费清单（ThingDef.defName；尸体/营养膏等任何食物都可加入，
        // 在"食物价格"页勾选）。例如加入 Corpse_Human 即"吃尸体免费"。
        public List<string> freeFoodItems = new List<string>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableOrganHarvest, "enableOrganHarvest", true);
            Scribe_Values.Look(ref organSaleAffectsGoodwill, "organSaleAffectsGoodwill", true);
            Scribe_Values.Look(ref foodPriceMultiplier, "foodPriceMultiplier", 1.0f);
            Scribe_Values.Look(ref wageMultiplier, "wageMultiplier", 1.0f);
            Scribe_Values.Look(ref defaultWagePerHour, "defaultWagePerHour", 2f);
            Scribe_Values.Look(ref silverToTicketRate, "silverToTicketRate", 1.0f);
            Scribe_Values.Look(ref startingTicketsPerPrisoner, "startingTicketsPerPrisoner", 3);
            Scribe_Values.Look(ref ignoreDuringRiot, "ignoreDuringRiot", true);
            Scribe_Values.Look(ref allowMealDebt, "allowMealDebt", true);
            Scribe_Values.Look(ref mealDebtFloor, "mealDebtFloor", -100f);
            Scribe_Values.Look(ref organSaleTriggerBalance, "organSaleTriggerBalance", 0f);
            Scribe_Values.Look(ref logVerbose, "logVerbose", false);
            Scribe_Values.Look(ref enableRansom, "enableRansom", true);
            Scribe_Values.Look(ref ransomTicketCost, "ransomTicketCost", 1000f);
            Scribe_Values.Look(ref ransomMinDays, "ransomMinDays", 0f);
            Scribe_Values.Look(ref enableChildParentPay, "enableChildParentPay", true);
            Scribe_Values.Look(ref childPayMode, "childPayMode", ChildPayMode.OwnFirst);
            Scribe_Values.Look(ref customTicketName, "customTicketName", "");
            Scribe_Collections.Look(ref workTypeWages, "workTypeWages", LookMode.Value, LookMode.Value);
            if (workTypeWages == null) workTypeWages = new Dictionary<string, float>();
            Scribe_Values.Look(ref enablePieceRate, "enablePieceRate", false);
            Scribe_Collections.Look(ref pieceRateWorkTypes, "pieceRateWorkTypes", LookMode.Value);
            if (pieceRateWorkTypes == null) pieceRateWorkTypes = new List<string>();
            Scribe_Collections.Look(ref workTypePieceWages, "workTypePieceWages", LookMode.Value, LookMode.Value);
            if (workTypePieceWages == null) workTypePieceWages = new Dictionary<string, float>();
            Scribe_Values.Look(ref enableIndividualWorkGiverBilling, "enableIndividualWorkGiverBilling", false);
            Scribe_Collections.Look(ref workGiverWages, "workGiverWages", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref workGiverPieceWages, "workGiverPieceWages", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref workGiverPieceModes, "workGiverPieceModes", LookMode.Value, LookMode.Value);
            workGiverWages ??= new Dictionary<string, float>();
            workGiverPieceWages ??= new Dictionary<string, float>();
            workGiverPieceModes ??= new Dictionary<string, bool>();
            Scribe_Collections.Look(ref customFoodPrices, "customFoodPrices", LookMode.Value, LookMode.Value);
            if (customFoodPrices == null) customFoodPrices = new Dictionary<string, float>();
            Scribe_Values.Look(ref enableLoans, "enableLoans", true);
            Scribe_Values.Look(ref loanTriggerBalance, "loanTriggerBalance", 10f);
            Scribe_Values.Look(ref loanCooldownDays, "loanCooldownDays", 1f);
            Scribe_Values.Look(ref maxLoanAmount, "maxLoanAmount", 50f);
            Scribe_Values.Look(ref loanInterestBase, "loanInterestBase", 0.3f);
            Scribe_Values.Look(ref loanInterestPerOpinion, "loanInterestPerOpinion", 0.003f);
            Scribe_Values.Look(ref loanInterestMin, "loanInterestMin", 0f);
            Scribe_Values.Look(ref loanInterestMax, "loanInterestMax", 1f);
            Scribe_Values.Look(ref allowPlayerLoans, "allowPlayerLoans", false);
            Scribe_Values.Look(ref badDebtMoodPenalty, "badDebtMoodPenalty", true);
            Scribe_Values.Look(ref enableRobbery, "enableRobbery", true);
            Scribe_Values.Look(ref robberyCooldownDays, "robberyCooldownDays", 3f);
            Scribe_Values.Look(ref robberyHungerThreshold, "robberyHungerThreshold", 0.4f);
            Scribe_Values.Look(ref robberyTakePercent, "robberyTakePercent", 0.3f);
            Scribe_Values.Look(ref enableBegging, "enableBegging", true);
            Scribe_Values.Look(ref beggingCooldownDays, "beggingCooldownDays", 1f);
            Scribe_Values.Look(ref beggingAmountMin, "beggingAmountMin", 1f);
            Scribe_Values.Look(ref beggingAmountMax, "beggingAmountMax", 3f);
            Scribe_Values.Look(ref beggingChanceBase, "beggingChanceBase", 0.35f);
            Scribe_Values.Look(ref beggingChancePerOpinion, "beggingChancePerOpinion", 0.004f);
            Scribe_Values.Look(ref enableBuiltinPrisonLabor, "enableBuiltinPrisonLabor", true);
            Scribe_Values.Look(ref prisonerWorkDefaultOn, "prisonerWorkDefaultOn", true);
            Scribe_Values.Look(ref enableMotivation, "enableMotivation", true);
            Scribe_Values.Look(ref motivationWorkThreshold, "motivationWorkThreshold", 0.2f);
            Scribe_Collections.Look(ref prisonerAllowedWorkTypes, "prisonerAllowedWorkTypes", LookMode.Value);
            if (prisonerAllowedWorkTypes == null) prisonerAllowedWorkTypes = PrisonerWorkSystem.DefaultWorkTypes();
            Scribe_Values.Look(ref forbidPrisonerMedicalRest, "forbidPrisonerMedicalRest", false);
            Scribe_Values.Look(ref forbidNonCriticalPrisonerMedicalRest, "forbidNonCriticalPrisonerMedicalRest", false);
            Scribe_Values.Look(ref criticalDiseaseThreshold, "criticalDiseaseThreshold", 0.9f);
            criticalDiseaseThreshold = float.IsFinite(criticalDiseaseThreshold)
                ? Mathf.Clamp01(criticalDiseaseThreshold) : 0.9f;
            Scribe_Values.Look(ref enableFactionRequest, "enableFactionRequest", true);
            Scribe_Values.Look(ref enableCellAutoRequest, "enableCellAutoRequest", true);
            Scribe_Values.Look(ref requestSilverMinFactor, "requestSilverMinFactor", 0.1f);
            Scribe_Values.Look(ref requestSilverMaxFactor, "requestSilverMaxFactor", 0.3f);
            Scribe_Values.Look(ref requestBaseChance, "requestBaseChance", 0.5f);
            Scribe_Values.Look(ref requestChancePerSocial, "requestChancePerSocial", 0.02f);
            Scribe_Values.Look(ref requestChancePerValue, "requestChancePerValue", 0.0001f);
            Scribe_Values.Look(ref requestAmountPenalty, "requestAmountPenalty", 0.3f);
            Scribe_Values.Look(ref requestCooldownDays, "requestCooldownDays", 1f);
            Scribe_Values.Look(ref requestTriggerBalance, "requestTriggerBalance", 5f);
            Scribe_Values.Look(ref enablePrisonerJoyNeed, "enablePrisonerJoyNeed", true);
            Scribe_Values.Look(ref enableJoyFees, "enableJoyFees", true);
            Scribe_Values.Look(ref joyFeePerUse, "joyFeePerUse", 50f);
            Scribe_Collections.Look(ref customJoyFees, "customJoyFees", LookMode.Value, LookMode.Value);
            if (customJoyFees == null) customJoyFees = new Dictionary<string, float>();
            Scribe_Values.Look(ref enableHygieneFees, "enableHygieneFees", true);
            Scribe_Values.Look(ref waterPricePerLiter, "waterPricePerLiter", 0.2f);
            Scribe_Values.Look(ref powerPricePerWd, "powerPricePerWd", 0.05f);
            Scribe_Values.Look(ref freeAllFood, "freeAllFood", false);
            Scribe_Values.Look(ref freeJoyAll, "freeJoyAll", false);
            Scribe_Values.Look(ref freeHygieneAll, "freeHygieneAll", false);
            Scribe_Collections.Look(ref freeFoodItems, "freeFoodItems", LookMode.Value);
            if (freeFoodItems == null) freeFoodItems = new List<string>();
        }

        /// <summary>Hourly wage for a given WorkTypeDef.defName; falls back to defaultWagePerHour.</summary>
        public float GetWageForWorkType(string defName)
        {
            if (defName != null && workTypeWages.TryGetValue(defName, out float v)) return v;
            return defaultWagePerHour;
        }

        // ================= Settings window: self-drawn tab bar + per-tab scrolling =================

        private int _curTab; // 0=General, 1=WorkTypeWages, 2=FoodPrices, 3=Social, 4=PrisonWork, 5=Request, 6=JoyHygiene

        public void DoWindowContents(Rect inRect)
        {
            const float tabBarH = 38f;
            string[] tabLabels =
            {
                "PPTE2_TabGeneral".Translate(),
                "PPTE2_TabWorkWages".Translate(),
                "PPTE2_TabFoodPrices".Translate(),
                "PPTE2_TabSocial".Translate(),
                "PPTE2_TabPrisonWork".Translate(),
                "PPTE2_TabRequest".Translate(),
                "PPTE2_TabJoyHygiene".Translate()
            };

            float tabW = inRect.width / tabLabels.Length;
            for (int i = 0; i < tabLabels.Length; i++)
            {
                var btn = new Rect(inRect.x + i * tabW + 2f, inRect.y, tabW - 4f, tabBarH - 4f);
                bool active = _curTab == i;
                Color saved = GUI.color;
                if (active) GUI.color = new Color(1f, 0.85f, 0.45f); // highlight selected tab
                if (Widgets.ButtonText(btn, tabLabels[i])) _curTab = i;
                GUI.color = saved;
                if (active)
                {
                    Widgets.DrawBoxSolidWithOutline(
                        new Rect(btn.x, btn.yMax - 2f, btn.width, 3f),
                        new Color(1f, 0.85f, 0.45f), new Color(1f, 0.85f, 0.45f), 0);
                }
            }
            Widgets.DrawLineHorizontal(inRect.x, inRect.y + tabBarH, inRect.width);

            var content = new Rect(inRect.x, inRect.y + tabBarH + 8f, inRect.width,
                inRect.height - tabBarH - 8f);

            DrawSettingsPage(_curTab, content);
        }

        internal void DrawSettingsPage(int page, Rect content)
        {
            switch (page)
            {
                case 0: DrawGeneralTab(content); break;
                case 1: DrawWorkTypeWagesTab(content); break;
                case 2: DrawFoodPricesTab(content); break;
                case 3: DrawSocialTab(content); break;
                case 4: DrawPrisonWorkTab(content); break;
                case 5: DrawRequestTab(content); break;
                default: DrawJoyHygieneTab(content); break;
            }
        }

        // ================= Tab 5: 囚犯工作（内置劳工系统） =================

        private Vector2 _prisonWorkScroll = Vector2.zero;

        private void DrawPrisonWorkTab(Rect rect)
        {
            EnsureWorkTypesCached();
            float width = rect.width - 20f;
            float contentHeight;
            if (PrisonLaborBridge.Present)
                contentHeight = 4f + Text.CalcHeight("PPTE2_BuiltinLaborDisabledByPL".Translate(), width) + 12f;
            else
            {
                int rows = 0;
                foreach (var wt in _cachedWorkTypes)
                    if (wt.visible && !PrisonerWorkSystem.NeverAllowedWorkTypes.Contains(wt.defName)) rows++;
                contentHeight = 4f + 90f + (enableMotivation ? 54f : 0f) + 16f + 32f
                    + rows * 30f + 12f + Text.CalcHeight("PPTE2_PrisonWorkHint".Translate(), width);
            }
            contentHeight += MedicalRestControlsHeight(width);
            var view = new Rect(0f, 0f, width, Mathf.Max(rect.height, contentHeight));
            Widgets.BeginScrollView(rect, ref _prisonWorkScroll, view);
            float colW = (view.width - 40f) / 2f;
            float ly = 4f;
            DrawMedicalRestControls(width, ref ly);

            if (PrisonLaborBridge.Present)
            {
                // Prison Labor 在场：内置系统整体停用，这里只做说明
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                float h = Text.CalcHeight("PPTE2_BuiltinLaborDisabledByPL".Translate(), view.width);
                Widgets.Label(new Rect(0f, ly, view.width, h), "PPTE2_BuiltinLaborDisabledByPL".Translate());
                GUI.color = Color.white;
                ly += h + 12f;
            }
            else
            {
                bool prev = enableBuiltinPrisonLabor;
                DrawCheckboxRow(0f, ref ly, "PPTE2_EnableBuiltinLabor".Translate(), ref enableBuiltinPrisonLabor);
                if (enableBuiltinPrisonLabor && !prev) PrisonerWorkSystem.OnAllowedTypesChanged();
                DrawCheckboxRow(0f, ref ly, "PPTE2_PrisonerWorkDefaultOn".Translate(), ref prisonerWorkDefaultOn);
                DrawCheckboxRow(0f, ref ly, "PPTE2_EnableMotivation".Translate(), ref enableMotivation);
                if (enableMotivation)
                    DrawSliderRow(0f, ref ly, colW,
                        "PPTE2_MotivationThreshold".Translate(motivationWorkThreshold.ToStringPercent()),
                        ref motivationWorkThreshold, 0f, 0.5f);
                ly += 16f;

                // 允许工种列表
                Widgets.Label(new Rect(0f, ly + 2f, colW, 24f), "PPTE2_AllowedWorkTypesTitle".Translate());
                if (Widgets.ButtonText(new Rect(view.width - 160f, ly, 160f, 26f), "PPTE2_ResetDefaults".Translate()))
                {
                    prisonerAllowedWorkTypes = PrisonerWorkSystem.DefaultWorkTypes();
                    PrisonerWorkSystem.OnAllowedTypesChanged();
                }
                ly += 32f;
                EnsureWorkTypesCached();
                bool changed = false;
                foreach (var wt in _cachedWorkTypes)
                {
                    if (!wt.visible || PrisonerWorkSystem.NeverAllowedWorkTypes.Contains(wt.defName)) continue;
                    bool on = prisonerAllowedWorkTypes.Contains(wt.defName);
                    bool newOn = on;
                    DrawCheckboxRow(0f, ref ly, wt.labelShort ?? wt.defName, ref newOn);
                    if (newOn == on) continue;
                    changed = true;
                    if (newOn) prisonerAllowedWorkTypes.Add(wt.defName);
                    else prisonerAllowedWorkTypes.Remove(wt.defName);
                }
                if (changed) PrisonerWorkSystem.OnAllowedTypesChanged();

                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                float hintH = Text.CalcHeight("PPTE2_PrisonWorkHint".Translate(), view.width);
                Widgets.Label(new Rect(0f, ly + 12f, view.width, hintH), "PPTE2_PrisonWorkHint".Translate());
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();
        }

        // ================= Tab 4: 囚犯社会行为（借款/抢劫/乞讨） =================

        private Vector2 _socialScroll = Vector2.zero;

        private void DrawSocialTab(Rect rect)
        {
            // Content: 5 checkboxes * 30 + 15 sliders * 54 + auto-height hint = ~1050
            var view = new Rect(0f, 0f, rect.width - 20f, 1150f);
            Widgets.BeginScrollView(rect, ref _socialScroll, view);

            float colW = (view.width - 40f) / 2f;

            // left column: feature toggles
            float ly = 4f;
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableLoans".Translate(), ref enableLoans);
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableRobbery".Translate(), ref enableRobbery);
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableBegging".Translate(), ref enableBegging);
            DrawCheckboxRow(0f, ref ly, "PPTE2_AllowPlayerLoans".Translate(), ref allowPlayerLoans);
            DrawCheckboxRow(0f, ref ly, "PPTE2_BadDebtPenalty".Translate(), ref badDebtMoodPenalty);
            ly += 20f; // breathing room

            // loan sliders (left)
            DrawSliderRow(0f, ref ly, colW, "PPTE2_LoanTriggerBalance".Translate(loanTriggerBalance.ToString("0.##")),
                ref loanTriggerBalance, 0f, 100f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_LoanCooldown".Translate(loanCooldownDays.ToString("0.#")),
                ref loanCooldownDays, 0f, 10f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_MaxLoanAmount".Translate(maxLoanAmount.ToString("0.##")),
                ref maxLoanAmount, 1f, 500f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_LoanInterestBase".Translate(loanInterestBase.ToString("0%")),
                ref loanInterestBase, 0f, 1f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_LoanInterestPerOpinion".Translate((loanInterestPerOpinion * 100f).ToString("0.0")),
                ref loanInterestPerOpinion, 0f, 0.01f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_LoanInterestMin".Translate(loanInterestMin.ToString("0%")),
                ref loanInterestMin, 0f, 1f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_LoanInterestMax".Translate(loanInterestMax.ToString("0%")),
                ref loanInterestMax, 0f, 2f);

            // right column: robbery + begging sliders
            float rx = colW + 40f;
            float ry = 4f;
            DrawSliderRow(rx, ref ry, colW, "PPTE2_RobberyCooldown".Translate(robberyCooldownDays.ToString("0.#")),
                ref robberyCooldownDays, 0f, 10f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_RobberyHungerThreshold".Translate(robberyHungerThreshold.ToString("0.##")),
                ref robberyHungerThreshold, 0f, 1f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_RobberyTakePercent".Translate(robberyTakePercent.ToString("0%")),
                ref robberyTakePercent, 0.05f, 1f);
            ry += 24f;
            DrawSliderRow(rx, ref ry, colW, "PPTE2_BeggingCooldown".Translate(beggingCooldownDays.ToString("0.#")),
                ref beggingCooldownDays, 0f, 10f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_BeggingAmountMin".Translate(beggingAmountMin.ToString("0.##")),
                ref beggingAmountMin, 0f, 10f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_BeggingAmountMax".Translate(beggingAmountMax.ToString("0.##")),
                ref beggingAmountMax, 1f, 20f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_BeggingChanceBase".Translate(beggingChanceBase.ToString("0%")),
                ref beggingChanceBase, 0f, 1f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_BeggingChancePerOpinion".Translate((beggingChancePerOpinion * 100f).ToString("0.0")),
                ref beggingChancePerOpinion, 0f, 0.02f);

            // hint at the bottom
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            float hintH = Text.CalcHeight("PPTE2_SocialHint".Translate(), view.width);
            Widgets.Label(new Rect(0f, Mathf.Max(ly, ry) + 20f, view.width, hintH), "PPTE2_SocialHint".Translate());
            GUI.color = Color.white;

            Widgets.EndScrollView();
        }

        // ================= Tab 6: 派系索要（通讯台） =================

        private Vector2 _requestScroll = Vector2.zero;

        private void DrawRequestTab(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 20f, 760f);
            Widgets.BeginScrollView(rect, ref _requestScroll, view);
            float colW = (view.width - 40f) / 2f;
            float ly = 4f;

            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableFactionRequest".Translate(), ref enableFactionRequest);
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableCellAutoRequest".Translate(), ref enableCellAutoRequest);
            ly += 8f;

            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestSilverMinFactor".Translate(requestSilverMinFactor.ToString("0.##")),
                ref requestSilverMinFactor, 0.01f, 1f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestSilverMaxFactor".Translate(requestSilverMaxFactor.ToString("0.##")),
                ref requestSilverMaxFactor, 0.05f, 2f);
            if (requestSilverMaxFactor < requestSilverMinFactor) requestSilverMaxFactor = requestSilverMinFactor;
            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestBaseChance".Translate(requestBaseChance.ToStringPercent()),
                ref requestBaseChance, 0f, 1f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestChancePerSocial".Translate((requestChancePerSocial * 100f).ToString("0.0")),
                ref requestChancePerSocial, 0f, 0.1f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestChancePerValue".Translate((requestChancePerValue * 10000f).ToString("0.0")),
                ref requestChancePerValue, 0f, 0.001f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestAmountPenalty".Translate(requestAmountPenalty.ToStringPercent()),
                ref requestAmountPenalty, 0f, 1f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestCooldownDays".Translate(requestCooldownDays.ToString("0.#")),
                ref requestCooldownDays, 0f, 10f);
            DrawSliderRow(0f, ref ly, colW, "PPTE2_RequestTriggerBalance".Translate(requestTriggerBalance.ToString("0.##")),
                ref requestTriggerBalance, 0f, 100f);

            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            float hintH = Text.CalcHeight("PPTE2_RequestHint".Translate(PPTEName.Ticket), view.width);
            Widgets.Label(new Rect(0f, ly + 12f, view.width, hintH), "PPTE2_RequestHint".Translate(PPTEName.Ticket));
            GUI.color = Color.white;
            Widgets.EndScrollView();
        }

        // ================= Tab 7: 娱乐与卫生 =================

        private Vector2 _joyHygieneScroll = Vector2.zero;
        private Vector2 _joyFeeScroll = Vector2.zero;
        private static List<ThingDef> _cachedJoyBuildings;

        private void DrawJoyHygieneTab(Rect rect)
        {
            float colW = (rect.width - 40f) / 2f;

            // 左列：娱乐设置
            var left = new Rect(rect.x, rect.y, colW, rect.height);
            var leftView = new Rect(0f, 0f, colW - 16f, 560f + JoyBuildingCount() * 30f);
            Widgets.BeginScrollView(left, ref _joyHygieneScroll, leftView);
            float ly = 4f;
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnablePrisonerJoyNeed".Translate(), ref enablePrisonerJoyNeed);
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableJoyFees".Translate(), ref enableJoyFees);
            DrawSliderRow(0f, ref ly, colW - 20f, "PPTE2_JoyFeePerUse".Translate(joyFeePerUse.ToString("0.##")),
                ref joyFeePerUse, 0f, 200f);
            ly += 6f;
            Widgets.Label(new Rect(0f, ly + 2f, colW - 60f, 24f), "PPTE2_CustomJoyFeesTitle".Translate());
            if (Widgets.ButtonText(new Rect(colW - 90f, ly, 86f, 26f), "PPTE2_ClearAllFoodPrices".Translate()))
                customJoyFees.Clear();
            ly += 32f;

            EnsureJoyBuildingsCached();
            float iconW = 34f;
            float inputW = 80f;
            float nameW = colW - 20f - iconW - inputW - 16f;
            for (int i = 0; i < _cachedJoyBuildings.Count; i++)
            {
                var def = _cachedJoyBuildings[i];
                var row = new Rect(0f, ly, colW - 20f, 28f);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);
                Widgets.ThingIcon(new Rect(2f, ly + 1f, 26f, 26f), def);
                Widgets.Label(new Rect(2f + iconW, ly + 4f, nameW, 22f), def.LabelCap);
                bool has = customJoyFees.TryGetValue(def.defName, out float cur);
                string buf = has ? cur.ToString("0.##") : "";
                string newBuf = Widgets.TextField(new Rect(2f + iconW + nameW + 8f, ly + 3f, inputW, 24f), buf);
                if (newBuf != buf)
                {
                    if (float.TryParse(newBuf, out float res) && res >= 0f) customJoyFees[def.defName] = res;
                    else customJoyFees.Remove(def.defName);
                }
                ly += 30f;
            }
            ly += 6f;
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            float joyHintH = Text.CalcHeight("PPTE2_JoyHint".Translate(PPTEName.Ticket), colW - 20f);
            Widgets.Label(new Rect(0f, ly, colW - 20f, joyHintH), "PPTE2_JoyHint".Translate(PPTEName.Ticket));
            GUI.color = Color.white;
            Widgets.EndScrollView();

            // 右列：卫生费设置
            float rx = rect.x + colW + 40f;
            var right = new Rect(rx, rect.y, colW, rect.height);
            var rightView = new Rect(0f, 0f, colW - 16f, 420f);
            Widgets.BeginScrollView(right, ref _joyFeeScroll, rightView);
            float ry = 4f;
            bool hyg = HygieneBridge.Present;
            if (!hyg)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                float nh = Text.CalcHeight("PPTE2_HygieneModMissing".Translate(), colW - 20f);
                Widgets.Label(new Rect(0f, ry, colW - 20f, nh), "PPTE2_HygieneModMissing".Translate());
                GUI.color = Color.white;
                ry += nh + 8f;
            }
            bool hygOn = enableHygieneFees;
            DrawCheckboxRow(0f, ref ry, "PPTE2_EnableHygieneFees".Translate(), ref hygOn);
            enableHygieneFees = hygOn;
            DrawSliderRow(0f, ref ry, colW - 20f, "PPTE2_WaterPricePerLiter".Translate(waterPricePerLiter.ToString("0.##"), PPTEName.Ticket),
                ref waterPricePerLiter, 0f, 2f);
            DrawSliderRow(0f, ref ry, colW - 20f, "PPTE2_PowerPricePerWd".Translate(powerPricePerWd.ToString("0.###"), PPTEName.Ticket),
                ref powerPricePerWd, 0f, 1f);
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            float hygHintH = Text.CalcHeight("PPTE2_HygieneHint".Translate(PPTEName.Ticket), colW - 20f);
            Widgets.Label(new Rect(0f, ry + 8f, colW - 20f, hygHintH), "PPTE2_HygieneHint".Translate(PPTEName.Ticket));
            GUI.color = Color.white;
            Widgets.EndScrollView();
        }

        private int JoyBuildingCount()
        {
            EnsureJoyBuildingsCached();
            return _cachedJoyBuildings.Count;
        }

        private static void EnsureJoyBuildingsCached()
        {
            if (_cachedJoyBuildings != null) return;
            _cachedJoyBuildings = new List<ThingDef>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null) continue;
                if (def.category != ThingCategory.Building) continue;
                if (def.building?.joyKind == null) continue;
                _cachedJoyBuildings.Add(def);
            }
            _cachedJoyBuildings.Sort((a, b) =>
                string.CompareOrdinal(a.label ?? a.defName, b.label ?? b.defName));
        }

        // ================= Tab 1: General =================

        private Vector2 _generalScroll = Vector2.zero;

        private void DrawGeneralTab(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 20f, 1200f);
            Widgets.BeginScrollView(rect, ref _generalScroll, view);

            float colW = (view.width - 40f) / 2f;

            // left column: checkboxes hugging labels
            float ly = 4f;
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableOrganHarvest".Translate(), ref enableOrganHarvest);
            DrawCheckboxRow(0f, ref ly, "PPTE2_OrganSaleAffectsGoodwill".Translate(), ref organSaleAffectsGoodwill);
            TooltipHandler.TipRegion(new Rect(0f, ly - 30f, colW, 30f), "PPTE2_OrganSaleAffectsGoodwillTip".Translate());
            DrawCheckboxRow(0f, ref ly, "PPTE2_IgnoreRiot".Translate(), ref ignoreDuringRiot);
            DrawCheckboxRow(0f, ref ly, "PPTE2_AllowMealDebt".Translate(), ref allowMealDebt);
            DrawCheckboxRow(0f, ref ly, "PPTE2_VerboseLog".Translate(), ref logVerbose);
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableRansom".Translate(), ref enableRansom);
            DrawCheckboxRow(0f, ref ly, "PPTE2_EnableChildParentPay".Translate(), ref enableChildParentPay);
            if (enableChildParentPay)
            {
                ly += 4f;
                Widgets.Label(new Rect(0f, ly + 2f, colW, 24f), "PPTE2_ChildPayModeLabel".Translate());
                ly += 28f;
                float bw = (colW - 8f) / 3f;
                Color gold = new Color(1f, 0.85f, 0.45f);
                for (int i = 0; i < 3; i++)
                {
                    var mode = (ChildPayMode)i;
                    string label = mode == ChildPayMode.OwnFirst ? "PPTE2_ChildPayModeOwnFirst".Translate()
                        : mode == ChildPayMode.ParentFirst ? "PPTE2_ChildPayModeParentFirst".Translate()
                        : "PPTE2_ChildPayModeMerged".Translate();
                    var r = new Rect(i * bw, ly, bw - 4f, 26f);
                    if (childPayMode == mode) GUI.color = gold;
                    if (Widgets.ButtonText(r, label)) childPayMode = mode;
                    GUI.color = Color.white;
                }
                ly += 32f;
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                float childHintH = Text.CalcHeight("PPTE2_ChildPayHint".Translate(), colW);
                Widgets.Label(new Rect(0f, ly, colW, childHintH), "PPTE2_ChildPayHint".Translate());
                GUI.color = Color.white;
                ly += childHintH + 8f;
            }

            // ticket name editor
            Widgets.Label(new Rect(0f, ly + 2f, 200f, 24f), "PPTE2_TicketNameLabel".Translate());
            customTicketName = Widgets.TextField(new Rect(210f, ly, colW - 220f, 26f), customTicketName);
            if (!customTicketName.NullOrEmpty())
            {
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                Widgets.Label(new Rect(0f, ly + 30f, colW, 20f),
                    "PPTE2_TicketNamePreview".Translate(customTicketName));
                GUI.color = Color.white;
                ly += 54f;
            }
            else
            {
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                Widgets.Label(new Rect(0f, ly + 30f, colW, 20f),
                    "PPTE2_TicketNamePreview".Translate("PPTE2_TicketDefaultName".Translate()));
                GUI.color = Color.white;
                ly += 54f;
            }

            // ransom hint under the left column
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            float ransomHintH = Text.CalcHeight("PPTE2_RansomHint".Translate(), colW);
            Widgets.Label(new Rect(0f, ly, colW, ransomHintH), "PPTE2_RansomHint".Translate());
            GUI.color = Color.white;
            ly += ransomHintH + 12f;

            // 免付费（全局内容豁免；每囚犯按类别豁免在"配置囚犯"窗口）
            Widgets.Label(new Rect(0f, ly + 2f, colW, 24f), "PPTE2_FeeExemptTitle".Translate());
            ly += 30f;
            DrawCheckboxRow(0f, ref ly, "PPTE2_FreeAllFood".Translate(), ref freeAllFood);
            DrawCheckboxRow(0f, ref ly, "PPTE2_FreeJoyAll".Translate(), ref freeJoyAll);
            DrawCheckboxRow(0f, ref ly, "PPTE2_FreeHygieneAll".Translate(), ref freeHygieneAll);
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            float freeHintH = Text.CalcHeight("PPTE2_FeeExemptHint".Translate(), colW);
            Widgets.Label(new Rect(0f, ly, colW, freeHintH), "PPTE2_FeeExemptHint".Translate());
            GUI.color = Color.white;
            ly += freeHintH + 8f;

            // right column: sliders
            float rx = colW + 40f;
            float ry = 4f;
            DrawSliderRow(rx, ref ry, colW, "PPTE2_FoodMul".Translate(foodPriceMultiplier.ToString("0.00")),
                ref foodPriceMultiplier, 0.1f, 10f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_WageMul".Translate(wageMultiplier.ToString("0.00")),
                ref wageMultiplier, 0.1f, 10f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_DefaultWagePerHour".Translate(defaultWagePerHour.ToString("0.0")),
                ref defaultWagePerHour, 0f, 20f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_SilverRate".Translate(silverToTicketRate.ToString("0.00")),
                ref silverToTicketRate, 0.1f, 10f);
            float startTickets = startingTicketsPerPrisoner;
            DrawSliderRow(rx, ref ry, colW, "PPTE2_StartingTickets".Translate(startingTicketsPerPrisoner),
                ref startTickets, 0f, 50f);
            startingTicketsPerPrisoner = (int)startTickets;
            DrawSliderRow(rx, ref ry, colW, "PPTE2_RansomTicketCost".Translate(ransomTicketCost.ToString("0.##")),
                ref ransomTicketCost, 10f, 5000f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_RansomMinDays".Translate(ransomMinDays.ToString("0.#")),
                ref ransomMinDays, 0f, 60f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_MealDebtFloor".Translate(mealDebtFloor.ToString("0.##")),
                ref mealDebtFloor, -1000f, 0f);
            DrawSliderRow(rx, ref ry, colW, "PPTE2_OrganSaleTrigger".Translate(organSaleTriggerBalance.ToString("0.##")),
                ref organSaleTriggerBalance, -1000f, 100f);
            string creditHint = "PPTE2_DebtOrganHint".Translate();
            Widgets.Label(new Rect(rx, ry, colW, Text.CalcHeight(creditHint, colW)), creditHint);

            Widgets.EndScrollView();
        }

        private static void DrawCheckboxRow(float x, ref float y, string label, ref bool value)
        {
            const float labelW = 320f;
            Widgets.Label(new Rect(x, y + 2f, labelW, 24f), label);
            Widgets.Checkbox(new Vector2(x + labelW + 8f, y), ref value);
            y += 30f;
        }

        private static void DrawSliderRow(float x, ref float y, float width, string label, ref float value, float min, float max)
        {
            Widgets.Label(new Rect(x, y, width, 24f), label);
            y += 26f;
            value = Widgets.HorizontalSlider(new Rect(x, y, width - 20f, 22f), value, min, max);
            y += 28f;
        }

        // ================= Tab 2: Per-work-type wages =================

        private static List<WorkTypeDef> _cachedWorkTypes;
        private Vector2 _workWageScroll = Vector2.zero;
        private static List<WorkGiverDef> _cachedWorkGivers;
        private readonly Dictionary<string, string> _workGiverInputBuffers = new Dictionary<string, string>();

        private void DrawWorkTypeWagesTab(Rect rect)
        {
            // toolbar: master switch + bulk actions
            float toolY = rect.y + 2f;
            Widgets.Label(new Rect(rect.x, toolY + 2f, 150f, 24f), "PPTE2_EnablePieceRate".Translate());
            Widgets.Checkbox(new Vector2(rect.x + 156f, toolY), ref enablePieceRate);
            if (Widgets.ButtonText(new Rect(rect.xMax - 320f, toolY, 155f, 26f), "PPTE2_ResetWorkTypeWages".Translate()))
            {
                workTypeWages.Clear();
                workTypePieceWages.Clear();
                pieceRateWorkTypes.Clear();
                workGiverWages.Clear();
                workGiverPieceWages.Clear();
                workGiverPieceModes.Clear();
                _workGiverInputBuffers.Clear();
            }
            if (Widgets.ButtonText(new Rect(rect.xMax - 155f, toolY, 155f, 26f), "PPTE2_FillWorkTypeWages".Translate()))
            {
                EnsureWorkTypesCached();
                foreach (var wt in _cachedWorkTypes) workTypeWages[wt.defName] = defaultWagePerHour;
                if (enableIndividualWorkGiverBilling)
                    foreach (var giver in _cachedWorkGivers) workGiverWages[giver.defName] = defaultWagePerHour;
                _workGiverInputBuffers.Clear();
            }
            float advancedY = toolY + 32f;
            Widgets.CheckboxLabeled(new Rect(rect.x, advancedY, rect.width, 28f),
                "PPTE2_IndividualWorkGiverBilling".Translate(), ref enableIndividualWorkGiverBilling);

            // wrapped hint below the toolbar (auto height so long translations never clip)
            GUI.color = new Color(0.7f, 0.7f, 0.65f);
            string hint = "PPTE2_WorkTypeWagesHint".Translate();
            if (enableIndividualWorkGiverBilling) hint += "\n" + "PPTE2_WorkGiverBillingHint".Translate();
            float hintH = Text.CalcHeight(hint, rect.width);
            Widgets.Label(new Rect(rect.x, toolY + 64f, rect.width, hintH), hint);
            GUI.color = Color.white;

            // scrollable list filling the rest of the tab
            var listRect = new Rect(rect.x, toolY + 68f + hintH, rect.width, Mathf.Max(0f, rect.yMax - toolY - 68f - hintH));
            EnsureWorkTypesCached();
            const float rowH = 28f;
            float individualHeight = enableIndividualWorkGiverBilling ? 66f + _cachedWorkGivers.Count * 32f : 0f;
            var view = new Rect(0f, 0f, listRect.width - 20f, 36f + _cachedWorkTypes.Count * rowH + individualHeight);
            Widgets.BeginScrollView(listRect, ref _workWageScroll, view);

            // column headers
            float labelW = view.width * 0.38f;
            float modeW = view.width * 0.28f;
            float inputW = (view.width - labelW - modeW - 20f) / 2f;
            float hourlyX = labelW + modeW + 6f;
            float pieceX = hourlyX + inputW + 8f;
            GUI.color = new Color(0.8f, 0.8f, 0.65f);
            Widgets.Label(new Rect(2f, 4f, labelW, 20f), "PPTE2_HdrWorkType".Translate());
            Widgets.Label(new Rect(labelW, 4f, modeW, 20f), "PPTE2_HdrMode".Translate());
            Widgets.Label(new Rect(hourlyX, 4f, inputW, 20f), "PPTE2_HdrHourlyWage".Translate());
            Widgets.Label(new Rect(pieceX, 4f, inputW, 20f), "PPTE2_HdrPieceWage".Translate());
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0f, 26f, view.width);

            float y = 32f;
            Color gold = new Color(1f, 0.85f, 0.45f);
            foreach (var wt in _cachedWorkTypes)
            {
                bool piece = pieceRateWorkTypes.Contains(wt.defName);

                // label
                Widgets.Label(new Rect(2f, y + 2f, labelW - 8f, 22f), wt.labelShort ?? wt.defName);

                // mode toggle: [按时] [按量]
                float mx = labelW;
                float btnW = (modeW - 4f) / 2f;
                GUI.color = piece ? Color.white : gold;
                if (Widgets.ButtonText(new Rect(mx, y, btnW, 22f), "PPTE2_ModePerHour".Translate()))
                    pieceRateWorkTypes.Remove(wt.defName);
                GUI.color = piece ? gold : Color.white;
                if (Widgets.ButtonText(new Rect(mx + btnW + 4f, y, btnW, 22f), "PPTE2_ModePerItem".Translate()))
                {
                    if (!pieceRateWorkTypes.Contains(wt.defName))
                    {
                        pieceRateWorkTypes.Add(wt.defName);
                        // prefill a sane per-unit wage the first time it is enabled
                        if (!workTypePieceWages.ContainsKey(wt.defName))
                            workTypePieceWages[wt.defName] = PieceRateWorker.GetPrefilledPieceWage(wt, GetWageForWorkType(wt.defName));
                    }
                }
                GUI.color = Color.white;

                // hourly wage input
                bool hasH = workTypeWages.TryGetValue(wt.defName, out float curH);
                string bufH = hasH ? curH.ToString("0.0") : "";
                string newBufH = Widgets.TextField(new Rect(hourlyX, y, inputW, 22f), bufH);
                if (newBufH != bufH)
                {
                    if (float.TryParse(newBufH, out float res) && res >= 0f) workTypeWages[wt.defName] = res;
                    else workTypeWages.Remove(wt.defName);
                }

                // piece-rate unit wage input
                bool hasP = workTypePieceWages.TryGetValue(wt.defName, out float curP);
                string bufP = hasP ? curP.ToString("0.##") : "";
                string newBufP = Widgets.TextField(new Rect(pieceX, y, inputW, 22f), bufP);
                if (newBufP != bufP)
                {
                    if (float.TryParse(newBufP, out float res) && res >= 0f) workTypePieceWages[wt.defName] = res;
                    else workTypePieceWages.Remove(wt.defName);
                }

                y += rowH;
            }
            if (enableIndividualWorkGiverBilling)
            {
                Widgets.Label(new Rect(2f, y + 4f, view.width, 28f), "PPTE2_WorkGiverBillingTitle".Translate());
                y += 36f;
                Widgets.Label(new Rect(2f, y, labelW, 24f), "PPTE2_HdrWorkType".Translate());
                Widgets.Label(new Rect(labelW, y, modeW, 24f), "PPTE2_HdrMode".Translate());
                Widgets.Label(new Rect(hourlyX, y, inputW, 24f), "PPTE2_HdrHourlyWage".Translate());
                Widgets.Label(new Rect(pieceX, y, inputW, 24f), "PPTE2_HdrPieceWage".Translate());
                y += 30f;
                for (int i = 0; i < _cachedWorkGivers.Count; i++)
                {
                    if (y + 32f >= _workWageScroll.y && y <= _workWageScroll.y + listRect.height)
                        DrawWorkGiverWageRow(_cachedWorkGivers[i], new Rect(0f, y, view.width, 32f));
                    y += 32f;
                }
            }

            Widgets.EndScrollView();
        }

        internal static void RefreshWorkTypes()
        {
            _cachedWorkTypes = null;
            _cachedWorkGivers = null;
            EnsureWorkTypesCached();
        }

        private static void EnsureWorkTypesCached()
        {
            if (_cachedWorkTypes != null) return;
            _cachedWorkTypes = new List<WorkTypeDef>();
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                _cachedWorkTypes.Add(wt);
            _cachedWorkTypes.Sort((a, b) =>
                string.CompareOrdinal(a.labelShort ?? a.defName, b.labelShort ?? b.defName));
            _cachedWorkGivers = new List<WorkGiverDef>(DefDatabase<WorkGiverDef>.AllDefsListForReading);
            _cachedWorkGivers.Sort((a, b) =>
            {
                int parent = string.CompareOrdinal(a.workType?.defName, b.workType?.defName);
                return parent != 0 ? parent : string.CompareOrdinal(a.label ?? a.defName, b.label ?? b.defName);
            });
        }

        private void DrawWorkGiverWageRow(WorkGiverDef giver, Rect row)
        {
            float nameWidth = row.width * 0.38f;
            float modeWidth = row.width * 0.28f;
            float inputWidth = (row.width - nameWidth - modeWidth - 20f) / 2f;
            string label = giver.label.NullOrEmpty() ? giver.defName : giver.label;
            Widgets.Label(new Rect(2f, row.y + 2f, nameWidth - 8f, 26f), label);
            TooltipHandler.TipRegion(new Rect(0f, row.y, nameWidth, row.height), giver.defName + "\n"
                + (giver.workType?.labelShort ?? "PPTE2_NoParentWorkType".Translate().ToString()));
            bool hasMode = workGiverPieceModes.TryGetValue(giver.defName, out bool piece);
            int selected = !hasMode ? 0 : piece ? 2 : 1;
            Color previous = GUI.color;
            for (int mode = 0; mode < 3; mode++)
            {
                GUI.color = mode == selected ? new Color(1f, 0.85f, 0.45f) : previous;
                string key = mode == 0 ? "PPTE2_WorkGiverInherit" : mode == 1 ? "PPTE2_ModePerHour" : "PPTE2_ModePerItem";
                if (Widgets.ButtonText(new Rect(nameWidth + mode * modeWidth / 3f, row.y, modeWidth / 3f - 3f, 24f), key.Translate()))
                {
                    if (mode == 0) workGiverPieceModes.Remove(giver.defName);
                    else workGiverPieceModes[giver.defName] = mode == 2;
                }
            }
            GUI.color = previous;
            float hourlyX = nameWidth + modeWidth + 6f;
            DrawWorkGiverWageInput(new Rect(hourlyX, row.y, inputWidth, 24f), giver.defName, false);
            DrawWorkGiverWageInput(new Rect(hourlyX + inputWidth + 8f, row.y, inputWidth, 24f), giver.defName, true);
        }

        private void DrawWorkGiverWageInput(Rect rect, string id, bool piece)
        {
            var rates = piece ? workGiverPieceWages : workGiverWages;
            string key = (piece ? "piece:" : "hour:") + id;
            if (!_workGiverInputBuffers.TryGetValue(key, out string buffer))
                buffer = rates.TryGetValue(id, out float value) ? value.ToString("0.##") : "";
            string next = Widgets.TextField(rect, buffer);
            _workGiverInputBuffers[key] = next;
            if (next == buffer) return;
            if (next.Trim().Length == 0) rates.Remove(id);
            else if (float.TryParse(next, out float value) && float.IsFinite(value) && value >= 0f) rates[id] = value;
        }

        private float MedicalRestControlsHeight(float width) => 104f
            + Text.CalcHeight("PPTE2_MedicalRestHint".Translate(), width)
            + (!forbidPrisonerMedicalRest && forbidNonCriticalPrisonerMedicalRest ? 54f : 0f);

        private void DrawMedicalRestControls(float width, ref float y)
        {
            Widgets.CheckboxLabeled(new Rect(0f, y, width, 28f), "PPTE2_ForbidMedicalRest".Translate(), ref forbidPrisonerMedicalRest);
            y += 32f;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !forbidPrisonerMedicalRest;
            Widgets.CheckboxLabeled(new Rect(0f, y, width, 28f), "PPTE2_ForbidNonCriticalMedicalRest".Translate(), ref forbidNonCriticalPrisonerMedicalRest);
            GUI.enabled = enabled;
            y += 32f;
            if (!forbidPrisonerMedicalRest && forbidNonCriticalPrisonerMedicalRest)
                DrawSliderRow(0f, ref y, width, "PPTE2_CriticalDiseaseThreshold".Translate(criticalDiseaseThreshold.ToStringPercent()),
                    ref criticalDiseaseThreshold, 0f, 1f);
            string hint = "PPTE2_MedicalRestHint".Translate();
            float height = Text.CalcHeight(hint, width);
            Widgets.Label(new Rect(0f, y, width, height), hint);
            y += height + 20f;
            Widgets.DrawLineHorizontal(0f, y, width);
            y += 20f;
        }

        // ================= Tab 3: Custom food prices =================

        private static List<ThingDef> _cachedFoodDefs;
        private static readonly Dictionary<string, string> _searchTextCache = new Dictionary<string, string>();
        private string _priceSearch = "";
        private Vector2 _priceScroll = Vector2.zero;

        private void DrawFoodPricesTab(Rect rect)
        {
            float toolY = rect.y + 2f;
            Widgets.Label(new Rect(rect.x, toolY + 2f, 60f, 24f), "PPTE2_Search".Translate());
            _priceSearch = Widgets.TextField(new Rect(rect.x + 65f, toolY, 220f, 26f), _priceSearch);
            if (Widgets.ButtonText(new Rect(rect.x + 295f, toolY, 80f, 26f), "PPTE2_ResetSearch".Translate()))
                _priceSearch = "";
            if (Widgets.ButtonText(new Rect(rect.xMax - 150f, toolY, 150f, 26f), "PPTE2_ClearAllFoodPrices".Translate()))
                customFoodPrices.Clear();

            var listRect = new Rect(rect.x, toolY + 34f, rect.width, rect.yMax - toolY - 34f);
            if (_cachedFoodDefs == null) CacheFoodDefs();

            string norm = (_priceSearch ?? "").Trim().ToLowerInvariant();
            var filtered = new List<ThingDef>();
            foreach (var def in _cachedFoodDefs)
            {
                string text;
                if (!_searchTextCache.TryGetValue(def.defName, out text))
                {
                    text = ((def.label ?? "") + "\n" + (def.defName ?? "")).ToLowerInvariant();
                    _searchTextCache[def.defName] = text;
                }
                if (string.IsNullOrEmpty(norm) || text.Contains(norm))
                    filtered.Add(def);
            }

            const float rowH = 30f;
            const float headerH = 26f;
            var view = new Rect(0f, 0f, listRect.width - 20f, headerH + filtered.Count * rowH + 10f);
            Widgets.BeginScrollView(listRect, ref _priceScroll, view);

            const float iconW = 34f;
            const float baseW = 90f;
            const float inputW = 110f;
            const float freeW = 60f;
            const float pad = 8f;
            float nameW = view.width - iconW - baseW - inputW - freeW - pad * 4f;

            float x1 = pad;
            float x2 = x1 + iconW + pad;
            float x3 = x2 + nameW + pad;
            float x4 = x3 + baseW + pad;
            float x5 = x4 + inputW + pad;

            GUI.color = new Color(0.8f, 0.8f, 0.6f);
            Widgets.Label(new Rect(x2, 2f, nameW, 22f), "PPTE2_HdrFoodName".Translate());
            Widgets.Label(new Rect(x3, 2f, baseW, 22f), "PPTE2_HdrBaseMarketValue".Translate());
            Widgets.Label(new Rect(x4, 2f, inputW, 22f), "PPTE2_HdrCustomPrice".Translate());
            Widgets.Label(new Rect(x5, 2f, freeW, 22f), "PPTE2_HdrFree".Translate());
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0f, headerH - 2f, view.width);

            float y = headerH;
            for (int i = 0; i < filtered.Count; i++)
            {
                var def = filtered[i];
                if (i % 2 == 0) Widgets.DrawLightHighlight(new Rect(0f, y, view.width, rowH));

                Widgets.ThingIcon(new Rect(x1, y + 2f, 26f, 26f), def);
                Widgets.Label(new Rect(x2, y + 4f, nameW, 24f), def.LabelCap);

                float baseVal = def.BaseMarketValue > 0 ? def.BaseMarketValue
                    : (def.ingestible != null ? def.ingestible.CachedNutrition * 10f : 0f);
                Widgets.Label(new Rect(x3, y + 4f, baseW, 24f), baseVal.ToString("F1"));

                bool has = customFoodPrices.TryGetValue(def.defName, out float cur);
                string buf = has ? cur.ToString("0.##") : "";
                string newBuf = Widgets.TextField(new Rect(x4, y + 3f, inputW, 24f), buf);
                if (newBuf != buf)
                {
                    if (float.TryParse(newBuf, out float res) && res > 0f) customFoodPrices[def.defName] = res;
                    else customFoodPrices.Remove(def.defName);
                }

                // 免费勾选：囚犯吃这种东西不收饭票
                bool isFree = freeFoodItems.Contains(def.defName);
                bool newFree = isFree;
                Widgets.Checkbox(x5 + 8f, y + 3f, ref newFree);
                if (newFree != isFree)
                {
                    if (newFree) freeFoodItems.Add(def.defName);
                    else freeFoodItems.Remove(def.defName);
                }
                y += rowH;
            }

            Widgets.EndScrollView();
        }

        private static void CacheFoodDefs()
        {
            _cachedFoodDefs = new List<ThingDef>();
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null) continue;
                if (def.ingestible == null) continue;
                if (def.category != ThingCategory.Item) continue; // 尸体也是 Item，因此会进列表
                if (!def.IsNutritionGivingIngestible) continue;
                // 不再排除尸体：玩家可以把尸体勾成免费（或单独定价）
                _cachedFoodDefs.Add(def);
            }
            _cachedFoodDefs.Sort((a, b) =>
                string.CompareOrdinal(a.label ?? a.defName, b.label ?? b.defName));
        }
    }
}