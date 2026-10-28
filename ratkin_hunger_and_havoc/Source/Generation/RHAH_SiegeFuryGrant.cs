using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_SiegeFuryGrant
    {
        // 关掉整个索粮事件就不该还挂这份增益 所以两个开关都要开
        internal static bool GrantingEnabled =>
            RHAH_ArmedDemand.Enabled && (RHAH_Mod.Settings == null || RHAH_Mod.Settings.siegeFuryEnabled);

        internal static bool CanApply(bool enabled, bool hasVisitorSnapshot, bool isPlayerFaction, string incidentDisplayId)
        {
            return enabled && hasVisitorSnapshot && !isPlayerFaction && RHAH_ArmedDemand.IsSiege(incidentDisplayId);
        }

        internal static bool AppliesTo(Verse.Pawn pawn)
        {
            if (pawn == null) return false;
            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            return CanApply(GrantingEnabled, snapshot != null, pawn.Faction?.IsPlayer == true, snapshot?.SourceIncidentDisplayId);
        }

        internal static void Apply(Verse.Pawn pawn)
        {
            if (!AppliesTo(pawn) || pawn.health?.hediffSet == null) return;
            HediffDef def = RHAH_DefOf.RHAH_SiegeFury;
            if (def == null || pawn.health.hediffSet.HasHediff(def)) return;
            // 围攻者从 0 饥饿开始攒档 所以先撤掉携带的营养不良再挂增益
            Hediff carriedMalnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (carriedMalnutrition != null) pawn.health.RemoveHediff(carriedMalnutrition);
            pawn.health.AddHediff(def);
        }
    }
}
