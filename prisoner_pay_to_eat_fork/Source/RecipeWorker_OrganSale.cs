using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>Vanilla organ-removal surgery with a final safety gate and success-only payment.</summary>
    public class RecipeWorker_OrganSale : Recipe_RemoveBodyPart
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.Dead || !pawn.IsPrisonerOfColony) yield break;
            var parts = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (OrganHarvestHelper.MatchesRecipe(part, recipe) && OrganHarvestHelper.CanRemove(pawn, part))
                    yield return part;
            }
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing is Pawn pawn && pawn.IsPrisonerOfColony && !pawn.Dead
                && PrisonersPayToEat2Manager.Current?.CanHarvestOrgans(pawn) == true
                && base.AvailableOnNow(thing, part)
                && (part == null || OrganHarvestHelper.MatchesRecipe(part, recipe));
        }

        public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part) => recipe.LabelCap;

        public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
        {
            return PrisonersPayToEat2Mod.Settings.organSaleAffectsGoodwill
                && base.IsViolationOnPawn(pawn, part, billDoerFaction);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // Recheck immediately before any damage: an old bill may outlive its eligibility,
            // another operation, a healed balance, changed anatomy or prisoner status.
            if (billDoer?.Map == null || pawn?.Map != billDoer.Map
                || !AvailableOnNow(pawn, part) || !OrganHarvestHelper.CanRemove(pawn, part)
                || (bill is Bill_OrganSale && !OrganSaleRequests.BelowTrigger(pawn))) return;

            bool violation = IsViolationOnPawn(pawn, part, Faction.OfPlayer);
            // Vanilla failure can injure/kill or even destroy the intended part. Do not infer
            // success from a missing part after this returns, and never award tickets on failure.
            if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            Thing organ = MedicalRecipesUtility.SpawnNaturalPartIfClean(pawn, part, billDoer.Position, billDoer.Map);
            DamagePart(pawn, part);
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            ApplyThoughts(pawn, billDoer);
            if (violation) ReportViolation(pawn, billDoer, pawn.HomeFaction, -70);
            if (organ != null && !pawn.Dead && pawn.IsPrisonerOfColony && !OrganHarvestHelper.IsPresent(pawn, part))
                OrganHarvestHelper.OnOrganRemoved(pawn, part);
        }
    }
}
