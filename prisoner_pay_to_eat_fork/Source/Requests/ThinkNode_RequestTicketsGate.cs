using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// 牢房自动索要的 gate 节点（注入 Humanlike_PostDuty，见 Defs/ThinkTreeDefs）：
    /// 殖民地囚犯、余额低于触发线、不在崩溃/越狱、派系合格、冷却到期才放行；
    /// 不满足时求值直接落回原分支，无开销。
    /// </summary>
    public class ThinkNode_RequestTicketsGate : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            var s = PrisonersPayToEat2Mod.Settings;
            if (s == null || !s.enableFactionRequest || !s.enableCellAutoRequest) return false;
            if (pawn == null || !pawn.IsPrisonerOfColony) return false;
            if (pawn.InMentalState || pawn.Downed) return false;
            if (Harmony_Ingest.InPrisonBreak(pawn)) return false;
            if (!FactionRequestSystem.FactionEligible(pawn)) return false;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return false;
            if (mgr.AvailableBalance(pawn) >= s.requestTriggerBalance) return false;
            if (FactionRequestSystem.OnCooldown(pawn)) return false;
            return true;
        }
    }

    /// <summary>
    /// 给走投无路的囚犯派发"向派系索要饭票"的工作：找牢房（当前房间）内可达且通电的通讯台。
    /// </summary>
    public class JobGiver_RequestTickets : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var jobDef = FactionRequestSystem.RequestJobDef;
            if (jobDef == null) return null;

            var room = pawn.GetRoom();
            if (room == null) return null;

            Building_CommsConsole best = null;
            float bestDist = float.MaxValue;
            foreach (var thing in room.ContainedThings(ThingDefOf.CommsConsole))
            {
                if (!(thing is Building_CommsConsole console)) continue;
                if (!console.CanUseCommsNow) continue;
                if (console.IsForbidden(pawn)) continue;
                if (!pawn.CanReach(console, PathEndMode.Touch, Danger.Deadly)) continue;
                float dist = (console.Position - pawn.Position).LengthHorizontalSquared;
                if (dist >= bestDist) continue;
                best = console;
                bestDist = dist;
            }
            if (best == null) return null;

            // 自动模式：不带 targetB/count，JobDriver 结算时取范围上限、以囚犯本人为打电话者
            return JobMaker.MakeJob(jobDef, best);
        }
    }
}
