using System.Collections.Generic;
using Verse;

namespace LeadYourPet
{
    public class LeadYourPetSettings : ModSettings
    {
        public const int DefaultMinPetAgeYears = 0;
        public const int DefaultMaxPetAgeYears = 14;
        public const int AbsoluteMaxPetAgeYears = 100;
        public const int DefaultOrdinaryTravelRatkinYoungCount = 2;
        public const int MaxOrdinaryTravelRatkinYoungCount = 8;

        public int maxLeashLength = 10;
        public int maxMouseEggPetLeashStartDistance = 10;
        public int minPetAgeYears = DefaultMinPetAgeYears;
        public int maxPetAgeYears = DefaultMaxPetAgeYears;
        public bool ignorePetAge;
        public int ordinaryTravelRatkinYoungCount = DefaultOrdinaryTravelRatkinYoungCount;
        public bool infiniteLeash;
        public bool showInteractionText = true;
        public bool allowAnimalPets = true;
        public bool allowRatkinYoungPets = true;
        public bool visitorsLeadRatkinYoung = true;
        public bool leashedHostilesCanAttack;
        public BoughtYoungRatkinStatus boughtYoungRatkinStatus = BoughtYoungRatkinStatus.Prisoner;
        public LeashedPawnMapExitBehavior leashedPawnMapExitBehavior = LeashedPawnMapExitBehavior.StayInPlace;
        private HashSet<string> enabledModFeedFoods = new HashSet<string>();
        private HashSet<string> disabledVanillaFeedFoods = new HashSet<string>();

        internal bool AllowsFeedFood(ThingDef food)
        {
            if (!LeadYourPetFeedFood.IsUsable(food)) return false;
            return LeadYourPetFeedFood.IsVanilla(food)
                ? !disabledVanillaFeedFoods.Contains(food.defName)
                : enabledModFeedFoods.Contains(food.defName);
        }

        internal void SetFeedFoodAllowed(ThingDef food, bool allowed)
        {
            HashSet<string> overrides = LeadYourPetFeedFood.IsVanilla(food) ? disabledVanillaFeedFoods : enabledModFeedFoods;
            if (allowed != LeadYourPetFeedFood.IsVanilla(food)) overrides.Add(food.defName);
            else overrides.Remove(food.defName);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxLeashLength, "maxLeashLength", 10);
            Scribe_Values.Look(ref maxMouseEggPetLeashStartDistance, "maxMouseEggPetLeashStartDistance", 10);
            Scribe_Values.Look(ref minPetAgeYears, "minPetAgeYears", DefaultMinPetAgeYears);
            Scribe_Values.Look(ref maxPetAgeYears, "maxPetAgeYears", DefaultMaxPetAgeYears);
            Scribe_Values.Look(ref ignorePetAge, "ignorePetAge", false);
            Scribe_Values.Look(ref ordinaryTravelRatkinYoungCount, "ordinaryTravelRatkinYoungCount", DefaultOrdinaryTravelRatkinYoungCount);
            Scribe_Values.Look(ref infiniteLeash, "infiniteLeash", false);
            Scribe_Values.Look(ref showInteractionText, "showInteractionText", true);
            Scribe_Values.Look(ref allowAnimalPets, "allowAnimalPets", true);
            Scribe_Values.Look(ref allowRatkinYoungPets, "allowRatkinYoungPets", true);
            Scribe_Values.Look(ref visitorsLeadRatkinYoung, "visitorsLeadRatkinYoung", true);
            Scribe_Values.Look(ref leashedHostilesCanAttack, "leashedHostilesCanAttack", false);
            Scribe_Values.Look(ref leashedPawnMapExitBehavior, "leashedPawnMapExitBehavior", LeashedPawnMapExitBehavior.StayInPlace);
            Scribe_Values.Look(ref boughtYoungRatkinStatus, "boughtYoungRatkinStatus", BoughtYoungRatkinStatus.Prisoner);
            Scribe_Collections.Look(ref enabledModFeedFoods, "enabledModFeedFoods", LookMode.Value);
            Scribe_Collections.Look(ref disabledVanillaFeedFoods, "disabledVanillaFeedFoods", LookMode.Value);
            if (enabledModFeedFoods == null) enabledModFeedFoods = new HashSet<string>();
            if (disabledVanillaFeedFoods == null) disabledVanillaFeedFoods = new HashSet<string>();
            Clamp();
        }

        public void Clamp()
        {
            LeadYourPetRules.ClampPetControlSettings(
                ref minPetAgeYears,
                ref maxPetAgeYears,
                ref ordinaryTravelRatkinYoungCount,
                ref maxLeashLength,
                ref maxMouseEggPetLeashStartDistance);
        }
    }
}
