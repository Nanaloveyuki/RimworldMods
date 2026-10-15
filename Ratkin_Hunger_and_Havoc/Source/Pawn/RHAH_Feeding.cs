using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal static class RHAH_Feeding
    {
        internal static bool TryComplete(Verse.Pawn pawn)
        {
            if (!RHAH_Api.IsVisitor(pawn) || !RHAH_VisitorRules.AllowsModBehavior(RHAH_VisitorStay.Kind(pawn)) ||
                pawn.needs == null || pawn.needs.food == null)
            {
                return false;
            }

            float level = pawn.needs.food.CurLevelPercentage;
            float satisfied = Core.RHAH_Mod.Settings == null ? RHAH_ReliefFood.SatisfiedLevel : Core.RHAH_Mod.Settings.satisfiedFoodPercent / 100f;
            if (float.IsNaN(level) || float.IsInfinity(level) || level < satisfied)
            {
                return false;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot == null)
            {
                return false;
            }

            bool already = snapshot.HasBeenFed;
            if (already)
            {
                return true;
            }

            RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.Fed);
            RHAH_FamineMood.NotifyFed(pawn);
            ScheduleStay(pawn);
            TryAddRefeeding(pawn);

            return RHAH_Api.Get(pawn) != null && RHAH_Api.Get(pawn).HasBeenFed;
        }
        static void ScheduleStay(Verse.Pawn pawn)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            Identity.CompRHAH_Pawn comp = Identity.CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return;
            }

            if (RHAH_ArmedDemand.Holds(pawn)) return;
            if (settings != null && !settings.leaveAfterFed)
            {
                return;
            }

            bool wander = settings == null || settings.fedWanderEnabled;
            int hours = settings == null ? RHAH_VisitorRules.DefaultFedWanderHours : settings.fedWanderHours;
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            comp.SetLeaveAfter(now + RHAH_VisitorRules.FedWanderTicks(wander, hours));
        }

        static void TryAddRefeeding(Verse.Pawn pawn)
        {
            if (pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            HediffDef syndrome = RHAH_DefOf.RHAH_RefeedingSyndrome;
            if (syndrome == null || pawn.health.hediffSet.HasHediff(syndrome))
            {
                return;
            }

            Hediff malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            float refeed = Core.RHAH_Mod.Settings == null ? RHAH_ReliefFood.RefeedMalnutrition : Core.RHAH_Mod.Settings.refeedMalnutrition;
            if (malnutrition == null || malnutrition.Severity < refeed)
            {
                return;
            }

            pawn.health.AddHediff(syndrome);
        }
    }
}
