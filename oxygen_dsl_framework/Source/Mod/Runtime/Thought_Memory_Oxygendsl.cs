using RimWorld;
using Verse;

namespace Oxygendsl.Mod
{
    public sealed class Thought_Memory_Oxygendsl : Thought_Memory
    {
        public string ruleId;
        public string ruleLabel;
        public string ruleDescription;
        public float ruleOffset;
        public int triggeredTick;
        public int expireTick = -1;
        public bool persistent;

        public override bool VisibleInNeedsTab
        {
            get { return base.VisibleInNeedsTab && HasContent; }
        }

        public override int DurationTicks
        {
            get
            {
                if (persistent || expireTick < 0) return int.MaxValue;
                int span = expireTick - triggeredTick;
                return span < 1 ? 1 : span;
            }
        }

        public override bool ShouldDiscard
        {
            get
            {
                if (persistent || expireTick < 0) return false;
                return Find.TickManager.TicksGame >= expireTick;
            }
        }

        public override string LabelCap
        {
            get { return string.IsNullOrEmpty(ruleLabel) ? base.LabelCap : ruleLabel.CapitalizeFirst(); }
        }

        public override string Description
        {
            get
            {
                string text = ruleDescription ?? string.Empty;
                string nullified = ThoughtUtility.ThoughtNullifiedMessage(pawn, def);
                if (!string.IsNullOrEmpty(nullified)) text = text + "\n\n(" + nullified + ")";
                return text;
            }
        }

        public override float MoodOffset()
        {
            if (!HasContent || ShouldDiscard || ThoughtUtility.ThoughtNullified(pawn, def)) return 0f;
            return ruleOffset;
        }

        public override bool GroupsWith(Thought other)
        {
            Thought_Memory_Oxygendsl owned = other as Thought_Memory_Oxygendsl;
            if (owned == null) return false;
            return !string.IsNullOrEmpty(ruleId) && ruleId == owned.ruleId;
        }

        public override bool TryMergeWithExistingMemory(out bool showBubble)
        {
            showBubble = false;
            return false;
        }
        public override void ThoughtInterval()
        {
            int ageFromTrigger = Find.TickManager.TicksGame - triggeredTick;
            age = ageFromTrigger < 0 ? 0 : ageFromTrigger;
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) ThoughtInterval();
            base.ExposeData();
            Scribe_Values.Look(ref ruleId, "oxygendslRuleId");
            Scribe_Values.Look(ref ruleLabel, "oxygendslLabel");
            Scribe_Values.Look(ref ruleDescription, "oxygendslDescription");
            Scribe_Values.Look(ref ruleOffset, "oxygendslOffset", 0f);
            Scribe_Values.Look(ref triggeredTick, "oxygendslTriggeredTick", 0);
            Scribe_Values.Look(ref expireTick, "oxygendslExpireTick", -1);
            Scribe_Values.Look(ref persistent, "oxygendslPersistent", false);
        }

        bool HasContent
        {
            get { return !string.IsNullOrEmpty(ruleId); }
        }
    }
}
