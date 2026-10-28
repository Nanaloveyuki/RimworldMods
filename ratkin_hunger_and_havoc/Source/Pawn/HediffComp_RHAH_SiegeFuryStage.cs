using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    public class HediffCompProperties_RHAH_SiegeFuryStage : HediffCompProperties
    {
        public HediffCompProperties_RHAH_SiegeFuryStage()
        {
            compClass = typeof(HediffComp_RHAH_SiegeFuryStage);
        }
    }

    public class HediffComp_RHAH_SiegeFuryStage : HediffComp
    {
        // 每个值必须落在 RHAH_SiegeFury.xml 第 n 档 minSeverity 区间 否则档位会串
        static readonly float[] SeverityPerStage = { 0.10f, 0.50f, 0.90f };

        internal static int StageOf(float malnutritionSeverity)
        {
            if (malnutritionSeverity >= 0.50f) return 2;
            if (malnutritionSeverity >= 0.25f) return 1;
            return 0;
        }

        internal static float SeverityForStage(float malnutritionSeverity)
        {
            return SeverityPerStage[StageOf(malnutritionSeverity)];
        }

        internal static float MalnutritionSeverity(Verse.Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition)?.Severity ?? 0f;
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            float malnutrition = MalnutritionSeverity(Pawn);
            parent.Severity = SeverityForStage(malnutrition);
            RHAH_FamineMood.SetSiegeFuryMood(Pawn, StageOf(malnutrition));
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!Pawn.IsHashIntervalTick(GenDate.TicksPerHour)) return;
            float malnutrition = MalnutritionSeverity(Pawn);
            float target = SeverityForStage(malnutrition);
            if (target == parent.Severity) return;
            severityAdjustment += target - parent.Severity;
            RHAH_FamineMood.SetSiegeFuryMood(Pawn, StageOf(malnutrition));
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RHAH_FamineMood.ClearSiegeFuryMood(Pawn);
        }
    }
}
