using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    // 走不到地图边缘时的凿墙出图 只由 JobGiver_RHAH_Leave 在搜不到出口时调用 不再进 Def
    internal sealed class JobGiver_RHAH_DigOutWhenNoExit : JobGiver_ExitMapBest
    {
        // 原版 duty 的 thinkNode 也是全 pawn 共用一个实例 giver 本身无状态可以照做 省掉每次新建
        static readonly JobGiver_RHAH_DigOutWhenNoExit sharedGiver = new JobGiver_RHAH_DigOutWhenNoExit();

        // 与其它 ThinkNode 同形保留 public 构造 类本身不出 Def 所以是 internal
        public JobGiver_RHAH_DigOutWhenNoExit()
        {
            defaultLocomotion = LocomotionUrgency.Jog;
        }

        // TryGiveJob 是 protected 只能由子类转发一次给外部调用
        internal static Job TryCreate(Verse.Pawn pawn)
        {
            return sharedGiver.TryGiveJob(pawn);
        }
    }
}
