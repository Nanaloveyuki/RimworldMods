using RimWorld;
using Toddlers; // ToddlerPlayDef (TPE_ToddlerPlayDefOf, below)
using Verse;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Auto-resolved DefOf for all custom JobDefs added by this mod.
    /// </summary>
    [DefOf]
    public static class TPE_JobDefOf
    {
        public static JobDef TPE_ToddlerSpinning;
        public static JobDef TPE_ToddlerWorkWatching;
        public static JobDef TPE_ToddlerRunning;
        public static JobDef TPE_ToddlerJumping;
        public static JobDef TPE_ToddlerMonsterHunt;
        public static JobDef TPE_ToddlerDinosaur;
        public static JobDef TPE_ToddlerLookThroughStorage;
        public static JobDef TPE_ToddlerSelfSoothing;
        public static JobDef TPE_ToddlerCribJumping;
        public static JobDef TPE_ToddlerCeilingWatching;
        public static JobDef TPE_ToddlerSpeechPractice;
        public static JobDef TPE_ToddlerSpeechPracticeInCrib;
        public static JobDef TPE_ToddlerHeaterSnuggling;
        // SHELVED: Pillow Nesting
        // public static JobDef TPE_ToddlerPillowNesting;

        // Bubble Machine toddler play activities
        public static JobDef TPE_ToddlerPoppingBubbles;
        public static JobDef TPE_ToddlerChasingBubbles;
        public static JobDef TPE_PlayWithToy;
        public static JobDef TPE_CarryToy;
        public static JobDef TPE_BugwatchingWithGlass;
        public static JobDef TPE_SeekPlushToy;
        public static JobDef TPE_ReturnToddlerToCrib;
        public static JobDef TPE_GiveToyToLockedToddler;

        // Child play activity jobs
        public static JobDef TPE_ChildPlayWithToy;
        public static JobDef TPE_ChildReadBook;
        public static JobDef TPE_ChildFloorDrawing;
        public static JobDef TPE_ChildWatchTelevision;
        public static JobDef TPE_ChildBuildDirtCastles;
        public static JobDef TPE_ChildNatureRunning;
        public static JobDef TPE_ChildSkydreaming;
        public static JobDef TPE_ChildRadiotalking;
        public static JobDef TPE_ChildActivePlay;

        // Story Spinning learning activity (disabled for now)
        // public static JobDef TPE_StorySpinning;
    }

    /// <summary>
    /// Auto-resolved DefOf for custom AnimationDefs (e.g. toddler spinning).
    /// </summary>
    [DefOf]
    public static class TPE_AnimationDefOf
    {
        public static AnimationDef TPE_ToddlerSpinning;
        // SHELVED: Pillow Nesting
        // public static AnimationDef TPE_PillowNesting;

        // Child Active Play sub-activity animations
        public static AnimationDef TPE_ChildSpinning;
        public static AnimationDef TPE_ChildJumping;
        public static AnimationDef TPE_ChildSnailCrawling;
    }

    /// <summary>
    /// Auto-resolved DefOf for custom HediffDefs (running speed boost, crib confinement, etc.).
    /// </summary>
    [DefOf]
    public static class TPE_HediffDefOf
    {
        public static HediffDef TPE_Running;
        public static HediffDef TPE_DinosaurStomp;
        public static HediffDef TPE_CribConfinement;
        public static HediffDef TPE_CribLocked;
        public static HediffDef TPE_WantsOutOfCrib;
        public static HediffDef TPE_SnailCrawling;
    }

    /// <summary>
    /// Auto-resolved DefOf for custom MentalStateDefs (e.g. wants out of crib, child crying).
    /// </summary>
    [DefOf]
    public static class TPE_MentalStateDefOf
    {
        public static MentalStateDef TPE_WantsOutOfCrib;
        public static MentalStateDef TPE_ChildCrying;
        public static MentalStateDef TPE_ChildTantrum;
    }

    /// <summary>
    /// Auto-resolved DefOf for custom ThoughtDefs (plush sleep thoughts, monster hunt thoughts, etc.).
    /// </summary>
    [DefOf]
    public static class TPE_ThoughtDefOf
    {
        public static ThoughtDef TPE_FoundMonster;
        public static ThoughtDef TPE_NoMonsters;
        public static ThoughtDef TPE_SleptWithPlushToy;
        public static ThoughtDef TPE_NoSoftCuddleBuddy;
        public static ThoughtDef TPE_BedTooBig;
        public static ThoughtDef TPE_ChildSleptWithPlushToy;
        public static ThoughtDef TPE_ChildNoPlushToy;
        public static ThoughtDef TPE_AdultSleptWithPlushToy;
        public static ThoughtDef TPE_AdultNoPlushToy;
        public static ThoughtDef TPE_CribRestless;
        public static ThoughtDef TPE_CribFrustrated;
        public static ThoughtDef TPE_WantsOutOfCrib_Active;
        public static ThoughtDef TPE_WasLockedInCrib;
        public static ThoughtDef TPE_CribLocked_Active;

        // Event-triggered mood thoughts
        public static ThoughtDef TPE_Event_RainStarted;
        public static ThoughtDef TPE_Event_SnowStarted;
        public static ThoughtDef TPE_Event_Aurora;
        public static ThoughtDef TPE_Event_RaidWon;
        public static ThoughtDef TPE_Event_NewColonist;
        public static ThoughtDef TPE_Event_Wedding;
        public static ThoughtDef TPE_Event_ToddlerRescued;
        public static ThoughtDef TPE_Event_FireOnMap;
        public static ThoughtDef TPE_Event_RaidActive;
        public static ThoughtDef TPE_Event_ToxicFallout;
        public static ThoughtDef TPE_Event_HeatWave;
        public static ThoughtDef TPE_Event_ColdSnap;
        public static ThoughtDef TPE_Event_Eclipse;
        public static ThoughtDef TPE_Event_ColonistDied;
        public static ThoughtDef TPE_Event_Burial;
        public static ThoughtDef TPE_Event_ToddlerInjured;
        public static ThoughtDef TPE_Event_ScaryPersonNearby;
        public static ThoughtDef TPE_AdoptedToddlerHomesick;

        // Environmental events
        public static ThoughtDef TPE_Event_VolcanicWinter;
        // (TPE_Event_SolarFlare removed — covered by TPE_Event_PowerOutage; see Architecture.md §5.2.)
        public static ThoughtDef TPE_Event_ThunderstormFascinating;
        public static ThoughtDef TPE_Event_ThunderstormScary;
        public static ThoughtDef TPE_Event_Flashstorm;
        public static ThoughtDef TPE_Event_Blight;
        public static ThoughtDef TPE_Event_Tornado;
        public static ThoughtDef TPE_Event_DryThunderstorm;

        // Incident / threat events
        public static ThoughtDef TPE_Event_ManhunterPack;
        public static ThoughtDef TPE_Event_Infestation;
        public static ThoughtDef TPE_Event_MechanoidRaid;
        public static ThoughtDef TPE_Event_MadAnimal;
        public static ThoughtDef TPE_Event_PrisonBreak;

        // Social / colony events
        public static ThoughtDef TPE_Event_Party;
        public static ThoughtDef TPE_Event_Birthday;
        public static ThoughtDef TPE_Event_SocialFight;
        public static ThoughtDef TPE_Event_MentalBreak;
        public static ThoughtDef TPE_Event_AnimalTamed;

        // Positive events
        public static ThoughtDef TPE_Event_WandererJoins;
        public static ThoughtDef TPE_Event_ResourcePodCrash;
        public static ThoughtDef TPE_Event_MeteoriteImpact;
        public static ThoughtDef TPE_Event_ThrumbosPassing;
        public static ThoughtDef TPE_Event_VisitorsArrived;
        public static ThoughtDef TPE_Event_TraderCaravan;

        // Medical / hardship events
        public static ThoughtDef TPE_Event_DiseaseOutbreak;
        public static ThoughtDef TPE_Event_MedicalOperation;
        public static ThoughtDef TPE_Event_FoodShortage;
        public static ThoughtDef TPE_Event_PowerOutage;

        // Crying child thoughts
        public static ThoughtDef TPE_ChildCrying;
        public static ThoughtDef TPE_MyChildCrying;
        public static ThoughtDef TPE_OlderChildCrying;
        public static ThoughtDef TPE_MyOlderChildCrying;
        public static ThoughtDef TPE_ChildCryingIgnored;

        // Disturbing events
        public static ThoughtDef TPE_Event_CorpseObserved;
        public static ThoughtDef TPE_Event_ButcheringHumanlike;
        public static ThoughtDef TPE_Event_ObservedBlood;

        public static ThoughtDef TPE_ChildPlayHigh;
        /// <summary>
        /// Negative-mood counterpart to <see cref="TPE_ChildPlayHigh"/>. Referenced
        /// directly (rather than only via its ThoughtWorker) by
        /// <c>Patch_ThoughtUtility_NullifyingTrait_PlayMood</c> so the
        /// "keep play mood for psychopaths" setting can identify it.
        /// </summary>
        public static ThoughtDef TPE_ChildPlayEmpty;
        public static ThoughtDef TPE_SelfSoothingPositive;
        public static ThoughtDef TPE_SelfSoothingNegative;
        public static ThoughtDef TPE_PlayedInPlayground;
        public static ThoughtDef TPE_LearnedInClassroom;

        /// <summary>
        /// VANILLA Biotech ThoughtDef (defName "NeedPlay") — NOT one of ours.
        /// This is the direct baby-side counterpart to <see cref="TPE_ChildPlayHigh"/>
        /// / <see cref="TPE_ChildPlayEmpty"/>: its <c>ThoughtWorker_NeedPlay</c>
        /// worker maps <c>Need_Play.CurCategory</c> (the vanilla baby Play need
        /// Cyanobot's Toddlers mod feeds into) straight to mood stages ranging
        /// from "desperately wants to play" (-20) up to "very entertained" (+10).
        /// Vanilla ships it with <c>nullifyingTraits: Psychopath</c> already, so
        /// it's included here purely so
        /// <c>Patch_ThoughtUtility_NullifyingTrait_PlayMood</c> in
        /// HarmonyPatches.cs can recognise it and apply the "keep play mood for
        /// psychopaths" setting to it too. [DefOf] resolves fields by matching
        /// the field name to a defName in ANY loaded mod's DefDatabase, vanilla
        /// included, so no special lookup code is needed.
        /// </summary>
        [MayRequire("Ludeon.RimWorld.Biotech")]
        public static ThoughtDef NeedPlay;
    }

    /// <summary>
    /// Auto-resolved DefOf for custom SoundDefs (dinosaur roar, child crying).
    /// </summary>
    [DefOf]
    public static class TPE_SoundDefOf
    {
        public static SoundDef TPE_DinoRoar;
        public static SoundDef TPE_ChildCryingSound;
        public static SoundDef TPE_SpeechPracticeBabble;
    }

    /// <summary>
    /// Auto-resolved DefOf for custom ThingDefs (all plush toys and the toy sword).
    /// </summary>
    [DefOf]
    public static class TPE_ThingDefOf
    {
        public static ThingDef TPE_PlushTeddy;
        public static ThingDef TPE_PlushMuffalo;
        public static ThingDef TPE_PlushSheep;
        public static ThingDef TPE_PlushDino;
        public static ThingDef TPE_PlushMonster;
        /// <summary>
        /// Plush fox toy — a clever orange fox with white markings.
        /// </summary>
        public static ThingDef TPE_PlushFox;
        public static ThingDef TPE_ToySword;
        /// <summary>
        /// Magnifying glass toy — used for bug watching activities.
        /// </summary>
        public static ThingDef TPE_MagnifyingGlass;
        public static ThingDef TPE_DirtCastle;
        /// <summary>
        /// The Bubble Machine building. Referenced by <see cref="BubbleMoodUtility"/> to find
        /// every machine on a pawn's map without a string-based DefDatabase lookup.
        /// </summary>
        public static ThingDef TPE_BubbleMachine;
    }

    /// <summary>
    /// Auto-resolved DefOf for the child play need.
    /// </summary>
    [DefOf]
    public static class TPE_ChildPlayDefOf
    {
        public static NeedDef TPE_ChildPlay;
    }

    /// <summary>
    /// Auto-resolved DefOf for the Learning time assignment.
    /// </summary>
    [DefOf]
    public static class TPE_TimeAssignmentDefOf
    {
        public static TimeAssignmentDef TPE_Learning;
    }

    /// <summary>
    /// Auto-resolved DefOf for the Playground room role.
    /// </summary>
    [DefOf]
    public static class TPE_RoomRoleDefOf
    {
        public static RoomRoleDef TPE_Playground;
    }

    /// <summary>
    /// Auto-resolved DefOf for all play desire defs.
    /// </summary>
    [DefOf]
    public static class TPE_PlayDesireDefOf
    {
        public static PlayDesireDef TPE_PlayWithToy;
        public static PlayDesireDef TPE_ReadBook;
        public static PlayDesireDef TPE_FloorDrawing;
        public static PlayDesireDef TPE_WatchTelevision;
        public static PlayDesireDef TPE_BuildDirtCastles;
        public static PlayDesireDef TPE_NatureRunning;
        public static PlayDesireDef TPE_Skydreaming;
        public static PlayDesireDef TPE_Radiotalking;
        public static PlayDesireDef TPE_ActivePlay;
        /// <summary>
        /// Age-gated (3–9) desire: run around an active Bubble Machine's zone, pausing
        /// occasionally to "pop" a bubble. See <see cref="PlayDesireGiver_BubblePopping"/>
        /// and <see cref="JobDriver_ChildBubblePopping"/>.
        /// </summary>
        public static PlayDesireDef TPE_BubblePopping;
    }

    /// <summary>Bound once for the shared crib-play chooser; field names match defName.</summary>
    [DefOf]
    public static class TPE_ToddlerPlayDefOf
    {
        public static ToddlerPlayDef TPE_ToddlerSelfSoothing;
        public static ToddlerPlayDef TPE_ToddlerCribJumping;
        public static ToddlerPlayDef TPE_ToddlerCeilingWatching;
        public static ToddlerPlayDef TPE_ToddlerSpeechPracticeInCrib;
    }
}

