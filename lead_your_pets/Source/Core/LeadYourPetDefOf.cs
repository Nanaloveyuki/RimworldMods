using RimWorld;
using Verse;

namespace LeadYourPet
{
    [DefOf]
    public static class LeadYourPetDefOf
    {
        public static JobDef LeadYourPet_BeginLeash;
        public static JobDef LeadYourPet_EndLeash;
        public static JobDef LeadYourPet_FollowMaster;
        [DefAlias("LeadYourPet_MouseEggInteraction")]
        public static JobDef LeadYourPet_YoungRatkinInteraction;
        [DefAlias("LeadYourPet_CarryTravelMouseEgg")]
        public static JobDef LeadYourPet_CarryTravelYoungRatkin;

        public static HediffDef LeadYourPet_DragBruise;
        public static HediffDef LeadYourPet_DragSlow;

        public static ThoughtDef LeadYourPet_LeadingPetMood;
        [DefAlias("LeadYourPet_MouseEggOwnerMood")]
        public static ThoughtDef LeadYourPet_YoungRatkinOwnerMood;
        [DefAlias("LeadYourPet_MouseEggFearMood")]
        public static ThoughtDef LeadYourPet_YoungRatkinFearMood;
        [DefAlias("LeadYourPet_MouseEggParentPetMood")]
        public static ThoughtDef LeadYourPet_YoungRatkinParentPetMood;
        [DefAlias("LeadYourPet_MouseEggCaretakerMood")]
        public static ThoughtDef LeadYourPet_YoungRatkinCaretakerMood;
        [DefAlias("LeadYourPet_MouseEggWatchedMood")]
        public static ThoughtDef LeadYourPet_YoungRatkinWatchedMood;
        [DefAlias("LeadYourPet_MouseEggCryingNearbyMood")]
        public static ThoughtDef LeadYourPet_YoungRatkinCryingNearbyMood;
        public static ThoughtDef LeadYourPet_RatkinMotherLeashMood;
        public static ThoughtDef LeadYourPet_RatkinBabyLeashedMood;
        [DefAlias("LeadYourPet_MouseDisasterBeggarMotherBonusMood")]
        public static ThoughtDef LeadYourPet_RatkinDisasterBeggarMotherBonusMood;
        [DefAlias("LeadYourPet_MouseDisasterBeggarBabyBonusMood")]
        public static ThoughtDef LeadYourPet_RatkinDisasterBeggarBabyBonusMood;
        [DefAlias("LeadYourPet_MouseDisasterChildExchangeMotherBonusMood")]
        public static ThoughtDef LeadYourPet_RatkinDisasterChildExchangeMotherBonusMood;
        [DefAlias("LeadYourPet_MouseDisasterChildExchangeBabyBonusMood")]
        public static ThoughtDef LeadYourPet_RatkinDisasterChildExchangeBabyBonusMood;
        public static ThoughtDef LeadYourPet_OwnerPlayWithChild;
        public static ThoughtDef LeadYourPet_ChildPlayedWithCaretaker;

        public static ThoughtDef LeadYourPet_OwnerKick;
        public static ThoughtDef LeadYourPet_PetKick;
        public static ThoughtDef LeadYourPet_OwnerObserve;
        public static ThoughtDef LeadYourPet_PetObserve;
        public static ThoughtDef LeadYourPet_OwnerDrag;
        public static ThoughtDef LeadYourPet_PetDrag;
        public static ThoughtDef LeadYourPet_OwnerTapHead;
        public static ThoughtDef LeadYourPet_PetTapHead;
        public static ThoughtDef LeadYourPet_OwnerPullTail;
        public static ThoughtDef LeadYourPet_PetPullTail;
        public static ThoughtDef LeadYourPet_OwnerPullEar;
        public static ThoughtDef LeadYourPet_PetPullEar;
        public static ThoughtDef LeadYourPet_OwnerWhirl;
        public static ThoughtDef LeadYourPet_PetWhirl;
        public static ThoughtDef LeadYourPet_OwnerSlap;
        public static ThoughtDef LeadYourPet_PetSlap;
        public static ThoughtDef LeadYourPet_OwnerPat;
        public static ThoughtDef LeadYourPet_PetPat;
        public static ThoughtDef LeadYourPet_OwnerKickButt;
        public static ThoughtDef LeadYourPet_PetKickButt;
        public static ThoughtDef LeadYourPet_PetWatchWork;
        public static ThoughtDef LeadYourPet_PetFed;
        public static ThoughtDef LeadYourPet_PetRestAtFeet;
        public static ThoughtDef LeadYourPet_PetIdleNearOwner;
        public static ThoughtDef LeadYourPet_PetStudyFloor;
        public static ThoughtDef LeadYourPet_PetSillySmile;
        public static ThoughtDef LeadYourPet_PetTailPetting;
        public static ThoughtDef LeadYourPet_PetEarPetting;
        public static ThoughtDef LeadYourPet_PetOwnerWaited;
        public static ThoughtDef LeadYourPet_PetSnack;

        static LeadYourPetDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LeadYourPetDefOf));
        }
    }
}
