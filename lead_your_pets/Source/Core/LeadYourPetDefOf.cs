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
        public static JobDef LeadYourPet_MouseEggInteraction;
        public static JobDef LeadYourPet_CarryTravelMouseEgg;

        public static HediffDef LeadYourPet_DragBruise;
        public static HediffDef LeadYourPet_DragSlow;

        public static ThoughtDef LeadYourPet_LeadingPetMood;
        public static ThoughtDef LeadYourPet_MouseEggOwnerMood;
        public static ThoughtDef LeadYourPet_MouseEggFearMood;
        public static ThoughtDef LeadYourPet_MouseEggParentPetMood;
        public static ThoughtDef LeadYourPet_MouseEggCaretakerMood;
        public static ThoughtDef LeadYourPet_MouseEggWatchedMood;
        public static ThoughtDef LeadYourPet_MouseEggCryingNearbyMood;
        public static ThoughtDef LeadYourPet_RatkinMotherLeashMood;
        public static ThoughtDef LeadYourPet_RatkinBabyLeashedMood;
        public static ThoughtDef LeadYourPet_MouseDisasterBeggarMotherBonusMood;
        public static ThoughtDef LeadYourPet_MouseDisasterBeggarBabyBonusMood;
        public static ThoughtDef LeadYourPet_MouseDisasterChildExchangeMotherBonusMood;
        public static ThoughtDef LeadYourPet_MouseDisasterChildExchangeBabyBonusMood;
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
