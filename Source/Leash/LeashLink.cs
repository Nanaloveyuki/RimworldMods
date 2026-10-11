using Verse;
using System.Collections.Generic;

namespace LeadYourPet
{
    public class LeashLink : IExposable
    {
        public Pawn Master;
        public Pawn Pet;
        public int CreatedTick;
        public int NextAutoInteractionTick;
        public int LastMoodRefreshTick = -99999;
        public int LastObserveMemoryTick = -99999;
        public bool IsDragging;
        public LeashLinkKind Kind = LeashLinkKind.NormalPet;
        public LeashAnchorMode AnchorMode = LeashAnchorMode.None;
        public Thing AnchorThing;
        public IntVec3 AnchorCell = IntVec3.Invalid;
        public bool MasterSleepSuspended;
        public int UpdateBucket;
        public int LastExpensiveUpdateTick = -99999;
        public int LastFollowIssueTick = -99999;
        internal int LastFeedAttemptTick = -99999;
        public int LastAutoInteractionPoolTick = -99999;
        public bool ForceImmediateUpdate;
        public bool CachedCanUseSingleDirectionInteractions;
        public bool CachedPlayerControlled;
        public bool CachedMasterSleeping;
        public bool CachedHasValidAutoInteractionPool;
        public List<LeadYourPetInteractionKind> CachedAutoInteractionPool;
        public LeashedMouseEggDutyState MouseEggDutyState;

        public void ExposeData()
        {
            Scribe_References.Look(ref Master, "master");
            Scribe_References.Look(ref Pet, "pet");
            Scribe_References.Look(ref AnchorThing, "anchorThing");
            Scribe_Values.Look(ref CreatedTick, "createdTick");
            Scribe_Values.Look(ref NextAutoInteractionTick, "nextAutoInteractionTick");
            Scribe_Values.Look(ref LastMoodRefreshTick, "lastMoodRefreshTick", -99999);
            Scribe_Values.Look(ref LastObserveMemoryTick, "lastObserveMemoryTick", -99999);
            Scribe_Values.Look(ref IsDragging, "isDragging");
            Scribe_Values.Look(ref Kind, "kind", LeashLinkKind.NormalPet);
            Scribe_Values.Look(ref AnchorMode, "anchorMode", LeashAnchorMode.None);
            Scribe_Values.Look(ref AnchorCell, "anchorCell", IntVec3.Invalid);
            Scribe_Values.Look(ref MasterSleepSuspended, "masterSleepSuspended", false);
            Scribe_Values.Look(ref UpdateBucket, "updateBucket", 0);
            Scribe_Values.Look(ref LastExpensiveUpdateTick, "lastExpensiveUpdateTick", -99999);
            Scribe_Values.Look(ref LastFollowIssueTick, "lastFollowIssueTick", -99999);
            Scribe_Values.Look(ref LastAutoInteractionPoolTick, "lastAutoInteractionPoolTick", -99999);
            Scribe_Values.Look(ref ForceImmediateUpdate, "forceImmediateUpdate", false);
            Scribe_Values.Look(ref CachedCanUseSingleDirectionInteractions, "cachedCanUseSingleDirectionInteractions", false);
            Scribe_Values.Look(ref CachedPlayerControlled, "cachedPlayerControlled", false);
            Scribe_Values.Look(ref CachedMasterSleeping, "cachedMasterSleeping", false);
            Scribe_Values.Look(ref CachedHasValidAutoInteractionPool, "cachedHasValidAutoInteractionPool", false);
            Scribe_Values.Look(ref MouseEggDutyState, "mouseEggDutyState", LeashedMouseEggDutyState.FollowMaster);
            Scribe_Collections.Look(ref CachedAutoInteractionPool, "cachedAutoInteractionPool", LookMode.Value);
        }
    }
}
