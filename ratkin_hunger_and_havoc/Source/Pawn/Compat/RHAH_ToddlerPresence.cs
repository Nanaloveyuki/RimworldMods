using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    // 判据 = 启用 且 两个成长 HediffDef 真的加载 防"启用了但没加载成功"
    internal static class RHAH_ToddlerPresence
    {
        internal const string PackageId = "cyanobot.toddlers";

        // 抱得走婴儿是 Toddlers 的扩展（RimTalk - Expand Toddlers）给的 不是 Toddlers 本体
        internal const string PickUpJobName = "RimTalk_PickUpToddler";

        internal static bool Active => From(
            ModsConfig.IsActive(PackageId),
            Loaded(RHAH_ChildMovement.LearningToWalk),
            Loaded(RHAH_ChildMovement.LearningManipulation));

        // 婴儿既不会走路也不会被抱走 刷出来只会被丢在路边 所以抬龄看这一条
        internal static bool BabiesCarriable => Active && DefDatabase<JobDef>.GetNamedSilentFail(PickUpJobName) != null;

        internal static bool From(bool modActive, bool walkLoaded, bool manipulationLoaded)
        {
            return modActive && walkLoaded && manipulationLoaded;
        }

        static bool Loaded(string defName)
        {
            return !string.IsNullOrEmpty(defName) && DefDatabase<HediffDef>.GetNamedSilentFail(defName) != null;
        }
    }
}
