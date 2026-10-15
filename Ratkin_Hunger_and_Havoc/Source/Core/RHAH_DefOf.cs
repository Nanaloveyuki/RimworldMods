using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Core
{
    [DefOf]
    public static class RHAH_DefOf
    {
        public static HediffDef RHAH_HungerMark;
        public static HediffDef RHAH_Plague;
        public static HediffDef RHAH_RefeedingSyndrome;
        public static HediffDef RHAH_GnawedBark;
        public static HediffDef RHAH_GnawedWall;
        public static HediffDef RHAH_OvergnawedWall;
        public static HediffDef RHAH_ClaySatiety;
        public static ThingDef RHAH_GuanyinTu;
        public static JobDef RHAH_Beg;
        public static JobDef RHAH_Gnaw;
        public static JobDef RHAH_DropChild;
        public static JobDef RHAH_MotherFeed;
        public static JobDef RHAH_Scavenge;
        public static JobDef RHAH_TailBite;
        public static JobDef RHAH_Expel;
        public static JobDef RHAH_BroadcastHope;
        public static JobDef RHAH_CarryYoung;
        public static ThoughtDef RHAH_Thought_ScavengedFilth;
        public static ThoughtDef RHAH_Thought_TailBitten;
        public static ThoughtDef RHAH_Thought_BitATail;
        public static DutyDef RHAH_VisitorSeek;
        public static DutyDef RHAH_VisitorLeave;
        public static ThinkTreeDef RHAH_VisitorFallback;
        public static FactionDef RHAH_Faction_Hostile;
        public static FactionDef RHAH_Faction_LeaningHostile;
        public static FactionDef RHAH_Faction_Neutral;
        public static FactionDef RHAH_Faction_LeaningFriendly;
        public static FactionDef RHAH_Faction_Friendly;
        public static LetterDef RHAH_ChoiceRequest;
        public static LetterDef RHAH_ChoiceVisitors;
        public static LetterDef RHAH_GreenIncident;
        public static LetterDef RHAH_ChoiceRequestGreen;
        public static LetterDef RHAH_ChoiceVisitorsGreen;
        public static PawnKindDef RHAH_PawnKind_Ratkin;
        public static GeneDef RHAH_Gene_LargeLitter;
        public static GeneDef RHAH_Gene_EarlyFertility;
        public static GeneDef RHAH_Gene_HighFertility;
        public static GeneDef RHAH_Gene_RoomFertility;
        public static GeneDef RHAH_Gene_FastBirth;

        static RHAH_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RHAH_DefOf));
        }
    }
}
