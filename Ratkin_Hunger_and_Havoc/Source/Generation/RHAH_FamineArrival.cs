using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_FamineArrival
    {
        internal static bool Applies(bool enabled, bool marked, bool player, bool fresh)
        {
            return enabled && marked && !player && fresh;
        }

        internal static void Initialize(Verse.Pawn pawn)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (!Applies(settings == null || settings.famineArrivalEnabled, RHAH_Api.Get(pawn) != null,
                pawn.Faction?.IsPlayer == true, RHAH_Api.Get(pawn)?.Lifecycle == RHAH_Lifecycle.Arriving)) return;
            if (pawn.needs?.food != null) pawn.needs.food.CurLevel = 0f;
            if (pawn.health?.hediffSet == null) return;
            Hediff malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition == null)
            {
                malnutrition = HediffMaker.MakeHediff(HediffDefOf.Malnutrition, pawn);
                pawn.health.AddHediff(malnutrition);
            }
            float severity = settings == null ? 0.25f : settings.famineArrivalMalnutrition;
            if (malnutrition.Severity < severity) malnutrition.Severity = severity;
        }
    }
}
