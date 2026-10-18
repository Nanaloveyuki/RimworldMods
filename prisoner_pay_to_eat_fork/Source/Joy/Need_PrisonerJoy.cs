using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 囚犯娱乐需求条（原版囚犯没有娱乐条）。随时间缓慢衰减；囚犯进行娱乐活动时恢复
    /// （由 <see cref="Harmony_JoyCharges.Patch_JoyTickCheckEnd"/> 在娱乐 tick 时充能）。
    /// 与动机 Need 同款做法：所有类人 pawn 持有，但只对殖民地囚犯显示/生效。
    /// 当前仅作展示与娱乐计费联动，不影响心情。
    /// </summary>
    public class Need_PrisonerJoy : Need
    {
        // NeedInterval 每 150 tick 触发一次（原版需求节拍），60000 tick = 1 游戏天
        private const int IntervalTicks = 150;
        private const float FallPerDay = 0.4f;   // 每日自然衰减
        private const float GainPerHour = 0.55f; // 每小时娱乐的恢复量

        public Need_PrisonerJoy(Pawn pawn) : base(pawn) { }

        public override void SetInitialLevel() => CurLevel = 0.5f;

        public override bool ShowOnNeedList =>
            PrisonersPayToEat2Mod.Settings != null
            && PrisonersPayToEat2Mod.Settings.enablePrisonerJoyNeed
            && pawn != null && pawn.IsPrisonerOfColony;

        protected override bool IsFrozen =>
            PrisonersPayToEat2Mod.Settings == null
            || !PrisonersPayToEat2Mod.Settings.enablePrisonerJoyNeed
            || pawn == null || !pawn.IsPrisonerOfColony;

        public override void NeedInterval()
        {
            if (IsFrozen) return;
            CurLevel = Mathf.Clamp01(CurLevel - FallPerDay * IntervalTicks / 60000f);
        }

        /// <summary>娱乐 tick 时充能（delta 为本次经过的 tick 数，extraFactor 为娱乐源的增益系数）。</summary>
        public void NotifyJoyTick(int delta, float extraFactor)
        {
            if (IsFrozen || delta <= 0) return;
            float factor = extraFactor > 0f ? extraFactor : 1f;
            CurLevel = Mathf.Clamp01(CurLevel + delta / 60000f * GainPerHour * factor);
        }
    }
}
