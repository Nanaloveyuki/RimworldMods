using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 通讯台索要工作：走到通讯台 → 通话一段时间 → 结算（见 <see cref="FactionRequestSystem"/>）。
    /// 两种发起方式共用：
    ///   玩家手动：caller=殖民者/奴隶（本 pawn），job.targetB=囚犯，job.count=索要白银数；
    ///   囚犯自动：pawn 本人就是囚犯（targetB 无效、count 为 0），金额取范围上限。
    /// 通话中 pawn 的社交技能影响成功率。
    /// </summary>
    public class JobDriver_RequestTickets : JobDriver
    {
        private const int TalkDurationTicks = 1400; // 约 0.56 个游戏小时

        private Building_CommsConsole Console => job.targetA.Thing as Building_CommsConsole;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => Console == null || !Console.CanUseCommsNow);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            var talk = new Toil
            {
                defaultDuration = TalkDurationTicks,
                defaultCompleteMode = ToilCompleteMode.Delay,
                handlingFacing = true
            };
            talk.tickAction = () =>
            {
                if (pawn.IsHashIntervalTick(60))
                    pawn.rotationTracker.FaceTarget(job.targetA);
            };
            talk.WithProgressBar(TargetIndex.A, () => 1f - (float)ticksLeftThisToil / TalkDurationTicks);
            yield return talk;

            var resolve = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
            resolve.initAction = () =>
            {
                var console = Console;
                if (console == null) return;

                // 手动：targetB 为囚犯；自动：pawn 本人就是囚犯
                var prisoner = job.targetB.Thing as Pawn ?? pawn;
                if (!FactionRequestSystem.FactionEligible(prisoner)) return;
                // 已处于冷却（例如窗口打开后另一人先索要走）则不再结算
                if (FactionRequestSystem.OnCooldown(prisoner)) return;

                int amount = job.count;
                if (amount <= 0)
                {
                    FactionRequestSystem.SilverRange(prisoner, out _, out float max);
                    amount = Mathf.RoundToInt(max);
                }
                FactionRequestSystem.Resolve(pawn.Map, console.Position, pawn, prisoner, amount);
            };
            yield return resolve;
        }
    }
}
