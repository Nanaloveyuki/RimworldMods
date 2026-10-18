using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 通讯台索要窗口：左侧按派系分组列出可索要的囚犯；右侧显示选中囚犯的市场价值、
    /// 可索要白银范围与金额滑条、以打电话小人社交技能计算的实时成功率。确认后小人
    /// 接到"向派系索要饭票"的工作，走到通讯台通话并结算。
    /// </summary>
    public class Dialog_RequestTickets : Window
    {
        private readonly Building_CommsConsole _console;
        private readonly Pawn _caller;

        // 派系 -> 该派系可索要囚犯列表
        private readonly List<KeyValuePair<Faction, List<Pawn>>> _groups =
            new List<KeyValuePair<Faction, List<Pawn>>>();
        private Pawn _selected;
        private float _amount;      // 当前滑条选中的白银数
        private Vector2 _listScroll = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(720f, 540f);

        public Dialog_RequestTickets(Building_CommsConsole console, Pawn caller)
        {
            _console = console;
            _caller = caller;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;

            var map = console?.Map;
            if (map == null) return;
            var prisoners = map.mapPawns.PrisonersOfColony;
            foreach (var p in prisoners)
            {
                if (!FactionRequestSystem.FactionEligible(p)) continue;
                var g = _groups.FirstOrDefault(kv => kv.Key == p.Faction);
                if (g.Key == null)
                {
                    _groups.Add(new KeyValuePair<Faction, List<Pawn>>(p.Faction, new List<Pawn> { p }));
                }
                else g.Value.Add(p);
            }
            foreach (var kv in _groups)
                kv.Value.SortBy(p => p.LabelShortCap);
            _groups.SortBy(kv => kv.Key.Name);
        }

        private void Select(Pawn p)
        {
            _selected = p;
            FactionRequestSystem.SilverRange(p, out _, out float max);
            _amount = max; // 默认拉满，玩家可自行下调
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "PPTE2_RequestDialogTitle".Translate());
            Text.Font = GameFont.Small;

            float top = 38f;
            float listW = 300f;
            var listRect = new Rect(0f, top, listW, inRect.height - top);
            var detailRect = new Rect(listW + 16f, top, inRect.width - listW - 16f, inRect.height - top);

            // ============ 左列：按派系分组的囚犯列表 ============
            const float rowH = 30f;
            const float headerH = 26f;
            float totalH = 0f;
            foreach (var kv in _groups) totalH += headerH + kv.Value.Count * rowH;
            var viewRect = new Rect(0f, 0f, listW - 20f, Mathf.Max(totalH, listRect.height));
            Widgets.BeginScrollView(listRect, ref _listScroll, viewRect);
            float y = 0f;
            foreach (var kv in _groups)
            {
                GUI.color = new Color(0.8f, 0.8f, 0.6f);
                Widgets.Label(new Rect(0f, y, viewRect.width, headerH), kv.Key.Name);
                GUI.color = Color.white;
                y += headerH;
                foreach (var p in kv.Value)
                {
                    var r = new Rect(12f, y, viewRect.width - 12f, rowH);
                    if (_selected == p) Widgets.DrawBoxSolid(r, new Color(1f, 0.85f, 0.45f, 0.25f));
                    string label = p.LabelShortCap;
                    if (FactionRequestSystem.OnCooldown(p)) label += "PPTE2_RequestOnCooldownTag".Translate();
                    if (Widgets.ButtonInvisible(r, true))
                    {
                        Select(p);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                    Widgets.Label(new Rect(r.x + 6f, r.y + 4f, r.width - 12f, rowH - 6f), label);
                    y += rowH;
                }
            }
            Widgets.EndScrollView();
            Widgets.DrawLineVertical(listW + 8f, top, inRect.height - top);

            // ============ 右列：选中囚犯详情 ============
            float dy = detailRect.y;
            float dw = detailRect.width;
            if (_selected == null)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.65f);
                Widgets.Label(detailRect, "PPTE2_RequestSelectHint".Translate());
                GUI.color = Color.white;
                return;
            }

            Widgets.Label(new Rect(detailRect.x, dy, dw, 26f), _selected.LabelShortCap);
            dy += 30f;

            float value = _selected.MarketValue;
            FactionRequestSystem.SilverRange(_selected, out float min, out float max);
            var s = PrisonersPayToEat2Mod.Settings;
            Widgets.Label(new Rect(detailRect.x, dy, dw, 24f),
                "PPTE2_RequestValue".Translate(value.ToString("0")));
            dy += 28f;
            Widgets.Label(new Rect(detailRect.x, dy, dw, 24f),
                "PPTE2_RequestRange".Translate(min.ToString("0"), max.ToString("0")));
            dy += 32f;

            // 金额滑条
            _amount = Mathf.Clamp(_amount, min, max);
            Widgets.Label(new Rect(detailRect.x, dy, dw, 24f),
                "PPTE2_RequestAmount".Translate(Mathf.RoundToInt(_amount),
                    (Mathf.RoundToInt(_amount) * s.silverToTicketRate).ToString("0.##"), PPTEName.Ticket));
            dy += 28f;
            _amount = Widgets.HorizontalSlider(new Rect(detailRect.x, dy, dw - 20f, 22f), _amount, min, max);
            _amount = Mathf.Round(_amount);
            dy += 34f;

            // 实时成功率
            float chance = FactionRequestSystem.SuccessChance(_caller, _selected, _amount);
            int social = _caller?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            Widgets.Label(new Rect(detailRect.x, dy, dw, 24f),
                "PPTE2_RequestChance".Translate(chance.ToStringPercent(), social));
            dy += 34f;

            // 冷却 / 确认
            if (FactionRequestSystem.OnCooldown(_selected))
            {
                float days = FactionRequestSystem.CooldownTicksRemaining(_selected) / 60000f;
                GUI.color = new Color(1f, 0.6f, 0.5f);
                Widgets.Label(new Rect(detailRect.x, dy, dw, 24f),
                    "PPTE2_RequestCooldown".Translate(days.ToString("0.#")));
                GUI.color = Color.white;
            }
            else if (Widgets.ButtonText(new Rect(detailRect.x, dy, 200f, 34f),
                "PPTE2_RequestConfirm".Translate()))
            {
                Confirm();
            }
        }

        private void Confirm()
        {
            var jobDef = FactionRequestSystem.RequestJobDef;
            if (jobDef == null || _caller == null || _selected == null) { Close(); return; }
            var job = JobMaker.MakeJob(jobDef, _console, _selected);
            job.count = Mathf.RoundToInt(_amount);
            job.playerForced = true;
            _caller.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            Close();
        }
    }
}
