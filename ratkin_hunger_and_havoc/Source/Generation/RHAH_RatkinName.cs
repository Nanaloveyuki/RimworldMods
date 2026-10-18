using RimWorld;
using Verse;
using Verse.Grammar;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_RatkinName
    {
        // 原版先抽固定传记和异种名库 种族 nameGenerator 排在后面
        internal static void Apply(Verse.Pawn pawn)
        {
            RulePackDef maker = Maker(pawn);
            pawn.Name = Choose(pawn?.Name, maker, Generated(maker));
        }

        static string Generated(RulePackDef maker)
        {
            if (maker == null)
            {
                return null;
            }

            return NameGenerator.GenerateName(
                maker,
                candidate => !NameTriple.FromString(candidate, false).UsedThisGame,
                false,
                null,
                null,
                null);
        }

        internal static Name Choose(Name current, RulePackDef maker, string generated)
        {
            if (maker == null || string.IsNullOrEmpty(generated))
            {
                return current;
            }

            return NameTriple.FromString(generated, false);
        }

        internal static RulePackDef Maker(Verse.Pawn pawn)
        {
            return pawn?.RaceProps?.GetNameGenerator(pawn.gender);
        }
    }
}
