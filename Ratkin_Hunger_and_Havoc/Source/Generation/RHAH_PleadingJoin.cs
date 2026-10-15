using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_PleadingJoin
    {
        static readonly string[] TraitNames = { "Kind", "NaturalMood", "Nerves", "Industriousness" };
        static readonly int[] TraitDegrees = { 0, 1, 2, 1 };
        static readonly TraitDef[] RequiredTraits = new TraitDef[TraitNames.Length];

        internal static float AdultAge(float? requested)
        {
            return requested.HasValue && !float.IsNaN(requested.Value) && !float.IsInfinity(requested.Value)
                ? System.Math.Max(18f, requested.Value)
                : 18f;
        }

        internal static bool Apply(Verse.Pawn pawn)
        {
            if (pawn?.story?.traits == null || pawn.skills == null || pawn.relations == null ||
                pawn.ageTracker == null || pawn.ageTracker.AgeBiologicalYearsFloat < 18f)
            {
                return false;
            }

            TraitDef[] required = RequiredTraits;
            for (int i = 0; i < required.Length; i++)
            {
                TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(TraitNames[i]);
                if (def?.modContentPack?.IsCoreMod != true || !HasDegree(def, TraitDegrees[i]))
                {
                    Log.Error("[RHAH] I-053 requires vanilla trait " + TraitNames[i] + " degree " + TraitDegrees[i]);
                    return false;
                }
                required[i] = def;
            }

            TraitSet traits = pawn.story.traits;
            // 不通过移除基因特质破坏已经核对的异种
            for (int i = 0; i < traits.allTraits.Count; i++)
            {
                Trait trait = traits.allTraits[i];
                if (trait.sourceGene == null) continue;
                for (int j = 0; j < required.Length; j++)
                {
                    if (required[j].ConflictsWith(trait) ||
                        (trait.def == required[j] && (trait.Degree != TraitDegrees[j] || trait.Suppressed)))
                    {
                        Log.Error("[RHAH] I-053 xenotype trait conflicts with " + TraitNames[j]);
                        return false;
                    }
                }
            }
            for (int i = traits.allTraits.Count - 1; i >= 0; i--)
            {
                Trait trait = traits.allTraits[i];
                if (trait.sourceGene == null) traits.RemoveTrait(trait, unsuppressConflicts: true);
            }
            for (int i = 0; i < required.Length; i++)
            {
                if (!traits.HasTrait(required[i], TraitDegrees[i]))
                {
                    traits.GainTrait(new Trait(required[i], TraitDegrees[i], true));
                }
            }
            for (int i = 0; i < required.Length; i++)
            {
                Trait trait = traits.GetTrait(required[i], TraitDegrees[i]);
                if (trait == null || trait.Suppressed) return false;
            }

            pawn.relations.ClearAllRelations();
            if (pawn.relations.RelatedToAnyoneOrAnyoneRelatedToMe) return false;
            for (int i = 0; i < pawn.skills.skills.Count; i++)
            {
                SkillRecord skill = pawn.skills.skills[i];
                skill.levelInt = Rand.RangeInclusive(10, 20);
                skill.xpSinceLastLevel = 0f;
                skill.xpSinceMidnight = 0f;
            }
            return true;
        }

        static bool HasDegree(TraitDef def, int degree)
        {
            if (def.degreeDatas == null) return false;
            for (int i = 0; i < def.degreeDatas.Count; i++)
            {
                if (def.degreeDatas[i]?.degree == degree) return true;
            }
            return false;
        }
    }
}
