using RimWorld;
using Verse;

namespace Oxygendsl.Mod.Runtime
{
    public class ThoughtWorker_OxygenRule : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            return NativeRuleRuntime.EvaluateThought(def, pawn);
        }
    }
}
