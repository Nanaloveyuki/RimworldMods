using RimWorld;
using Verse;

namespace LeadYourPet
{
    internal static class LeadYourPetFeedFood
    {
        internal static bool IsUsable(ThingDef food)
        {
            return food != null && food.IsNutritionGivingIngestible && !food.IsDrug
                && food.category != ThingCategory.Pawn && food.category != ThingCategory.Building
                && (food.thingClass == null || !typeof(Corpse).IsAssignableFrom(food.thingClass));
        }

        internal static bool IsVanilla(ThingDef food)
        {
            return food?.modContentPack != null && food.modContentPack.IsOfficialMod;
        }

        internal static bool Allows(ThingDef food)
        {
            return LeadYourPetMod.Settings != null
                ? LeadYourPetMod.Settings.AllowsFeedFood(food)
                : IsUsable(food) && IsVanilla(food);
        }
    }
}
