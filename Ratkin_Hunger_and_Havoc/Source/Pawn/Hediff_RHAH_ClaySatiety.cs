using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    public class Hediff_RHAH_ClaySatiety : HediffWithComps
    {
        int windowStartTick = -1;
        int ingestionCount;
        internal int ActiveBites => RHAH_ClaySatiety.FreshStart(
            Find.TickManager?.TicksGame ?? 0, windowStartTick, GenDate.TicksPerDay) < 0 ? 0 : ingestionCount;

        public override bool ShouldRemove =>
            RHAH_ClaySatiety.ShouldRemove(Severity, ingestionCount, windowStartTick);

        public override string LabelInBrackets
        {
            get
            {
                int shown = ingestionCount < 0 ? 0 : ingestionCount;
                int maxBites = RHAH_ClaySatiety.Limit;
                return shown + "/" + maxBites;
            }
        }

        internal static bool CanEat(Verse.Pawn pawn, Thing food)
        {
            return CanEat(pawn, food != null ? food.def : null);
        }

        internal static bool CanEat(Verse.Pawn pawn, ThingDef food)
        {
            if (food == null || food != HungerAndHavoc.Core.RHAH_DefOf.RHAH_GuanyinTu)
            {
                return true;
            }

            Hediff_RHAH_ClaySatiety satiety = Get(pawn);
            if (satiety == null)
            {
                return true;
            }

            int tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            return satiety.CanEatNow(tick);
        }

        internal static void NotifyEaten(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return;
            }

            HediffDef def = HungerAndHavoc.Core.RHAH_DefOf.RHAH_ClaySatiety;
            if (def == null)
            {
                return;
            }

            int tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            Hediff_RHAH_ClaySatiety satiety = Get(pawn);
            if (satiety == null)
            {
                satiety = HediffMaker.MakeHediff(def, pawn) as Hediff_RHAH_ClaySatiety;
                if (satiety == null)
                {
                    return;
                }

                pawn.health.AddHediff(satiety);
            }

            satiety.RegisterBite(tick);
        }

        public bool CanEatNow(int tick)
        {
            Refresh(tick);
            return ingestionCount < RHAH_ClaySatiety.Limit;
        }

        public bool RegisterBite(int tick)
        {
            Refresh(tick);
            ClayBite bite = RHAH_ClaySatiety.Register(
                tick,
                windowStartTick,
                ingestionCount,
                Severity,
                GenDate.TicksPerDay);
            if (!bite.Accepted)
            {
                return false;
            }

            if (windowStartTick < 0)
            {
                windowStartTick = tick;
            }

            ingestionCount = bite.Count;
            Severity = bite.Severity;
            return true;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            int tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            Severity = RHAH_ClaySatiety.Decay(Severity, delta, GenDate.TicksPerDay);
            Refresh(tick);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref windowStartTick, "windowStartTick", -1);
            Scribe_Values.Look(ref ingestionCount, "ingestionCount", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ingestionCount < 0)
                {
                    ingestionCount = 0;
                }

            }
        }

        static Hediff_RHAH_ClaySatiety Get(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return null;
            }

            HediffDef def = HungerAndHavoc.Core.RHAH_DefOf.RHAH_ClaySatiety;
            return def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_RHAH_ClaySatiety;
        }

        void Refresh(int tick)
        {
            int start = RHAH_ClaySatiety.FreshStart(tick, windowStartTick, GenDate.TicksPerDay);
            if (start < 0)
            {
                windowStartTick = -1;
                ingestionCount = 0;
            }
        }
    }
}
