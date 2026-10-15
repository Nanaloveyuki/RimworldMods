using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    internal static class MouseDisasterPawnGenerationPolicy
    {
        public static bool ShouldBlockUnnecessaryRelations(PawnGenerationRequest request)
        {
            if (MouseDisasterMod.Settings?.preventUnnecessaryNeutralPawnRelations == false ||
                !request.CanGeneratePawnRelations || request.ExtraPawnForExtraRelationChance != null ||
                request.AllowedDevelopmentalStages.Newborn() || request.KindDef == null ||
                string.IsNullOrEmpty(request.KindDef.defName) ||
                !request.KindDef.defName.StartsWith("MouseDisaster_", StringComparison.OrdinalIgnoreCase) ||
                request.Faction == Faction.OfPlayer || request.Faction?.IsPlayer == true)
            {
                return false;
            }

            return request.Faction == null || MouseDisasterUtility.IsMouseDisasterNeutralFaction(request.Faction);
        }

        public static bool AllowRelationsForDirectMouseDisasterGeneration()
        {
            return MouseDisasterMod.Settings?.preventUnnecessaryNeutralPawnRelations == false;
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    internal static class MouseDisasterPawnGenerationRelationsPatch
    {
        public static void Prefix(ref PawnGenerationRequest request)
        {
            if (MouseDisasterPawnGenerationPolicy.ShouldBlockUnnecessaryRelations(request))
            {
                request.CanGeneratePawnRelations = false;
            }
        }
    }
}
