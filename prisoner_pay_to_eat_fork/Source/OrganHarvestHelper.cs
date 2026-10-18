using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>Shared anatomy checks for proposals, medical bills and the final surgery boundary.</summary>
    public static class OrganHarvestHelper
    {
        public const string RecipeDefKidney = "PPTE2_RemoveKidney";
        public const string RecipeDefLungLobe = "PPTE2_RemoveLungLobe";
        public const string RecipeDefOther = "PPTE2_RemoveOtherOrgan";

        public static string OrganKey(BodyPartRecord part)
        {
            string name = part?.def?.defName;
            return name == "Lung" ? "LungLobe" : name;
        }

        public static int OrganPayout(string organKey) => organKey switch
        {
            "Kidney" => 40,
            "LungLobe" => 35,
            _ => 30
        };

        public static RecipeDef RecipeFor(BodyPartRecord part)
        {
            string key = OrganKey(part);
            string name = key == "Kidney" ? RecipeDefKidney
                : key == "LungLobe" ? RecipeDefLungLobe : RecipeDefOther;
            return DefDatabase<RecipeDef>.GetNamedSilentFail(name);
        }

        public static bool MatchesRecipe(BodyPartRecord part, RecipeDef recipe)
        {
            string key = OrganKey(part);
            if (key == null || recipe == null) return false;
            if (recipe.defName == RecipeDefKidney) return key == "Kidney";
            if (recipe.defName == RecipeDefLungLobe) return key == "LungLobe";
            return recipe.defName == RecipeDefOther && key != "Kidney" && key != "LungLobe";
        }

        public static bool IsPresent(Pawn pawn, BodyPartRecord part)
        {
            if (part == null || pawn?.health == null || part.body != pawn.RaceProps.body) return false;
            for (var current = part; current != null; current = current.parent)
                if (pawn.health.hediffSet.PartIsMissing(current)) return false;
            return true;
        }

        private static int PresentCount(Pawn pawn, BodyPartDef def)
        {
            int count = 0;
            var parts = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].def == def && IsPresent(pawn, parts[i])) count++;
            return count;
        }

        public static bool CanRemove(Pawn pawn, BodyPartRecord part)
        {
            var manager = PrisonersPayToEat2Manager.Current;
            if (pawn == null || pawn.Dead || !pawn.IsPrisonerOfColony || manager == null
                || !manager.CanHarvestOrgans(pawn) || !IsPresent(pawn, part)) return false;
            // Only natural internal organs that vanilla can actually spawn as items. Never limbs
            // or organs hidden under artificial replacements.
            if (part.depth != BodyPartDepth.Inside || part.def.conceptual
                || pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(part)
                || !MedicalRecipesUtility.IsCleanAndDroppable(pawn, part)
                || manager.IsOrganTaken(pawn, OrganKey(part))) return false;
            string key = OrganKey(part);
            if ((key == "Kidney" || key == "LungLobe") && PresentCount(pawn, part.def) < 2) return false;
            // Counting paired organs alone misses damage/disease in the remaining one.
            // This predicts direct removal only, not complications or surgical failure.
            return !pawn.health.WouldDieAfterAddingHediff(HediffDefOf.MissingBodyPart, part, 1f);
        }

        public static BodyPartRecord SelectOrgan(Pawn pawn)
        {
            if (pawn?.RaceProps?.body == null) return null;
            BodyPartRecord best = null;
            int bestPriority = int.MaxValue;
            var parts = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                if (part.depth != BodyPartDepth.Inside || part.def.spawnThingOnRemoved == null) continue;
                string key = OrganKey(part);
                int priority = key == "Kidney" ? 0 : key == "LungLobe" ? 1
                    : PresentCount(pawn, part.def) >= 2 ? 2 : 3;
                // Body-definition order breaks ties deterministically without sorting/allocating.
                if (priority >= bestPriority) continue;
                var recipe = RecipeFor(part);
                if (recipe == null || !recipe.Worker.AvailableOnNow(pawn, part) || !CanRemove(pawn, part)) continue;
                best = part;
                bestPriority = priority;
            }
            return best;
        }

        public static void OnOrganRemoved(Pawn prisoner, BodyPartRecord part)
        {
            var manager = PrisonersPayToEat2Manager.Current;
            string key = OrganKey(part);
            if (prisoner == null || manager == null || key == null || manager.IsOrganTaken(prisoner, key)) return;
            manager.MarkOrganTaken(prisoner, key);
            int pay = OrganPayout(key);
            manager.AddTickets(prisoner, pay);
            if (PrisonersPayToEat2Mod.Settings.logVerbose)
                Log.Message($"[PPTE2] {prisoner.LabelShortCap} sold {key} for {pay} tickets. Balance={manager.Balance(prisoner)}");
            Messages.Message("PPTE2_OrganSold".Translate(prisoner.LabelShortCap, part.Label, pay, PPTEName.Ticket),
                prisoner, MessageTypeDefOf.NeutralEvent);
        }
    }
}
