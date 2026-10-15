using HungerAndHavoc.Core;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_FamineMood
    {
        internal static void NotifyFed(Verse.Pawn pawn)
        {
            if (RHAH_Mod.Settings != null && !RHAH_Mod.Settings.famineMoodEnabled) return;
            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("RHAH_Thought_FamineFed");
            if (thought != null) pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
        }
    }

    // XML 反射入口
    public sealed class ThoughtWorker_RHAH_FamineHunger : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Verse.Pawn pawn)
        {
            if ((RHAH_Mod.Settings != null && !RHAH_Mod.Settings.famineMoodEnabled) ||
                !RHAH_Api.IsVisitor(pawn) || pawn.Faction == Faction.OfPlayer || pawn.IsPrisoner || pawn.IsSlave ||
                !RHAH_VisitorRules.AllowsModBehavior(RHAH_VisitorStay.Kind(pawn)) ||
                pawn.needs?.food == null || pawn.needs.food.CurCategory != HungerCategory.Starving)
                return ThoughtState.Inactive;
            bool rejected = RHAH_ArmedDemand.Refused(pawn);
            System.Collections.Generic.List<Thought_Memory> memories = pawn.needs.mood?.thoughts?.memories?.Memories;
            for (int i = 0; !rejected && memories != null && i < memories.Count; i++)
            {
                rejected = memories[i].def.defName == "RHAH_Thought_BeggingRejected";
            }
            return ThoughtState.ActiveAtStage(rejected ? 1 : 0);
        }
    }

    // XML 反射入口
    public sealed class ThoughtWorker_RHAH_ClaySatiety : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Verse.Pawn pawn)
        {
            if (RHAH_Mod.Settings != null && !RHAH_Mod.Settings.clayMoodEnabled) return ThoughtState.Inactive;
            Hediff_RHAH_ClaySatiety clay = pawn.health?.hediffSet?.GetFirstHediffOfDef(RHAH_DefOf.RHAH_ClaySatiety) as Hediff_RHAH_ClaySatiety;
            int count = clay?.ActiveBites ?? 0;
            return count > 0 ? ThoughtState.ActiveAtStage(System.Math.Min(count, 3) - 1) : ThoughtState.Inactive;
        }
    }

    // XML 反射入口 关闭设置时已保存的心情也立即失效
    public class Thought_Memory_RHAH_FamineFed : Thought_Memory
    {
        public override float MoodOffset()
        {
            return RHAH_Mod.Settings != null && !RHAH_Mod.Settings.famineMoodEnabled ? 0f : base.MoodOffset();
        }
        public override bool VisibleInNeedsTab =>
            (RHAH_Mod.Settings == null || RHAH_Mod.Settings.famineMoodEnabled) && base.VisibleInNeedsTab;
    }
}
