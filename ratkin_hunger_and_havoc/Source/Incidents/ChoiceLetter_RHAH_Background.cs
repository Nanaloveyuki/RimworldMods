using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // Verse 创建并恢复选择信
    public sealed class ChoiceLetter_RHAH_Background : ChoiceLetter
    {
        RHAH_BackgroundStory backgroundStory;
        internal RHAH_BackgroundStory Story { get => backgroundStory; set => backgroundStory = value; }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return new DiaOption(("RHAH_Background_" + backgroundStory + "_Option").Translate())
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref backgroundStory, "backgroundStory", RHAH_BackgroundStory.Rebellion);
        }
    }
}
