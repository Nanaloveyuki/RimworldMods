using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    [DefOf]
    public static class MouseDisasterDefOf
    {
        public static DutyDef MouseDisaster_FamilyExit;
        public static DutyDef MouseDisaster_ReliefVisit;
        public static BackstoryDef MouseDisaster_Newborn;
        public static BackstoryDef MouseDisaster_Refugee;
        public static HediffDef MouseDisaster_Plague;
        public static HediffDef MouseDisaster_FedOnce;
        public static HediffDef MouseDisaster_RefeedingSyndrome;
        public static HediffDef MouseDisaster_GnawedTreeBark;
        public static HediffDef MouseDisaster_GnawedWall;
        public static HediffDef MouseDisaster_OverGnawedWall;
        public static HediffDef MouseDisaster_TemporaryShelterMark;
        public static HediffDef MouseDisaster_HiredWorkerMark;
        public static HediffDef MouseDisaster_DisasterRefugeeMark;

        public static HediffDef MouseDisaster_AbandonedEgg;
        public static HediffDef MouseDisaster_AbandoningMother;
        public static HediffDef MouseDisaster_ShatteredMother_Dying;
        public static HediffDef MouseDisaster_ShatteredMother_Children;
        public static HediffDef MouseDisaster_BeggarMother;
        public static HediffDef MouseDisaster_BeggarChild;
        public static HediffDef MouseDisaster_BeggarGroupAdult;
        public static HediffDef MouseDisaster_BeggarGroupChild;
        public static HediffDef MouseDisaster_ThiefAdult;
        public static HediffDef MouseDisaster_ThiefChild;
        public static HediffDef MouseDisaster_ThiefChildOnly;
        public static HediffDef MouseDisaster_LargeRefugeeAdult;
        public static HediffDef MouseDisaster_LargeRefugeeChild;
        public static HediffDef MouseDisaster_FamineRefugee;
        public static HediffDef MouseDisaster_WildWanderer;
        public static HediffDef MouseDisaster_WildChildWanderer;
        public static HediffDef MouseDisaster_WildGroupWanderer;
        public static HediffDef MouseDisaster_TraderCaravanTrader;
        public static HediffDef MouseDisaster_TraderCaravanEscort;
        public static HediffDef MouseDisaster_TraderCaravanChild;
        public static HediffDef MouseDisaster_ChildExchangeTrader;
        public static HediffDef MouseDisaster_ChildExchangeChild;
        public static HediffDef MouseDisaster_SiegeBeggar;
        public static HediffDef MouseDisaster_GreatFamineAdult;
        public static HediffDef MouseDisaster_GreatFamineChild;
        public static HediffDef MouseDisaster_GuanyinTuSatiety;

        public static MentalStateDef MouseDisaster_BeggingState;
        public static MentalStateDef MouseDisaster_ThievingState;
        public static JobDef MouseDisaster_BegForFood;
        public static JobDef MouseDisaster_BroadcastHope;
        public static JobDef MouseDisaster_GnawTreeBark;
        public static JobDef MouseDisaster_GnawWall;
        public static JobDef MouseDisaster_PrisonerScavenge;
        public static JobDef MouseDisaster_TailBite;
        public static PawnKindDef MouseDisaster_BeggarRatkinAdult;
        public static PawnKindDef MouseDisaster_BeggarRatkinChild;
        public static PawnKindDef MouseDisaster_TraderRatkinAdult;
        public static PawnKindDef MouseDisaster_TraderRatkinEscort;
        public static PawnKindDef MouseDisaster_ThiefRatkinAdult;
        public static PawnKindDef MouseDisaster_ThiefRatkinChild;
        public static PawnKindDef MouseDisaster_WildRatkinAdult;
        public static PawnKindDef MouseDisaster_WildRatkinChild;
        public static FactionDef MouseDisaster_HiddenFaction;
        public static FactionDef MouseDisaster_HostileVisitors;
        public static FactionDef MouseDisaster_NeutralVisitors;
        public static FactionDef MouseDisaster_FriendlyVisitors;
        public static LetterDef MouseDisaster_AcceptFamineRefugees;
        public static LetterDef MouseDisaster_ChildExchangeLetter;
        public static LetterDef MouseDisaster_FoodGiveLetter;
        public static LetterDef MouseDisaster_VisitorControlLetter;
        public static LetterDef MouseDisaster_RequestLetter;
        public static LetterDef MouseDisaster_AbandonedChildrenLetter;
        public static LetterDef MouseDisaster_N006Letter;
        public static LetterDef MouseDisaster_N007Letter;
        public static LetterDef MouseDisaster_StoryLetter;
        public static SitePartDef MouseDisaster_RecordSite;
        public static ThingDef MouseDisaster_RecordCache;
        public static ThingDef MouseDisaster_RecordPages;
        public static ThingDef MouseDisaster_GrainBurrow;
        public static ThingDef MouseDisaster_GuanyinTu;
        public static ThingDef Filth_MouseDisasterPoop;
        public static GeneDef MouseDisaster_Gene_MoreLitter;
        public static GeneDef MouseDisaster_Gene_PrimalFertility;
        public static GeneDef MouseDisaster_Gene_HyperBreeding;
        public static GeneDef MouseDisaster_Gene_ChaosFertility;
        public static GeneDef MouseDisaster_Gene_MoreComing;
        public static ThoughtDef MouseDisaster_ScavengedFloorDust;
        public static ThoughtDef MouseDisaster_ScavengedDirtyFilth;
        public static ThoughtDef MouseDisaster_ScavengedVomit;
        public static ThoughtDef MouseDisaster_ScavengedAmnioticFluid;
        public static ThoughtDef MouseDisaster_ScavengedBlood;
        public static ThoughtDef MouseDisaster_ScavengeToSurvive;
        public static ThoughtDef MouseDisaster_TailBitten;
        public static ThoughtDef MouseDisaster_TailBittenHatred;
        public static ThoughtDef MouseDisaster_TailBittenInjured;
        public static ThoughtDef MouseDisaster_TailBiteSuccess;
        public static ThoughtDef MouseDisaster_TailBiteFailed;
        public static ThoughtDef MouseDisaster_TailCut;
        public static ThoughtDef MouseDisaster_TailBruised;
        public static ThoughtDef MouseDisaster_EarTorn;
        public static ThoughtDef MouseDisaster_EarInjured;
        public static ThoughtDef MouseDisaster_BeggingSucceeded;
        public static ThoughtDef MouseDisaster_BeggingRejected;
        public static ThoughtDef MouseDisaster_SawBeggarRatkin_Kind;
        public static ThoughtDef MouseDisaster_SawBeggarRatkin_Psychopath;
        public static ThoughtDef MouseDisaster_BloodlineContinuation;
        public static ThoughtDef MouseDisaster_BloodlineRenewed;
        public static ThoughtDef MouseDisaster_OverGnawedWallThought;
        public static ThoughtDef MouseDisaster_ChildExchangeFamilyMood;
        public static ThoughtDef MouseDisaster_VisitorSheltered;
        public static ThoughtDef MouseDisaster_N004_MissingMother;
        public static ThoughtDef MouseDisaster_N004_SurvivedCaptivity;
        public static ThoughtDef MouseDisaster_N004_FamilySurvived;
        public static ThoughtDef MouseDisaster_N004_MotherRegret;

        static MouseDisasterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MouseDisasterDefOf));
        }
    }
}
