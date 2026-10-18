using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 通讯台派系索要：玩家（殖民者/奴隶）或牢房里的囚犯本人通过通讯台联系囚犯的所属派系，
    /// 为囚犯索要生活费。成功后白银空投到通讯台附近，囚犯同时获得等额饭票（按银换票汇率）。
    /// 可索要金额范围与成功率按囚犯市场价值计算，打电话者的社交技能提高成功率；
    /// 所有系数均可在设置中调整。每名囚犯独立冷却（默认 1 游戏天）。
    /// </summary>
    public static class FactionRequestSystem
    {
        private static JobDef _jobDef;
        public static JobDef RequestJobDef =>
            _jobDef ?? (_jobDef = DefDatabase<JobDef>.GetNamedSilentFail("PPTE2_RequestTickets"));

        /// <summary>囚犯的派系可以被索要（非玩家、类人、非隐藏派系）。</summary>
        public static bool FactionEligible(Pawn prisoner)
        {
            var f = prisoner?.Faction;
            if (f == null || f.IsPlayer) return false;
            if (f.def == null || f.def.hidden || !f.def.humanlikeFaction) return false;
            return true;
        }

        /// <summary>该囚犯是否在索要冷却中。</summary>
        public static bool OnCooldown(Pawn prisoner)
        {
            var d = PrisonersPayToEat2Manager.Current?.DataFor(prisoner);
            return d != null && d.requestCooldownUntilTick > Find.TickManager.TicksGame;
        }

        public static int CooldownTicksRemaining(Pawn prisoner)
        {
            var d = PrisonersPayToEat2Manager.Current?.DataFor(prisoner);
            if (d == null) return 0;
            return Mathf.Max(0, d.requestCooldownUntilTick - Find.TickManager.TicksGame);
        }

        /// <summary>可索要白银范围：[市场价值×min系数, 市场价值×max系数]，下限至少 1。</summary>
        public static void SilverRange(Pawn prisoner, out float min, out float max)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            float value = Mathf.Max(1f, prisoner?.MarketValue ?? 0f);
            min = Mathf.Max(1f, value * s.requestSilverMinFactor);
            max = Mathf.Max(min, value * s.requestSilverMaxFactor);
        }

        /// <summary>
        /// 成功率 = 基础 + 打电话者社交×每点加成 + 囚犯价值×价值加成 − (索要额/上限)×索要惩罚。
        /// caller 为 null 时社交按 0 计。
        /// </summary>
        public static float SuccessChance(Pawn caller, Pawn prisoner, float silverAmount)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            SilverRange(prisoner, out _, out float max);
            float fraction = max <= 0.001f ? 1f : Mathf.Clamp01(silverAmount / max);
            float value = Mathf.Max(0f, prisoner?.MarketValue ?? 0f);
            int social = caller?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            return Mathf.Clamp01(s.requestBaseChance + social * s.requestChancePerSocial
                + value * s.requestChancePerValue - fraction * s.requestAmountPenalty);
        }

        /// <summary>地图上是否存在任何可被索要的囚犯（决定通讯台浮动菜单项是否显示）。</summary>
        public static bool AnyEligiblePrisoner(Map map)
        {
            if (map == null) return false;
            var prisoners = map.mapPawns.PrisonersOfColony;
            for (int i = 0; i < prisoners.Count; i++)
                if (FactionEligible(prisoners[i])) return true;
            return false;
        }

        /// <summary>
        /// 结算一次索要并进入冷却。caller 是实际"打电话"的 pawn（玩家手动=殖民者/奴隶，
        /// 自动索要=囚犯本人），其社交技能影响成功率。成功：白银空投 + 囚犯加饭票；失败：仅消息。
        /// </summary>
        public static void Resolve(Map map, IntVec3 nearPos, Pawn caller, Pawn prisoner, int silverAmount)
        {
            var mgr = PrisonersPayToEat2Manager.Current;
            var s = PrisonersPayToEat2Mod.Settings;
            if (map == null || mgr == null || s == null) return;
            if (!FactionEligible(prisoner) || silverAmount <= 0) return;

            // 冷却：无论成败都进入，防止反复尝试
            mgr.DataFor(prisoner).requestCooldownUntilTick =
                Find.TickManager.TicksGame + Mathf.RoundToInt(s.requestCooldownDays * 60000f);

            float chance = SuccessChance(caller, prisoner, silverAmount);
            if (!Rand.Chance(chance))
            {
                Messages.Message("PPTE2_RequestFailed".Translate(
                        prisoner.LabelShortCap, prisoner.Faction.Name, silverAmount),
                    new LookTargets(prisoner), MessageTypeDefOf.NeutralEvent);
                if (s.logVerbose)
                    Log.Message($"[PPTE2] Request for {prisoner.LabelShort} failed (chance={chance:P0}, amount={silverAmount}).");
                return;
            }

            float tickets = silverAmount * s.silverToTicketRate;
            mgr.AddTickets(prisoner, tickets);
            DropSilver(map, nearPos, silverAmount);

            Find.LetterStack.ReceiveLetter(
                "PPTE2_RequestSucceededLetter".Translate(),
                "PPTE2_RequestSucceededText".Translate(
                    prisoner.LabelShortCap, prisoner.Faction.Name, silverAmount,
                    tickets.ToString("0.##"), PPTEName.Ticket),
                LetterDefOf.PositiveEvent,
                new LookTargets(new TargetInfo(nearPos, map)));
            if (s.logVerbose)
                Log.Message($"[PPTE2] Request for {prisoner.LabelShort} succeeded: {silverAmount} silver + {tickets:0.##} tickets (chance={chance:P0}).");
        }

        /// <summary>把白银拆成不超过堆叠上限的若干堆，空投到指定位置附近（不破坏屋顶，落地即可拾取）。</summary>
        private static void DropSilver(Map map, IntVec3 nearPos, int silverAmount)
        {
            var things = new List<Thing>();
            int left = silverAmount;
            while (left > 0)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = Mathf.Min(left, silver.def.stackLimit);
                left -= silver.stackCount;
                things.Add(silver);
            }
            // openDelay=110, canInstaDropDuringInit=false, leaveSlag=false, canRoofPunch=false（不砸屋顶）, forbid=false
            DropPodUtility.DropThingsNear(nearPos, map, things, 110, false, false, false, false);
        }
    }
}
