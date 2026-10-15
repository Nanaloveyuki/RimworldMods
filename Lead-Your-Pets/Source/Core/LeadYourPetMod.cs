using UnityEngine;
using Verse;

namespace LeadYourPet
{
    public class LeadYourPetMod : Mod
    {
        public static LeadYourPetSettings Settings;

        public LeadYourPetMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LeadYourPetSettings>();
            IrisMenusCompat.TryRegister(this);
        }

        public override string SettingsCategory()
        {
            return "牵着你的宠物 Continued";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            bool infinite = Settings.infiniteLeash;
            listing.CheckboxLabeled("拴绳无限长度", ref infinite, "开启后忽略最大拴绳长度。");
            Settings.infiniteLeash = infinite;

            bool showInteractionText = Settings.showInteractionText;
            listing.CheckboxLabeled("显示头顶浮字", ref showInteractionText, "关闭后不再显示互动时的头顶文字。");
            Settings.showInteractionText = showInteractionText;
            bool allowAnimalPets = Settings.allowAnimalPets;
            listing.CheckboxLabeled("LeadYourPet_Settings_AllowAnimalPets".Translate(), ref allowAnimalPets, "LeadYourPet_Settings_AllowAnimalPets_Tooltip".Translate());
            Settings.allowAnimalPets = allowAnimalPets;
            listing.CheckboxLabeled("LeadYourPet_Settings_LeashedHostilesCanAttack".Translate(), ref Settings.leashedHostilesCanAttack,
                "LeadYourPet_Settings_LeashedHostilesCanAttack_Tooltip".Translate());

            bool allowRatkinYoungPets = Settings.allowRatkinYoungPets;
            listing.CheckboxLabeled("LeadYourPet_Settings_AllowRatkinYoungPets".Translate(), ref allowRatkinYoungPets, "LeadYourPet_Settings_AllowRatkinYoungPets_Tooltip".Translate());
            Settings.allowRatkinYoungPets = allowRatkinYoungPets;

            bool visitorsLeadRatkinYoung = Settings.visitorsLeadRatkinYoung;
            listing.CheckboxLabeled("LeadYourPet_Settings_VisitorsLeadRatkinYoung".Translate(), ref visitorsLeadRatkinYoung, "LeadYourPet_Settings_VisitorsLeadRatkinYoung_Tooltip".Translate());
            Settings.visitorsLeadRatkinYoung = visitorsLeadRatkinYoung;

            listing.CheckboxLabeled("LeadYourPet_Settings_IgnorePetAge".Translate(), ref Settings.ignorePetAge,
                "LeadYourPet_Settings_IgnorePetAge_Tooltip".Translate());
            listing.Label("LeadYourPet_Settings_PetAge_Note".Translate());

            listing.Label("LeadYourPet_Settings_MinPetAge".Translate(Settings.minPetAgeYears));
            Settings.minPetAgeYears = Mathf.RoundToInt(listing.Slider(Settings.minPetAgeYears, 0f, LeadYourPetSettings.AbsoluteMaxPetAgeYears));
            if (Settings.maxPetAgeYears < Settings.minPetAgeYears)
            {
                Settings.maxPetAgeYears = Settings.minPetAgeYears;
            }

            listing.Label("LeadYourPet_Settings_MaxPetAge".Translate(Settings.maxPetAgeYears));
            Settings.maxPetAgeYears = Mathf.RoundToInt(listing.Slider(Settings.maxPetAgeYears, Settings.minPetAgeYears, LeadYourPetSettings.AbsoluteMaxPetAgeYears));

            if (Settings.visitorsLeadRatkinYoung)
            {
                listing.Label("LeadYourPet_Settings_OrdinaryTravelRatkinYoungCount".Translate(Settings.ordinaryTravelRatkinYoungCount));
                Settings.ordinaryTravelRatkinYoungCount = Mathf.RoundToInt(listing.Slider(Settings.ordinaryTravelRatkinYoungCount, 0f, LeadYourPetSettings.MaxOrdinaryTravelRatkinYoungCount));
            }

            listing.Label("LeadYourPet_Settings_BoughtYoungRatkinStatus".Translate().Resolve());
            if (listing.RadioButton("LeadYourPet_Settings_BoughtYoungRatkinStatus_Colonist".Translate().Resolve(), Settings.boughtYoungRatkinStatus == BoughtYoungRatkinStatus.Colonist))
            {
                Settings.boughtYoungRatkinStatus = BoughtYoungRatkinStatus.Colonist;
            }

            if (listing.RadioButton("LeadYourPet_Settings_BoughtYoungRatkinStatus_Prisoner".Translate().Resolve(), Settings.boughtYoungRatkinStatus == BoughtYoungRatkinStatus.Prisoner))
            {
                Settings.boughtYoungRatkinStatus = BoughtYoungRatkinStatus.Prisoner;
            }

            if (ModsConfig.IdeologyActive && listing.RadioButton("LeadYourPet_Settings_BoughtYoungRatkinStatus_Slave".Translate().Resolve(), Settings.boughtYoungRatkinStatus == BoughtYoungRatkinStatus.Slave))
            {
                Settings.boughtYoungRatkinStatus = BoughtYoungRatkinStatus.Slave;
            }

            Settings.Clamp();

            if (!Settings.infiniteLeash)
            {
                listing.Label($"拴绳最高长度: {Settings.maxLeashLength}");
                Settings.maxLeashLength = Mathf.RoundToInt(listing.Slider(Settings.maxLeashLength, 3f, 30f));
            }
            else
            {
                listing.Label("拴绳最高长度: 无限");
            }

            listing.Label($"{"LeadYourPet_Settings_MouseEggLeashStartDistance".Translate().Resolve()} {Settings.maxMouseEggPetLeashStartDistance}");
            Settings.maxMouseEggPetLeashStartDistance = Mathf.RoundToInt(listing.Slider(Settings.maxMouseEggPetLeashStartDistance, 1f, 30f));

            listing.Label("LeadYourPet_Settings_LeashedPawnMapExitBehavior".Translate().Resolve());
            if (listing.RadioButton("LeadYourPet_Settings_LeashedPawnMapExitBehavior_StayInPlace".Translate().Resolve(), Settings.leashedPawnMapExitBehavior == LeashedPawnMapExitBehavior.StayInPlace))
            {
                Settings.leashedPawnMapExitBehavior = LeashedPawnMapExitBehavior.StayInPlace;
            }

            if (listing.RadioButton("LeadYourPet_Settings_LeashedPawnMapExitBehavior_LeaveMap".Translate().Resolve(), Settings.leashedPawnMapExitBehavior == LeashedPawnMapExitBehavior.LeaveMap))
            {
                Settings.leashedPawnMapExitBehavior = LeashedPawnMapExitBehavior.LeaveMap;
            }

            if (listing.RadioButton("LeadYourPet_Settings_LeashedPawnMapExitBehavior_Disappear".Translate().Resolve(), Settings.leashedPawnMapExitBehavior == LeashedPawnMapExitBehavior.Disappear))
            {
                Settings.leashedPawnMapExitBehavior = LeashedPawnMapExitBehavior.Disappear;
            }

            if (listing.ButtonText("LeadYourPet_Menu_Feeding".Translate()))
            {
                Find.WindowStack.Add(new Dialog_LeadYourPetFeeding());
            }

            listing.End();
            Settings.Write();
        }
    }
}
